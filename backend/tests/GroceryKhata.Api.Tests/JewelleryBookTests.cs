using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Girvi loans, savings schemes, orders and repairs, and approval memos at a jewellery shop.</summary>
[Collection(ApiCollection.Name)]
public sealed class JewelleryBookTests(ApiFactory api)
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);

    [Fact]
    public void Girvi_interest_counts_full_months_or_days()
    {
        // Month-wise: any part of a month is a whole month.
        Assert.Equal(200m, GirviMath.Interest(10_000m, 2m, false, Jan1, new DateOnly(2026, 1, 10), []));
        Assert.Equal(200m, GirviMath.Interest(10_000m, 2m, false, Jan1, new DateOnly(2026, 2, 1), []));
        Assert.Equal(400m, GirviMath.Interest(10_000m, 2m, false, Jan1, new DateOnly(2026, 2, 2), []));
        // Day-wise: 45 days is a month and a half.
        Assert.Equal(300m, GirviMath.Interest(10_000m, 2m, true, Jan1, Jan1.AddDays(45), []));
        // ₹5,000 of principal paid back on day 30: the rest of the time runs on ₹5,000.
        Assert.Equal(250m, GirviMath.Interest(10_000m, 2m, true, Jan1, Jan1.AddDays(45), [(Jan1.AddDays(30), 5_000m)]));
    }

    private static async Task<Guid> Customer(HttpClient shop, string name)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customers", new { id, name })).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task A_girvi_loan_leaves_the_drawer_and_closes_when_paid_back()
    {
        var (shop, _) = await api.SignUpAsync("Lakshmi Sarafa");
        var customer = await Customer(shop, "Ramu");
        var loan = await (await shop.PostJsonAsync("/api/v1/jewellery/girvi", new { customerId = customer, items = "Gold chain", metal = "gold", gross = 10m, reading = 91.6m, principal = 30_000m, ratePerMonth = 1.5m }))
            .ReadAsync<GirviDto>();
        Assert.Equal((1, 9.160m, 30_000m), (loan.Number, loan.Fine, loan.PrincipalLeft));
        Assert.Equal(450m, loan.InterestDue); // the first month counts in full at once

        var paid = await (await shop.PostJsonAsync($"/api/v1/jewellery/girvi/{loan.Id}/payments", new { amount = 30_450m, mode = "cash" })).ReadAsync<GirviDto>();
        Assert.NotNull(paid.ClosedOn);
        Assert.Equal((450m, 30_000m), (paid.Payments[0].InterestPart, paid.Payments[0].PrincipalPart));

        // The drawer: ₹30,000 out, ₹30,450 back.
        var drawer = await shop.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.Equal(450m, drawer.CashIn - drawer.CashOut);

        var tooMuch = await shop.PostJsonAsync($"/api/v1/jewellery/girvi/{loan.Id}/payments", new { amount = 10m });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    [Fact]
    public async Task A_gram_scheme_saves_gold_at_each_days_rate_and_the_rise_comes_off_the_bill()
    {
        var (shop, _) = await api.SignUpAsync("Swarna Bachat");
        async Task Rate(decimal r) => await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = 10_000m }, new { metal = "gold", purity = 916, sellPerGram = r } } });
        var customer = await Customer(shop, "Kavita");
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/jewellery/schemes", new { name = "Gold 3", months = 3, instalment = 7_000m, kind = "grams" })).StatusCode);
        var plan = await (await shop.PostJsonAsync("/api/v1/jewellery/schemes", new { name = "Gold 3", months = 3, instalment = 7_000m, kind = "grams", purity = 916 })).ReadAsync<SchemePlanDto>();
        Assert.Equal(("grams", 916), (plan.Kind, plan.Purity));
        var member = await (await shop.PostJsonAsync("/api/v1/jewellery/schemes/members", new { planId = plan.Id, customerId = customer })).ReadAsync<SchemeMemberDto>();

        // ₹7,000 at ₹7,000/g = 1 g; at ₹7,000/g again = 1 g; at ₹8,750/g = 0.8 g: 2.8 g for ₹21,000.
        foreach (var r in new[] { 7_000m, 7_000m, 8_750m })
        {
            await Rate(r);
            member = await (await shop.PostJsonAsync($"/api/v1/jewellery/schemes/members/{member.Id}/instalments", new { entryId = Guid.CreateVersion7(), mode = "upi" })).ReadAsync<SchemeMemberDto>();
        }

        Assert.Equal((true, 2.8m, 21_000m), (member.Complete, member.Grams, member.PaidTotal));
        // Today 22K is ₹9,000/g: 2.8 g is worth ₹25,200, ₹4,200 more than paid. It comes off the making (₹5,000) first.
        await Rate(9_000m);
        var chain = new { name = "Chain", metal = "gold", purity = 916, gross = 10m, makingType = "per_gram", makingValue = 500m, huid = "GS12CH" };
        var quote = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { customerId = customer, lines = new[] { chain }, schemeMemberId = member.Id })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal((4_200m, 800m, 90_800m), (quote.SchemeDiscount, quote.Lines[0].MakingAmount, quote.Taxable));

        // Gold falls below what was paid: nothing extra comes off, and the ₹21,000 on the khata still pays.
        await Rate(6_000m);
        var down = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { customerId = customer, lines = new[] { chain }, schemeMemberId = member.Id })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal((0m, 21_000m), (down.SchemeDiscount, down.KhataAdvance));
    }

    [Fact]
    public async Task A_finished_scheme_takes_its_bonus_off_making_and_the_instalments_pay_the_bill()
    {
        var (shop, _) = await api.SignUpAsync("Ganesh Jewellers");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = 10_000m } } });
        var customer = await Customer(shop, "Sunita");
        var plan = await (await shop.PostJsonAsync("/api/v1/jewellery/schemes", new { name = "Swarna 3", months = 3, instalment = 10_000m, bonus = 2_000m })).ReadAsync<SchemePlanDto>();
        var member = await (await shop.PostJsonAsync("/api/v1/jewellery/schemes/members", new { planId = plan.Id, customerId = customer })).ReadAsync<SchemeMemberDto>();
        for (var i = 0; i < 3; i++)
        {
            member = await (await shop.PostJsonAsync($"/api/v1/jewellery/schemes/members/{member.Id}/instalments", new { entryId = Guid.CreateVersion7(), mode = "cash" })).ReadAsync<SchemeMemberDto>();
        }

        Assert.True(member.Complete);
        var ring = new { name = "Ring", metal = "gold", purity = 999, gross = 3m, makingType = "per_piece", makingValue = 3_000m, huid = "RR11SS" };
        var quote = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { customerId = customer, lines = new[] { ring }, schemeMemberId = member.Id })).ReadAsync<JewelleryQuoteDto>();
        // 3 g × ₹10,000 = ₹30,000 + making ₹3,000 − bonus ₹2,000 = ₹31,000 taxable; ₹930 GST.
        Assert.Equal((2_000m, 31_000m, 31_930m, 30_000m), (quote.SchemeDiscount, quote.Taxable, quote.Total, quote.KhataAdvance));

        var bill = await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), customerId = customer, lines = new[] { ring }, schemeMemberId = member.Id, moneyPaid = 1_930m, mode = "cash" });
        Assert.Equal(HttpStatusCode.OK, bill.StatusCode);
        var after = (await shop.GetJsonAsync<List<SchemeMemberDto>>("/api/v1/jewellery/schemes/members?status=all")).Single();
        Assert.NotNull(after.RedeemedBillId);
        var again = await shop.PostJsonAsync("/api/v1/jewellery/quote", new { customerId = customer, lines = new[] { ring }, schemeMemberId = member.Id });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task A_repair_is_billed_at_18_percent_and_an_order_takes_an_advance()
    {
        var (shop, _) = await api.SignUpAsync("Kundan Jewellers");
        var customer = await Customer(shop, "Asha");
        var due = DateTime.UtcNow.AddHours(5.5).AddDays(5).ToString("yyyy-MM-dd");
        var order = await (await shop.PostJsonAsync("/api/v1/jewellery/orders", new { kind = "order", customerId = customer, description = "Mangalsutra, 22K", metal = "gold", purity = 916, weight = 12m, dueOn = due })).ReadAsync<JewelleryOrderDto>();
        order = await (await shop.PostJsonAsync($"/api/v1/jewellery/orders/{order.Id}/advance", new { entryId = Guid.CreateVersion7(), amount = 20_000m, mode = "upi" })).ReadAsync<JewelleryOrderDto>();
        Assert.Equal(20_000m, order.Advance);

        var repair = await (await shop.PostJsonAsync("/api/v1/jewellery/orders", new { kind = "repair", customerId = customer, description = "Payal hook", metal = "silver", weight = 40m, dueOn = due })).ReadAsync<JewelleryOrderDto>();
        var done = await (await shop.PostJsonAsync($"/api/v1/jewellery/orders/{repair.Id}/deliver-repair", new { billId = Guid.CreateVersion7(), weightOut = 40.2m, charge = 200m, moneyPaid = 236m, mode = "cash" })).ReadAsync<JewelleryOrderDto>();
        Assert.Equal(("delivered", 40.200m), (done.Status, done.WeightOut!.Value));
        var bill = (await shop.GetJsonAsync<BillView>($"/api/v1/bills/{done.BillId}")).Bill;
        Assert.Equal(236m, bill.Total);
    }

    [Fact]
    public async Task Pieces_on_approval_come_back_weighed_or_get_billed()
    {
        var (shop, _) = await api.SignUpAsync("Shree Jewellers");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = 10_000m } } });
        async Task<JewelleryPieceDto> Piece(string name) => await (await shop.PostJsonAsync("/api/v1/jewellery/pieces", new { name, metal = "gold", purity = 916, gross = 5m, huid = $"HU{name.Length:00}{name[0]}X" })).ReadAsync<JewelleryPieceDto>();
        var a = await Piece("Bangle");
        var b = await Piece("Earring");
        var memo = await (await shop.PostJsonAsync("/api/v1/jewellery/approvals", new { party = "Mrs Kapoor", pieceIds = new[] { a.Id, b.Id }, days = 2 })).ReadAsync<ApprovalDto>();
        Assert.Equal(2, memo.Items.Count);
        Assert.All(await shop.GetJsonAsync<List<JewelleryPieceDto>>("/api/v1/jewellery/pieces"), p => Assert.True(p.OnApproval));

        // One comes back lighter: flagged. The other is bought: the memo closes.
        memo = await (await shop.PostJsonAsync($"/api/v1/jewellery/approvals/{memo.Id}/return", new { pieceId = a.Id, grossBack = 4.98m })).ReadAsync<ApprovalDto>();
        Assert.True(memo.Items.Single(i => i.PieceId == a.Id).WeightDiffers);
        (await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), lines = new[] { new { pieceId = b.Id } }, moneyPaid = 47_174m, mode = "upi" })).EnsureSuccessStatusCode();
        var closed = (await shop.GetJsonAsync<List<ApprovalDto>>("/api/v1/jewellery/approvals?status=all")).Single();
        Assert.NotNull(closed.ClosedOn);
        Assert.True(closed.Items.Single(i => i.PieceId == b.Id).Sold);
    }
}
