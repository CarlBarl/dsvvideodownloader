namespace DsvDownloader.Core.Models;

public sealed class DownloadProgress
{
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public double? SpeedBytesPerSecond { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
}

