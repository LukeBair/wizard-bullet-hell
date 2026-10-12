using System.Collections.Generic;
using UnityEngine;

// The five core spells. Each can Evolve twice (tier I -> III).

// I: straight bolts. II: homing. III: strong homing, and every hit launches a follow-up missile.
public class ArcaneMissile : Spell
{
    public override string Name => "Arcane Missile";
    public override Element Element => Element.Arcane;
    public override string Blurb => "Fires bolts at the nearest foes.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 3 ? 14.5f : 12f;
    protected override float BaseCooldown => 0.85f;

    public override string TierName(int tier) => tier == 2 ? "Seeker Bolts" : tier == 3 ? "Arcane Barrage" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Missiles home in on foes." : "Stronger tracking, a little more damage, and each hit fires another missile at a new foe.";

    protected override void Cast()
    {
        Vector2 from = Owner.Position;
        Sfx.Play(Sound.Missile, 0.35f);
        var used = new List<Enemy>();
        for (int i = 0; i < Count; i++)
        {
            var target = Enemy.Nearest(from, Range, used) ?? Enemy.Nearest(from, Range);
            if (target == null) return;
            used.Add(target);
            Vector2 dir = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * 14f) * (target.Position - from);
            Fire(from, dir, true);
        }
    }

    void Fire(Vector2 from, Vector2 dir, bool canSplit)
    {
        var p = Shoot(from, dir, TT("speed", 11f), 0.15f * Area);
        p.Homing = TT("homing", Tier == 1 ? 0f : Tier == 2 ? 180f : 420f);
        p.Lifetime = T("lifetime", 2f);
        p.OnHit = (proj, e) =>
        {
            Hit(e, Damage, proj.Velocity.normalized * 2f);
            if (Tier == 3 && canSplit)
            {
                var next = Enemy.Nearest(e.Position, Range * T("followUpRange", 0.6f), new[] { e });
                if (next != null) Fire(e.Position, next.Position - e.Position, false);
            }
        };
    }
}

// I: small fireball. II: slower, bigger, burns everything it blasts. III: explosions throw out more fireballs.
public class Fireball : Spell
{
    public override string Name => "Fireball";
    public override Element Element => Element.Fire;
    public override string Blurb => "Explodes on impact and sets foes ablaze.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 1 ? 20f : 30f;
    protected override float BaseCooldown => 2.1f;
    protected override float PotencyPerRune => 0.25f;
    float Radius => TT("radius", Tier == 1 ? 1.0f : 1.8f) * Area;
    float Speed => TT("speed", Tier == 1 ? 7.5f : 6f);

    public override string TierName(int tier) => tier == 2 ? "Greater Fireball" : tier == 3 ? "Meteor Cluster" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Slower but much bigger and stronger, and burns everything in the blast."
                  : $"Each fireball's explosion throws out a ring of {T("shrapnelCount_t3", 6f):0} smaller fireballs.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} damage and burn";
            case Rune.Split: return "+1 fireball";
            case Rune.Area: return $"+{Pct(AreaStep)} explosion radius";
            case Rune.Pierce: return "Bursts through +1 enemy";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        Vector2 from = Owner.Position;
        var target = Enemy.Nearest(from, Range);
        if (target == null) return;
        Sfx.Play(Sound.FireballCast, 0.5f);
        for (int i = 0; i < Count; i++)
        {
            Vector2 dir = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * 18f) * (target.Position - from);
            Launch(from, dir, Tier == 3, 2.5f, false);
        }
    }

    // Shrapnel = the smaller fireballs thrown out by a tier III explosion.
    float ShrapnelScale => T("shrapnelRadiusScale_t3", 0.5f);

    void Launch(Vector2 from, Vector2 dir, bool splits, float life, bool shrapnel)
    {
        float scale = shrapnel ? ShrapnelScale : 1f;
        var p = Shoot(from, dir, Speed, (Tier == 1 ? 0.22f : 0.34f) * Mathf.Sqrt(Area) * scale);
        p.Lifetime = life;
        p.OnHit = (proj, e) =>
        {
            if (Tier == 1) e.ApplyBurn(Damage * T("burnFactor", 0.25f), T("burnDuration", 2f));
            Explode(proj.transform.position, splits, scale);
        };
        if (!splits) p.OnExpire = proj => Explode(proj.transform.position, false, scale);   // bursts even on a miss
    }

    void Explode(Vector2 at, bool splits, float scale)
    {
        float radius = Radius * scale;
        Fx.Burst(at, Color, radius * 2.2f);
        Fx.Ring(at, Color, radius);
        Sfx.Play(Sound.Explosion, 0.5f);
        foreach (var e in EnemiesWithin(at, radius))
        {
            if (Tier >= 2) e.ApplyBurn(Damage * TT("burnFactor", 0.3f), TT("burnDuration", 2.5f));
            Hit(e, Damage, (e.Position - at).normalized * 4f);
        }
        if (splits)
        {
            int n = Mathf.RoundToInt(T("shrapnelCount_t3", 6f));
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n * Mathf.Deg2Rad;
                Launch(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), false, T("shrapnelLifetime_t3", 0.6f), true);
            }
        }
    }
}

