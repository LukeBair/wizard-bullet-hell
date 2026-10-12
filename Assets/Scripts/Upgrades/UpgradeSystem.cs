using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum UpgradeMode { Draft, Runes, Elements }

// One way of turning level-ups into stronger spells. The three implementations share the same spells
// and runes, so they can be compared head to head; pick one on the title screen.
public abstract class UpgradeSystem
{
    public abstract UpgradeMode Mode { get; }
    public abstract string Name { get; }
    public abstract string Pitch { get; }   // one line for the title screen

    protected Player Player { get; private set; }

    public static UpgradeSystem Create(UpgradeMode mode)
    {
        switch (mode)
        {
            case UpgradeMode.Runes: return new RuneSystem();
            case UpgradeMode.Elements: return new ElementSystem();
            default: return new DraftSystem();
        }
    }

    public virtual void Begin(Player player)
    {
        Player = player;
        Fusions.Reset();
        player.Learn(SpellId.ArcaneMissile);
    }

    // Called each time a level-up screen opens.
    public abstract void Open();

    // Draws the level-up screen. Returns true once the player has finished choosing.
    public abstract bool Draw();

    // Makes a random choice without UI (used by the autoplay soak test).
    public abstract void AutoPick();

    // Optional extra HUD (e.g. element levels).
    public virtual void DrawHud(Rect area) { }

    protected List<SpellId> UnlearnedSpells()
    {
        var list = new List<SpellId>();
        foreach (var id in Spell.AllIds) if (Player.GetSpell(id) == null) list.Add(id);
        return list;
    }

    protected const int MaxSpells = 4;

    protected static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    protected static readonly Rune[] AllRunes = (Rune[])Enum.GetValues(typeof(Rune));

    protected void Header(string title, string subtitle)
    {
        UI.Dim();
        var col = UI.Column(0.1f, 0.14f);
        UI.Text(new Rect(col.x, col.y, col.width, 60f * UI.S), title, 48, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.4f));
        UI.Text(new Rect(col.x, col.y + 64f * UI.S, col.width, 80f * UI.S), subtitle, 26, TextAnchor.UpperCenter, new Color(0.85f, 0.85f, 0.95f), false);
    }
}
