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

        // Counter two types the slip number (or scans its QR, which is the id).
        var slip = await counter2.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/find?no={Uri.EscapeDataString(note.NoteNo)}");
        Assert.Equal((note.Id, 699m, "open", sale.BillNo), (slip.Id, slip.Amount, slip.Status, slip.BillNo));
        Assert.Equal(note.Id, (await counter2.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/find?no={note.Id}")).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await counter2.GetAsync("/api/v1/exchange-slips/find?no=9999")).StatusCode);

        // A ₹1,499 saree: the slip pays ₹699, ₹800 to pay.
        var newBill = await (await counter2.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = 1499m } }, paidAmount = 800m, paymentMode = "cash", exchangeNoteId = slip.Id })).ReadAsync<BillDto>();
        var used = await owner.GetJsonAsync<ExchangeSlip>($"/api/v1/exchange-slips/find?no={note.Id}");
        Assert.Equal(("used", newBill.BillNo), (used.Status, used.UsedOnBillNo));

        // The same slip again, at any counter: refused.
        var again = await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Dupatta", quantity = 1m, unitPrice = 699m } }, paidAmount = 0m, paymentMode = "cash", exchangeNoteId = slip.Id });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(await owner.GetJsonAsync<List<ExchangeSlip>>("/api/v1/exchange-slips"), s => s.Id == note.Id && s.Status == "used");
    }

    [Fact]
    public void A_slip_lasts_ninety_days_by_india_date()
    {
        var issued = new DateTimeOffset(2026, 11, 17, 20, 0, 0, TimeSpan.Zero); // 18 Nov 1:30 am in India
        Assert.Equal(new DateOnly(2027, 2, 16), ExchangeSlips.ValidTill(issued));
        Assert.False(ExchangeSlips.Expired(issued, new DateTimeOffset(2027, 2, 16, 18, 0, 0, TimeSpan.Zero)));
        Assert.True(ExchangeSlips.Expired(issued, new DateTimeOffset(2027, 2, 16, 19, 0, 0, TimeSpan.Zero)));
    }
}
