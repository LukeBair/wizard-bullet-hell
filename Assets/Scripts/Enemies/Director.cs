using System.Collections.Generic;
using UnityEngine;

// Decides what spawns and when. Pressure builds in stages:
//   Stage 0 (before the first swarm): a light trickle of chaff, a rare basic medium.
//   Stage 1 (after the first swarm): mediums show up sometimes, all types possible.
//   Stage 2+ (each boss killed): mediums come often and in packs; swarms bring escorts.
// Enemy health steps up +25% per swarm and +100% per boss killed.
public class Director : MonoBehaviour
{
    const int MaxEnemies = 200;
    const float BossEvery = 150f;
    const float SwarmEvery = 45f;

    public float Elapsed { get; private set; }
    public float HealthScale { get; private set; } = 1f;
    public int Threat { get; private set; }   // swarms survived
    public int BossesDefeated { get; private set; }
    public int Stage => Threat == 0 ? 0 : 1 + BossesDefeated;
    public Enemy Boss { get; private set; }

    EnemyRoster roster;
    float chaffBudget, mediumTimer, swarmTimer, bossTimer;
    int bossIndex;

    public void ResetRun(EnemyRoster r)
    {
        roster = r;
        Elapsed = 0f;
        HealthScale = 1f;
        Threat = 0;
        BossesDefeated = 0;
        chaffBudget = 0f;
        mediumTimer = 40f;
        swarmTimer = SwarmEvery;
        bossTimer = BossEvery;
        bossIndex = 0;
        Boss = null;
    }

    void OnEnable() => Enemy.Died += OnEnemyDied;
    void OnDisable() => Enemy.Died -= OnEnemyDied;

    void OnEnemyDied(Enemy e)
    {
        if (e != Boss) return;
        Boss = null;
        BossesDefeated++;
        HealthScale += 1f;
        XpGem.MagnetAll();
        Sfx.Play(Sound.BossDie, 1f, 0f);
        Haptics.Notification(Haptics.Notify.Success);
        GameManager.Instance.Player.Heal(30f);
        GameManager.Instance.Toast($"{e.Def.Name} defeated!");
        GameManager.Instance.Toast("The forest grows stronger...");
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing || roster == null) return;
        float dt = Time.deltaTime;
        Elapsed += dt;

        float chaffRate = (0.4f + Elapsed / 90f) * (1f + 0.2f * Threat) * (1f + 0.5f * BossesDefeated);
        chaffRate = Mathf.Min(chaffRate, 10f) * (Boss != null ? 0.4f : 1f);
        chaffBudget += chaffRate * dt;
        while (chaffBudget >= 1f)
        {
            chaffBudget -= 1f;
            if (Enemy.All.Count < MaxEnemies) SpawnAtEdge(Pick(roster.Chaff));
        }

        mediumTimer -= dt;
        if (mediumTimer <= 0f)
        {
            int stage = Stage;
            if (stage == 0)
            {
                mediumTimer = 14f;
                SpawnAtEdge(PickMedium());
            }
            else if (stage == 1)
            {
                mediumTimer = 9f;
                int pack = Random.value < 0.3f ? 2 : 1;
                for (int i = 0; i < pack; i++) SpawnAtEdge(PickMedium());
            }
            else
            {
                // Packs that come from one direction, bigger and more often with each boss killed.
                mediumTimer = Mathf.Max(2.5f, 6f - (stage - 2));
                int pack = Mathf.Min(Random.Range(2, stage + 1), 5);
                var def = PickMedium();
                Vector2 at = EdgePoint(def.Radius);
                for (int i = 0; i < pack && Enemy.All.Count < MaxEnemies; i++)
                    Spawn(i == 0 ? def : PickMedium(), at + Random.insideUnitCircle * 1.5f);
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
            HealthScale += 0.25f;
        }

        bossTimer -= dt;
        if (bossTimer <= 0f)
        {
            bossTimer = BossEvery;
            var def = roster.Bosses[bossIndex++ % roster.Bosses.Length];
            Boss = SpawnAtEdge(def);
            gm.Toast($"{def.Name} approaches!");
            Sfx.Play(Sound.Warning, 0.7f, 0f);
            Haptics.Tap(Haptics.Impact.Heavy);
        }
    }

    void SpawnSwarm(GameManager gm)
    {
        // A closing ring of bats; slimes join from the second swarm, medium escorts after a boss kill.
        int n = 16 + 6 * Threat;
        var bat = roster.Chaff[Mathf.Min(1, roster.Chaff.Length - 1)];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var def = Threat > 0 && i % 3 == 0 ? roster.Chaff[0] : bat;
            Spawn(def, gm.Player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (10f + (i % 2) * 1.5f));
        }
        for (int i = 0; i < (Stage - 1) * 2; i++) SpawnAtEdge(PickMedium());
        gm.Toast(Threat == 0 ? "A swarm descends!" : $"A bigger swarm descends! (Threat {Threat + 1})");
    }

    static EnemyDef Pick(EnemyDef[] defs) => defs[Random.Range(0, defs.Length)];

    readonly List<EnemyDef> pool = new List<EnemyDef>();

    EnemyDef PickMedium()
    {
        pool.Clear();
        foreach (var d in roster.Medium) if (d.EarlyGame || Threat > 0) pool.Add(d);
        return pool[Random.Range(0, pool.Count)];
    }

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

    Enemy SpawnAtEdge(EnemyDef def) => Spawn(def, EdgePoint(def.Radius));

    Enemy Spawn(EnemyDef def, Vector2 p)
    {
        var gm = GameManager.Instance;
        p = gm.Arena.PushOut(p, def.Radius);
        // Bosses already have huge health; they scale at half rate.
        float scale = def.Tier == EnemyTier.Boss ? 1f + (HealthScale - 1f) * 0.5f : HealthScale;
        return Enemy.Spawn(def, p, scale);
    }
}
