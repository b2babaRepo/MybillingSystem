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
            return data.Length is > 0 && data.Length <= maxBytes ? (type, data) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
