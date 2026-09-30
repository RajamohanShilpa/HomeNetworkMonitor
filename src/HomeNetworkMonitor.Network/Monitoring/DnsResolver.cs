using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HomeNetworkMonitor.Network.Monitoring;

public sealed class DnsResolver
{
    public IReadOnlyList<string> GetDnsServers()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(network =>
                network.OperationalStatus ==
                OperationalStatus.Up)
            .SelectMany(network =>
                network.GetIPProperties()
                    .DnsAddresses)
            .Where(address =>
                address.AddressFamily ==
                AddressFamily.InterNetwork)
            .Select(address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string?> ResolveAsync(
        string hostName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var addresses =
                await Dns.GetHostAddressesAsync(
                    hostName,
                    cancellationToken);

            return addresses
                .FirstOrDefault()?
                .ToString();
        }
        catch (SocketException)
        {
            return null;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<string?> ReverseResolveAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hostEntry =
                await Dns.GetHostEntryAsync(
                    ipAddress,
                    cancellationToken);

            return hostEntry.HostName;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}