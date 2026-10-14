using System.Collections.Generic;
using UnityEngine;

// Curved mirror shards orbit the wizard and throw enemy bullets back as bolts of light.
// II: reflected bolts home in on foes. III: each reflected bolt splits into three.
public class MirrorWard : Spell, IOrbiter
{
    public override string Name => "Mirror Ward";
    public override Element Element => Element.Light;
    public override string Blurb => "A mirror shard circles you, reflecting enemy bullets back at foes. Harder bullets reflect harder.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 12f;   // per reflected bolt
    protected override float BaseCooldown => 1f;  // only sets the spin speed (Haste spins faster)
    protected override float AreaPerRune => 0.2f;
    protected override bool EchoRecasts => false;
    protected override bool Overloadable => false;

    readonly List<(Transform shard, int set, int index)> shards = new List<(Transform, int, int)>();
    readonly List<Transform> pieces = new List<Transform>();   // every shard's segments, laid along the ring
    Transform track;
    float laidRadius;
    static readonly List<Projectile> scratch = new List<Projectile>();
    float angle;

    public int OrbitRings => 1;
    int Sets => 1 + Echoes;
    float OrbitRadius => Orbits.Radius(this, 0);
    float ShardLength => T("shardLength", 1f) * Area;        // along the ring
    float ShardThickness => T("shardThickness", 0.16f) * Area;
    const int Segments = 6;

    public override string TierName(int tier) => tier == 2 ? "Prism" : tier == 3 ? "Hall of Mirrors" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Reflected bolts home in on foes." : $"Homing, and each reflected bolt splits into {T("splitCount_t3", 3f):0}.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} reflected damage";
            case Rune.Haste: return $"Shards spin {Pct(HastePerRune)} faster";
            case Rune.Split: return "+1 shard in every set";
            case Rune.Pierce: return "Reflected bolts pass through +1 enemy";
            case Rune.Area: return $"+{Pct(AreaStep)} shard size";
            case Rune.Echo: return "+1 set of shards spinning the other way";
            case Rune.Ward: return "No effect: this spell never casts";
            default: return base.RuneEffect(r);
        }
    }

    protected override void OnLearned() => Rebuild();
    protected override void OnRunesChanged() => Rebuild();
    protected override bool Ready() => false;   // never casts; works in OnTick
    protected override void Cast() { }

    void Rebuild()
    {
        if (Owner == null) return;
        foreach (var s in shards) if (s.shard != null) Object.Destroy(s.shard.gameObject);
        if (track != null) Object.Destroy(track.gameObject);
        shards.Clear();
        pieces.Clear();
        track = SpriteFactory.Child(Owner.transform, "MirrorOrbit", SpriteFactory.Ring, new Color(Color.r, Color.g, Color.b, 0.15f), Vector2.zero, OrbitRadius * 2f + ShardThickness, 47).transform;
        Color light = Color.Lerp(Color, Color.white, 0.6f);
        for (int set = 0; set < Sets; set++)
        {
            for (int i = 0; i < Count; i++)
            {
                // The shard pivots on the wizard; its segments sit on the ring and bend with it.
                var go = new GameObject("MirrorShard");
                go.transform.SetParent(Owner.transform, false);
                for (int k = 0; k < Segments; k++)
                {
                    var piece = new GameObject("Piece").transform;
                    piece.SetParent(go.transform, false);
                    SpriteFactory.Child(piece, "Glow", SpriteFactory.Glow, new Color(Color.r, Color.g, Color.b, 0.2f), Vector2.zero, 1f, 56).transform.localScale = new Vector3(1.4f, 3f, 1f);
                    SpriteFactory.Child(piece, "Bar", SpriteFactory.Square, light, Vector2.zero, 1f, 58);
                    pieces.Add(piece);
                }
                shards.Add((go.transform, set, i));
            }
        }
        laidRadius = 0f;   // lay the segments on the next tick
    }

    // Place each shard's segments along an arc of the ring, tangent to it.
    void LaySegments(float radius)
    {
        laidRadius = radius;
        float span = ShardLength / radius;   // radians
        float step = span / Segments;
        var size = new Vector3(step * radius * 1.15f, ShardThickness, 1f);   // slight overlap so the bar reads as one piece
        for (int n = 0; n < pieces.Count; n++)
        {
            float phi = -span * 0.5f + step * (n % Segments + 0.5f);
            pieces[n].localPosition = new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * radius;
            pieces[n].localRotation = Quaternion.Euler(0, 0, phi * Mathf.Rad2Deg + 90f);
            pieces[n].localScale = size;
        }
    }

    protected override void OnTick(float dt)
    {
        angle += T("spinSpeed", 160f) / Cooldown * dt * Tuning.Pace;
        // The ring shifts outward when another orbiting spell is learned first, so keep the track in step.
        float radius = OrbitRadius;
        if (track != null) track.localScale = Vector3.one * (radius * 2f + ShardThickness);
        if (!Mathf.Approximately(radius, laidRadius)) LaySegments(radius);
        float halfSpan = ShardLength / radius * 0.5f;
        scratch.Clear();
        foreach (var p in Projectile.All) if (p.Team == Team.Enemy && p.enabled) scratch.Add(p);

        foreach (var s in shards)
        {
            var shard = s.shard;
            float a = Orbits.Angle(angle, 0, s.set, Sets, s.index, Count) * Mathf.Deg2Rad;
            shard.localPosition = Vector3.zero;
            shard.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);

            // A bullet touches the shard when it is on the ring band and within the shard's arc.
            foreach (var p in scratch)
            {
                if (!p.enabled) continue;
                Vector2 at = p.Position;
                Vector2 d = at - Owner.Position;
                float dist = d.magnitude;
                if (Mathf.Abs(dist - radius) > ShardThickness * 0.5f + p.Radius) continue;
                float off = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (off > halfSpan + p.Radius / Mathf.Max(0.01f, dist)) continue;
                Reflect(p, at);
            }
        }
    }

    void Reflect(Projectile bullet, Vector2 at)
    {
        Vector2 back = -bullet.Velocity;
        // Bolts hit harder off harder bullets, so the ward keeps up late and against bosses.
        float damage = Damage + bullet.Damage * T("reflectScale", 2f) * Potency;
        bullet.Release();   // leaves Projectile.All at once, so no other shard takes it
        Fx.Burst(at, Color, 1f);
        Sfx.Play(Sound.WispHit, 0.3f, 0.3f);
        Grimoire.Feat(SpellId.MirrorWard);

        var target = Enemy.Nearest(at, Range);
        Vector2 dir = target != null ? target.Position - at : back;
        int n = Tier == 3 ? Mathf.RoundToInt(T("splitCount_t3", 3f)) : 1;
        for (int i = 0; i < n; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (n - 1) * 0.5f) * T("splitSpread_t3", 20f)) * dir;
            var p = Shoot(at, d, T("boltSpeed", 10f), 0.14f);
            p.Homing = Tier >= 2 ? TT("homing", 300f) : 0f;
            p.Lifetime = 1.5f;
            p.OnHit = (proj, e) => Hit(e, damage, proj.Velocity.normalized * 2f);
        }
    }
}

