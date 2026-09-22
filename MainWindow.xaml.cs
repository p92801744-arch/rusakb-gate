using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
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
        VersionText.Text = "Личный VPN · v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");
        Loaded += (_, _) => RefreshProfileStatus();
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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var ip = (await http.GetStringAsync("https://ifconfig.me")).Trim();
            if (_vpn.IsRunning && _profile is not null)
            {
                var ok = _profile.AllServerHosts().Contains(ip);
                StatusText.Text = ok
                    ? $"IP: {ip} — совпадает с сервером, туннель работает."
                    : $"IP: {ip}. Ожидали {_profile.ServerHost} или {_profile.ServerHostBackup}.";
                SetPill(ok, error: !ok);
            }
            else
                StatusText.Text = $"Сейчас IP: {ip}. Включи VPN и проверь снова.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Проверка не удалась: " + ex.Message;
            SetPill(false, error: true);
        }
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

    protected override async void OnClosed(EventArgs e)
    {
        try { await _vpn.StopAsync(); } catch { /* exit */ }
        base.OnClosed(e);
    }
}
