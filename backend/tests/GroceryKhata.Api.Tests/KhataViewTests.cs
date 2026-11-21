using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Transparency step 2: customers read their own khata, by private link or in the household app.</summary>
[Collection(ApiCollection.Name)]
public sealed class KhataViewTests(ApiFactory api)
{
    [Fact]
    public async Task The_private_link_shows_the_khata_read_only_and_a_reset_cuts_off_the_old_link()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 500m);
        await KhataTests.AddEntryAsync(shop, customer, "payment", 200m);

        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var again = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var anyone = api.CreateClient();
        var view = await anyone.GetJsonAsync<KhataViewDto>($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}");

        Assert.Equal(link.Token, again.Token); // sharing twice sends the same link
        Assert.Equal(("Sharma General Store", 300m, 2), (view.ShopName, view.Statement.ClosingBalance, view.Statement.Entries.Count));
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t=wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}")).StatusCode);

        var reset = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link/reset", new { })).ReadAsync<KhataLinkDto>();
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={reset.Token}")).StatusCode);
    }

    [Fact]
    public async Task A_household_sees_its_khata_in_every_shop_by_phone_and_nobody_elses()
    {
        var phone = ApiFactory.NewPhone();
        var (sharma, _) = await api.SignUpAsync("Sharma General Store");
        var (gupta, guptaAuth) = await api.SignUpAsync("Gupta Kirana");
        var atSharma = await CreateCustomerAsync(sharma, "Priya", phone);
        await CreateCustomerAsync(gupta, "Priya ji", phone);
        await CreateCustomerAsync(sharma, "Someone else", ApiFactory.NewPhone());
        await KhataTests.AddEntryAsync(sharma, atSharma, "credit", 640m);

        var priya = await RegisterAsync("Priya Agarwal", phone);
        // A password account hasn't proved the number is hers: it sees nothing until an SMS code does.
        Assert.Empty(await priya.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata"));
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var verify = new Npgsql.NpgsqlCommand("UPDATE users SET phone_verified_at = now() WHERE phone = @p", db);
            verify.Parameters.AddWithValue("p", GroceryKhata.Api.Common.PhoneNumber.Normalize(phone)!);
            await verify.ExecuteNonQueryAsync();
        }

        var stranger = await RegisterAsync("Stranger", ApiFactory.NewPhone());

        // The number matches, but the shop only typed it: nothing shows until a code from the shop proves it's hers.
        var unproved = await priya.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata");
        Assert.Equal(["Sharma General Store", "Gupta Kirana"], unproved.OrderBy(k => k.ShopName == "Gupta Kirana").Select(k => k.ShopName));
        Assert.All(unproved, k => Assert.Equal((false, 0m, ""), (k.Confirmed, k.Balance, k.CustomerName)));
        var sharmaId = unproved.Single(k => k.ShopName == "Sharma General Store").ShopId;
        Assert.Equal(HttpStatusCode.NotFound, (await priya.GetAsync($"/api/v1/me/khata/{sharmaId}/{atSharma}")).StatusCode);

        await ConfirmAsync(sharma, priya, sharmaId, atSharma);
        var guptaEntry = unproved.Single(k => k.ShopName == "Gupta Kirana");
        await ConfirmAsync(gupta, priya, guptaEntry.ShopId, guptaEntry.CustomerId);

        var mine = await priya.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata");
        Assert.Equal(["Sharma General Store", "Gupta Kirana"], mine.Select(k => k.ShopName));
        Assert.Equal(640m, mine[0].Balance);

        var sharmaKhata = await priya.GetJsonAsync<KhataViewDto>($"/api/v1/me/khata/{mine[0].ShopId}/{atSharma}");
        Assert.Equal(640m, sharmaKhata.Statement.ClosingBalance);

        // Someone with another phone sees nothing, and can't open Priya's khata by its IDs.
        Assert.Empty(await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata"));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/me/khata/{mine[0].ShopId}/{atSharma}")).StatusCode);
        // Nor can Priya open her Sharma khata through another shop's ID.
        Assert.Equal(HttpStatusCode.NotFound, (await priya.GetAsync($"/api/v1/me/khata/{guptaAuth.ShopId}/{atSharma}")).StatusCode);
    }

    [Fact]
    public async Task A_stranger_on_a_mistyped_number_sees_no_amounts_and_cannot_guess_the_code()
    {
        var wrongNumber = ApiFactory.NewPhone();
        var (shop, _) = await api.SignUpAsync("Typo Store");
        var customer = await CreateCustomerAsync(shop, "Priya", wrongNumber);
        await KhataTests.AddEntryAsync(shop, customer, "credit", 640m);
        var stranger = await RegisterAsync("Stranger", wrongNumber);
        await VerifyPhoneAsync(wrongNumber);

        var seen = (await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata")).Single();
        Assert.Equal((false, 0m, ""), (seen.Confirmed, seen.Balance, seen.CustomerName));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}")).StatusCode);

        var code = (await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/phone-code", new { })).ReadAsync<PhoneCodeDto>()).Code!;
        var wrong = code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await stranger.PostJsonAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}/confirm", new { code = wrong })).StatusCode);
        }

        // Five wrong tries and the code stops: even the right one no longer works.
        Assert.Equal(HttpStatusCode.BadRequest, (await stranger.PostJsonAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}/confirm", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}")).StatusCode);
    }

    [Fact]
    public async Task Correcting_a_phone_cuts_off_the_old_number_and_its_link_and_keeps_secrets_out_of_the_audit_log()
    {
        var wrong = ApiFactory.NewPhone();
        var right = ApiFactory.NewPhone();
        var (shop, auth) = await api.SignUpAsync("Fix Store");
        var customer = await CreateCustomerAsync(shop, "Priya", wrong);
        await KhataTests.AddEntryAsync(shop, customer, "credit", 640m);
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var stranger = await RegisterAsync("Stranger", wrong);
        await VerifyPhoneAsync(wrong);
        await ConfirmAsync(shop, stranger, auth.Memberships.Single().ShopId, customer);
        Assert.Equal(640m, (await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata")).Single().Balance);

        var fixedUp = await (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}", new { name = "Priya Agarwal", phone = right })).ReadAsync<GroceryKhata.Api.Ledger.CustomerDto>();
        Assert.Equal(("Priya Agarwal", 640m), (fixedUp.Name, fixedUp.Balance));

        Assert.Empty(await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata"));
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).StatusCode);

        var audit = await shop.GetStringAsync($"/api/v1/audit?recordId={customer}");
        Assert.Contains("phone", audit);
        Assert.DoesNotContain(link.Token, audit);
        Assert.DoesNotContain("confirm_code_hash", audit);
    }

    [Fact]
    public async Task A_regular_medicine_shows_as_due_only_after_the_khata_is_confirmed()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Refill Chemist", fullName = "Dr Shah", phone = ApiFactory.NewPhone(), password = "secret123", language = "en", type = "medical" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var phone = ApiFactory.NewPhone();
        var customer = await CreateCustomerAsync(shop, "Kamla", phone);
        var tablet = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = tablet, name = "Metformin 500", unit = "strip", sellingPrice = 40m, openingStock = 50m })).EnsureSuccessStatusCode();
        // Bought every 30 days; the last 29 days ago, so due tomorrow.
        foreach (var daysAgo in new[] { 89, 59, 29 })
        {
            var bill = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/bills", new { id = bill, customerId = customer, lines = new[] { new { productId = tablet, name = "Metformin 500", quantity = 2m, unitPrice = 40m } }, paidAmount = 80m, paymentMode = "cash" })).EnsureSuccessStatusCode();
            await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
            await db.OpenAsync();
            await using var back = new Npgsql.NpgsqlCommand("UPDATE bills SET occurred_at = now() - make_interval(days => @d) WHERE id = @id", db);
            back.Parameters.AddWithValue("d", daysAgo);
            back.Parameters.AddWithValue("id", bill);
            await back.ExecuteNonQueryAsync();
        }

        var person = await RegisterAsync("Kamla", phone);
        await VerifyPhoneAsync(phone);
        // Health data: nothing until the person proves the khata is theirs with the shop's code.
        Assert.Empty(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.MyRefillDto>>("/api/v1/me/refills"));
        await ConfirmAsync(shop, person, auth.Memberships.Single().ShopId, customer);
        var due = Assert.Single(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.MyRefillDto>>("/api/v1/me/refills"));
        Assert.Equal(("Metformin 500", 2m, 30, -1, true), (due.Medicine, due.LastQuantity, due.EveryDays, due.DaysLate, due.InStock));
    }

    [Fact]
    public async Task A_customer_books_a_salon_visit_sees_it_and_cancels_it()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Glow Salon", fullName = "Neha", phone = ApiFactory.NewPhone(), password = "secret123", language = "en", type = "beauty" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var shopId = auth.Memberships.Single().ShopId;
        var facial = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = facial, name = "Facial", unit = "piece", sellingPrice = 800m, isService = true, durationMinutes = 45 })).EnsureSuccessStatusCode();

        var phone = ApiFactory.NewPhone();
        var person = await RegisterAsync("Ritu", phone);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(1);
        var id = Guid.CreateVersion7();
        var booking = new { id, date = tomorrow, start = "11:00", serviceIds = new[] { facial }, note = "Sensitive skin" };
        // A proved mobile number first: the salon must be able to reach the person.
        Assert.Equal(HttpStatusCode.BadRequest, (await person.PostJsonAsync($"/api/v1/me/salon/{shopId}/appointments", booking)).StatusCode);
        await VerifyPhoneAsync(phone);
        Assert.Equal(HttpStatusCode.NoContent, (await person.PostJsonAsync($"/api/v1/me/salon/{shopId}/appointments", booking)).StatusCode);

        // The salon's day has it, for any stylist, marked as from the app.
        var day = await shop.GetJsonAsync<SalonDayDto>($"/api/v1/appointments?date={tomorrow:yyyy-MM-dd}");
        var seen = Assert.Single(day.Appointments);
        Assert.Equal(("Ritu", new TimeOnly(11, 45), (Guid?)null, "Booked in the app: Sensitive skin"), (seen.Name, seen.End, seen.StylistId, seen.Note));

        var visit = Assert.Single(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.MyVisitDto>>("/api/v1/me/salon/visits"));
        Assert.Equal((true, "Facial", 800m), (visit.Upcoming, visit.Services.Single().Name, visit.Total));
        Assert.Equal(HttpStatusCode.NoContent, (await person.PostJsonAsync($"/api/v1/me/salon/{shopId}/appointments/{id}/cancel", new { })).StatusCode);
        Assert.Empty(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.MyVisitDto>>("/api/v1/me/salon/visits"));
        // Someone else can't cancel it, or book at a shop that isn't a salon.
        Assert.Equal(HttpStatusCode.NotFound, (await (await RegisterAsync("Other", ApiFactory.NewPhone())).PostJsonAsync($"/api/v1/me/salon/{shopId}/appointments/{id}/cancel", new { })).StatusCode);
    }

    [Fact]
    public async Task A_daily_milk_plan_shows_on_the_shops_round_until_paused_or_stopped()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Gopal Dairy", fullName = "Gopal", phone = ApiFactory.NewPhone(), password = "secret123", language = "en", type = "kirana" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var shopId = auth.Memberships.Single().ShopId;
        var milk = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = milk, name = "Toned milk 500 ml", unit = "packet", sellingPrice = 28m, openingStock = 100m })).EnsureSuccessStatusCode();

        var phone = ApiFactory.NewPhone();
        var person = await RegisterAsync("Asha", phone);
        await VerifyPhoneAsync(phone);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(1);
        var id = Guid.CreateVersion7();
        (await person.PostJsonAsync($"/api/v1/me/deliveries/{shopId}", new { id, productId = milk, quantity = 2m, days = 127, startOn = tomorrow, address = "B-12, Lake View" })).EnsureSuccessStatusCode();

        var mine = Assert.Single(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.DeliveryPlanDto>>("/api/v1/me/deliveries"));
        Assert.Equal(("Toned milk 500 ml", 2m, (DateOnly?)tomorrow), (mine.ProductName, mine.Quantity, mine.NextOn));
        var round = await shop.GetJsonAsync<GroceryKhata.Api.Planner.DeliveryDayDto>($"/api/v1/deliveries?date={tomorrow:yyyy-MM-dd}");
        Assert.Equal(("Asha", "B-12, Lake View", 2m), (round.Deliveries.Single().Name, round.Deliveries.Single().Address, round.Totals.Single().Quantity));

        // Away tomorrow: off the round that day, back the day after.
        (await person.PostJsonAsync($"/api/v1/me/deliveries/{shopId}/{id}/pause", new { from = tomorrow, to = tomorrow })).EnsureSuccessStatusCode();
        Assert.Empty((await shop.GetJsonAsync<GroceryKhata.Api.Planner.DeliveryDayDto>($"/api/v1/deliveries?date={tomorrow:yyyy-MM-dd}")).Deliveries);
        Assert.Single((await shop.GetJsonAsync<GroceryKhata.Api.Planner.DeliveryDayDto>($"/api/v1/deliveries?date={tomorrow.AddDays(1):yyyy-MM-dd}")).Deliveries);

        (await person.PostJsonAsync($"/api/v1/me/deliveries/{shopId}/{id}/stop", new { })).EnsureSuccessStatusCode();
        Assert.Empty(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.DeliveryPlanDto>>("/api/v1/me/deliveries"));
        Assert.Empty((await shop.GetJsonAsync<GroceryKhata.Api.Planner.DeliveryDayDto>($"/api/v1/deliveries?date={tomorrow.AddDays(1):yyyy-MM-dd}")).Deliveries);
    }

    [Fact]
    public async Task Posting_a_days_round_bills_each_khata_once()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Post Dairy", fullName = "Ramesh", phone = ApiFactory.NewPhone(), password = "secret123", language = "en", type = "dairy" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var shopId = auth.Memberships.Single().ShopId;
        var milk = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = milk, name = "Cow milk 1 L", unit = "packet", sellingPrice = 60m, openingStock = 50m })).EnsureSuccessStatusCode();

        // Two customers on the round: one has a khata at the shop, one doesn't.
        var (withKhata, without) = (ApiFactory.NewPhone(), ApiFactory.NewPhone());
        var customer = await CreateCustomerAsync(shop, "Sunita", withKhata);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        foreach (var (name, phone) in new[] { ("Sunita", withKhata), ("Vikas", without) })
        {
            var person = await RegisterAsync(name, phone);
            await VerifyPhoneAsync(phone);
            (await person.PostJsonAsync($"/api/v1/me/deliveries/{shopId}", new { id = Guid.CreateVersion7(), productId = milk, quantity = 2m, days = 127, startOn = today.AddDays(1), address = "Gali 4" })).EnsureSuccessStatusCode();
        }

        // As if they started yesterday.
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var back = new Npgsql.NpgsqlCommand("UPDATE delivery_plans SET start_on = @d WHERE shop_id = @s", db);
            back.Parameters.AddWithValue("d", today.AddDays(-1));
            back.Parameters.AddWithValue("s", shopId);
            await back.ExecuteNonQueryAsync();
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/deliveries/{today.AddDays(1):yyyy-MM-dd}/post", new { })).StatusCode);
        var first = await (await shop.PostJsonAsync($"/api/v1/deliveries/{today:yyyy-MM-dd}/post", new { })).ReadAsync<GroceryKhata.Api.Planner.DeliveryPostDto>();
        Assert.Equal((1, 0, "Vikas"), (first.Posted, first.AlreadyPosted, first.NoKhata.Single()));
        var again = await (await shop.PostJsonAsync($"/api/v1/deliveries/{today:yyyy-MM-dd}/post", new { })).ReadAsync<GroceryKhata.Api.Planner.DeliveryPostDto>();
        Assert.Equal((0, 1), (again.Posted, again.AlreadyPosted));
        Assert.Equal(120m, (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single(c => c.Id == customer).Balance);

        var round = await shop.GetJsonAsync<GroceryKhata.Api.Planner.DeliveryDayDto>($"/api/v1/deliveries?date={today:yyyy-MM-dd}");
        Assert.Equal([("Sunita", true, true), ("Vikas", false, false)], round.Deliveries.OrderBy(d => d.Name).Select(d => (d.Name, d.OnKhata, d.Posted)).ToArray());
    }

    [Fact]
    public async Task The_passbook_lists_bills_and_payments_from_confirmed_khatas_only()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Passbook Kirana", fullName = "Mohan", phone = ApiFactory.NewPhone(), password = "secret123", language = "en" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var phone = ApiFactory.NewPhone();
        var customer = await CreateCustomerAsync(shop, "Lata", phone);
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { name = "Ghee 1 L", quantity = 1m, unitPrice = 500m } }, paidAmount = 100m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 50m, paymentMode = "upi" })).EnsureSuccessStatusCode();

        var person = await RegisterAsync("Lata", phone);
        await VerifyPhoneAsync(phone);
        Assert.Empty((await person.GetJsonAsync<GroceryKhata.Api.Planner.PassbookDto>("/api/v1/me/passbook")).Items);
        await ConfirmAsync(shop, person, auth.Memberships.Single().ShopId, customer);

        var book = await person.GetJsonAsync<GroceryKhata.Api.Planner.PassbookDto>("/api/v1/me/passbook");
        var bill = Assert.Single(book.Items, i => i.Kind == "bill");
        Assert.Equal((500m, 100m, 400m), (bill.Amount, bill.Paid, bill.OnKhata));
        Assert.Equal(50m, Assert.Single(book.Items, i => i.Kind == "payment").Amount);
        Assert.Equal(350m, book.Shops.Single().Balance);
        // The bill opens on its signed page.
        Assert.Equal(500m, (await api.CreateClient().GetJsonAsync<PublicBillDto>($"/api/v1/bill/{bill.ShopSlug}/{bill.BillId}?t={bill.BillToken}")).Bill.Total);
        // A month with nothing in it, in the passbook and the khata statement.
        Assert.Empty((await person.GetJsonAsync<GroceryKhata.Api.Planner.PassbookDto>("/api/v1/me/passbook?month=2001-01")).Items);
        var shopId = auth.Memberships.Single().ShopId;
        Assert.Empty((await person.GetJsonAsync<KhataViewDto>($"/api/v1/me/khata/{shopId}/{customer}?month=2001-01")).Statement.Entries);

        // A GSTIN on the profile must be a real one.
        var profile = new { fullName = "Lata", preferredLanguage = "en", gstin = "27AAPFU0939F1ZX" };
        Assert.Equal(HttpStatusCode.BadRequest, (await person.PutAsJsonAsync("/api/v1/profile", profile)).StatusCode);
        var saved = await (await person.PutAsJsonAsync("/api/v1/profile", profile with { gstin = "27aapfu0939f1zv" })).ReadAsync<ProfileDto>();
        Assert.Equal("27AAPFU0939F1ZV", saved.Gstin);
    }

    [Fact]
    public async Task Udhaar_falls_due_after_the_shops_credit_days_and_is_reminded_once()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Due Kirana", fullName = "Suresh", phone = ApiFactory.NewPhone(), password = "secret123", language = "en" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var phone = ApiFactory.NewPhone();
        var customer = await CreateCustomerAsync(shop, "Kavita", phone);
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/credit-days", new { creditDays = 10 })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount = 300m })).EnsureSuccessStatusCode();
        // As if taken 9 days ago: due tomorrow.
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var back = new Npgsql.NpgsqlCommand("UPDATE ledger_entries SET occurred_at = now() - interval '9 days' WHERE customer_id = @c", db);
            back.Parameters.AddWithValue("c", customer);
            await back.ExecuteNonQueryAsync();
        }

        var person = await RegisterAsync("Kavita", phone);
        await VerifyPhoneAsync(phone);
        Assert.Empty(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.KhataDueDto>>("/api/v1/me/khata-dues"));
        await ConfirmAsync(shop, person, auth.Memberships.Single().ShopId, customer);
        var due = Assert.Single(await person.GetJsonAsync<List<GroceryKhata.Api.Planner.KhataDueDto>>("/api/v1/me/khata-dues"));
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(1);
        Assert.Equal(((DateOnly?)tomorrow, 300m, 0m), (due.DueOn, due.DueAmount, due.Overdue));

        // The daily reminder marks it reminded (no phone has alerts on here, so nothing is sent), once a day.
        var scopes = api.Services.GetRequiredService<IServiceScopeFactory>();
        await GroceryKhata.Api.Planner.KhataDues.RemindAsync(scopes, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, default);
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var read = new Npgsql.NpgsqlCommand("SELECT last_due_reminder_on FROM customers WHERE id = @c", db);
            read.Parameters.AddWithValue("c", customer);
            Assert.Equal(tomorrow.AddDays(-1), (DateOnly)(await read.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public void Due_day_is_the_oldest_unpaid_udhaar_plus_credit_days()
    {
        var at = (int daysAgo) => new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(6), TimeSpan.Zero);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        // 200 taken 20 days ago (paid 150 of it), 100 taken 5 days ago, 15 days' credit: 50 overdue, due day missed.
        var (dueOn, amount, overdue) = GroceryKhata.Api.Planner.KhataDues.Due([new(at(20), 200m), new(at(10), -150m), new(at(5), 100m)], 15, today);
        Assert.Equal(50m, overdue);
        Assert.True(dueOn < today);
        Assert.Equal(50m, amount);
    }

    private async Task VerifyPhoneAsync(string phone)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var verify = new Npgsql.NpgsqlCommand("UPDATE users SET phone_verified_at = now() WHERE phone = @p", db);
        verify.Parameters.AddWithValue("p", GroceryKhata.Api.Common.PhoneNumber.Normalize(phone)!);
        await verify.ExecuteNonQueryAsync();
    }

    private static async Task ConfirmAsync(HttpClient shop, HttpClient person, Guid shopId, Guid customer)
    {
        var made = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/phone-code", new { })).ReadAsync<PhoneCodeDto>();
        (await person.PostJsonAsync($"/api/v1/me/khata/{shopId}/{customer}/confirm", new { code = made.Code })).EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient shop, string name, string phone)
    {
        var id = Guid.CreateVersion7();
        await (await shop.PostJsonAsync("/api/v1/customers", new { id, name, phone })).ReadAsync<CustomerDto>();
        return id;
    }

    private async Task<HttpClient> RegisterAsync(string name, string phone)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/register", new { fullName = name, phone, password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
