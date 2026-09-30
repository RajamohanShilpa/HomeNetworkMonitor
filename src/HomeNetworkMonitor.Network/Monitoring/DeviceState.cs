using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Network.Monitoring;

public sealed class DeviceState
{
    public required string Identity { get; init; }

    public required NetworkDevice Device { get; set; }

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastSeen { get; set; }

    public bool IsOnline { get; set; }
}