using System;
using System.Collections.Generic;
using UnityEngine;

// A beam of water attached to the wizard. Turns slowly to follow the nearest foe, damaging and
// soaking (Wet) everything along it every tick, and shoving them away. With a cone angle it sprays
// a wide wedge instead (evolved Tidal Lance).
public class WaterBeam : MonoBehaviour
{
    const float TickEvery = 0.12f;

    Player owner;
    Spell spell;
    float offset, life, length, width, tickTimer, coneHalf, push;
    Vector2 dir;
    Action<Enemy, Vector2> hit;
    Transform core;

    public static void Create(Spell spell, Player owner, float angleOffset, float duration, float length, float width, float coneHalfAngle, float push, Action<Enemy, Vector2> onHit)
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
        b.coneHalf = coneHalfAngle;
        b.push = push;
        var target = Enemy.Nearest(owner.Position, GameManager.Instance.TargetRange);
        b.dir = Quaternion.Euler(0, 0, angleOffset) * (target != null ? (target.Position - owner.Position).normalized : owner.Facing);
        var c = spell.Color;
        if (coneHalfAngle > 0f)
        {
            // Triangle sprite points up; flipped so its tip sits at the wizard.
            var tri = SpriteFactory.Child(go.transform, "Cone", SpriteFactory.Triangle, new Color(c.r, c.g, c.b, 0.45f), Vector2.zero, 1f, 72);
            tri.transform.localRotation = Quaternion.Euler(0, 0, 180f);
            b.core = SpriteFactory.Child(go.transform, "Core", SpriteFactory.Triangle, new Color(0.85f, 0.95f, 1f, 0.45f), new Vector2(0f, 0.1f), 1f, 73).transform;
            b.core.localRotation = Quaternion.Euler(0, 0, 180f);
            b.core.localScale = new Vector3(0.45f, 0.8f, 1f);
        }
        else
        {
            SpriteFactory.Add(go, SpriteFactory.Square, new Color(c.r, c.g, c.b, 0.55f), 72);
            b.core = SpriteFactory.Child(go.transform, "Core", SpriteFactory.Square, new Color(0.85f, 0.95f, 1f, 0.8f), Vector2.zero, 1f, 73).transform;
            b.core.localScale = new Vector3(0.35f, 1f, 1f);
        }
        b.Place();
    }

    void Place()
    {
        Vector2 from = owner.Position;
        transform.position = from + dir * length * 0.5f;
        transform.up = dir;
        float wobble = 1f + 0.1f * Mathf.Sin(Time.time * 40f);
        float w = coneHalf > 0f ? 2f * length * Mathf.Tan(coneHalf * Mathf.Deg2Rad) : width;
        transform.localScale = new Vector3(w * wobble, length, 1f);
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
            if (along < 0f || along > length) continue;
            float halfWidth = coneHalf > 0f ? along * Mathf.Tan(coneHalf * Mathf.Deg2Rad) : width * 0.5f;
            if (across > halfWidth + e.Radius) continue;
            e.ApplyWet(2.5f);
            hit(e, (coneHalf > 0f ? rel.normalized : dir) * push);
        }
        if (UnityEngine.Random.value < 0.5f) Fx.Burst(from + dir * length, spell.Color, 0.8f);
    }
}

// A whirlwind that pulls foes toward its centre and damages them a few times a second.
public class Vortex : MonoBehaviour
{
    float radius, life, pull, tickTimer, tickEvery;
    Vector2 velocity;
    Action<Enemy> hit;
    Action<Vector2, List<Enemy>> swirl;
    Transform spinA, spinB;

    // onSwirl (optional) gets every foe in the eye once per tick, before the hits (Cyclone passes
    // conditions between them; swirling first lets a foe just dragged in catch what the others carry,
    // instead of the hit handing every foe the same conditions first).
    public static void Create(Vector2 at, float radius, float duration, float pull, Color color, Action<Enemy> onHit, Vector2 velocity = default, float tickEvery = 0.25f, Action<Vector2, List<Enemy>> onSwirl = null)
    {
        var go = new GameObject("Vortex");
        go.transform.position = at;
        var v = go.AddComponent<Vortex>();
        v.radius = radius;
        v.life = duration;
        v.pull = pull;
        v.hit = onHit;
        v.velocity = velocity;
        v.tickEvery = tickEvery;
        v.swirl = onSwirl;
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

        if (velocity != Vector2.zero)
            transform.position = gm.Arena.PushOut((Vector2)transform.position + velocity * dt * Tuning.Pace, 0.5f);
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
        tickTimer = tickEvery;
        var eye = Spell.EnemiesWithin(c, radius * 0.6f);
        if (swirl != null && eye.Count > 1) swirl(c, eye);
        foreach (var e in eye) hit(e);
    }
}