// I: small pulse. II: full size and chills. III: bigger, stacks frostbite until foes shatter.
public class FrostNova : Spell
{
    public override string Name => "Frost Nova";
    public override Element Element => Element.Frost;
    public override string Blurb => "Freezing pulse around you.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 10f;
    protected override float BaseCooldown => 2.6f;
    protected override float PotencyPerRune => 0.2f;
    protected override float AreaPerRune => 0.15f;
    float Radius => TT("radius", Tier == 1 ? 2.0f : Tier == 2 ? 2.4f : 2.8f) * Area;

    public override string TierName(int tier) => tier == 2 ? "Glacial Nova" : tier == 3 ? "Shatterstorm" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Bigger, and chills foes (slows {Pct(Tune.F("status.chillSlow", 0.5f))})."
                  : $"Bigger. Stacks frostbite on every pulse; at 100% the foe shatters for {Pct(Tune.F("status.shatter", 0.3f))} of its remaining health. Bigger foes resist.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return Tier >= 2 ? $"+{Pct(PotencyStep)} damage and chill time" : $"+{Pct(PotencyStep)} damage";
            case Rune.Split: return "+1 follow-up pulse";
            case Rune.Pierce: return "Pushes foes back harder";
            case Rune.Area: return $"+{Pct(AreaStep)} radius";
            case Rune.Frost: return Tier == 3 ? "Much more frostbite per pulse" : base.RuneEffect(r);
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
        Sfx.Play(Sound.Nova, 0.45f);
        Fx.Ring(at, Color, Radius, 0.4f);
        Fx.Burst(at, new Color(Color.r, Color.g, Color.b, 0.4f), Radius * 2f);
        Particles.Frost(at, Radius);
        float push = T("push", 4f) + T("pushPerPierce", 3f) * RuneCount(Rune.Pierce);
        float frostbite = T("frostbite_t3", 0.35f) + T("frostbitePerFrostRune_t3", 0.25f) * RuneCount(Rune.Frost);
        foreach (var e in EnemiesWithin(at, Radius))
        {
            if (Tier >= 2) e.ApplyChill(TT("chill", 1.8f) * Potency);
            if (Tier == 3) e.AddFrostbite(frostbite);
            Hit(e, Damage, (e.Position - at).normalized * push);
        }
    }
}

// I: one bolt with a small splash. II: chains between foes. III: a heavy strike on the toughest foe, then chains.
public class ChainLightning : Spell
{
    public override string Name => "Chain Lightning";
    public override Element Element => Element.Storm;
    public override string Blurb => "Strikes a foe, splashing those nearby.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 18f;
    protected override float BaseCooldown => 1.5f;
    int Jumps => Mathf.RoundToInt(TT("jumps", 2f)) + PierceCount;

