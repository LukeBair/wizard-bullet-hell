using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Progress kept between runs (in PlayerPrefs): gold, which tomes are unlocked, feat progress and
// which evolutions are unlocked. None of it makes the wizard stronger on its own: a tome adds a
// spell to the pool a run can offer, an evolution lets that spell evolve during a run.
//   Tomes: three are free, the rest cost gold (meta.tomeCost).
//   Evolution II of a spell: feat "defeat meta.featBossesT2 bosses with it in your build", then gold.
//   Evolution III: feat "win a run with it at tier II or higher", then gold.
// The tuner on the Mac mirrors all of this (SyncWithMac) and can edit it.
public static class Grimoire
{
    static readonly SpellId[] FreeTomes = { SpellId.ArcaneMissile, SpellId.Fireball, SpellId.FrostNova };

    public static int Gold
    {
        get => PlayerPrefs.GetInt("gold", 0);
        private set { PlayerPrefs.SetInt("gold", value); PlayerPrefs.Save(); }
    }

    public static void AddGold(int amount)
    {
        if (amount > 0) Gold += amount;
    }

    static bool Spend(int cost)
    {
        if (Gold < cost) return false;
        Gold -= cost;
        return true;
    }

    // ---------------------------------------------------------------- tomes

    public static bool HasTome(SpellId id) => Array.IndexOf(FreeTomes, id) >= 0 || PlayerPrefs.GetInt($"tome.{id}", 0) == 1;

    public static int TomeCost => Tune.I("meta.tomeCost", 100);

    public static bool BuyTome(SpellId id)
    {
        if (HasTome(id) || !Spend(TomeCost)) return false;
        PlayerPrefs.SetInt($"tome.{id}", 1);
        PlayerPrefs.Save();
        return true;
    }

    // ---------------------------------------------------------------- evolutions and feats

    public static bool HasEvolution(SpellId id, int tier) => PlayerPrefs.GetInt($"evo.{id}.{tier}", 0) == 1;

    public static int EvolutionCost(int tier) => tier == 2 ? Tune.I("meta.evolveCostT2", 150) : Tune.I("meta.evolveCostT3", 300);

    static int FeatGoal(int tier) => tier == 2 ? Mathf.Max(1, Tune.I("meta.featBossesT2", 2)) : 1;
    static int FeatProgress(SpellId id, int tier) => PlayerPrefs.GetInt($"feat.{id}.{tier}", 0);
    public static bool FeatDone(SpellId id, int tier) => FeatProgress(id, tier) >= FeatGoal(tier);

    public static string FeatText(SpellId id, int tier)
    {
        string name = Spell.Preview(id).Name;
        if (tier == 2)
            return $"Defeat {FeatGoal(2)} bosses with {name} in your build ({Mathf.Min(FeatProgress(id, 2), FeatGoal(2))}/{FeatGoal(2)})";
        return $"Win a run with {name} at tier II or higher";
    }

    // Tier III also needs tier II unlocked first.
    public static bool CanBuyEvolution(SpellId id, int tier) =>
        !HasEvolution(id, tier) && FeatDone(id, tier) && (tier == 2 || HasEvolution(id, 2)) && Gold >= EvolutionCost(tier);

    public static bool BuyEvolution(SpellId id, int tier)
    {
        if (!CanBuyEvolution(id, tier) || !Spend(EvolutionCost(tier))) return false;
        PlayerPrefs.SetInt($"evo.{id}.{tier}", 1);
        PlayerPrefs.Save();
        return true;
    }

    // A boss died: every evolving spell in the build counts toward its tier II feat.
    public static void RecordBossKill(Player player)
    {
        foreach (var s in player.Spells)
            if (s.CanEvolve) Bump(s, 2, FeatProgress(s.Id, 2) + 1);
        PlayerPrefs.Save();
    }

    // The run was won: spells at tier II or higher complete their tier III feat.
    public static void RecordWin(Player player)
    {
        foreach (var s in player.Spells)
            if (s.CanEvolve && s.Tier >= 2) Bump(s, 3, 1);
        PlayerPrefs.Save();
    }

