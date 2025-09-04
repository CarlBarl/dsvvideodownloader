using System.Net;
using System.Net.Http.Headers;
using DsvDownloader.Core.Models;
using System.Diagnostics;

namespace DsvDownloader.Core;

public sealed partial class DownloadService
{
    private readonly HttpClient _http;

    public DownloadService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? CreateDefaultHttpClient();
    }

    public async Task<DownloadResult> DownloadAsync(
        string url,
        string targetDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancel = default)
    {
        if (!UrlValidator.TryValidate(url, out var reason))
        {
            return new DownloadResult { Success = false, ErrorMessage = reason };
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
        }
        catch (Exception ex)
        {
            return new DownloadResult { Success = false, ErrorMessage = $"Cannot access directory: {ex.Message}" };
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // Mirror the minimal PowerShell call that works:
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0");
        req.Headers.Referrer = new Uri("https://play-store-prod.dsv.su.se/");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancel);
        if (!resp.IsSuccessStatusCode)
        {
            return new DownloadResult
            {
                Success = false,
                StatusCode = resp.StatusCode,
                ErrorMessage = MapHttpError(resp.StatusCode)
            };
        }

        if (!IsLikelyMp4(resp.Content.Headers))
        {
            // Fallback to PowerShell's Invoke-WebRequest which is known to work for this endpoint
            var psFallback = await DownloadWithPowerShellAsync(url, targetDirectory, cancel);
            if (!psFallback.Success)
            {
                return new DownloadResult
                {
                    Success = false,
                    StatusCode = resp.StatusCode,
                    ErrorMessage = psFallback.ErrorMessage ?? $"Server did not return MP4 (Content-Type: {resp.Content.Headers.ContentType})"
                };
            }
            return psFallback;
        }

        var baseName = FileNameHelper.DeriveBaseNameFromUrl(url);
        var finalPath = FileNameHelper.EnsureUniquePath(targetDirectory, baseName);
        var tempPath = finalPath + ".part";

        long totalBytes = resp.Content.Headers.ContentLength ?? -1L;
        long received = 0L;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double emaSpeed = 0; // exponential moving average for speed

        try
        {
            await using var source = await resp.Content.ReadAsStreamAsync(cancel);
            await using var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancel)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), cancel);
                received += read;

                var elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed > 0)
                {
                    var inst = received / elapsed;
                    emaSpeed = emaSpeed == 0 ? inst : (emaSpeed * 0.85 + inst * 0.15);
                }

                TimeSpan? eta = null;
                if (totalBytes > 0 && emaSpeed > 0)
                {
                    var remaining = totalBytes - received;
                    eta = TimeSpan.FromSeconds(Math.Max(0, remaining / emaSpeed));
                }

                progress?.Report(new DownloadProgress
                {
                    BytesReceived = received,
                    TotalBytes = totalBytes > 0 ? totalBytes : null,
                    SpeedBytesPerSecond = emaSpeed > 0 ? emaSpeed : null,
                    EstimatedRemaining = eta
                });
            }
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            return new DownloadResult { Success = false, ErrorMessage = "Download canceled by user." };
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return new DownloadResult { Success = false, ErrorMessage = $"Network/IO error: {ex.Message}" };
        }

        try
        {
            if (File.Exists(finalPath))
            {
                // If for some reason exists, pick a new name and move
                finalPath = FileNameHelper.EnsureUniquePath(Path.GetDirectoryName(finalPath)!, Path.GetFileName(finalPath));
            }
            File.Move(tempPath, finalPath);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return new DownloadResult { Success = false, ErrorMessage = $"Finalize error: {ex.Message}" };
        }

        return new DownloadResult { Success = true, FilePath = finalPath, Bytes = received, StatusCode = HttpStatusCode.OK };
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(20)
        };
        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan // we manage timeouts via handler + cancellation
        };
    }

    private static bool IsLikelyMp4(HttpContentHeaders headers)
    {
        var ct = headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(ct)) return true; // Some servers omit
        if (ct.StartsWith("video/mp4", StringComparison.OrdinalIgnoreCase)) return true;
        if (ct.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string MapHttpError(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Access denied or token expired. Fetch a fresh link.",
        HttpStatusCode.NotFound => "Video not found (URL may be stale).",
        _ => $"HTTP error: {(int)code} {code}"
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}

public partial class DownloadService
{
    private async Task<DownloadResult> DownloadWithPowerShellAsync(string url, string targetDirectory, CancellationToken cancel)
    {
        try
        {
            Directory.CreateDirectory(targetDirectory);
        }
        catch (Exception ex)
        {
            return new DownloadResult { Success = false, ErrorMessage = $"Cannot access directory: {ex.Message}" };
        }

        var baseName = FileNameHelper.DeriveBaseNameFromUrl(url);
        var finalPath = FileNameHelper.EnsureUniquePath(targetDirectory, baseName);

        string scriptPath = Path.Combine(Path.GetTempPath(), $"dsvdl_{Guid.NewGuid():N}.ps1");
        string psUrl = EscapePsSingleQuoted(url);
        string psOut = EscapePsSingleQuoted(finalPath);
        var script = "\uFEFF" + // BOM for PS to read UTF-8 correctly
                     "$ErrorActionPreference = 'Stop'\n" +
                     $"Invoke-WebRequest -Uri '{psUrl}' -OutFile '{psOut}' -Headers @{{ Referer='https://play-store-prod.dsv.su.se/' }} -UserAgent 'Mozilla/5.0'\n";
        await File.WriteAllTextAsync(scriptPath, script, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancel);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        var stdOutTask = proc.StandardOutput.ReadToEndAsync();
        var stdErrTask = proc.StandardError.ReadToEndAsync();

        using (cancel.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch { } }))
        {
            await proc.WaitForExitAsync(cancel).ConfigureAwait(false);
        }

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        try { File.Delete(scriptPath); } catch { }

        if (proc.ExitCode != 0)
        {
            TryDelete(finalPath);
            var msg = string.IsNullOrWhiteSpace(stdErr) ? ($"PowerShell download failed (code {proc.ExitCode}).") : stdErr.Trim();
            return new DownloadResult { Success = false, ErrorMessage = msg };
        }

        try
        {
            var info = new FileInfo(finalPath);
            if (!info.Exists || info.Length <= 0)
            {
                return new DownloadResult { Success = false, ErrorMessage = "PowerShell reported success but file is missing or empty." };
            }
            return new DownloadResult { Success = true, FilePath = finalPath, Bytes = info.Length, StatusCode = HttpStatusCode.OK };
        }
        catch (Exception ex)
        {
            return new DownloadResult { Success = false, ErrorMessage = $"Finalize error: {ex.Message}" };
        }
    }

    private static string EscapePsSingleQuoted(string s) => s.Replace("'", "''");
}
