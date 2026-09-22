using System.Text.Json;
using RusakbGate.Models;

namespace RusakbGate.Services;

public static class SingBoxConfigBuilder
{
    public static string Build(GateProfile p)
    {
        var exclude = new List<string>
        {
            "192.168.0.0/16", "10.0.0.0/8", "172.16.0.0/12",
            "127.0.0.0/8", "169.254.0.0/16", "224.0.0.0/4"
        };
        foreach (var h in p.AllServerHosts())
            exclude.Add($"{h}/32");

        var directRules = p.AllServerHosts()
            .Select(h => new { ip_cidr = new[] { $"{h}/32" }, outbound = "direct" })
            .Cast<object>()
            .ToArray();

        var obj = new Dictionary<string, object?>
        {
            ["log"] = new { level = "info", timestamp = true },
            ["dns"] = new Dictionary<string, object?>
            {
                ["servers"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "udp",
                        ["tag"] = "dns-remote",
                        ["server"] = "1.1.1.1",
                        ["detour"] = "proxy"
                    },
                    new Dictionary<string, object?> { ["type"] = "local", ["tag"] = "dns-local" }
                },
                ["final"] = "dns-remote",
                ["strategy"] = "ipv4_only"
            },
            ["inbounds"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "tun",
                    ["tag"] = "tun-in",
                    ["interface_name"] = "RusakbGate",
                    ["address"] = new[] { "10.0.85.1/24" },
                    ["mtu"] = 9000,
                    ["stack"] = "system",
                    ["auto_route"] = true,
                    ["strict_route"] = true,
                    ["route_exclude_address"] = exclude
                }
            },
            ["outbounds"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "socks",
                    ["tag"] = "proxy",
                    ["server"] = "127.0.0.1",
                    ["server_port"] = p.SocksPort
                },
                new Dictionary<string, object?> { ["type"] = "direct", ["tag"] = "direct" }
            },
            ["route"] = new Dictionary<string, object?>
            {
                ["rules"] = directRules,
                ["final"] = "proxy",
                ["default_domain_resolver"] = new Dictionary<string, object?> { ["server"] = "dns-local" }
            }
        };

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }
}
