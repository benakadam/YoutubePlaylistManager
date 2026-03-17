using Microsoft.Extensions.Options;
using NReco.VideoConverter;
using System.Diagnostics;
using YoutubePlaylistManager.Cli.Options;

namespace YoutubePlaylistManager.Cli.Manager;
public class DownloadManager(IOptions<DownloadManagerOptions> options)
{
    private bool _isUpdated = false;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly FFMpegConverter _converter = new();
    private readonly DownloadManagerOptions _options = options.Value;


    public async Task DownloadWebmAudioAsync(string url)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
        string exePath = Path.Combine(projectRoot, "Thirdparty", "yt-dlp.exe");

        await EnsureUpdated(exePath);

        string outputTemplate = Path.Combine(_options.DownloadPath, "%(title)s.%(ext)s");
        string args = $"-f bestaudio --extract-audio --audio-format mp3 --audio-quality 0 " +
                      $"\"{url}\" -o \"{outputTemplate}\"";

        await RunProcessAsync(exePath, args);

        // --- Biztonsági ellenőrzés ---
        string? downloadedFile = Directory.GetFiles(_options.DownloadPath, "*.mp3")
            .OrderByDescending(File.GetCreationTimeUtc)
            .FirstOrDefault();

        if (downloadedFile == null)
        {
            // ha nem jött létre mp3 → keress webm-et és konvertáld
            string? webmFile = Directory.GetFiles(_options.DownloadPath, "*.webm")
                .OrderByDescending(File.GetCreationTimeUtc)
                .FirstOrDefault();

            if (webmFile != null)
            {
                string mp3File = Path.ChangeExtension(webmFile, ".mp3");
                _converter.ConvertMedia(webmFile, mp3File, "mp3");
                File.Delete(webmFile);
            }
        }
    }

    private static Task RunProcessAsync(string fileName, string args)
    {
        var tcs = new TaskCompletionSource<bool>();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.Exited += (s, e) =>
        {
            tcs.TrySetResult(true);
            process.Dispose();
        };

        process.Start();
        return tcs.Task;
    }

    private async Task EnsureUpdated(string exePath)
    {
        if (_isUpdated) return;

        await _updateLock.WaitAsync();
        try
        {
            if (!_isUpdated)
            {
                await RunProcessAsync(exePath, "-U");
                _isUpdated = true;
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }
}
