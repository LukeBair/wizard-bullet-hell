using System.Collections.Generic;
using UnityEngine;

// Blazing Halo: a persistent aura. Every pulse burns and shoves everything within reach, so you
// fight by staying in the thick of it. Gated by its tome like any other spell (Grimoire.HasTome).
// II (Searing Halo): wider, and pulses set foes burning. III (Sunfire Corona): every few pulses a
// huge corona erupts for heavy damage and a hard shove.
public class BlazingHalo : Spell
{
    public override string Name => "Blazing Halo";
    public override Element Element => Element.Fire;
    public override string Blurb => "A ring of fire follows you, burning and shoving every foe that gets close.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 3 ? 5.5f : Tier == 2 ? 5f : 4f;   // per pulse
    protected override float BaseCooldown => 0.6f;
    protected override bool CastAnimates => false;   // it never stops pulsing; don't flail the wizard each time

    int pulses;
    SpriteRenderer glow;

    float Radius => TT("radius", Tier == 1 ? 1.5f : Tier == 2 ? 1.9f : 2.1f) * Area;
    float BandGrow => T("bandGrow", 0.35f);
    int Bands => Count;
    float Outer => Radius * (1f + BandGrow * (Bands - 1));
    float Knock => T("knock", 1.5f) * (1f + T("knockPerPierce", 0.5f) * PierceCount);

    public override string TierName(int tier) => tier == 2 ? "Searing Halo" : tier == 3 ? "Sunfire Corona" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"A wider ring, and every pulse sets foes burning."
                  : $"Wider again, and every {T("coronaEvery_t3", 4)}th pulse erupts into a corona for {T("coronaDamage_t3", 2.5f):0.#}x damage and a hard shove.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return $"+1 outer band ({Pct(BandGrow)} wider) that burns for {Pct(T("bandShare", 0.5f))} damage";
            case Rune.Pierce: return $"+{Pct(T("knockPerPierce", 0.5f))} knockback";
            case Rune.Haste: return $"Pulses {Pct(HastePerRune)} faster";
            case Rune.Area: return $"+{Pct(AreaStep)} ring size";
            case Rune.Echo: return "Pulses again a moment later";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th pulse is {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} stronger";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => EnemiesWithin(Owner.Position, Outer).Count > 0;

    protected override void Cast()
    {
        Vector2 c = Owner.Position;
        pulses++;
        bool corona = Tier == 3 && pulses % Mathf.Max(1, Mathf.RoundToInt(T("coronaEvery_t3", 4))) == 0;
        float dmg = Damage;
        float scale = corona ? T("coronaRadius_t3", 1.6f) : 1f;
        float inner = Radius * scale, outer = Outer * scale;
        float push = Knock * (corona ? T("coronaKnock_t3", 2.5f) : 1f);
        Fx.Ring(c, corona ? Color.Lerp(Color, Color.white, 0.5f) : Color, outer, corona ? 0.45f : 0.3f);
        Sfx.Play("spells.BlazingHalo.cast", Sound.Comet, corona ? 0.4f : 0.15f, 0.2f);

        foreach (var e in EnemiesWithin(c, outer))
        {
            Vector2 away = e.Position - c;
            float d = away.magnitude;
            int band = d <= inner ? 0 : Mathf.Min(Bands - 1, Mathf.CeilToInt((d - inner) / (inner * BandGrow)));
            float share = band == 0 ? 1f : T("bandShare", 0.5f);
            if (corona) share *= T("coronaDamage_t3", 2.5f);
            if (Tier >= 2) e.ApplyBurn(dmg * share * T("burnShare_t2", 0.4f), T("burnTime", 2f));
            Hit(e, dmg * share, away.normalized * push);
        }
    }

    protected override void OnTick(float dt)
    {
        if (Owner == null) return;
        if (glow == null)
        {
            glow = SpriteFactory.Child(Owner.transform, "HaloGlow", SpriteFactory.Glow, Color.white, Vector2.zero, 1f, -47);
        }
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f);
        glow.color = new Color(Color.r, Color.g, Color.b, 0.22f * flicker);
        glow.transform.localScale = Vector3.one * Radius * 2.4f;
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
