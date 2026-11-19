using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class YearReportsTests(ApiFactory api)
{
    [Fact]
    public async Task The_trial_balance_agrees_and_shows_the_years_sales_and_the_dues()
    {
        var (shop, _) = await api.SignUpAsync("Trial Balance Store");
        var customer = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Ramesh" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { name = "Atta", quantity = 2m, unitPrice = 250m } }, paidAmount = 200m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        var tb = await shop.GetJsonAsync<TrialBalanceDto>("/api/v1/reports/trial-balance");
        Assert.Equal(tb.TotalDebit, tb.TotalCredit);
        Assert.Equal(500m, tb.Lines.Single(l => l.Key == "sales").Credit);
        Assert.Equal(300m, tb.Lines.Single(l => l.Key == "debtors").Debit);
        Assert.Equal(200m, tb.Lines.Single(l => l.Key == "cash").Debit);
        Assert.All(tb.Lines, l => Assert.True(l.Debit == 0 || l.Credit == 0));

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.GetAsync($"/api/v1/reports/trial-balance?fy={DateTime.UtcNow.Year + 2}")).StatusCode);
    }

    [Fact]
    public async Task The_annual_gst_summary_adds_up_the_years_months()
    {
        var (shop, _) = await api.SignUpAsync("Annual GST Store");
        var gst = await shop.GetJsonAsync<GstAnnualDto>("/api/v1/gst/annual");
        Assert.NotEmpty(gst.Months);
        Assert.StartsWith(gst.Months[0].Month[..4], gst.FinancialYear);
        Assert.EndsWith("-04", gst.Months[0].Month);
        Assert.Equal(gst.Months.Sum(m => m.Taxable), gst.Taxable);
        Assert.Equal(gst.Taxable - gst.B2bSales, gst.B2cSales);
    }
}