// A column of water that erupts under foes (Monsoon): soaks, hurts and throws foes outward for a
// couple of seconds.
public class WaterPillar : MonoBehaviour
{
    float radius, life, maxLife, tickTimer;
    Action<Enemy, Vector2> hit;
    SpriteRenderer column;
    Color color;

    float wet;

    public static void Create(Vector2 at, float radius, float duration, Color color, Action<Enemy, Vector2> onHit, float wetTime = 2.5f)
    {
        var go = new GameObject("WaterPillar");
        go.transform.position = at;
        var w = go.AddComponent<WaterPillar>();
        w.radius = radius;
        w.life = w.maxLife = duration;
        w.hit = onHit;
        w.wet = wetTime;
        w.color = color;
        SpriteFactory.Child(go.transform, "Edge", SpriteFactory.Ring, new Color(color.r, color.g, color.b, 0.8f), Vector2.zero, radius * 2f, 74);
        w.column = SpriteFactory.Child(go.transform, "Column", SpriteFactory.Circle, new Color(0.7f, 0.9f, 1f, 0.55f), Vector2.zero, radius * 1.7f, 75);
        Fx.Burst(at, color, radius * 3f);
        Sfx.Play("spells.TidalLance.pillar", Sound.Explosion, 0.35f, 0.3f);
    }

    void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 25f);
        column.transform.localScale = Vector3.one * radius * 1.7f * pulse;
        column.color = new Color(0.7f, 0.9f, 1f, 0.55f * Mathf.Clamp01(life / 0.3f));

        tickTimer -= dt;
        if (tickTimer > 0f) return;
        tickTimer = 0.25f;
        Vector2 c = transform.position;
        foreach (var e in Spell.EnemiesWithin(c, radius))
        {
            e.ApplyWet(wet);
            hit(e, (e.Position - c).normalized * 6f);
        }
        if (UnityEngine.Random.value < 0.6f) Fx.Burst(c + UnityEngine.Random.insideUnitCircle * radius * 0.6f, color, 0.8f);
    }
}

// One stone spike (Stone Spikes). Erupts once, hurting and pinning foes on top of it. As a wall
// (Stonewall) it stays up: enemy bullets that touch it break, and foes are held outside it.
public class Spike : MonoBehaviour
{
    float radius, life;
    bool wall;
    float healthLeft;   // walls break after absorbing this much bullet damage
    Action<Enemy, Vector2> onErupt;
    Action<Vector2> onCrumble;
    Transform body;
    static readonly List<Projectile> scratch = new List<Projectile>();

    // A boss that walks into a wall takes the spike's hit and smashes it.
    public static void Create(Vector2 at, float radius, float duration, Color color, bool wall, Action<Enemy, Vector2> onErupt, Action<Vector2> onCrumble, float wallHealth = 0f)
    {
        var go = new GameObject("Spike");
        go.transform.position = at;
        go.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 0;   // depth-sort with trees so it can stand behind one
        var s = go.AddComponent<Spike>();
        s.radius = radius;
        s.life = duration;
        s.wall = wall;
        s.onCrumble = onCrumble;
        s.onErupt = onErupt;
        s.healthLeft = wallHealth > 0f ? wallHealth : float.MaxValue;
        SpriteFactory.Child(go.transform, "Base", SpriteFactory.Circle, new Color(0.25f, 0.18f, 0.1f, 0.5f), Vector2.zero, radius * 2.2f, 40);
        s.body = SpriteFactory.Child(go.transform, "Body", SpriteFactory.Triangle, color, new Vector2(0f, radius * 0.4f), radius * 2.2f, 62).transform;
        SpriteFactory.Child(s.body, "Tip", SpriteFactory.Triangle, Color.Lerp(color, Color.white, 0.4f), new Vector2(0f, 0.15f), 0.45f, 63);
        Fx.Burst(at, color, radius * 2.5f);
        foreach (var e in Spell.EnemiesWithin(at, radius)) onErupt(e, at);
    }

