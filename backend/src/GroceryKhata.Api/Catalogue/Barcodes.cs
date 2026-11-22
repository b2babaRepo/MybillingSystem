namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One spelling per barcode, so the same pack always matches: a 12-digit UPC-A is the same code as the 13-digit EAN
/// with a leading 0 (cameras and scanners report either), so it is kept in the 13-digit form. The phone matches the
/// same way (frontend lib/barcode.ts normBarcode).
/// </summary>
public static class Barcodes
{
    /// <summary>Letters and digits only; a 12-digit code gets its leading 0.</summary>
    public static string Normalize(string? code)
    {
        var clean = new string((code ?? "").Where(char.IsAsciiLetterOrDigit).ToArray());
        return clean.Length == 12 && clean.All(char.IsAsciiDigit) ? "0" + clean : clean;
    }

    /// <summary>
    /// For an all-digit EAN-8 or EAN-13 code: does its last digit match? A wrong one is a misread or a typing slip.
    /// Other codes (Code 128 shop labels, letters) have no check digit to test.
    /// </summary>
    public static bool CheckDigitOk(string code)
    {
        if (!code.All(char.IsAsciiDigit) || code.Length is not (8 or 13))
        {
            return true;
        }

        var body = code[..^1];
        // Weights 3,1 from the right of the body (EAN-13 and EAN-8 alike).
        var sum = body.Reverse().Select((c, i) => (c - '0') * (i % 2 == 0 ? 3 : 1)).Sum();
        return (10 - (sum % 10)) % 10 == code[^1] - '0';
    }
}
