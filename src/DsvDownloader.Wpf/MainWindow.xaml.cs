using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using DsvDownloader.Core;
using DsvDownloader.Core.Models;

namespace DsvDownloader.Wpf;

public partial class MainWindow : Window
{
    private readonly DownloadService _downloader = new();
    private string _folder = @"A:\\DSV";
    private string? _lastSavedPath;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        this.Title = $"DSV Video Downloader v{AppVersion.Display}";
        InitializeDefaults();
        VersionText.Text = $"v{AppVersion.Display}";
    }

    private void InitializeDefaults()
    {
        try
        {
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
            }
        }
        catch
        {
            _folder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }
        FolderText.Text = _folder;
        UrlBox.TextChanged += (_, __) => UpdateValidationState();
        UpdateValidationState();
    }

    private void UpdateValidationState()
    {
        if (UrlValidator.TryValidate(UrlBox.Text, out var reason))
        {
            StatusText.Text = "";
            DownloadBtn.IsEnabled = true;
        }
        else
        {
            StatusText.Text = reason ?? "";
            DownloadBtn.IsEnabled = false;
        }
    }

    private void PasteBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var t = System.Windows.Clipboard.GetText();
                UrlBox.Text = t?.Trim() ?? string.Empty;
            }
        }
        catch { }
    }

    private void ValidateBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdateValidationState();
    }

    private void ChooseFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose download folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = _folder
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _folder = dlg.SelectedPath;
            FolderText.Text = _folder;
        }
    }

    private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!UrlValidator.TryValidate(UrlBox.Text, out var reason))
        {
            StatusText.Text = reason ?? "Invalid URL.";
            return;
        }

        ToggleUI(isDownloading: true);
        _cts = new CancellationTokenSource();
        _lastSavedPath = null;
        Progress.Value = 0;
        ProgressDetail.Text = string.Empty;
        StatusText.Text = "Downloading...";

        var progress = new Progress<DownloadProgress>(p =>
        {
            double percent = p.TotalBytes.HasValue && p.TotalBytes.Value > 0
                ? (double)p.BytesReceived / p.TotalBytes.Value * 100.0
                : 0;
            Progress.Value = Math.Min(100, Math.Max(0, percent));

            var sb = new StringBuilder();
            sb.Append(FormatBytes(p.BytesReceived));
            if (p.TotalBytes.HasValue) sb.Append($" / {FormatBytes(p.TotalBytes.Value)}");
            if (p.SpeedBytesPerSecond.HasValue) sb.Append($"  •  {FormatBytes((long)p.SpeedBytesPerSecond.Value)}/s");
            if (p.EstimatedRemaining.HasValue) sb.Append($"  •  ETA {FormatEta(p.EstimatedRemaining.Value)}");
            ProgressDetail.Text = sb.ToString();
        });

        try
        {
            var result = await _downloader.DownloadAsync(UrlBox.Text, _folder, progress, _cts.Token);
            if (result.Success)
            {
                _lastSavedPath = result.FilePath;
                StatusText.Text = $"Saved: {result.FilePath} ({FormatBytes(result.Bytes)})";
                OpenBtn.Visibility = Visibility.Visible;
                ShowBtn.Visibility = Visibility.Visible;
                Progress.Value = 100;
            }
            else
            {
                StatusText.Text = result.ErrorMessage ?? "Download failed.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            ToggleUI(isDownloading: false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSavedPath is null || !File.Exists(_lastSavedPath)) return;
        try { Process.Start(new ProcessStartInfo(_lastSavedPath) { UseShellExecute = true }); } catch { }
    }

    private void ShowBtn_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastSavedPath ?? _folder;
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select, \"{path}\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void ToggleUI(bool isDownloading)
    {
        UrlBox.IsEnabled = !isDownloading;
        PasteBtn.IsEnabled = !isDownloading;
        ValidateBtn.IsEnabled = !isDownloading;
        ChooseFolderBtn.IsEnabled = !isDownloading;
        DownloadBtn.IsEnabled = !isDownloading;
        CancelBtn.Visibility = isDownloading ? Visibility.Visible : Visibility.Collapsed;
        if (!isDownloading)
        {
            // Hide open/show buttons until a successful save
            if (_lastSavedPath is null)
            {
                OpenBtn.Visibility = Visibility.Collapsed;
                ShowBtn.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"]; double size = bytes; int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalHours >= 1) return $"{(int)eta.TotalHours}h {eta.Minutes}m";
        if (eta.TotalMinutes >= 1) return $"{(int)eta.TotalMinutes}m {eta.Seconds}s";
        return $"{eta.Seconds}s";
    }
}
