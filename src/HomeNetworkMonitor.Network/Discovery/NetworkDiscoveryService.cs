using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HomeNetworkMonitor.Core.Interfaces;
using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Network.Discovery;

public sealed class NetworkDiscoveryService : INetworkDiscovery
{
    private readonly ArpTableReader _arpTableReader;
    private readonly HostnameResolver _hostnameResolver;
    private readonly MacVendorResolver _macVendorResolver;
    private readonly DeviceTypeDetector _deviceTypeDetector;
    private readonly PortScanner _portScanner;
    
    public NetworkDiscoveryService()
    {
        _arpTableReader = new ArpTableReader();
        _hostnameResolver = new HostnameResolver();
        _macVendorResolver = new MacVendorResolver();
        _deviceTypeDetector = new DeviceTypeDetector();
        _portScanner = new PortScanner();
    }

    public async Task<IReadOnlyList<NetworkDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var network = GetLocalNetwork();

        Console.WriteLine($"Interface : {network.InterfaceName}");
        Console.WriteLine($"Local IP  : {network.LocalAddress}");
        Console.WriteLine(
            $"Subnet    : {network.NetworkAddress}/{network.PrefixLength}");
        Console.WriteLine();

        var addresses = EnumerateHosts(
            network.NetworkAddress,
            network.PrefixLength);

        /*
         * Step 1:
         * Probe every host using ICMP.
         *
         * Even when a device doesn't answer ping,
         * the network stack may still attempt ARP resolution.
         */
        var tasks = addresses.Select(
            ip => ProbeAsync(
                ip,
                network.InterfaceName,
                cancellationToken));

        var pingResults = await Task.WhenAll(tasks);

        /*
         * Step 2:
         * Read the ARP table after the scan.
         */
        var arpTable = await _arpTableReader.ReadAsync(
            cancellationToken);

        /*
         * Step 3:
         * Combine ICMP and ARP information.
         */
        var devices =
            new Dictionary<string, NetworkDevice>(
                StringComparer.OrdinalIgnoreCase);

        /*
         * First add all ICMP responders.
         */
        foreach (var result in pingResults)
        {
            if (result is null)
                continue;

            arpTable.TryGetValue(
                result.IpAddress,
                out var macAddress);

            var source =
                macAddress is not null
                    ? "ICMP + ARP"
                    : "ICMP";

            devices[result.IpAddress] =
                new NetworkDevice
                {
                    IpAddress = result.IpAddress,
                    MacAddress = macAddress,
                    HostName = null,
                    PingSucceeded = true,
                    Latency = result.Latency,
                    InterfaceName = network.InterfaceName,
                    DiscoverySource = source,
                    DiscoveredAt = result.DiscoveredAt,
                    Vendor = _macVendorResolver.Resolve(macAddress)
                };
        
        
        }

        /*
         * Step 4:
         * Add ARP entries that were not found by ICMP.
         *
         * These are particularly interesting because they may
         * represent devices that do not respond to ping.
         */
        foreach (var arpEntry in arpTable)
        {
            var ip = arpEntry.Key;
            var mac = arpEntry.Value;

            var arpAddress = IPAddress.Parse(ip);

            /*
             * Only include addresses belonging to our
             * current local network.
             */
            if (!IsAddressInNetwork(
                    arpAddress,
                    network.NetworkAddress,
                    network.PrefixLength))
            {
                continue;
            }

            if (IsNetworkOrBroadcastAddress(
                    arpAddress,
                    network.NetworkAddress,
                    network.PrefixLength))
            {
                continue;
            }

            if (devices.ContainsKey(ip))
                continue;

            devices[ip] =
                new NetworkDevice
                {
                    IpAddress = ip,
                    MacAddress = mac,
                    Vendor = _macVendorResolver.Resolve(mac),
                    HostName = null,
                    PingSucceeded = false,
                    Latency = null,
                    InterfaceName = network.InterfaceName,
                    DiscoverySource = "ARP",
                    DiscoveredAt = DateTimeOffset.UtcNow
                };
        }

        var discoveredDevices = devices.Values
        .OrderBy(device => GetIpValue(device.IpAddress))
        .ToList();

        var hostnameTasks = discoveredDevices.Select(
            async device =>
            {
                var hostname =
                    await _hostnameResolver.ResolveAsync(
                        device.IpAddress,
                        cancellationToken);

                var openPorts =
                    await _portScanner.ScanAsync(
                        device.IpAddress,
                        cancellationToken);

                var deviceWithFingerprint = new NetworkDevice
                {
                    IpAddress = device.IpAddress,
                    MacAddress = device.MacAddress,
                    Vendor = device.Vendor,
                    HostName = hostname,
                    PingSucceeded = device.PingSucceeded,
                    Latency = device.Latency,
                    InterfaceName = device.InterfaceName,
                    DiscoverySource = device.DiscoverySource,
                    DiscoveredAt = device.DiscoveredAt,
                    OpenPorts = openPorts
                };

                var deviceType =
                    _deviceTypeDetector.Detect(
                        deviceWithFingerprint);

                return new NetworkDevice
                {
                    IpAddress = deviceWithFingerprint.IpAddress,
                    MacAddress = deviceWithFingerprint.MacAddress,
                    Vendor = deviceWithFingerprint.Vendor,
                    HostName = deviceWithFingerprint.HostName,
                    DeviceType = deviceType,
                    PingSucceeded = deviceWithFingerprint.PingSucceeded,
                    Latency = deviceWithFingerprint.Latency,
                    InterfaceName = deviceWithFingerprint.InterfaceName,
                    DiscoverySource = deviceWithFingerprint.DiscoverySource,
                    DiscoveredAt = deviceWithFingerprint.DiscoveredAt,
                    OpenPorts = deviceWithFingerprint.OpenPorts
                };
            });
            
