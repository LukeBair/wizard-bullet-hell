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
        Sfx.Play("spells.ArcaneMissile.cast", Sound.Missile, 0.35f);
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
        SpellVfx.Skin(p, Element, 1.6f * Area);
        p.Homing = TT("homing", Tier == 1 ? 0f : Tier == 2 ? 180f : 420f);
        p.Lifetime = T("lifetime", 2f);
        p.OnHit = (proj, e) =>
        {
            SpellVfx.Impact(proj.Position, Element, 0.7f * Area);
            Hit(e, Damage, proj.Velocity.normalized * 2f);
            Grimoire.Feat(SpellId.ArcaneMissile);
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
        Sfx.Play("spells.Fireball.cast", Sound.FireballCast, 0.5f);
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
            Explode(proj.Position, splits, scale);
        };
        if (!splits) p.OnExpire = proj => Explode(proj.Position, false, scale);   // bursts even on a miss
    }

    void Explode(Vector2 at, bool splits, float scale)
    {
        float radius = Radius * scale;
        Fx.Burst(at, Color, radius * 2.2f);
        Fx.Ring(at, Color, radius);
        Sfx.Play("spells.Fireball.explode", Sound.Explosion, 0.5f);
        var caught = EnemiesWithin(at, radius);
        foreach (var e in caught)
        {
            if (Tier >= 2) e.ApplyBurn(Damage * TT("burnFactor", 0.3f), TT("burnDuration", 2.5f));
            Hit(e, Damage, (e.Position - at).normalized * 4f);
        }
        Grimoire.FeatMax(SpellId.Fireball, caught.Count);
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

// I: small pulse. II: full size and chills. III: bigger, stacks frostbite until foes shatter; shatters spread frostbite.
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
                  : $"Bigger. Stacks frostbite on every pulse; at 100% the foe shatters for {Pct(Tune.F("status.shatter", 0.3f))} of its remaining health and gives foes near it {Pct(T("shatterSpread_t3", 0.5f))} frostbite, so shatters chain. Bigger foes resist.";

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
        Sfx.Play("spells.FrostNova.pulse", Sound.Nova, 0.45f);
        Fx.Ring(at, Color, Radius, 0.4f);
        Fx.Burst(at, new Color(Color.r, Color.g, Color.b, 0.4f), Radius * 2f);
        Particles.Frost(at, Radius);
        float push = T("push", 4f) + T("pushPerPierce", 3f) * RuneCount(Rune.Pierce);
        float frostbite = T("frostbite_t3", 0.35f) + T("frostbitePerFrostRune_t3", 0.25f) * RuneCount(Rune.Frost);
        foreach (var e in EnemiesWithin(at, Radius))
        {
            if (Tier >= 2) e.ApplyChill(TT("chill", 1.8f) * Potency);
            if (Tier == 3) e.AddFrostbite(frostbite, T("shatterSpread_t3", 0.5f), T("shatterSpreadRadius_t3", 1.8f));
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
        Sfx.Play("spells.ChainLightning.cast", Sound.Lightning, 0.45f);
        var struck = new List<Enemy>();
        Bolts(struck);
        Grimoire.FeatMax(SpellId.ChainLightning, struck.Count);   // foes the lightning passed through this cast
    }

    void Bolts(List<Enemy> struck)
    {
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

// Spirits orbiting the wizard. Echo adds a counter-rotating set of wisps on the same ring.
// II: starts with two rings; 3s without a wisp hit heals you 10%. III: every kill adds a temporary wisp to the inner ring.
public class Wisps : Spell, IOrbiter
{
    public override string Name => "Forest Wisps";
    public override Element Element => Element.Nature;
    public override string Blurb => "Spirits circle you and dart out at nearby foes. A foe's element changes the strike: burning foes make the wisp explode.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 9f;
    protected override float BaseCooldown => 1f;
    protected override int BaseCount => 2;
    protected override float AreaPerRune => 0.15f;
    protected override bool EchoRecasts => false;
    protected override bool Overloadable => false;

    readonly List<(Transform orb, int ring, int set, int index, int ringCount)> orbs = new List<(Transform, int, int, int, int)>();
    readonly List<float> bonusWisps = new List<float>();   // tier III: time left on each extra wisp
    readonly List<Transform> tracks = new List<Transform>();
    readonly Dictionary<Enemy, float> lastHit = new Dictionary<Enemy, float>();
    float angle;
    float quietTimer;
    int MaxBonusWisps => Mathf.RoundToInt(T("maxBonusWisps", 4f));

    // Hunting: each wisp darts at a nearby foe every so often, strikes it and flies back to its place
    // in the orbit. What the foe is suffering from changes the strike (Strike). State is keyed by the
    // wisp's slot so it survives Rebuild (bonus wisps come and go).
    enum Mode { Orbit, Dart, Return }
    // Tier II+: a wisp whose strike set off a reaction absorbs that element (absorbed, a reaction bit) for a
    // few hits or seconds, and its strikes then always do that reaction. tint is the element its sprites show.
    class WispState { public Mode mode; public Enemy target; public Vector2 pos; public float cool, dead, absorbTime; public int absorbed, absorbHits, tint = -1; }
    readonly Dictionary<(int, int, int), WispState> states = new Dictionary<(int, int, int), WispState>();
    readonly List<Enemy> claimed = new List<Enemy>();
    int reactionsSeen;   // bitmask of the five reactions triggered this run (signature feat)
    // Tier III kill credit: the wisp striking right now. Bonus wisps never earn more wisps (no runaway),
    // and a kill by a wisp holding an element spawns a bonus wisp holding the same element.
    bool strikerBonus;
    int strikerElement;
    readonly List<int> spawns = new List<int>();

    void Credit(Enemy x)
    {
        if (Tier == 3 && !x.IsAlive && !strikerBonus) spawns.Add(strikerElement);
    }
    float HuntEvery => T("huntEvery", 2.5f) * Cooldown;
    float HuntRange => T("huntRange", 4.5f) * (1f + 0.5f * (Area - 1f));
    int RingCount(int ring) => Count + (ring == 0 ? bonusWisps.Count : 0);

    public int OrbitRings => 1;
    int Sets => 1 + Echoes;
    float OrbitRadius(int ring) => Orbits.Radius(this, ring);
    float OrbRadius => T("wispSize", 0.28f) * Area;

    public override string TierName(int tier) => tier == 2 ? "Twin Halo" : tier == 3 ? "Lifebloom Halo" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"+2 wisps. A wisp that sets off a reaction absorbs that element for {T("absorbHits", 3f):0} strikes or {T("absorbTime", 5f):0.#}s and repeats the reaction on any foe. After {T("quietSeconds_t2", 3f):0.#}s without striking, the wisps heal you {Pct(T("quietHeal_t2", 0.1f))}."
                  : $"Twin Halo, and every foe a wisp kills adds a wisp for {T("bonusWispLife", 6f):0.#}s (up to {MaxBonusWisps} extra), holding the killer's absorbed element. Added wisps' kills don't add more.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 wisp in every set";
            case Rune.Haste: return $"Wisps orbit and hunt {Pct(HastePerRune)} faster";
            case Rune.Pierce: return "Strikes the same foe more often while orbiting";
            case Rune.Area: return $"+{Pct(AreaStep)} wisp size, hunt range and blast size";
            case Rune.Echo: return "+1 set of wisps spinning the other way";
            case Rune.Ward: return "No effect: this spell never casts";
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
        foreach (var st in states.Values) st.tint = -1;   // new sprites start in the spell's own color
        orbs.Clear();
        tracks.Clear();
        for (int ring = 0; ring < OrbitRings; ring++)
        {
            tracks.Add(SpriteFactory.Child(Owner.transform, "WispOrbit", SpriteFactory.Ring, new Color(Color.r, Color.g, Color.b, 0.22f), Vector2.zero, OrbitRadius(ring) * 2f + OrbRadius, 47).transform);
            int n = RingCount(ring);
            for (int set = 0; set < Sets; set++)
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Wisp");
                go.transform.SetParent(Owner.transform, false);
                SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(Color.r, Color.g, Color.b, 0.5f), Vector2.zero, OrbRadius * 5f, 56);
                SpriteFactory.Child(go.transform, "Halo", SpriteFactory.Ring, Color, Vector2.zero, OrbRadius * 2.4f, 57);
                SpriteFactory.Child(go.transform, "Core", SpriteFactory.Diamond, Color.Lerp(Color, Color.white, 0.4f), Vector2.zero, OrbRadius * 1.4f, 58);
                orbs.Add((go.transform, ring, set, i, n));
            }
        }
    }

    protected override void OnTick(float dt)
    {
        angle += T("spinSpeed", 200f) / Cooldown * dt * Tuning.Pace;
        // Rings shift outward when another orbiting spell is learned first, so keep the tracks in step.
        for (int r = 0; r < tracks.Count; r++) tracks[r].localScale = Vector3.one * (OrbitRadius(r) * 2f + OrbRadius);
        float hitEvery = T("hitEvery", 0.5f) / (1f + 0.3f * RuneCount(Rune.Pierce));
        bool hitSomething = false;
        spawns.Clear();
        claimed.Clear();
        for (int i = 0; i < orbs.Count; i++)
        {
            var (orb, ring, set, index, ringCount) = orbs[i];
            float a = Orbits.Angle(angle, ring, set, Sets, index, ringCount) * Mathf.Deg2Rad;
            Vector2 local = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadius(ring);
            Vector2 world = Owner.Position + local;

            var key = (ring, set, index);
            if (!states.TryGetValue(key, out var st))
                states[key] = st = new WispState { cool = HuntEvery * (0.3f + 0.7f * (index + set * 0.5f) / Mathf.Max(1, ringCount)) };

            // A wisp that exploded is gone until it re-forms.
            if (st.dead > 0f)
            {
                st.dead -= dt;
                if (orb.gameObject.activeSelf) orb.gameObject.SetActive(false);
                if (st.dead > 0f) continue;
                orb.gameObject.SetActive(true);
                Fx.Ring(world, Color, OrbRadius * 2f, 0.3f);
                st.mode = Mode.Orbit;
            }

            if (st.absorbed != 0)
            {
                st.absorbTime -= dt;
                if (st.absorbTime <= 0f || st.absorbHits <= 0) st.absorbed = 0;
            }
            if (st.tint != st.absorbed) Tint(orb, st.tint = st.absorbed);

            strikerBonus = ring == 0 && index >= Count;
            strikerElement = st.absorbed;
            if (st.mode != Mode.Orbit && Hunt(st, world, dt, ref hitSomething))
            {
                orb.localPosition = st.pos - Owner.Position;
                continue;
            }

            st.pos = world;
            orb.localPosition = local;
            st.cool -= dt;
            if (st.cool <= 0f)
            {
                var prey = Enemy.Nearest(Owner.Position, HuntRange, claimed);
                if (prey != null) { claimed.Add(prey); st.target = prey; st.mode = Mode.Dart; }
                else st.cool = 0.25f;   // nothing in reach: look again shortly
            }

            foreach (var e in EnemiesWithin(world, OrbRadius))
            {
                if (lastHit.TryGetValue(e, out float t) && Time.time - t < hitEvery) continue;
                lastHit[e] = Time.time;
                hitSomething = true;
                Fx.Burst(e.Position, Color, 1f);
                Sfx.Play("spells.Wisps.orbit", Sound.WispHit, 0.35f, 0.2f);
                Fx.Ring(world, Color, OrbRadius * 1.5f, 0.2f);
                Hit(e, Damage, local.normalized * 3f);
                Credit(e);
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
        // Tier III: kills add short-lived wisps to the inner ring.
        int before = bonusWisps.Count;
        for (int i = bonusWisps.Count - 1; i >= 0; i--)
        {
            bonusWisps[i] -= dt;
            if (bonusWisps[i] <= 0f) bonusWisps.RemoveAt(i);
        }
        // Slots past the bonus wisps left are gone: drop their state so a new wisp there starts fresh.
        var stale = new List<(int, int, int)>();
        foreach (var k in states.Keys) if (k.Item1 == 0 && k.Item3 >= Count + bonusWisps.Count) stale.Add(k);
        foreach (var k in stale) states.Remove(k);
        foreach (int element in spawns)
        {
            int slot;
            if (bonusWisps.Count < MaxBonusWisps) { slot = bonusWisps.Count; bonusWisps.Add(T("bonusWispLife", 6f)); }
            else { bonusWisps.Sort(); slot = 0; bonusWisps[0] = T("bonusWispLife", 6f); }   // refresh the oldest
            for (int set = 0; set < Sets; set++)
                states[(0, set, Count + slot)] = new WispState
                {
                    cool = 0.2f, absorbed = element,
                    absorbHits = element != 0 ? Mathf.RoundToInt(T("absorbHits", 3f)) : 0,
                    absorbTime = element != 0 ? T("absorbTime", 5f) : 0f,
                };
        }
        if (spawns.Count > 0 || bonusWisps.Count != before) Rebuild();
    }

    // Moves a darting or returning wisp. Returns false once it is back in its orbit slot.
    bool Hunt(WispState st, Vector2 home, float dt, ref bool hitSomething)
    {
        float step = T("huntSpeed", 11f) * Tuning.Pace * dt;
        if (st.mode == Mode.Dart)
        {
            if (st.target == null || !st.target.IsAlive || st.target.Hidden) { st.mode = Mode.Return; return true; }
            claimed.Add(st.target);
            Vector2 to = st.target.Position - st.pos;
            float reach = st.target.Radius + OrbRadius;
            if (to.magnitude > reach + step) { st.pos += to.normalized * step; return true; }
            hitSomething = true;
            bool spent = Strike(st, st.target, st.pos, to.normalized);
            st.target = null;
            st.cool = HuntEvery;
            if (spent) { st.dead = T("respawnTime", 4f); st.mode = Mode.Orbit; return true; }
            st.mode = Mode.Return;
            return true;
        }
        // Returning: fly back to wherever its orbit slot is now.
        Vector2 back = home - st.pos;
        if (back.magnitude <= step + 0.05f) { st.mode = Mode.Orbit; return false; }
        st.pos += back.normalized * step * 1.3f;
        return true;
    }

    // A darting wisp hits its target. The foe's strongest status picks the reaction:
    // burning: the wisp explodes (big damage around it) and has to re-form; wet: lightning arcs to
    // other wet foes; chilled: heavy frostbite; infested: a weakened foe is devoured outright (its bugs burst);
    // trapped (rooted): it drains life back to you. Returns true if the wisp was used up.
    bool Strike(WispState st, Enemy e, Vector2 at, Vector2 dir)
    {
        bool burning = e.IsBurning, wet = e.IsWet, chilled = e.IsChilled, infested = e.IsInfested, rooted = e.IsRooted;
        // An absorbed element forces its reaction whatever the foe is suffering from, and nothing else.
        int forced = st.absorbed;
        if (forced != 0)
        {
            burning = forced == 1; wet = forced == 2; chilled = forced == 4; infested = forced == 8; rooted = forced == 16;
            st.absorbHits--;
        }
        Sfx.Play("spells.Wisps.dart", Sound.WispHit, 0.45f, 0.2f);
        Fx.Burst(at, Color, 1.2f);
        Hit(e, Damage * T("huntDamage", 1.5f), dir * 3f);
        Credit(e);
        float power = Damage / Mathf.Max(0.01f, TT("damage", BaseDamage));   // potency, sunlit, overload
        int reaction = burning ? 1 : wet ? 2 : chilled ? 4 : infested ? 8 : rooted ? 16 : 0;
        if (Tier >= 2 && reaction != 0 && forced == 0)
        {
            st.absorbed = reaction;
            st.absorbHits = Mathf.RoundToInt(T("absorbHits", 3f));
            st.absorbTime = T("absorbTime", 5f);
        }
        if (reaction != 0 && (reactionsSeen & reaction) == 0)
        {
            reactionsSeen |= reaction;
            int n = 0;
            for (int bits = reactionsSeen; bits != 0; bits >>= 1) n += bits & 1;
            Grimoire.FeatMax(SpellId.Wisps, n);
        }

        if (burning)
        {
            float radius = T("flareRadius", 1.6f) * Area;
            Fx.Burst(at, new Color(1f, 0.55f, 0.2f), radius * 2.5f);
            Fx.Ring(at, new Color(1f, 0.7f, 0.3f), radius, 0.4f);
            Sfx.Play("spells.Wisps.fireBurst", Sound.Explosion, 0.5f, 0.2f);
            foreach (var x in EnemiesWithin(at, radius))
            {
                Hit(x, T("flareDamage", 40f) * power, (x.Position - at).normalized * 5f);
                Credit(x);
            }
            return st.absorbed == 0;   // a wisp holding fire keeps flying; otherwise it burns out and re-forms
        }
        if (wet)
        {
            int arcs = Mathf.RoundToInt(T("arcTargets", 3f));
            Vector2 from = e.Position;
            foreach (var x in EnemiesWithin(from, T("arcRange", 3.5f)))
            {
                if (arcs <= 0) break;
                if (x == e || (!x.IsWet && forced == 0)) continue;   // absorbed water arcs to any foe
                Fx.Bolt(from, x.Position, new Color(1f, 0.95f, 0.5f));
                Hit(x, Damage * T("arcDamage", 1.5f));
                Credit(x);
                arcs--;
            }
            Sfx.Play("spells.Wisps.stormBurst", Sound.Lightning, 0.35f, 0.3f);
            return false;
        }
        if (chilled)
        {
            if (e.IsAlive) e.AddFrostbite(T("frostbite", 0.5f));
            Particles.Frost(at, 1f);
            return false;
        }
        if (infested)
        {
            // Devour: a weakened infested foe is eaten outright, so its bugs burst on the crowd.
            if (e.IsAlive && !e.IsBoss && e.Health <= e.MaxHealth * T("devourBelow", 0.35f))
            {
                Particles.Ants(at, 0.8f, false);
                Hit(e, e.Health + 1f, dir * 3f);
                Credit(e);
            }
            return false;
        }
        if (rooted) HealOwner(T("drainHeal", 0.02f));
        return false;
    }

    // Element colors for absorbed wisps (reaction bits), or the spell's own color for 0.
    Color ElementColor(int reaction) =>
        reaction == 1 ? new Color(1f, 0.55f, 0.2f) : reaction == 2 ? new Color(0.35f, 0.6f, 1f) : reaction == 4 ? new Color(0.7f, 0.95f, 1f)
        : reaction == 8 ? new Color(0.75f, 0.85f, 0.25f) : reaction == 16 ? new Color(0.55f, 0.4f, 0.2f) : Color;

    void Tint(Transform orb, int reaction)
    {
        Color c = ElementColor(reaction);
        foreach (var r in orb.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Color want = r.name == "Core" ? Color.Lerp(c, Color.white, 0.4f) : c;
            r.color = new Color(want.r, want.g, want.b, r.color.a);
        }
        if (reaction != 0) Fx.Ring(orb.position, c, OrbRadius * 2f, 0.25f);
    }

    void HealOwner(float fraction)
    {
        if (Owner.Health >= Owner.MaxHealth) return;
        Owner.Heal(Owner.MaxHealth * fraction, source: Id.ToString());
        if (fraction >= 0.05f)
        {
            Fx.Burst(Owner.Position, Color, 2.5f);
            DamageText.Spawn(Owner.Position + Vector2.up * 0.6f, Owner.MaxHealth * fraction, new Color(0.5f, 1f, 0.5f));
        }
    }
}
