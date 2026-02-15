using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using YoutubePlaylistManager.Cli.Helpers;
using YoutubePlaylistManager.Cli.Interface;
using YoutubePlaylistManager.Cli.Manager;
using YoutubePlaylistManager.Cli.Model;
using YoutubePlaylistManager.Cli.Options;

namespace YoutubePlaylistManager.Cli;

public partial class PlaylistManagerService(
    IDataAccess dataAccess,
    YoutubeApiManager playlistManager,
    DownloadManager downloadManager,
    IOptions<YoutubePlaylistOptions> options)
{

    #region Variables
    private readonly YoutubePlaylistOptions _options = options.Value;
    private const string DOWNLOAD = "Download";
    private const string SECOND_ATTEMPT = "Egyéb videók";
    #endregion

    public async Task StartProcess()
    {
        if (!Helper.IsInternetAvailable())
        {
            Console.WriteLine("Nincs internet kapcsolat!");
            return;
        }

        try
        {
            var playlistsResponse = await playlistManager.GetPlaylistsAsync();
            if (playlistsResponse is null)
            {
                Console.WriteLine("Nem található lejátszási lista");
                return;
            }

            Playlist downloadPlaylist = playlistsResponse.Items.First(x => x.Snippet.Title == DOWNLOAD);
            List<string> downloadVideos = await playlistManager.GetPlaylistItemsAsync(downloadPlaylist.Id);
            List<string> downloadIDs = await playlistManager.GetPlaylistItemsAsync(downloadPlaylist.Id, true);

            List<Playlist> playlists = [.. playlistsResponse.Items.Where(x => x.Snippet.Title != DOWNLOAD)];

            Console.WriteLine("Könyvtár karbantartása");
            await CheckForMissingItemsWithApiAsync(playlists, downloadVideos);
            var affectedPlaylistIds = await CheckForMissingItemsWithHtmlAsync(playlists);

            List<Deleted> deletedVideos = await dataAccess.GetLatestDeletedAsync();
            var index = deletedVideos.FindIndex(d => d.Playlist == SECOND_ATTEMPT);
            WriteOutResult(deletedVideos.Take(index));
            Console.WriteLine($"{SECOND_ATTEMPT}:");
            WriteOutResult(deletedVideos.Skip(index + 1));

            OpenAffectedPlaylistsInBrowser(affectedPlaylistIds);

            if (downloadIDs.Count == 0) return;
            await DownloadVideosAsync(downloadIDs, downloadVideos);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }

        Console.WriteLine("\nKész vagyunk Mester!");
        Console.ReadKey();
    }

    private async Task CheckForMissingItemsWithApiAsync(List<Playlist> playlists, List<string> downloadVideos)
    {
        foreach (var playlist in playlists)
        {
            var currentItems = await playlistManager.GetPlaylistItemsAsync(playlist.Id);
            if (currentItems.Count == 0) continue;

            var playlistTitle = playlist.Snippet.Title;

            await dataAccess.CreateTableIfNotExistAsync(playlistTitle);
            List<string> previousItems = await dataAccess.GetPlaylistItemsAsync(playlistTitle);

            var diffTitles = previousItems.Except(currentItems).ToList();
            diffTitles = [.. diffTitles.Except(downloadVideos)];

            diffTitles.RemoveAll(x => x == "Deleted video");
            if (diffTitles.Count != 0)
                await dataAccess.InsertDeletedAsync(playlistTitle, diffTitles);

            await dataAccess.TruncateTableAsync(playlistTitle);
            await dataAccess.InsertPlaylistItemsAsync(playlistTitle, currentItems);
        }
    }

    private async Task<List<string>> CheckForMissingItemsWithHtmlAsync(List<Playlist> playlists)
    {
        await dataAccess.InsertDeletedAsync(SECOND_ATTEMPT, [""]);
        List<string> affectedPlaylistIds = [];


        foreach (var playlist in playlists)
        {
            string url = _options.PlaylistBaseUrl + playlist.Id;
            using HttpClient httpClient = new();
            string html = await httpClient.GetStringAsync(url);

            if (HasHiddenVideos(html))
            {
                affectedPlaylistIds.Add(playlist.Id);
            }

            MatchCollection matches = Regexes.TitleRuns().Matches(html);
            var matchList = matches.Take(matches.Count - 7);

            List<string> videos = [.. matchList.Select(x => Regex.Unescape(x.Groups[1].Value))];
            List<string> allVideos = await dataAccess.GetPlaylistItemsAsync(playlist.Snippet.Title);

            List<string> missings = [.. allVideos.Take(100).Except(videos)];

            if (missings.Count == 0) continue;

            await dataAccess.InsertDeletedAsync(playlist.Snippet.Title, missings);
        }

        return affectedPlaylistIds;
    }

    private static bool HasHiddenVideos(string html)
    {
        var match = Regexes.YtInitialData().Match(html);
        if (!match.Success)
            return false;

        using var doc = JsonDocument.Parse(match.Groups[1].Value);
        var root = doc.RootElement;

        if (!root.TryGetProperty("alerts", out var alerts))
            return false;

        foreach (var alert in alerts.EnumerateArray())
        {
            if (!alert.TryGetProperty("alertWithButtonRenderer", out var renderer))
                continue;

            if (!renderer.TryGetProperty("text", out var text))
                continue;

            if (!text.TryGetProperty("simpleText", out var simpleText))
                continue;

            string message = simpleText.GetString() ?? string.Empty;

            if (message.Contains("rendelkezésre nem álló videó"))
                return true;
        }

        return false;
    }


    private static void WriteOutResult(IEnumerable<Deleted> deletedVideos)
    {
        foreach (var group in deletedVideos.GroupBy(d => d.Playlist))
        {
            Console.WriteLine(group.Key);
            foreach (var item in group)
            {
                Console.WriteLine(item.Title);
            }
            Console.WriteLine();
        }
    }

    private void OpenAffectedPlaylistsInBrowser(List<string> affectedPlaylistIds)
        => affectedPlaylistIds.ForEach(id =>
                Process.Start(new ProcessStartInfo
                {
                    FileName = _options.PlaylistBaseUrl + id,
                    UseShellExecute = true
                }));

    private async Task DownloadVideosAsync(List<string> downloadIDs, List<string> titles)
    {
        Console.WriteLine("Letöltés");
        Directory.CreateDirectory(_options.DownloadPath);
        string baseUrl = Helper.GetConfigValue("VideoBaseUrl");

        var downloadTasks = downloadIDs.Select(id => downloadManager.DownloadWebmAudioAsync(baseUrl + id)).ToList();

        var progressTask = Task.Run(() => ConsoleManager.ShowProgressBarWhileTasksRunning(downloadTasks));

        await Task.WhenAll(downloadTasks);
        await progressTask;

        Console.WriteLine($"\nA letöltés elkészült Mester!");
        CheckForUnsuccesfulDownloads(titles, RenameFiles());
    }

    private IEnumerable<string> RenameFiles()
    {
        var files = Directory.GetFiles(_options.DownloadPath)
            .Select(x => Path.GetFileNameWithoutExtension(x));

        foreach (string file in files)
        {
            string newFileName = ConsoleManager.ReadInputWithDefault(file);
            if (newFileName == file) continue;

            string destinationPath = Path.Combine(_options.DownloadPath, newFileName + ".mp3");
            if (File.Exists(destinationPath)) continue;

            File.Move(Path.Combine(_options.DownloadPath, file + ".mp3"), destinationPath);
            dataAccess.InsertPlaylistItemAsync("ALLSONGS", newFileName);
        }

        return files;
    }

    private static void CheckForUnsuccesfulDownloads(List<string> titles, IEnumerable<string> files)
    {
        files = files.Select(x => Regexes.FileTitle().Replace(x, ""));

        List<string> unsuccesfulDownloads = [.. titles.Where(title => !files.Contains(Regexes.FileTitle().Replace(title, "")))];

        if (unsuccesfulDownloads.Count > 0)
        {
            Console.WriteLine("\nA következő számok letöltése nem sikerült:");
            unsuccesfulDownloads.ForEach(x => Console.WriteLine(x));
        }
    }
}