// A line of stone spikes erupts toward the nearest foe, pinning what it hits for a moment.
// II: spikes stay up as a wall that blocks enemy bullets and holds foes back. III: crumbling walls burst into shards.
public class StoneSpikes : Spell
{
    public override string Name => "Stone Spikes";
    public override Element Element => Element.Earth;
    public override string Blurb => "A line of spikes erupts toward foes, pinning them in place.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 14f;
    protected override float BaseCooldown => 3f;
    protected override float PotencyPerRune => 0.25f;

    int SpikesPerLine => Mathf.RoundToInt(T("spikes", 5f)) + Mathf.RoundToInt(T("spikesPerPierce", 2f)) * PierceCount;
    float SpikeRadius => T("spikeRadius", 0.45f) * Area;
    float WallHealth => T("wallHealth", 80f) * Potency;

    public override string TierName(int tier) => tier == 2 ? "Stonewall" : tier == 3 ? "Rockburst" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Spikes stay up for {T("wallTime_t2", 3f):0.#}s as a wall that blocks enemy bullets and holds foes back. Each spike breaks after absorbing {WallHealth:0} bullet damage, and bosses smash through."
                  : $"Walls, and each spike bursts into {T("shards_t3", 4f):0} stone shards when the wall crumbles.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return Tier >= 2 ? $"+{Pct(PotencyStep)} damage, pin time and wall toughness" : $"+{Pct(PotencyStep)} damage and pin time";
            case Rune.Split: return "+1 line of spikes";
            case Rune.Pierce: return $"+{T("spikesPerPierce", 2f):0} spikes per line";
            case Rune.Area: return $"+{Pct(AreaStep)} spike size";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var target = Enemy.Nearest(Owner.Position, Range);
        if (target == null) return;
        Sfx.Play(Sound.Burrow, 0.5f, 0.2f);
        Vector2 from = Owner.Position;
        Vector2 dir = (target.Position - from).normalized;
        int spikes = SpikesPerLine;
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * T("fanAngle", 25f)) * dir;
            Owner.StartCoroutine(Line(from, d, spikes));
        }
    }

    System.Collections.IEnumerator Line(Vector2 from, Vector2 dir, int spikes)
    {
        float spacing = T("spacing", 0.8f) * Mathf.Sqrt(Area);
        float wall = Tier >= 2 ? TT("wallTime", 3f) : T("spikeTime", 0.4f);
        for (int i = 1; i <= spikes; i++)
        {
            Vector2 at = from + dir * spacing * i;
            if (!GameManager.Instance.Arena.Inside(at)) yield break;
            Spike.Create(at, SpikeRadius, wall, Color, Tier >= 2, Erupt, Tier == 3 ? new System.Action<Vector2>(Crumble) : null, WallHealth);
            yield return new WaitForSeconds(T("stagger", 0.05f));
        }
    }

    void Erupt(Enemy e, Vector2 at)
    {
        e.Root(T("pinTime", 0.3f) * Potency, 0f);
        Hit(e, Damage);   // no knockback: the foe stays pinned where the spike caught it
    }

    // Rockburst: a crumbling spike throws stone shards in a ring.
    void Crumble(Vector2 at)
    {
        if (Owner == null) return;
        int n = Mathf.RoundToInt(T("shards_t3", 4f));
        float offset = Random.value * 360f;
        for (int i = 0; i < n; i++)
        {
            float a = (offset + i * 360f / n) * Mathf.Deg2Rad;
            var p = Shoot(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), T("shardSpeed_t3", 8f), 0.12f);
            p.Lifetime = T("shardLife_t3", 0.5f);
            p.OnHit = (proj, e) => Hit(e, Damage * T("shardDamage_t3", 0.5f), proj.Velocity.normalized * 2f);
        }
    }
}