    void Crumble()
    {
        Fx.Burst(transform.position, new Color(0.6f, 0.45f, 0.3f), radius * 2f);
        onCrumble?.Invoke(transform.position);
        Destroy(gameObject);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Crumble(); return; }
        // Plain spikes sink away as they expire; walls stay at full height.
        if (healthLeft < 30f && UnityEngine.Random.value < 0.1f) Fx.Burst(transform.position, new Color(0.6f, 0.45f, 0.3f), radius);   // cracking
        body.localScale = Vector3.one * radius * 2.2f * Mathf.Min(1f, 0.4f + (wall ? 1f : 0f) + life * 4f);
        if (!wall) return;

        Vector2 c = transform.position;
        scratch.Clear();
        foreach (var p in Projectile.All) if (p.Team == Team.Enemy && p.enabled) scratch.Add(p);
        foreach (var p in scratch)
        {
            float r = radius + p.Radius;
            if ((p.Position - c).sqrMagnitude > r * r) continue;
            Fx.Burst(p.Position, new Color(0.8f, 0.65f, 0.45f), 0.6f);
            Grimoire.Feat(SpellId.StoneSpikes, p.Damage);
            healthLeft -= p.Damage;
            p.Release();
            if (healthLeft <= 0f) { Crumble(); return; }
        }
        foreach (var e in Spell.EnemiesWithin(c, radius))
        {
            if (e.IsBoss)
            {
                onErupt(e, c);
                Crumble();
                return;
            }
            Vector2 away = e.Position - c;
            float d = Mathf.Max(0.05f, away.magnitude);
            e.Pull(away / d * Mathf.Max(0f, radius + e.Radius - d) / Mathf.Max(dt, 0.001f), dt);
        }
    }
}

// A boomerang blade (Spectral Blades): flies out from the wizard, then back to wherever the wizard
// is now, hitting each foe once per pass.
public class Blade : MonoBehaviour
{
    Player owner;
    Vector2 dir;
    float reach, speed, size, travelled;
    int tripsLeft;
    bool returning;
    Action<Enemy, Vector2> hit;
    readonly HashSet<Enemy> struck = new HashSet<Enemy>();
    readonly HashSet<Enemy> everHit = new HashSet<Enemy>();   // across the whole throw, for the tier III feat
    Transform spin;

    public static void Create(Player owner, Vector2 dir, float reach, float speed, float size, int trips, Color color, float alpha, Action<Enemy, Vector2> onHit)
    {
        var go = new GameObject("Blade");
        go.transform.position = owner.Position;
        go.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 0;   // depth-sort with trees
        var b = go.AddComponent<Blade>();
        b.owner = owner;
        b.dir = dir.normalized;
        b.reach = reach;
        b.speed = speed;
        b.size = size;
        b.tripsLeft = trips;
        b.hit = onHit;
        SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(color.r, color.g, color.b, 0.4f * alpha), Vector2.zero, size * 4f, 69);
        b.spin = new GameObject("Spin").transform;
        b.spin.SetParent(go.transform, false);
        SpriteFactory.Child(b.spin, "EdgeA", SpriteFactory.Diamond, new Color(color.r, color.g, color.b, alpha), Vector2.zero, size * 2.4f, 71).transform.localScale = new Vector3(size * 0.9f, size * 2.6f, 1f);
        SpriteFactory.Child(b.spin, "EdgeB", SpriteFactory.Diamond, new Color(1f, 0.85f, 0.95f, alpha), Vector2.zero, size * 2.4f, 72).transform.localScale = new Vector3(size * 2.6f, size * 0.9f, 1f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        if (owner == null || !owner.IsAlive) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        spin.Rotate(0f, 0f, 900f * dt);
        Vector2 pos = transform.position;
        float step = speed * dt * Tuning.Pace;

        if (!returning)
        {
            pos += dir * step;
            travelled += step;
            if (travelled >= reach) { returning = true; struck.Clear(); }
        }
        else
        {
            Vector2 to = owner.Position - pos;
            if (to.magnitude <= step + 0.2f)
            {
                if (--tripsLeft <= 0) { Destroy(gameObject); return; }
                returning = false;
                travelled = 0f;
                struck.Clear();
                var target = Enemy.Nearest(owner.Position, gm.TargetRange);
                if (target != null) dir = (target.Position - owner.Position).normalized;
            }
            pos += to.normalized * Mathf.Min(step, to.magnitude);
        }
        transform.position = pos;

        foreach (var e in Spell.EnemiesWithin(pos, size))
        {
            if (!struck.Add(e)) continue;
            if (everHit.Add(e)) Grimoire.FeatMax(SpellId.SpectralBlades, everHit.Count);
            Particles.Hit(e.Position, new Color(1f, 0.5f, 0.8f), dir);
            hit(e, (returning ? -dir : dir) * 2f);
        }
    }
}

