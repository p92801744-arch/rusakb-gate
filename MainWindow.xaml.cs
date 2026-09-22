using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using RusakbGate.Models;
using RusakbGate.Services;

namespace RusakbGate;

public partial class MainWindow : Window
{
    private readonly VpnController _vpn = new();
    private GateProfile? _profile;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        _vpn.CoreDied += OnCoreDied;
        VersionText.Text = "Личный VPN · v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");
        Loaded += (_, _) =>
        {
            try { RefreshProfileStatus(); }
            catch (Exception ex)
            {
                ProfileStore.AppendLog("LOAD " + ex);
                StatusText.Text = ex.Message;
                PowerToggle.IsEnabled = false;
                SetPill(false, error: true);
            }
        };
    }

    private void RefreshProfileStatus()
    {
        try
        {
            _profile = ProfileStore.Load();
            ClientConfigWriter.WriteAll(_profile);
            ServerLine.Text = $"Основной { _profile.ServerHost}" +
                              (string.IsNullOrWhiteSpace(_profile.ServerHostBackup)
                                  ? ""
                                  : $" · запасной {_profile.ServerHostBackup}");
            SetPill(false);
            var hint = VpnEnvironment.IsHappActive()
                ? "\n⚠ Сначала полностью выключи Happ — иначе VPN не поднимется."
                : "";
            StatusText.Text = "Нажми круг — весь ПК пойдёт через VPN." + hint;
            PowerToggle.IsEnabled = !VpnEnvironment.IsHappActive();
            if (VpnEnvironment.IsHappActive())
                SetPill(false, error: true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            PowerToggle.IsEnabled = false;
            SetPill(false, error: true);
        }
    }

    private void OnCoreDied(string message)
    {
        Dispatcher.Invoke(() =>
        {
            _busy = true;
            try
            {
                PowerToggle.IsChecked = false;
                SetPill(false, error: true);
                StatusText.Text = "VPN оборвался: " + message + "\nЛог: " + ProfileStore.LogPath;
            }
            finally
            {
                _busy = false;
                PowerToggle.IsEnabled = true;
            }
        });
    }

    private void SetPill(bool on, bool error = false)
    {
        if (error)
        {
            StatusPill.Background = (Brush)FindResource("Err");
            StatusPillText.Text = "ОШИБКА";
            StatusPillText.Foreground = Brushes.White;
            return;
        }
        if (on)
        {
            StatusPill.Background = (Brush)FindResource("OkDark");
            StatusPillText.Text = "ПОДКЛЮЧЕНО";
            StatusPillText.Foreground = Brushes.White;
        }
        else
        {
            StatusPill.Background = (Brush)FindResource("Bg2");
            StatusPillText.Text = "ВЫКЛ";
            StatusPillText.Foreground = (Brush)FindResource("Muted");
        }
    }

    private async void PowerToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_busy || _profile is null) return;
        await SetVpnAsync(true);
    }

    private async void PowerToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_busy || _profile is null) return;
        await SetVpnAsync(false);
    }

    private async Task SetVpnAsync(bool on)
    {
        _busy = true;
        PowerToggle.IsEnabled = false;
        try
        {
            if (on)
            {
                StatusText.Text = "Подключаю… (нужны права администратора)";
                await _vpn.StartAsync(_profile!);
                SetPill(true);
                StatusText.Text = "VPN включён. Локальная сеть и IP сервера — мимо туннеля.";
            }
            else
            {
                StatusText.Text = "Отключаю…";
                await _vpn.StopAsync();
                SetPill(false);
                StatusText.Text = "VPN выключен.";
            }
        }
        catch (Exception ex)
        {
            PowerToggle.IsChecked = false;
            SetPill(false, error: true);
            StatusText.Text = ex.Message;
        }
        finally
        {
            PowerToggle.IsEnabled = true;
            _busy = false;
        }
    }

    private async void CheckIp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Проверяю IP…";
            var ip = await ReadPublicIpAsync(_vpn.IsRunning ? _profile : null);
            if (_vpn.IsRunning && _profile is not null)
            {
                var ok = _profile.AllServerHosts().Contains(ip);
                StatusText.Text = ok
                    ? $"IP: {ip} — совпадает с сервером, туннель работает."
                    : $"Туннель работает, наружу виден {ip}. В профиле сейчас {_profile.ServerHost}.";
                SetPill(true, error: false);
            }
            else
                StatusText.Text = $"Сейчас IP: {ip}. Включи VPN и проверь снова.";
            ShowIpResult();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Проверка не удалась: " + ex.Message;
            SetPill(false, error: true);
            ShowIpResult();
        }
    }

    private void ShowIpResult()
    {
        MainActions.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Visible;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        MainActions.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Collapsed;
        if (_vpn.IsRunning)
        {
            SetPill(true);
            StatusText.Text = "VPN включён. Локальная сеть и IP сервера — мимо туннеля.";
            return;
        }

        SetPill(false);
        var hint = VpnEnvironment.IsHappActive()
            ? "\n⚠ Сначала полностью выключи Happ — иначе VPN не поднимется."
            : "";
        StatusText.Text = "Нажми круг — весь ПК пойдёт через VPN." + hint;
    }

    /// <summary>
    /// При включённом VPN спрашиваем IP через локальный HTTP-порт xray.
    /// ifconfig.me через TUN часто зависает и кнопка падала по таймауту.
    /// </summary>
    private static async Task<string> ReadPublicIpAsync(GateProfile? viaTunnel)
    {
        using var handler = new HttpClientHandler();
        if (viaTunnel is not null)
        {
            handler.Proxy = new WebProxy($"http://127.0.0.1:{viaTunnel.HttpPort}");
            handler.UseProxy = true;
        }

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.0");

        var urls = new[]
        {
            "https://icanhazip.com",
            "https://ifconfig.me/ip",
            "https://api.ipify.org"
        };
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                var body = await http.GetStringAsync(url);
                var ip = ExtractIpv4(body);
                if (ip is not null) return ip;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            "Сервисы проверки IP не ответили. " + (last?.Message ?? ""));
    }

    private static string? ExtractIpv4(string body)
    {
        var trace = Regex.Match(body, @"(?m)^ip=(\d{1,3}(?:\.\d{1,3}){3})\s*$");
        if (trace.Success) return trace.Groups[1].Value;
        var plain = Regex.Match(body.Trim(), @"^(?:\d{1,3}\.){3}\d{1,3}$");
        return plain.Success ? plain.Value : null;
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var path = ProfileStore.LogPath;
        if (!File.Exists(path))
        {
            StatusText.Text = "Лога пока нет — попробуй включить VPN.";
            return;
        }
        Process.Start(new ProcessStartInfo("notepad.exe", path) { UseShellExecute = true });
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
            ShowInTaskbar = true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try { _vpn.StopAsync().GetAwaiter().GetResult(); } catch { /* exit */ }
        base.OnClosing(e);
    }
}
