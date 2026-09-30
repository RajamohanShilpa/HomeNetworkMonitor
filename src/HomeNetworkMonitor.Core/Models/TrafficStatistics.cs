namespace HomeNetworkMonitor.Core.Models;

public sealed class TrafficStatistics
{
    public required string InterfaceName { get; init; }

    public long BytesSent { get; init; }

    public long BytesReceived { get; init; }

    public long PacketsSent { get; init; }

    public long PacketsReceived { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public double UploadBytesPerSecond { get; init; }

    public double DownloadBytesPerSecond { get; init; }
}