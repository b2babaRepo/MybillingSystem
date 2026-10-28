using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GroceryKhata.Api.Catalogue;

namespace GroceryKhata.Api.Planner;

/// <summary>What a household gave: a typed note ("do kilo aata, ek litre sarson tel"), a photo or screenshot, or both.</summary>
public sealed record OrderInput(string? Text, string? ImageMediaType, string? ImageBase64);

public enum ReadConfidence
{
    High,
    Medium,
    Low,
}

/// <summary>
/// One line read from the household's words. <see cref="Said"/> keeps their words, shown to them and to the shop.
/// <see cref="CatalogueItemId"/> is a common-list item or null (not understood, or not on the list).
/// </summary>
public sealed record ReadLine(string Said, int? CatalogueItemId, string Name, decimal? Quantity, Unit? Unit, ReadConfidence Confidence, string? Reason);

/// <summary>Reads an order into lines. Implementations: Claude (when an API key is set) and a rule-based reader for typed text.</summary>
public interface IOrderReader
{
    /// <summary>"claude-…" or "rules": recorded with each proposal.</summary>
    string Name { get; }

    bool CanReadImages { get; }

    Task<IReadOnlyList<ReadLine>> ReadAsync(OrderInput input, CancellationToken ct);
}

/// <summary>
/// Hindi and Hinglish grocery words, used by the rule reader and in the AI's instructions: number words, units, and
/// what people call common-list items ("aata", "cheeni", "sarson ka tel").
/// </summary>
public static class GroceryWords
{
    /// <summary>Number words → value. "paav" (a quarter) and "dhai sau" are measures, handled with the unit.</summary>
    public static readonly IReadOnlyDictionary<string, decimal> Numbers = new Dictionary<string, decimal>
    {
        ["ek"] = 1, ["एक"] = 1, ["do"] = 2, ["दो"] = 2, ["teen"] = 3, ["तीन"] = 3, ["char"] = 4, ["chaar"] = 4, ["चार"] = 4,
        ["paanch"] = 5, ["panch"] = 5, ["पांच"] = 5, ["पाँच"] = 5, ["chhe"] = 6, ["chah"] = 6, ["छह"] = 6, ["saat"] = 7, ["सात"] = 7,
        ["aath"] = 8, ["आठ"] = 8, ["nau"] = 9, ["नौ"] = 9, ["das"] = 10, ["दस"] = 10, ["barah"] = 12, ["darjan"] = 12, ["दर्जन"] = 12,
        ["adha"] = 0.5m, ["aadha"] = 0.5m, ["आधा"] = 0.5m, ["dedh"] = 1.5m, ["डेढ़"] = 1.5m, ["dhai"] = 2.5m, ["ढाई"] = 2.5m,
        ["sava"] = 1.25m, ["सवा"] = 1.25m, ["paune"] = 0.75m,
    };

    public static readonly IReadOnlyDictionary<string, Unit> Units = new Dictionary<string, Unit>
    {
        ["kg"] = Unit.Kg, ["kilo"] = Unit.Kg, ["kilogram"] = Unit.Kg, ["किलो"] = Unit.Kg,
        ["g"] = Unit.G, ["gm"] = Unit.G, ["gram"] = Unit.G, ["grams"] = Unit.G, ["ग्राम"] = Unit.G,
        ["l"] = Unit.Litre, ["ltr"] = Unit.Litre, ["litre"] = Unit.Litre, ["liter"] = Unit.Litre, ["लीटर"] = Unit.Litre,
        ["ml"] = Unit.Ml, ["packet"] = Unit.Packet, ["packets"] = Unit.Packet, ["pkt"] = Unit.Packet, ["पैकेट"] = Unit.Packet,
        ["piece"] = Unit.Piece, ["pieces"] = Unit.Piece, ["pc"] = Unit.Piece, ["pcs"] = Unit.Piece, ["नग"] = Unit.Piece,
    };

