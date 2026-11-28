using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>Bills: making, cancelling and reading them, their tax lines and their numbers.</summary>
public sealed partial class LedgerService
{
    /// <summary>
    /// Saves a cash memo. With a customer, the bill total posts to their khata as a credit and anything
    /// paid now posts as a payment, so the statement shows both; without one it must be paid in full.
    /// </summary>
    /// <param name="stockAlreadyOut">True for an invoice made from delivery challans: the goods left the shelf with the challans.</param>
    /// <param name="taxSnapshot">GST and HSN per line, kept from the challans; without it each line takes the item's current ones.</param>
    /// <param name="beforeCommit">Runs in the bill's transaction, last: a message from it undoes the bill.</param>
    public Task<LedgerResult<BillDto>> CreateBillAsync(
        BillRequest request, Guid userId, CancellationToken cancellationToken, bool stockAlreadyOut = false, bool isOwner = true,
        IReadOnlyList<(decimal? GstRate, string? HsnCode)>? taxSnapshot = null, Func<Task<string?>>? beforeCommit = null, Guid? approvedBy = null) =>
        WriteOnceAsync(
            () => FindBillAsync(request.Id, cancellationToken),
            async () =>
            {
                var errors = new Dictionary<string, string[]>();
                RequireId(request.Id, errors);
                // Salespeople must have access to this shop; a line without one (or with a stranger) is the biller's.
                var sellers = (await db.Memberships.AsNoTracking().Where(m => m.ShopId == tenant.ShopId).Select(m => m.UserId).ToListAsync(cancellationToken)).ToHashSet();
                var staffMaxDiscount = isOwner || approvedBy is not null ? 100m : await db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.StaffMaxDiscountPercent).SingleAsync(cancellationToken);
                // A contractor's or dealer's price is the owner's own price list, so a helper may bill at it.
                var groupOff = request.CustomerId is { } groupCustomer
                    ? await (from c in db.Customers.AsNoTracking()
                             where c.Id == groupCustomer
                             from sh in db.Shops.AsNoTracking().Where(sh => sh.Id == tenant.ShopId)
                             select c.PriceGroup == "contractor" ? sh.ContractorDiscountPercent : c.PriceGroup == "dealer" ? sh.DealerDiscountPercent : 0m).FirstOrDefaultAsync(cancellationToken)
                    : 0m;
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

                // Promotions over the whole bill (buy-get, slabs, combos, bill offers) priced by the same engine as the app:
                // a helper may sell at what it gives, but no lower.
                var billOffer = request.BillOfferId is { } billOfferId ? await db.SaleOffers.AsNoTracking().SingleOrDefaultAsync(o => o.Id == billOfferId, cancellationToken) : null;
                // Loyalty points spent: the customer's code by SMS proves it's them; their value comes off the bill.
                var loyaltyValue = 0m;
                if (request.LoyaltyPoints is { } spend && spend != 0)
                {
                    var (value, problem) = spend < 0 ? (0m, "Points to spend must be more than 0.")
                        : await Loyalty.CheckRedeemAsync(db, tenant.ShopId, request.CustomerId, spend, request.LoyaltyCode, otpOptions?.Value ?? new Identity.OtpOptions(), clock.GetUtcNow(), cancellationToken);
                    if (problem is not null)
                    {
                        errors[nameof(request.LoyaltyPoints)] = [problem];
                    }

                    loyaltyValue = value;
                }

                var promotionsAllow = !isOwner && (billOffer is not null || loyaltyValue > 0 || offers.Values.Any(o => o.Kind is not (SaleOfferKind.Percent or SaleOfferKind.Amount)))
                    && await PromotionsAllowAsync(request.Lines, products, billAt, cancellationToken, loyaltyValue);

                // A till that was turned off can't make new bills (one already queued offline before that still can).
                var till = request.TerminalId is { } tillId ? await db.Terminals.SingleOrDefaultAsync(t => t.Id == tillId, cancellationToken) : null;
                if (till is { IsActive: false, TurnedOffAt: { } offAt } && OccurredAt(request.OccurredAt) > offAt)
                {
                    errors[nameof(request.TerminalId)] = [$"Till {till.Code} was turned off by the owner. Ask them to turn it on, or bill from another till."];
                }

                if (till is not null)
                {
                    till.LastBillAt = clock.GetUtcNow();
                }

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
                    var offerPrice = offer is { Kind: SaleOfferKind.Percent or SaleOfferKind.Amount } ? SaleOffers.PriceFor(offer, product!.SellingPrice) : (decimal?)null;
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
                    else if (line.Bags is > 0 and var bagCount && product?.BagSize is { } bagSize && bagSize > 0
                        && Math.Abs(line.Quantity - (bagCount * bagSize)) > bagCount * bagSize * 0.05m)
                    {
                        errors[field] = [$"{name}: {bagCount:0.##} bags of {bagSize:0.##} should be about {bagCount * bagSize:0.##}, not {line.Quantity:0.###}."];
                    }
                    else if (product?.Mrp is { } mrp && mrp > 0 && line.UnitPrice > mrp)
                    {
                        // Legal Metrology: nothing may be sold above the MRP printed on it.
                        errors[field] = [$"{name} has MRP ₹{mrp:0.##}: the price can't be above it."];
                    }
                    // The owner set the offer's price before billing, so a helper may sell at it, past their own limit.
                    // So did a wholesale price, for a line that takes at least its quantity.
                    else if (!isOwner && approvedBy is null && !promotionsAllow && !(offerPrice is { } floor && line.UnitPrice >= floor)
                        && !(product is { WholesalePrice: { } wholesale, WholesaleMinQty: { } atLeast } && line.Quantity >= atLeast && line.UnitPrice >= wholesale)
                        // Whole bags at the owner's bag price (per unit, to the paisa).
                        && !(line.Bags is > 0 && product is { BagPrice: { } bagPrice, BagSize: { } perBag } && perBag > 0 && line.UnitPrice >= decimal.Round(bagPrice / perBag, 2) - 0.01m)
                        && !(groupOff > 0 && product is { SellingPrice: > 0 } listed && line.UnitPrice >= decimal.Round(listed.SellingPrice * (1 - (groupOff / 100)), 2))
                        && DiscountProblem(name, product, line.UnitPrice, staffMaxDiscount) is { } tooLow)
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
                    if (exchange is not { ForExchange: true, SlipCancelledAt: null } || exchange.Spent >= exchange.Total)
                    {
                        errors[nameof(request.ExchangeNoteId)] = ["That exchange credit was already used, or isn't for an exchange."];
                        exchange = null;
                    }
                    else if (await ExchangeSlips.DaysAsync(db, cancellationToken) is var slipDays && ExchangeSlips.Expired(exchange.OccurredAt, clock.GetUtcNow(), slipDays))
                    {
                        errors[nameof(request.ExchangeNoteId)] = [$"This exchange slip ran out on {ExchangeSlips.ValidTill(exchange.OccurredAt, slipDays):d MMM yyyy} ({slipDays} days)."];
                        exchange = null;
                    }
                    else if (!isOwner && Math.Min(exchange.Total - exchange.Spent, total) > await db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.ExchangeSlipHelperMax).SingleAsync(cancellationToken))
                    {
                        errors[nameof(request.ExchangeNoteId)] = ["An exchange slip this big needs the shop owner: ask them to make this bill."];
                        exchange = null;
                    }
                    else if (exchange.CustomerId is { } creditOwner && creditOwner != request.CustomerId)
                    {
                        errors[nameof(request.ExchangeNoteId)] = ["This credit belongs to the customer who returned the goods: choose them on the bill."];
                        exchange = null;
                    }
                }

                // Only what's left on the slip; more than this bill stays on the slip for next time (no cash change).
                var exchangeCredit = exchange is null ? 0m : Math.Min(exchange.Total - exchange.Spent, total);
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
                if (parts.Count > 0 && (parts.Count > 4 || parts.Any(p => p.Amount < 0 || decimal.Round(p.Amount, 2) != p.Amount || p.Mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Card or PaymentMode.Voucher)
                    || (p.Mode == PaymentMode.Voucher && GiftVouchers.Clean(p.Reference).Length != 12)
                    || (p.Reference is { } r && p.Mode != PaymentMode.Voucher && (p.Mode != PaymentMode.Upi || r.Length is < 6 or > 30 || !r.All(char.IsAsciiLetterOrDigit))))
                    || parts.Sum(p => p.Amount) != request.PaidAmount))
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.Payments), "The cash, UPI and card parts must add up to the amount paid, and a UPI reference is 6 to 30 letters and digits.");
                }

                // A voucher's code is kept as printed on it (capitals, no spaces).
                parts = parts.Select(p => p.Mode == PaymentMode.Voucher ? p with { Reference = GiftVouchers.Clean(p.Reference) } : p).ToList();
                if (parts.Where(p => p.Mode == PaymentMode.Voucher).GroupBy(p => p.Reference).Any(g => g.Count() > 1))
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.Payments), "Use each gift voucher once on a bill.");
                }

                // Parts of one mode join up (vouchers stay one part each); one part left is an ordinary one-way payment.
                parts = parts.GroupBy(p => p.Mode == PaymentMode.Voucher ? $"V:{p.Reference}" : p.Mode.ToString()).Select(g => new BillPaymentPart(g.First().Mode, g.Sum(p => p.Amount), g.Select(p => p.Reference).FirstOrDefault(r => r is not null))).OrderByDescending(p => p.Amount).ToList();
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
                    // A registered till names the counter itself ("Counter 2 (T02)"), whatever the device typed.
                    Counter = till is not null ? $"{till.Name} ({till.Code})"[..Math.Min(30, $"{till.Name} ({till.Code})".Length)]
                        : string.IsNullOrWhiteSpace(request.Counter) ? null : request.Counter.Trim()[..Math.Min(30, request.Counter.Trim().Length)],
                    PainterId = request.PainterId,
                    // A shift this shop doesn't have is left off rather than refusing the sale.
                    BillOfferId = billOffer?.Id,
                    TerminalId = till?.Id,
                    ApprovedByUserId = approvedBy,
                    LoyaltyPoints = loyaltyValue > 0 ? request.LoyaltyPoints!.Value : 0,
                    LoyaltyValue = loyaltyValue,
                    ShiftId = request.ShiftId is { } shiftId && await db.CashierShifts.AnyAsync(s => s.Id == shiftId, cancellationToken) ? shiftId : null,
                    Gstin = request.WithoutGst ? null : await BillGstinAsync(cancellationToken),
                    EntrySeconds = BillSpeed.Clean(request.EntrySeconds),
                };
                if (bill.Gstin is not null)
                {
                    TaxLines(bill, lines, await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.GstComposition).SingleAsync(cancellationToken),
                        bill.CustomerId is { } buyerId ? await db.Customers.AsNoTracking().Where(c => c.Id == buyerId).Select(c => c.Gstin).SingleOrDefaultAsync(cancellationToken) : null,
                        request.PlaceOfSupply?.Trim());
                }

                // The shop's own bill fields as filled in, and its prefix as it is today (kept on the bill).
                var (fieldsJson, fieldsProblem) = BillFields.ValuesJson(request.Fields);
                if (fieldsProblem is not null)
                {
                    return LedgerResult<BillDto>.Invalid(new Dictionary<string, string[]> { [nameof(request.Fields)] = [fieldsProblem] });
                }

                bill.Fields = fieldsJson;
                bill.Prefix = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.BillPrefix).SingleAsync(cancellationToken);

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                // A placeholder until the end: the real number is taken just before commit (see NextBillNumberAsync), so the
                // shop's counter row is locked for a moment, not for the whole save, and tills don't queue on each other.
                bill.FinancialYear = FinancialYear(clock.GetUtcNow());
                bill.Number = -System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
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
                // Items kept in bags: what is on hand now (before this sale) and the sealed bags counted, so a loose sale
                // bigger than the loose stock opens a bag, as the shopkeeper does at the counter.
                var baggedIds = sold.Select(s => s.ProductId).ToList();
                var bagged = baggedIds.Count == 0 ? [] : await db.Products.AsNoTracking()
                    .Where(p => baggedIds.Contains(p.Id) && p.BagSize != null && p.BagSize > 0)
                    .Select(p => new { p.Id, p.Stock, Size = p.BagSize!.Value }).ToDictionaryAsync(p => p.Id, cancellationToken);
                var counted = bagged.Count == 0 ? [] : await db.StockMovements.AsNoTracking()
                    .Where(m => bagged.Keys.Contains(m.ProductId) && m.Bags != null)
                    .GroupBy(m => m.ProductId).Select(g => new { g.Key, Bags = g.Sum(m => m.Bags ?? 0) }).ToDictionaryAsync(g => g.Key, g => g.Bags, cancellationToken);
                foreach (var (productId, quantity, bagsSold) in sold)
                {
                    if (bagged.GetValueOrDefault(productId) is { } bag
                        && StockService.BagsToOpen(bag.Stock - quantity, counted.GetValueOrDefault(productId) - bagsSold, bag.Size) is > 0 and var opened)
                    {
                        db.StockMovements.Add(new StockMovement
                        {
                            ProductId = productId, Kind = StockMovementKind.OpenBag, Quantity = 0, Bags = -opened, BillId = bill.Id,
                            Note = "Opened for a loose sale", OccurredAt = occurredAt, CreatedByUserId = userId,
                        });
                    }

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

                // Loyalty: points spent leave, points earned on what is paid for arrive.
                if (bill.CustomerId is { } pointsOwner)
                {
                    var earnPer = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.LoyaltyEarnPer).SingleAsync(cancellationToken);
                    if (bill.LoyaltyPoints > 0)
                    {
                        db.LoyaltyEntries.Add(new LoyaltyEntry { CustomerId = pointsOwner, BillId = bill.Id, Kind = LoyaltyKind.Redeem, Points = -bill.LoyaltyPoints, OccurredAt = occurredAt });
                    }

                    if (Loyalty.Earned(total, earnPer) is > 0 and var earned)
                    {
                        db.LoyaltyEntries.Add(new LoyaltyEntry { CustomerId = pointsOwner, BillId = bill.Id, Kind = LoyaltyKind.Earn, Points = earned, OccurredAt = occurredAt });
                    }
                }

                await db.SaveChangesAsync(cancellationToken);

                // Gift vouchers that paid part of it: taken off their balance now, or the bill isn't saved.
                if (await GiftVouchers.SpendAsync(db, parts, IndiaTime.Today(clock), cancellationToken) is { } voucherProblem)
                {
                    return LedgerResult<BillDto>.Invalid(nameof(request.Payments), voucherProblem);
                }

                // Two tills spending the same points at once: one at a time per customer, and none may go below zero.
                if (bill.LoyaltyPoints > 0 && bill.CustomerId is { } spender)
                {
                    await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({"loyalty:" + spender}))", cancellationToken);
                    if (await Loyalty.BalanceAsync(db, spender, cancellationToken) < 0)
                    {
                        return LedgerResult<BillDto>.Conflicted("Those points were just spent on another bill.");
                    }
                }

                // One update decides which bill gets the credit: two tills can't both spend it.
                if (exchange is not null && await db.CreditNotes.Where(n => n.Id == exchange.Id && n.Spent == exchange.Spent && n.Spent + exchangeCredit <= n.Total)
                        .ExecuteUpdateAsync(s => s.SetProperty(n => n.ExchangeBillId, bill.Id).SetProperty(n => n.Spent, n => n.Spent + exchangeCredit), cancellationToken) == 0)
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
                    // A helper can't take a customer past their credit limit; the owner sees the warning and decides.
                    if (!isOwner && delta > 0 && await db.Customers.Where(c => c.Id == owingCustomerId).Select(c => c.CreditLimit).SingleAsync(cancellationToken) is { } limit && balance > limit)
                    {
                        return LedgerResult<BillDto>.Invalid(nameof(request.CustomerId), $"This bill takes the customer past their ₹{limit:0.##} credit limit. Take more payment now, or ask the owner.");
                    }
                }

                if (beforeCommit is not null && await beforeCommit() is { } refused)
                {
                    return LedgerResult<BillDto>.Conflicted(refused);
                }

                // Last of all, the bill's real number: still inside the transaction, so a bill that isn't saved uses no
                // number and the series keeps no gaps, but the counter row is held only from here to the commit.
                bill.Number = await NextBillNumberAsync(bill.FinancialYear, cancellationToken);
                await db.Database.ExecuteSqlAsync($"UPDATE bills SET number = {bill.Number} WHERE id = {bill.Id}", cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return LedgerResult<BillDto>.Created(new BillDto(
                    bill.Id, bill.CustomerId, bill.Total, bill.PaidAmount, bill.Total - bill.PaidAmount - bill.ExchangeCredit, bill.PaymentMode, bill.OccurredAt,
                    lines.Select(l => new BillLineDto(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Amount, l.GstRate, l.HsnCode, l.TaxableValue, l.Cgst, l.Sgst, l.Igst, l.Batch, l.ListPrice, l.PrintName, l.Bags)).ToList(),
                    balance, bill.Site, bill.VehicleNumber, bill.PainterId, bill.BillNo, bill.Gstin, BuyerGstin: bill.BuyerGstin, BillOfSupply: bill.BillOfSupply,
                    Prescription: prescription is null ? null : new PrescriptionRequest(prescription.DoctorName, prescription.DoctorAddress, prescription.DoctorRegNo, prescription.PatientName, prescription.PatientAddress, prescription.PrescribedOn),
                    ExchangeCredit: bill.ExchangeCredit, ExchangeNoteNo: exchange?.NoteNo, PlaceOfSupply: bill.PlaceOfSupply, Payments: bill.Parts(), Counter: bill.Counter,
                    Fields: BillFields.Values(bill.Fields)));
            });

    /// <summary>
    /// Cancels a whole bill, owner only, with a reason. Nothing is deleted: the number stays used, its khata entries are
    /// reversed, shelf stock comes back and challans can be billed again (their goods stay out). Safe to repeat.
    /// </summary>
    public async Task<LedgerResult<BillDto>> CancelBillAsync(Guid id, CancelBillRequest request, Guid userId, CancellationToken cancellationToken, Guid? approvedBy = null)
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
            .SetProperty(b => b.CancelledAt, now).SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledByUserId, userId)
            .SetProperty(b => b.ApprovedByUserId, b => approvedBy ?? b.ApprovedByUserId), cancellationToken);
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
        // Gift vouchers that paid for it get their amount back.
        await GiftVouchers.RefundAsync(db, (await db.Bills.AsNoTracking().SingleAsync(b => b.Id == id, cancellationToken)).Parts(), cancellationToken);
        // Points the bill earned go, points it spent come back.
        db.LoyaltyEntries.AddRange((await db.LoyaltyEntries.AsNoTracking().Where(e => e.BillId == id).ToListAsync(cancellationToken))
            .Select(e => new LoyaltyEntry { CustomerId = e.CustomerId, BillId = id, Kind = LoyaltyKind.Reverse, Points = -e.Points, OccurredAt = now }));

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

    public async Task<BillView?> GetBillAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await FindBillAsync(id, cancellationToken) is not { } bill)
        {
            return null;
        }

        var customer = bill.CustomerId is { } customerId
            ? await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit, c.NameLater, c.CreditDays, c.PriceGroup)).SingleAsync(cancellationToken)
            : null;
        // What went on the khata is due the customer's credit days after the bill.
        DateOnly? dueOn = bill.OnKhata > 0 && customer?.CreditDays is { } creditDays
            ? DateOnly.FromDateTime(bill.OccurredAt.ToOffset(Common.IndiaTime.Offset).DateTime).AddDays(creditDays) : null;
        // For the printed bill. Today's addresses, not a copy kept on the bill: a shop that moves shows its new address on old bills.
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.ShopNo, s.Address, s.Area, s.Village, s.City, s.District, s.State }).SingleAsync(cancellationToken);
        var shopAddress = string.Join(", ", new[] { shop.ShopNo, shop.Address, shop.Area, shop.Village, shop.City, shop.District, shop.State }
            .Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        var buyerAddress = bill.CustomerId is { } buyerId ? await db.Customers.AsNoTracking().Where(c => c.Id == buyerId).Select(c => c.Address).SingleAsync(cancellationToken) : null;
        // How the shop prints: logo, signature, style and terms, on the shop's copy and the customer's link alike.
        var print = await Shops.PrintLooks.ForBillAsync(db, cancellationToken);
        return new BillView(bill with { ShopAddress = shopAddress.Length > 0 ? shopAddress : null, BuyerAddress = buyerAddress, Print = print, DueOn = dueOn }, customer);
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
            bill.ExchangeNoteId is { } exchangeId ? (await db.CreditNotes.AsNoTracking().Where(n => n.Id == exchangeId).Select(n => new { n.FinancialYear, n.Number, n.TaxFree }).SingleOrDefaultAsync(cancellationToken)) is { } x ? CreditNote.No(x.FinancialYear, x.Number, x.TaxFree) : null : null,
            bill.PlaceOfSupply, Payments: bill.Parts(), Counter: bill.Counter, Fields: BillFields.Values(bill.Fields));
    }

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

    /// <summary>
    /// The next number in this shop's series for <paramref name="year"/>, taken inside the bill's transaction: if the bill
    /// isn't saved the number isn't used either, so the series has no gaps. Tills saving at once queue on the counter
    /// row, which is why it is the bill's last step before commit.
    /// </summary>
    /// <summary>
    /// The bill is priced no lower than the running promotions give its items (worked out from the items' own prices,
    /// whatever the lines say): then the lines' prices may stand for a helper. A paisa a line is allowed for rounding.
    /// </summary>
    private async Task<bool> PromotionsAllowAsync(IReadOnlyList<BillLineRequest> requested, IReadOnlyDictionary<Guid, Catalogue.Product> products, DateTimeOffset at, CancellationToken ct, decimal pointsOff = 0)
    {
        var rules = (await SaleOffers.RunningAsync(db, at, ct)).Select(SaleOffers.ToRule).ToList();
        var cart = requested.Where(l => l.ProductId is not null).GroupBy(l => l.ProductId!.Value)
            .Select(g => new Promotions.Line(g.Key, products.GetValueOrDefault(g.Key)?.Category, products.GetValueOrDefault(g.Key)?.SellingPrice ?? 0, g.Sum(l => l.Quantity))).ToList();
        var priced = Promotions.Apply(cart, rules, at);
        var floor = priced.Parts.Sum(ps => ps.Sum(p => Money.LineAmount(p.Quantity, p.UnitPrice))) - (priced.Bill?.Amount ?? 0) - pointsOff;
        var asked = requested.Where(l => l.ProductId is not null).Sum(l => Money.LineAmount(l.Quantity, l.UnitPrice));
        return asked >= floor - (0.01m * requested.Count);
    }

    private async Task<int> NextBillNumberAsync(string year, CancellationToken cancellationToken)
    {
        var number = (await db.Database.SqlQuery<int>($"""
            INSERT INTO bill_counters (shop_id, financial_year, last_number)
            VALUES (NULLIF(current_setting('app.shop_id', true), '')::uuid, {year}, 1)
            ON CONFLICT (shop_id, financial_year) DO UPDATE SET last_number = bill_counters.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync(cancellationToken)).Single();
        return number;
    }
}
