namespace HomeNetworkMonitor.Network.Discovery;

public sealed class MacVendorResolver
{
    private static readonly IReadOnlyDictionary<string, string> Vendors =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["30-4F-75"] = "Zhone Technologies, Inc.",
            ["D8-07-B6"] = "TP-LINK TECHNOLOGIES CO., LTD.",
            ["F4-14-BF"] = "LG Innotek"
        };

    public string? Resolve(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return null;

        var normalized = NormalizeMacAddress(macAddress);

        if (normalized is null)
            return null;

        var oui = normalized[..8];

        return Vendors.TryGetValue(oui, out var vendor)
            ? vendor
            : null;
    }

    private static string? NormalizeMacAddress(string macAddress)
    {
        var hex = new string(
            macAddress
                .Where(Uri.IsHexDigit)
                .ToArray());

        if (hex.Length != 12)
            return null;

        return string.Join(
            "-",
            Enumerable.Range(0, 6)
                .Select(index =>
                    hex.Substring(index * 2, 2)))
            .ToUpperInvariant();
    }
}