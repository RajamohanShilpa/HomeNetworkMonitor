using Microsoft.Data.Sqlite;

namespace HomeNetworkMonitor.Network.Persistence;

public sealed class SQLiteConnectionFactory
{
    private readonly string _connectionString;

    public SQLiteConnectionFactory(
        string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException(
                "Database path cannot be empty.",
                nameof(databasePath));
        }

        var connectionStringBuilder =
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

        _connectionString =
            connectionStringBuilder.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(
            _connectionString);
    }
}