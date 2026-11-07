using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ReminderTests(ApiFactory api)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Consent_and_money_waiting_beat_any_schedule()
    {
        Assert.Equal("noPhone", Reminders.Hold(null, true, false, 30, null, Now));
        Assert.Equal("noConsent", Reminders.Hold("+919811111111", false, false, 30, null, Now));
        Assert.Equal("moneyWaiting", Reminders.Hold("+919811111111", true, true, 30, null, Now));
        Assert.Equal("recent", Reminders.Hold("+919811111111", true, false, 30, Now.AddDays(-6), Now));
        Assert.Equal("tooNew", Reminders.Hold("+919811111111", true, false, 2, null, Now));
        Assert.Null(Reminders.Hold("+919811111111", true, false, 3, Now.AddDays(-7), Now));
    }

    private static async Task<Guid> Customer(HttpClient shop, string name, string? phone)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customers", new { id, name, phone })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task Owes(HttpClient shop, Guid customer, int daysAgo) =>
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount = 500m, occurredAt = DateTimeOffset.UtcNow.AddDays(-daysAgo) })).EnsureSuccessStatusCode();

    private static Task<ReminderPlan> Plan(HttpClient shop) => shop.GetJsonAsync<ReminderPlan>("/api/v1/reminders/today");

    [Fact]
    public async Task Only_customers_who_agreed_are_listed_and_a_reminder_takes_them_off_for_a_week()
    {
        var (shop, _) = await api.SignUpAsync("Reminder Store");
        var agreed = await Customer(shop, "Agreed", ApiFactory.NewPhone());
        var silent = await Customer(shop, "Silent", ApiFactory.NewPhone());
        await Owes(shop, agreed, 10);
        await Owes(shop, silent, 10);

        var before = await Plan(shop);
        Assert.Empty(before.Ready);
        Assert.Equal(["noConsent", "noConsent"], before.Held.Select(h => h.Hold));

        (await shop.PutAsJsonAsync($"/api/v1/customers/{agreed}/reminder-consent", new { given = true })).EnsureSuccessStatusCode();
        Assert.Equal(agreed, (await Plan(shop)).Ready.Single().CustomerId);

        (await shop.PostJsonAsync($"/api/v1/customers/{agreed}/reminded", new { })).EnsureSuccessStatusCode();
        var after = await Plan(shop);
        Assert.Empty(after.Ready);
        Assert.Equal("recent", after.Held.Single(h => h.CustomerId == agreed).Hold);
    }

    [Fact]
    public async Task Stopping_or_changing_the_number_ends_the_consent()
    {
        var (shop, _) = await api.SignUpAsync("Stop Store");
        var customer = await Customer(shop, "Ramesh", ApiFactory.NewPhone());
        await Owes(shop, customer, 10);
        async Task Agree() => (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/reminder-consent", new { given = true })).EnsureSuccessStatusCode();

        await Agree();
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/reminder-consent", new { given = false })).EnsureSuccessStatusCode();
        Assert.Empty((await Plan(shop)).Ready);

        await Agree();
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}", new { name = "Ramesh", phone = ApiFactory.NewPhone() })).EnsureSuccessStatusCode();
        Assert.Empty((await Plan(shop)).Ready); // agreement was for the old number
    }

    [Fact]
    public async Task Someone_with_no_number_cannot_be_marked_as_agreeing()
    {
        var (shop, _) = await api.SignUpAsync("No Phone Store");
        var customer = await Customer(shop, "Ramesh", null);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/reminder-consent", new { given = true })).StatusCode);
    }
}
