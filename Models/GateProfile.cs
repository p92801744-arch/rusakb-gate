namespace RusakbGate.Models;

public sealed class GateProfile
{
    public string AppName { get; set; } = "RusakbGate";
    public string ServerHost { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string ShortId { get; set; } = "";
    public string Sni { get; set; } = "www.apple.com";
    public int SocksPort { get; set; } = 10818;
    public int HttpPort { get; set; } = 10819;
    public int XhttpPrimary { get; set; } = 443;
    public int XhttpBackup { get; set; } = 2053;

    /// <summary>Запасной публичный IP того же VPS (автовыбор через xray observatory).</summary>
    public string? ServerHostBackup { get; set; }

    public IReadOnlyList<string> AllServerHosts()
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(ServerHost)) list.Add(ServerHost.Trim());
        if (!string.IsNullOrWhiteSpace(ServerHostBackup) &&
            !list.Contains(ServerHostBackup.Trim(), StringComparer.Ordinal))
            list.Add(ServerHostBackup.Trim());
        return list;
    }
}
