using System.Text.Json;
using RusakbGate.Models;

namespace RusakbGate.Services;

public static class XrayConfigBuilder
{
    /// <summary>Стабильная схема: основной IP:443, запасной IP — второй outbound без observatory.</summary>
    public static string Build(GateProfile p)
    {
        var outbounds = new List<object> { new { protocol = "freedom", tag = "direct" } };
        outbounds.Add(BuildXhttp(p, p.ServerHost, p.XhttpPrimary, "proxy443"));

        if (!string.IsNullOrWhiteSpace(p.ServerHostBackup))
            outbounds.Add(BuildXhttp(p, p.ServerHostBackup.Trim(), p.XhttpPrimary, "proxy443-backup"));

        outbounds.Add(BuildXhttp(p, p.ServerHost, p.XhttpBackup, "proxy2053"));

        var rules = new List<object>
        {
            new { type = "field", inboundTag = new[] { "socks-in", "http-in" }, outboundTag = "proxy443" }
        };

        var root = new
        {
            log = new { loglevel = "warning" },
            inbounds = new object[]
            {
                new
                {
                    tag = "socks-in",
                    listen = "127.0.0.1",
                    port = p.SocksPort,
                    protocol = "socks",
                    settings = new { udp = true }
                },
                new
                {
                    tag = "http-in",
                    listen = "127.0.0.1",
                    port = p.HttpPort,
                    protocol = "http"
                }
            },
            outbounds,
            routing = new { domainStrategy = "AsIs", rules }
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object BuildXhttp(GateProfile p, string host, int port, string tag) => new
    {
        protocol = "vless",
        tag,
        settings = new
        {
            vnext = new[]
            {
                new
                {
                    address = host,
                    port,
                    users = new[] { new { id = p.Uuid, encryption = "none" } }
                }
            }
        },
        streamSettings = new
        {
            network = "xhttp",
            security = "reality",
            xhttpSettings = new
            {
                path = "/",
                mode = "stream-one",
                xPaddingBytes = "100-1000",
                xmux = new
                {
                    maxConcurrency = 48,
                    cMaxReuseTimes = 0,
                    hMaxRequestTimes = 0,
                    hMaxReusableSecs = 0,
                    hKeepAlivePeriod = 20
                }
            },
            realitySettings = new
            {
                serverName = p.Sni,
                fingerprint = "firefox",
                publicKey = p.PublicKey,
                shortId = p.ShortId,
                spiderX = "/"
            }
        }
    };
}
