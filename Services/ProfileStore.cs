using System.IO;
using System.Text.Json;
using RusakbGate.Models;

namespace RusakbGate.Services;

public static class ProfileStore
{
    public static string AppDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RusakbGate");

    public static string ProfilePath => Path.Combine(AppDir, "profile.json");

    public static string BinDir => Path.Combine(AppContext.BaseDirectory, "bin");

    public static string LogPath => Path.Combine(AppDir, "gate.log");

    public static void ClearLog()
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(LogPath, "");
    }

    public static void AppendLog(string line) =>
        File.AppendAllText(LogPath, line + Environment.NewLine);

    public static string ReadLogTail(int lines = 10)
    {
        if (!File.Exists(LogPath)) return "";
        var all = File.ReadAllLines(LogPath);
        return string.Join(Environment.NewLine, all.TakeLast(lines));
    }

    public static GateProfile Load()
    {
        Directory.CreateDirectory(AppDir);
        if (!File.Exists(ProfilePath))
            throw new InvalidOperationException(
                $"Нет profile.json. Скопируй rusakb-gate-profile.json в:\n{ProfilePath}");

        var json = File.ReadAllText(ProfilePath);
        var p = JsonSerializer.Deserialize<GateProfile>(json, JsonOptions()) 
                ?? throw new InvalidOperationException("profile.json пустой");
        if (string.IsNullOrWhiteSpace(p.ServerHost) || string.IsNullOrWhiteSpace(p.Uuid))
            throw new InvalidOperationException("В profile.json не заполнены serverHost / uuid");
        return p;
    }

    public static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
