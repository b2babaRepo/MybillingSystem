using System.Net;
using GroceryKhata.Api.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Reading a distributor's bill photo: the model's output is bounded, matched to the shop's medicines and checked; nothing saves on its own.</summary>
public sealed class InvoicePhotoTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Fact]
    public void Other_trades_match_by_shared_words_and_size_with_no_batch_or_expiry_needed()
    {
        var white20 = (Guid.NewGuid(), "Apcolite Emulsion White 20L");
        var white10 = (Guid.NewGuid(), "Apcolite Emulsion White 10L");
        var cement = (Guid.NewGuid(), "Cement");
        (Guid, string)[] items = [white10, white20, cement];

        Assert.Equal(white20, InvoicePhotos.Match("ASIAN PAINTS APCOLITE EMULSION WHITE 20L", items, medical: false));
        Assert.Equal(cement, InvoicePhotos.Match("ACC CEMENT 50 KG", items, medical: false));
        Assert.Null(InvoicePhotos.Match("APCOLITE PRIMER 4L", items, medical: false));

        var guess = new InvoiceGuess("Sharma Traders", "S-9", null, [new InvoiceLineGuess("ACC CEMENT 50 KG", null, null, null, 10m, 0m, 380m, 0m, 18m)]);
        var draft = InvoicePhotos.Draft(guess, [(cement.Item1, cement.Item2, null)], [], Today, medical: false);
        Assert.Empty(draft.Lines.Single().Problems);
    }

    [Fact]
    public async Task The_daily_ai_allowance_is_kept_in_the_database_so_a_restart_cannot_reset_it()
    {
        var day = DateOnly.FromDayNumber(Random.Shared.Next(800_000, 3_000_000));
        async Task<bool> Take()
        {
            using var scope = api.Services.CreateScope();
            return await GroceryKhata.Api.Common.AiBudget.TryTakeAsync(scope.ServiceProvider.GetRequiredService<GroceryKhata.Api.Persistence.AppDbContext>(), day, 2, default);
        }

        var taken = new List<bool>();
        for (var i = 0; i < 4; i++) taken.Add(await Take());
        Assert.Equal(new List<bool> { true, true, false, false }, taken);
    }

    [Fact]
    public void Model_output_is_bounded_and_unreadable_values_become_null()
    {
        var guess = ClaudeInvoiceReader.Parse("""
            {"supplierName":"Shree Pharma Distributors","billNo":"SPD/881","grandTotal":1491.0,"lines":[
              {"name":"DOLO 650 TAB 15'S","batchNo":"dob2611","expiryMonth":"2027-05","mrp":33.5,"quantity":20,"freeQuantity":2,"rate":24.8,"discountPercent":0,"gstRate":5},
              {"name":"PAN 40 TAB","batchNo":null,"expiryMonth":"05/27","mrp":-4,"quantity":10,"freeQuantity":0,"rate":95,"discountPercent":150,"gstRate":12},
              {"name":"","batchNo":"X","expiryMonth":null,"mrp":1,"quantity":1,"freeQuantity":0,"rate":1,"discountPercent":0,"gstRate":5}
            ]}
            """);
        Assert.Equal(("Shree Pharma Distributors", "SPD/881", 2), (guess.SupplierName, guess.BillNo, guess.Lines.Count));
        Assert.Equal(("DOB2611", "2027-05", 2m), (guess.Lines[0].BatchNo, guess.Lines[0].ExpiryMonth, guess.Lines[0].FreeQuantity!.Value));
        // A month not in YYYY-MM, a negative MRP and a 150% discount are not trusted.
        Assert.Equal(((string?)null, (decimal?)null, (decimal?)null), (guess.Lines[1].ExpiryMonth, guess.Lines[1].Mrp, guess.Lines[1].DiscountPercent));
    }

    [Fact]
    public void Lines_match_by_brand_and_strength_never_a_different_strength()
    {
        var products = new (Guid, string)[] { (Guid.NewGuid(), "Dolo 650 (strip of 15)"), (Guid.NewGuid(), "Dolo 500 (strip of 10)"), (Guid.NewGuid(), "Pan 40 (strip of 15)") };
        Assert.Equal("Dolo 650 (strip of 15)", InvoicePhotos.Match("DOLO-650 TAB 15'S", products)?.Name);
        Assert.Equal("Pan 40 (strip of 15)", InvoicePhotos.Match("PAN 40MG TAB", products.Append((Guid.NewGuid(), "Pantop 40")))?.Name);
        Assert.Null(InvoicePhotos.Match("DOLO 1000 TAB", products));
        Assert.Null(InvoicePhotos.Match("CROCIN ADVANCE", products));
    }

    [Fact]
    public void The_draft_flags_what_to_check_and_a_total_that_does_not_add_up()
    {
        var dolo = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var guess = new InvoiceGuess("SHREE PHARMA DISTRIBUTORS", "SPD/881", 900m,
        [
            new InvoiceLineGuess("DOLO 650 TAB 15'S", "B1", "2027-05", 34m, 20, 2, 24.8m, 0, 5),
            new InvoiceLineGuess("UNKNOWN SYRUP", null, "2026-12", 50m, 5, 0, 60m, 0, 12),
            new InvoiceLineGuess("DOLO 650 TAB 15'S", "OLD", "2026-09", 33.5m, 2, 0, 24.8m, 0, 5),
        ]);
        var draft = InvoicePhotos.Draft(guess, [(dolo, "Dolo 650 (strip of 15)", 33.5m)], [(supplier, "Shree Pharma Distributors")], Today);
        Assert.Equal(supplier, draft.SupplierId);
        Assert.Equal(["mrpChanged"], draft.Lines[0].Problems);
        Assert.Equal(dolo, draft.Lines[0].ProductId);
        Assert.Equal(["noMatch", "noBatch", "nearExpiry", "rateAboveMrp"], draft.Lines[1].Problems);
        Assert.Contains("expired", draft.Lines[2].Problems);
        // 20 × 24.8 + 5% = 520.80; 5 × 60 + 12% = 336; 2 × 24.8 + 5% = 52.08: ₹908.88, not the ₹900 printed.
        Assert.Equal((908.88m, true), (draft.LinesTotal, draft.TotalDiffers));
    }

    [Fact]
    public async Task Reading_a_bill_photo_needs_the_owner_ai_consent_and_the_ai_switched_on()
    {
        var (shop, _) = await api.SignUpAsync();
        var photo = new { imageDataUrl = "data:image/png;base64,iVBORw0KGgo=" };
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.PostJsonAsync("/api/v1/suppliers/purchases/photo", photo)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync("/api/v1/me/ai/consent", new { agree = true })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await shop.PostJsonAsync("/api/v1/suppliers/purchases/photo", photo)).StatusCode);
    }
}
