namespace DsvDownloader.Core;

public static class TokenMasker
{
    public static string MaskUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var rawQuery = uri.Query.TrimStart('?');
            if (string.IsNullOrEmpty(rawQuery)) return url;
            var parts = rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);
            var rebuilt = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                var key = kv.Length > 0 ? kv[0] : string.Empty;
                if (key.Equals("token", StringComparison.OrdinalIgnoreCase))
                {
                    rebuilt.Add("token=***");
                }
                else
                {
                    rebuilt.Add(part);
                }
            }
            var builder = new UriBuilder(uri) { Query = string.Join('&', rebuilt) };
            return builder.Uri.ToString();
        }
        catch
        {
            return url;
        }
    }
}
