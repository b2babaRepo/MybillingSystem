using System.Text.Json;
using System.Text.Json.Serialization;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Persistence;

/// <summary>
/// DEVELOPMENT ONLY: fills an empty local database with one shop and one household from demo-seed.json,
/// through the same services the app uses, so the data obeys every ledger and tenancy rule. Runs once:
/// skipped when the shop's phone already has an account.
/// </summary>
public static class DemoSeeder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static async Task RunAsync(IServiceProvider services, string contentRoot, ILogger logger)
    {
        var path = Path.Combine(contentRoot, "demo-seed.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Demo seed skipped: {Path} not found", path);
            return;
        }

        var seed = JsonSerializer.Deserialize<DemoSeed>(await File.ReadAllTextAsync(path), Json)!;
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        if (!await db.Users.AnyAsync(u => u.Phone == PhoneNumber.Normalize(seed.Shop.Phone)))
        {
            await SeedMainShopAsync(sp, seed);
        }

        // The rest of the common catalogue in the main shop, and the other shops nearby with their own prices, so
        // a household can compare shops. Each step is skipped when already done, so it also tops up an older seed.
        await StockCatalogueAsync(services, seed.Shop.Phone, seed.Shop.ShopName, 1m);
        await SeedKhataBillsAsync(services, seed.Shop.Phone);
        await SeedHouseholdKhataAsync(services, seed.Shop.Phone, seed.Household.FullName, seed.Household.Phone);
        await SeedOptionsAsync(services, seed.Shop.Phone);
        await SeedBuyingAsync(services, seed.Shop.Phone);
        if (seed.Staff is { } staff)
        {
            await SeedStaffAsync(services, seed.Shop.Phone, staff);
        }
        foreach (var other in seed.OtherShops)
        {
            if (!await db.Users.AnyAsync(u => u.Phone == PhoneNumber.Normalize(other.Phone)))
            {
                await using var signUp = services.CreateAsyncScope();
                await signUp.ServiceProvider.GetRequiredService<AuthService>().SignUpAsync(
                    new SignUpRequest(other.ShopName, other.OwnerName, other.Phone, other.Password, "hi", other.Area), CancellationToken.None);
            }

            await StockCatalogueAsync(services, other.Phone, other.ShopName, other.PriceFactor);
        }

        logger.LogWarning("DEVELOPMENT demo data ready: logins are in demo-seed.json");
    }

    /// <summary>
    /// Adds every common-catalogue item the shop doesn't sell yet, at the typical price times the shop's factor
    /// (one shop a little cheaper, one dearer), with some stock. Import skips names the shop already has.
    /// </summary>
    private static async Task StockCatalogueAsync(IServiceProvider services, string phone, string shopName, decimal priceFactor)
    {
        // A scope of its own: a scope works as one shop only.
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var normalized = PhoneNumber.Normalize(phone);
        var shopId = await db.Memberships.AsNoTracking()
            .Where(m => db.Users.Any(u => u.Id == m.UserId && u.Phone == normalized) && db.Shops.Any(s => s.Id == m.ShopId && s.Name == shopName))
            .Select(m => new { m.ShopId, m.UserId }).FirstOrDefaultAsync();
        if (shopId is null)
        {
            return;
        }

        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(shopId.ShopId);
        tenant.SetUser(shopId.UserId);
        var sold = (await db.Products.AsNoTracking().Select(p => p.Name).ToListAsync())
            .Select(CommonCatalogue.Match).OfType<int>().ToHashSet();
        var rows = CommonCatalogue.Items.Where(i => !sold.Contains(i.Id)).Select((i, n) => new ImportRow(
            i.Name, i.Unit, decimal.Round(i.TypicalPrice * priceFactor, i.TypicalPrice < 5 ? 2 : 0), null, i.Category,
            i.Unit is Unit.G ? 500 : 10 + n % 7 * 5, i.Unit is Unit.G ? 100 : 5)).ToList();
        if (rows.Count > 0)
        {
            await sp.GetRequiredService<StockService>().ImportAsync(new ImportRequest(rows), shopId.UserId, CancellationToken.None);
        }
    }

    /// <summary>
    /// The buying side, so its screens show something: two suppliers who both sell one item, an order already late, a
    /// draft order, stock kept aside at the counter, a godown holding some goods, and empty bags. Skipped once the shop
    /// has a godown.
    /// </summary>
    private static async Task SeedBuyingAsync(IServiceProvider services, string phone)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var normalized = PhoneNumber.Normalize(phone);
        var owner = await db.Memberships.AsNoTracking().Where(m => db.Users.Any(u => u.Id == m.UserId && u.Phone == normalized))
            .OrderBy(m => m.CreatedAt).Select(m => new { m.ShopId, m.UserId }).FirstOrDefaultAsync();
        if (owner is null)
        {
            return;
        }

        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(owner.ShopId);
        tenant.SetUser(owner.UserId);
        if (await db.Godowns.AnyAsync())
        {
            return;
        }

        var items = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService && p.SellingPrice > 0 && p.Stock > 5).OrderBy(p => p.CreatedAt).Take(4).ToListAsync();
        if (items.Count < 4)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var today = IndiaTime.Today(TimeProvider.System);
        var suppliers = await db.Suppliers.OrderBy(x => x.CreatedAt).Take(2).ToListAsync();
        foreach (var name in new[] { "Agarwal Wholesale", "Gupta Distributors" }.Skip(suppliers.Count))
        {
            var supplier = new Supplier { Name = name, Phone = name.StartsWith('A') ? "9000000101" : "9000000102", PaymentDays = 15 };
            db.Suppliers.Add(supplier);
            suppliers.Add(supplier);
        }

        var number = await db.PurchaseOrders.MaxAsync(o => (int?)o.Number) ?? 0;
        PurchaseOrder Order(Supplier supplier, PurchaseOrderStatus status, DateOnly? expected, string? note, params (Product Item, decimal Quantity)[] lines) => new()
        {
            Number = ++number, SupplierId = supplier.Id, Status = status, ExpectedOn = expected, Note = note, CreatedByUserId = owner.UserId,
            Lines = lines.Select(l => new PurchaseOrderLine { ProductId = l.Item.Id, Quantity = l.Quantity, Rate = decimal.Round(l.Item.SellingPrice * 0.8m, 0) }).ToList(),
        };
        // Both suppliers have the first item on order: the order is split, and one of them is three days late.
        db.PurchaseOrders.AddRange(
            Order(suppliers[0], PurchaseOrderStatus.Open, today.AddDays(-3), null, (items[0], 10), (items[1], 6)),
            Order(suppliers[1], PurchaseOrderStatus.Open, today.AddDays(2), null, (items[0], 6)),
            Order(suppliers[0], PurchaseOrderStatus.Draft, null, "Festival stock", (items[2], 20)));
        db.StockHolds.Add(new StockHold
        {
            ProductId = items[3].Id, Quantity = 2, PartyName = "Mohan Lal", Until = today.AddDays(1), Note = "Will collect tomorrow", Status = HoldStatus.Active,
            CreatedByUserId = owner.UserId,
        });

        var godown = new Godown { Name = "Back room" };
        db.Godowns.Add(godown);
        db.StockTransfers.Add(new StockTransfer
        {
            Number = 1, ToGodownId = godown.Id, Note = "Extra stock kept at the back", OccurredAt = now, CreatedByUserId = owner.UserId,
            Lines = items.Take(2).Select(i => new StockTransferLine { ProductId = i.Id, Quantity = Math.Floor(i.Stock / 2) }).ToList(),
        });

        var jute = new BagType { Name = "Jute 50 kg", SaleRate = 12, FromGoods = true };
        db.BagTypes.Add(jute);
        db.BagMoves.AddRange(
            new BagMove { BagTypeId = jute.Id, Kind = BagMoveKind.CameWithGoods, Quantity = 40, SupplierId = suppliers[0].Id, OccurredAt = now.AddDays(-6), CreatedByUserId = owner.UserId },
            new BagMove { BagTypeId = jute.Id, Kind = BagMoveKind.ReturnedToSupplier, Quantity = -10, SupplierId = suppliers[0].Id, OccurredAt = now.AddDays(-2), CreatedByUserId = owner.UserId });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Itemised bills on khata for the customers who owe most, so a customer's page shows what they took, when and
    /// at what price. Skipped once three customers have khata bills.
    /// </summary>
    private static async Task SeedKhataBillsAsync(IServiceProvider services, string phone)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var normalized = PhoneNumber.Normalize(phone);
        var owner = await db.Memberships.AsNoTracking().Where(m => db.Users.Any(u => u.Id == m.UserId && u.Phone == normalized))
            .OrderBy(m => m.CreatedAt).Select(m => new { m.ShopId, m.UserId }).FirstOrDefaultAsync();
        if (owner is null)
        {
            return;
        }

        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(owner.ShopId);
        tenant.SetUser(owner.UserId);
        if (await db.Bills.Where(b => b.CustomerId != null).Select(b => b.CustomerId).Distinct().CountAsync() >= 3)
        {
            return;
        }

        var products = await db.Products.AsNoTracking().Where(p => p.SellingPrice > 0 && p.Unit != Unit.G).OrderBy(p => p.CreatedAt).ToListAsync();
        var customers = await db.Customers.AsNoTracking().OrderByDescending(c => c.Balance).Take(3).ToListAsync();
        var ledger = sp.GetRequiredService<LedgerService>();
        var n = 0;
        foreach (var customer in customers)
        {
            for (var bill = 0; bill < 2; bill++)
            {
                var lines = Enumerable.Range(0, 3).Select(i => products[(n * 3 + i) % products.Count])
                    .Select((p, i) => new BillLineRequest(p.Id, null, 1 + (n + i) % 3, null, p.SellingPrice)).ToList();
                n++;
                await ledger.CreateBillAsync(new BillRequest(Guid.CreateVersion7(), customer.Id, lines, 0, null, null), owner.UserId, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// The demo household is also a khata customer at the main shop (same phone), so its app shows "My khata"
    /// with a bill and a part payment. Skipped once that customer exists.
    /// </summary>
    private static async Task SeedHouseholdKhataAsync(IServiceProvider services, string shopPhone, string name, string phone)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var owner = PhoneNumber.Normalize(shopPhone);
        var shop = await db.Memberships.AsNoTracking().Where(m => db.Users.Any(u => u.Id == m.UserId && u.Phone == owner))
            .OrderBy(m => m.CreatedAt).Select(m => new { m.ShopId, m.UserId }).FirstOrDefaultAsync();
        if (shop is null)
        {
            return;
        }

        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(shop.ShopId);
        tenant.SetUser(shop.UserId);
        var normalized = PhoneNumber.Normalize(phone);
        if (await db.Customers.AnyAsync(c => c.Phone == normalized))
        {
            return;
        }

        var ledger = sp.GetRequiredService<LedgerService>();
        var customer = (await ledger.CreateCustomerAsync(new CustomerRequest(Guid.CreateVersion7(), name, phone), CancellationToken.None)).Value!;
        var products = await db.Products.AsNoTracking().Where(p => p.SellingPrice > 0 && p.Unit != Unit.G).OrderBy(p => p.CreatedAt).Take(4).ToListAsync();
        await ledger.CreateBillAsync(new BillRequest(Guid.CreateVersion7(), customer.Id,
            products.Select((p, i) => new BillLineRequest(p.Id, null, 1 + i % 2, null, p.SellingPrice)).ToList(), 0, null, null), shop.UserId, CancellationToken.None);
        await ledger.AddKhataEntryAsync(new KhataEntryRequest(Guid.CreateVersion7(), customer.Id, KhataEntryType.Payment, 200, PaymentMode.Upi, null, null), shop.UserId, CancellationToken.None);
    }

    /// <summary>
    /// Brands and pack sizes for a few items at the main shop, so a household can pick an exact option or leave it
    /// to the shop. Skipped once the shop has any packed option.
    /// </summary>
    private static async Task SeedOptionsAsync(IServiceProvider services, string phone)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var normalized = PhoneNumber.Normalize(phone);
        var owner = await db.Memberships.AsNoTracking().Where(m => db.Users.Any(u => u.Id == m.UserId && u.Phone == normalized))
            .OrderBy(m => m.CreatedAt).Select(m => new { m.ShopId, m.UserId }).FirstOrDefaultAsync();
        if (owner is null)
        {
            return;
        }

        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(owner.ShopId);
        tenant.SetUser(owner.UserId);
        if (await db.Products.AnyAsync(p => p.PackSize != null))
        {
            return;
        }

        int Item(string en) => CommonCatalogue.Items.Single(i => i.NameEn == en).Id;
        ImportRow Pack(string name, string en, decimal price, decimal size, Unit packUnit, decimal stock) =>
            new(name, Unit.Packet, price, null, CommonCatalogue.Items.Single(i => i.Id == Item(en)).Category, stock, 2, Item(en), size, packUnit);
        await sp.GetRequiredService<StockService>().ImportAsync(new ImportRequest(
        [
            Pack("Aashirvaad atta 5 kg", "Atta", 265, 5, Unit.Kg, 12),
            Pack("Pillsbury atta 5 kg", "Atta", 249, 5, Unit.Kg, 8),
            Pack("Aashirvaad atta 10 kg", "Atta", 499, 10, Unit.Kg, 4),
            Pack("Fortune mustard oil 1 L", "Mustard oil", 165, 1, Unit.Litre, 20),
            Pack("Dhara mustard oil 1 L", "Mustard oil", 172, 1, Unit.Litre, 10),
            Pack("Tata salt 1 kg", "Salt", 28, 1, Unit.Kg, 30),
            Pack("Tata Tea Premium 500 g", "Tea", 285, 500, Unit.G, 10),
            Pack("Red Label 250 g", "Tea", 140, 250, Unit.G, 12),
        ]), owner.UserId, CancellationToken.None);
    }

    /// <summary>A helper at the main shop who signs in with phone + PIN. Skipped once that phone has an account.</summary>
    private static async Task SeedStaffAsync(IServiceProvider services, string shopPhone, SeedStaff staff)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phone = PhoneNumber.Normalize(staff.Phone);
        var owner = PhoneNumber.Normalize(shopPhone);
        var shopId = await db.Memberships.Where(m => db.Users.Any(u => u.Id == m.UserId && u.Phone == owner)).OrderBy(m => m.CreatedAt).Select(m => (Guid?)m.ShopId).FirstOrDefaultAsync();
        if (shopId is null || await db.Users.AnyAsync(u => u.Phone == phone))
        {
            return;
        }

        var user = new User { Phone = phone!, FullName = staff.FullName };
        db.Users.Add(user);
        db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            ShopId = shopId.Value,
            Role = ShopRole.Staff,
            PinHash = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>().HashPassword(user, staff.Pin),
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedMainShopAsync(IServiceProvider sp, DemoSeed seed)
    {
        var db = sp.GetRequiredService<AppDbContext>();

        var auth = sp.GetRequiredService<AuthService>();
        var shop = (await auth.SignUpAsync(
            new SignUpRequest(seed.Shop.ShopName, seed.Shop.OwnerName, seed.Shop.Phone, seed.Shop.Password, "hi", seed.Shop.Area),
            CancellationToken.None)).Value!;
        var home = (await auth.RegisterAsync(
            new RegisterRequest(seed.Household.FullName, seed.Household.Phone, seed.Household.Password, "hi"),
            CancellationToken.None)).Value!;

        // Shop data: work as the shop, like a signed-in owner would.
        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(shop.ShopId!.Value);
        tenant.SetUser(shop.User.Id);
        var ledger = sp.GetRequiredService<LedgerService>();
        var ownerId = shop.User.Id;

        // Through the import path, so opening stock is recorded as movements like any real shop's.
        await sp.GetRequiredService<StockService>().ImportAsync(
            new ImportRequest(seed.Products.Select(p => new ImportRow(p.Name, p.Unit, p.Price, null, p.Category, p.Stock, p.ReorderLevel)).ToList()),
            ownerId, CancellationToken.None);
        var products = await db.Products.AsNoTracking().OrderBy(p => p.CreatedAt).ToListAsync();

        foreach (var c in seed.Customers)
        {
            var customer = (await ledger.CreateCustomerAsync(new CustomerRequest(Guid.CreateVersion7(), c.Name, c.Phone), CancellationToken.None)).Value!;
            foreach (var amount in c.Udhaar)
            {
                await ledger.AddKhataEntryAsync(new KhataEntryRequest(Guid.CreateVersion7(), customer.Id, KhataEntryType.Credit, amount, null, null, null), ownerId, CancellationToken.None);
            }

            foreach (var amount in c.Paid)
            {
                await ledger.AddKhataEntryAsync(new KhataEntryRequest(Guid.CreateVersion7(), customer.Id, KhataEntryType.Payment, amount, PaymentMode.Cash, null, null), ownerId, CancellationToken.None);
            }
        }

        // Two of today's walk-in sales, so the home screen has numbers.
        var atta = products.Single(p => p.Name == seed.Products[0].Name);
        var biscuits = products.Single(p => p.Unit == Unit.Packet && p.SellingPrice == 10);
        await ledger.CreateBillAsync(new BillRequest(Guid.CreateVersion7(), null,
            [new BillLineRequest(atta.Id, null, 5, null, atta.SellingPrice), new BillLineRequest(biscuits.Id, null, 4, null, biscuits.SellingPrice)],
            260, PaymentMode.Cash, null), ownerId, CancellationToken.None);
        await ledger.CreateBillAsync(new BillRequest(Guid.CreateVersion7(), null,
            [new BillLineRequest(atta.Id, null, 10, null, atta.SellingPrice)], 440, PaymentMode.Upi, null), ownerId, CancellationToken.None);

        // The household's standing lists.
        foreach (var (kind, items) in new[] { (ListKind.Monthly, seed.Lists.Monthly), (ListKind.Weekly, seed.Lists.Weekly) })
        {
            db.ShoppingLists.Add(new ShoppingList
            {
                UserId = home.User.Id,
                Kind = kind,
                Items = items.Select(i => new ShoppingListItem { Name = i.Name, CatalogueItemId = CommonCatalogue.Match(i.Name), Quantity = i.Quantity, Unit = i.Unit }).ToList(),
            });
        }

        // And the weekly list already sent to the shop, waiting in its inbox.
        var lineNo = 0;
        db.Orders.Add(new Order
        {
            PlacedByUserId = home.User.Id,
            CustomerName = seed.Household.FullName,
            CustomerPhone = PhoneNumber.Normalize(seed.Household.Phone)!,
            ListKind = ListKind.Weekly,
            Fulfilment = Fulfilment.Pickup,
            Slot = "आज शाम 7 बजे",
            Payment = OrderPayment.Upi,
            Lines = seed.Lists.Weekly.Select(i => new OrderLine
            {
                PlacedByUserId = home.User.Id,
                LineNo = ++lineNo,
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit,
            }).ToList(),
        });
        await db.SaveChangesAsync();
    }

    private sealed record DemoSeed(
        SeedShop Shop, SeedHousehold Household, List<SeedProduct> Products, List<SeedCustomer> Customers, SeedLists Lists, List<SeedOtherShop> OtherShops, SeedStaff? Staff = null);

    private sealed record SeedStaff(string FullName, string Phone, string Pin);

    private sealed record SeedOtherShop(string ShopName, string Area, string OwnerName, string Phone, string Password, decimal PriceFactor);

    private sealed record SeedShop(string ShopName, string Area, string OwnerName, string Phone, string Password);

    private sealed record SeedHousehold(string FullName, string Phone, string Password);

    private sealed record SeedProduct(string Name, Unit Unit, decimal Price, string? Category, decimal? Stock, decimal? ReorderLevel);

    private sealed record SeedCustomer(string Name, string? Phone, List<decimal> Udhaar, List<decimal> Paid);

    private sealed record SeedItem(string Name, decimal Quantity, Unit Unit);

    private sealed record SeedLists(List<SeedItem> Monthly, List<SeedItem> Weekly);
}
