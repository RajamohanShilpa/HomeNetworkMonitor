using HomeNetworkMonitor.Core.Interfaces;
using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Network.Monitoring;

public sealed class NetworkMonitorService
{
    private readonly INetworkDiscovery _discovery;

    private readonly Dictionary<string, DeviceState> _devices =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly NetworkTrafficCollector _trafficCollector;

    public NetworkMonitorService(
        INetworkDiscovery discovery)
    {
        _discovery = discovery;
        _trafficCollector = new NetworkTrafficCollector();
    }

    public async Task MonitorAsync(
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<NetworkDevice> discoveredDevices;

            try
            {
                discoveredDevices =
                    await _discovery.DiscoverAsync(
                        cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            UpdateDeviceState(discoveredDevices);

            PrintCurrentState();

            var interfaceName =
                discoveredDevices
                    .Select(device => device.InterfaceName)
                    .FirstOrDefault(
                        name => !string.IsNullOrWhiteSpace(name));

            if (!string.IsNullOrWhiteSpace(interfaceName))
            {
                var traffic =
                    _trafficCollector.Collect(
                        interfaceName);

                if (traffic is not null)
                {
                    PrintTrafficStatistics(traffic);
                }
            }

            try
            {
                await Task.Delay(
                    interval,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void UpdateDeviceState(
        IReadOnlyList<NetworkDevice> discoveredDevices)
    {
        var now = DateTimeOffset.UtcNow;

        var currentlyDetected =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var discoveredDevice in discoveredDevices)
        {
            var identity =
                GetDeviceIdentity(discoveredDevice);

            currentlyDetected.Add(identity);

            if (_devices.TryGetValue(
                    identity,
                    out var existingState))
            {
                existingState.Device =
                    discoveredDevice;

                existingState.LastSeen = now;
                existingState.IsOnline = true;
            }
            else
            {
                _devices[identity] =
                    new DeviceState
                    {
                        Identity = identity,
                        Device = discoveredDevice,
                        FirstSeen = now,
                        LastSeen = now,
                        IsOnline = true
                    };
            }
        }

        foreach (var state in _devices.Values)
        {
            if (!currentlyDetected.Contains(
                    state.Identity))
            {
                state.IsOnline = false;
            }
        }
    }

    private static string GetDeviceIdentity(
        NetworkDevice device)
    {
        if (!string.IsNullOrWhiteSpace(
                device.MacAddress))
        {
            return $"MAC:{NormalizeMac(device.MacAddress)}";
        }

        return $"IP:{device.IpAddress}";
    }

    private static string NormalizeMac(
        string macAddress)
    {
        return new string(
            macAddress
                .Where(char.IsLetterOrDigit)
                .ToArray())
            .ToUpperInvariant();
    }

    private void PrintCurrentState()
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{"IP Address",-16}" +
            $"{"MAC Address",-20}" +
            $"{"Device Type",-18}" +
            $"{"Status",-10}" +
            $"{"First Seen",-22}" +
            $"{"Last Seen",-22}");

        Console.WriteLine(
            new string('-', 108));

        foreach (var state in _devices.Values
                     .OrderBy(
                         state =>
                             GetIpValue(
                                 state.Device.IpAddress)))
        {
            var device = state.Device;

            var status =
                state.IsOnline
                    ? "Online"
                    : "Offline";

            Console.WriteLine(
                $"{device.IpAddress,-16}" +
                $"{device.MacAddress ?? "Unknown",-20}" +
                $"{device.DeviceType ?? "Unknown",-18}" +
                $"{status,-10}" +
                $"{state.FirstSeen.LocalDateTime,-22:yyyy-MM-dd HH:mm:ss}" +
                $"{state.LastSeen.LocalDateTime,-22:yyyy-MM-dd HH:mm:ss}");
        }
    }

    private static long GetIpValue(
        string ipAddress)
    {
        var parts =
            ipAddress.Split('.');

        if (parts.Length != 4)
            return long.MaxValue;

        return
            (long.Parse(parts[0]) << 24) |
            (long.Parse(parts[1]) << 16) |
            (long.Parse(parts[2]) << 8) |
            long.Parse(parts[3]);
    }

    private static void PrintTrafficStatistics(TrafficStatistics traffic)
    {
        Console.WriteLine();

        Console.WriteLine(
            $"Traffic Statistics - {traffic.InterfaceName}");

        Console.WriteLine(
            new string('-', 60));

        Console.WriteLine(
            $"{"Bytes Sent",-22}" +
            $"{FormatBytes(traffic.BytesSent),-18}");

        Console.WriteLine(
            $"{"Bytes Received",-22}" +
            $"{FormatBytes(traffic.BytesReceived),-18}");

        Console.WriteLine(
            $"{"Packets Sent",-22}" +
            $"{traffic.PacketsSent,-18:N0}");

        Console.WriteLine(
            $"{"Packets Received",-22}" +
            $"{traffic.PacketsReceived,-18:N0}");

        Console.WriteLine(
            $"{"Upload Rate",-22}" +
            $"{FormatRate(traffic.UploadBytesPerSecond),-18}");

        Console.WriteLine(
            $"{"Download Rate",-22}" +
            $"{FormatRate(traffic.DownloadBytesPerSecond),-18}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.00} KB";

        if (bytes < 1024L * 1024L * 1024L)
            return $"{bytes / (1024d * 1024d):0.00} MB";

        return $"{bytes / (1024d * 1024d * 1024d):0.00} GB";
    }

    private static string FormatRate(
        double bytesPerSecond)
    {
        return $"{FormatBytes((long)bytesPerSecond)}/s";
    }

}

