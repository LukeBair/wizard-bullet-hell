using System;
using UnityEngine;

// Progress kept between runs (in PlayerPrefs): gold, which tomes are unlocked, feat progress and
// which evolutions are unlocked. None of it makes the wizard stronger on its own: a tome adds a
// spell to the pool a run can offer, an evolution lets that spell evolve during a run.
//   Tomes: three are free, the rest cost gold (meta.tomeCost).
//   Evolution II of a spell: feat "defeat meta.featBossesT2 bosses with it in your build", then gold.
//   Evolution III: feat "win a run with it at tier II or higher", then gold.
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
}
