using System.Collections.Generic;
using UnityEngine;

// Starfall: calls stars down on random foes in range. Each lands after a short telegraphed delay,
// so it rewards foes that stay put and tags groups all over the screen rather than the nearest one.
// II (Meteor Shower): more stars, and impacts leave foes burning. III (Cataclysm): the first star of
// each cast is a giant meteor. Self-contained and gated by its tome.
public class Starfall : Spell
{
    public override string Name => "Starfall";
    public override Element Element => Element.Arcane;
    public override string Blurb => "Stars crash down on random foes in range after a short warning.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 3 ? 16f : Tier == 2 ? 14f : 12f;   // per star
    protected override float BaseCooldown => 3f;
    protected override float AreaPerRune => 0.15f;

    class Star { public Vector2 pos; public float timer, damage, radius; public bool big; }
    readonly List<Star> stars = new List<Star>();

    int Stars => Mathf.RoundToInt(TT("stars", Tier == 1 ? 3 : Tier == 2 ? 5 : 5)) + (Count - 1) * Mathf.RoundToInt(T("starsPerSplit", 2)) + PierceCount;
    float Radius => T("radius", 1f) * Area;

    public override string TierName(int tier) => tier == 2 ? "Meteor Shower" : tier == 3 ? "Cataclysm" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "More stars per cast, and every impact sets foes burning."
                  : $"Cataclysm: the first star of each cast is a giant meteor that also stuns, with {T("bigRadius_t3", 2.5f):0.#}x the reach and {T("bigDamage_t3", 3f):0.#}x damage.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return $"+{T("starsPerSplit", 2):0} stars per cast";
            case Rune.Pierce: return "+1 star per cast";
            case Rune.Haste: return $"Casts {Pct(HastePerRune)} faster";
            case Rune.Area: return $"+{Pct(AreaStep)} impact size";
            case Rune.Echo: return "Another volley a moment later";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th volley hits {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} harder";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var foes = EnemiesWithin(Owner.Position, Range);
        if (foes.Count == 0) return;
        float dmg = Damage;   // fixed at the call, so an Overloaded volley stays strong
        int n = Stars;
        n = Mathf.Min(n, Mathf.RoundToInt(T("maxStars", 8)));
        // Spread the stars over different foes first (shuffled), only doubling up once every foe has one.
        var order = new List<Enemy>(foes);
        for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
        for (int i = 0; i < n; i++)
        {
            var e = order[i % order.Count];
            bool big = Tier == 3 && i == 0;
            Vector2 offset = i < order.Count ? Random.insideUnitCircle * 0.3f : Random.insideUnitCircle.normalized * Radius * 1.2f;
            var s = new Star
            {
                pos = e.Position + offset,
                timer = T("delay", 0.7f) + i * T("stagger", 0.12f),
                damage = dmg * (big ? T("bigDamage_t3", 3f) : 1f),
                radius = Radius * (big ? T("bigRadius_t3", 2.5f) : 1f),
                big = big,
            };
            stars.Add(s);
            Fx.Marker(s.pos, new Color(Color.r, Color.g, Color.b, 0.7f), s.radius, s.timer);   // one warning ring per star
        }
        Sfx.Play("spells.Starfall.call", Sound.Missile, 0.3f, 0.15f);
    }

    protected override void OnTick(float dt)
    {
        if (Owner == null) return;
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            var s = stars[i];
            s.timer -= dt;
            if (s.timer > 0f) continue;
            stars.RemoveAt(i);
            Land(s);
        }
    }

    void Land(Star s)
    {
        Fx.Burst(s.pos, Color, s.radius * 2.4f);
        Fx.Ring(s.pos, Color.Lerp(Color, Color.white, 0.5f), s.radius, 0.3f, s.big);   // spark ring only for the big meteor
        Sfx.Play("spells.Starfall.land", Sound.Comet, s.big ? 0.6f : 0.35f, 0.2f);
        var caught = EnemiesWithin(s.pos, s.radius);
        Grimoire.FeatMax(SpellId.Starfall, caught.Count);
        foreach (var e in caught)
        {
            if (s.big) e.Daze(T("bigDaze_t3", 1.2f));
            if (Tier >= 2) e.ApplyBurn(s.damage * T("burnShare_t2", 0.3f), T("burnTime", 2f));
            Hit(e, s.damage, (e.Position - s.pos).normalized * T("knock", 1.5f));
        }
    }
}

