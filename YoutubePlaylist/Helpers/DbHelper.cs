using System.Data;

namespace YoutubePlaylistManager.Cli.Helpers;
public static class DbHelper
{
    public static async Task<T> ExecuteWithConnectionAsync<T>(Func<IDbConnection, Task<T>> query)
    {
        using IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.GetConnectionString("DBConnection"));
        return await query(connection);
    }
}
