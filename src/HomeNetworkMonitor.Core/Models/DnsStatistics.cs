namespace HomeNetworkMonitor.Core.Models;

public sealed class DnsStatistics
{
    public required string DnsServer { get; init; }

    public int QueriesSucceeded { get; init; }

    public int QueriesFailed { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public double SuccessRate
    {
        get
        {
            var total =
                QueriesSucceeded + QueriesFailed;

            return total == 0
                ? 0
                : (double)QueriesSucceeded / total * 100;
        }
    }
}