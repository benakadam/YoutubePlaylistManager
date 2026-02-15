using System.Data;
using Dapper;
using YoutubePlaylistManager.Cli.Helpers;
using YoutubePlaylistManager.Cli.Interface;
using YoutubePlaylistManager.Cli.Model;

namespace YoutubePlaylistManager.Cli.DataAccess;

public class DataAccess(IDateTimeProvider dateTimeProvider) : IDataAccess
{
    public async Task<List<string>> GetPlaylistItemsAsync(string playlistName)
    {
        playlistName = Helper.SanitizeTableName(playlistName);

        return await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            var result = await connection.QueryAsync<string>($"SELECT Name FROM {playlistName}");
            return result.ToList();
        });
    }

    public async Task<bool> CreateTableIfNotExistAsync(string tableName)
    {
        tableName = Helper.SanitizeTableName(tableName);

        if (await DoesTableExistAsync(tableName)) return false;
        

        await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            return await connection.ExecuteAsync($"CREATE TABLE {tableName} (Name NVARCHAR(255))");
        });
        return true;
    }

    public async Task InsertPlaylistItemAsync(string playlist, string playlistItem)
    {
        playlist = Helper.SanitizeTableName(playlist);

        await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            return await connection.ExecuteAsync($"INSERT INTO {playlist}(Name) VALUES(@PlaylistItem)", new { PlaylistItem = playlistItem});
        });       
    }

    public async Task InsertPlaylistItemsAsync(string playlist, List<string> playlistItems)
    {
        playlist = Helper.SanitizeTableName(playlist);

        await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            var parameters = playlistItems.Select(item => new { PlaylistItem = item }).ToArray();

            return await connection.ExecuteAsync($"INSERT INTO {playlist}(Name) VALUES(@PlaylistItem)", parameters);
        });
    }

    public async Task InsertDeletedAsync(string playlist, List<string> playlistItems)
    {
        await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            var parameters = playlistItems.Select(item => new
            {
                Playlist = playlist,
                PlaylistItem = item,
                DeletedAt = dateTimeProvider.Now,
            }).ToArray();

            return await connection.ExecuteAsync($"INSERT INTO DELETED(Playlist, Title, DeletedAt) VALUES(@Playlist, @PlaylistItem, @DeletedAt)", parameters);
        });
    }

    public async Task<List<Deleted>> GetLatestDeletedAsync()
    {
        return await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            var result = await connection.QueryAsync<Deleted>($"SELECT * FROM DELETED WHERE DeletedAt = @DeletedAt",
                new { DeletedAt = dateTimeProvider.Now });
            return result.ToList();
        });
    }

    public async Task TruncateTableAsync(string tableName)
    {
        tableName = Helper.SanitizeTableName(tableName);

        await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            return await connection.ExecuteAsync($"TRUNCATE TABLE {tableName}");
        });
    }

    private async Task<bool> DoesTableExistAsync(string tableName)
    {
        return await DbHelper.ExecuteWithConnectionAsync(async connection =>
        {
            string query = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
            var result = await connection.QuerySingleAsync<int>(query, new { TableName = tableName });
            return result > 0;
        });
    }
}