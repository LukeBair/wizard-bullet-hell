using System.Collections.Generic;
using UnityEngine;

public enum SpellId
{
    ArcaneMissile, Fireball, FrostNova, ChainLightning, Wisps,
    Rejuvenate, Overgrowth, Veil, TidalLance, Cyclone, Enthrall,
    MirrorWard, StoneSpikes, SpectralBlades, Sunspots,
}

public enum Element { Arcane, Fire, Frost, Storm, Nature, Water, Wind, Light, Shadow, Earth }

// Modifiers socketed into spells. Split and Echo are rare; the rest are common.
public enum Rune { Potency, Haste, Split, Pierce, Area, Echo, Ember, Frost, Curse, Infest, Bless, Overload, Siphon, Ward }

// A spell the wizard casts automatically. Subclasses define the base numbers and what a cast does;
// runes stack on top and are read through the computed properties below. Some spells can Evolve
// (tier 1 -> 3): evolving empties that spell's sockets for a stronger base form, but keeps a share
// of the runes' power (runes.evolveRetain, default 50%). Kept power can be fractional: continuous
// effects (Potency, Area, Haste, Ember...) use it directly, and whole-number effects (Split, Pierce,
// Echo) roll the fraction as a chance each cast, e.g. 1.5 Pierce = 1 pierce plus a 50% chance of 2.
public abstract class Spell
{
    public static readonly SpellId[] AllIds = (SpellId[])System.Enum.GetValues(typeof(SpellId));
    public const int MaxTier = 3;

    public SpellId Id { get; private set; }
    public Player Owner { get; private set; }
    public abstract string Name { get; }
    public abstract Element Element { get; }
    public abstract string Blurb { get; }

    protected abstract float BaseDamage { get; }
    protected abstract float BaseCooldown { get; }
    protected virtual int BaseCount => 1;
    protected virtual int BasePierce => 0;
    protected virtual float PotencyPerRune => 0.3f;
    protected virtual float AreaPerRune => 0.2f;

    public int Tier { get; private set; } = 1;
    public virtual bool CanEvolve => false;

    readonly int[] runes = new int[System.Enum.GetValues(typeof(Rune)).Length];
    readonly float[] retained = new float[System.Enum.GetValues(typeof(Rune)).Length];   // kept through evolutions
    float timer = 0.3f;
    readonly List<float> echoes = new List<float>();
    int rolledSplit, rolledPierce, rolledEcho;   // whole-number rune effects, rolled per cast

    // Socketed runes plus power kept from evolving (may be fractional).
    public float RuneCount(Rune r) => runes[(int)r] + retained[(int)r];
    public float Retained(Rune r) => retained[(int)r];

    // A fractional count becomes a chance: 1.5 -> 1, or 2 half the time.
    static int Roll(float v)
    {
        int whole = Mathf.FloorToInt(v);
        return whole + (Random.value < v - whole ? 1 : 0);
    }

    protected void Reroll()
    {
        rolledSplit = Roll(RuneCount(Rune.Split));
        rolledPierce = Roll(RuneCount(Rune.Pierce));
        rolledEcho = Roll(RuneCount(Rune.Echo));
    }
    public int TotalRunes { get { int n = 0; foreach (var r in runes) n += r; return n; } }
    public int Level => 1 + TotalRunes;

    // Tuner lookups: spells.<SpellId>.<key>. Tier 2/3 read "<key>_t2" / "<key>_t3"; if that key is
    // missing they use the code's default for that tier.
    protected float T(string key, float fallback) => Tune.F(Tune.Key(TunePrefix, key), fallback);
    protected float TT(string key, float fallback)
    {
        if (Tier > 1)
        {
            string tiered = Tune.Key(key, Tier == 2 ? "_t2" : "_t3");
            return Tune.Has(Tune.Key(TunePrefix, tiered)) ? T(tiered, fallback) : fallback;
        }
        return T(key, fallback);
    }
    string tunePrefix;
    string TunePrefix => tunePrefix ??= "spells." + Id + ".";

    protected float PotencyStep => TT("potencyPerRune", PotencyPerRune);
    protected float AreaStep => TT("areaPerRune", AreaPerRune);
    public float Potency => 1f + PotencyStep * RuneCount(Rune.Potency);
    // Sunspots' Sunlit buff and an Overloaded cast both raise it.
    public float Damage => TT("damage", BaseDamage) * Potency * (Owner != null ? Owner.DamageMultiplier : 1f) * (overloaded ? OverloadMultiplier : 1f);
    public float Cooldown => TT("cooldown", BaseCooldown) * Tuning.CastRate * HasteFactor / (Owner != null ? Owner.CastSpeedMultiplier : 1f);
    // Haste adds cast speed (+15% per rune by default) rather than multiplying the cooldown, so it doesn't compound.
    public float HasteFactor => 1f / (1f + HastePerRune * RuneCount(Rune.Haste));
    public int Count => Mathf.RoundToInt(TT("count", BaseCount)) + rolledSplit;
    public int PierceCount => Mathf.RoundToInt(TT("pierce", BasePierce)) + rolledPierce;
    public float Area => 1f + AreaStep * RuneCount(Rune.Area);
    public int Echoes => rolledEcho;
    public virtual float CooldownProgress => 1f - Mathf.Clamp01(timer / Cooldown);
    protected float Range => GameManager.Instance.TargetRange;

