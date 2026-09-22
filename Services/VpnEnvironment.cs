using System.Diagnostics;
using System.Net.NetworkInformation;

namespace RusakbGate.Services;

public static class VpnEnvironment
{
    public static bool IsHappActive()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.Name.Contains("happ", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return Process.GetProcessesByName("Happ").Length > 0;
    }
}
