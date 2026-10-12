using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Before each player build, bakes where the tuner server (tools/tuner) lives into
// Resources/tuning_host.txt: the public URL of a running ngrok tunnel (`ngrok http 8787`), so the phone
// reaches it from any network, or else this Mac's LAN address for a phone on the same Wi-Fi.
// Also allows plain-HTTP requests, which the local tuner server needs.
class TuningHost : IPreprocessBuildWithReport
{
    const string HostFile = "Assets/Resources/tuning_host.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Directory.CreateDirectory("Assets/Resources");
        File.WriteAllText(HostFile, NgrokUrl() ?? LanAddress());
        AssetDatabase.ImportAsset(HostFile);
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
    }

    // Asks ngrok's local API for the tunnel's public URL; null when ngrok isn't running.
    static string NgrokUrl()
    {
        try
        {
            using (var web = new WebClient())
            {
                string json = web.DownloadString("http://127.0.0.1:4040/api/tunnels");
                var m = System.Text.RegularExpressions.Regex.Match(json, "\"public_url\":\"(https://[^\"]+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
        }
        catch (WebException) { return null; }
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