    public Color Color => ElementColor(Element);

    public static bool IsRare(Rune r) => r == Rune.Split || r == Rune.Echo;

    public static Color ElementColor(Element e)
    {
        switch (e)
        {
            case Element.Arcane: return new Color(0.75f, 0.5f, 1f);
            case Element.Fire: return new Color(1f, 0.5f, 0.2f);
            case Element.Frost: return new Color(0.5f, 0.85f, 1f);
            case Element.Storm: return new Color(1f, 0.95f, 0.4f);
            case Element.Water: return new Color(0.25f, 0.55f, 1f);
            case Element.Wind: return new Color(0.75f, 0.95f, 0.85f);
            case Element.Light: return new Color(1f, 0.9f, 0.6f);
            case Element.Shadow: return new Color(1f, 0.4f, 0.75f);
            case Element.Earth: return new Color(0.8f, 0.6f, 0.35f);
            default: return new Color(0.5f, 1f, 0.45f);
        }
    }

    public static Spell Create(SpellId id, Player owner)
    {
        Spell s;
        switch (id)
        {
            case SpellId.ArcaneMissile: s = new ArcaneMissile(); break;
            case SpellId.Fireball: s = new Fireball(); break;
            case SpellId.FrostNova: s = new FrostNova(); break;
            case SpellId.ChainLightning: s = new ChainLightning(); break;
            case SpellId.Wisps: s = new Wisps(); break;
            case SpellId.Rejuvenate: s = new Rejuvenate(); break;
            case SpellId.Overgrowth: s = new Overgrowth(); break;
            case SpellId.Veil: s = new Veil(); break;
            case SpellId.TidalLance: s = new TidalLance(); break;
            case SpellId.Cyclone: s = new Cyclone(); break;
            case SpellId.MirrorWard: s = new MirrorWard(); break;
            case SpellId.StoneSpikes: s = new StoneSpikes(); break;
            case SpellId.SpectralBlades: s = new SpectralBlades(); break;
            case SpellId.Sunspots: s = new Sunspots(); break;
            default: s = new Enthrall(); break;
        }
        s.Id = id;
        s.Owner = owner;
        s.OnLearned();
        return s;
    }

    // Name and blurb without needing a Player, for upgrade cards.
    public static Spell Preview(SpellId id) => Create(id, null);

    public void AddRune(Rune r, int amount = 1)
    {
        runes[(int)r] += amount;
        Reroll();
        OnRunesChanged();
    }

    // Raise the tier, empty the sockets and keep a share of their power.
    public void Evolve()
    {
        if (!CanEvolve || Tier >= MaxTier) return;
        Tier++;
        float keep = Mathf.Clamp01(Tune.F("runes.evolveRetain", 0.5f));
        for (int i = 0; i < runes.Length; i++)
        {
            retained[i] = (retained[i] + runes[i]) * keep;
            runes[i] = 0;
        }
        Reroll();
        OnRunesChanged();
        Particles.Evolve(Owner.Position, Color);
    }

    // "Pierce 1.5 · Potency 0.75" for the UI; empty if nothing was kept.
    public string RetainedSummary()
    {
        var parts = new List<string>();
        for (int i = 0; i < retained.Length; i++)
            if (retained[i] > 0.01f) parts.Add($"{(Rune)i} {retained[i]:0.##}");
        return string.Join(" · ", parts);
    }

    // What the next tier does, for the evolve card.
    public virtual string TierDescription(int tier) => "";

    // Each evolved form has its own name; tier I is just the spell's name.
    public virtual string TierName(int tier) => Name;
    public string Title => Tier == 1 ? Name : TierName(Tier);

