using System.Text;
using System.Text.Json;

namespace DsvDownloader.Core;

internal static class JwtHelper
{
    public static string? TryGetIssuer(string jwt)
    {
        try
        {
            var parts = jwt.Split('.')
                           .Where(p => !string.IsNullOrEmpty(p))
                           .Take(3)
                           .ToArray();
            if (parts.Length < 2) return null;
            var payload = Base64UrlDecode(parts[1]);
            var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("iss", out var issProp))
            {
                return issProp.GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecodeToBytes(string input)
    {
        input = input.Replace('-', '+').Replace('_', '/');
        switch (input.Length % 4)
        {
            case 2: input += "=="; break;
            case 3: input += "="; break;
        }
        return Convert.FromBase64String(input);
    }

    private static string Base64UrlDecode(string input)
    {
        var bytes = Base64UrlDecodeToBytes(input);
        return Encoding.UTF8.GetString(bytes);
    }
}

