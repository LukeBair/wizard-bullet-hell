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
//   Evolution III: the spell's signature feat (meta.feats, done in one run at tier II+), then gold.
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
        if (!HasSignatureFeat(id)) return $"Win a run with {name} at tier II or higher";
        return Tune.S($"meta.feats.{id}.text", "Signature feat").Replace("{goal}", $"{SignatureGoal(id):0}") + $" (with {name} at tier II+)";
    }

    // ---------------------------------------------------------------- tier III signature feats
    // Each spell's tier III feat is a challenge done within one run while the spell is at tier II or
    // higher (meta.feats.<SpellId>: goal, text, kind). kind "count" adds up over the run, "max" is the
    // best single moment (e.g. foes hit by one explosion), "win" counts up but only completes on a win.
    // Spells without an entry keep the old feat: win a run with the spell at tier II+.

    static readonly Dictionary<SpellId, float> runFeat = new Dictionary<SpellId, float>();
    // Signature feats not done yet, with their goal and whether they need a win; read once per run.
    static readonly Dictionary<SpellId, (float goal, bool needsWin)> openFeats = new Dictionary<SpellId, (float, bool)>();

    // This run's progress on open signature feats, for the run log.
    public static Dictionary<string, float> RunFeats()
    {
        var m = new Dictionary<string, float>();
        foreach (var kv in runFeat) m[kv.Key.ToString()] = kv.Value;
        return m;
    }

    static bool HasSignatureFeat(SpellId id) => Tune.Has($"meta.feats.{id}.goal");
    static float SignatureGoal(SpellId id) => Tune.F($"meta.feats.{id}.goal", 1f);
    static string FeatKind(SpellId id) => Tune.S($"meta.feats.{id}.kind", "count");

    public static void BeginRunFeats()
    {
        runFeat.Clear();
        openFeats.Clear();
        foreach (var id in Spell.AllIds)
            if (HasSignatureFeat(id) && !FeatDone(id, 3)) openFeats[id] = (SignatureGoal(id), FeatKind(id) == "win");
    }

    // Progress on a spell's tier III feat. Ignored unless the spell is in the build at tier II+.
    public static void Feat(SpellId id, float amount = 1f)
    {
        if (!Tracking(id)) return;
        runFeat.TryGetValue(id, out float v);
        runFeat[id] = v + amount;
        CheckFeat(id, false);
    }

    // A "max" feat: the best value reached in one go this run.
    public static void FeatMax(SpellId id, float value)
    {
        if (!Tracking(id)) return;
        runFeat.TryGetValue(id, out float v);
        if (value <= v) return;
        runFeat[id] = value;
        CheckFeat(id, false);
    }

    static bool Tracking(SpellId id)
    {
        if (!openFeats.ContainsKey(id)) return false;   // hot path (every missile hit): no string or PlayerPrefs work
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        var spell = player != null ? player.GetSpell(id) : null;
        return spell != null && spell.Tier >= 2;
    }

    static void CheckFeat(SpellId id, bool won)
    {
        if (!openFeats.TryGetValue(id, out var feat) || (feat.needsWin && !won)) return;
        runFeat.TryGetValue(id, out float v);
        if (v < feat.goal) return;
        openFeats.Remove(id);
        PlayerPrefs.SetInt($"feat.{id}.3", 1);
        PlayerPrefs.Save();
        GameManager.Instance.Toast($"Feat done: {Spell.Preview(id).Name} III in the Grimoire");
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

    // The run was won: spells at tier II+ without a signature feat complete their tier III feat, and
    // "win" signature feats (e.g. Rejuvenate's) complete if their goal was reached.
    public static void RecordWin(Player player)
    {
        foreach (var s in player.Spells)
        {
            if (!s.CanEvolve || s.Tier < 2) continue;
            if (!HasSignatureFeat(s.Id)) Bump(s, 3, 1);
            else CheckFeat(s.Id, true);
        }
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
            req.SetRequestHeader("ngrok-skip-browser-warning", "1");
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) TryImport(req.downloadHandler.text);
        }
        using (var req = new UnityWebRequest(baseUrl + "/save.json", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(Export()));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("ngrok-skip-browser-warning", "1");
            req.timeout = 5;
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
