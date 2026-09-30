using HomeNetworkMonitor.Core.Interfaces;
using HomeNetworkMonitor.Network.Discovery;
using HomeNetworkMonitor.Network.Monitoring;

Console.WriteLine("========================================");
Console.WriteLine("     Home Network Monitor");
Console.WriteLine("     Network Discovery");
Console.WriteLine("========================================");
Console.WriteLine();

INetworkDiscovery discovery =
    new NetworkDiscoveryService();

var devices =
    await discovery.DiscoverAsync();

Console.WriteLine();
Console.WriteLine(
    $"Found {devices.Count} discovered device(s).");

Console.WriteLine();

Console.WriteLine(
    $"{"IP Address",-16}" +
    $"{"MAC Address",-20}" +
    $"{"Vendor",-32}" +
    $"{"Hostname",-25}" +
    $"{"Device Type",-18}" +
    $"{"Open Ports",-22}" +
    $"{"Ping",-8}" +
    $"{"Latency",-10}" +
    $"{"Source",-12}");

Console.WriteLine(new string('-', 167));

foreach (var device in devices)
{
    var latency =
        device.Latency.HasValue
            ? $"{device.Latency.Value.TotalMilliseconds:0} ms"
            : "-";

    var ping =
        device.PingSucceeded
            ? "Yes"
            : "No";

    var openPorts =
        device.OpenPorts.Count > 0
            ? string.Join(", ", device.OpenPorts)
            : "-";

    Console.WriteLine(
        $"{device.IpAddress,-16}" +
        $"{device.MacAddress ?? "Unknown",-20}" +
        $"{device.Vendor ?? "Unknown",-32}" +
        $"{device.HostName ?? "Unknown",-25}" +
        $"{device.DeviceType ?? "Unknown",-18}" +
        $"{openPorts,-22}" +
        $"{ping,-8}" +
        $"{latency,-10}" +
        $"{device.DiscoverySource,-12}");
}

Console.WriteLine();


Console.WriteLine();
Console.WriteLine(
    "Continuous monitoring started.");

Console.WriteLine(
    "Press Ctrl+C to stop.");

Console.WriteLine();

var monitor =
    new NetworkMonitorService(discovery);

using var cancellationTokenSource =
    new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

await monitor.MonitorAsync(
    TimeSpan.FromSeconds(10),
    cancellationTokenSource.Token);