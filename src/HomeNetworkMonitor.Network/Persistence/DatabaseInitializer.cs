using Microsoft.Data.Sqlite;

namespace HomeNetworkMonitor.Network.Persistence;

public sealed class DatabaseInitializer
{
    private readonly SQLiteConnectionFactory
        _connectionFactory;

    public DatabaseInitializer(
        SQLiteConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(
            cancellationToken);

        await EnableForeignKeysAsync(
            connection,
            cancellationToken);

        await CreateTablesAsync(
            connection,
            cancellationToken);
    }

    private static async Task EnableForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "PRAGMA foreign_keys = ON;";

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task CreateTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Devices
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                Identity TEXT NOT NULL UNIQUE,

                IpAddress TEXT NOT NULL,

                MacAddress TEXT NULL,

                Vendor TEXT NULL,

                HostName TEXT NULL,

                DnsHostName TEXT NULL,

                DeviceType TEXT NULL,

                InterfaceName TEXT NULL,

                DiscoverySource TEXT NULL,

                FirstSeen TEXT NOT NULL,

                LastSeen TEXT NOT NULL,

                IsOnline INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Devices_IpAddress
                ON Devices(IpAddress);

            CREATE INDEX IF NOT EXISTS IX_Devices_MacAddress
                ON Devices(MacAddress);


            CREATE TABLE IF NOT EXISTS DeviceHistory
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                DeviceId INTEGER NOT NULL,

                IpAddress TEXT NOT NULL,

                IsOnline INTEGER NOT NULL,

                Timestamp TEXT NOT NULL,

                FOREIGN KEY(DeviceId)
                    REFERENCES Devices(Id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS
                IX_DeviceHistory_DeviceId
                ON DeviceHistory(DeviceId);

            CREATE INDEX IF NOT EXISTS
                IX_DeviceHistory_Timestamp
                ON DeviceHistory(Timestamp);


            CREATE TABLE IF NOT EXISTS TrafficStatistics
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                InterfaceName TEXT NOT NULL,

                BytesSent INTEGER NOT NULL,

                BytesReceived INTEGER NOT NULL,

                PacketsSent INTEGER NOT NULL,

                PacketsReceived INTEGER NOT NULL,

                UploadBytesPerSecond REAL NOT NULL,

                DownloadBytesPerSecond REAL NOT NULL,

                Timestamp TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS
                IX_TrafficStatistics_InterfaceName
                ON TrafficStatistics(InterfaceName);

            CREATE INDEX IF NOT EXISTS
                IX_TrafficStatistics_Timestamp
                ON TrafficStatistics(Timestamp);
            """;

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}