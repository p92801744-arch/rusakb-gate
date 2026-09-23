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

        // Сначала DNS, потом обход программ. Иначе браузер не находит ни один сайт.
        var directRules = new List<object>
        {
            new Dictionary<string, object?> { ["action"] = "sniff" },
            new Dictionary<string, object?> { ["protocol"] = "dns", ["action"] = "hijack-dns" },
            new Dictionary<string, object?> { ["network"] = "udp", ["port"] = 443, ["action"] = "reject" }
        };
        if (UiSettings.BypassApps)
        {
            directRules.Add(new Dictionary<string, object?>
            {
                ["process_name"] = new[] { "chrome.exe", "browser.exe", "Bitrix24.exe" },
                ["outbound"] = "direct"
            });
        }
        foreach (var h in p.AllServerHosts())
            directRules.Add(new { ip_cidr = new[] { $"{h}/32" }, outbound = "direct" });
        directRules.Add(new { domain_suffix = RuDirect.Suffixes, outbound = "direct" });

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
                    }
                },
                ["rules"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["domain_suffix"] = RuDirect.Suffixes,
                        ["server"] = "dns-remote"
                    }
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
                new Dictionary<string, object?>
                {
                    ["type"] = "direct",
                    ["tag"] = "direct",
                    ["connect_timeout"] = "5s"
                }
            },
            ["route"] = new Dictionary<string, object?>
            {
                ["rules"] = directRules.ToArray(),
                ["final"] = "proxy",
                ["auto_detect_interface"] = true,
                ["default_domain_resolver"] = new Dictionary<string, object?> { ["server"] = "dns-remote" }
            }
        };

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }
}
