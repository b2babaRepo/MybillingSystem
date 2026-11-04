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
    public void Flags_point_at_fat_jumps_water_signs_typed_readings_and_dairy_shortfalls()
    {
        var (ramesh, sita) = (Guid.NewGuid(), Guid.NewGuid());
        var today = new DateOnly(2026, 11, 4);
        var farmers = new List<DairyFarmer> { new() { Id = ramesh, Name = "Ramesh" }, new() { Id = sita, Name = "Sita" } };
        MilkCollection Milk(Guid who, int daysAgo, decimal fat, decimal snf) =>
            new() { Id = Guid.NewGuid(), FarmerId = who, Date = today.AddDays(-daysAgo), Shift = Shift.Morning, Kind = MilkKind.Buffalo, Litres = 8, Fat = fat, Snf = snf };
        var milk = new List<MilkCollection>
        {
            // Ramesh usually brings 6.0–6.3 fat; today 7.8.
            Milk(ramesh, 6, 6.1m, 9.0m), Milk(ramesh, 5, 6.2m, 9.1m), Milk(ramesh, 4, 6.0m, 9.0m), Milk(ramesh, 3, 6.3m, 9.0m), Milk(ramesh, 0, 7.8m, 9.1m),
            // Sita: the same reading five times, the last with low SNF and normal fat.
            Milk(sita, 4, 6.5m, 8.2m), Milk(sita, 3, 6.5m, 8.2m), Milk(sita, 2, 6.5m, 8.2m), Milk(sita, 1, 6.5m, 8.2m), Milk(sita, 0, 6.5m, 8.2m),
        };
        var standards = new List<DairyRate> { new() { Kind = MilkKind.Buffalo, MinFat = 5, MinSnf = 9 } };
        var dispatch = new MilkDispatch { Id = Guid.NewGuid(), Agency = "Sudha", Date = today, Litres = 100, Fat = 6.4m, Snf = 8.9m, AcceptedLitres = 98, AcceptedFat = 6.1m, AcceptedSnf = 8.9m };
        var flags = DairyChecks.Flags(milk, farmers, standards, [dispatch], today.AddDays(-10), today);
        Assert.Contains(flags, f => f.Kind == "fatJump" && f.FarmerId == ramesh);
        Assert.Contains(flags, f => f.Kind == "waterSign" && f.FarmerId == sita);
        Assert.Contains(flags, f => f.Kind == "sameReading" && f.FarmerId == sita);
        Assert.Contains(flags, f => f.Kind == "dairyShort" && f.DispatchId == dispatch.Id);
        Assert.DoesNotContain(flags, f => f.Kind == "fatJump" && f.FarmerId == sita);
    }

    [Theory]
    [InlineData("ek number das litre bhains fat saadhe chhe snf aath point aath", 1, MilkKind.Buffalo, 10, 6.5, 8.8)]
    [InlineData("number 12, gaay 5.5 litre, fat 4.2, snf 8.4", 12, MilkKind.Cow, 5.5, 4.2, 8.4)]
    [InlineData("सात नंबर भैंस आठ लीटर फैट साढ़े सात", 7, MilkKind.Buffalo, 8, 7.5, null)]
    [InlineData("3 number dedh litre fat paune paanch", 3, null, 1.5, 4.75, null)]
    public void Spoken_entries_become_fields(string said, int code, MilkKind? kind, double litres, double fat, double? snf)
    {
        var e = DairyAssistant.Parse(said);
        Assert.Equal((code, kind, (decimal)litres, (decimal)fat, snf is null ? null : (decimal?)snf), (e.FarmerCode!.Value, e.Kind, e.Litres!.Value, e.Fat!.Value, e.Snf));
    }

    [Fact]
    public void Spoken_values_milk_cannot_have_are_left_out()
    {
        var e = DairyAssistant.Parse("number paanch fat pachchis litre do");
        Assert.Equal((5, (decimal?)null, 2m), (e.FarmerCode!.Value, e.Fat, e.Litres!.Value));
    }

    [Fact]
    public void Helper_drafts_are_kept_only_when_they_fit_the_records()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var farmers = new Dictionary<int, (Guid Id, string Name, decimal Due, string? Phone)> { [1] = (a, "Ramesh", 2500m, "9876543210"), [2] = (b, "Sita", 800m, null) };
        var drafts = DairyAssistant.CheckDrafts(
        [
            ("payout", 1, 2500m, "10-day bill"),
            ("payout", 2, 5000m, "more than due"),
            ("remind", 2, 0m, "no phone"),
            ("payout", 9, 100m, "no such farmer"),
            ("delete", 1, 0m, "not an action"),
        ], farmers);
        var only = Assert.Single(drafts);
        Assert.Equal(("payout", a, 2500m), (only.Type, only.FarmerId, only.Amount));
    }

    [Fact]
    public async Task The_helper_needs_consent_and_the_ai_and_its_records_cover_dues_and_flags()
    {
        var (shop, _) = await api.SignUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.PostJsonAsync("/api/v1/dairy/ask", new { question = "kisko paisa dena hai?" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync("/api/v1/me/ai/consent", new { agree = true })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await shop.PostJsonAsync("/api/v1/dairy/ask", new { question = "kisko paisa dena hai?" })).StatusCode);
        var parsed = await (await shop.PostJsonAsync("/api/v1/dairy/parse-entry", new { question = "do number teen litre gaay fat chaar" })).ReadAsync<SpokenEntry>();
        Assert.Equal((2, MilkKind.Cow, 3m, 4m), (parsed.FarmerCode!.Value, parsed.Kind!.Value, parsed.Litres!.Value, parsed.Fat!.Value));
    }

    [Fact]
    public void Read_slips_and_statements_keep_only_values_milk_can_have()
    {
        var slip = ClaudeDairyPhotoReader.ParseSlip("""{"litres":8.5,"fat":6.4,"snf":42,"clr":28.5,"farmerCode":17}""");
        Assert.Equal((8.5m, 6.4m, (decimal?)null, 28.5m, 17), (slip.Litres!.Value, slip.Fat!.Value, slip.Snf, slip.Clr!.Value, slip.FarmerCode!.Value));
        var lines = ClaudeDairyPhotoReader.ParseStatement("""{"lines":[{"date":"2026-11-03","shift":"evening","litres":96.5,"fat":6.1,"snf":8.9,"amount":5120.4},{"date":"03/11","shift":null,"litres":null,"fat":99,"snf":null,"amount":null}]}""");
        Assert.Equal((new DateOnly(2026, 11, 3), Shift.Evening, 96.5m), (lines[0].Date!.Value, lines[0].Shift!.Value, lines[0].Litres!.Value));
        Assert.Equal(((DateOnly?)null, (decimal?)null), (lines[1].Date, lines[1].Fat));
    }

    [Fact]
    public async Task A_rate_chart_prices_milk_by_its_cell_and_the_statement_matches_our_dispatch()
    {
        var (shop, _) = await api.SignUpAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var farmer = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/dairy/farmers", new { id = farmer, name = "Sita Devi", defaultKind = "buffalo" })).EnsureSuccessStatusCode();
        // A small chart: fat 6.0–6.1 × SNF 8.9–9.0.
        var grid = new[] { (6.0m, 8.9m, 48.10m), (6.0m, 9.0m, 48.60m), (6.1m, 8.9m, 48.70m), (6.1m, 9.0m, 49.20m) }
            .Select(c => new { kind = "buffalo", fat = c.Item1, snf = c.Item2, ratePerLitre = c.Item3 }).ToArray();
        var rates = new[] { new { kind = "buffalo", fatRate = 0m, snfRate = 0m, flatRate = 0m, minFat = 5m, minSnf = 9m } };
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/dairy/rate-plans", new { id = Guid.CreateVersion7(), @for = "farmers", from = today.AddDays(-5), method = "grid", rates })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/dairy/rate-plans", new { id = Guid.CreateVersion7(), @for = "farmers", from = today.AddDays(-5), method = "grid", rates, grid })).EnsureSuccessStatusCode();
        Assert.Equal(4, (await shop.GetJsonAsync<List<RatePlanDto>>("/api/v1/dairy/rate-plans"))[0].GridCells);

        // 10 L at 6.08 fat, 8.96 SNF: read as 6.1 / 9.0 → ₹49.20 a litre.
        var milk = await (await shop.PostJsonAsync("/api/v1/dairy/collections", new { id = Guid.CreateVersion7(), date = today, shift = "morning", farmerId = farmer, kind = "buffalo", litres = 10m, fat = 6.08m, snf = 8.96m })).ReadAsync<CollectionDto>();
        Assert.Equal((492m, 49.20m), (milk.Amount, milk.Rate));
        // Off the chart: refused, never guessed.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/dairy/collections", new { id = Guid.CreateVersion7(), date = today, shift = "morning", farmerId = farmer, kind = "buffalo", litres = 5m, fat = 7.5m, snf = 9.0m })).StatusCode);

        var sent = await (await shop.PostJsonAsync("/api/v1/dairy/dispatches", new { id = Guid.CreateVersion7(), date = today, shift = "morning", agency = "Sudha", kind = "buffalo", litres = 100m, fat = 6.1m, snf = 9.0m })).ReadAsync<DispatchDto>();
        var matched = Assert.Single(await (await shop.PostJsonAsync("/api/v1/dairy/statement/match", new[] { new { date = today, shift = "morning", litres = 98.5m, fat = 6.0m, snf = 9.0m, amount = 4790m } })).ReadAsync<List<DairyStatementMatch>>());
        Assert.Equal((sent.Id, 1.5m), (matched.DispatchId!.Value, matched.LitresShort!.Value));
        Assert.Empty(await shop.GetJsonAsync<List<DairyFlagDto>>("/api/v1/dairy/flags"));
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