// Spectral blades fly out and boomerang back, cutting every foe they pass, both ways.
// II: blades fly out a second time. III: an afterimage of each blade follows 0.4s behind for half damage.
public class SpectralBlades : Spell
{
    public override string Name => "Spectral Blades";
    public override Element Element => Element.Shadow;
    public override string Blurb => "Throws blades that boomerang back, cutting foes both ways.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 8.5f;
    protected override float BaseCooldown => 2f;
    protected override int BaseCount => 2;

    float Reach => T("reach", 5f) * Area;
    float Speed => T("speed", 9f) * (1f + T("speedPerPierce", 0.3f) * RuneCount(Rune.Pierce));
    float BladeSize => T("bladeSize", 0.35f) * Mathf.Sqrt(Area);

    public override string TierName(int tier) => tier == 2 ? "Reaper's Arc" : tier == 3 ? "Thousand Cuts" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Blades fly out a second time after they return."
                  : $"Two throws, and an afterimage of each blade follows {T("afterimageDelay_t3", 0.4f):0.#}s behind for {Pct(T("afterimageDamage_t3", 0.5f))} damage.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 blade";
            case Rune.Pierce: return $"Blades fly {Pct(T("speedPerPierce", 0.3f))} faster";
            case Rune.Area: return $"+{Pct(AreaStep)} reach and blade size";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var target = Enemy.Nearest(Owner.Position, Range);
        if (target == null) return;
        Sfx.Play(Sound.Teleport, 0.35f, 0.3f);
        Vector2 dir = (target.Position - Owner.Position).normalized;
        int trips = Tier >= 2 ? 2 : 1;
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * T("spread", 30f)) * dir;
            Blade.Create(Owner, d, Reach, Speed, BladeSize, trips, Color, 1f, (e, push) => Hit(e, Damage, push));
            if (Tier == 3) Owner.StartCoroutine(Afterimage(d, trips));
        }
    }

    System.Collections.IEnumerator Afterimage(Vector2 dir, int trips)
    {
        yield return new WaitForSeconds(T("afterimageDelay_t3", 0.4f));
        if (Owner == null || !Owner.IsAlive) yield break;
        float share = T("afterimageDamage_t3", 0.5f);
        Blade.Create(Owner, dir, Reach, Speed, BladeSize, trips, Color, 0.4f, (e, push) => Hit(e, Damage * share, push));
    }
}

