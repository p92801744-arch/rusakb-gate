using System.Text.Json;

namespace RusakbGate.Services;

public static class UiSettings
{
    private static string FilePath => Path.Combine(ProfileStore.AppDir, "ui-settings.json");

    /// <summary>По умолчанию включено: Chrome, Яндекс.Браузер и Bitrix24 мимо туннеля.</summary>
    public static bool BypassApps
    {
        get
        {
            try
            {
                if (!File.Exists(FilePath)) return true;
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                if (doc.RootElement.TryGetProperty("bypassApps", out var value) &&
                    value.ValueKind == JsonValueKind.False)
                    return false;
            }
            catch
            {
                /* битый файл — оставляем обход включённым */
            }

            return true;
        }
    }

    public static void SaveBypassApps(bool on)
    {
        Directory.CreateDirectory(ProfileStore.AppDir);
        var json = JsonSerializer.Serialize(new Dictionary<string, bool> { ["bypassApps"] = on });
        File.WriteAllText(FilePath, json);
    }
}