    public override string TierName(int tier) => tier == 2 ? "Arc Lightning" : tier == 3 ? "Stormcaller" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Lightning leaps from foe to foe." : "Calls down a heavy bolt on the toughest foe, then chains from there.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 bolt";
            case Rune.Pierce: return Tier >= 2 ? "Chains to +1 more enemy" : "Bigger splash";
            case Rune.Area: return $"+{Pct(AreaStep)} splash and jump distance";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        Sfx.Play(Sound.Lightning, 0.45f);
        var struck = new List<Enemy>();
        for (int b = 0; b < Count; b++)
        {
            Vector2 from = Owner.Position;
            Enemy target;
            float dmg = Damage;
            if (Tier == 3 && b == 0)
            {
                target = Toughest();
                if (target == null) return;
                // Heavy bolt from the sky.
                Fx.Bolt(target.Position + new Vector2(0.5f, 9f), target.Position, Color.white);
                Fx.Bolt(target.Position + new Vector2(-0.4f, 9f), target.Position, Color);
                Splash(target.Position, T("strikeSplash_t3", 1.6f) * Area, dmg * T("strikeSplashDamage_t3", 1.2f), struck);
                Hit(target, dmg * T("strikeMultiplier_t3", 3f));
                struck.Add(target);
                from = target.Position;
                target = Enemy.Nearest(from, 3.5f * Area, struck);
            }
            else
            {
                target = Enemy.Nearest(from, Range, struck);
                if (target == null) return;
            }

            int jumps = Tier == 1 ? 0 : Jumps;
            for (int j = 0; j <= jumps && target != null; j++)
            {
                Fx.Bolt(from, target.Position, Color);
                struck.Add(target);
                if (j == 0 && Tier < 3) Splash(target.Position, (TT("splash", 1f) + 0.25f * (Tier == 1 ? PierceCount : 0)) * Area, dmg * TT("splashDamage", 0.5f), struck);
                from = target.Position;
                Hit(target, dmg);
                dmg *= T("chainFalloff", 0.9f);
                target = Enemy.Nearest(from, T("jumpRange", 3.5f) * Area, struck);
            }
        }
    }

    void Splash(Vector2 at, float radius, float dmg, List<Enemy> exclude)
    {
        Fx.Ring(at, Color, radius, 0.25f);
        Particles.Crackle(at, radius, Color);
        foreach (var e in EnemiesWithin(at, radius))
            if (!exclude.Contains(e)) Hit(e, dmg);
    }

    Enemy Toughest()
    {
        Enemy best = null;
        float r2 = Range * Range;
        foreach (var e in Enemy.All)
        {
            if (!e.IsAlive || e.Hidden || e.IsCharmed || (e.Position - Owner.Position).sqrMagnitude > r2) continue;
            if (best == null || e.Health > best.Health) best = e;
        }
        return best;
    }
}

// Spirits orbiting the wizard. Echo adds a counter-rotating outer ring.
// II: starts with two rings; 3s without a wisp hit heals you 10%. III: also heals a little on each hit.
public class Wisps : Spell
{
    public override string Name => "Forest Wisps";
    public override Element Element => Element.Nature;
    public override string Blurb => "Spirits circle you, striking what they touch.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 9f;
    protected override float BaseCooldown => 1f;
    protected override int BaseCount => 2;
    protected override float AreaPerRune => 0.15f;
    protected override bool EchoRecasts => false;

    readonly List<(Transform orb, int ring)> orbs = new List<(Transform, int)>();
    readonly List<Transform> tracks = new List<Transform>();
    readonly Dictionary<Enemy, float> lastHit = new Dictionary<Enemy, float>();
    float angle;
    float quietTimer, hitHealTimer;

    int Rings => 1 + Echoes + (Tier >= 2 ? 1 : 0);
    float OrbitRadius(int ring) => T("orbit", 1.7f) * Area * (1f + T("ringSpacing", 0.55f) * ring);
    float OrbRadius => T("wispSize", 0.28f) * Mathf.Sqrt(Area);

