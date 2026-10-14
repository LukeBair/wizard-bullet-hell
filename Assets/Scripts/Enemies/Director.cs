using System.Collections.Generic;
using UnityEngine;

// Decides what spawns and when. Pressure builds in stages:
//   Stage 0 (before the first swarm): a light trickle of chaff, a rare basic medium.
//   Stage 1 (after the first swarm): mediums show up sometimes, all types possible.
//   Stage 2+ (each boss killed): mediums come often and in packs; swarms bring escorts.
// Enemy health steps up +25% per swarm and +100% per boss killed.
// While a boss is alive nothing else spawns (apart from whatever the boss summons itself), and all
// the timers pause until it's dead.
// A run has run.bosses bosses (0 = endless). Killing one moves the run to the next zone; killing the
// last one wins the run. A new zone starts empty and spawning ramps back up from zero over
// run.zoneRampSeconds, a little faster in each later zone (x run.zoneRampFactor per zone).
// spawning.zones has one row per zone that overrides its chaff and medium numbers; a zone without a
// row (or a key a row leaves out) uses the stage rules above.
public class Director : MonoBehaviour
{
    // All spawn numbers live in the tuner under spawning.*
    static int MaxEnemies => Tune.I("spawning.maxEnemies", 200);
    static float BossEvery => Tune.F("spawning.bossEvery", 150f);
    static float SwarmEvery => Tune.F("spawning.swarmEvery", 45f);
    static float T(string key, float fallback) => Tune.F(Tune.Key("spawning.", key), fallback);
    public static int RunBosses => Tune.I("run.bosses", 4);

    public float Elapsed { get; private set; }
    public float HealthScale { get; private set; } = 1f;
    public int Threat { get; private set; }   // swarms survived
    public int BossesDefeated { get; private set; }
    public int Stage => Threat == 0 ? 0 : 1 + BossesDefeated;
    public Enemy Boss { get; private set; }
    public bool IsFinalBoss => RunBosses > 0 && BossesDefeated == RunBosses - 1;
    public bool Holding;   // between zones: nothing spawns and the timers wait

    Biome zone;
    EnemyRoster roster;
    float chaffBudget, mediumTimer, swarmTimer, bossTimer;
    int bossIndex;
    int zoneNumber;          // 0 for the first zone of the run
    int zoneThreat;          // swarms survived in this zone
    float zoneTime;          // seconds since entering this zone
    Dictionary<string, object> zoneRow;   // this zone's row in spawning.zones, or null

    // A number from this zone's row, or the fallback when the row doesn't set it.
    float Z(string key, float fallback) => zoneRow != null ? Tune.Num(zoneRow, key, fallback) : fallback;
    // Multiplier on all damage enemies deal to the wizard in the current zone (its row's damageScale).
    public float DamageScale => Z("damageScale", 1f);
    bool ZoneHas(string key) => zoneRow != null && zoneRow.ContainsKey(key);

    public void ResetRun(Biome b)
    {
        zoneNumber = -1;
        EnterZone(b);
        Elapsed = 0f;
        HealthScale = 1f;
        Threat = 0;
        BossesDefeated = 0;
        chaffBudget = 0f;
        mediumTimer = T("firstMediumAt", 40f);
        swarmTimer = SwarmEvery;
        bossTimer = BossEvery;
        Boss = null;
        Holding = false;
    }

    // A new zone brings its own medium mix, and its own boss comes next.
    public void EnterZone(Biome b)
    {
        zone = b;
        roster = b.Roster;
        bossIndex = 0;
        zoneNumber++;
        zoneTime = 0f;
        zoneThreat = 0;
        var rows = Tune.List("spawning.zones");
        zoneRow = zoneNumber < rows.Count ? rows[zoneNumber] : null;
        chaffBudget = 0f;
        if (zoneNumber > 0)
        {
            mediumTimer = Tune.F("run.zoneFirstMedium", 8f) * RampSpeed;
            swarmTimer = Mathf.Max(swarmTimer, Tune.F("run.zoneRampSeconds", 40f) * RampSpeed);   // no swarm before the ramp is done
        }
    }

    // How fast this zone's spawning ramps up: 1 in the second zone, quicker after that.
    float RampSpeed => Mathf.Pow(Tune.F("run.zoneRampFactor", 0.75f), Mathf.Max(0, zoneNumber - 1));

