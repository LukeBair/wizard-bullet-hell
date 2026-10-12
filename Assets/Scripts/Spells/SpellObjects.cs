using System;
using UnityEngine;

// A beam of water attached to the wizard. Turns slowly to follow the nearest foe, damaging and
// soaking (Wet) everything along it every tick, and shoving them away.
public class WaterBeam : MonoBehaviour
{
    const float TickEvery = 0.12f;

    Player owner;
    Spell spell;
    float offset, life, length, width, tickTimer;
    Vector2 dir;
    Action<Enemy, Vector2> hit;
    Transform core;

    public static void Create(Spell spell, Player owner, float angleOffset, float duration, float length, float width, Action<Enemy, Vector2> onHit)
    {
        var go = new GameObject("WaterBeam");
        var b = go.AddComponent<WaterBeam>();
        b.spell = spell;
        b.owner = owner;
        b.offset = angleOffset;
        b.life = duration;
        b.length = length;
        b.width = width;
        b.hit = onHit;
        var target = Enemy.Nearest(owner.Position, GameManager.Instance.TargetRange);
        b.dir = Quaternion.Euler(0, 0, angleOffset) * (target != null ? (target.Position - owner.Position).normalized : owner.Facing);
        var c = spell.Color;
        SpriteFactory.Add(go, SpriteFactory.Square, new Color(c.r, c.g, c.b, 0.55f), 72);
        b.core = SpriteFactory.Child(go.transform, "Core", SpriteFactory.Square, new Color(0.85f, 0.95f, 1f, 0.8f), Vector2.zero, 1f, 73).transform;
        b.core.localScale = new Vector3(0.35f, 1f, 1f);
        b.Place();
    }

    void Place()
    {
        Vector2 from = owner.Position;
        transform.position = from + dir * length * 0.5f;
        transform.up = dir;
        float wobble = 1f + 0.1f * Mathf.Sin(Time.time * 40f);
        transform.localScale = new Vector3(width * wobble, length, 1f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        if (owner == null || !owner.IsAlive) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        var target = Enemy.Nearest(owner.Position, gm.TargetRange);
        if (target != null)
        {
            Vector2 want = Quaternion.Euler(0, 0, offset) * (target.Position - owner.Position).normalized;
            dir = Vector3.RotateTowards(dir, want, 120f * Mathf.Deg2Rad * dt, 0f);
        }
        Place();

        tickTimer -= dt;
        if (tickTimer > 0f) return;
        tickTimer = TickEvery;
        Vector2 from = owner.Position;
        foreach (var e in Spell.EnemiesWithin(from + dir * length * 0.5f, length * 0.5f + 0.5f))
        {
            Vector2 rel = e.Position - from;
            float along = Vector2.Dot(rel, dir);
            float across = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
            if (along < 0f || along > length || across > width * 0.5f + e.Radius) continue;
            e.ApplyWet(2.5f);
            hit(e, dir * 5f);
        }
        if (UnityEngine.Random.value < 0.5f) Fx.Burst(from + dir * length, spell.Color, 0.8f);
    }
}

// A whirlwind that pulls foes toward its centre and damages them a few times a second.
public class Vortex : MonoBehaviour
{
    float radius, life, pull, tickTimer;
    Action<Enemy> hit;
    Transform spinA, spinB;

    public static void Create(Vector2 at, float radius, float duration, float pull, Color color, Action<Enemy> onHit)
    {
        var go = new GameObject("Vortex");
        go.transform.position = at;
        var v = go.AddComponent<Vortex>();
        v.radius = radius;
        v.life = duration;
        v.pull = pull;
        v.hit = onHit;
        SpriteFactory.Child(go.transform, "Area", SpriteFactory.Circle, new Color(color.r, color.g, color.b, 0.1f), Vector2.zero, radius * 2f, 2);
        v.spinA = SpriteFactory.Child(go.transform, "SpinA", SpriteFactory.Ring, new Color(color.r, color.g, color.b, 0.6f), Vector2.zero, radius * 1.2f, 74).transform;
        v.spinB = SpriteFactory.Child(go.transform, "SpinB", SpriteFactory.Ring, new Color(color.r, color.g, color.b, 0.4f), new Vector2(0.15f, 0f), radius * 0.6f, 74).transform;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        spinA.Rotate(0f, 0f, 400f * dt);
        spinB.Rotate(0f, 0f, -700f * dt);
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 20f);
        spinA.localScale = Vector3.one * radius * 1.2f * pulse;

        Vector2 c = transform.position;
        foreach (var e in Spell.EnemiesWithin(c, radius))
        {
            Vector2 to = c - e.Position;
            float d = to.magnitude;
            if (d < 0.15f) continue;
            Vector2 swirl = new Vector2(-to.y, to.x) / d * 0.4f;
            e.Pull((to / d + swirl) * pull * (e.IsBoss ? 0.15f : 1f) * Mathf.Clamp01(d / 0.8f), dt);
        }

        tickTimer -= dt;
        if (tickTimer > 0f) return;
        tickTimer = 0.25f;
        foreach (var e in Spell.EnemiesWithin(c, radius * 0.6f)) hit(e);
    }
}
