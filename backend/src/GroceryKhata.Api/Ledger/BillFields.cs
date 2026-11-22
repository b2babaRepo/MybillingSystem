using System.Text.Json;
using System.Text.RegularExpressions;

namespace GroceryKhata.Api.Ledger;

/// <summary>One of the shop's own bill fields as filled on a bill: "PO no." = "4471".</summary>
public sealed record BillFieldValue(string? Label, string? Value);

/// <summary>
/// The shop's bill-number prefix and its own extra bill fields: checking them, and keeping them as JSON on the shop
/// (the labels) and on each bill (labels with what was filled in, so a later change of labels leaves old bills as printed).
/// </summary>
public static partial class BillFields
{
    public const int MaxFields = 4;
    public const int MaxLabel = 30;
    public const int MaxValue = 100;

    [GeneratedRegex("^[A-Z0-9]{1,3}$")]
    private static partial Regex PrefixShape();

    /// <summary>The prefix cleaned ("mb " → "MB"), "" to clear it, or null when it isn't 1 to 3 letters or digits.</summary>
    public static string? CleanPrefix(string text)
    {
        var clean = text.Trim().ToUpperInvariant();
        return clean.Length == 0 ? "" : PrefixShape().IsMatch(clean) ? clean : null;
    }

    /// <summary>The labels cleaned (blank ones dropped, trimmed, no repeats), or null when there are too many or one is too long.</summary>
    public static IReadOnlyList<string>? CleanLabels(IEnumerable<string?> labels)
    {
        var clean = labels.Select(l => l?.Trim() ?? "").Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return clean.Count > MaxFields || clean.Any(l => l.Length > MaxLabel) ? null : clean;
    }

    public static string? LabelsJson(IReadOnlyList<string> labels) => labels.Count == 0 ? null : JsonSerializer.Serialize(labels);

    public static IReadOnlyList<string> Labels(string? json) => json is null ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];

    /// <summary>The filled fields to keep on a bill (blank values dropped), or null when one is too long or there are too many.</summary>
    public static (string? Json, string? Problem) ValuesJson(IReadOnlyList<BillFieldValue>? values)
    {
        var filled = (values ?? []).Select(v => new BillFieldValue(v.Label?.Trim(), v.Value?.Trim()))
            .Where(v => !string.IsNullOrEmpty(v.Label) && !string.IsNullOrEmpty(v.Value)).ToList();
        if (filled.Count > MaxFields + 2 || filled.Any(v => v.Label!.Length > MaxLabel || v.Value!.Length > MaxValue))
        {
            return (null, $"Keep each bill field under {MaxValue} letters.");
        }

        return (filled.Count == 0 ? null : JsonSerializer.Serialize(filled, Json), null);
    }

    public static IReadOnlyList<BillFieldValue>? Values(string? json) => json is null ? null : JsonSerializer.Deserialize<List<BillFieldValue>>(json, Json);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
