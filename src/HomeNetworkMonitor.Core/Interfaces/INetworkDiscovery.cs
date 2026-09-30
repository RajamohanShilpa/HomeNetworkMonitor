using HomeNetworkMonitor.Core.Models;

namespace HomeNetworkMonitor.Core.Interfaces;

public interface INetworkDiscovery
{
    Task<IReadOnlyList<NetworkDevice>> DiscoverAsync(
        CancellationToken cancellationToken = default);
}