// Pools of light appear near the wizard. Standing in one heals and makes you Sunlit:
// faster, quicker casting and harder hitting for a few seconds after you step out.
// II: pools also burn foes inside them, and Sunlit is twice as strong.
// III: after a few seconds inside, the pool erupts in a Sunburst and Sunlit lasts twice as long.
public class Sunspots : Spell
{
    public override string Name => "Sunspots";
    public override Element Element => Element.Light;
    public override string Blurb => $"Pools of light appear near you. Stand in one to heal and become Sunlit: +{Pct(Tune.F("spells.Sunspots.moveBuff", 0.2f))} speed, +{Pct(Tune.F("spells.Sunspots.castBuff", 0.15f))} cast speed and +{Pct(Tune.F("spells.Sunspots.damageBuff", 0.2f))} damage.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 0.02f;   // share of max health healed per second in a pool
    protected override float BaseCooldown => 8f;
    protected override float PotencyPerRune => 0.25f;
    protected override bool EchoRecasts => false;
    protected override bool HitsFoes => Tier >= 2;

    float Radius => T("radius", 1.2f) * Area;
    float PoolTime => T("poolTime", 6f) * (1f + T("poolTimePerEcho", 0.5f) * RuneCount(Rune.Echo));
    float SunlitTime => (T("sunlitTime", 4f) + T("sunlitPerPierce", 1f) * RuneCount(Rune.Pierce));
    float SunlitPower => Potency * (Tier >= 2 ? T("powerMultiplier_t2", 2f) : 1f);

    public override string TierName(int tier) => tier == 2 ? "Solar Flare" : tier == 3 ? "Zenith" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Pools burn foes inside them, and Sunlit is {T("powerMultiplier_t2", 2f):0.#}x as strong."
                  : $"Solar Flare, and after {T("burstAfter_t3", 2.5f):0.#}s in a pool it erupts in a Sunburst ({T("burstDamage_t3", 80f):0} damage) and leaves you Sunlit twice as long.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} healing and Sunlit strength";
            case Rune.Haste: return $"Pools appear {Pct(HastePerRune)} more often";
            case Rune.Split: return "+1 pool";
            case Rune.Pierce: return $"Sunlit lasts {T("sunlitPerPierce", 1f):0.#}s longer";
            case Rune.Area: return $"+{Pct(AreaStep)} pool size";
            case Rune.Echo: return $"Pools last {Pct(T("poolTimePerEcho", 0.5f))} longer";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th pool heals {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} more";
            default:
                if (IsStatusRune(r) && Tier < 2) return "No effect until Solar Flare (only its burn hits foes)";
                return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => true;

    protected override void Cast()
    {
        var arena = GameManager.Instance.Arena;
        float baseAngle = Random.value * 360f;
        float heal = Damage;   // read now, so an Overloaded cast keeps its bonus for the pool's whole life
        for (int i = 0; i < Count; i++)
        {
            float a = (baseAngle + i * 360f / Count) * Mathf.Deg2Rad;
            float dist = Random.Range(T("minDistance", 2f), T("maxDistance", 3f));
            Vector2 at = arena.PushOut(Owner.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist, Radius);
            SunPool.Create(this, Owner, at, Radius, PoolTime, heal, Color);
        }
        Sfx.Play(Sound.Gem, 0.4f, 0.2f);
    }

    // Called by a pool every frame the wizard stands in it.
    public void Bask(float heal, float dt, bool burst)
    {
        Owner.Heal(Owner.MaxHealth * heal * dt, false, Id.ToString());
        Owner.Sunlight(SunlitPower, SunlitTime * (burst ? 2f : 1f));
    }

    public bool BurnsFoes => Tier >= 2;
    public bool CanBurst => Tier == 3;
    public float BurstAfter => T("burstAfter_t3", 2.5f);

    public void Burn(Enemy e) => Hit(e, T("burnDamage_t2", 8f) * Potency * (Owner != null ? Owner.DamageMultiplier : 1f));

    public void Sunburst(Vector2 at)
    {
        float radius = Radius * T("burstRadius_t3", 2f);
        Fx.Ring(at, Color, radius, 0.5f);
        Fx.Burst(at, Color.white, radius * 2f);
        Sfx.Play(Sound.Explosion, 0.6f, 0.1f);
        GameManager.Instance.Shake(0.15f);
        foreach (var e in EnemiesWithin(at, radius))
            Hit(e, T("burstDamage_t3", 80f) * Potency * (Owner != null ? Owner.DamageMultiplier : 1f), (e.Position - at).normalized * 6f);
    }
}

// While you move, you leave a trail of flame patches that burn foes standing in them. Standing
// still leaves nothing. II (Afterburner): a flat move speed buff and a wider trail.
// III (Phoenix Path): each patch bursts when it burns out.
public class CometTrail : Spell
{
    public override string Name => "Comet Trail";
    public override Element Element => Element.Fire;
    public override string Blurb => "While you move, you leave a trail of fire that burns foes standing in it.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 3f;      // per tick
    protected override float BaseCooldown => 0.15f; // a patch can drop this often (if you've moved far enough)
    protected override bool CastAnimates => false;