    public void Tick(float dt)
    {
        float siphon = RuneCount(Rune.Siphon);
        if (siphon > 0f) siphonBank = Mathf.Min(SiphonCap, siphonBank + SiphonCap * dt);
        OnTick(dt);

        for (int i = echoes.Count - 1; i >= 0; i--)
        {
            echoes[i] -= dt;
            if (echoes[i] <= 0f) { echoes.RemoveAt(i); Reroll(); Cast(); }
        }

        timer -= dt;
        if (timer > 0f || !Ready()) return;
        castCount++;
        overloaded = Overloadable && RuneCount(Rune.Overload) > 0f && castCount % Mathf.Max(1, Tune.I("runes.Overload.every", 4)) == 0;
        timer = Cooldown;
        Reroll();
        if (overloaded)
        {
            Fx.Ring(Owner.Position, Color.Lerp(Color, Color.white, 0.5f), 1.6f, 0.4f);
            Sfx.Play(Sound.Lightning, 0.35f, 0.3f);
        }
        Cast();
        if (HitsFoes) Owner?.CastAnim();
        if (RuneCount(Rune.Ward) > 0f) Owner.GainWard();
        if (EchoRecasts)
            for (int i = 1; i <= Echoes; i++) echoes.Add(0.22f * i);
    }

    protected void ResetCooldown() => timer = Cooldown;
    protected bool OffCooldown => timer <= 0f;

    protected virtual bool EchoRecasts => true;   // false when Echo means something else (Wisps)

    // Overload: every Nth cast is much stronger until the next cast. Spells that never cast ignore it.
    int castCount;
    bool overloaded;
    protected virtual bool Overloadable => true;
    float OverloadMultiplier => 1f + Tune.F("runes.Overload.bonusPerRune", 1.5f) * RuneCount(Rune.Overload);

    // Siphon: hits heal a share of their damage, from a bank that refills at the per-second cap.
    float siphonBank;
    float SiphonCap => Tune.F("runes.Siphon.capPerRune", 2f) * RuneCount(Rune.Siphon);
    protected virtual void OnLearned() { }
    protected virtual void OnRunesChanged() { }
    protected virtual void OnTick(float dt) { }
    protected virtual bool Ready() => Enemy.Nearest(Owner.Position, Range) != null;
    protected abstract void Cast();