    // 0 -> 1 over the zone's ramp time; the first zone of a run has no ramp.
    float ZoneRamp => zoneNumber == 0 ? 1f : Mathf.Clamp01(zoneTime / Mathf.Max(0.01f, Tune.F("run.zoneRampSeconds", 40f) * RampSpeed));

    void OnEnable() => Enemy.Died += OnEnemyDied;
    void OnDisable() => Enemy.Died -= OnEnemyDied;

    void OnEnemyDied(Enemy e)
    {
        if (e != Boss) return;
        Boss = null;
        BossesDefeated++;
        HealthScale += T("healthPerBossKill", 1f);
        XpGem.MagnetAll();
        Sfx.Play(Sound.BossDie, 1f, 0f);
        Haptics.Notification(Haptics.Notify.Success);
        GameManager.Instance.Player.Heal(T("bossKillHeal", 30f), source: "Boss kill");
        GameManager.Instance.Toast($"{e.Def.Name} defeated!");
        GameManager.Instance.BossDefeated(RunBosses > 0 && BossesDefeated >= RunBosses);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing || roster == null) return;
        float dt = Time.deltaTime;
        Elapsed += dt;
        if (Boss != null || Holding) return;   // boss fight or zone change: no other spawns
        zoneTime += dt;

        // Every zone climbs on the same curve (time and swarms counted from the zone's start), scaled by
        // the zone's chaffScale and raised by its chaffAdd (default: run.zoneRateStep per zone cleared).
        float chaffRate = (T("chaffBase", 0.4f) + zoneTime * T("chaffPerSecond", 1f / 240f))
                          * (1f + T("chaffPerThreat", 0.1f) * zoneThreat) * Z("chaffScale", 1f)
                          + Z("chaffAdd", Tune.F("run.zoneRateStep", 2f) * zoneNumber);
        chaffRate = Mathf.Min(chaffRate, T("chaffMax", 10f)) * ZoneRamp;
        chaffBudget += chaffRate * dt;
        while (chaffBudget >= 1f)
        {
            chaffBudget -= 1f;
            if (Enemy.All.Count < MaxEnemies) SpawnAtEdge(Pick(roster.Chaff), "trickle");
        }

        mediumTimer -= dt;
        if (mediumTimer <= 0f)
        {
            int stage = Stage;
            if (ZoneHas("mediumEvery"))
            {
                // This zone's row sets the pace: a pack of packMin..packMax from one direction.
                mediumTimer = Z("mediumEvery", 7f);
                int pack = Random.Range(Mathf.RoundToInt(Z("packMin", 1f)), Mathf.RoundToInt(Z("packMax", 1f)) + 1);
                var def = PickMedium();
                Vector2 at = EdgePoint(def.Radius);
                for (int i = 0; i < pack && Enemy.All.Count < MaxEnemies; i++)
                    Spawn(i == 0 ? def : PickMedium(), at + Random.insideUnitCircle * 1.5f, "pack");
            }
            else if (stage == 0)
            {
                mediumTimer = T("mediumEveryStage0", 14f);
                SpawnAtEdge(PickMedium(), "medium");
            }
            else if (stage == 1)
            {
                mediumTimer = T("mediumEveryStage1", 11f);
                int pack = Random.value < T("pairChanceStage1", 0.3f) ? 2 : 1;
                for (int i = 0; i < pack; i++) SpawnAtEdge(PickMedium(), "medium");
            }
            else
            {
                // Packs that come from one direction, bigger and more often with each boss killed.
                mediumTimer = Mathf.Max(T("mediumEveryMin", 2.5f), T("mediumEveryStage2", 6f) - (stage - 2));
                int pack = Mathf.Min(Random.Range(2, stage + 1), Tune.I("spawning.maxPack", 5));
                var def = PickMedium();
                Vector2 at = EdgePoint(def.Radius);
                for (int i = 0; i < pack && Enemy.All.Count < MaxEnemies; i++)
                    Spawn(i == 0 ? def : PickMedium(), at + Random.insideUnitCircle * 1.5f, "pack");
            }
        }

        swarmTimer -= dt;
        if (swarmTimer <= 0f)
        {
            swarmTimer = SwarmEvery;
            SpawnSwarm(gm);
            Sfx.Play(Sound.Warning, 0.5f, 0f);
            Haptics.Tap(Haptics.Impact.Medium);
            Threat++;
            zoneThreat++;
            HealthScale += T("healthPerSwarm", 0.25f);
        }