        return await Task.WhenAll(hostnameTasks);
    }

    private static async Task<NetworkDevice?> ProbeAsync(IPAddress address, string interfaceName, CancellationToken cancellationToken)
    {
        using var ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(
                address,
                500);

            cancellationToken.ThrowIfCancellationRequested();

            if (reply.Status != IPStatus.Success)
                return null;

            return new NetworkDevice
            {
                IpAddress = address.ToString(),
                MacAddress = null,
                HostName = null,
                PingSucceeded = true,
                Latency = TimeSpan.FromMilliseconds(
                    reply.RoundtripTime),
                InterfaceName = interfaceName,
                DiscoverySource = "ICMP",
                DiscoveredAt = DateTimeOffset.UtcNow
            };
        }
        catch (PingException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static NetworkInfo GetLocalNetwork()
    {
        foreach (var networkInterface in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus !=
                OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType ==
                NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var properties =
                networkInterface.GetIPProperties();

            var ipv4 = properties.UnicastAddresses
                .FirstOrDefault(
                    address =>
                        address.Address.AddressFamily ==
                        AddressFamily.InterNetwork &&
                        address.IPv4Mask is not null);

            if (ipv4 is null)
                continue;

            var localAddress = ipv4.Address;
            var mask = ipv4.IPv4Mask;

            var networkAddress =
                GetNetworkAddress(
                    localAddress,
                    mask);

            var prefixLength =
                GetPrefixLength(mask);

            return new NetworkInfo(
                networkInterface.Name,
                localAddress,
                networkAddress,
                prefixLength);
        }

        throw new InvalidOperationException(
            "No active IPv4 network interface was found.");
    }

    private static IPAddress GetNetworkAddress(IPAddress address, IPAddress mask)
    {
        var addressBytes =
            address.GetAddressBytes();

        var maskBytes =
            mask.GetAddressBytes();

        var networkBytes =
            new byte[4];

        for (var i = 0; i < 4; i++)
        {
            networkBytes[i] =
                (byte)(addressBytes[i] &
                       maskBytes[i]);
        }

        return new IPAddress(networkBytes);
    }

    private static int GetPrefixLength(IPAddress mask)
    {
        var bytes = mask.GetAddressBytes();

        return bytes.Sum(
            b => Convert.ToString(
                    b,
                    2)
                .Count(c => c == '1'));
    }

    private static IEnumerable<IPAddress> EnumerateHosts(IPAddress networkAddress, int prefixLength)
    {
        var networkBytes =
            networkAddress.GetAddressBytes();

        var network =
            ((uint)networkBytes[0] << 24) |
            ((uint)networkBytes[1] << 16) |
            ((uint)networkBytes[2] << 8) |
            networkBytes[3];

        var hostBits =
            32 - prefixLength;

        if (hostBits <= 1)
            yield break;

        var hostCount =
            1u << hostBits;

        /*
         * Safety limit for the first version.
         */
        if (hostCount > 4096)
        {
            throw new InvalidOperationException(
                $"Network /{prefixLength} is too large " +
                "for the initial scanner.");
        }

        for (uint i = 1;
             i < hostCount - 1;
             i++)
        {
            var value =
                network + i;

            yield return new IPAddress(
                new[]
                {
                    (byte)(value >> 24),
                    (byte)(value >> 16),
                    (byte)(value >> 8),
                    (byte)value
                });
        }
    }

    private static bool IsAddressInNetwork(IPAddress address, IPAddress networkAddress, int prefixLength)
    {
        var addressBytes =
            address.GetAddressBytes();

        var networkBytes =
            networkAddress.GetAddressBytes();

        var fullBytes =
            prefixLength / 8;

        var remainingBits =
            prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
                return false;
        }

        if (remainingBits == 0)
            return true;

        var mask =
            (byte)(0xFF << (8 - remainingBits));

        return
            (addressBytes[fullBytes] & mask) ==
            (networkBytes[fullBytes] & mask);
    }

    private static bool IsNetworkOrBroadcastAddress(IPAddress address, IPAddress networkAddress, int prefixLength)
    {
        var addressBytes =
            address.GetAddressBytes();

        var networkBytes =
            networkAddress.GetAddressBytes();

        var addressValue =
            ((uint)addressBytes[0] << 24) |
            ((uint)addressBytes[1] << 16) |
            ((uint)addressBytes[2] << 8) |
            addressBytes[3];

        var networkValue =
            ((uint)networkBytes[0] << 24) |
            ((uint)networkBytes[1] << 16) |
            ((uint)networkBytes[2] << 8) |
            networkBytes[3];

        var hostBits =
            32 - prefixLength;

        var hostMask =
            (1u << hostBits) - 1;

        var broadcastValue =
            networkValue | hostMask;

        return
            addressValue == networkValue ||
            addressValue == broadcastValue;
    }


    private static uint GetIpValue(string ip)
    {
        var bytes =
            IPAddress.Parse(ip)
                .GetAddressBytes();

        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               bytes[3];
    }

    private sealed record NetworkInfo(string InterfaceName, IPAddress LocalAddress, IPAddress NetworkAddress, int PrefixLength);
}