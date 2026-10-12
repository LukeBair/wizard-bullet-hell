using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Before each player build, bakes this Mac's LAN address into Resources/tuning_host.txt so the game
// on a phone can fetch live values from the tuner server (tools/tuner) on the same Wi-Fi.
// Also allows plain-HTTP requests, which the local tuner server needs.
class TuningHost : IPreprocessBuildWithReport
{
    const string HostFile = "Assets/Resources/tuning_host.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Directory.CreateDirectory("Assets/Resources");
        File.WriteAllText(HostFile, LanAddress());
        AssetDatabase.ImportAsset(HostFile);
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
    }

    static string LanAddress()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var a in ni.GetIPProperties().UnicastAddresses)
            {
                var ip = a.Address;
                if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                string s = ip.ToString();
                if (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("172.")) return s;
            }
        }
        return "127.0.0.1";
    }
}
