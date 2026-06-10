namespace ZooSanMarino.Application.Calculos;

public static class CompanyCalculos
{
    public static string? BuildLogoDataUrl(byte[]? bytes, string? contentType)
    {
        if (bytes == null || bytes.Length == 0) return null;
        if (string.IsNullOrWhiteSpace(contentType)) contentType = "image/png";
        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>
    /// Extrae bytes y content-type de un dataURL.
    /// Retorna false si logoDataUrl es null (sin cambio).
    /// Si es string vacío, clear=true (borrar logo).
    /// Límite: 512 KB, solo image/*.
    /// </summary>
    public static bool TryExtractLogo(
        string? logoDataUrl,
        out byte[]? bytes,
        out string? contentType,
        out bool clear)
    {
        bytes = null;
        contentType = null;
        clear = false;

        if (logoDataUrl == null) return false;

        var s = logoDataUrl.Trim();
        if (s.Length == 0) { clear = true; return true; }

        if (!s.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        var comma = s.IndexOf(',');
        if (comma < 0) return false;

        var meta = s.Substring(5, comma - 5);
        var data = s[(comma + 1)..];

        var metaParts = meta.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (metaParts.Length == 0) return false;

        var ct = metaParts[0];
        var isBase64 = metaParts.Any(p => p.Equals("base64", StringComparison.OrdinalIgnoreCase));
        if (!isBase64) return false;
        if (!ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            var raw = Convert.FromBase64String(data);
            if (raw.Length > 512 * 1024) return false;
            bytes = raw;
            contentType = ct;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
