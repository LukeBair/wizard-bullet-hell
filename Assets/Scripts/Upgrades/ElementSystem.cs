using System.Collections.Generic;
using UnityEngine;

// Variant C, simplest choice with the deepest payoff: you only ever pick an element. Each element
// owns one spell that follows a fixed upgrade path, and any two elements at level 2+ fuse into a
// cross-spell bonus (Fire + Frost = Thermal Shock, and so on).
public class ElementSystem : UpgradeSystem
{
    public override UpgradeMode Mode => UpgradeMode.Elements;
    public override string Name => "Element Fusion";
    public override string Pitch => "Choose an element. Pair elements to unlock fusions.";

    const int FusionLevel = 2;
    const int MaxElements = 4;

    static readonly Dictionary<Element, SpellId> SpellFor = new Dictionary<Element, SpellId>
    {
        { Element.Arcane, SpellId.ArcaneMissile },
        { Element.Fire, SpellId.Fireball },
        { Element.Frost, SpellId.FrostNova },
        { Element.Storm, SpellId.ChainLightning },
        { Element.Nature, SpellId.Wisps },
    };

    // Rune granted at each element level after the first (level 2 gets [0], level 3 gets [1]...).
    static readonly Dictionary<Element, Rune[]> Path = new Dictionary<Element, Rune[]>
    {
        { Element.Arcane, new[] { Rune.Split, Rune.Power, Rune.Haste, Rune.Split, Rune.Pierce, Rune.Echo } },
        { Element.Fire, new[] { Rune.Power, Rune.Area, Rune.Split, Rune.Haste, Rune.Power, Rune.Echo } },
        { Element.Frost, new[] { Rune.Area, Rune.Power, Rune.Haste, Rune.Split, Rune.Area, Rune.Echo } },
        { Element.Storm, new[] { Rune.Pierce, Rune.Power, Rune.Split, Rune.Haste, Rune.Pierce, Rune.Echo } },
        { Element.Nature, new[] { Rune.Split, Rune.Power, Rune.Area, Rune.Split, Rune.Haste, Rune.Echo } },
    };

    readonly Dictionary<Element, int> levels = new Dictionary<Element, int>();
    readonly List<Element> offer = new List<Element>();

    int LevelOf(Element e) => levels.TryGetValue(e, out int l) ? l : 0;

    public override void Begin(Player player)
    {
        base.Begin(player);
        levels.Clear();
        levels[Element.Arcane] = 1;
    }

    public override void Open()
    {
        offer.Clear();
        var owned = new List<Element>();
        var fresh = new List<Element>();
        foreach (Element e in System.Enum.GetValues(typeof(Element)))
            (LevelOf(e) > 0 ? owned : fresh).Add(e);
        Shuffle(owned);
        Shuffle(fresh);

        // Mix what you have with something new, unless you're at the element cap.
        if (owned.Count < MaxElements && fresh.Count > 0) offer.Add(fresh[0]);
        foreach (var e in owned) if (offer.Count < 3) offer.Add(e);
        if (owned.Count < MaxElements)
            for (int i = 1; i < fresh.Count && offer.Count < 3; i++) offer.Add(fresh[i]);
    }

    public override void AutoPick() => Raise(offer[Random.Range(0, offer.Count)]);

    public override bool Draw()
    {
        Header($"Level {Player.Level}", "Attune to an element");
        var col = UI.Column(0.24f, 0.7f);
        float h = 210f * UI.S, gap = 22f * UI.S;
        for (int i = 0; i < offer.Count; i++)
        {
            var e = offer[i];
            var r = new Rect(col.x, col.y + i * (h + gap), col.width, h);
            int lvl = LevelOf(e);
            var preview = Player.GetSpell(SpellFor[e]) ?? Spell.Preview(SpellFor[e]);
            string body = lvl == 0 ? $"Learn {preview.Name}. {preview.Blurb}" : $"{preview.Name}: {preview.RuneEffect(NextRune(e, lvl + 1))}";
            foreach (var f in FusionsUnlockedBy(e, lvl + 1))
                body += $"\n<color=#ffd966>FUSION {f.Name}: {f.Effect}</color>";
            if (UI.Card(r, e.ToString(), body, Spell.ElementColor(e), lvl == 0 ? "NEW" : $"LV {lvl} → {lvl + 1}"))
            {
                Raise(e);
                return true;
            }
        }
        return false;
    }

    static Rune NextRune(Element e, int newLevel)
    {
        var path = Path[e];
        int i = newLevel - 2;
        return i < path.Length ? path[i] : (i % 2 == 0 ? Rune.Power : Rune.Haste);
    }

    List<Fusions.Def> FusionsUnlockedBy(Element e, int newLevel)
    {
        var list = new List<Fusions.Def>();
        if (newLevel != FusionLevel) return list;
        foreach (var f in Fusions.All)
        {
            if (Fusions.Has(f.Id)) continue;
            Element other;
            if (f.A == e) other = f.B; else if (f.B == e) other = f.A; else continue;
            if (LevelOf(other) >= FusionLevel) list.Add(f);
        }
        return list;
    }

    void Raise(Element e)
    {
        int newLevel = LevelOf(e) + 1;
        var unlocked = FusionsUnlockedBy(e, newLevel);
        levels[e] = newLevel;
        var spell = Player.Learn(SpellFor[e]);
        if (newLevel >= 2) spell.AddRune(NextRune(e, newLevel));
        foreach (var f in unlocked)
        {
            Fusions.Unlock(f.Id);
            GameManager.Instance.Toast($"Fusion: {f.Name}!");
        }
    }

    public override void DrawHud(Rect area)
    {
        // Element badges with level, plus a line per active fusion.
        float s = UI.S, d = 46f * s, x = area.x;
        foreach (Element e in System.Enum.GetValues(typeof(Element)))
        {
            int lvl = LevelOf(e);
            if (lvl == 0) continue;
            var c = new Vector2(x + d * 0.5f, area.y + d * 0.5f);
            UI.Circle(c, d * 0.5f, Spell.ElementColor(e));
            UI.Text(new Rect(c.x - d, c.y - d * 0.5f, d * 2f, d), lvl.ToString(), 24, TextAnchor.MiddleCenter, Color.black);
            x += d + 10f * s;
        }
        float y = area.y + d + 6f * s;
        foreach (var f in Fusions.All)
        {
            if (!Fusions.Has(f.Id)) continue;
            UI.Text(new Rect(area.x, y, area.width, 30f * s), f.Name, 20, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.4f));
            y += 26f * s;
        }
    }
}
