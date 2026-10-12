using System.Collections.Generic;
using UnityEngine;

public enum SpellId
{
    ArcaneMissile, Fireball, FrostNova, ChainLightning, Wisps,
    Rejuvenate, Overgrowth, Veil, TidalLance, Cyclone, Enthrall,
}

public enum Element { Arcane, Fire, Frost, Storm, Nature, Water, Wind, Light, Shadow }

// Modifiers socketed into spells. Split and Echo are rare; the rest are common.
public enum Rune { Potency, Haste, Split, Pierce, Area, Echo, Ember, Frost, Curse, Infest, Bless }

// A spell the wizard casts automatically. Subclasses define the base numbers and what a cast does;
// runes stack on top and are read through the computed properties below. Some spells can Evolve
// (tier 1 -> 3): evolving clears that spell's runes in exchange for a stronger base form.
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
    float timer = 0.3f;
    readonly List<float> echoes = new List<float>();

    public int RuneCount(Rune r) => runes[(int)r];
    public int TotalRunes { get { int n = 0; foreach (var r in runes) n += r; return n; } }
    public int Level => 1 + TotalRunes;

    public float Potency => 1f + PotencyPerRune * RuneCount(Rune.Potency);
    public float Damage => BaseDamage * Potency;
    public float Cooldown => BaseCooldown * Tuning.CastRate * Mathf.Pow(0.85f, RuneCount(Rune.Haste));
    public int Count => BaseCount + RuneCount(Rune.Split);
    public int PierceCount => BasePierce + RuneCount(Rune.Pierce);
    public float Area => 1f + AreaPerRune * RuneCount(Rune.Area);
    public int Echoes => RuneCount(Rune.Echo);
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
        OnRunesChanged();
    }

    // Raise the tier and wipe all runes.
    public void Evolve()
    {
        if (!CanEvolve || Tier >= MaxTier) return;
        Tier++;
        for (int i = 0; i < runes.Length; i++) runes[i] = 0;
        OnRunesChanged();
    }

    // What the next tier does, for the evolve card.
    public virtual string TierDescription(int tier) => "";

    public void Tick(float dt)
    {
        OnTick(dt);

        for (int i = echoes.Count - 1; i >= 0; i--)
        {
            echoes[i] -= dt;
            if (echoes[i] <= 0f) { echoes.RemoveAt(i); Cast(); }
        }

        timer -= dt;
        if (timer > 0f || !Ready()) return;
        timer = Cooldown;
        Cast();
        if (EchoRecasts)
            for (int i = 1; i <= Echoes; i++) echoes.Add(0.22f * i);
    }

    protected void ResetCooldown() => timer = Cooldown;
    protected bool OffCooldown => timer <= 0f;

    protected virtual bool EchoRecasts => true;   // false when Echo means something else (Wisps)
    protected virtual void OnLearned() { }
    protected virtual void OnRunesChanged() { }
    protected virtual void OnTick(float dt) { }
    protected virtual bool Ready() => Enemy.Nearest(Owner.Position, Range) != null;
    protected abstract void Cast();

    // What one rune does to this particular spell, for upgrade descriptions.
    public virtual string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{PotencyPerRune * 100f:0}% damage";
            case Rune.Haste: return "Casts 15% faster";
            case Rune.Split: return "+1 projectile";
            case Rune.Pierce: return "Passes through +1 enemy";
            case Rune.Area: return $"+{AreaPerRune * 100f:0}% size";
            case Rune.Echo: return "Casts again a moment later";
            case Rune.Ember: return "Sets enemies on fire";
            case Rune.Frost: return "Chills enemies (slows 50%)";
            case Rune.Curse: return "Marks foes: their next hit deals +50% (stacks to 6x)";
            case Rune.Infest: return "Infests foes for 1s: if they die, bugs burst out";
            default: return "Kills may drop a big XP gem";
        }
    }

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
            damage *= 1.5f;
            Interactions.Trigger(Interaction.Conductive, e.Position);
        }
        if (Element == Element.Frost && e.IsWet)
        {
            e.ApplyChill(3f);
            e.AddFrostbite(0.35f);
            Interactions.Trigger(Interaction.DeepFreeze, e.Position);
        }

        int ember = RuneCount(Rune.Ember);
        if (ember > 0) e.ApplyBurn(Damage * 0.4f * ember, 2.5f);
        int frost = RuneCount(Rune.Frost);
        if (frost > 0) e.ApplyChill(1f + 0.6f * frost);
        int infest = RuneCount(Rune.Infest);
        if (infest > 0) e.Infest(Damage * 0.5f * infest, 1f);
        int bless = RuneCount(Rune.Bless);
        if (bless > 0) e.Bless(Mathf.Min(0.8f, 0.15f * bless));

        e.TakeDamage(damage, push, Color.Lerp(Color, Color.white, 0.4f));

        int curse = RuneCount(Rune.Curse);
        if (curse > 0 && e.IsAlive) e.MarkCurse(Mathf.Min(5f, 0.5f * curse));
    }

    protected Projectile Shoot(Vector2 from, Vector2 dir, float speed, float radius)
    {
        var p = Projectile.Spawn(from, dir.normalized * speed, Team.Player, Color, radius);
        p.Damage = 0f;   // damage goes through Hit() so runes and interactions apply
        p.Pierce = PierceCount;
        return p;
    }

    public static List<Enemy> EnemiesWithin(Vector2 center, float radius)
    {
        var list = new List<Enemy>();
        foreach (var e in Enemy.All)
        {
            if (!e.IsAlive || e.Hidden || e.IsCharmed) continue;   // charmed foes are allies for now
            float r = radius + e.Radius;
            if ((e.Position - center).sqrMagnitude < r * r) list.Add(e);
        }
        return list;
    }

    protected static string Roman(int tier) => tier == 1 ? "I" : tier == 2 ? "II" : "III";
}
