using System.Net.Sockets;

namespace HomeNetworkMonitor.Network.Discovery;

public sealed class PortScanner
{
    private static readonly int[] CommonPorts =
    [
        22,
        23,
        53,
        80,
        139,
        443,
        445,
        3389,
        8080,
        8443
    ];

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<int>> ScanAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var scanTasks = CommonPorts.Select(
            port => IsPortOpenAsync(
                ipAddress,
                port,
                cancellationToken));

        var results = await Task.WhenAll(scanTasks);

        return results
            .Where(result => result.IsOpen)
            .Select(result => result.Port)
            .OrderBy(port => port)
            .ToArray();
    }

    private static async Task<(int Port, bool IsOpen)>
        IsPortOpenAsync(
            string ipAddress,
            int port,
            CancellationToken cancellationToken)
    {
        using var client = new TcpClient();

        try
        {
            await client.ConnectAsync(
                    ipAddress,
                    port)
                .WaitAsync(
                    ConnectionTimeout,
                    cancellationToken);

            return (port, true);
        }
        catch (TimeoutException)
        {
            return (port, false);
        }
        catch (SocketException)
        {
            return (port, false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return (port, false);
        }
    }
}