    // What one rune does to this particular spell, for upgrade descriptions.
    public virtual string RuneEffect(Rune r)
    {
        if (IsStatusRune(r) && !HitsFoes) return "No effect: this spell never hits foes";
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} damage";
            case Rune.Haste: return $"+{Pct(HastePerRune)} cast speed";
            case Rune.Split: return "+1 projectile";
            case Rune.Pierce: return "Passes through +1 enemy";
            case Rune.Area: return $"+{Pct(AreaStep)} size";
            case Rune.Echo: return "Casts again a moment later";
            case Rune.Overload: return Overloadable ? OverloadEffect : "No effect: this spell never casts";
            case Rune.Ward: return WardEffect;
            default: return StatusRuneEffect(r);
        }
    }

    // Ember, Frost, Curse, Infest and Bless all ride on Hit(), so they only work on spells that hit foes.
    public static bool IsStatusRune(Rune r) => r == Rune.Ember || r == Rune.Frost || r == Rune.Curse || r == Rune.Infest || r == Rune.Bless || r == Rune.Siphon;

    protected static string OverloadEffect => $"Every {Tune.I("runes.Overload.every", 4)}th cast is {Pct(Tune.F("runes.Overload.bonusPerRune", 1.5f))} stronger";
    protected static string WardEffect => $"Casting grants a ward that blocks one bullet (at most one every {Tune.F("runes.Ward.cooldown", 3f):0.#}s)";
    protected virtual bool HitsFoes => true;

    protected static string StatusRuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Ember: return $"Hits burn foes for {Pct(Tune.F("runes.Ember.burnPerRune", 0.2f) * Tune.F("runes.Ember.duration", 2.5f))} of the spell's damage over {Tune.F("runes.Ember.duration", 2.5f):0.#}s";
            case Rune.Frost: return $"Hits chill foes (slows {Pct(Tune.F("status.chillSlow", 0.5f))})";
            case Rune.Curse: return $"Foes it hits take +{Pct(Tune.F("runes.Curse.bonusPerRune", 0.5f))} from the next spell that hits them";
            case Rune.Infest: return $"Foes it hits burst into bugs if they die within {Tune.F("runes.Infest.duration", 1f):0.#}s";
            case Rune.Siphon: return $"Hits heal you {Pct(Tune.F("runes.Siphon.healPerRune", 0.02f))} of their damage (up to {Tune.F("runes.Siphon.capPerRune", 2f):0.#} HP a second)";
            default: return $"Foes it hits have a {Pct(Tune.F("runes.Bless.chancePerRune", 0.15f))} chance to drop a big XP gem when they die";
        }
    }

    protected static float HastePerRune => Tune.F("runes.Haste.castSpeedPerRune", 0.15f);
    protected static string Pct(float fraction) => $"{fraction * 100f:0}%";
    // Seconds as the player feels them: cooldowns are scaled by the global cast rate.
    protected static string Secs(float cooldown) => $"{cooldown * Tuning.CastRate:0.#}s";

    public static string RuneName(Rune r) => r.ToString();

    public static Color RuneColor(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return new Color(1f, 0.4f, 0.4f);
            case Rune.Haste: return new Color(1f, 0.9f, 0.35f);
            case Rune.Split: return new Color(0.5f, 1f, 0.6f);
            case Rune.Pierce: return new Color(0.85f, 0.85f, 0.95f);
            case Rune.Area: return new Color(0.45f, 0.75f, 1f);
            case Rune.Echo: return new Color(0.85f, 0.55f, 1f);
            case Rune.Ember: return new Color(1f, 0.55f, 0.2f);
            case Rune.Frost: return new Color(0.6f, 0.95f, 1f);
            case Rune.Curse: return new Color(0.65f, 0.3f, 0.85f);
            case Rune.Infest: return new Color(0.6f, 0.75f, 0.2f);
            case Rune.Overload: return new Color(1f, 0.3f, 0.2f);
            case Rune.Siphon: return new Color(0.85f, 0.15f, 0.3f);
            case Rune.Ward: return new Color(0.75f, 0.9f, 1f);
            case Rune.Bless: return new Color(1f, 0.95f, 0.6f);
            default: return new Color(1f, 0.95f, 0.6f);
        }
    }

    // Everything that happens when this spell damages an enemy: curse marks, rune statuses and
    // element interactions with whatever the enemy is already suffering from.
    protected void Hit(Enemy e, float damage, Vector2 push = default)
    {
        if (e == null || !e.IsAlive || e.Hidden) return;

        damage *= 1f + e.ConsumeCurse();
        if (Element == Element.Storm && e.IsWet)
        {
            damage *= Tune.F("combos.conductiveMultiplier", 1.5f);
            Interactions.Trigger(Interaction.Conductive, e.Position);
            Grimoire.Feat(SpellId.TidalLance);
        }
        if (Element == Element.Frost && e.IsWet)
        {
            e.ApplyChill(3f);
            e.AddFrostbite(0.35f);
            Interactions.Trigger(Interaction.DeepFreeze, e.Position);
            Grimoire.Feat(SpellId.TidalLance);
        }

        float ember = RuneCount(Rune.Ember);
        if (ember > 0) e.ApplyBurn(Damage * Tune.F("runes.Ember.burnPerRune", 0.2f) * ember, Tune.F("runes.Ember.duration", 2.5f));
        float frost = RuneCount(Rune.Frost);
        if (frost > 0) e.ApplyChill(Tune.F("runes.Frost.chillBase", 1f) + Tune.F("runes.Frost.chillPerRune", 0.6f) * frost);
        float infest = RuneCount(Rune.Infest);
        if (infest > 0) e.Infest(Damage * Tune.F("runes.Infest.burstPerRune", 0.5f) * infest, Tune.F("runes.Infest.duration", 1f));
        float bless = RuneCount(Rune.Bless);
        if (bless > 0) e.Bless(Mathf.Min(Tune.F("runes.Bless.maxChance", 0.8f), Tune.F("runes.Bless.chancePerRune", 0.15f) * bless));

        e.TakeDamage(damage, push, Color.Lerp(Color, Color.white, 0.4f));

        float siphon = RuneCount(Rune.Siphon);
        if (siphon > 0f && siphonBank > 0f && Owner != null)
        {
            float heal = Mathf.Min(siphonBank, damage * Tune.F("runes.Siphon.healPerRune", 0.02f) * siphon);
            siphonBank -= heal;
            Owner.Heal(heal, false);
        }

        float curse = RuneCount(Rune.Curse);
        if (curse > 0 && e.IsAlive) e.MarkCurse(Mathf.Min(Tune.F("runes.Curse.maxBonus", 1f), Tune.F("runes.Curse.bonusPerRune", 0.5f) * curse));
    }

    protected Projectile Shoot(Vector2 from, Vector2 dir, float speed, float radius)
    {
        var p = Projectile.Spawn(from, dir.normalized * speed, Team.Player, Color, radius);
        p.Damage = 0f;   // damage goes through Hit() so runes and interactions apply
        p.Pierce = PierceCount;
        return p;
    }

    // A fresh list callers may keep, or a shared empty one (don't add to it) when nothing is in range,
    // which is most calls from per-frame checks like orbiting wisps and beams.
    public static List<Enemy> EnemiesWithin(Vector2 center, float radius)
    {
        List<Enemy> list = null;
        foreach (var e in Enemy.All)
        {
            if (!e.IsAlive || e.Hidden || e.IsCharmed) continue;   // charmed foes are allies for now
            float r = radius + e.Radius;
            if ((e.Position - center).sqrMagnitude < r * r) (list ??= new List<Enemy>()).Add(e);
        }
        return list ?? NoEnemies;
    }
    static readonly List<Enemy> NoEnemies = new List<Enemy>();

    protected static string Roman(int tier) => tier == 1 ? "I" : tier == 2 ? "II" : "III";
}