        bossTimer -= dt;
        if (bossTimer <= 0f)
        {
            bossTimer = BossEvery;
            var def = roster.Bosses[bossIndex++ % roster.Bosses.Length];
            Boss = SpawnAtEdge(def, "boss");
            gm.Toast(IsFinalBoss ? $"Final boss: {def.Name}!" : $"{def.Name} approaches!");
            Sfx.Play(Sound.Warning, 0.7f, 0f);
            Haptics.Tap(Haptics.Impact.Heavy);
        }
    }

    void SpawnSwarm(GameManager gm)
    {
        // A closing ring of bats; slimes join from the second swarm, medium escorts after a boss kill.
        int n = BossesDefeated == 0
            ? Mathf.RoundToInt(T("swarmBasePreBoss", 14f) + T("swarmPerThreatPreBoss", 3f) * Threat)
            : Mathf.RoundToInt((T("swarmBase", 16f) + T("swarmPerThreat", 6f) * Threat) * Z("swarmScale", 1f));
        var bat = roster.Chaff[Mathf.Min(1, roster.Chaff.Length - 1)];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var def = Threat > 0 && i % 3 == 0 ? roster.Chaff[0] : bat;
            Spawn(def, gm.Player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (10f + (i % 2) * 1.5f), "swarm");
        }
        for (int i = 0; i < (Stage - 1) * 2; i++) SpawnAtEdge(PickMedium(), "swarm");
        Telemetry.Swarm(Threat + 1, n + Mathf.Max(0, (Stage - 1) * 2));
        gm.Toast(Threat == 0 ? "A swarm descends!" : $"A bigger swarm descends! (Threat {Threat + 1})");
    }

    static EnemyDef Pick(EnemyDef[] defs) => defs[Random.Range(0, defs.Length)];

    readonly List<EnemyDef> pool = new List<EnemyDef>();

    static bool IsCaster(EnemyDef d) =>
        d.Behavior == EnemyBehavior.Caster || d.Behavior == EnemyBehavior.Blinker ||
        d.Behavior == EnemyBehavior.Lobber || d.Behavior == EnemyBehavior.Trickster;

    // Casters are rare (a quarter as likely) until the first boss is down; a zone row can set its own
    // casterWeight.
    EnemyDef PickMedium()
    {
        pool.Clear();
        foreach (var d in roster.Medium) if ((d.EarlyGame || Threat > 0) && !AtCap(d)) pool.Add(d);
        if (pool.Count == 0) foreach (var d in roster.Medium) if (d.EarlyGame || Threat > 0) pool.Add(d);
        float total = 0f;
        foreach (var d in pool) total += Weight(d);
        float roll = Random.value * total;
        foreach (var d in pool)
        {
            roll -= Weight(d);
            if (roll <= 0f) return d;
        }
        return pool[pool.Count - 1];
    }

    static bool AtCap(EnemyDef d)
    {
        if (d.MaxAlive <= 0) return false;
        int n = 0;
        foreach (var e in Enemy.All) if (e.Def.Name == d.Name && ++n >= d.MaxAlive) return true;
        return false;
    }

    float Weight(EnemyDef d) =>
        (IsCaster(d) ? Z("casterWeight", BossesDefeated == 0 ? T("casterWeightPreBoss", 0.25f) : 1f) : 1f)
        * (System.Array.IndexOf(zone.Favored, d.Name) >= 0 ? Tune.F("run.favoredWeight", 3f) : 1f);

    Vector2 EdgePoint(float radius)
    {
        var gm = GameManager.Instance;
        float distance = gm.ViewRadius + 1.5f + radius;
        Vector2 center = gm.Player.Position;
        Vector2 p = center;
        for (int i = 0; i < 12; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance;
            if (gm.Arena.Inside(p, -radius) && !gm.Arena.Blocked(p, radius)) break;
        }
        return p;
    }

    Enemy SpawnAtEdge(EnemyDef def, string cause) => Spawn(def, EdgePoint(def.Radius), cause);

    // cause is for telemetry: trickle, medium, pack, swarm or boss.
    Enemy Spawn(EnemyDef def, Vector2 p, string cause)
    {
        var gm = GameManager.Instance;
        p = gm.Arena.PushOut(p, def.Radius);
        // Bosses already have huge health; they scale at half rate.
        float scale = def.Tier == EnemyTier.Boss ? 1f + (HealthScale - 1f) * T("bossHealthScaleRate", 0.5f) : HealthScale;
        return Enemy.Spawn(def, p, scale, cause);
    }
}
