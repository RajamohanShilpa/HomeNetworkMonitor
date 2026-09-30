namespace HomeNetworkMonitor.Core.Interfaces;

public interface INetworkDeviceController
{
    Task BlockAsync(
        string ipAddress,
        CancellationToken cancellationToken = default);

    Task UnblockAsync(
        string ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(
        string ipAddress,
        CancellationToken cancellationToken = default);
}