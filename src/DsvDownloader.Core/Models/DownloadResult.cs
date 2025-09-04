using System.Net;

namespace DsvDownloader.Core.Models;

public sealed class DownloadResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public long Bytes { get; init; }
    public string? ErrorMessage { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
}

