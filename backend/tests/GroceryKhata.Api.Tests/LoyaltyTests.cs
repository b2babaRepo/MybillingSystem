using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Loyalty points on the customer's mobile: earned on bills, spent with a code sent to that mobile.</summary>
[Collection(ApiCollection.Name)]
public sealed class LoyaltyTests(ApiFactory api)
{
    [Fact]
    public async Task Points_are_earned_spent_with_the_customers_code_and_put_back_when_a_bill_is_cancelled()
    {
        var (owner, _) = await api.SignUpAsync("Points Mart");
        (await owner.PutAsJsonAsync("/api/v1/shop", new { name = "Points Mart", loyaltyEarnPer = 100m, loyaltyPointValue = 1m })).EnsureSuccessStatusCode();
        var phone = ApiFactory.NewPhone();
        var customer = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Asha", phone })).EnsureSuccessStatusCode();
        Task<LoyaltyDto> BalanceAsync() => owner.GetJsonAsync<LoyaltyDto>($"/api/v1/customers/{customer}/loyalty");
        Task<HttpResponseMessage> BillAsync(Guid id, decimal price, int? points = null, string? code = null) => owner.PostJsonAsync("/api/v1/bills", new
        {
            id, customerId = customer, lines = new[] { new { name = "Saree", quantity = 1m, unitPrice = price } }, paidAmount = price, paymentMode = "cash",
            loyaltyPoints = points, loyaltyCode = code,
        });

        // ₹1,050 earns 10 points (1 a ₹100).
        (await BillAsync(Guid.CreateVersion7(), 1050m)).EnsureSuccessStatusCode();
        Assert.Equal((10, 10m), ((await BalanceAsync()).Points, (await BalanceAsync()).Value));

        (await owner.PostAsync($"/api/v1/customers/{customer}/loyalty/code", null)).EnsureSuccessStatusCode();
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!)!;
        // A wrong code, or more points than they have: refused, nothing spent.
        Assert.Equal(HttpStatusCode.BadRequest, (await BillAsync(Guid.CreateVersion7(), 495m, 5, code == "000000" ? "111111" : "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await BillAsync(Guid.CreateVersion7(), 485m, 15, code)).StatusCode);
        Assert.Equal(10, (await BalanceAsync()).Points);

        // 5 points take ₹5 off a ₹500 bill: ₹495 paid, which earns 4.
        var spent = Guid.CreateVersion7();
        (await BillAsync(spent, 495m, 5, code)).EnsureSuccessStatusCode();
        Assert.Equal(9, (await BalanceAsync()).Points);
        // The code is used up.
        Assert.Equal(HttpStatusCode.BadRequest, (await BillAsync(Guid.CreateVersion7(), 495m, 5, code)).StatusCode);

        // Cancelled: the 5 spent come back, the 4 earned go.
        (await owner.PostJsonAsync($"/api/v1/bills/{spent}/cancel", new { reason = "Customer changed mind" })).EnsureSuccessStatusCode();
        Assert.Equal(10, (await BalanceAsync()).Points);
    }
}