    public override string TierName(int tier) => tier == 2 ? "Twin Halo" : tier == 3 ? "Lifebloom Halo" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Starts with two rings. After {T("quietSeconds_t2", 3f):0.#}s without striking anything, the wisps heal you {Pct(T("quietHeal_t2", 0.1f))}."
                  : $"Two rings, and the wisps also heal you {Pct(T("hitHeal_t3", 0.01f))} when they strike (up to 4 times a second).";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 wisp on every ring";
            case Rune.Haste: return $"Wisps orbit {Pct(1f / HasteMultiplier - 1f)} faster";
            case Rune.Pierce: return "Strikes the same foe more often";
            case Rune.Area: return $"+{Pct(AreaStep)} orbit size";
            case Rune.Echo: return "+1 ring spinning the other way";
            default: return base.RuneEffect(r);
        }
    }

    protected override void OnLearned() => Rebuild();
    protected override void OnRunesChanged() => Rebuild();
    protected override bool Ready() => false;   // never "casts"; works in OnTick
    protected override void Cast() { }
    public override float CooldownProgress => Tier >= 2 ? Mathf.Clamp01(quietTimer / 3f) : 1f;

    void Rebuild()
    {
        if (Owner == null) return;
        foreach (var o in orbs) if (o.orb != null) Object.Destroy(o.orb.gameObject);
        foreach (var t in tracks) if (t != null) Object.Destroy(t.gameObject);
        orbs.Clear();
        tracks.Clear();
        for (int ring = 0; ring < Rings; ring++)
        {
            tracks.Add(SpriteFactory.Child(Owner.transform, "WispOrbit", SpriteFactory.Ring, new Color(Color.r, Color.g, Color.b, 0.22f), Vector2.zero, OrbitRadius(ring) * 2f + OrbRadius, 47).transform);
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Wisp");
                go.transform.SetParent(Owner.transform, false);
                SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(Color.r, Color.g, Color.b, 0.5f), Vector2.zero, OrbRadius * 5f, 56);
                SpriteFactory.Child(go.transform, "Halo", SpriteFactory.Ring, Color, Vector2.zero, OrbRadius * 2.4f, 57);
                SpriteFactory.Child(go.transform, "Core", SpriteFactory.Diamond, Color.Lerp(Color, Color.white, 0.4f), Vector2.zero, OrbRadius * 1.4f, 58);
                orbs.Add((go.transform, ring));
            }
        }
    }

    protected override void OnTick(float dt)
    {
        angle += T("spinSpeed", 200f) / Cooldown * dt * Tuning.Pace;
        float hitEvery = T("hitEvery", 0.5f) / (1f + 0.3f * RuneCount(Rune.Pierce));
        bool hitSomething = false;
        for (int i = 0; i < orbs.Count; i++)
        {
            int ring = orbs[i].ring;
            int index = i % Count;
            float dirSign = ring % 2 == 0 ? 1f : -1f;
            float a = (angle * dirSign + index * 360f / Count + ring * 30f) * Mathf.Deg2Rad;
            Vector2 local = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadius(ring);
            orbs[i].orb.localPosition = local;
            Vector2 world = Owner.Position + local;
            foreach (var e in EnemiesWithin(world, OrbRadius))
            {
                if (lastHit.TryGetValue(e, out float t) && Time.time - t < hitEvery) continue;
                lastHit[e] = Time.time;
                hitSomething = true;
                Fx.Burst(e.Position, Color, 1f);
                Sfx.Play(Sound.WispHit, 0.35f, 0.2f);
                Fx.Ring(world, Color, OrbRadius * 1.5f, 0.2f);
                Hit(e, Damage, local.normalized * 3f);
            }
        }
        if (lastHit.Count > 200) lastHit.Clear();

        // Healing from evolved wisps.
        if (Tier >= 2)
        {
            quietTimer = hitSomething ? 0f : quietTimer + dt;
            if (quietTimer >= T("quietSeconds_t2", 3f))
            {
                quietTimer = 0f;
                HealOwner(T("quietHeal_t2", 0.1f));
            }
        }
        hitHealTimer -= dt;
        if (Tier == 3 && hitSomething && hitHealTimer <= 0f)
        {
            hitHealTimer = 0.25f;
            HealOwner(T("hitHeal_t3", 0.01f));
        }
    }

    void HealOwner(float fraction)
    {
        if (Owner.Health >= Owner.MaxHealth) return;
        Owner.Heal(Owner.MaxHealth * fraction);
        if (fraction >= 0.05f)
        {
            Fx.Burst(Owner.Position, Color, 2.5f);
            DamageText.Spawn(Owner.Position + Vector2.up * 0.6f, Owner.MaxHealth * fraction, new Color(0.5f, 1f, 0.5f));
        }
    }
}
