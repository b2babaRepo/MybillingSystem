using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Customers, khata entries, bills and statements for the current shop. Rules:
/// entries are append-only (fix with a reversal); a customer's balance changes only in the same
/// transaction as the entry that moves it; every write carries the app's own ID, so a retry is a no-op.
/// </summary>
public sealed partial class LedgerService(AppDbContext db, TimeProvider clock, ITenantContext tenant, IHttpContextAccessor? http = null,
    Microsoft.Extensions.Options.IOptions<Identity.OtpOptions>? otpOptions = null)
{
    public async Task<IReadOnlyList<CustomerDto>> ListCustomersAsync(string? search, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern) || (c.Phone != null && c.Phone.Contains(search.Trim())));
        }

        return await query.OrderBy(c => c.Name).Take(500)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit, c.NameLater, c.CreditDays))
            .ToListAsync(cancellationToken);
    }

    public Task<LedgerResult<CustomerDto>> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken) =>
        WriteOnceAsync(
            () => db.Customers.AsNoTracking().Where(c => c.Id == request.Id)
                .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit, c.NameLater, c.CreditDays)).SingleOrDefaultAsync(cancellationToken),
            async () =>
            {
                var errors = new Dictionary<string, string[]>();
                RequireId(request.Id, errors);
                if (request.NameLater && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim() == "Name later"))
                {
                    // Numbered, so two of them on one day can be told apart.
                    request = request with { Name = $"Name later {await db.Customers.CountAsync(c => c.NameLater, cancellationToken) + 1}" };
                }

                if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
                {
                    errors[nameof(request.Name)] = ["Enter the customer's name."];
                }

                var phone = PhoneNumber.Normalize(request.Phone);
                if (!string.IsNullOrWhiteSpace(request.Phone) && phone is null)
                {
                    errors[nameof(request.Phone)] = ["Enter a valid mobile number, or leave it empty."];
                }

                if (errors.Count > 0)
                {
                    return LedgerResult<CustomerDto>.Invalid(errors);
                }

                var customer = new Customer { Id = request.Id, Name = request.Name!.Trim(), Phone = phone, NameLater = request.NameLater };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
                return LedgerResult<CustomerDto>.Created(new CustomerDto(customer.Id, customer.Name, customer.Phone, 0, null, customer.NameLater));
            });

    /// <summary>How far back a helper may date an entry or bill; older needs the owner.</summary>
    public static readonly TimeSpan HelperBackdateLimit = TimeSpan.FromDays(7);

    /// <summary>An entry recorded this long after the date it carries is marked "entered later" on the khata.</summary>
    public static readonly TimeSpan LateAfter = TimeSpan.FromHours(24);

    public Task<LedgerResult<KhataEntryResponse>> AddKhataEntryAsync(
        KhataEntryRequest request, Guid userId, CancellationToken cancellationToken, bool isOwner = true, Guid? fromReceiptId = null) =>
        WriteOnceAsync(
            () => FindEntryResponseAsync(request.Id, cancellationToken),
            async () =>
            {
                var errors = new Dictionary<string, string[]>();
                RequireId(request.Id, errors);
                if (request.Type is null)
                {
                    errors[nameof(request.Type)] = ["Choose credit (udhaar) or payment (jama)."];
                }

                if (!Money.IsValidAmount(request.Amount))
                {
                    errors[nameof(request.Amount)] = ["Enter an amount above ₹0 with at most two decimals."];
                }

                if (request.Note?.Length > 200)
                {
                    errors[nameof(request.Note)] = ["Keep the note under 200 characters."];
                }

                CheckBackdate(request.OccurredAt, isOwner, errors);
                // Customers pay by cash or UPI for now: "bank" is for suppliers, and the customer reports and the Tally
                // file only know cash and UPI, so a bank receipt would go missing from them.
                if (request.PaymentMode == PaymentMode.Bank)
                {
                    errors[nameof(request.PaymentMode)] = ["Choose cash, UPI, card or cheque for a customer's payment."];
                }

                // A cheque: its number (and, if known, bank and date) so the owner can match it with the bank later.
                var isCheque = request.PaymentMode == PaymentMode.Cheque;
                var chequeNo = request.ChequeNo?.Trim();
                if (request.Type == KhataEntryType.Credit && tenant.IsBlocked(Identity.StaffLimits.Udhaar))
                {
                    errors[nameof(request.Type)] = [Identity.StaffLimits.Message(Identity.StaffLimits.Udhaar)];
                }

                if (isCheque && request.Type != KhataEntryType.Payment)
                {
                    errors[nameof(request.PaymentMode)] = ["A cheque is a payment (jama)."];
                }
                else if (isCheque && (chequeNo is null || chequeNo.Length is < 4 or > 12 || !chequeNo.All(char.IsAsciiDigit)))
                {
                    errors[nameof(request.ChequeNo)] = ["Enter the cheque number (the 6 digits at the bottom left)."];
                }
                else if (!isCheque && (!string.IsNullOrWhiteSpace(request.ChequeNo) || !string.IsNullOrWhiteSpace(request.ChequeBank) || request.ChequeDate is not null))
                {
                    errors[nameof(request.ChequeNo)] = ["Cheque details go only with a cheque payment."];
                }
                else if (request.ChequeBank?.Trim().Length > 40)
                {
                    errors[nameof(request.ChequeBank)] = ["Keep the bank name under 40 characters."];
                }

                var upiRef = string.IsNullOrWhiteSpace(request.UpiRef) ? null : request.UpiRef.Trim().ToUpperInvariant();
                if (upiRef is not null && (request.Type != KhataEntryType.Payment || request.PaymentMode != PaymentMode.Upi))
                {
                    errors[nameof(request.UpiRef)] = ["A UPI reference goes only with a UPI payment."];
                }
                else if (upiRef is not null && (upiRef.Length is < 6 or > 30 || !upiRef.All(char.IsAsciiLetterOrDigit)))
                {
                    errors[nameof(request.UpiRef)] = ["The UPI reference is 6 to 30 letters and digits (the UTR in the payment app)."];
                }

                if (errors.Count > 0)
                {
                    return LedgerResult<KhataEntryResponse>.Invalid(errors);
                }

                if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
                {
                    return LedgerResult<KhataEntryResponse>.Invalid(nameof(request.CustomerId), "Customer not found.");
                }

                var isPayment = request.Type == KhataEntryType.Payment;
                var entry = new LedgerEntry
                {
                    Id = request.Id,
                    CustomerId = request.CustomerId,
                    Kind = isPayment ? LedgerEntryKind.Payment : LedgerEntryKind.Credit,
                    Amount = isPayment ? -request.Amount : request.Amount,
                    PaymentMode = isPayment ? request.PaymentMode ?? PaymentMode.Cash : null,
                    Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    UpiRef = upiRef,
                    ChequeNo = isCheque ? chequeNo : null,
                    ChequeBank = isCheque && !string.IsNullOrWhiteSpace(request.ChequeBank) ? request.ChequeBank.Trim() : null,
                    ChequeDate = isCheque ? request.ChequeDate : null,
                    ChequeStatus = isCheque ? ChequeStatus.Pending : null,
                    OccurredAt = OccurredAt(request.OccurredAt),
                    CreatedByUserId = userId,
                };

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var receipt = await ReceiptForAsync(fromReceiptId, upiRef, request.Amount, userId, cancellationToken);
                if (receipt.Problem is { } problem)
                {
                    return problem;
                }

                entry.UpiRef = receipt.Receipt?.Utr ?? upiRef;
                // Matched later, but received then: the day's books must agree with the bank.
                if (fromReceiptId is not null)
                {
                    entry.OccurredAt = receipt.Receipt!.ReceivedAt;
                }

                db.LedgerEntries.Add(entry);
                if (receipt.Receipt is { } proof)
                {
                    db.ReceiptAllocations.Add(new ReceiptAllocation { ReceiptId = proof.Id, CustomerId = entry.CustomerId, LedgerEntryId = entry.Id, Amount = request.Amount, UserId = userId });
                }

                await db.SaveChangesAsync(cancellationToken);
                var balance = await AdjustBalanceAsync(entry.CustomerId, entry.Amount, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return LedgerResult<KhataEntryResponse>.Created(new KhataEntryResponse(LedgerEntryDto.From(entry), balance));
            });

    /// <summary>
    /// The proof this payment hangs on, locked for the rest of the transaction so two matches can't both take the same
    /// money: the receipt being matched, or the one for a typed UTR, made if it's the first time this UTR is seen.
    /// </summary>
    private async Task<(IncomingReceipt? Receipt, LedgerResult<KhataEntryResponse>? Problem)> ReceiptForAsync(
        Guid? fromReceiptId, string? upiRef, decimal amount, Guid userId, CancellationToken cancellationToken)
    {
        const string twice = "This UPI reference is already recorded. The same payment can't be entered twice.";
        if (fromReceiptId is null && upiRef is null)
        {
            return (null, null);
        }

        var locked = await (fromReceiptId is { } receiptId
            ? db.IncomingReceipts.FromSql($"SELECT * FROM incoming_receipts WHERE id = {receiptId} FOR UPDATE")
            : db.IncomingReceipts.FromSql($"SELECT * FROM incoming_receipts WHERE utr = {upiRef} FOR UPDATE")).SingleOrDefaultAsync(cancellationToken);
        if (locked is null)
        {
            if (fromReceiptId is not null)
            {
                return (null, LedgerResult<KhataEntryResponse>.Missing());
            }

            var typed = new IncomingReceipt { Source = ReceiptSource.Manual, Amount = amount, Utr = upiRef, ReceivedAt = clock.GetUtcNow(), CreatedByUserId = userId };
            db.IncomingReceipts.Add(typed);
            return (typed, null);
        }

        if (locked.Disposition is not null)
        {
            return (null, LedgerResult<KhataEntryResponse>.Conflicted("This money is marked as not khata. Put it back in the tray first."));
        }

        var matched = await Receipts.MatchedAsync(db, locked.Id, cancellationToken);
        if (fromReceiptId is null && matched > 0)
        {
            return (null, LedgerResult<KhataEntryResponse>.Conflicted(twice));
        }

        // A hand-typed amount is only the shop's word: once freed, retyping the same UTR may put a wrong figure right.
        if (fromReceiptId is null && locked.Source == ReceiptSource.Manual)
        {
            locked.Amount = amount;
        }
        else if (matched + amount > locked.Amount)
        {
            return (null, LedgerResult<KhataEntryResponse>.Conflicted(fromReceiptId is null ? twice : "That's more than is left of this payment."));
        }

        return (locked, null);
    }

    /// <summary>
    /// How long anyone may remove an entry, to fix a slip. After that only the owner can, and must say why: a
    /// payment can't quietly disappear days later. The removal itself is a new entry, visible to both sides.
    /// </summary>
    public static readonly TimeSpan RemovalWindow = TimeSpan.FromMinutes(15);

    public Task<LedgerResult<KhataEntryResponse>> ReverseAsync(
        Guid entryId, ReverseRequest request, Guid userId, bool isOwner, CancellationToken cancellationToken) =>
        WriteOnceAsync(
            () => FindEntryResponseAsync(request.Id, cancellationToken),
            async () =>
            {
                var errors = new Dictionary<string, string[]>();
                RequireId(request.Id, errors);
                if (errors.Count > 0)
                {
                    return LedgerResult<KhataEntryResponse>.Invalid(errors);
                }

                var original = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId, cancellationToken);
                if (original is null)
                {
                    return LedgerResult<KhataEntryResponse>.Missing();
                }

                if (original.Kind == LedgerEntryKind.Reversal || original.ReversesEntryId is not null)
                {
                    return LedgerResult<KhataEntryResponse>.Conflicted("A reversal can't be reversed. Add a new entry instead.");
                }

                if (await db.LedgerEntries.AnyAsync(e => e.ReversesEntryId == entryId, cancellationToken))
                {
                    return LedgerResult<KhataEntryResponse>.Conflicted("This entry is already reversed.");
                }

                // On its own this would leave the sale, its stock and its tax standing with nothing owed.
                if (original.BillId is not null)
                {
                    return LedgerResult<KhataEntryResponse>.Conflicted("This line belongs to a bill. Cancel the bill, or take the goods back on a credit note.");
                }

                // The bank's proof stays either way: this only says the money isn't this customer's, so say why.
                if (original.UpiRef is not null && request.Note?.Trim() is not { Length: >= 3 and <= 200 })
                {
                    return LedgerResult<KhataEntryResponse>.Invalid(nameof(request.Note), "Say why this payment is being removed. The customer will see it.");
                }

                if (clock.GetUtcNow() - original.CreatedAt > RemovalWindow)
                {
                    if (!isOwner)
                    {
                        return LedgerResult<KhataEntryResponse>.Invalid("Id", "After 15 minutes only the shop owner can remove an entry.");
                    }

                    if (request.Note is not { Length: >= 3 } reason || reason.Trim().Length < 3 || reason.Length > 200)
                    {
                        return LedgerResult<KhataEntryResponse>.Invalid(nameof(request.Note), "Say why this entry is being removed. The customer will see it.");
                    }
                }

                var reversal = new LedgerEntry
                {
                    Id = request.Id,
                    CustomerId = original.CustomerId,
                    Kind = original.Kind == LedgerEntryKind.Opening ? LedgerEntryKind.Opening : LedgerEntryKind.Reversal,
                    Amount = -original.Amount,
                    PaymentMode = original.PaymentMode,
                    ChequeNo = original.ChequeNo,
                    // Carries the cheque's status, so a pending cheque and its removal both stay out of money received.
                    ChequeStatus = original.ChequeStatus,
                    BillId = original.BillId,
                    ReversesEntryId = original.Id,
                    Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    OccurredAt = clock.GetUtcNow(),
                    CreatedByUserId = userId,
                };

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                db.LedgerEntries.Add(reversal);
                await db.SaveChangesAsync(cancellationToken);
                var balance = await AdjustBalanceAsync(reversal.CustomerId, reversal.Amount, cancellationToken);
                // Money coming off a khata needs a new home, even if the rest of that receipt was set aside.
                await db.IncomingReceipts
                    .Where(r => r.Disposition != null && db.ReceiptAllocations.Any(a => a.ReceiptId == r.Id && a.LedgerEntryId == entryId))
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Disposition, (ReceiptDisposition?)null).SetProperty(r => r.DispositionByUserId, (Guid?)null), cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return LedgerResult<KhataEntryResponse>.Created(new KhataEntryResponse(LedgerEntryDto.From(reversal), balance));
            });

    /// <summary>
    /// The customer's khata. With a month ("2026-10"), only that month's entries, starting from the balance
    /// carried forward from before it (the month-end statement); without, everything.
    /// </summary>
    public async Task<StatementDto?> GetStatementAsync(Guid customerId, (int Year, int Month)? month, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit))
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var query = db.LedgerEntries.AsNoTracking().Where(e => e.CustomerId == customerId);
        var opening = 0m;
        if (month is { } m)
        {
            var (start, end) = IndiaTime.Month(m.Year, m.Month);
            opening = await query.Where(e => e.OccurredAt < start).SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
            query = query.Where(e => e.OccurredAt >= start && e.OccurredAt < end);
        }

        var entries = await query.OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync(cancellationToken);
        // A reversal may fall in a later month than the entry it cancels: look across the whole khata.
        var reversed = (await db.LedgerEntries.AsNoTracking()
                .Where(e => e.CustomerId == customerId && e.ReversesEntryId != null)
                .Select(e => e.ReversesEntryId!.Value).ToListAsync(cancellationToken))
            .ToHashSet();

        var billIds = entries.Where(e => e.BillId != null).Select(e => e.BillId!.Value).Distinct().ToList();
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines).Where(b => billIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new BillSummary(b.Total, b.PaidAmount,
                b.Lines.OrderBy(l => l.LineNo).Select(l => new BillItem(l.Id, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount)).ToList(), b.Site, Bill.No(b.Prefix, b.FinancialYear, b.Number)), cancellationToken);

        // Who wrote each line: a removal shows the name of the person who removed it.
        var userIds = entries.Select(e => e.CreatedByUserId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var entryIds = entries.Select(e => e.Id).ToList();
        // By customer (indexed), then narrowed to these entries here.
        var shown = entryIds.ToHashSet();
        var latest = (await db.Disputes.AsNoTracking().Where(d => d.CustomerId == customerId).OrderBy(d => d.RaisedAt).ToListAsync(cancellationToken))
            .Where(d => shown.Contains(d.EntryId))
            .GroupBy(d => d.EntryId).Select(g => g.Last()).ToList();
        var disputes = (await DisputeEndpoints.ViewsAsync(db, latest, cancellationToken)).ToDictionary(v => latest.Single(d => d.Id == v.Id).EntryId);

        var running = opening;
        var lines = entries.Select(e =>
        {
            running += e.Amount;
            return new StatementLine(e.Id, e.Kind, e.Amount, e.PaymentMode, e.BillId, e.ReversesEntryId, e.Note, e.OccurredAt,
                running, reversed.Contains(e.Id), e.BillId is { } billId && e.Kind != LedgerEntryKind.Return ? bills.GetValueOrDefault(billId) : null, names.GetValueOrDefault(e.CreatedByUserId), e.UpiRef, disputes.GetValueOrDefault(e.Id),
                e.CreatedAt - e.OccurredAt > LateAfter, e.CreatedAt, e.ChequeNo, e.ChequeStatus);
        }).ToList();

        return new StatementDto(customer, lines, opening, running,
            month is { } chosen ? $"{chosen.Year:D4}-{chosen.Month:D2}" : null, await BalanceConfirmations.LatestAsync(db, customerId, cancellationToken));
    }

    /// <summary>Customers whose stored balance differs from the sum of their entries. Should always be empty.</summary>
    public async Task<IReadOnlyList<BalanceMismatch>> ReconcileAsync(CancellationToken cancellationToken) =>
        await (from c in db.Customers.AsNoTracking()
               let sum = db.LedgerEntries.Where(e => e.CustomerId == c.Id).Sum(e => (decimal?)e.Amount) ?? 0m
               where c.Balance != sum
               select new BalanceMismatch(c.Id, c.Name, c.Balance, sum))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns what was already saved under this ID, or runs <paramref name="write"/>. Two requests with the same
    /// ID racing each other end with one row: the loser hits the unique key and replays the winner's result.
    /// </summary>
    private async Task<LedgerResult<T>> WriteOnceAsync<T>(Func<Task<T?>> findExisting, Func<Task<LedgerResult<T>>> write)
        where T : class
    {
        if (await findExisting() is { } existing)
        {
            return LedgerResult<T>.Replay(existing);
        }

        try
        {
            return await write();
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            db.ChangeTracker.Clear();
            return await findExisting() is { } saved
                ? LedgerResult<T>.Replay(saved)
                : LedgerResult<T>.Conflicted("This ID is already in use.");
        }
    }

    private async Task<KhataEntryResponse?> FindEntryResponseAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entry is null ? null : new KhataEntryResponse(LedgerEntryDto.From(entry), await BalanceAsync(entry.CustomerId, cancellationToken));
    }

    private Task<decimal> BalanceAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Balance).SingleAsync(cancellationToken);

    /// <summary>
    /// Atomic in the database (balance = balance + delta), so two tills posting at once can't lose an update. Returns the
    /// new balance from the same statement, saving a read while the row is locked.
    /// </summary>
    /// <summary>
    /// Settles a customer's pending cheque: cleared, it now counts as money received; bounced, its amount goes back on
    /// the khata as a reversal the customer sees, with the reason. Safe to repeat: a settled cheque comes back as it is.
    /// </summary>
    public async Task<LedgerResult<KhataEntryResponse>> SettleChequeAsync(Guid entryId, bool cleared, string? reason, Guid userId, CancellationToken cancellationToken)
    {
        reason = reason?.Trim();
        if (!cleared && reason is not { Length: >= 3 and <= 150 })
        {
            return LedgerResult<KhataEntryResponse>.Invalid("Reason", "Say why the cheque bounced (as the bank wrote it). The customer will see it.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entry = await db.LedgerEntries.FromSql($"SELECT * FROM ledger_entries WHERE id = {entryId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (entry is null || entry.PaymentMode != PaymentMode.Cheque || entry.ReversesEntryId is not null)
        {
            return LedgerResult<KhataEntryResponse>.Missing();
        }

        var balance = await db.Customers.Where(c => c.Id == entry.CustomerId).Select(c => c.Balance).SingleAsync(cancellationToken);
        if (entry.ChequeStatus != ChequeStatus.Pending)
        {
            var wanted = cleared ? ChequeStatus.Cleared : ChequeStatus.Bounced;
            return entry.ChequeStatus == wanted
                ? LedgerResult<KhataEntryResponse>.Replay(new KhataEntryResponse(LedgerEntryDto.From(entry), balance))
                : LedgerResult<KhataEntryResponse>.Conflicted(entry.ChequeStatus == ChequeStatus.Cleared ? "This cheque is already marked cleared." : "This cheque is already marked bounced.");
        }

        if (await db.LedgerEntries.AnyAsync(e => e.ReversesEntryId == entryId, cancellationToken))
        {
            return LedgerResult<KhataEntryResponse>.Conflicted("This cheque payment was removed from the khata.");
        }

        var now = clock.GetUtcNow();
        entry.ChequeStatus = cleared ? ChequeStatus.Cleared : ChequeStatus.Bounced;
        entry.ChequeSettledAt = now;
        if (!cleared)
        {
            var bounce = new LedgerEntry
            {
                CustomerId = entry.CustomerId, Kind = LedgerEntryKind.Reversal, Amount = -entry.Amount, PaymentMode = PaymentMode.Cheque,
                ChequeNo = entry.ChequeNo, ChequeStatus = ChequeStatus.Bounced, ReversesEntryId = entry.Id,
                Note = $"Cheque {entry.ChequeNo} bounced: {reason}", OccurredAt = now, CreatedByUserId = userId,
            };
            db.LedgerEntries.Add(bounce);
            await db.SaveChangesAsync(cancellationToken);
            balance = await AdjustBalanceAsync(entry.CustomerId, bounce.Amount, cancellationToken);
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return LedgerResult<KhataEntryResponse>.Created(new KhataEntryResponse(LedgerEntryDto.From(entry), balance));
    }

    /// <summary>The bill total at which GST needs the buyer named, so a "name later" khata can't be used.</summary>
    public const decimal NameLaterBillLimit = 50_000m;

    private async Task<decimal> AdjustBalanceAsync(Guid customerId, decimal delta, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<decimal>($"UPDATE customers SET balance = balance + {delta} WHERE id = {customerId} RETURNING balance AS \"Value\"")
            .ToListAsync(cancellationToken)).Single();

    /// <summary>India's financial year, April to March: 30 Oct 2026 → "2026-27".</summary>
    public static string FinancialYear(DateTimeOffset at)
    {
        var local = at.ToOffset(IndiaTime.Offset);
        var start = local.Month >= 4 ? local.Year : local.Year - 1;
        return $"{start}-{(start + 1) % 100:D2}";
    }

    /// <summary>What's wrong with a helper's price for this item, or null when it's inside the shop's discount limit.</summary>
    public static string? DiscountProblem(string name, Product? product, decimal price, decimal maxDiscount) =>
        product is { SellingPrice: > 0 } regular && price < regular.SellingPrice && (regular.SellingPrice - price) / regular.SellingPrice * 100 > maxDiscount
            ? $"{name}: helpers can take off at most {maxDiscount:0.##}% (₹{regular.SellingPrice * (1 - maxDiscount / 100):0.##} or more). Ask the owner."
            : null;

    /// <summary>A helper can't date an entry more than a week back (room for a phone offline for days): older entries need the owner.</summary>
    private void CheckBackdate(DateTimeOffset? requested, bool isOwner, Dictionary<string, string[]> errors)
    {
        if (!isOwner && Corrected(requested) is { } at && at < clock.GetUtcNow() - HelperBackdateLimit)
        {
            errors["OccurredAt"] = ["Only the owner can enter something older than 7 days. Ask the owner to add it."];
        }
    }

    /// <summary>The phone's time, put right for a wrong phone clock, and never in the future.</summary>
    private DateTimeOffset OccurredAt(DateTimeOffset? requested)
    {
        var now = clock.GetUtcNow();
        return Corrected(requested) is { } at && at <= now ? at.ToUniversalTime() : now;
    }

    /// <summary>Phone clocks this far off the server's are put right; closer than this is ordinary drift.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The app sends the phone's clock with every request (X-Client-Time). When it is wrong (a phone set to last month),
    /// the entry's time moves by the same error, so a sale lands on the day it really happened, in the right GST month.
    /// An entry queued offline keeps its place: the error is measured when it is sent, from the same clock.
    /// </summary>
    private DateTimeOffset? Corrected(DateTimeOffset? requested)
    {
        if (requested is not { } at
            || http?.HttpContext?.Request.Headers["X-Client-Time"].ToString() is not { Length: > 0 } header
            || !DateTimeOffset.TryParse(header, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var phoneNow))
        {
            return requested;
        }

        var error = clock.GetUtcNow() - phoneNow;
        return error.Duration() > ClockTolerance ? at + error : at;
    }

    private static void RequireId(Guid id, Dictionary<string, string[]> errors)
    {
        if (id == Guid.Empty)
        {
            errors["Id"] = ["The app must send an ID for every new record."];
        }
    }
}
