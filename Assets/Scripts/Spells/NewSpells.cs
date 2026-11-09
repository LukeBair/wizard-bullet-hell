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
                  : $"The first star of each cast is a giant meteor with {T("bigRadius_t3", 2.5f):0.#}x the reach and {T("bigDamage_t3", 3f):0.#}x damage.";

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
        for (int i = 0; i < n; i++)
        {
            var e = foes[Random.Range(0, foes.Count)];
            bool big = Tier == 3 && i == 0;
            var s = new Star
            {
                pos = e.Position + Random.insideUnitCircle * 0.3f,
                timer = T("delay", 0.7f) + i * T("stagger", 0.12f),
                damage = dmg * (big ? T("bigDamage_t3", 3f) : 1f),
                radius = Radius * (big ? T("bigRadius_t3", 2.5f) : 1f),
                big = big,
            };
            stars.Add(s);
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
            if (s.timer > 0f)
            {
                // Warning ring, pulsed a few times while it falls.
                if (Mathf.FloorToInt((s.timer + dt) * 8f) != Mathf.FloorToInt(s.timer * 8f))
                    Fx.Ring(s.pos, new Color(Color.r, Color.g, Color.b, 0.6f), s.radius, 0.12f);
                continue;
            }
            stars.RemoveAt(i);
            Land(s);
        }
    }

    void Land(Star s)
    {
        Fx.Burst(s.pos, Color, s.radius * 2.4f);
        Fx.Ring(s.pos, Color.Lerp(Color, Color.white, 0.5f), s.radius, 0.3f);
        Sfx.Play("spells.Starfall.land", Sound.Comet, s.big ? 0.6f : 0.35f, 0.2f);
        foreach (var e in EnemiesWithin(s.pos, s.radius))
        {
            if (Tier >= 2) e.ApplyBurn(s.damage * T("burnShare_t2", 0.3f), T("burnTime", 2f));
            Hit(e, s.damage, (e.Position - s.pos).normalized * T("knock", 1.5f));
        }
    }
}

// Tesla Totem: plants a stationary totem that zaps the nearest foes for a few seconds, then
// crumbles; a few can stand at once. A summoned turret: you kite, it shoots. Self-contained and
// gated by its tome. II (Tesla Coil): lasts longer and each zap arcs to one more foe. III (Storm
// Spire): every few zaps the totem calls down a thunderclap.
public class TeslaTotem : Spell
{
    public override string Name => "Tesla Totem";
    public override Element Element => Element.Storm;
    public override string Blurb => "Plants a totem that zaps nearby foes for a few seconds.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 3 ? 9f : Tier == 2 ? 8f : 7f;   // per zap
    protected override float BaseCooldown => 6f;

    class Totem
    {
        public Vector2 pos;
        public float life, fire, damage;
        public int zaps;
        public GameObject go;
        public SpriteRenderer body, glow;
    }
    readonly List<Totem> totems = new List<Totem>();

    float Reach => T("range", 5f) * Area;
    float Life => T("life", 8f) * (Tier >= 2 ? T("lifeMult_t2", 1.5f) : 1f);
    float Interval => T("interval", 0.8f) * HasteFactor;
    int Targets => 1 + PierceCount + (Tier >= 2 ? 1 : 0);
    int MaxActive => Mathf.Max(1, Mathf.RoundToInt(T("maxActive", 2))) * Count;

    public override string TierName(int tier) => tier == 2 ? "Tesla Coil" : tier == 3 ? "Storm Spire" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Totems last {Pct(T("lifeMult_t2", 1.5f) - 1f)} longer and every zap arcs to one more foe."
                  : $"Every {T("thunderEvery_t3", 5)}th zap becomes a thunderclap for {T("thunderDamage_t3", 2.5f):0.#}x damage in an area.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 totem planted per cast";
            case Rune.Pierce: return "Each zap arcs to +1 more foe";
            case Rune.Haste: return $"Casts and zaps {Pct(HastePerRune)} faster";
            case Rune.Area: return $"+{Pct(AreaStep)} zap range (and thunderclap size)";
            case Rune.Echo: return "Plants a second totem a moment later";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th totem zaps {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} harder";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        float dmg = Damage;   // fixed at planting, so an Overloaded totem stays strong
        int n = Count;
        for (int i = 0; i < n; i++)
        {
            float a = (i + Random.value * 0.4f) / n * Mathf.PI * 2f;
            Vector2 pos = Owner.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (n > 1 ? 0.9f : 0.6f);
            Plant(pos, dmg);
        }
        Sfx.Play("spells.TeslaTotem.plant", Sound.Spark, 0.4f, 0.15f);
    }

    void Plant(Vector2 pos, float dmg)
    {
        while (totems.Count >= MaxActive) Remove(0);
        var t = new Totem { pos = pos, life = Life, fire = 0.3f, damage = dmg };
        t.go = new GameObject("TeslaTotem");
        t.go.transform.position = pos;
        t.glow = SpriteFactory.Child(t.go.transform, "Glow", SpriteFactory.Glow, new Color(Color.r, Color.g, Color.b, 0.5f), Vector2.zero, 1.4f, -46);
        t.body = SpriteFactory.Child(t.go.transform, "Body", SpriteFactory.Diamond, Color, Vector2.zero, 0.5f, -45);
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
            if (t.life <= 0f || t.go == null) { if (t.go != null) Fx.Burst(t.pos, Color, 0.8f, false); Remove(i); continue; }
            float fade = Mathf.Clamp01(t.life / 1f);   // dims over its last second
            t.body.color = new Color(Color.r, Color.g, Color.b, fade);
            t.glow.color = new Color(Color.r, Color.g, Color.b, 0.35f * fade * (0.8f + 0.2f * Mathf.Sin(Time.time * 10f + i)));
            t.fire -= dt;
            if (t.fire <= 0f) Zap(t);
        }
    }

    void Zap(Totem t)
    {
        var used = new List<Enemy>();
        float reach = Reach;
        Vector2 from = t.pos;
        var first = Enemy.Nearest(t.pos, reach);
        if (first == null) { t.fire = 0.2f; return; }   // nothing in range: keep checking
        t.fire = Interval;
        t.zaps++;
        Sfx.Play("spells.TeslaTotem.zap", Sound.Spark, 0.3f, 0.2f);

        bool thunder = Tier == 3 && t.zaps % Mathf.Max(1, Mathf.RoundToInt(T("thunderEvery_t3", 5))) == 0;
        for (int i = 0; i < Targets; i++)
        {
            var e = i == 0 ? first : Enemy.Nearest(from, T("arcRange", 3f) * Area, used);
            if (e == null) break;
            used.Add(e);
            SpellVfx.Spark(from, e.Position, Color);
            float share = i == 0 ? 1f : T("arcShare", 0.7f);
            if (thunder && i == 0)
            {
                float r = T("thunderRadius_t3", 1.4f) * Area;
                Fx.Burst(e.Position, Color, r * 2f);
                Sfx.Play("spells.TeslaTotem.thunder", Sound.Lightning, 0.45f, 0.1f);
                foreach (var x in EnemiesWithin(e.Position, r)) Hit(x, t.damage * T("thunderDamage_t3", 2.5f));
            }
            else Hit(e, t.damage * share);
            from = e.Position;
        }
    }
}
