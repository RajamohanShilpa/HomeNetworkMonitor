using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Network.Controls;

public sealed class DeviceControlService
{
    private readonly WindowsFirewallDeviceController
        _firewallController;

    private readonly Dictionary<string, bool> _blockedDevices =
        new(StringComparer.OrdinalIgnoreCase);

    public DeviceControlService()
    {
        _firewallController =
            new WindowsFirewallDeviceController();
    }

    public async Task BlockDeviceAsync(
        NetworkDevice device,
        CancellationToken cancellationToken = default)
    {
        await _firewallController.BlockAsync(
            device.IpAddress,
            cancellationToken);

        _blockedDevices[device.IpAddress] = true;
    }

    public async Task UnblockDeviceAsync(
        NetworkDevice device,
        CancellationToken cancellationToken = default)
    {
        await _firewallController.UnblockAsync(
            device.IpAddress,
            cancellationToken);

        _blockedDevices.Remove(
            device.IpAddress);
    }

    public async Task<bool> IsBlockedAsync(
        NetworkDevice device,
        CancellationToken cancellationToken = default)
    {
        if (_blockedDevices.TryGetValue(
                device.IpAddress,
                out var blocked))
        {
            return blocked;
        }

        var firewallBlocked =
            await _firewallController.IsBlockedAsync(
                device.IpAddress,
                cancellationToken);

        if (firewallBlocked)
        {
            _blockedDevices[device.IpAddress] = true;
        }

        return firewallBlocked;
    }
}