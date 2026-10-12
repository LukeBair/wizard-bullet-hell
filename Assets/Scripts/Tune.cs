using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Every tunable number in the game, read from tuning.json. Code asks for a value by dotted path
// with a fallback default, e.g. Tune.F("spells.Fireball.damage", 20f).
//
// Sources, in order:
//   1. The tuner server on Luke's Mac (tools/tuner), fetched at startup and on the title screen,
//      so edits apply on the next run without rebuilding. The host (ngrok URL or LAN IP) is baked in
//      at build time.
//   2. Assets/Resources/tuning.json bundled into the build.
//   3. The defaults written in code.
public static class Tune
{
    public const int Port = 8787;

    static Dictionary<string, object> values = new Dictionary<string, object>();
    public static string Source { get; private set; } = "defaults";
    public static bool Fetching { get; private set; }
    public static int Version { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void LoadBundled()
    {
        var asset = Resources.Load<TextAsset>("tuning");
        if (asset != null && TryApply(asset.text)) Source = "bundled";
    }

    static bool TryApply(string json)
    {
        try
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null) return false;
            var flat = new Dictionary<string, object>();
            Flatten(root, "", flat);
            values = flat;
            Version++;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"tuning.json could not be read: {e.Message}");
            return false;
        }
    }

    static void Flatten(Dictionary<string, object> node, string prefix, Dictionary<string, object> into)
    {
        foreach (var kv in node)
        {
            string key = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
            into[key] = kv.Value;   // keep objects and lists too, for List() lookups
            if (kv.Value is Dictionary<string, object> child) Flatten(child, key, into);
        }
    }

    public static float F(string path, float fallback)
    {
        if (values.TryGetValue(path, out var v))
        {
            if (v is double d) return (float)d;
            if (v is bool b) return b ? 1f : 0f;
        }
        return fallback;
    }

    public static bool Has(string path) => values.ContainsKey(path);

    public static int I(string path, int fallback) => Mathf.RoundToInt(F(path, fallback));

    public static bool B(string path, bool fallback)
    {
        if (values.TryGetValue(path, out var v))
        {
            if (v is bool b) return b;
            if (v is double d) return d != 0;
        }
        return fallback;
    }

    public static string S(string path, string fallback) =>
        values.TryGetValue(path, out var v) && v is string s ? s : fallback;

    // A list of objects, e.g. drops.extra. Empty if missing.
    public static List<Dictionary<string, object>> List(string path)
    {
        var result = new List<Dictionary<string, object>>();
        if (values.TryGetValue(path, out var v) && v is List<object> list)
            foreach (var item in list)
                if (item is Dictionary<string, object> d) result.Add(d);
        return result;
    }

    public static float Num(Dictionary<string, object> item, string key, float fallback) =>
        item.TryGetValue(key, out var v) && v is double d ? (float)d : fallback;

    public static string Str(Dictionary<string, object> item, string key, string fallback) =>
        item.TryGetValue(key, out var v) && v is string s ? s : fallback;

    // Fills public float/int/bool fields of an object from "<prefix>.<field>" keys.
    public static void Apply(object target, string prefix)
    {
        foreach (var f in target.GetType().GetFields())
        {
            string path = prefix + "." + f.Name;
            if (!values.ContainsKey(path)) continue;
            if (f.FieldType == typeof(float)) f.SetValue(target, F(path, (float)f.GetValue(target)));
            else if (f.FieldType == typeof(int)) f.SetValue(target, I(path, (int)f.GetValue(target)));
            else if (f.FieldType == typeof(bool)) f.SetValue(target, B(path, (bool)f.GetValue(target)));
        }
    }

    // ------------------------------------------------------------------ live fetch from the Mac

    public static IEnumerator FetchFromMac()
    {
        // tuning_host.txt holds either a full URL (an ngrok tunnel, reachable from any network) or a LAN IP.
        var host = Resources.Load<TextAsset>("tuning_host");
        string baseUrl = host != null ? host.text.Trim() : "127.0.0.1";
#if UNITY_EDITOR
        baseUrl = "127.0.0.1";
#endif
        if (!baseUrl.StartsWith("http")) baseUrl = $"http://{baseUrl}:{Port}";
        Fetching = true;
        bool reached = false;
        using (var req = UnityWebRequest.Get(baseUrl + "/tuning.json"))
        {
            req.SetRequestHeader("ngrok-skip-browser-warning", "1");
            req.timeout = 5;
            yield return req.SendWebRequest();
            reached = req.result == UnityWebRequest.Result.Success;
            if (reached && TryApply(req.downloadHandler.text))
                Source = $"live from {baseUrl}";
        }
        // Same server: pick up save edits made in the tuner and report this device's progress.
        if (reached) yield return Grimoire.SyncWithMac(baseUrl);
        Fetching = false;
    }

    public static object ParseJson(string json) => Json.Parse(json);

    // ------------------------------------------------------------------ tiny JSON parser

    static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            return v;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                i++; // :
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // }
                return d;
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ]
                return l;
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'n') sb.Append('\n');
                    else if (e == 't') sb.Append('\t');
                    else if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
