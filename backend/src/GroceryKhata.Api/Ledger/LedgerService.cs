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
public sealed class LedgerService(AppDbContext db, TimeProvider clock, ITenantContext tenant, Billing.Entitlements entitlements, IHttpContextAccessor? http = null)
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
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit))
            .ToListAsync(cancellationToken);
    }

    public Task<LedgerResult<CustomerDto>> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken) =>
        WriteOnceAsync(
            () => db.Customers.AsNoTracking().Where(c => c.Id == request.Id)
                .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit)).SingleOrDefaultAsync(cancellationToken),
            async () =>
            {
                var errors = new Dictionary<string, string[]>();
                RequireId(request.Id, errors);
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

                var customer = new Customer { Id = request.Id, Name = request.Name!.Trim(), Phone = phone };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
                return LedgerResult<CustomerDto>.Created(new CustomerDto(customer.Id, customer.Name, customer.Phone, 0));
            });

    /// <summary>How far back a helper may date an entry or bill; older needs the owner.</summary>
    public static readonly TimeSpan HelperBackdateLimit = TimeSpan.FromDays(7);

    /// <summary>An entry recorded this long after the date it carries is marked "entered later" on the khata.</summary>
    public static readonly TimeSpan LateAfter = TimeSpan.FromHours(24);

    public Task<LedgerResult<KhataEntryResponse>> AddKhataEntryAsync(
        KhataEntryRequest request, Guid userId, CancellationToken cancellationToken, bool isOwner = true) =>
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

                if (upiRef is not null && await db.LedgerEntries.AnyAsync(e => e.UpiRef == upiRef, cancellationToken))
                {
                    return LedgerResult<KhataEntryResponse>.Conflicted("This UPI reference is already recorded. The same payment can't be entered twice.");
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
                    OccurredAt = OccurredAt(request.OccurredAt),
                    CreatedByUserId = userId,
                };

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                db.LedgerEntries.Add(entry);
                await db.SaveChangesAsync(cancellationToken);
                var balance = await AdjustBalanceAsync(entry.CustomerId, entry.Amount, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return LedgerResult<KhataEntryResponse>.Created(new KhataEntryResponse(LedgerEntryDto.From(entry), balance));
            });

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

                if (original.UpiRef is not null)
                {
                    return LedgerResult<KhataEntryResponse>.Conflicted("A UPI payment with a bank reference can't be removed: the bank's record proves it.");
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
                await transaction.CommitAsync(cancellationToken);

                return LedgerResult<KhataEntryResponse>.Created(new KhataEntryResponse(LedgerEntryDto.From(reversal), balance));
            });

    /// <summary>
    /// Saves a cash memo. With a customer, the bill total posts to their khata as a credit and anything
    /// paid now posts as a payment, so the statement shows both; without one it must be paid in full.
    /// </summary>
    /// <param name="stockAlreadyOut">True for an invoice made from delivery challans: the goods left the shelf with the challans.</param>
    /// <param name="taxSnapshot">GST and HSN per line, kept from the challans; without it each line takes the item's current ones.</param>
    public Task<LedgerResult<BillDto>> CreateBillAsync(
        BillRequest request, Guid userId, CancellationToken cancellationToken, bool stockAlreadyOut = false, bool isOwner = true,
        IReadOnlyList<(decimal? GstRate, string? HsnCode)>? taxSnapshot = null) =>
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
                    else if (!isOwner && product is { SellingPrice: > 0 } regular && line.UnitPrice < regular.SellingPrice
                        && (regular.SellingPrice - line.UnitPrice) / regular.SellingPrice * 100 > staffMaxDiscount)
                    {
                        errors[field] = [$"{name}: helpers can take off at most {staffMaxDiscount:0.##}% (₹{regular.SellingPrice * (1 - staffMaxDiscount / 100):0.##} or more). Ask the owner."];
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
                            SalespersonId = line.SalespersonId is { } seller && sellers.Contains(seller) ? seller : userId,
                            UnitCost = product?.CostPrice,
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
                    exchange = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == noteId, cancellationToken);
                    if (exchange is not { ForExchange: true, ExchangeBillId: null })
                    {
                        errors[nameof(request.ExchangeNoteId)] = ["That exchange credit was already used, or isn't for an exchange."];
                        exchange = null;
                    }
                }

                var exchangeCredit = exchange is null ? 0m : Math.Min(exchange.Total, total);
                var due = total - exchangeCredit;
                if (request.PaidAmount < 0 || decimal.Round(request.PaidAmount, 2) != request.PaidAmount || request.PaidAmount > due)
                {
                    errors[nameof(request.PaidAmount)] = [exchange is null ? "Paid amount must be between ₹0 and the bill total." : $"With the exchange credit, at most ₹{due:0.##} is left to pay."];
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

                var occurredAt = OccurredAt(request.OccurredAt);
                var paymentMode = request.PaidAmount > 0 ? request.PaymentMode ?? PaymentMode.Cash : (PaymentMode?)null;
                var bill = new Bill
                {
                    Id = request.Id,
                    CustomerId = request.CustomerId,
                    Total = total,
                    PaidAmount = request.PaidAmount,
                    PaymentMode = paymentMode,
                    ExchangeNoteId = exchange?.Id,
                    ExchangeCredit = exchangeCredit,
                    OccurredAt = occurredAt,
                    CreatedByUserId = userId,
                    Lines = lines,
                    Site = site,
                    VehicleNumber = vehicle,
                    PainterId = request.PainterId,
                    Gstin = await BillGstinAsync(cancellationToken),
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
                if (exchange is not null)
                {
                    exchange.ExchangeBillId = bill.Id;
                    // Returned goods worth more than the new ones: the change goes back out of the drawer.
                    if (exchange.Total > total)
                    {
                        db.Expenses.Add(new Expense { Amount = exchange.Total - total, Category = ExpenseCategory.Other, Mode = PaymentMode.Cash, Note = $"Refund {exchange.NoteNo} (exchange)", OccurredAt = occurredAt, CreatedByUserId = userId });
                    }
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

                    if (bill.PaidAmount > 0)
                    {
                        db.LedgerEntries.Add(new LedgerEntry
                        {
                            CustomerId = billCustomerId,
                            Kind = LedgerEntryKind.Payment,
                            Amount = -bill.PaidAmount,
                            PaymentMode = paymentMode,
                            BillId = bill.Id,
                            // A microsecond after the credit, so the statement always lists the bill before its payment.
                            OccurredAt = occurredAt.AddMicroseconds(1),
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
                    .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity)))
                    .ToList();
                foreach (var (productId, quantity) in sold)
                {
                    db.StockMovements.Add(new StockMovement
                    {
                        ProductId = productId,
                        Kind = StockMovementKind.Sale,
                        Quantity = -quantity,
                        BillId = bill.Id,
                        OccurredAt = occurredAt,
                        CreatedByUserId = userId,
                    });
                }

                await db.SaveChangesAsync(cancellationToken);

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
                    var delta = total - bill.PaidAmount;
                    balance = delta != 0
                        ? await AdjustBalanceAsync(owingCustomerId, delta, cancellationToken)
                        : await BalanceAsync(owingCustomerId, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return LedgerResult<BillDto>.Created(new BillDto(
                    bill.Id, bill.CustomerId, bill.Total, bill.PaidAmount, bill.Total - bill.PaidAmount - bill.ExchangeCredit, bill.PaymentMode, bill.OccurredAt,
                    lines.Select(l => new BillLineDto(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount, l.GstRate, l.HsnCode, l.TaxableValue, l.Cgst, l.Sgst, l.Igst, l.Batch, l.ListPrice, l.PrintName)).ToList(),
                    balance, bill.Site, bill.VehicleNumber, bill.PainterId, bill.BillNo, bill.Gstin, BuyerGstin: bill.BuyerGstin, BillOfSupply: bill.BillOfSupply,
                    Prescription: prescription is null ? null : new PrescriptionRequest(prescription.DoctorName, prescription.DoctorAddress, prescription.DoctorRegNo, prescription.PatientName, prescription.PatientAddress, prescription.PrescribedOn),
                    ExchangeCredit: bill.ExchangeCredit, ExchangeNoteNo: exchange?.NoteNo, PlaceOfSupply: bill.PlaceOfSupply));
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
        if (entries.Any(e => e.Kind == LedgerEntryKind.Return))
        {
            return LedgerResult<BillDto>.Conflicted("Items on this bill were returned, so it can't be cancelled as a whole.");
        }

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
            .GroupBy(m => m.ProductId).Select(g => (ProductId: g.Key, Quantity: -g.Sum(m => m.Quantity))).OrderBy(x => x.ProductId).ToList();
        foreach (var (productId, quantity) in sold)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = productId, Kind = StockMovementKind.Adjustment, Quantity = quantity, BillId = id, OccurredAt = now,
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
                e.CreatedAt - e.OccurredAt > LateAfter, e.CreatedAt);
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

    /// <summary>The GSTIN a new bill carries: none unless the shop's plan has GST billing, so it prints as a plain bill.</summary>
    private async Task<string?> BillGstinAsync(CancellationToken cancellationToken)
    {
        var gstin = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.Gstin).SingleOrDefaultAsync(cancellationToken);
        return gstin is not null && (await entitlements.ForShopAsync(tenant.ShopId, cancellationToken)).Has(Billing.Features.Gst) ? gstin : null;
    }

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
                .Select(l => new BillLineDto(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount, l.GstRate, l.HsnCode, l.TaxableValue, l.Cgst, l.Sgst, l.Igst, l.Batch, l.ListPrice, l.PrintName)).ToList(),
            balance, bill.Site, bill.VehicleNumber, bill.PainterId, bill.BillNo, bill.Gstin, bill.CancelledAt, bill.CancelReason, bill.BuyerGstin, bill.BillOfSupply,
            await db.BillPrescriptions.AsNoTracking().Where(r => r.BillId == id)
                .Select(r => new PrescriptionRequest(r.DoctorName, r.DoctorAddress, r.DoctorRegNo, r.PatientName, r.PatientAddress, r.PrescribedOn)).SingleOrDefaultAsync(cancellationToken),
            bill.ExchangeCredit,
            bill.ExchangeNoteId is { } exchangeId ? (await db.CreditNotes.AsNoTracking().Where(n => n.Id == exchangeId).Select(n => new { n.FinancialYear, n.Number }).SingleOrDefaultAsync(cancellationToken)) is { } x ? $"CN/{x.FinancialYear}/{x.Number:D4}" : null : null,
            bill.PlaceOfSupply);
    }

    private Task<decimal> BalanceAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Balance).SingleAsync(cancellationToken);

    /// <summary>
    /// Atomic in the database (balance = balance + delta), so two tills posting at once can't lose an update. Returns the
    /// new balance from the same statement, saving a read while the row is locked.
    /// </summary>
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
