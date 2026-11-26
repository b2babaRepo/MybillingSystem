using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>The server's promotion engine against the cases the app's engine is tested with (shared/promotions.cases.json).</summary>
public sealed class PromotionsTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Load().EnumerateArray())
        {
            data.Add(c.GetProperty("name").GetString()!);
        }

        return data;
    }

    private static JsonElement Load() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "promotions.cases.json"))).RootElement;

    /// <summary>The cases name items "rice", "dal": the same name always gives the same id.</summary>
    private static Guid Id(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Dec(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static int? Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Prices_a_cart_as_the_app_does(string name)
    {
        var c = Load().EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var lines = c.GetProperty("lines").EnumerateArray().Select(l => new Promotions.Line(
            Str(l, "productId") is { } pid ? Id(pid) : null, Str(l, "category"), l.GetProperty("regular").GetDecimal(), l.GetProperty("quantity").GetDecimal())).ToList();
        var rules = c.GetProperty("promotions").EnumerateArray().Select(p => new Promotions.Rule(
            Str(p, "id")!, Str(p, "name")!, Str(p, "kind")!, p.GetProperty("value").GetDecimal(),
            DateTimeOffset.Parse(Str(p, "startsAt")!), DateTimeOffset.Parse(Str(p, "endsAt")!), Str(p, "stoppedAt") is { } stopped ? DateTimeOffset.Parse(stopped) : null,
            Str(p, "scope")!, Str(p, "category"), p.GetProperty("productIds").EnumerateArray().Select(x => Id(x.GetString()!)).ToList(),
            Int(p, "buyQty"), Int(p, "getQty"), Dec(p, "minQty"), Dec(p, "minBill"), Dec(p, "comboPrice"), Int(p, "days"), Int(p, "fromMinute"), Int(p, "toMinute"))).ToList();

        var result = Promotions.Apply(lines, rules, DateTimeOffset.Parse(Str(c, "at")!));

        var expect = c.GetProperty("expect");
        var parts = expect.GetProperty("parts").EnumerateArray().Select(ps => ps.EnumerateArray()
            .Select(p => (p.GetProperty("quantity").GetDecimal(), p.GetProperty("unitPrice").GetDecimal(), Str(p, "offerId"))).ToList()).ToList();
        Assert.Equal(parts, result.Parts.Select(ps => ps.Select(p => (p.Quantity, p.UnitPrice, p.OfferId)).ToList()).ToList());
        var bill = expect.GetProperty("billOff");
        Assert.Equal(bill.ValueKind == JsonValueKind.Null ? null : (Str(bill, "offerId"), bill.GetProperty("amount").GetDecimal()),
            result.Bill is { } b ? (b.OfferId, b.Amount) : ((string?, decimal)?)null);
        Assert.Equal(expect.GetProperty("saved").GetDecimal(), result.Saved);
    }
}
