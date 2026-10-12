using System.Collections.Generic;
using UnityEngine;

// Homing bolts at the nearest enemies. The starter spell.
public class ArcaneMissile : Spell
{
    public override string Name => "Arcane Missile";
    public override Element Element => Element.Arcane;
    public override string Blurb => "Homing bolts seek the nearest foes.";
    protected override float BaseDamage => 12f;
    protected override float BaseCooldown => 0.85f;

    protected override void Cast()
    {
        Vector2 from = Owner.Position;
        var used = new List<Enemy>();
        for (int i = 0; i < Count; i++)
        {
            var target = Enemy.Nearest(from, 10f, used) ?? Enemy.Nearest(from, 10f);
            if (target == null) return;
            used.Add(target);
            Vector2 dir = target.Position - from;
            float spread = (i - (Count - 1) * 0.5f) * 14f;
            dir = Quaternion.Euler(0, 0, spread) * dir;
            var p = Shoot(from, dir, 11f, 0.15f * Area);
            p.Homing = 360f;
            p.Lifetime = 2f;
            p.OnHit = (proj, e) => Hit(e, Damage, proj.Velocity.normalized * 2f);
        }
    }
}

// Slow fireball that explodes on impact and leaves enemies burning.
public class Fireball : Spell
{
    public override string Name => "Fireball";
    public override Element Element => Element.Fire;
    public override string Blurb => "Explodes on impact and sets foes ablaze.";
    protected override float BaseDamage => 24f;
    protected override float BaseCooldown => 2.1f;
    float Radius => 1.4f * Area;

    public override string RuneEffect(Rune r) =>
        r == Rune.Area ? "+30% explosion radius" : r == Rune.Pierce ? "Bursts through +1 enemy" : base.RuneEffect(r);

    protected override void Cast()
    {
        Vector2 from = Owner.Position;
        var target = Enemy.Nearest(from, 10f);
        if (target == null) return;
        for (int i = 0; i < Count; i++)
        {
            Vector2 dir = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * 18f) * (target.Position - from);
            var p = Shoot(from, dir, 7.5f, 0.28f * Mathf.Sqrt(Area));
            p.Lifetime = 2.5f;
            p.OnHit = (proj, e) => Explode(proj.transform.position);
        }
    }

    void Explode(Vector2 at)
    {
        Fx.Burst(at, Color, Radius * 2.2f);
        Fx.Ring(at, Color, Radius);
        foreach (var e in EnemiesWithin(at, Radius))
        {
            e.ApplyBurn(Damage * 0.25f, 2f);
            Hit(e, Damage, (e.Position - at).normalized * 4f);
        }
    }
}

// Pulse of cold around the wizard that damages and slows everything nearby.
public class FrostNova : Spell
{
    public override string Name => "Frost Nova";
    public override Element Element => Element.Frost;
    public override string Blurb => "Freezing pulse around you that slows foes.";
    protected override float BaseDamage => 10f;
    protected override float BaseCooldown => 2.6f;
    float Radius => 2.4f * Area;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 follow-up pulse";
            case Rune.Pierce: return "Pushes foes back harder";
            case Rune.Area: return "+30% radius";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => Enemy.Nearest(Owner.Position, Radius + 0.5f) != null;

    protected override void Cast()
    {
        Pulse();
        for (int i = 1; i < Count; i++) Owner.StartCoroutine(Delayed(0.3f * i));
    }

    System.Collections.IEnumerator Delayed(float t)
    {
        yield return new WaitForSeconds(t);
        Pulse();
    }

    void Pulse()
    {
        Vector2 at = Owner.Position;
        Fx.Ring(at, Color, Radius, 0.4f);
        Fx.Burst(at, new Color(Color.r, Color.g, Color.b, 0.4f), Radius * 2f);
        float push = 4f + 3f * RuneCount(Rune.Pierce);
        foreach (var e in EnemiesWithin(at, Radius))
        {
            e.ApplyChill(1.8f);
            Hit(e, Damage, (e.Position - at).normalized * push);
        }
    }
}

