using HomeNetworkMonitor.Core.Interfaces;
using HomeNetworkMonitor.Core.Models;
using HomeNetworkMonitor.Network.Controls;
using HomeNetworkMonitor.Network.Discovery;
using HomeNetworkMonitor.Network.Monitoring;
using HomeNetworkMonitor.Network.Persistence;

Console.WriteLine("========================================");
Console.WriteLine("     Home Network Monitor");
Console.WriteLine("     Network Discovery");
Console.WriteLine("========================================");
Console.WriteLine();

var databaseDirectory =
    Path.Combine(
        Directory.GetCurrentDirectory(),
        "data");

Directory.CreateDirectory(
    databaseDirectory);

var databasePath =
    Path.Combine(
        databaseDirectory,
        "HomeNetworkMonitor.db");

var connectionFactory =
    new SQLiteConnectionFactory(
        databasePath);

var databaseInitializer =
    new DatabaseInitializer(
        connectionFactory);

await databaseInitializer.InitializeAsync();

var repository =
    new NetworkRepository(
        connectionFactory);

Console.WriteLine(
    $"Database : {databasePath}");

Console.WriteLine();


INetworkDiscovery discovery =
    new NetworkDiscoveryService();

var devices =
    await discovery.DiscoverAsync();

PrintDevices(devices);

var controlService =
    new DeviceControlService();

await RunControlConsoleAsync(
    devices,
    controlService);

Console.WriteLine();
Console.WriteLine("Continuous monitoring started.");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

var monitor =
    new NetworkMonitorService(discovery, repository);

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


static void PrintDevices(
    IReadOnlyList<NetworkDevice> devices)
{
    Console.WriteLine();
    Console.WriteLine(
        $"Found {devices.Count} discovered device(s).");

    Console.WriteLine();

    Console.WriteLine(
        $"{"IP Address",-16}" +
        $"{"MAC Address",-20}" +
        $"{"Vendor",-32}" +
        $"{"Hostname",-25}" +
        $"{"DNS Hostname",-30}" +
        $"{"Device Type",-18}" +
        $"{"Open Ports",-22}" +
        $"{"Ping",-8}" +
        $"{"Latency",-10}" +
        $"{"Source",-12}");

    Console.WriteLine(
        new string('-', 197));

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
            $"{device.DnsHostName ?? "Unknown",-30}" +
            $"{device.DeviceType ?? "Unknown",-18}" +
            $"{openPorts,-22}" +
            $"{ping,-8}" +
            $"{latency,-10}" +
            $"{device.DiscoverySource,-12}");
    }
}


static async Task RunControlConsoleAsync(
    IReadOnlyList<NetworkDevice> devices,
    DeviceControlService controlService)
{
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("     Device Controls");
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  block <ip>     Block device on this PC");
    Console.WriteLine("  unblock <ip>   Remove block from this PC");
    Console.WriteLine("  status <ip>    Show block status");
    Console.WriteLine("  controls       Show blocked devices");
    Console.WriteLine("  continue       Start monitoring");
    Console.WriteLine();

    while (true)
    {
        Console.Write("Control> ");

        var commandLine =
            Console.ReadLine();

        if (string.IsNullOrWhiteSpace(commandLine))
            continue;

        var parts =
            commandLine
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

        var command =
            parts[0].ToLowerInvariant();

        if (command == "continue")
            break;

        if (command == "controls")
        {
            await PrintControlStatusAsync(
                devices,
                controlService);

            continue;
        }

        if (parts.Length < 2)
        {
            Console.WriteLine(
                "Usage: block <ip>, unblock <ip>, status <ip>");

            continue;
        }

        var ipAddress =
            parts[1];

        var device =
            devices.FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.IpAddress,
                        ipAddress,
                        StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            Console.WriteLine(
                $"Device {ipAddress} was not discovered.");

            continue;
        }

        try
        {
            switch (command)
            {
                case "block":

                    await controlService.BlockDeviceAsync(
                        device);

                    Console.WriteLine();
                    Console.WriteLine(
                        $"BLOCKED: {device.IpAddress}");

                    Console.WriteLine(
                        "Windows Firewall rules were created.");

                    Console.WriteLine();

                    break;

                case "unblock":

                    await controlService.UnblockDeviceAsync(
                        device);

                    Console.WriteLine();
                    Console.WriteLine(
                        $"UNBLOCKED: {device.IpAddress}");

                    Console.WriteLine(
                        "Windows Firewall rules were removed.");

                    Console.WriteLine();

                    break;

                case "status":

                    var blocked =
                        await controlService.IsBlockedAsync(
                            device);

                    Console.WriteLine();

                    Console.WriteLine(
                        $"Device : {device.IpAddress}");

                    Console.WriteLine(
                        $"Status : {(blocked ? "BLOCKED" : "ALLOWED")}");

                    Console.WriteLine();

                    break;

                default:

                    Console.WriteLine(
                        "Unknown command.");

                    break;
            }
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Control operation failed: {exception.Message}");

            Console.WriteLine();
        }
    }
}


static async Task PrintControlStatusAsync(
    IReadOnlyList<NetworkDevice> devices,
    DeviceControlService controlService)
{
    Console.WriteLine();
    Console.WriteLine(
        $"{"IP Address",-16}" +
        $"{"Device Type",-18}" +
        $"{"Status",-12}");

    Console.WriteLine(
        new string('-', 50));

    foreach (var device in devices)
    {
        var blocked =
            await controlService.IsBlockedAsync(
                device);

        Console.WriteLine(
            $"{device.IpAddress,-16}" +
            $"{device.DeviceType ?? "Unknown",-18}" +
            $"{(blocked ? "BLOCKED" : "ALLOWED"),-12}");
    }

    Console.WriteLine();
}