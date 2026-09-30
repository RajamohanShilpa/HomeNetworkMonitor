using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HomeNetworkMonitor.Network.Discovery;

public sealed class ArpTableReader
{
    private static readonly Regex ArpEntryRegex = new(
        @"^\s*(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+"
        + @"(?<mac>[0-9a-fA-F]{2}(?:-[0-9a-fA-F]{2}){5})\s+"
        + @"(?<type>dynamic|static)\s*$",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase);

    public async Task<IReadOnlyDictionary<string, string>>
        ReadAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "arp",
            Arguments = "-a",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var output = await process.StandardOutput
            .ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split(
                     Environment.NewLine,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var match = ArpEntryRegex.Match(line);

            if (!match.Success)
                continue;

            var ip = match.Groups["ip"].Value;
            var mac = match.Groups["mac"].Value;

            result[ip] = mac;
        }

        return result;
    }
}