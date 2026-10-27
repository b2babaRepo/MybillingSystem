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
        if (await db.Users.AnyAsync(u => u.Phone == PhoneNumber.Normalize(seed.Shop.Phone)))
        {
            return;
        }

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
                Items = items.Select(i => new ShoppingListItem { Name = i.Name, Quantity = i.Quantity, Unit = i.Unit }).ToList(),
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

        logger.LogWarning("DEVELOPMENT demo data seeded: logins are in demo-seed.json");
    }

    private sealed record DemoSeed(SeedShop Shop, SeedHousehold Household, List<SeedProduct> Products, List<SeedCustomer> Customers, SeedLists Lists);

    private sealed record SeedShop(string ShopName, string Area, string OwnerName, string Phone, string Password);

    private sealed record SeedHousehold(string FullName, string Phone, string Password);

    private sealed record SeedProduct(string Name, Unit Unit, decimal Price, string? Category, decimal? Stock, decimal? ReorderLevel);

    private sealed record SeedCustomer(string Name, string? Phone, List<decimal> Udhaar, List<decimal> Paid);

    private sealed record SeedItem(string Name, decimal Quantity, Unit Unit);

    private sealed record SeedLists(List<SeedItem> Monthly, List<SeedItem> Weekly);
}
