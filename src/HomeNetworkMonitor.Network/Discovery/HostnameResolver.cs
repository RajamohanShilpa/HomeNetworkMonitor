using System.Net;
using System.Net.Sockets;

namespace HomeNetworkMonitor.Network.Discovery;

public sealed class HostnameResolver
{
    public async Task<string?> ResolveAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hostEntry =
                await Dns.GetHostEntryAsync(
                    ipAddress,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    hostEntry.HostName))
            {
                return null;
            }

            /*
             * If DNS simply returns the IP address,
             * it isn't useful as a hostname.
             */
            if (string.Equals(
                    hostEntry.HostName,
                    ipAddress,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

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
        {
            throw;
        }
    }
}