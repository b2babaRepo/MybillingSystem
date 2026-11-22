using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ExchangeSlipTests(ApiFactory api)
{
    [Fact]
    public async Task A_slip_from_the_returns_counter_is_found_by_number_at_another_counter_and_used_only_once()
    {
        var (owner, _) = await api.SignUpAsync("Slip Store");
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Counter Two", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var counter2 = api.CreateClient();
        counter2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var kurti = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/products", new { id = kurti, name = "Kurti", unit = "piece", sellingPrice = 699m, openingStock = 5m })).EnsureSuccessStatusCode();
        var sale = await (await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = kurti, quantity = 1m, unitPrice = 699m } }, paidAmount = 699m, paymentMode = "cash" })).ReadAsync<BillDto>();
        var note = await (await owner.PostJsonAsync($"/api/v1/bills/{sale.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Exchange", lines = new[] { new { lineNo = 1, quantity = 1m } }, exchange = true })).ReadAsync<CreditNoteDto>();

        // The owner's copy carries the slip's secret code; counter two types the number with it, or scans the QR.
        var code = (await owner.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/{note.Id}")).Code!;
        Assert.Matches("^[0-9]{4}$", code);
        var slip = await counter2.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/find?no={Uri.EscapeDataString(note.NoteNo)}&code={code}");
        Assert.Equal((note.Id, 699m, "open", sale.BillNo, (string?)null), (slip.Id, slip.Amount, slip.Status, slip.BillNo, slip.Code));
        Assert.Equal(note.Id, (await counter2.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/find?no={Uri.EscapeDataString($"GKSLIP:{note.Id}:{code}")}")).Id);
        // The number alone, or with a wrong code, finds nothing: slip numbers can't be guessed.
        var wrong = code == "0000" ? "1111" : "0000";
        Assert.Equal(HttpStatusCode.NotFound, (await counter2.GetAsync($"/api/v1/exchange-slips/find?no={Uri.EscapeDataString(note.NoteNo)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await counter2.GetAsync($"/api/v1/exchange-slips/find?no={Uri.EscapeDataString(note.NoteNo)}&code={wrong}")).StatusCode);
        // Helpers don't get the slips list or the code.
        Assert.Equal(HttpStatusCode.Forbidden, (await counter2.GetAsync($"/api/v1/exchange-slips/{note.Id}")).StatusCode);

        // A ₹1,499 saree: the slip pays ₹699, ₹800 to pay.
        var newBill = await (await counter2.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = 1499m } }, paidAmount = 800m, paymentMode = "cash", exchangeNoteId = slip.Id })).ReadAsync<BillDto>();
        var used = await owner.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/{note.Id}");
        Assert.Equal(("used", newBill.BillNo), (used.Status, used.UsedOnBillNo));

        // The same slip again, at any counter: refused.
        var again = await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Dupatta", quantity = 1m, unitPrice = 699m } }, paidAmount = 0m, paymentMode = "cash", exchangeNoteId = slip.Id });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(await owner.GetJsonAsync<List<ExchangeSlip>>("/api/v1/exchange-slips"), s => s.Id == note.Id && s.Status == "used");
    }

    [Fact]
    public async Task What_a_smaller_bill_leaves_stays_on_the_slip_for_the_next_bill_instead_of_cash_change()
    {
        var (shop, _) = await api.SignUpAsync("Slip Balance Store");
        var sale = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Kurti", quantity = 1m, unitPrice = 699m } }, paidAmount = 699m, paymentMode = "cash" })).ReadAsync<BillDto>();
        var note = await (await shop.PostJsonAsync($"/api/v1/bills/{sale.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Exchange", lines = new[] { new { lineNo = 1, quantity = 1m } }, exchange = true, restock = false })).ReadAsync<CreditNoteDto>();

        // A ₹599 frock: the slip pays all of it, nothing to pay, ₹100 stays on the slip.
        var frock = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Frock", quantity = 1m, unitPrice = 599m } }, paidAmount = 0m, paymentMode = "cash", exchangeNoteId = note.Id })).ReadAsync<BillDto>();
        Assert.Equal(599m, frock.ExchangeCredit);
        var slip = await shop.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/{note.Id}");
        Assert.Equal(("open", 100m, 699m, frock.BillNo), (slip.Status, slip.Balance, slip.Amount, slip.UsedOnBillNo));

        // Next visit: a ₹1,499 saree, the slip's ₹100 off, ₹1,399 to pay; then the slip is used up.
        var saree = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = 1499m } }, paidAmount = 1399m, paymentMode = "cash", exchangeNoteId = note.Id })).ReadAsync<BillDto>();
        Assert.Equal(100m, saree.ExchangeCredit);
        slip = await shop.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/{note.Id}");
        Assert.Equal(("used", 0m), (slip.Status, slip.Balance));
    }

    [Fact]
    public async Task A_lost_slip_gets_a_new_code_after_the_mobile_matches_and_the_owner_can_cancel_a_slip()
    {
        var (owner, _) = await api.SignUpAsync("Lost Slip Store");
        var customer = await (await owner.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "Meena", phone = "9811100099" })).ReadAsync<CustomerDto>();
        var sale = await (await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer.Id, lines = new[] { new { name = "Kurti", quantity = 1m, unitPrice = 699m } }, paidAmount = 699m, paymentMode = "cash" })).ReadAsync<BillDto>();
        var note = await (await owner.PostJsonAsync($"/api/v1/bills/{sale.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Exchange", lines = new[] { new { lineNo = 1, quantity = 1m } }, exchange = true, restock = false })).ReadAsync<CreditNoteDto>();
        // A shop without a GSTIN: a return note, not a GST credit note.
        Assert.StartsWith("RN/", note.NoteNo);
        var old = (await owner.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/{note.Id}")).Code!;

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostJsonAsync($"/api/v1/exchange-slips/{note.Id}/new-code", new { phone = "9000000000" })).StatusCode);
        var fresh = await (await owner.PostJsonAsync($"/api/v1/exchange-slips/{note.Id}/new-code", new { phone = "+91 98111 00099" })).ReadAsync<ExchangeSlip>();
        Assert.NotNull(fresh.Code);
        if (fresh.Code != old)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/exchange-slips/find?no={note.Id}&code={old}")).StatusCode);
        }

        Assert.Equal(note.Id, (await owner.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/find?no={note.Id}&code={fresh.Code}")).Id);

        var cancelled = await (await owner.PostJsonAsync($"/api/v1/exchange-slips/{note.Id}/cancel", new { reason = "Given by mistake" })).ReadAsync<ExchangeSlip>();
        Assert.Equal(("cancelled", 0m), (cancelled.Status, cancelled.Balance));
        var bill = await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer.Id, lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = 1499m } }, paidAmount = 1499m, paymentMode = "cash", exchangeNoteId = note.Id });
        Assert.Equal(HttpStatusCode.BadRequest, bill.StatusCode);
    }

    [Fact]
    public async Task A_helper_can_not_use_a_slip_above_the_shops_limit()
    {
        var (owner, _) = await api.SignUpAsync("Slip Limit Store");
        (await owner.PutJsonAsync("/api/v1/shop", new { name = "Slip Limit Store", exchangeSlipHelperMax = 500m })).EnsureSuccessStatusCode();
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var sale = await (await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Kurti", quantity = 1m, unitPrice = 699m } }, paidAmount = 699m, paymentMode = "cash" })).ReadAsync<BillDto>();
        var note = await (await owner.PostJsonAsync($"/api/v1/bills/{sale.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Exchange", lines = new[] { new { lineNo = 1, quantity = 1m } }, exchange = true, restock = false })).ReadAsync<CreditNoteDto>();

        var big = await helper.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = 1499m } }, paidAmount = 800m, paymentMode = "cash", exchangeNoteId = note.Id });
        Assert.Equal(HttpStatusCode.BadRequest, big.StatusCode);
        (await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = 1499m } }, paidAmount = 800m, paymentMode = "cash", exchangeNoteId = note.Id })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_shop_sets_how_many_days_its_slips_last()
    {
        var (shop, _) = await api.SignUpAsync("Slip Days Store");
        Assert.Equal(90, (await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop")).ExchangeSlipDays);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutJsonAsync("/api/v1/shop", new { name = "Slip Days Store", exchangeSlipDays = 0 })).StatusCode);
        (await shop.PutJsonAsync("/api/v1/shop", new { name = "Slip Days Store", exchangeSlipDays = 180 })).EnsureSuccessStatusCode();

        var sale = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Kurti", quantity = 1m, unitPrice = 699m } }, paidAmount = 699m, paymentMode = "cash" })).ReadAsync<BillDto>();
        var note = await (await shop.PostJsonAsync($"/api/v1/bills/{sale.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Exchange", lines = new[] { new { lineNo = 1, quantity = 1m } }, exchange = true, restock = false })).ReadAsync<CreditNoteDto>();
        var slip = await shop.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/{note.Id}");
        Assert.Equal(ExchangeSlips.ValidTill(slip.IssuedAt, 180), slip.ValidTill);
    }

    [Fact]
    public void A_slip_lasts_ninety_days_by_india_date()
    {
        Assert.Equal(new DateOnly(2026, 11, 25), ExchangeSlips.ValidTill(new DateTimeOffset(2026, 11, 18, 6, 0, 0, TimeSpan.Zero), 7));
        var issued = new DateTimeOffset(2026, 11, 17, 20, 0, 0, TimeSpan.Zero); // 18 Nov 1:30 am in India
        Assert.Equal(new DateOnly(2027, 2, 16), ExchangeSlips.ValidTill(issued));
        Assert.False(ExchangeSlips.Expired(issued, new DateTimeOffset(2027, 2, 16, 18, 0, 0, TimeSpan.Zero)));
        Assert.True(ExchangeSlips.Expired(issued, new DateTimeOffset(2027, 2, 16, 19, 0, 0, TimeSpan.Zero)));
    }
}
