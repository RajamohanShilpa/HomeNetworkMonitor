using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Network.Discovery;

public sealed class DeviceTypeDetector
{
    public string Detect(NetworkDevice device)
    {
        if (IsLocalComputer(device))
            return "Computer";

        if (IsRouter(device))
            return "Router";

        if (IsTpLink(device))
            return "Network Device";

        return "Unknown";
    }

    private static bool IsLocalComputer(NetworkDevice device)
    {
        return string.Equals(
                   device.IpAddress,
                   device.InterfaceName,
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   device.HostName,
                   Environment.MachineName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRouter(NetworkDevice device)
    {
        return device.IpAddress.EndsWith(".1");
    }

    private static bool IsTpLink(NetworkDevice device)
    {
        return device.Vendor?.Contains(
                   "TP-LINK",
                   StringComparison.OrdinalIgnoreCase)
               == true;
    }
}