using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// One enemy. Its EnemyDef says how it looks, moves and attacks; this class runs the behavior,
// status effects (burn, chill), contact damage and death drops.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();
    public static event Action<Enemy> Died;

    public EnemyDef Def { get; private set; }
    public float Radius => Def.Radius;
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool IsAlive => Health > 0f;
    public bool IsBoss => Def.Tier == EnemyTier.Boss;
    public bool IsChilled => chillTimer > 0f;
    public bool IsBurning => burnTimer > 0f;
    public Vector2 Position => transform.position;

    float fireTimer;
    float stateTimer;
    int state;
    int attackIndex;
    float spinAngle;
    float phase;
    Vector2 lockedDir;
    Vector2 knockback;
    float flash;
    float chillTimer;
    float burnTimer, burnDps;
    SpriteRenderer body;
    Color bodyColor;

    public static Enemy Spawn(EnemyDef def, Vector2 position, float healthScale)
    {
        var go = new GameObject(def.Name);
        go.transform.position = position;
        var e = go.AddComponent<Enemy>();
        e.Def = def;
        e.MaxHealth = e.Health = def.Health * healthScale;
        e.phase = Random.value * 10f;
        e.fireTimer = def.FireInterval * Random.Range(0.5f, 1f);
        e.stateTimer = Random.Range(2f, 4f);
        e.BuildVisual();
        return e;
    }

    public static Enemy Nearest(Vector2 from, float maxRange, ICollection<Enemy> exclude = null)
    {
        Enemy best = null;
        float bestSq = maxRange * maxRange;
        foreach (var e in All)
        {
            if (!e.IsAlive || (exclude != null && exclude.Contains(e))) continue;
            float sq = (e.Position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void BuildVisual()
    {
        var d = Def;
        float s = d.Radius * 2f;
        bodyColor = d.Color;
        int o = d.Tier == EnemyTier.Boss ? 40 : 20;
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.05f, -0.35f) * s, s, o - 2);

        switch (d.Look)
        {
            case EnemyLook.Bat:
                Wing(s, -1, o); Wing(s, 1, o);
                break;
            case EnemyLook.Beast:
                Horn(s, -1, o, 0.32f, 25f, d.Color * 0.8f);
                Horn(s, 1, o, 0.32f, 25f, d.Color * 0.8f);
                SpriteFactory.Child(transform, "Spines", SpriteFactory.Triangle, d.Accent, new Vector2(0f, -0.35f) * s, s * 0.4f, o - 1).transform.localRotation = Quaternion.Euler(0, 0, 180f);
                break;
            case EnemyLook.Goblin:
                Horn(s, -1, o, 0.45f, 70f, d.Color);
                Horn(s, 1, o, 0.45f, 70f, d.Color);
                break;
            case EnemyLook.Treant:
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f * Mathf.Deg2Rad;
                    SpriteFactory.Child(transform, "Leaf", SpriteFactory.Circle, d.Accent * 0.8f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.42f, s * 0.5f, o - 1);
                }
                break;
        }

        body = SpriteFactory.Child(transform, "Body", SpriteFactory.Circle, bodyColor, Vector2.zero, s, o);

        if (d.Look == EnemyLook.Wizard || d.Look == EnemyLook.Witch)
        {
            SpriteFactory.Child(transform, "Brim", SpriteFactory.Circle, d.Accent * 0.5f + new Color(0, 0, 0, 0.5f), new Vector2(0f, 0.3f) * s, s * 0.85f, o + 2).transform.localScale = new Vector3(s * 1.1f, s * 0.3f, 1f);
            SpriteFactory.Child(transform, "Hat", SpriteFactory.Triangle, d.Accent * 0.6f + new Color(0, 0, 0, 0.4f), new Vector2(0f, 0.6f) * s, s * 0.7f, o + 3);
        }

        Color eye = d.Tier == EnemyTier.Chaff ? Color.white : d.Accent;
        SpriteFactory.Child(transform, "EyeL", SpriteFactory.Circle, eye, new Vector2(-0.17f, 0.05f) * s, s * 0.17f, o + 1);
        SpriteFactory.Child(transform, "EyeR", SpriteFactory.Circle, eye, new Vector2(0.17f, 0.05f) * s, s * 0.17f, o + 1);
        SpriteFactory.Child(transform, "PupilL", SpriteFactory.Circle, Color.black, new Vector2(-0.17f, 0.03f) * s, s * 0.08f, o + 2);
        SpriteFactory.Child(transform, "PupilR", SpriteFactory.Circle, Color.black, new Vector2(0.17f, 0.03f) * s, s * 0.08f, o + 2);
    }

    void Wing(float s, int side, int order)
    {
        var w = SpriteFactory.Child(transform, "Wing", SpriteFactory.Triangle, Def.Accent, new Vector2(side * 0.55f, 0.05f) * s, s * 0.8f, order - 1);
        w.transform.localRotation = Quaternion.Euler(0, 0, side * 70f);
    }

    void Horn(float s, int side, int order, float size, float angle, Color c)
    {
        var h = SpriteFactory.Child(transform, "Horn", SpriteFactory.Triangle, c, new Vector2(side * 0.32f, 0.4f) * s, s * size, order - 1);
        h.transform.localRotation = Quaternion.Euler(0, 0, -side * angle);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        var player = gm.Player;
        if (player == null) return;

        float dt = Time.deltaTime;
        Vector2 pos = Position;
        Vector2 toPlayer = player.Position - pos;
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.up;
        float speed = Def.Speed * (IsChilled ? 0.5f : 1f);

        Vector2 move = Behave(dir, dist, speed, dt);

        // Light separation so crowds spread out instead of stacking into one blob.
        if (!IsBoss)
        {
            foreach (var other in All)
            {
                if (other == this) continue;
                Vector2 d = pos - other.Position;
                float r = (Radius + other.Radius) * 0.9f;
                float sq = d.sqrMagnitude;
                if (sq < r * r && sq > 0.0001f)
                    move += d / Mathf.Sqrt(sq) * 2f;
            }
        }

        pos += (move + knockback) * dt;
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, 30f * dt);
        if (Def.Look != EnemyLook.Bat) pos = gm.Arena.PushOut(pos, Radius * 0.8f);
        transform.position = pos;

        UpdateStatus(dt);

        float hit = Radius * 0.85f + Player.BodyRadius;
        if ((player.Position - pos).sqrMagnitude < hit * hit)
            player.TakeDamage(Def.ContactDamage);
    }

    Vector2 Behave(Vector2 dir, float dist, float speed, float dt)
    {
        var player = GameManager.Instance.Player;
        switch (Def.Behavior)
        {
            case EnemyBehavior.Chase:
                return dir * speed;

            case EnemyBehavior.Flutter:
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                return (dir + perp * Mathf.Sin(Time.time * 5f + phase)).normalized * speed;
            }

            case EnemyBehavior.Lunge:
            {
                // 0 = stalk, 1 = wind up (telegraph), 2 = dash, 3 = recover
                stateTimer -= dt;
                switch (state)
                {
                    case 0:
                        if (dist < 5f && stateTimer <= 0f) { state = 1; stateTimer = 0.55f; lockedDir = dir; }
                        return dir * speed;
                    case 1:
                        flash = Mathf.Max(flash, 0.4f);
                        if (stateTimer <= 0f) { state = 2; stateTimer = 0.35f; }
                        return Vector2.zero;
                    case 2:
                        if (stateTimer <= 0f) { state = 3; stateTimer = 0.6f; }
                        return lockedDir * speed * 5f;
                    default:
                        if (stateTimer <= 0f) { state = 0; stateTimer = 1.4f; }
                        return dir * speed * 0.3f;
                }
            }

            case EnemyBehavior.Caster:
            {
                TickFire(dt, dist, () => Fan(Position, AngleTo(player.Position), Def.Bullets, Def.Spread, Def.BulletSpeed));
                return KeepRange(dir, dist, speed);
            }

            case EnemyBehavior.Blinker:
            {
                stateTimer -= dt;
                if (stateTimer <= 0f && dist < 12f)
                {
                    stateTimer = Random.Range(4f, 6f);
                    Blink(Def.PreferredRange);
                    fireTimer = 0.35f;  // fire shortly after appearing
                }
                TickFire(dt, dist, () => { Ring(Position, Def.Bullets, Def.BulletSpeed, spinAngle); spinAngle += 15f; });
                return KeepRange(dir, dist, speed);
            }

            case EnemyBehavior.BossTreant:
            {
                fireTimer -= dt;
                if (fireTimer <= 0f)
                {
                    fireTimer = Def.FireInterval * (Health < MaxHealth * 0.5f ? 0.7f : 1f);
                    switch (attackIndex++ % 3)
                    {
                        case 0: Ring(Position, Def.Bullets, Def.BulletSpeed, spinAngle += 10f); break;
                        case 1: Summon(GameManager.Instance.Arena.Biome.Roster.Chaff[0], 5); break;
                        default:
                            for (int i = 0; i < 3; i++)
                                Fan(Position, AngleTo(player.Position), 5, 12f, Def.BulletSpeed * (1f + i * 0.25f));
                            break;
                    }
                }
                return dir * speed;
            }

            case EnemyBehavior.BossWitch:
            {
                // Spiral for a while, then aimed fans, then blink somewhere else.
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = (state + 1) % 3;
                    stateTimer = state == 0 ? 4f : state == 1 ? 3f : 0.6f;
                    if (state == 2) Blink(Def.PreferredRange);
                }
                fireTimer -= dt;
                if (fireTimer <= 0f)
                {
                    if (state == 0)
                    {
                        fireTimer = Def.FireInterval;
                        Ring(Position, Def.Bullets, Def.BulletSpeed, spinAngle);
                        spinAngle += 13f;
                    }
                    else if (state == 1)
                    {
                        fireTimer = 0.7f;
                        Fan(Position, AngleTo(player.Position), 7, 10f, 5f);
                    }
                    else fireTimer = 0.2f;
                }
                return state == 2 ? Vector2.zero : KeepRange(dir, dist, speed);
            }
        }
        return Vector2.zero;
    }

    Vector2 KeepRange(Vector2 dir, float dist, float speed)
    {
        float range = Def.PreferredRange;
        if (dist > range + 1f) return dir * speed;
        if (dist < range - 1.5f) return -dir * speed;
        return new Vector2(-dir.y, dir.x) * speed * 0.5f * Mathf.Sign(Mathf.Sin(phase));
    }

    void TickFire(float dt, float dist, Action fire)
    {
        fireTimer -= dt;
        if (fireTimer > 0f) return;
        fireTimer = Def.FireInterval;
        if (dist < 11f) fire();
    }

    void Blink(float range)
    {
        var gm = GameManager.Instance;
        for (int i = 0; i < 8; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector2 p = gm.Player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * range;
            if (!gm.Arena.Inside(p, -1f) || gm.Arena.Blocked(p, Radius)) continue;
            Fx.Burst(Position, Def.Accent, Radius * 3f);
            transform.position = p;
            Fx.Burst(p, Def.Accent, Radius * 3f);
            return;
        }
    }

    void Summon(EnemyDef def, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            Spawn(def, Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Radius + 0.8f), GameManager.Instance.Director.HealthScale);
        }
        Fx.Burst(Position, Def.Accent, Radius * 4f);
    }

    float AngleTo(Vector2 target) => Mathf.Atan2(target.y - Position.y, target.x - Position.x) * Mathf.Rad2Deg;

    void Fan(Vector2 from, float centerDeg, int n, float spread, float speed)
    {
        for (int i = 0; i < n; i++)
            Shoot(from, centerDeg + (i - (n - 1) * 0.5f) * spread, speed);
    }

    void Ring(Vector2 from, int n, float speed, float offset)
    {
        for (int i = 0; i < n; i++)
            Shoot(from, offset + i * 360f / n, speed);
    }

    void Shoot(Vector2 from, float angleDeg, float speed)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        var p = Projectile.Spawn(from, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed, Team.Enemy, Def.Accent, 0.14f);
        p.Damage = Def.BulletDamage;
        p.Lifetime = 6f;
    }

    void UpdateStatus(float dt)
    {
        if (burnTimer > 0f)
        {
            burnTimer -= dt;
            Health -= burnDps * dt;
            if (Health <= 0f) { Die(); return; }
        }
        if (chillTimer > 0f) chillTimer -= dt;
        if (flash > 0f) flash -= dt * 6f;

        Color c = bodyColor;
        if (IsChilled) c = Color.Lerp(c, new Color(0.55f, 0.85f, 1f), 0.6f);
        if (IsBurning) c = Color.Lerp(c, new Color(1f, 0.5f, 0.15f), 0.35f + 0.25f * Mathf.Sin(Time.time * 20f));
        body.color = Color.Lerp(c, Color.white, Mathf.Clamp01(flash));
    }

    public void TakeDamage(float amount, Vector2 push = default)
    {
        if (!IsAlive) return;
        Health -= amount;
        flash = 1f;
        if (!IsBoss) knockback += push;
        if (Health <= 0f) Die();
    }

    public void ApplyBurn(float dps, float duration)
    {
        burnDps = Mathf.Max(burnDps * (burnTimer > 0f ? 1f : 0f), dps);
        burnTimer = Mathf.Max(burnTimer, duration);
    }

    public void ApplyChill(float duration) => chillTimer = Mathf.Max(chillTimer, duration);

    void Die()
    {
        if (!enabled) return;
        Health = 0f;
        enabled = false;
        var gm = GameManager.Instance;
        gm.AddScore(Def.Xp * 10);
        XpGem.Spawn(Position, Def.Xp);
        Fx.Burst(Position, Def.Color, Radius * 2.5f);
        Died?.Invoke(this);
        Destroy(gameObject);
    }
}
