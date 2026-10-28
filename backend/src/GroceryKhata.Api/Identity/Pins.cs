namespace GroceryKhata.Api.Identity;

/// <summary>Counter PINs for staff: 4 to 6 digits, and not one anyone would guess first.</summary>
public static class Pins
{
    public static bool LooksLikePin(string? value) => value is { Length: >= 4 and <= 6 } && value.All(char.IsAsciiDigit);

    /// <summary>Why a PIN can't be used, or null when it's fine: all one digit (0000) or a run (1234, 9876) is refused.</summary>
    public static string? Problem(string? pin)
    {
        if (!LooksLikePin(pin))
        {
            return "Choose a PIN of 4 to 6 digits.";
        }

        var digits = pin!.Select(c => c - '0').ToArray();
        var same = digits.All(d => d == digits[0]);
        var up = digits.Zip(digits.Skip(1)).All(p => p.Second == p.First + 1);
        var down = digits.Zip(digits.Skip(1)).All(p => p.Second == p.First - 1);
        return same || up || down ? "That PIN is too easy to guess. Choose another." : null;
    }
}
