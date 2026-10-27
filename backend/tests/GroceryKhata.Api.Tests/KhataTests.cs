using System.Net;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class KhataTests(ApiFactory api)
{
    [Fact]
    public async Task Walking_skeleton_sign_up_add_product_bill_on_khata_see_statement()
    {
        var (client, auth) = await api.SignUpAsync();
        Assert.NotNull(auth.ShopId);

        var atta = Guid.CreateVersion7();
        await (await client.PostJsonAsync("/api/v1/products", new { id = atta, name = "Aashirvaad Atta", unit = "kg", sellingPrice = 44m }))
            .ReadAsync<ProductDto>();
        var customer = await CreateCustomerAsync(client, "Suresh");

        var bill = await (await client.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            customerId = customer,
            lines = new object[]
            {
                new { productId = atta, quantity = 2.5m, unitPrice = 44m },
                new { name = "Parle-G", quantity = 2m, unit = "packet", unitPrice = 10m },
            },
            paidAmount = 30m,
            paymentMode = "upi",
        })).ReadAsync<BillDto>();

        Assert.Equal(130m, bill.Total);
        Assert.Equal(100m, bill.OnKhata);
        Assert.Equal(100m, bill.CustomerBalance);
        Assert.Equal("Aashirvaad Atta", bill.Lines[0].Name);
        Assert.Equal(Unit.Kg, bill.Lines[0].Unit);

        var statement = await client.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal(100m, statement.Customer.Balance);
        Assert.Collection(statement.Entries,
            e => Assert.Equal((LedgerEntryKind.Credit, 130m, 130m), (e.Kind, e.Amount, e.RunningBalance)),
            e => Assert.Equal((LedgerEntryKind.Payment, -30m, 100m, PaymentMode.Upi), (e.Kind, e.Amount, e.RunningBalance, e.PaymentMode!.Value)));
    }

    [Fact]
    public async Task Amount_only_khata_entries_move_the_balance_both_ways()
    {
        var (client, _) = await api.SignUpAsync();
        var customer = await CreateCustomerAsync(client, "Ramesh");

        var credit = await AddEntryAsync(client, customer, "credit", 340m);
        Assert.Equal(340m, credit.CustomerBalance);
        Assert.Equal(340m, credit.Entry.Amount);

        var payment = await AddEntryAsync(client, customer, "payment", 100m);
        Assert.Equal(240m, payment.CustomerBalance);
        Assert.Equal(-100m, payment.Entry.Amount);
        Assert.Equal(PaymentMode.Cash, payment.Entry.PaymentMode);
    }

    [Fact]
    public async Task Sending_the_same_entry_twice_saves_it_once()
    {
        var (client, _) = await api.SignUpAsync();
        var customer = await CreateCustomerAsync(client, "Ramesh");
        var entry = new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount = 250m };

        var first = await client.PostJsonAsync("/api/v1/khata-entries", entry);
        var second = await client.PostJsonAsync("/api/v1/khata-entries", entry);
        var parallel = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostJsonAsync("/api/v1/khata-entries", entry)));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.All(parallel, r => Assert.True(r.IsSuccessStatusCode));
        var statement = await client.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Single(statement.Entries);
        Assert.Equal(250m, statement.Customer.Balance);
    }

    [Fact]
    public async Task Sending_the_same_bill_twice_charges_the_customer_once()
    {
        var (client, _) = await api.SignUpAsync();
        var customer = await CreateCustomerAsync(client, "Ramesh");
        var bill = new
        {
            id = Guid.CreateVersion7(),
            customerId = customer,
            lines = new[] { new { name = "Toor dal", quantity = 1m, unit = "kg", unitPrice = 160m } },
            paidAmount = 0m,
        };

        await (await client.PostJsonAsync("/api/v1/bills", bill)).ReadAsync<BillDto>();
        var replay = await client.PostJsonAsync("/api/v1/bills", bill);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(160m, (await client.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).Customer.Balance);
    }

    [Fact]
    public async Task A_cash_sale_without_a_customer_must_be_paid_in_full()
    {
        var (client, _) = await api.SignUpAsync();
        var lines = new[] { new { name = "Sugar", quantity = 1m, unit = "kg", unitPrice = 48m } };

        var unpaid = await client.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines, paidAmount = 0m });
        var paid = await client.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines, paidAmount = 48m });

        Assert.Equal(HttpStatusCode.BadRequest, unpaid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
    }

    [Fact]
    public async Task Corrections_are_reversals_and_an_entry_can_be_reversed_only_once()
    {
        var (client, _) = await api.SignUpAsync();
        var customer = await CreateCustomerAsync(client, "Ramesh");
        var wrong = await AddEntryAsync(client, customer, "credit", 500m);

        var reversal = await (await client.PostJsonAsync($"/api/v1/ledger-entries/{wrong.Entry.Id}/reverse",
            new { id = Guid.CreateVersion7(), note = "typed 500 instead of 50" })).ReadAsync<KhataEntryResponse>();
        var again = await client.PostJsonAsync($"/api/v1/ledger-entries/{wrong.Entry.Id}/reverse", new { id = Guid.CreateVersion7() });
        var reverseTheReversal = await client.PostJsonAsync($"/api/v1/ledger-entries/{reversal.Entry.Id}/reverse", new { id = Guid.CreateVersion7() });

        Assert.Equal(0m, reversal.CustomerBalance);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reverseTheReversal.StatusCode);

        var statement = await client.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal(2, statement.Entries.Count);
        Assert.True(statement.Entries.Single(e => e.Id == wrong.Entry.Id).IsReversed);
    }

    [Fact]
    public async Task Balances_always_equal_the_sum_of_entries()
    {
        var (client, _) = await api.SignUpAsync();
        var customers = new[] { await CreateCustomerAsync(client, "A"), await CreateCustomerAsync(client, "B"), await CreateCustomerAsync(client, "C") };
        var random = new Random(42);
        var expected = customers.ToDictionary(c => c, _ => 0m);
        var entries = new List<Guid>();

        for (var i = 0; i < 60; i++)
        {
            var customer = customers[random.Next(customers.Length)];
            switch (random.Next(3))
            {
                case 0:
                case 1:
                    var amount = random.Next(1, 100_000) / 100m;
                    var isPayment = random.Next(2) == 0;
                    var added = await AddEntryAsync(client, customer, isPayment ? "payment" : "credit", amount);
                    expected[customer] += isPayment ? -amount : amount;
                    entries.Add(added.Entry.Id);
                    break;
                case 2 when entries.Count > 0:
                    var target = entries[random.Next(entries.Count)];
                    entries.Remove(target);
                    var reversed = await (await client.PostJsonAsync($"/api/v1/ledger-entries/{target}/reverse", new { id = Guid.CreateVersion7() }))
                        .ReadAsync<KhataEntryResponse>();
                    expected[reversed.Entry.CustomerId] += reversed.Entry.Amount;
                    break;
            }
        }

        foreach (var customer in customers)
        {
            var statement = await client.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
            Assert.Equal(expected[customer], statement.Customer.Balance);
            Assert.Equal(expected[customer], statement.Entries.Sum(e => e.Amount));
        }

        Assert.Empty(await client.GetJsonAsync<List<BalanceMismatch>>("/api/v1/ledger/reconcile"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.555)]
    public async Task Invalid_amounts_are_refused(decimal amount)
    {
        var (client, _) = await api.SignUpAsync();
        var customer = await CreateCustomerAsync(client, "Ramesh");

        var response = await client.PostJsonAsync("/api/v1/khata-entries",
            new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    internal static async Task<Guid> CreateCustomerAsync(HttpClient client, string name)
    {
        var id = Guid.CreateVersion7();
        await (await client.PostJsonAsync("/api/v1/customers", new { id, name, phone = ApiFactory.NewPhone() })).ReadAsync<CustomerDto>();
        return id;
    }

    internal static async Task<KhataEntryResponse> AddEntryAsync(HttpClient client, Guid customerId, string type, decimal amount) =>
        await (await client.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId, type, amount }))
            .ReadAsync<KhataEntryResponse>();
}
