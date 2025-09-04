using System.Text.RegularExpressions;

namespace DsvDownloader.Core;

public static class UrlValidator
{
    private static readonly Regex Pattern = new(
        pattern: "^(https?):\\/\\/([a-z0-9-]+)\\.play-store-prod\\.dsv\\.su\\.se\\/.+\\.mp4\\?token=.+$",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryValidate(string? url, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            reason = "Enter a URL.";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            reason = "Invalid URL format.";
            return false;
        }

        if (!Pattern.IsMatch(url))
        {
            reason = "URL must be a DSV .mp4 link with ?token= on play-store-prod.dsv.su.se.";
            return false;
        }

        // Basic sanity check that token has some length
        var query = uri.Query;
        if (!query.Contains("token=", StringComparison.OrdinalIgnoreCase) || query.Length < 15)
        {
            reason = "Missing or short token in query.";
            return false;
        }

        reason = null;
        return true;
    }
}

