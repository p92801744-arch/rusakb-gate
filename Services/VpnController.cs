using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using RusakbGate.Models;

namespace RusakbGate.Services;

public sealed class VpnController
{
    private Process? _xray;
    private Process? _singbox;
    private readonly List<string> _bypassHosts = new();

    /// <summary>Ядро VPN само завершилось — UI должен снять «подключено».</summary>
    public event Action<string>? CoreDied;

    public bool IsRunning =>
        _xray is { HasExited: false } && _singbox is { HasExited: false };

    public async Task StartAsync(GateProfile profile)
    {
        await StopAsync();

        if (VpnEnvironment.IsHappActive())
            throw new InvalidOperationException(
                "Сначала выключи Happ (happ-tun). Два VPN сразу ломают интернет.");

        var bin = ProfileStore.BinDir;
        var xrayExe = Path.Combine(bin, "xray.exe");
        var sbExe = Path.Combine(bin, "sing-box.exe");
        var wintun = Path.Combine(bin, "wintun.dll");
        if (!File.Exists(xrayExe)) throw new FileNotFoundException("Нет xray.exe в папке bin рядом с exe.", xrayExe);
        if (!File.Exists(sbExe)) throw new FileNotFoundException("Нет sing-box.exe в папке bin.", sbExe);
        if (!File.Exists(wintun)) throw new FileNotFoundException("Нет wintun.dll в папке bin.", wintun);

        var work = ProfileStore.AppDir;
        Directory.CreateDirectory(work);
        ProfileStore.ClearLog();

        var xrayCfg = Path.Combine(work, "xray-client.json");
        var sbCfg = Path.Combine(work, "sing-box.json");
        Utf8Json.WriteFile(xrayCfg, XrayConfigBuilder.Build(profile));
        Utf8Json.WriteFile(sbCfg, SingBoxConfigBuilder.Build(profile));

        ValidateXray(xrayExe, xrayCfg);
        ValidateSingBox(sbExe, sbCfg);

        foreach (var h in profile.AllServerHosts())
        {
            AddServerBypassRoute(h);
            _bypassHosts.Add(h);
        }

        _xray = StartProcess(xrayExe, $"run -config \"{xrayCfg}\"", bin, "xray");
        await Task.Delay(1200);
        if (_xray.HasExited)
            throw Fail("xray сразу завершился", work);

        _singbox = StartProcess(sbExe, $"run -c \"{sbCfg}\"", bin, "sing-box");
        await Task.Delay(2500);
        if (_singbox.HasExited)
            throw Fail("sing-box сразу завершился", work);

        if (!await TunUpAsync())
            throw Fail("TUN не поднялся (10.0.85.1). Проверь wintun.dll и права администратора.", work);

        _ = WatchProcessesAsync();
    }

    private async Task WatchProcessesAsync()
    {
        while (_xray is not null || _singbox is not null)
        {
            await Task.Delay(2000);
            if (_xray is { HasExited: true })
            {
                var msg = "xray завершился, код " + _xray.ExitCode;
                ProfileStore.AppendLog("[watch] " + msg);
                await StopAsync();
                CoreDied?.Invoke(msg);
                return;
            }
            if (_singbox is { HasExited: true })
            {
                var msg = "sing-box завершился, код " + _singbox.ExitCode;
                ProfileStore.AppendLog("[watch] " + msg);
                await StopAsync();
                CoreDied?.Invoke(msg);
                return;
            }
        }
    }

    public Task StopAsync()
    {
        Kill(_singbox); _singbox = null;
        Kill(_xray); _xray = null;
        foreach (var h in _bypassHosts)
            RemoveServerBypassRoute(h);
        _bypassHosts.Clear();
        return Task.CompletedTask;
    }

    private static void ValidateXray(string xrayExe, string cfg)
    {
        var psi = new ProcessStartInfo
        {
            FileName = xrayExe,
            Arguments = $"run -test -config \"{cfg}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("xray -test не запустился");
        var text = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
        p.WaitForExit(15000);
        if (p.ExitCode != 0 || !text.Contains("Configuration OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Конфиг xray: " + text.Trim());
    }

    private static void ValidateSingBox(string sbExe, string sbCfg)
    {
        var psi = new ProcessStartInfo
        {
            FileName = sbExe,
            Arguments = $"check -c \"{sbCfg}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("sing-box check не запустился");
        var err = p.StandardError.ReadToEnd();
        var outp = p.StandardOutput.ReadToEnd();
        p.WaitForExit(15000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException("Конфиг sing-box: " + (err + outp).Trim());
    }

    private static InvalidOperationException Fail(string msg, string work) =>
        new(msg + "\n\n" + ProfileStore.ReadLogTail(12) + "\n\nЛог: " + ProfileStore.LogPath);

    private static Process StartProcess(string exe, string args, string cwd, string tag)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        var p = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить " + exe);
        try { ChildProcessJob.Add(p); } catch { /* job optional on older Windows */ }
        void Log(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            ProfileStore.AppendLog($"[{tag}] {line}");
        }
        p.ErrorDataReceived += (_, e) => Log(e.Data);
        p.OutputDataReceived += (_, e) => Log(e.Data);
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        return p;
    }

    private static void Kill(Process? p)
    {
        if (p is null || p.HasExited) return;
        try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
    }

    private static void AddServerBypassRoute(string host)
    {
        try
        {
            var gw = GetDefaultGateway();
            if (gw is null) return;
            RunNetsh($"interface ip add route {host}/32 \"{GetDefaultInterfaceName()}\" {gw} metric=1");
        }
        catch { /* optional */ }
    }

    private static void RemoveServerBypassRoute(string host)
    {
        try { RunNetsh($"interface ip delete route {host}/32"); } catch { /* ignore */ }
    }

    private static void RunNetsh(string args) =>
        Process.Start(new ProcessStartInfo("netsh", args) { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();

    private static string? GetDefaultGateway()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.Name.Contains("happ", StringComparison.OrdinalIgnoreCase)) continue;
            var ip = ni.GetIPProperties().GatewayAddresses.FirstOrDefault(g =>
                g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ip is not null) return ip.Address.ToString();
        }
        return null;
    }

    private static string GetDefaultInterfaceName()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            if (ni.Name.Contains("happ", StringComparison.OrdinalIgnoreCase)) continue;
            return ni.Name;
        }
        return "Ethernet";
    }

    private static async Task<bool> TunUpAsync()
    {
        for (var i = 0; i < 24; i++)
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var addr = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.ToString() == "10.0.85.1");
                if (addr is not null) return true;
            }
            await Task.Delay(250);
        }
        return false;
    }
}