// Tesla Totem: plants a ring of stationary totems around you that zap the nearest foes for a few
// seconds, then crumble. A summoned turret: you kite, they shoot. Self-contained and gated by its tome.
// II (Tesla Coil): a chain of lightning links you and the totems together, shocking foes it crosses.
// III (Storm Spire): each totem also pulses circular shocks around itself; zaps still chain with Pierce.
// Split plants more totems in the ring, Echo plants another ring, Area widens ring and reach, Potency
// also lengthens how long they stand.
public class TotemMarker : MonoBehaviour { }   // lets GameManager.ClearField remove planted totems

public class TeslaTotem : Spell
{
    public override string Name => "Tesla Totem";
    public override Element Element => Element.Storm;
    public override string Blurb => "Plants a ring of totems around you that zap nearby foes for a few seconds.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 3 ? 9f : Tier == 2 ? 8f : 7f;   // per zap
    protected override float BaseCooldown => 6f;

    class Totem
    {
        public Vector2 pos;
        public float life, fire, shock, damage;
        public GameObject go;
        public SpriteRenderer body, glow;
    }
    readonly List<Totem> totems = new List<Totem>();
    float linkTimer;
    float ringAngle = -1f;

    float Reach => T("range", 5f) * Area;
    float Life => T("life", 8f) * (1f + T("lifePerPotency", 0.1f) * RuneCount(Rune.Potency));
    float Interval => T("interval", 0.8f) * HasteFactor;
    int Targets => 1 + PierceCount;   // Pierce: each zap chains to more foes
    int MaxActive => Mathf.Max(1, Mathf.RoundToInt(T("maxActive", 2))) * Count;

