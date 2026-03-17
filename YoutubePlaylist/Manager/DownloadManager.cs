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
                      $"--print after_move:filepath " +
                      $"\"{url}\" -o \"{outputTemplate}\"";

        string output = await RunProcessAsync(exePath, args);

        string? filePath = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (filePath == null || !File.Exists(filePath))
        {
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

    private static async Task<string> RunProcessAsync(string fileName, string args)
    {
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
            }
        };

        process.Start();

        Task<string> stdOut = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErr = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await stdOut;

        process.Dispose();
        return output;
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
