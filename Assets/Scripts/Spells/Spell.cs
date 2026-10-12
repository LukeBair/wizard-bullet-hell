using System.Collections.Generic;
using UnityEngine;

public enum SpellId { ArcaneMissile, Fireball, FrostNova, ChainLightning, Wisps }
public enum Element { Arcane, Fire, Frost, Storm, Nature }

// Runes are the shared modifier vocabulary every upgrade system speaks: a Draft card, a socketed
// rune and an element level-up all end up as "+1 Split on Fireball".
public enum Rune { Power, Haste, Split, Pierce, Area, Echo, Ember, Frost }

// A spell the wizard casts automatically. Subclasses define the base numbers and what a cast does;
// runes stack on top and are read through the computed properties below.
public abstract class Spell
{
    public static readonly SpellId[] AllIds = { SpellId.ArcaneMissile, SpellId.Fireball, SpellId.FrostNova, SpellId.ChainLightning, SpellId.Wisps };

    public SpellId Id { get; private set; }
    public Player Owner { get; private set; }
    public abstract string Name { get; }
    public abstract Element Element { get; }
    public abstract string Blurb { get; }

    protected abstract float BaseDamage { get; }
    protected abstract float BaseCooldown { get; }
    protected virtual int BaseCount => 1;
    protected virtual int BasePierce => 0;

    readonly int[] runes = new int[System.Enum.GetValues(typeof(Rune)).Length];
    float timer = 0.3f;
    readonly List<float> echoes = new List<float>();

    public int RuneCount(Rune r) => runes[(int)r];
    public int TotalRunes { get { int n = 0; foreach (var r in runes) n += r; return n; } }
    public int Level => 1 + TotalRunes;

    public float Damage => BaseDamage * (1f + 0.35f * RuneCount(Rune.Power));
    public float Cooldown => BaseCooldown * Mathf.Pow(0.85f, RuneCount(Rune.Haste));
    public int Count => BaseCount + RuneCount(Rune.Split);
    public int PierceCount => BasePierce + RuneCount(Rune.Pierce);
    public float Area => 1f + 0.3f * RuneCount(Rune.Area);
    public int Echoes => RuneCount(Rune.Echo);
    public float CooldownProgress => 1f - Mathf.Clamp01(timer / Cooldown);

    public Color Color => ElementColor(Element);

    public static Color ElementColor(Element e)
    {
        switch (e)
        {
            case Element.Arcane: return new Color(0.75f, 0.5f, 1f);
            case Element.Fire: return new Color(1f, 0.5f, 0.2f);
            case Element.Frost: return new Color(0.5f, 0.85f, 1f);
            case Element.Storm: return new Color(1f, 0.95f, 0.4f);
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
            default: s = new Wisps(); break;
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
        for (int i = 1; i <= Echoes; i++) echoes.Add(0.22f * i);
    }

    protected virtual void OnLearned() { }
    protected virtual void OnRunesChanged() { }
    protected virtual void OnTick(float dt) { }
    protected virtual bool Ready() => Enemy.Nearest(Owner.Position, 10f) != null;
    protected abstract void Cast();

    // What one rune does to this particular spell, for upgrade descriptions.
    public virtual string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Power: return "+35% damage";
            case Rune.Haste: return "Casts 15% faster";
            case Rune.Split: return "+1 projectile";
            case Rune.Pierce: return "Passes through +1 enemy";
            case Rune.Area: return "+30% size";
            case Rune.Echo: return "Casts again a moment later";
            case Rune.Ember: return "Sets enemies on fire";
            default: return "Chills enemies (slows 50%)";
        }
    }

    public static string RuneName(Rune r) => r.ToString();

    public static Color RuneColor(Rune r)
    {
        switch (r)
        {
            case Rune.Power: return new Color(1f, 0.4f, 0.4f);
            case Rune.Haste: return new Color(1f, 0.9f, 0.35f);
            case Rune.Split: return new Color(0.5f, 1f, 0.6f);
            case Rune.Pierce: return new Color(0.85f, 0.85f, 0.95f);
            case Rune.Area: return new Color(0.45f, 0.75f, 1f);
            case Rune.Echo: return new Color(0.85f, 0.55f, 1f);
            case Rune.Ember: return new Color(1f, 0.55f, 0.2f);
            default: return new Color(0.6f, 0.95f, 1f);
        }
    }

    // Everything that happens when this spell damages an enemy: runes, element traits and fusions.
    protected void Hit(Enemy e, float damage, Vector2 push = default)
    {
        if (e == null || !e.IsAlive) return;
        if (Element == Element.Fire && Fusions.Has(Fusion.ThermalShock) && e.IsChilled) damage *= 2f;
        if (Fusions.Has(Fusion.Shatter) && Element == Element.Arcane) e.ApplyChill(1.5f);
        if (Fusions.Has(Fusion.Wildfire) && Element == Element.Nature) e.ApplyBurn(Damage * 0.6f, 2f);
        if (Fusions.Has(Fusion.Bramble) && Element == Element.Frost) e.ApplyBurn(Damage * 0.4f, 3f);

        int ember = RuneCount(Rune.Ember);
        if (ember > 0) e.ApplyBurn(Damage * 0.4f * ember, 2.5f);
        int frost = RuneCount(Rune.Frost);
        if (frost > 0) e.ApplyChill(1f + 0.6f * frost);

        e.TakeDamage(damage, push, Color.Lerp(Color, Color.white, 0.4f));

        if (Fusions.Has(Fusion.Conduit) && Element == Element.Arcane && Random.value < 0.35f)
        {
            var next = Enemy.Nearest(e.Position, 3.5f, new[] { e });
            if (next != null)
            {
                Fx.Bolt(e.Position, next.Position, ElementColor(Element.Storm));
                next.TakeDamage(damage * 0.6f);
            }
        }
    }

    protected Projectile Shoot(Vector2 from, Vector2 dir, float speed, float radius)
    {
        var p = Projectile.Spawn(from, dir.normalized * speed, Team.Player, Color, radius);
        p.Damage = 0f;   // damage goes through Hit() so runes and fusions apply
        p.Pierce = PierceCount;
        return p;
    }

    protected static List<Enemy> EnemiesWithin(Vector2 center, float radius)
    {
        var list = new List<Enemy>();
        foreach (var e in Enemy.All)
        {
            if (!e.IsAlive || e.Hidden) continue;
            float r = radius + e.Radius;
            if ((e.Position - center).sqrMagnitude < r * r) list.Add(e);
        }
        return list;
    }
}
