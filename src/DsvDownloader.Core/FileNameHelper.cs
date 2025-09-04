using System.Text;

namespace DsvDownloader.Core;

public static class FileNameHelper
{
    public static string DeriveBaseNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                return FallbackName();
            }
            var decoded = Uri.UnescapeDataString(lastSegment);
            // Strip query if somehow included
            var qIndex = decoded.IndexOf('?', StringComparison.Ordinal);
            if (qIndex >= 0) decoded = decoded[..qIndex];
            if (!decoded.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                decoded += ".mp4";
            }
            decoded = Sanitize(decoded);
            if (string.IsNullOrWhiteSpace(decoded))
                return FallbackName();
            if (decoded.Length > 140)
                decoded = decoded[..140] + ".mp4";
            return decoded;
        }
        catch
        {
            return FallbackName();
        }
    }

    public static string EnsureUniquePath(string directory, string baseName)
    {
        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(directory, baseName);
        if (!File.Exists(candidate)) return candidate;

        var name = Path.GetFileNameWithoutExtension(baseName);
        var ext = Path.GetExtension(baseName);
        for (int i = 1; i < 10_000; i++)
        {
            var attempt = Path.Combine(directory, $"{name}-{i}{ext}");
            if (!File.Exists(attempt)) return attempt;
        }
        // Last resort, timestamp
        return Path.Combine(directory, FallbackName());
    }

    public static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? FallbackName() : cleaned;
    }

    private static string FallbackName()
    {
        return $"dsv_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
    }
}

