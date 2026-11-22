namespace GroceryKhata.Api.Common;

/// <summary>Photos the phone sends as a data URL ("data:image/webp;base64,AAAA…"), already shrunk on the phone.</summary>
public static class Photos
{
    private static readonly string[] Allowed = ["image/webp", "image/jpeg", "image/png"];

    /// <summary>The photo's type and bytes, or null when it isn't a WebP, JPEG or PNG of at most <paramref name="maxBytes"/>.</summary>
    public static (string ContentType, byte[] Data)? Decode(string? dataUrl, int maxBytes)
    {
        var parts = dataUrl?.Split(',', 2);
        var type = parts is [var head, _] && head.StartsWith("data:", StringComparison.Ordinal) && head.EndsWith(";base64", StringComparison.Ordinal) ? head[5..^7] : null;
        if (type is null || !Allowed.Contains(type))
        {
            return null;
        }

        try
        {
            var data = Convert.FromBase64String(parts![1]);
            // The bytes must really be that kind of picture, not just labelled so: photos are served publicly.
            return data.Length is > 0 && data.Length <= maxBytes && Looks(type, data) ? (type, data) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The file's opening bytes for its type: PNG's signature, JPEG's start marker, WebP's RIFF…WEBP header.</summary>
    private static bool Looks(string type, byte[] d) => type switch
    {
        "image/png" => d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A,
        "image/jpeg" => d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF,
        "image/webp" => d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P',
        _ => false,
    };
}