// Instant bolt that arcs between nearby enemies.
public class ChainLightning : Spell
{
    public override string Name => "Chain Lightning";
    public override Element Element => Element.Storm;
    public override string Blurb => "Lightning leaps from foe to foe.";
    protected override float BaseDamage => 16f;
    protected override float BaseCooldown => 1.5f;
    int Jumps => 2 + PierceCount;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 bolt";
            case Rune.Pierce: return "Chains to +1 more enemy";
            case Rune.Area: return "+30% range and jump distance";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => Enemy.Nearest(Owner.Position, 7f * Area) != null;

    protected override void Cast()
    {
        var struck = new List<Enemy>();
        for (int b = 0; b < Count; b++)
        {
            Vector2 from = Owner.Position;
            var target = Enemy.Nearest(from, 7f * Area, struck);
            float dmg = Damage;
            for (int j = 0; j <= Jumps && target != null; j++)
            {
                Fx.Bolt(from, target.Position, Color);
                struck.Add(target);
                if (Fusions.Has(Fusion.Overload)) target.ApplyBurn(Damage * 0.5f, 2f);
                from = target.Position;
                Hit(target, dmg);
                dmg *= 0.9f;
                target = Enemy.Nearest(from, 3.5f * Area, struck);
            }
        }
    }
}

// Spirits that orbit the wizard and hurt anything they touch. Always active.
public class Wisps : Spell
{
    public override string Name => "Forest Wisps";
    public override Element Element => Element.Nature;
    public override string Blurb => "Spirits circle you, striking what they touch.";
    protected override float BaseDamage => 9f;
    protected override float BaseCooldown => 1f;
    protected override int BaseCount => 2;

    readonly List<Transform> orbs = new List<Transform>();
    readonly Dictionary<Enemy, float> lastHit = new Dictionary<Enemy, float>();
    float angle;

    int OrbCount => Count + 2 * Echoes;
    float OrbitRadius => 1.7f * Area;
    float OrbRadius => 0.28f * Area;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 wisp";
            case Rune.Haste: return "Wisps orbit 15% faster";
            case Rune.Pierce: return "Strikes the same foe more often";
            case Rune.Area: return "+30% orbit and wisp size";
            case Rune.Echo: return "+2 wisps";
            default: return base.RuneEffect(r);
        }
    }

    protected override void OnLearned() => Rebuild();
    protected override void OnRunesChanged() => Rebuild();
    protected override bool Ready() => false;   // never "casts"; works in OnTick
    protected override void Cast() { }

    void Rebuild()
    {
        if (Owner == null) return;
        foreach (var o in orbs) if (o != null) Object.Destroy(o.gameObject);
        orbs.Clear();
        for (int i = 0; i < OrbCount; i++)
        {
            var go = new GameObject("Wisp");
            go.transform.SetParent(Owner.transform, false);
            SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(Color.r, Color.g, Color.b, 0.5f), Vector2.zero, OrbRadius * 5f, 56);
            SpriteFactory.Child(go.transform, "Halo", SpriteFactory.Ring, Color, Vector2.zero, OrbRadius * 2.4f, 57);
            SpriteFactory.Child(go.transform, "Core", SpriteFactory.Diamond, Color.Lerp(Color, Color.white, 0.4f), Vector2.zero, OrbRadius * 1.4f, 58);
            orbs.Add(go.transform);
        }
    }

    protected override void OnTick(float dt)
    {
        angle += 200f / Cooldown * dt;
        float hitEvery = 0.5f / (1f + 0.3f * RuneCount(Rune.Pierce));
        for (int i = 0; i < orbs.Count; i++)
        {
            float a = (angle + i * 360f / orbs.Count) * Mathf.Deg2Rad;
            Vector2 local = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadius;
            orbs[i].localPosition = local;
            Vector2 world = Owner.Position + local;
            foreach (var e in EnemiesWithin(world, OrbRadius))
            {
                if (lastHit.TryGetValue(e, out float t) && Time.time - t < hitEvery) continue;
                lastHit[e] = Time.time;
                Hit(e, Damage, local.normalized * 3f);
            }
        }
        if (lastHit.Count > 200) lastHit.Clear();
    }
}