    Vector2 lastDrop;
    bool hasDropped;
    readonly Dictionary<Enemy, float> burned = new Dictionary<Enemy, float>();   // trail-burned foes, for the feat
    float featTimer;

    float Spacing => T("spacing", 0.6f);
    float PatchRadius => T("patchRadius", 0.45f) * Area * (Tier >= 2 ? 1f + T("widen_t2", 0.3f) : 1f);
    float PatchLife => T("patchLife", 1.5f) * (1f + T("lifePerPierce", 0.3f) * PierceCount);
    float TickEvery => T("tickEvery", 0.5f) * HasteFactor;

    public override string TierName(int tier) => tier == 2 ? "Afterburner" : tier == 3 ? "Phoenix Path" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"+{Pct(T("moveBuff_t2", 0.08f))} move speed, and the trail is {Pct(T("widen_t2", 0.3f))} wider."
                  : $"Afterburner, and each patch bursts when it burns out for {T("burstDamage_t3", 1.5f):0.#}x damage.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 parallel trail";
            case Rune.Pierce: return $"Patches last {Pct(T("lifePerPierce", 0.3f))} longer";
            case Rune.Haste: return $"Patches burn {Pct(HastePerRune)} faster";
            case Rune.Area: return $"+{Pct(AreaStep)} trail width";
            case Rune.Echo: return "Drops a second patch a moment later";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th patch is {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} stronger";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => !hasDropped || (Owner.Position - lastDrop).sqrMagnitude >= Spacing * Spacing;

    protected override void Cast()
    {
        Vector2 at = Owner.Position;
        Vector2 dir = hasDropped && (at - lastDrop).sqrMagnitude > 0.0001f ? (at - lastDrop).normalized : Vector2.up;
        Vector2 side = new Vector2(-dir.y, dir.x);
        lastDrop = at;
        hasDropped = true;
        float damage = Damage;   // fixed at drop time, so an Overloaded patch stays strong
        for (int i = 0; i < Count; i++)
        {
            Vector2 p = at + side * (i - (Count - 1) * 0.5f) * T("splitGap", 0.8f);
            FlamePatch.Create(p, PatchRadius, PatchLife, TickEvery, Color, e => Burn(e, damage), Tier == 3 ? new System.Action<Vector2>(c => Burst(c, damage)) : null);
        }
    }

    void Burn(Enemy e, float damage)
    {
        e.ApplyBurn(damage * T("burnShare", 0.5f), T("burnTime", 1.5f));
        Hit(e, damage);
        if (e.IsAlive) burned[e] = Time.time;
    }

    // Phoenix Path: a patch bursting as it burns out.
    void Burst(Vector2 at, float damage)
    {
        if (Owner == null) return;
        float r = T("burstRadius_t3", 0.8f) * Area;
        Fx.Burst(at, Color, r * 2f);
        foreach (var e in EnemiesWithin(at, r)) Hit(e, damage * T("burstDamage_t3", 1.5f));
    }

    protected override void OnTick(float dt)
    {
        if (Owner == null) return;
        Owner.BonusMove = Tier >= 2 ? T("moveBuff_t2", 0.08f) : 0f;

        // Feat: foes burning from the trail at the same moment.
        featTimer -= dt;
        if (featTimer > 0f || burned.Count == 0) return;
        featTimer = 0.25f;
        int n = 0;
        var gone = new List<Enemy>();
        foreach (var kv in burned)
        {
            if (kv.Key == null || !kv.Key.IsAlive || Time.time - kv.Value > 3f) gone.Add(kv.Key);
            else if (kv.Key.IsBurning) n++;
        }
        foreach (var e in gone) burned.Remove(e);
        Grimoire.FeatMax(SpellId.CometTrail, n);
    }
}

// Banishes the most dangerous non-boss foe nearby for a few seconds; it comes back where it was,
// briefly dazed, and the spell's status runes land on it as it returns. Bosses can't be banished
// and are dazed instead. II (Exile): every foe in a small circle around the target goes too.
// III (Oblivion): chaff never come back (no bug burst), and others return missing max health.
public class Banish : Spell
{
    public override string Name => "Banish";
    public override Element Element => Element.Shadow;
    public override string Blurb => $"Banishes the most dangerous foe nearby for {T("duration", 4f):0.#}s. Bosses are dazed instead.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 6f;   // only feeds status runes and Oblivion
    protected override float BaseCooldown => 6f;
    protected override float PotencyPerRune => 0.1f;

