using YoutubePlaylistManager.Cli.Model;

namespace YoutubePlaylistManager.Cli.Interface;
public interface IDataAccess
{
    public Task<List<string>> GetPlaylistItemsAsync(string playlistName);

    public Task<bool> CreateTableIfNotExistAsync(string tableName);

    public Task InsertPlaylistItemAsync(string playlist, string playlistItem);

    public Task InsertPlaylistItemsAsync(string playlist, List<string> playlistItems);

    public Task InsertDeletedAsync(string playlist, List<string> playlistItems);

    public Task<List<Deleted>> GetLatestDeletedAsync();

    public Task TruncateTableAsync(string tableName);
}
