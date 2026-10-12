using UnityEngine;

// Decides what spawns and when. Pressure ramps with time: a steady trickle of chaff, medium enemies
// from ~35s, bat swarms every 45s, and a boss every 2.5 minutes.
public class Director : MonoBehaviour
{
    const int MaxEnemies = 140;
    const float BossEvery = 150f;
    const float SwarmEvery = 45f;

    public float Elapsed { get; private set; }
    public float HealthScale => 1f + Elapsed / 110f;
    public Enemy Boss { get; private set; }
    public float NextBossIn => BossEvery - Elapsed % BossEvery;

    EnemyRoster roster;
    float chaffBudget, mediumTimer, swarmTimer, bossTimer;
    int bossIndex;

    public void ResetRun(EnemyRoster r)
    {
        roster = r;
        Elapsed = 0f;
        chaffBudget = 0f;
        mediumTimer = 35f;
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
        XpGem.MagnetAll();
        GameManager.Instance.Player.Heal(30f);
        GameManager.Instance.Toast($"{e.Def.Name} defeated!");
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing || roster == null) return;
        float dt = Time.deltaTime;
        Elapsed += dt;

        float chaffRate = Mathf.Min(0.9f + Elapsed / 25f, 7f) * (Boss != null ? 0.4f : 1f);
        chaffBudget += chaffRate * dt;
        while (chaffBudget >= 1f)
        {
            chaffBudget -= 1f;
            if (Enemy.All.Count < MaxEnemies) SpawnAtEdge(Pick(roster.Chaff));
        }

        mediumTimer -= dt;
        if (mediumTimer <= 0f)
        {
            mediumTimer = Mathf.Max(2.5f, 9f - Elapsed / 35f);
            SpawnAtEdge(Pick(roster.Medium));
        }

        swarmTimer -= dt;
        if (swarmTimer <= 0f)
        {
            swarmTimer = SwarmEvery;
            int n = 12 + (int)(Elapsed / 30f);
            var bat = roster.Chaff[Mathf.Min(1, roster.Chaff.Length - 1)];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Spawn(bat, gm.Player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 10f);
            }
            gm.Toast("A swarm descends!");
        }

        bossTimer -= dt;
        if (bossTimer <= 0f)
        {
            bossTimer = BossEvery;
            var def = roster.Bosses[bossIndex++ % roster.Bosses.Length];
            Boss = SpawnAtEdge(def);
            gm.Toast($"{def.Name} approaches!");
        }
    }

    static EnemyDef Pick(EnemyDef[] defs) => defs[Random.Range(0, defs.Length)];

    Enemy SpawnAtEdge(EnemyDef def)
    {
        var gm = GameManager.Instance;
        float distance = gm.ViewRadius + 1.5f + def.Radius;
        Vector2 center = gm.Player.Position;
        Vector2 p = center;
        for (int i = 0; i < 12; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance;
            if (gm.Arena.Inside(p, -def.Radius) && !gm.Arena.Blocked(p, def.Radius)) break;
        }
        return Spawn(def, p);
    }

    Enemy Spawn(EnemyDef def, Vector2 p)
    {
        var gm = GameManager.Instance;
        p = gm.Arena.PushOut(p, def.Radius);
        float scale = def.Tier == EnemyTier.Boss ? 1f + Elapsed / 600f : HealthScale;
        return Enemy.Spawn(def, p, scale);
    }
}