// A pool of light (Sunspots). The wizard standing in it heals and becomes Sunlit. Evolved pools burn
// foes inside them; at tier III, standing in one long enough makes it erupt in a Sunburst.
public class SunPool : MonoBehaviour
{
    Sunspots spell;
    Player owner;
    float radius, life, maxLife, heal, burnTimer, basked;
    SpriteRenderer fill, edge;

    public static void Create(Sunspots spell, Player owner, Vector2 at, float radius, float duration, float heal, Color color)
    {
        var go = new GameObject("SunPool");
        go.transform.position = at;
        var p = go.AddComponent<SunPool>();
        p.spell = spell;
        p.owner = owner;
        p.radius = radius;
        p.life = p.maxLife = duration;
        p.heal = heal;
        p.fill = SpriteFactory.Child(go.transform, "Fill", SpriteFactory.Glow, new Color(1f, 0.9f, 0.55f, 0.4f), Vector2.zero, radius * 2.6f, 2);
        p.edge = SpriteFactory.Child(go.transform, "Edge", SpriteFactory.Ring, new Color(color.r, color.g, color.b, 0.8f), Vector2.zero, radius * 2f, 3);
        Fx.Ring(at, color, radius, 0.4f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        if (owner == null || !owner.IsAlive) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        Vector2 c = transform.position;
        float fade = Mathf.Clamp01(life / 0.5f);
        bool inside = (owner.Position - c).sqrMagnitude < radius * radius;
        float glow = inside ? 0.6f + 0.1f * Mathf.Sin(Time.time * 8f) : 0.4f;
        fill.color = new Color(1f, 0.9f, 0.55f, glow * fade);
        edge.transform.localScale = Vector3.one * radius * 2f * (1f + 0.04f * Mathf.Sin(Time.time * 5f));

        if (inside)
        {
            basked += dt;
            bool burst = spell.CanBurst && basked >= spell.BurstAfter;
            spell.Bask(heal, dt, burst);
            if (burst)
            {
                spell.Sunburst(c);
                Destroy(gameObject);
                return;
            }
        }

        if (!spell.BurnsFoes) return;
        burnTimer -= dt;
        if (burnTimer > 0f) return;
        burnTimer = 0.5f;
        foreach (var e in Spell.EnemiesWithin(c, radius)) spell.Burn(e);
    }
}

// A patch of burning ground (Comet Trail). Hurts foes standing in it every tickEvery seconds, and
// can burst when it burns out (onExpire).
public class FlamePatch : MonoBehaviour
{
    float radius, life, maxLife, tickEvery, tickTimer;
    Action<Enemy> hit;
    Action<Vector2> onExpire;
    SpriteRenderer fill;
    Color color;

    public static void Create(Vector2 at, float radius, float duration, float tickEvery, Color color, Action<Enemy> onHit, Action<Vector2> onExpire)
    {
        var go = new GameObject("FlamePatch");
        go.transform.position = at;
        var p = go.AddComponent<FlamePatch>();
        p.radius = radius;
        p.life = p.maxLife = duration;
        p.tickEvery = tickEvery;
        p.hit = onHit;
        p.onExpire = onExpire;
        p.color = color;
        p.fill = SpriteFactory.Child(go.transform, "Fill", SpriteFactory.Glow, new Color(color.r, color.g, color.b, 0.55f), Vector2.zero, radius * 2.6f, 2);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        life -= dt;
        Vector2 c = transform.position;
        if (life <= 0f)
        {
            onExpire?.Invoke(c);
            Destroy(gameObject);
            return;
        }
        float fade = Mathf.Clamp01(life / (maxLife * 0.4f));
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f + c.x * 3f);
        fill.color = new Color(color.r, color.g, color.b, 0.55f * fade * flicker);
        fill.transform.localScale = Vector3.one * radius * 2.6f * (0.8f + 0.2f * fade);

        tickTimer -= dt;
        if (tickTimer > 0f) return;
        tickTimer = tickEvery;
        foreach (var e in Spell.EnemiesWithin(c, radius)) hit(e);
    }
}