    float Duration => (T("duration", 4f) * Potency + T("durationPerPierce", 1f) * PierceCount) * (Overloaded ? T("overloadDuration", 2f) : 1f);
    // Area grows the Exile circle at a reduced rate so it doesn't swallow the screen.
    float ExileRadius => T("exileRadius_t2", 0.9f) * (1f + T("areaScale", 0.5f) * (Area - 1f));

    public override string TierName(int tier) => tier == 2 ? "Exile" : tier == 3 ? "Oblivion" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Banishes every foe within {T("exileRadius_t2", 0.9f):0.#} of the target too."
                  : $"Exile, and banished chaff never come back. Others return missing {Pct(T("oblivionLoss_t3", 0.25f))} of their max health.";

    public override string RuneEffect(Rune r)
    {
        if ((r == Rune.Static || r == Rune.Siphon) && Tier < 3) return "No effect until Oblivion (only its return hit deals damage)";
        if (IsStatusRune(r) && r != Rune.Midas && r != Rune.Static && r != Rune.Siphon) return base.RuneEffect(r).Replace("Hits", "Banished foes").Replace("Foes it hits", "Banished foes") + " (as they return)";
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} banish time";
            case Rune.Split: return "+1 foe banished";
            case Rune.Pierce: return $"+{T("durationPerPierce", 1f):0.#}s banish time";
            case Rune.Area: return Tier >= 2 ? $"+{Pct(AreaStep * T("areaScale", 0.5f))} Exile circle" : "No effect until Exile";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th cast banishes for {T("overloadDuration", 2f):0.#}x as long";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var targets = new List<Enemy>();
        for (int i = 0; i < Count; i++)
        {
            var t = Pick(targets);
            if (t == null) break;
            targets.Add(t);
        }
        if (targets.Count == 0)
        {
            // Only bosses around: daze the nearest one.
            var boss = Enemy.Nearest(Owner.Position, Range);
            if (boss != null && boss.IsBoss) { boss.Daze(T("bossDaze", 1f)); Fx.Ring(boss.Position, Color, boss.Radius * 1.5f, 0.3f); }
            return;
        }
        Sfx.Play(Sound.Teleport, 0.4f, 0.3f);
        float duration = Duration;
        foreach (var t in targets)
        {
            Fx.Ring(t.Position, Color, Tier >= 2 ? ExileRadius : t.Radius * 1.5f, 0.35f);
            if (Tier >= 2) foreach (var x in EnemiesWithin(t.Position, ExileRadius)) Send(x, duration);
            else Send(t, duration);
        }
        Grimoire.FeatMax(SpellId.Banish, Enemy.BanishedCount());
    }

    void Send(Enemy e, float duration) => e.Banish(duration, Tier == 3 && e.Def.Tier == EnemyTier.Chaff, Returned);

    void Returned(Enemy e)
    {
        if (!e.IsAlive) { Killed(e); return; }   // Oblivion erased it
        if (Tier == 3) Hit(e, e.MaxHealth * T("oblivionLoss_t3", 0.25f));
        else ApplyStatusRunes(e, true);
    }

    // Shooters and mediums first, then the toughest.
    Enemy Pick(List<Enemy> taken)
    {
        Enemy best = null;
        float bestScore = 0f, range = Range * Range;
        foreach (var e in Enemy.All)
        {
            if (!e.IsAlive || e.Hidden || e.IsCharmed || e.IsBoss || taken.Contains(e)) continue;
            if ((e.Position - Owner.Position).sqrMagnitude > range) continue;
            var b = e.Def.Behavior;
            bool shooter = b != EnemyBehavior.Chase && b != EnemyBehavior.Flutter && b != EnemyBehavior.Lunge && b != EnemyBehavior.Splitter;
            float score = e.MaxHealth * (shooter ? 2f : 1f) * (e.Def.Tier == EnemyTier.Medium ? 3f : 1f);
            if (score > bestScore) { bestScore = score; best = e; }
        }
        return best;
    }
}
