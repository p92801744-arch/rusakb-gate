using RusakbGate.Models;

namespace RusakbGate.Services;

public static class ClientConfigWriter
{
    public static void WriteAll(GateProfile profile)
    {
        Directory.CreateDirectory(ProfileStore.AppDir);
        Utf8Json.WriteFile(Path.Combine(ProfileStore.AppDir, "xray-client.json"), XrayConfigBuilder.Build(profile));
        Utf8Json.WriteFile(Path.Combine(ProfileStore.AppDir, "sing-box.json"), SingBoxConfigBuilder.Build(profile));
    }
}
