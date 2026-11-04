namespace GroceryKhata.Api.Common;

/// <summary>Shared answers for endpoints. Files use it with <c>using static GroceryKhata.Api.Common.Problems;</c>.</summary>
public static class Problems
{
    /// <summary>A 400 with one field's message, shown under that field in the app.</summary>
    public static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>
    /// An enum value from the address ("buffalo", "in_stock"), as the app writes them. Minimal APIs bind query enums
    /// case-sensitively and without the underscore, so endpoints take a string and read it with this. Null when unknown.
    /// </summary>
    public static T? FromQuery<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value?.Replace("_", ""), ignoreCase: true, out var parsed) ? parsed : null;
}
