namespace HomeNetworkMonitor.Core.Models;

public sealed class NetworkDevice
{
    public required string IpAddress { get; init; }

    public string? MacAddress { get; init; }

    public string? Vendor { get; init; }

    public string? HostName { get; init; }

    public string? DnsHostName { get; init; }

    public string? DeviceType { get; init; }

    /// <summary>
    /// Indicates whether the device responded to ICMP ping.
    /// </summary>
    public bool PingSucceeded { get; init; }

    public TimeSpan? Latency { get; init; }

    public string? InterfaceName { get; init; }

    /// <summary>
    /// ICMP, ARP, or ICMP + ARP.
    /// </summary>
    public required string DiscoverySource { get; init; }

    public DateTimeOffset DiscoveredAt { get; init; }

    public IReadOnlyList<int> OpenPorts { get; init; } = Array.Empty<int>();

    public DateTimeOffset FirstSeen { get; init; }

    public DateTimeOffset LastSeen { get; init; }

    public bool IsOnline { get; init; }
}