using HomeNetworkMonitor.Core.Models;
using Microsoft.Data.Sqlite;

namespace HomeNetworkMonitor.Network.Persistence;

public sealed class NetworkRepository
{
    private readonly SQLiteConnectionFactory
        _connectionFactory;

    public NetworkRepository(
        SQLiteConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }

    public async Task<long> UpsertDeviceAsync(
        string identity,
        NetworkDevice device,
        DateTimeOffset firstSeen,
        DateTimeOffset lastSeen,
        bool isOnline,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(
            cancellationToken);

        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var deviceId =
            await GetDeviceIdAsync(
                connection,
                transaction,
                identity,
                cancellationToken);

        if (deviceId is null)
        {
            deviceId =
                await InsertDeviceAsync(
                    connection,
                    transaction,
                    identity,
                    device,
                    firstSeen,
                    lastSeen,
                    isOnline,
                    cancellationToken);
        }
        else
        {
            await UpdateDeviceAsync(
                connection,
                transaction,
                deviceId.Value,
                device,
                lastSeen,
                isOnline,
                cancellationToken);
        }

        await InsertDeviceHistoryAsync(
            connection,
            transaction,
            deviceId.Value,
            device,
            isOnline,
            lastSeen,
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return deviceId.Value;
    }

    public async Task SaveTrafficStatisticsAsync(
        TrafficStatistics statistics,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(
            cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO TrafficStatistics
            (
                InterfaceName,
                BytesSent,
                BytesReceived,
                PacketsSent,
                PacketsReceived,
                UploadBytesPerSecond,
                DownloadBytesPerSecond,
                Timestamp
            )
            VALUES
            (
                $interfaceName,
                $bytesSent,
                $bytesReceived,
                $packetsSent,
                $packetsReceived,
                $uploadRate,
                $downloadRate,
                $timestamp
            );
            """;

        command.Parameters.AddWithValue(
            "$interfaceName",
            statistics.InterfaceName);

        command.Parameters.AddWithValue(
            "$bytesSent",
            statistics.BytesSent);

        command.Parameters.AddWithValue(
            "$bytesReceived",
            statistics.BytesReceived);

        command.Parameters.AddWithValue(
            "$packetsSent",
            statistics.PacketsSent);

        command.Parameters.AddWithValue(
            "$packetsReceived",
            statistics.PacketsReceived);

        command.Parameters.AddWithValue(
            "$uploadRate",
            statistics.UploadBytesPerSecond);

        command.Parameters.AddWithValue(
            "$downloadRate",
            statistics.DownloadBytesPerSecond);

        command.Parameters.AddWithValue(
            "$timestamp",
            statistics.Timestamp.ToString("O"));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task<long?> GetDeviceIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string identity,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction =
            transaction;

        command.CommandText =
            """
            SELECT Id
            FROM Devices
            WHERE Identity = $identity;
            """;

        command.Parameters.AddWithValue(
            "$identity",
            identity);

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (result is null ||
            result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt64(result);
    }

    private static async Task<long> InsertDeviceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string identity,
        NetworkDevice device,
        DateTimeOffset firstSeen,
        DateTimeOffset lastSeen,
        bool isOnline,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction =
            transaction;

        command.CommandText =
            """
            INSERT INTO Devices
            (
                Identity,
                IpAddress,
                MacAddress,
                Vendor,
                HostName,
                DnsHostName,
                DeviceType,
                InterfaceName,
                DiscoverySource,
                FirstSeen,
                LastSeen,
                IsOnline
            )
            VALUES
            (
                $identity,
                $ipAddress,
                $macAddress,
                $vendor,
                $hostName,
                $dnsHostName,
                $deviceType,
                $interfaceName,
                $discoverySource,
                $firstSeen,
                $lastSeen,
                $isOnline
            );

            SELECT last_insert_rowid();
            """;

        AddDeviceParameters(
            command,
            identity,
            device,
            firstSeen,
            lastSeen,
            isOnline);

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt64(result);
    }

    private static async Task UpdateDeviceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long deviceId,
        NetworkDevice device,
        DateTimeOffset lastSeen,
        bool isOnline,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction =
            transaction;

        command.CommandText =
            """
            UPDATE Devices
            SET
                IpAddress = $ipAddress,
                MacAddress = $macAddress,
                Vendor = $vendor,
                HostName = $hostName,
                DnsHostName = $dnsHostName,
                DeviceType = $deviceType,
                InterfaceName = $interfaceName,
                DiscoverySource = $discoverySource,
                LastSeen = $lastSeen,
                IsOnline = $isOnline
            WHERE Id = $deviceId;
            """;

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        command.Parameters.AddWithValue(
            "$ipAddress",
            device.IpAddress);

        command.Parameters.AddWithValue(
            "$macAddress",
            (object?)device.MacAddress ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$vendor",
            (object?)device.Vendor ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$hostName",
            (object?)device.HostName ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$dnsHostName",
            (object?)device.DnsHostName ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$deviceType",
            (object?)device.DeviceType ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$interfaceName",
            (object?)device.InterfaceName ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$discoverySource",
            (object?)device.DiscoverySource ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$lastSeen",
            lastSeen.ToString("O"));

        command.Parameters.AddWithValue(
            "$isOnline",
            isOnline ? 1 : 0);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task InsertDeviceHistoryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long deviceId,
        NetworkDevice device,
        bool isOnline,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction =
            transaction;

        command.CommandText =
            """
            INSERT INTO DeviceHistory
            (
                DeviceId,
                IpAddress,
                IsOnline,
                Timestamp
            )
            VALUES
            (
                $deviceId,
                $ipAddress,
                $isOnline,
                $timestamp
            );
            """;

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        command.Parameters.AddWithValue(
            "$ipAddress",
            device.IpAddress);

        command.Parameters.AddWithValue(
            "$isOnline",
            isOnline ? 1 : 0);

        command.Parameters.AddWithValue(
            "$timestamp",
            timestamp.ToString("O"));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static void AddDeviceParameters(
        SqliteCommand command,
        string identity,
        NetworkDevice device,
        DateTimeOffset firstSeen,
        DateTimeOffset lastSeen,
        bool isOnline)
    {
        command.Parameters.AddWithValue(
            "$identity",
            identity);

        command.Parameters.AddWithValue(
            "$ipAddress",
            device.IpAddress);

        command.Parameters.AddWithValue(
            "$macAddress",
            (object?)device.MacAddress ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$vendor",
            (object?)device.Vendor ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$hostName",
            (object?)device.HostName ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$dnsHostName",
            (object?)device.DnsHostName ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$deviceType",
            (object?)device.DeviceType ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$interfaceName",
            (object?)device.InterfaceName ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$discoverySource",
            (object?)device.DiscoverySource ??
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$firstSeen",
            firstSeen.ToString("O"));

        command.Parameters.AddWithValue(
            "$lastSeen",
            lastSeen.ToString("O"));

        command.Parameters.AddWithValue(
            "$isOnline",
            isOnline ? 1 : 0);
    }
}