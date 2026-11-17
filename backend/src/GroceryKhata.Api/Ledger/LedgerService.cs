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
public sealed class LedgerService(AppDbContext db, TimeProvider clock, ITenantContext tenant, IHttpContextAccessor? http = null)
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
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit, c.NameLater))
            .ToListAsync(cancellationToken);
    }

    public Task<LedgerResult<CustomerDto>> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken) =>
        WriteOnceAsync(
            () => db.Customers.AsNoTracking().Where(c => c.Id == request.Id)
                .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit, c.NameLater)).SingleOrDefaultAsync(cancellationToken),
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
    /// Saves a cash memo. With a customer, the bill total posts to their khata as a credit and anything
    /// paid now posts as a payment, so the statement shows both; without one it must be paid in full.
    /// </summary>
    /// <param name="stockAlreadyOut">True for an invoice made from delivery challans: the goods left the shelf with the challans.</param>
    /// <param name="taxSnapshot">GST and HSN per line, kept from the challans; without it each line takes the item's current ones.</param>
    /// <param name="beforeCommit">Runs in the bill's transaction, last: a message from it undoes the bill.</param>
    public Task<LedgerResult<BillDto>> CreateBillAsync(
        BillRequest request, Guid userId, CancellationToken cancellationToken, bool stockAlreadyOut = false, bool isOwner = true,
        IReadOnlyList<(decimal? GstRate, string? HsnCode)>? taxSnapshot = null, Func<Task<string?>>? beforeCommit = null) =>
        WriteOnceAsync(
            () => FindBillAsync(request.Id, cancellationToken),
            async () =>
            {
                var errors = new Dictionary<string, string[]>();
                RequireId(request.Id, errors);
                // Salespeople must have access to this shop; a line without one (or with a stranger) is the biller's.
                var sellers = (await db.Memberships.AsNoTracking().Where(m => m.ShopId == tenant.ShopId).Select(m => m.UserId).ToListAsync(cancellationToken)).ToHashSet();
                var staffMaxDiscount = isOwner ? 100m : await db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.StaffMaxDiscountPercent).SingleAsync(cancellationToken);
                if (request.Lines is not { Count: > 0 and <= 200 })
                {
                    errors[nameof(request.Lines)] = ["Add between 1 and 200 items."];
                    return LedgerResult<BillDto>.Invalid(errors);
                }

                var productIds = request.Lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
                var products = await db.Products.AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                // Planned offers named on the lines: one the bill's moment falls in, on that item, lets its price stand.
                var billAt = OccurredAt(request.OccurredAt);
                var offerIds = request.Lines.Where(l => l.OfferId is not null).Select(l => l.OfferId!.Value).Distinct().ToList();
                var offers = offerIds.Count == 0 ? [] : await db.SaleOffers.AsNoTracking().Where(o => offerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);

                var lines = new List<BillLine>();
                for (var i = 0; i < request.Lines.Count; i++)
                {
                    var line = request.Lines[i];
                    var field = $"Lines[{i}]";
                    var product = line.ProductId is { } productId ? products.GetValueOrDefault(productId) : null;
                    if (line.ProductId is not null && product is null)
                    {
                        errors[field] = ["Product not found."];
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(line.Name) ? product?.Name : line.Name.Trim();
                    var offer = line.OfferId is { } offerId && product is not null && offers.GetValueOrDefault(offerId) is { } named && SaleOffers.Covers(named, product, billAt) ? named : null;
                    var offerPrice = offer is null ? (decimal?)null : SaleOffers.PriceFor(offer, product!.SellingPrice);
                    if (string.IsNullOrEmpty(name) || name.Length > 120)
                    {
                        errors[field] = ["Each item needs a name."];
                    }
                    else if (!Money.IsValidQuantity(line.Quantity))
                    {
                        errors[field] = ["Quantity must be above 0, with at most three decimals."];
                    }
                    else if (line.UnitPrice < 0 || line.UnitPrice > Money.MaxAmount || decimal.Round(line.UnitPrice, 2) != line.UnitPrice)
                    {
                        errors[field] = ["Enter a valid price."];
                    }
                    else if (product?.Mrp is { } mrp && mrp > 0 && line.UnitPrice > mrp)
                    {
                        // Legal Metrology: nothing may be sold above the MRP printed on it.
                        errors[field] = [$"{name} has MRP ₹{mrp:0.##}: the price can't be above it."];
                    }
                    // The owner set the offer's price before billing, so a helper may sell at it, past their own limit.
                    else if (!isOwner && !(offerPrice is { } floor && line.UnitPrice >= floor) && DiscountProblem(name, product, line.UnitPrice, staffMaxDiscount) is { } tooLow)
                    {
                        errors[field] = [tooLow];
                    }
                    else
                    {
                        lines.Add(new BillLine
                        {
                            LineNo = i + 1,
                            ProductId = line.ProductId,
                            Name = name,
                            Quantity = line.Quantity,
                            Unit = line.Unit ?? product?.Unit ?? Catalogue.Unit.Piece,
                            UnitPrice = line.UnitPrice,
                            // Sold below the item's regular price: keep that price, so every discount is on record.
                            ListPrice = product is { SellingPrice: > 0 } p && line.UnitPrice < p.SellingPrice ? p.SellingPrice : null,
                            PrintName = Catalogue.BillNames.For(product),
                            OfferId = offer?.Id,
                            OfferName = offer?.Name,
                            SalespersonId = line.SalespersonId is { } seller && sellers.Contains(seller) ? seller : userId,
                            UnitCost = product?.CostPrice,
                            Bags = line.Bags is > 0 and <= 100_000 && product?.BagSize != null ? line.Bags : null,
                            Amount = Money.LineAmount(line.Quantity, line.UnitPrice),
                            // Clothes: 5% or 18% by the piece price (GarmentGst); a bill re-made from a snapshot keeps its rates.
                            GstRate = taxSnapshot is null ? GarmentGst.RateFor(product?.HsnCode, product?.GstRate, line.UnitPrice) : taxSnapshot[i].GstRate,
                            HsnCode = taxSnapshot is null ? product?.HsnCode : taxSnapshot[i].HsnCode,
                        });
                    }
                }

                var total = lines.Sum(l => l.Amount);
                if (errors.Count == 0 && (total <= 0 || total > Money.MaxAmount))
                {
                    errors[nameof(request.Lines)] = ["The bill total must be above ₹0."];
                }

                // An exchange: the returned goods' credit note pays part of this bill (all of it, with change, if worth more).
                CreditNote? exchange = null;
                if (request.ExchangeNoteId is { } noteId)
                {
                    exchange = await db.CreditNotes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == noteId, cancellationToken);
                    if (exchange is not { ForExchange: true, ExchangeBillId: null })
                    {
                        errors[nameof(request.ExchangeNoteId)] = ["That exchange credit was already used, or isn't for an exchange."];
                        exchange = null;
                    }
                    else if (ExchangeSlips.Expired(exchange.OccurredAt, clock.GetUtcNow()))
                    {
                        errors[nameof(request.ExchangeNoteId)] = [$"This exchange slip ran out on {ExchangeSlips.ValidTill(exchange.OccurredAt):d MMM yyyy} ({ExchangeSlips.ValidDays} days)."];
                        exchange = null;
                    }
                    else if (exchange.CustomerId is { } creditOwner && creditOwner != request.CustomerId)
                    {
                        errors[nameof(request.ExchangeNoteId)] = ["This credit belongs to the customer who returned the goods: choose them on the bill."];
                        exchange = null;
                    }
                }

                var exchangeCredit = exchange is null ? 0m : Math.Min(exchange.Total, total);
                var due = total - exchangeCredit;
                if (request.PaidAmount < 0 || decimal.Round(request.PaidAmount, 2) != request.PaidAmount || request.PaidAmount > due)
                {
                    errors[nameof(request.PaidAmount)] = [exchange is null ? "Paid amount must be between ₹0 and the bill total." : $"With the exchange credit, at most ₹{due:0.##} is left to pay."];
                }
                else if (request.PaidAmount < due && tenant.IsBlocked(Identity.StaffLimits.Udhaar))
                {
                    errors[nameof(request.PaidAmount)] = [Identity.StaffLimits.Message(Identity.StaffLimits.Udhaar)];
                }
                else if (request.CustomerId is null && request.PaidAmount != due)
                {
                    errors[nameof(request.PaidAmount)] = ["Choose a customer to put the rest on khata."];
                }

                CheckBackdate(request.OccurredAt, isOwner, errors);
                // Schedule H, H1 and X medicines sell only on a prescription: the doctor and patient go with the bill.
                var (prescription, rxError) = Prescriptions.Check(request.Prescription,
                    lines.Where(l => l.ProductId is not null).Select(l => (l.Name, products[l.ProductId!.Value].Schedule)),
                    DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime));
                if (rxError is not null)
                {
                    errors[nameof(request.Prescription)] = [rxError];
                }

                // Serialised units (a pump, an inverter) go out by serial number, one per unit, for the warranty.
                if (errors.Count == 0 && await Catalogue.Serials.CheckAsync(db, lines, products, request.Serials, cancellationToken) is { } serialError)
                {
                    errors[nameof(request.Serials)] = [serialError];
                }

                var site = string.IsNullOrWhiteSpace(request.Site) ? null : request.Site.Trim();
                var vehicle = string.IsNullOrWhiteSpace(request.VehicleNumber) ? null : request.VehicleNumber.Trim().ToUpperInvariant().Replace(" ", "");
                if (site?.Length > 80 || vehicle?.Length > 15)
                {
                    errors[nameof(request.Site)] = ["Keep the site and vehicle number short."];
                }

                if (errors.Count > 0)
                {
                    return LedgerResult<BillDto>.Invalid(errors);
                }

                if (request.PainterId is { } painterId && !await db.Painters.AnyAsync(p => p.Id == painterId, cancellationToken))
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.PainterId), "Painter not found.");
                }

                if (request.CustomerId is { } customerId && !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.CustomerId), "Customer not found.");
                }

                // GST wants the buyer's name and address on a bill of ₹50,000 or more: a "name later" khata can't take one.
                if (request.CustomerId is { } unnamedId && total >= NameLaterBillLimit && await db.Customers.AnyAsync(c => c.Id == unnamedId && c.NameLater, cancellationToken))
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.CustomerId), "A bill of ₹50,000 or more needs the buyer's name. Add their name first.");
                }

                if (request.PaymentMode is PaymentMode.Bank or PaymentMode.Cheque && request.PaidAmount > 0)
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.PaymentMode), "Choose cash, UPI or card for a bill's payment. A cheque goes on the khata as a payment.");
                }

                // Paid two ways: each part cash or UPI, above ₹0, together exactly what was paid.
                var parts = (request.Payments ?? []).Where(p => p.Amount != 0).Select(p => new BillPaymentPart(p.Mode, p.Amount, string.IsNullOrWhiteSpace(p.Reference) ? null : p.Reference.Trim().ToUpperInvariant())).ToList();
                if (parts.Count > 0 && (parts.Count > 4 || parts.Any(p => p.Amount < 0 || decimal.Round(p.Amount, 2) != p.Amount || p.Mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Card) || (p.Reference is { } r && (p.Mode != PaymentMode.Upi || r.Length is < 6 or > 30 || !r.All(char.IsAsciiLetterOrDigit))))
                    || parts.Sum(p => p.Amount) != request.PaidAmount))
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.Payments), "The cash, UPI and card parts must add up to the amount paid, and a UPI reference is 6 to 30 letters and digits.");
                }

                // Parts of one mode join up; one part left is an ordinary one-way payment.
                parts = parts.GroupBy(p => p.Mode).Select(g => new BillPaymentPart(g.Key, g.Sum(p => p.Amount), g.Select(p => p.Reference).FirstOrDefault(r => r is not null))).OrderByDescending(p => p.Amount).ToList();
                var occurredAt = OccurredAt(request.OccurredAt);
                var paymentMode = request.PaidAmount > 0 ? parts.FirstOrDefault()?.Mode ?? request.PaymentMode ?? PaymentMode.Cash : (PaymentMode?)null;
                var bill = new Bill
                {
                    Id = request.Id,
                    CustomerId = request.CustomerId,
                    Total = total,
                    PaidAmount = request.PaidAmount,
                    Payments = BillPayments.ToJson(parts),
                    PaymentMode = paymentMode,
                    ExchangeNoteId = exchange?.Id,
                    ExchangeCredit = exchangeCredit,
                    OccurredAt = occurredAt,
                    CreatedByUserId = userId,
                    Lines = lines,
                    Site = site,
                    VehicleNumber = vehicle,
                    Counter = string.IsNullOrWhiteSpace(request.Counter) ? null : request.Counter.Trim()[..Math.Min(30, request.Counter.Trim().Length)],
                    PainterId = request.PainterId,
                    Gstin = request.WithoutGst ? null : await BillGstinAsync(cancellationToken),
                    EntrySeconds = BillSpeed.Clean(request.EntrySeconds),
                };
                if (bill.Gstin is not null)
                {
                    TaxLines(bill, lines, await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.GstComposition).SingleAsync(cancellationToken),
                        bill.CustomerId is { } buyerId ? await db.Customers.AsNoTracking().Where(c => c.Id == buyerId).Select(c => c.Gstin).SingleOrDefaultAsync(cancellationToken) : null,
                        request.PlaceOfSupply?.Trim());
                }

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                (bill.FinancialYear, bill.Number) = await NextBillNumberAsync(cancellationToken);
                db.Bills.Add(bill);
                // Returned goods worth more than the new ones: the change goes back out of the drawer.
                if (exchange is not null && exchange.Total > total)
                {
                    db.Expenses.Add(new Expense { Amount = exchange.Total - total, Category = ExpenseCategory.Other, Mode = PaymentMode.Cash, Note = $"Refund {exchange.NoteNo} (exchange)", OccurredAt = occurredAt, CreatedByUserId = userId });
                }

                if (prescription is not null)
                {
                    prescription.BillId = bill.Id;
                    db.BillPrescriptions.Add(prescription);
                }

                if (bill.CustomerId is { } billCustomerId)
                {
                    db.LedgerEntries.Add(new LedgerEntry
                    {
                        CustomerId = billCustomerId,
                        Kind = LedgerEntryKind.Credit,
                        Amount = total,
                        BillId = bill.Id,
                        OccurredAt = occurredAt,
                        CreatedByUserId = userId,
                    });
                    if (exchangeCredit > 0)
                    {
                        // The returned goods pay this part, on the statement as a return against the new bill.
                        db.LedgerEntries.Add(new LedgerEntry
                        {
                            CustomerId = billCustomerId, Kind = LedgerEntryKind.Return, Amount = -exchangeCredit, BillId = bill.Id,
                            Note = exchange!.NoteNo, OccurredAt = occurredAt.AddMicroseconds(1), CreatedByUserId = userId,
                        });
                    }

                    // One payment line per way it was paid, so the statement says "₹500 cash, ₹300 UPI".
                    var paidParts = bill.Parts();
                    for (var p = 0; p < paidParts.Count; p++)
                    {
                        db.LedgerEntries.Add(new LedgerEntry
                        {
                            CustomerId = billCustomerId,
                            Kind = LedgerEntryKind.Payment,
                            Amount = -paidParts[p].Amount,
                            PaymentMode = paidParts[p].Mode,
                            // The UTR typed on the bill, so the khata line shows the proof too.
                            UpiRef = paidParts[p].Mode == PaymentMode.Upi ? paidParts[p].Reference : null,
                            BillId = bill.Id,
                            // A microsecond after the credit, so the statement always lists the bill before its payment.
                            OccurredAt = occurredAt.AddMicroseconds(1 + p),
                            CreatedByUserId = userId,
                        });
                    }
                }

                // Items from the catalogue leave the shelf: one sale movement per product, in this same transaction.
                // Services (a haircut, a facial) have no shelf, so they move no stock.
                var lineProductIds = lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
                var services = lineProductIds.Count == 0 ? [] : await db.Products.AsNoTracking()
                    .Where(p => lineProductIds.Contains(p.Id) && p.IsService).Select(p => p.Id).ToListAsync(cancellationToken);
                var sold = lines.Where(l => l.ProductId is not null && !stockAlreadyOut && !services.Contains(l.ProductId.Value))
                    .GroupBy(l => l.ProductId!.Value)
                    .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity), Bags: g.Sum(l => l.Bags ?? 0)))
                    .ToList();
                foreach (var (productId, quantity, bagsSold) in sold)
                {
                    db.StockMovements.Add(new StockMovement
                    {
                        ProductId = productId,
                        Kind = StockMovementKind.Sale,
                        Quantity = -quantity,
                        Bags = bagsSold > 0 ? -bagsSold : null,
                        BillId = bill.Id,
                        OccurredAt = occurredAt,
                        CreatedByUserId = userId,
                    });
                }

                await db.SaveChangesAsync(cancellationToken);

                // One update decides which bill gets the credit: two tills can't both spend it.
                if (exchange is not null && await db.CreditNotes.Where(n => n.Id == exchange.Id && n.ExchangeBillId == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(n => n.ExchangeBillId, bill.Id), cancellationToken) == 0)
                {
                    return LedgerResult<BillDto>.Conflicted("That exchange credit was just used on another bill.");
                }

                // The evening rush showed bills waiting on each other here: two cashiers selling the same items lock the
                // same product rows. So keep the locks short: every product's stock in one statement, in id order (two
                // bills can't deadlock), and the customer's new balance read straight from its update.
                if (sold.Count > 0)
                {
                    var ordered = sold.OrderBy(x => x.ProductId).ToList();
                    var ids = ordered.Select(x => x.ProductId).ToArray();
                    var quantities = ordered.Select(x => x.Quantity).ToArray();
                    await db.Database.ExecuteSqlAsync(
                        $"UPDATE products AS p SET stock = p.stock - v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id",
                        cancellationToken);
                }

                // Medicines kept by batch leave the batch that expires first; expired stock stops the bill.
                if (await Catalogue.Batches.AllocateAsync(db, bill.Id, lines, sold.Select(x => x.ProductId).ToList(),
                        DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime), cancellationToken) is { } shortage)
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.Lines), shortage);
                }

                await Catalogue.Serials.SellAsync(db, bill.Id, bill.CustomerId, lines, products, request.Serials,
                    DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime), cancellationToken);

                decimal? balance = null;
                if (bill.CustomerId is { } owingCustomerId)
                {
                    var delta = total - exchangeCredit - bill.PaidAmount;
                    balance = delta != 0
                        ? await AdjustBalanceAsync(owingCustomerId, delta, cancellationToken)
                        : await BalanceAsync(owingCustomerId, cancellationToken);
                }

                if (beforeCommit is not null && await beforeCommit() is { } refused)
                {
                    return LedgerResult<BillDto>.Conflicted(refused);
                }

                await transaction.CommitAsync(cancellationToken);
                return LedgerResult<BillDto>.Created(new BillDto(
                    bill.Id, bill.CustomerId, bill.Total, bill.PaidAmount, bill.Total - bill.PaidAmount - bill.ExchangeCredit, bill.PaymentMode, bill.OccurredAt,
                    lines.Select(l => new BillLineDto(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount, l.GstRate, l.HsnCode, l.TaxableValue, l.Cgst, l.Sgst, l.Igst, l.Batch, l.ListPrice, l.PrintName, l.Bags)).ToList(),
                    balance, bill.Site, bill.VehicleNumber, bill.PainterId, bill.BillNo, bill.Gstin, BuyerGstin: bill.BuyerGstin, BillOfSupply: bill.BillOfSupply,
                    Prescription: prescription is null ? null : new PrescriptionRequest(prescription.DoctorName, prescription.DoctorAddress, prescription.DoctorRegNo, prescription.PatientName, prescription.PatientAddress, prescription.PrescribedOn),
                    ExchangeCredit: bill.ExchangeCredit, ExchangeNoteNo: exchange?.NoteNo, PlaceOfSupply: bill.PlaceOfSupply, Payments: bill.Parts(), Counter: bill.Counter));
            });

    /// <summary>
    /// Cancels a whole bill, owner only, with a reason. Nothing is deleted: the number stays used, its khata entries are
    /// reversed, shelf stock comes back and challans can be billed again (their goods stay out). Safe to repeat.
    /// </summary>
    public async Task<LedgerResult<BillDto>> CancelBillAsync(Guid id, CancelBillRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (reason is not { Length: >= 3 and <= 200 })
        {
            return LedgerResult<BillDto>.Invalid(nameof(request.Reason), "Say why this bill is being cancelled.");
        }

        var bill = await db.Bills.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill is null)
        {
            return LedgerResult<BillDto>.Missing();
        }

        var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.BillId == id).ToListAsync(cancellationToken);
        // A walk-in return or an exchange leaves no khata line, so ask the credit notes too.
        if (entries.Any(e => e.Kind == LedgerEntryKind.Return) || bill.ExchangeNoteId is not null || await db.CreditNotes.AnyAsync(n => n.BillId == id, cancellationToken))
        {
            return LedgerResult<BillDto>.Conflicted("Items on this bill were returned, so it can't be cancelled as a whole.");
        }

        // A bill in a closed month stays as filed: a credit note in the open month undoes it instead.
        await Books.EnsureOpenAsync(db, tenant, bill.OccurredAt, cancellationToken);
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // One update decides who cancels: a second attempt changes no row and gets the saved bill back.
        var claimed = await db.Bills.Where(b => b.Id == id && b.CancelledAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.CancelledAt, now).SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledByUserId, userId), cancellationToken);
        if (claimed == 0)
        {
            return LedgerResult<BillDto>.Replay((await FindBillAsync(id, cancellationToken))!);
        }

        var reversed = entries.Where(e => e.ReversesEntryId != null).Select(e => e.ReversesEntryId!.Value).ToHashSet();
        var reversals = entries.Where(e => e.Kind != LedgerEntryKind.Reversal && !reversed.Contains(e.Id)).Select(e => new LedgerEntry
        {
            CustomerId = e.CustomerId,
            Kind = LedgerEntryKind.Reversal,
            Amount = -e.Amount,
            PaymentMode = e.PaymentMode,
            BillId = id,
            ReversesEntryId = e.Id,
            Note = reason,
            OccurredAt = now,
            CreatedByUserId = userId,
        }).ToList();
        db.LedgerEntries.AddRange(reversals);

        var sold = (await db.StockMovements.AsNoTracking().Where(m => m.BillId == id && m.Kind == StockMovementKind.Sale).ToListAsync(cancellationToken))
            .GroupBy(m => m.ProductId).Select(g => (ProductId: g.Key, Quantity: -g.Sum(m => m.Quantity), Bags: -g.Sum(m => m.Bags ?? 0))).OrderBy(x => x.ProductId).ToList();
        foreach (var (productId, quantity, bagsBack) in sold)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = productId, Kind = StockMovementKind.Adjustment, Quantity = quantity, Bags = bagsBack > 0 ? bagsBack : null, BillId = id, OccurredAt = now,
                CreatedByUserId = userId, Note = "Bill cancelled",
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        if (sold.Count > 0)
        {
            var ids = sold.Select(x => x.ProductId).ToArray();
            var quantities = sold.Select(x => x.Quantity).ToArray();
            await db.Database.ExecuteSqlAsync(
                $"UPDATE products AS p SET stock = p.stock + v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", cancellationToken);
            await Catalogue.Batches.RestoreAsync(db, id, cancellationToken);
            await Catalogue.Serials.RestoreAsync(db, id, cancellationToken);
        }

        var delta = reversals.Sum(r => r.Amount);
        if (bill.CustomerId is { } customerId && delta != 0)
        {
            await AdjustBalanceAsync(customerId, delta, cancellationToken);
        }

        await db.Challans.Where(c => c.InvoiceId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.InvoiceId, (Guid?)null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return LedgerResult<BillDto>.Created((await FindBillAsync(id, cancellationToken))!);
    }

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
                b.Lines.OrderBy(l => l.LineNo).Select(l => new BillItem(l.Id, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount)).ToList(), b.Site), cancellationToken);

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
            month is { } chosen ? $"{chosen.Year:D4}-{chosen.Month:D2}" : null);
    }

    public async Task<BillView?> GetBillAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await FindBillAsync(id, cancellationToken) is not { } bill)
        {
            return null;
        }

        var customer = bill.CustomerId is { } customerId
            ? await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit)).SingleAsync(cancellationToken)
            : null;
        // For the printed bill. Today's addresses, not a copy kept on the bill: a shop that moves shows its new address on old bills.
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.ShopNo, s.Address, s.Area, s.Village, s.City, s.District, s.State }).SingleAsync(cancellationToken);
        var shopAddress = string.Join(", ", new[] { shop.ShopNo, shop.Address, shop.Area, shop.Village, shop.City, shop.District, shop.State }
            .Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        var buyerAddress = bill.CustomerId is { } buyerId ? await db.Customers.AsNoTracking().Where(c => c.Id == buyerId).Select(c => c.Address).SingleAsync(cancellationToken) : null;
        return new BillView(bill with { ShopAddress = shopAddress.Length > 0 ? shopAddress : null, BuyerAddress = buyerAddress }, customer);
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

    /// <summary>
    /// Works out and keeps each line's tax (prices include GST), so returns read the bill as it was. A composition shop
    /// collects no tax: its bill is a bill of supply. A buyer whose GSTIN shows another state is charged IGST, others
    /// CGST and SGST (the place of supply is the buyer's state when known, else the shop's).
    /// </summary>
    private static void TaxLines(Bill bill, List<BillLine> lines, bool composition, string? buyerGstin, string? deliveredTo = null)
    {
        bill.BillOfSupply = composition;
        bill.BuyerGstin = buyerGstin;
        // Place of supply: a registered buyer's own state; else the state the goods were delivered to; else the shop's.
        var shopState = bill.Gstin is { Length: 15 } ? bill.Gstin[..2] : null;
        bill.PlaceOfSupply = buyerGstin is { Length: 15 } && GstExport.States.ContainsKey(buyerGstin[..2]) ? buyerGstin[..2]
            : deliveredTo is not null && GstExport.States.ContainsKey(deliveredTo) ? deliveredTo : shopState;
        if (composition)
        {
            return;
        }

        var interState = shopState is not null && bill.PlaceOfSupply is not null && bill.PlaceOfSupply != shopState;
        foreach (var line in lines.Where(l => l.GstRate is not null))
        {
            var taxable = decimal.Round(line.Amount / (1 + (line.GstRate!.Value / 100)), 2);
            var tax = line.Amount - taxable;
            var half = decimal.Round(tax / 2, 2);
            (line.TaxableValue, line.Igst, line.Cgst, line.Sgst) = interState ? (taxable, tax, 0m, 0m) : (taxable, 0m, half, tax - half);
        }
    }

    /// <summary>The GSTIN a new bill carries. On every plan: a registered shop's bill is a tax document, whatever it pays us.</summary>
    private Task<string?> BillGstinAsync(CancellationToken cancellationToken) =>
        db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.Gstin).SingleOrDefaultAsync(cancellationToken);

    private async Task<BillDto?> FindBillAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking().Include(b => b.Lines).SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        decimal? balance = bill.CustomerId is { } customerId ? await BalanceAsync(customerId, cancellationToken) : null;
        return new BillDto(
            bill.Id, bill.CustomerId, bill.Total, bill.PaidAmount, bill.Total - bill.PaidAmount - bill.ExchangeCredit, bill.PaymentMode, bill.OccurredAt,
            bill.Lines.OrderBy(l => l.LineNo)
                .Select(l => new BillLineDto(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount, l.GstRate, l.HsnCode, l.TaxableValue, l.Cgst, l.Sgst, l.Igst, l.Batch, l.ListPrice, l.PrintName, l.Bags)).ToList(),
            balance, bill.Site, bill.VehicleNumber, bill.PainterId, bill.BillNo, bill.Gstin, bill.CancelledAt, bill.CancelReason, bill.BuyerGstin, bill.BillOfSupply,
            await db.BillPrescriptions.AsNoTracking().Where(r => r.BillId == id)
                .Select(r => new PrescriptionRequest(r.DoctorName, r.DoctorAddress, r.DoctorRegNo, r.PatientName, r.PatientAddress, r.PrescribedOn)).SingleOrDefaultAsync(cancellationToken),
            bill.ExchangeCredit,
            bill.ExchangeNoteId is { } exchangeId ? (await db.CreditNotes.AsNoTracking().Where(n => n.Id == exchangeId).Select(n => new { n.FinancialYear, n.Number }).SingleOrDefaultAsync(cancellationToken)) is { } x ? $"CN/{x.FinancialYear}/{x.Number:D4}" : null : null,
            bill.PlaceOfSupply, Payments: bill.Parts(), Counter: bill.Counter);
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

    /// <summary>
    /// Puts a paid walk-in bill on a customer's khata after the fact: the bill line and its payments, dated when it was
    /// made and marked "added later", so the statement shows the purchase while the balance doesn't move. The bill itself
    /// (number, GST, money) never changes: the cash book, day book and GST file stay as they were. Once only.
    /// </summary>
    public async Task<LedgerResult<BillDto>> AttachCustomerAsync(Guid billId, AttachCustomerRequest request, Guid userId, bool isOwner, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking().SingleOrDefaultAsync(b => b.Id == billId, cancellationToken);
        if (bill is null)
        {
            return LedgerResult<BillDto>.Missing();
        }

        if (bill.CustomerId == request.CustomerId)
        {
            return LedgerResult<BillDto>.Replay((await FindBillAsync(billId, cancellationToken))!);
        }

        if (bill.CustomerId is not null)
        {
            return LedgerResult<BillDto>.Conflicted("This bill is already on a customer's khata.");
        }

        if (bill.CancelledAt is not null)
        {
            return LedgerResult<BillDto>.Conflicted("This bill was cancelled.");
        }

        var reason = request.Reason?.Trim();
        var old = clock.GetUtcNow() - bill.OccurredAt > HelperBackdateLimit;
        if (old && !isOwner)
        {
            return LedgerResult<BillDto>.Invalid("Reason", "After a week only the shop owner can add a customer to a bill.");
        }

        if (old && reason is not { Length: >= 3 and <= 200 })
        {
            return LedgerResult<BillDto>.Invalid("Reason", "Say why this bill is being put on the customer's khata now. They will see it.");
        }

        if (reason?.Length > 200)
        {
            return LedgerResult<BillDto>.Invalid("Reason", "Keep the reason under 200 characters.");
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            return LedgerResult<BillDto>.Invalid(nameof(request.CustomerId), "Customer not found.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // One update decides it: a second attempt changes no row.
        var claimed = await db.Bills.Where(b => b.Id == billId && b.CustomerId == null && b.CancelledAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.CustomerId, request.CustomerId), cancellationToken);
        if (claimed == 0)
        {
            return LedgerResult<BillDto>.Conflicted("This bill was just put on a khata.");
        }

        var note = string.IsNullOrEmpty(reason) ? "Added to khata later" : $"Added to khata later: {reason}";
        db.LedgerEntries.Add(new LedgerEntry
        {
            CustomerId = request.CustomerId, Kind = LedgerEntryKind.Credit, Amount = bill.Total, BillId = bill.Id, Note = note,
            OccurredAt = bill.OccurredAt, CreatedByUserId = userId,
        });
        if (bill.ExchangeCredit > 0)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = request.CustomerId, Kind = LedgerEntryKind.Return, Amount = -bill.ExchangeCredit, BillId = bill.Id,
                OccurredAt = bill.OccurredAt.AddMicroseconds(1), CreatedByUserId = userId,
            });
        }

        var parts = bill.Parts();
        for (var p = 0; p < parts.Count; p++)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = request.CustomerId, Kind = LedgerEntryKind.Payment, Amount = -parts[p].Amount, PaymentMode = parts[p].Mode,
                UpiRef = parts[p].Mode == PaymentMode.Upi ? parts[p].Reference : null, BillId = bill.Id,
                OccurredAt = bill.OccurredAt.AddMicroseconds(1 + p), CreatedByUserId = userId,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        // A walk-in bill is paid in full, so the lines net to nothing; whatever they net to goes on the balance.
        var net = bill.Total - bill.ExchangeCredit - parts.Sum(p => p.Amount);
        if (net != 0)
        {
            await AdjustBalanceAsync(request.CustomerId, net, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return LedgerResult<BillDto>.Created((await FindBillAsync(billId, cancellationToken))!);
    }

    private async Task<decimal> AdjustBalanceAsync(Guid customerId, decimal delta, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<decimal>($"UPDATE customers SET balance = balance + {delta} WHERE id = {customerId} RETURNING balance AS \"Value\"")
            .ToListAsync(cancellationToken)).Single();

    /// <summary>
    /// The next number in this shop's current financial year, taken inside the bill's transaction: if the bill isn't
    /// saved the number isn't used either, so the series has no gaps. Two tills at once queue on the counter row.
    /// </summary>
    private async Task<(string Year, int Number)> NextBillNumberAsync(CancellationToken cancellationToken)
    {
        var year = FinancialYear(clock.GetUtcNow());
        var number = (await db.Database.SqlQuery<int>($"""
            INSERT INTO bill_counters (shop_id, financial_year, last_number)
            VALUES (NULLIF(current_setting('app.shop_id', true), '')::uuid, {year}, 1)
            ON CONFLICT (shop_id, financial_year) DO UPDATE SET last_number = bill_counters.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync(cancellationToken)).Single();
        return (year, number);
    }

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
