using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>A village milk collection centre: farmers' milk priced by fat and SNF, bills with deductions, and milk sent to the dairy checked against what it accepted.</summary>
public sealed class DairyTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Milk_is_priced_by_kg_of_fat_and_snf_or_fat_only_or_per_litre()
    {
        var rate = new DairyRate { FatRate = 600, SnfRate = 400, FlatRate = 45, MinFat = 5, MinSnf = 9 };
        // 10 L buffalo at 6.5 fat, 9.0 SNF: 10.3 kg × (0.065 × 600 + 0.09 × 400) = 10.3 × 75 = ₹772.50.
        Assert.Equal((772.50m, 77.25m, false), Dairy.Price(RateMethod.FatSnf, rate, 10, 6.5m, 9.0m));
        Assert.Equal(401.70m, Dairy.Price(RateMethod.FatOnly, rate, 10, 6.5m, 0).Amount);
        Assert.Equal(450m, Dairy.Price(RateMethod.Flat, rate, 10, 6.5m, 0).Amount);
        Assert.True(Dairy.Price(RateMethod.FatSnf, rate, 10, 4.8m, 9.0m).BelowStandard);
        // From the lactometer: 28/4 + 0.21 × 6.5 + 0.36 = 8.725 → 8.73.
        Assert.Equal(8.73m, Dairy.SnfFromClr(28, 6.5m));
    }

    [Fact]
    public async Task A_dairy_centre_collects_bills_farmers_and_checks_the_dairys_figures()
    {
        var (shop, _) = await api.SignUpAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var farmer = Guid.CreateVersion7();
        var saved = await (await shop.PostJsonAsync("/api/v1/dairy/farmers", new { id = farmer, name = "Ramesh Yadav", phone = "9876543210", village = "Bhagwanpur", defaultKind = "buffalo" })).ReadAsync<FarmerDto>();
        Assert.Equal(1, saved.Code);

        // No rate yet: milk can't be priced.
        object Milk(Guid id, decimal litres, decimal fat, decimal? snf = null, decimal? clr = null, string shift = "morning", bool rejected = false) =>
            new { id, date = today, shift, farmerId = farmer, kind = "buffalo", litres, fat, snf, clr, rejected };
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/dairy/collections", Milk(Guid.CreateVersion7(), 10, 6.5m, 9.0m))).StatusCode);

        var rates = new[] { new { kind = "buffalo", fatRate = 600m, snfRate = 400m, flatRate = 0m, minFat = 5m, minSnf = 9m } };
        (await shop.PostJsonAsync("/api/v1/dairy/rate-plans", new { id = Guid.CreateVersion7(), @for = "farmers", from = today.AddDays(-30), method = "fat_snf", rates })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/dairy/rate-plans", new
        {
            id = Guid.CreateVersion7(), @for = "dairy", from = today.AddDays(-30), method = "fat_snf",
            rates = new[] { new { kind = "buffalo", fatRate = 640m, snfRate = 420m, flatRate = 0m, minFat = 5m, minSnf = 9m } },
        })).EnsureSuccessStatusCode();

        // The counter's preview, as the app asks for it (lower-case values in the address).
        var quote = await shop.GetAsync($"/api/v1/dairy/quote?date={today:yyyy-MM-dd}&kind=buffalo&litres=10&fat=6.5&clr=28");
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        Assert.Contains("\"snf\":8.73", await quote.Content.ReadAsStringAsync());

        var morning = await (await shop.PostJsonAsync("/api/v1/dairy/collections", Milk(Guid.CreateVersion7(), 10, 6.5m, 9.0m))).ReadAsync<CollectionDto>();
        Assert.Single((await shop.GetJsonAsync<ShiftDto>($"/api/v1/dairy/collections?date={today:yyyy-MM-dd}&shift=morning")).Collections);
        Assert.Equal((772.50m, 77.25m), (morning.Amount, morning.Rate));
        // Evening: SNF from the lactometer; sour milk sent back is recorded but not paid.
        var evening = await (await shop.PostJsonAsync("/api/v1/dairy/collections", Milk(Guid.CreateVersion7(), 8, 6.0m, clr: 28, shift: "evening"))).ReadAsync<CollectionDto>();
        Assert.Equal(8.62m, evening.Snf);
        Assert.Equal(0m, (await (await shop.PostJsonAsync("/api/v1/dairy/collections", Milk(Guid.CreateVersion7(), 2, 6m, 9m, shift: "evening", rejected: true))).ReadAsync<CollectionDto>()).Amount);

        // The owner corrects the morning fat; the change is logged and the amount follows.
        var fixedMilk = await (await shop.PutAsJsonAsync($"/api/v1/dairy/collections/{morning.Id}", Milk(morning.Id, 10, 6.2m, 9.0m), ApiFactory.Json)).ReadAsync<CollectionDto>();
        Assert.True(fixedMilk.Edited);
        Assert.Equal(753.96m, fixedMilk.Amount); // 10.3 kg × (0.062 × 600 + 0.09 × 400) = 10.3 × 73.2

        // Cattle feed ₹300 taken; bill for the period.
        (await shop.PostJsonAsync($"/api/v1/dairy/farmers/{farmer}/entries", new { id = Guid.CreateVersion7(), kind = "feed", amount = 300m, note = "Pashu aahar 1 bag" })).EnsureSuccessStatusCode();
        var bill = Assert.Single(await shop.GetJsonAsync<List<FarmerBillDto>>($"/api/v1/dairy/bills?from={today.AddDays(-9):yyyy-MM-dd}&to={today:yyyy-MM-dd}"));
        Assert.Equal((18m, 2, 300m), (bill.Litres, bill.Collections, bill.Feed));
        Assert.Equal(bill.MilkAmount - 300m, bill.Due);

        // Paying him out clears the due and shows in the kharcha book.
        (await shop.PostJsonAsync($"/api/v1/dairy/farmers/{farmer}/entries", new { id = Guid.CreateVersion7(), kind = "payout", amount = bill.Due, mode = "upi" })).EnsureSuccessStatusCode();
        Assert.Equal(0m, Assert.Single(await shop.GetJsonAsync<List<FarmerDto>>("/api/v1/dairy/farmers")).Due);

        // 17.5 L sent to Sudha; it accepted 17.2 L at its own test, priced by the dairy rate.
        var sent = await (await shop.PostJsonAsync("/api/v1/dairy/dispatches", new { id = Guid.CreateVersion7(), date = today, shift = "evening", agency = "Sudha (COMFED)", kind = "buffalo", litres = 17.5m, fat = 6.1m, snf = 8.8m })).ReadAsync<DispatchDto>();
        var accepted = await (await shop.PutAsJsonAsync($"/api/v1/dairy/dispatches/{sent.Id}/accepted", new { acceptedLitres = 17.2m, acceptedFat = 6.0m, acceptedSnf = 8.8m }, ApiFactory.Json)).ReadAsync<DispatchDto>();
        // 17.2 × 1.03 = 17.716 kg × (0.06 × 640 + 0.088 × 420) = 17.716 × 75.36 = 1335.08.
        Assert.Equal(1335.08m, accepted.Amount);

        var summary = await shop.GetJsonAsync<DairySummaryDto>("/api/v1/dairy/summary");
        Assert.Equal((18m, 17.5m, 17.2m, 1, 0), (summary.CollectedLitres, summary.DispatchedLitres, summary.AcceptedLitres, summary.Rejected, summary.DispatchesAwaitingDairy));
        Assert.Equal(1335.08m - summary.PaidToFarmers, summary.Margin);
        Assert.Equal(1335.08m, summary.DairyUnpaid);
    }
}