    static void Bump(Spell s, int tier, int value)
    {
        bool was = FeatDone(s.Id, tier);
        PlayerPrefs.SetInt($"feat.{s.Id}.{tier}", value);
        if (!was && FeatDone(s.Id, tier))
            GameManager.Instance.Toast($"Feat done: {s.Name} {(tier == 2 ? "II" : "III")} in the Grimoire");
    }

    // ---------------------------------------------------------------- tuner sync

    const string RevisionKey = "save.revision";   // last tuner edit applied on this device

    static long AppliedRevision => long.TryParse(PlayerPrefs.GetString(RevisionKey, "0"), out var r) ? r : 0;

    // Applies a save edit waiting on the tuner server (if it's newer than the last one applied), then
    // reports this device's save so the tuner page shows it.
    public static IEnumerator SyncWithMac(string baseUrl)
    {
        using (var req = UnityWebRequest.Get(baseUrl + "/save.json"))
        {
            req.timeout = 2;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) TryImport(req.downloadHandler.text);
        }
        using (var req = new UnityWebRequest(baseUrl + "/save.json", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(Export()));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 2;
            yield return req.SendWebRequest();
        }
    }

    static void TryImport(string json)
    {
        Dictionary<string, object> root;
        try { root = Tune.ParseJson(json) as Dictionary<string, object>; }
        catch (Exception) { return; }
        if (root == null || !(root.TryGetValue("pending", out var p) && p is bool pending && pending)) return;
        long revision = root.TryGetValue("revision", out var r) && r is double d ? (long)d : 0;
        if (revision <= AppliedRevision) return;

        if (root.TryGetValue("gold", out var g) && g is double gold) PlayerPrefs.SetInt("gold", Mathf.Max(0, (int)gold));
        if (root.TryGetValue("spells", out var sp) && sp is Dictionary<string, object> spells)
            foreach (var kv in spells)
            {
                if (!Enum.TryParse(kv.Key, out SpellId id) || !(kv.Value is Dictionary<string, object> e)) continue;
                SetFlag(e, "tome", $"tome.{id}");
                if (e.TryGetValue("feat2", out var f2) && f2 is double n) PlayerPrefs.SetInt($"feat.{id}.2", Mathf.Max(0, (int)n));
                SetFlag(e, "evo2", $"evo.{id}.2");
                SetFlag(e, "feat3", $"feat.{id}.3");
                SetFlag(e, "evo3", $"evo.{id}.3");
            }
        PlayerPrefs.SetString(RevisionKey, revision.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        GameManager.Instance.Toast("Save updated from the tuner");
    }

    static void SetFlag(Dictionary<string, object> e, string field, string key)
    {
        if (e.TryGetValue(field, out var v) && v is bool b) PlayerPrefs.SetInt(key, b ? 1 : 0);
    }

    static string Export()
    {
        var sb = new StringBuilder();
        sb.Append($"{{\"revision\":{AppliedRevision.ToString(CultureInfo.InvariantCulture)},\"gold\":{Gold},\"spells\":{{");
        var ids = Spell.AllIds;
        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            var spell = Spell.Preview(id);
            string B(bool v) => v ? "true" : "false";
            sb.Append($"\"{id}\":{{\"name\":\"{spell.Name}\",\"free\":{B(Array.IndexOf(FreeTomes, id) >= 0)},\"tome\":{B(HasTome(id))},\"canEvolve\":{B(spell.CanEvolve)},");
            sb.Append($"\"feat2\":{FeatProgress(id, 2)},\"feat2Goal\":{FeatGoal(2)},\"evo2\":{B(HasEvolution(id, 2))},\"feat3\":{B(FeatDone(id, 3))},\"evo3\":{B(HasEvolution(id, 3))}}}");
            if (i < ids.Length - 1) sb.Append(',');
        }
        sb.Append("}}");
        return sb.ToString();
    }
}