    /// <summary>What households call common-list items, by item (English name), beyond the list's own Hindi and English names.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        ["Atta"] = ["aata", "atta", "gehun ka aata", "गेहूं का आटा"],
        ["Rice"] = ["chawal", "chaawal"],
        ["Sugar"] = ["cheeni", "chini", "shakkar"],
        ["Salt"] = ["namak"],
        ["Jaggery"] = ["gud", "gur"],
        ["Mustard oil"] = ["sarson tel", "sarson ka tel", "sarso tel", "kadwa tel"],
        ["Refined oil"] = ["refined", "refine tel"],
        ["Desi ghee"] = ["ghee", "ghi", "घी"],
        ["Toor dal"] = ["arhar dal", "arhar", "tuar dal", "toor"],
        ["Moong dal"] = ["moong"],
        ["Chana dal"] = ["chana"],
        ["Urad dal"] = ["urad"],
        ["Masoor dal"] = ["masoor", "lal dal"],
        ["Turmeric powder"] = ["haldi"],
        ["Red chilli powder"] = ["lal mirch", "mirchi", "mirch"],
        ["Coriander powder"] = ["dhaniya"],
        ["Tea"] = ["chai patti", "chaipatti", "chai", "patti"],
        ["Milk"] = ["doodh", "dudh"],
        ["Curd"] = ["dahi"],
        ["Eggs"] = ["anda", "ande", "egg"],
        ["Biscuits"] = ["biscuit", "parle", "parle g", "parle-g"],
        ["Bath soap"] = ["sabun", "nahane ka sabun"],
        ["Detergent"] = ["surf", "sarf", "washing powder"],
        ["Onion"] = ["pyaz", "pyaaz", "kanda"],
        ["Potato"] = ["aloo", "alu"],
        ["Tomato"] = ["tamatar"],
        ["Cold drink"] = ["coke", "pepsi", "thanda"],
        ["Matchbox"] = ["machis"],
    };

    /// <summary>The common-list item a phrase names: its Hindi or English name, or an alias; longest match wins.</summary>
    public static CatalogueItem? FindItem(string phrase)
    {
        var p = " " + Regex.Replace(phrase.ToLowerInvariant(), @"[^\p{L}\p{M}\p{N}]+", " ").Trim() + " ";
        return CommonCatalogue.Items
            .SelectMany(item => Aliases.GetValueOrDefault(item.NameEn, []).Append(item.Name).Append(item.NameEn)
                .Select(name => (Item: item, Name: " " + name.ToLowerInvariant() + " ")))
            .Where(c => p.Contains(c.Name, StringComparison.Ordinal))
            .OrderByDescending(c => c.Name.Length)
            .Select(c => c.Item)
            .FirstOrDefault();
    }
}

/// <summary>
/// Reads typed notes with fixed rules, no AI: splits on commas, new lines and "aur"/"and", finds a number word or
/// digits, a unit and a common-list item in each piece. Good enough for "2 kg aata, ek litre sarson tel"; anything
/// it can't place comes back as "not understood" for the household to fix. Used when no AI key is set.
/// </summary>
public sealed class RuleOrderReader : IOrderReader
{
    public string Name => "rules";

    public bool CanReadImages => false;

    public Task<IReadOnlyList<ReadLine>> ReadAsync(OrderInput input, CancellationToken ct)
    {
        var pieces = Regex.Split(input.Text ?? "", @"[,\n;।]+|\s+(?:aur|और|and)\s+", RegexOptions.IgnoreCase)
            .Select(p => p.Trim()).Where(p => p.Length > 0);
        return Task.FromResult<IReadOnlyList<ReadLine>>(pieces.Select(Read).ToList());
    }

    private static ReadLine Read(string said)
    {
        var words = Regex.Split(said.ToLowerInvariant(), @"\s+");
        decimal? quantity = null;
        Unit? unit = null;
        foreach (var word in words)
        {
            var digits = Regex.Match(word, @"^(\d+(?:\.\d+)?)([a-z\p{L}]*)$");
            if (quantity is null && digits.Success)
            {
                quantity = decimal.Parse(digits.Groups[1].Value, CultureInfo.InvariantCulture);
                if (GroceryWords.Units.TryGetValue(digits.Groups[2].Value, out var attached))
                {
                    unit = attached;
                }
            }
            else if (quantity is null && GroceryWords.Numbers.TryGetValue(word, out var number))
            {
                quantity = number;
            }
            else if (unit is null && GroceryWords.Units.TryGetValue(word, out var u))
            {
                unit = u;
            }
            else if (word is "paav" or "pav" or "पाव")
            {
                // "paav" is a quarter kilo; "ek paav" = 250 g, "do paav" = 500 g.
                quantity = (quantity ?? 1) * 250;
                unit = Unit.G;
            }
            else if (word is "sau" or "सौ" && quantity is { } q)
            {
                quantity = q * 100; // "dhai sau gram" = 250 g
            }
        }

        var item = GroceryWords.FindItem(said);
        if (item is null)
        {
            return new ReadLine(said, null, said, quantity, unit, ReadConfidence.Low, "Not on the common list, or not understood.");
        }

        var confident = quantity is not null;
        return new ReadLine(said, item.Id, item.Name, quantity ?? 1, unit ?? item.Unit,
            confident ? ReadConfidence.High : ReadConfidence.Medium, confident ? null : "Quantity not said: 1 assumed.");
    }
}

/// <summary>Parses the AI's JSON lines (see <see cref="ClaudeOrderReader"/>'s schema) into <see cref="ReadLine"/>s.</summary>
internal static class ReadLineJson
{
    public static IReadOnlyList<ReadLine> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var lines = new List<ReadLine>();
        foreach (var l in doc.RootElement.GetProperty("lines").EnumerateArray())
        {
            static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            int? id = l.TryGetProperty("catalogueItemId", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : null;
            decimal? qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : null;
            Unit? unit = Enum.TryParse<Unit>(Str(l, "unit"), ignoreCase: true, out var u) ? u : null;
            var confidence = Enum.TryParse<ReadConfidence>(Str(l, "confidence"), ignoreCase: true, out var c) ? c : ReadConfidence.Low;
            lines.Add(new ReadLine(Str(l, "said") ?? "", id, Str(l, "name") ?? Str(l, "said") ?? "", qty, unit, confidence, Str(l, "reason")));
        }

        return lines;
    }
}
