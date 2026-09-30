using System.Diagnostics;
using System.Net;

namespace HomeNetworkMonitor.Network.Controls;

public sealed class WindowsFirewallDeviceController
{
    private const string RulePrefix =
        "HomeNetworkMonitor - Block - ";

    public async Task BlockAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        ValidateIpAddress(ipAddress);

        var ruleName =
            GetRuleName(ipAddress);

        await AddRuleAsync(
            ruleName,
            "out",
            ipAddress,
            cancellationToken);

        await AddRuleAsync(
            ruleName,
            "in",
            ipAddress,
            cancellationToken);
    }

    public async Task UnblockAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        ValidateIpAddress(ipAddress);

        var ruleName =
            GetRuleName(ipAddress);

        await DeleteRuleAsync(
            ruleName,
            cancellationToken);
    }

    public async Task<bool> IsBlockedAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        ValidateIpAddress(ipAddress);

        var ruleName =
            GetRuleName(ipAddress);

        var result =
            await ExecuteNetshAsync(
                $"advfirewall firewall show rule name=\"{ruleName}\"",
                cancellationToken);

        return result.ExitCode == 0 &&
               result.StandardOutput.Contains(
                   ruleName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AddRuleAsync(
        string ruleName,
        string direction,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var arguments =
            $"advfirewall firewall add rule " +
            $"name=\"{ruleName}\" " +
            $"dir={direction} " +
            $"action=block " +
            $"remoteip={ipAddress} " +
            $"profile=any";

        var result =
            await ExecuteNetshAsync(
                arguments,
                cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to create Windows Firewall rule. " +
                $"Direction: {direction}. " +
                $"Error: {result.StandardError}");
        }
    }

    private static async Task DeleteRuleAsync(
        string ruleName,
        CancellationToken cancellationToken)
    {
        var arguments =
            $"advfirewall firewall delete rule " +
            $"name=\"{ruleName}\"";

        var result =
            await ExecuteNetshAsync(
                arguments,
                cancellationToken);

        if (result.ExitCode != 0 &&
            !result.StandardOutput.Contains(
                "No rules match",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unable to remove Windows Firewall rule. " +
                $"Error: {result.StandardError}");
        }
    }

    private static async Task<ProcessResult>
        ExecuteNetshAsync(
            string arguments,
            CancellationToken cancellationToken)
    {
        using var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
            };

        process.Start();

        var standardOutputTask =
            process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        var standardErrorTask =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        var standardOutput =
            await standardOutputTask;

        var standardError =
            await standardErrorTask;

        return new ProcessResult(
            process.ExitCode,
            standardOutput,
            standardError);
    }

    private static string GetRuleName(
        string ipAddress)
    {
        return $"{RulePrefix}{ipAddress}";
    }

    private static void ValidateIpAddress(
        string ipAddress)
    {
        if (!IPAddress.TryParse(
                ipAddress,
                out var address))
        {
            throw new ArgumentException(
                $"Invalid IP address: {ipAddress}",
                nameof(ipAddress));
        }

        if (address.AddressFamily !=
            System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                "Only IPv4 addresses are currently supported.",
                nameof(ipAddress));
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}