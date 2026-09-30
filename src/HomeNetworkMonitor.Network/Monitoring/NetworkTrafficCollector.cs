using System.Net.NetworkInformation;
using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Network.Monitoring;

public sealed class NetworkTrafficCollector
{
    private readonly Dictionary<
        string,
        TrafficSnapshot> _previousSnapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public TrafficStatistics? Collect(
        string interfaceName)
    {
        var networkInterface =
            NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(
                    network =>
                        string.Equals(
                            network.Name,
                            interfaceName,
                            StringComparison.OrdinalIgnoreCase));

        if (networkInterface is null)
            return null;

        var statistics =
            networkInterface.GetIPStatistics();

        var now =
            DateTimeOffset.UtcNow;

        var bytesSent =
            statistics.BytesSent;

        var bytesReceived =
            statistics.BytesReceived;

        var packetsSent =
            statistics.UnicastPacketsSent;

        var packetsReceived =
            statistics.UnicastPacketsReceived;

        double uploadRate = 0;
        double downloadRate = 0;

        if (_previousSnapshots.TryGetValue(
                interfaceName,
                out var previous))
        {
            var elapsed =
                (now - previous.Timestamp).TotalSeconds;

            if (elapsed > 0)
            {
                var sentDelta =
                    Math.Max(
                        0,
                        bytesSent - previous.BytesSent);

                var receivedDelta =
                    Math.Max(
                        0,
                        bytesReceived - previous.BytesReceived);

                uploadRate =
                    sentDelta / elapsed;

                downloadRate =
                    receivedDelta / elapsed;
            }
        }

        _previousSnapshots[interfaceName] =
            new TrafficSnapshot(
                bytesSent,
                bytesReceived,
                packetsSent,
                packetsReceived,
                now);

        return new TrafficStatistics
        {
            InterfaceName = interfaceName,
            BytesSent = bytesSent,
            BytesReceived = bytesReceived,
            PacketsSent = packetsSent,
            PacketsReceived = packetsReceived,
            Timestamp = now,
            UploadBytesPerSecond = uploadRate,
            DownloadBytesPerSecond = downloadRate
        };
    }

    private sealed record TrafficSnapshot(
        long BytesSent,
        long BytesReceived,
        long PacketsSent,
        long PacketsReceived,
        DateTimeOffset Timestamp);
}