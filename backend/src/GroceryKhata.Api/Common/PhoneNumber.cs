namespace GroceryKhata.Api.Common;

/// <summary>Phone numbers in E.164 form (+919876543210). Indian 10-digit numbers get +91.</summary>
public static class PhoneNumber
{
    /// <summary>The E.164 form, or null when the input isn't a usable mobile number.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // Any character other than digits, spaces, dashes, brackets or a leading + means it isn't a phone number.
        if (input.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '+')))
        {
            return null;
        }

        var hasPlus = input.TrimStart().StartsWith('+');
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());

        if (!hasPlus)
        {
            if (digits.Length == 11 && digits[0] == '0')
            {
                digits = digits[1..];
            }

            if (digits.Length == 10 && digits[0] is >= '6' and <= '9')
            {
                return "+91" + digits;
            }

            if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) && digits[2] is >= '6' and <= '9')
            {
                return "+" + digits;
            }

            return null;
        }

        return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? "+" + digits : null;
    }

    /// <summary>Masked for logs: +919876543210 → +91 98XXX XX210.</summary>
    public static string Mask(string e164)
    {
        if (e164.StartsWith("+91", StringComparison.Ordinal) && e164.Length == 13)
        {
            var local = e164[3..];
            return $"+91 {local[..2]}XXX XX{local[^3..]}";
        }

        return e164.Length > 4 ? new string('X', e164.Length - 3) + e164[^3..] : "XXX";
    }
}
