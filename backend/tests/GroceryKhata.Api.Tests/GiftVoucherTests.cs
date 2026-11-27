using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Gift vouchers: money in when sold, a way to pay bills after, back on the voucher when a bill is cancelled.</summary>
[Collection(ApiCollection.Name)]
public sealed class GiftVoucherTests(ApiFactory api)
{
    [Fact]
    public async Task A_voucher_pays_bills_up_to_its_balance_and_its_money_is_counted_once()
    {
        var (owner, _) = await api.SignUpAsync("Voucher Mart");
        var voucher = await (await owner.PostJsonAsync("/api/v1/gift-vouchers", new { id = Guid.CreateVersion7(), amount = 1000m, mode = "cash", buyerName = "Asha" })).ReadAsync<GiftVoucherDto>();
        Assert.Equal((1000m, 12), (voucher.Balance, voucher.Code.Length));
        // Read back however it is typed.
        Assert.Equal(voucher.Id, (await owner.GetJsonAsync<GiftVoucherDto>($"/api/v1/gift-vouchers/{voucher.Code.ToLowerInvariant()}")).Id);

        Task<HttpResponseMessage> BillAsync(decimal total, decimal byVoucher, string code) => owner.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { name = "Kurta", quantity = 1m, unitPrice = total } }, paidAmount = total, paymentMode = "cash",
            payments = new object[] { new { mode = "voucher", amount = byVoucher, reference = code }, new { mode = "cash", amount = total - byVoucher } },
        });

        // ₹1,200 kurta: ₹700 from the voucher, ₹500 cash.
        var first = await (await BillAsync(1200m, 700m, voucher.Code)).ReadAsync<BillDto>();
        Assert.Equal(300m, (await owner.GetJsonAsync<GiftVoucherDto>($"/api/v1/gift-vouchers/{voucher.Code}")).Balance);
        // More than is left, or a made-up code: refused, nothing taken.
        Assert.Equal(HttpStatusCode.BadRequest, (await BillAsync(800m, 400m, voucher.Code)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await BillAsync(800m, 100m, "GVAAAAAAAAAA")).StatusCode);
        Assert.Equal(300m, (await owner.GetJsonAsync<GiftVoucherDto>($"/api/v1/gift-vouchers/{voucher.Code}")).Balance);

        // The cash book: ₹1,000 when sold, ₹500 cash on the bill; the ₹700 from the voucher is not money again.
        var book = await owner.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal(1500m, book.Cash);
        Assert.Equal(1500m, book.Total);

        // Cancelled: the ₹700 goes back on the voucher, and a voucher partly spent can't be cancelled.
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/v1/gift-vouchers/{voucher.Id}/cancel", null)).StatusCode);
        (await owner.PostJsonAsync($"/api/v1/bills/{first.Id}/cancel", new { reason = "Changed mind" })).EnsureSuccessStatusCode();
        Assert.Equal(1000m, (await owner.GetJsonAsync<GiftVoucherDto>($"/api/v1/gift-vouchers/{voucher.Code}")).Balance);
        (await owner.PostAsync($"/api/v1/gift-vouchers/{voucher.Id}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await BillAsync(500m, 100m, voucher.Code)).StatusCode);
    }
}