    public override string TierName(int tier) => tier == 2 ? "Tesla Coil" : tier == 3 ? "Storm Spire" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"A chain of lightning links you and your totems, shocking every foe it crosses for {Pct(T("linkShare", 0.6f))} damage."
                  : $"Coil, and each totem pulses a circular shock every {T("shockEvery_t3", 1.6f):0.#}s around itself for {Pct(T("shockDamage_t3", 1f))} damage. Zaps still chain with Pierce.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} damage and +{Pct(T("lifePerPotency", 0.1f))} totem duration";
            case Rune.Split: return "+1 totem in the ring";
            case Rune.Pierce: return "Each zap chains to +1 more foe";
            case Rune.Haste: return $"Casts and zaps {Pct(HastePerRune)} faster";
            case Rune.Area: return $"+{Pct(AreaStep)} ring size, zap range and shock size";
            case Rune.Echo: return "Plants a second ring a moment later";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th ring of totems hits {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} harder";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        float dmg = Damage;   // fixed at planting, so an Overloaded ring stays strong
        int n = Count;
        // Evenly spaced on a circle round the wizard; each ring (cast or echo) starts at a fresh angle
        // so a second ring fills the gaps of the first.
        ringAngle = ringAngle < 0f ? Random.value * Mathf.PI * 2f : ringAngle + Mathf.PI / n + 0.4f;
        float radius = T("ringRadius", 2.2f) * Area;
        for (int i = 0; i < n; i++)
        {
            float a = ringAngle + i * Mathf.PI * 2f / n;
            Plant(Owner.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, dmg);
        }
        Sfx.Play("spells.TeslaTotem.plant", Sound.Spark, 0.4f, 0.15f);
    }

    void Plant(Vector2 pos, float dmg)
    {
        while (totems.Count >= MaxActive) Remove(0);
        var t = new Totem { pos = pos, life = Life, fire = 0.3f, shock = T("shockEvery_t3", 1.6f) * 0.5f, damage = dmg };
        t.go = new GameObject("TeslaTotem");
        t.go.AddComponent<TotemMarker>();
        t.go.transform.position = pos;
        float big = Tier == 1 ? 1f : Tier == 2 ? 1.5f : 2f;
        t.glow = SpriteFactory.Child(t.go.transform, "Glow", SpriteFactory.Glow, new Color(Color.r, Color.g, Color.b, 0.5f), Vector2.zero, 1.4f * big, -46);
        t.body = SpriteFactory.Child(t.go.transform, "Body", SpriteFactory.Diamond, Tier >= 2 ? Color.Lerp(Color, Color.white, 0.4f) : Color, Vector2.zero, 0.5f * big, -45);
        totems.Add(t);
        Fx.Ring(pos, Color, 0.8f, 0.25f);
    }

    void Remove(int i)
    {
        if (totems[i].go != null) Object.Destroy(totems[i].go);
        totems.RemoveAt(i);
    }

    protected override void OnTick(float dt)
    {
        if (Owner == null) return;
        for (int i = totems.Count - 1; i >= 0; i--)
        {
            var t = totems[i];
            t.life -= dt;
            if (t.life <= 0f || t.go == null)
            {
                if (t.go != null) Fx.Burst(t.pos, Color, 0.8f, false);
                Remove(i);
                continue;
            }
            float fade = Mathf.Clamp01(t.life / 1f);   // dims over its last second
            t.body.color = new Color(Color.r, Color.g, Color.b, fade);
            t.glow.color = new Color(Color.r, Color.g, Color.b, 0.35f * fade * (0.8f + 0.2f * Mathf.Sin(Time.time * 10f + i)));
            t.fire -= dt;
            if (t.fire <= 0f) Zap(t);
            if (Tier == 3)
            {
                t.shock -= dt;
                if (t.shock <= 0f) Shock(t);
            }
        }

        if (Tier >= 2 && totems.Count > 0)
        {
            linkTimer -= dt;
            if (linkTimer <= 0f) { linkTimer = T("linkTick", 0.4f) * HasteFactor; Link(); }
        }
    }

    // Tier II+: you -> nearest totem -> its nearest unlinked neighbour -> ..., within link range.
    // Every foe touching a link takes a hit (once per pulse, however many links it crosses).
    void Link()
    {
        var left = new List<Totem>(totems);
        var hit = new HashSet<Enemy>();
        float range = T("linkRange", 6f) * Area, width = T("linkWidth", 0.35f);
        Vector2 cur = Owner.Position;
        while (left.Count > 0)
        {
            Totem next = null;
            float best = range * range;
            foreach (var t in left)
            {
                float d = (t.pos - cur).sqrMagnitude;
                if (d < best) { best = d; next = t; }
            }
            if (next == null) break;
            left.Remove(next);
            Fx.Bolt(cur, next.pos, Color);
            Vector2 mid = (cur + next.pos) * 0.5f;
            foreach (var e in EnemiesWithin(mid, Mathf.Sqrt(best) * 0.5f + width + 0.5f))
            {
                if (hit.Contains(e) || DistToSegment(e.Position, cur, next.pos) > width + e.Radius) continue;
                hit.Add(e);
                var foe = e;
                KillCheck(foe, () => Hit(foe, next.damage * T("linkShare", 0.6f)));
            }
            cur = next.pos;
        }
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    // Tier III: a circle of lightning around the totem.
    void Shock(Totem t)
    {
        t.shock = T("shockEvery_t3", 1.6f) * HasteFactor;
        float r = T("shockRadius_t3", 2f) * Area;
        var foes = EnemiesWithin(t.pos, r);
        Fx.Ring(t.pos, Color.Lerp(Color, Color.white, 0.4f), r, 0.35f);
        if (foes.Count == 0) return;
        Sfx.Play("spells.TeslaTotem.thunder", Sound.Lightning, 0.35f, 0.1f);
        foreach (var e in foes)
        {
            var foe = e;
            KillCheck(foe, () => Hit(foe, t.damage * T("shockDamage_t3", 1f), (foe.Position - t.pos).normalized * 1.5f));
        }
    }

    // Runs a hit and counts a kill toward the signature feat.
    void KillCheck(Enemy e, System.Action hit)
    {
        hit();
        if (!e.IsAlive) Grimoire.Feat(SpellId.TeslaTotem);
    }

    void Zap(Totem t)
    {
        var first = Enemy.Nearest(t.pos, Reach);
        if (first == null) { t.fire = 0.2f; return; }   // nothing in range: keep checking
        t.fire = Interval;
        Sfx.Play("spells.TeslaTotem.zap", Sound.Spark, 0.3f, 0.2f);
        if (Tier >= 2) Fx.Ring(t.pos, Color, 0.9f, 0.2f);   // II+: every zap pulses visibly

        var used = new List<Enemy>();
        Vector2 from = t.pos;
        for (int i = 0; i < Targets; i++)
        {
            var e = i == 0 ? first : Enemy.Nearest(from, T("arcRange", 3f) * Area, used);
            if (e == null) break;
            used.Add(e);
            SpellVfx.Spark(from, e.Position, Color);
            float share = i == 0 ? 1f : T("arcShare", 0.7f);
            var foe = e;
            KillCheck(foe, () => Hit(foe, t.damage * share));
            from = e.Position;
        }
    }
}
