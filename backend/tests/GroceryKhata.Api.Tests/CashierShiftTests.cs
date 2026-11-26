using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Cashier shifts: a float to start, the shift's bills, a blind count at the close, and over or short.</summary>
[Collection(ApiCollection.Name)]
public sealed class CashierShiftTests(ApiFactory api)
{
    private async Task<HttpClient> HelperOfAsync(HttpClient owner)
    {
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return helper;
    }

    private static Task<HttpResponseMessage> BillAsync(HttpClient till, Guid? shiftId, decimal total, object[]? payments = null, string mode = "cash") =>
        till.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = total } },
            paidAmount = total, paymentMode = mode, payments, counter = "Counter 1", shiftId,
        });

    [Fact]
    public async Task A_shift_counts_its_own_cash_bills_and_shows_the_cashier_the_result_only_after_the_blind_count()
    {
        var (owner, _) = await api.SignUpAsync("Shift Mart");
        var cashier = await HelperOfAsync(owner);
        var id = Guid.CreateVersion7();

        var opened = await (await cashier.PostJsonAsync("/api/v1/shifts", new { id, counter = "Counter 1", openingFloat = 500m })).ReadAsync<CashierShiftDto>();
        Assert.Equal((500m, (decimal?)null), (opened.OpeningFloat, opened.Expected));
        // Sent again: the same shift. Another on the same counter, or a second one for the same cashier: refused.
        Assert.Equal(id, (await (await cashier.PostJsonAsync("/api/v1/shifts", new { id, counter = "Counter 1", openingFloat = 500m })).ReadAsync<CashierShiftDto>()).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync("/api/v1/shifts", new { id = Guid.CreateVersion7(), counter = "Counter 1", openingFloat = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cashier.PostJsonAsync("/api/v1/shifts", new { id = Guid.CreateVersion7(), counter = "Counter 2", openingFloat = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostJsonAsync("/api/v1/shifts", new { id = Guid.CreateVersion7(), counter = " ", openingFloat = 0m })).StatusCode);

        (await BillAsync(cashier, id, 300m)).EnsureSuccessStatusCode();
        (await BillAsync(cashier, id, 300m, [new { mode = "cash", amount = 200m }, new { mode = "upi", amount = 100m }])).EnsureSuccessStatusCode();
        (await BillAsync(cashier, id, 150m, mode: "upi")).EnsureSuccessStatusCode();
        (await BillAsync(cashier, null, 999m)).EnsureSuccessStatusCode(); // not on the shift

        // While open, the cashier sees the bills but not the cash the drawer should hold; the owner sees it.
        var mine = await cashier.GetJsonAsync<CashierShiftDto>("/api/v1/shifts/mine");
        Assert.Equal((3, 750m, (decimal?)null), (mine.Bills, mine.Sales, mine.Expected));
        var seen = Assert.Single(await owner.GetJsonAsync<List<CashierShiftDto>>("/api/v1/shifts"));
        Assert.Equal((500m, (decimal?)1000m), (seen.CashSales ?? 0m, seen.Expected));

        var closed = await (await cashier.PostJsonAsync($"/api/v1/shifts/{id}/close", new { counted = 980m, note = "₹20 short" })).ReadAsync<CashierShiftDto>();
        Assert.Equal(((decimal?)1000m, (decimal?)980m, (decimal?)-20m), (closed.Expected, closed.Counted, closed.Difference));
        Assert.NotNull(closed.ClosedAt);
        // Closing again changes nothing; a new shift can then open on the counter.
        Assert.Equal((decimal?)-20m, (await (await cashier.PostJsonAsync($"/api/v1/shifts/{id}/close", new { counted = 1000m })).ReadAsync<CashierShiftDto>()).Difference);
        Assert.Equal(HttpStatusCode.NoContent, (await cashier.GetAsync("/api/v1/shifts/mine")).StatusCode);
        (await owner.PostJsonAsync("/api/v1/shifts", new { id = Guid.CreateVersion7(), counter = "Counter 1", openingFloat = 1000m })).EnsureSuccessStatusCode();

        // A helper lists only their own shifts; another shop sees none.
        Assert.Single(await cashier.GetJsonAsync<List<CashierShiftDto>>("/api/v1/shifts"));
        Assert.Equal(2, (await owner.GetJsonAsync<List<CashierShiftDto>>("/api/v1/shifts")).Count);
        var (other, _) = await api.SignUpAsync("Other Mart");
        Assert.Empty(await other.GetJsonAsync<List<CashierShiftDto>>("/api/v1/shifts"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostJsonAsync($"/api/v1/shifts/{id}/close", new { counted = 0m })).StatusCode);
    }

    [Fact]
    public async Task Only_the_cashier_or_the_owner_closes_a_shift()
    {
        var (owner, _) = await api.SignUpAsync("Shift Mart 2");
        var one = await HelperOfAsync(owner);
        var two = await HelperOfAsync(owner);
        var id = Guid.CreateVersion7();
        (await one.PostJsonAsync("/api/v1/shifts", new { id, counter = "Counter 3", openingFloat = 0m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await two.PostJsonAsync($"/api/v1/shifts/{id}/close", new { counted = 0m })).StatusCode);
        Assert.Equal((decimal?)0m, (await (await owner.PostJsonAsync($"/api/v1/shifts/{id}/close", new { counted = 0m })).ReadAsync<CashierShiftDto>()).Difference);
    }

    [Fact]
    public async Task A_return_sent_later_keeps_its_time_and_its_cash_refund_comes_out_of_the_shift()
    {
        var (owner, _) = await api.SignUpAsync("Return Mart");
        var shiftId = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/shifts", new { id = shiftId, counter = "Counter 5", openingFloat = 1000m })).EnsureSuccessStatusCode();
        var billId = Guid.CreateVersion7();
        var soldAt = DateTimeOffset.UtcNow.AddHours(-3);
        (await owner.PostJsonAsync("/api/v1/bills", new
        {
            id = billId, lines = new[] { new { name = "Shirt", quantity = 2m, unitPrice = 400m } }, paidAmount = 800m, paymentMode = "cash", occurredAt = soldAt, shiftId,
        })).EnsureSuccessStatusCode();

        // Made offline an hour ago, sent now: it keeps that time. The refund came out of the shift's drawer.
        var returnedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var note = await (await owner.PostJsonAsync($"/api/v1/bills/{billId}/credit-notes", new
        {
            id = Guid.CreateVersion7(), reason = "Wrong size", lines = new[] { new { lineNo = 1, quantity = 1m } }, refundMode = "cash", occurredAt = returnedAt, shiftId,
        })).ReadAsync<CreditNoteDto>();
        Assert.InRange(note.OccurredAt, returnedAt.AddSeconds(-5), returnedAt.AddSeconds(5));

        // A time before the sale or in the future is kept between the sale and now.
        var early = await (await owner.PostJsonAsync($"/api/v1/bills/{billId}/credit-notes", new
        {
            id = Guid.CreateVersion7(), reason = "Torn", lines = new[] { new { lineNo = 1, quantity = 1m } }, refundMode = "upi", occurredAt = soldAt.AddDays(-2), shiftId,
        })).ReadAsync<CreditNoteDto>();
        Assert.InRange(early.OccurredAt, soldAt.AddSeconds(-5), soldAt.AddSeconds(5));

        var shift = Assert.Single(await owner.GetJsonAsync<List<CashierShiftDto>>("/api/v1/shifts"), s => s.Id == shiftId);
        // 1000 float + 800 cash − 400 cash refund (the UPI refund didn't come out of the drawer).
        Assert.Equal((decimal?)1400m, shift.Expected);
    }
}
