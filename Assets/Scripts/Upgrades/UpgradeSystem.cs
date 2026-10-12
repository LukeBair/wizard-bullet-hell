using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// Turns level-ups into stronger spells. Rune Sockets (RuneSystem) is the only one for now; the base
// class stays so other systems can be tried again later.
public abstract class UpgradeSystem
{
    public abstract string Name { get; }

    protected Player Player { get; private set; }

    // Spells that can open a run: the ones that deal damage on their own.
    static readonly SpellId[] Starters =
        { SpellId.ArcaneMissile, SpellId.Fireball, SpellId.FrostNova, SpellId.ChainLightning, SpellId.Wisps, SpellId.TidalLance, SpellId.Cyclone };

    readonly List<SpellId> startOffer = new List<SpellId>();

    // Starts a run with no spells; the player picks a starting tome first (DrawStart).
    public virtual void Begin(Player player)
    {
        Player = player;
        Interactions.Reset();
        startOffer.Clear();
        foreach (var id in Starters) if (Grimoire.HasTome(id)) startOffer.Add(id);
        Shuffle(startOffer);
        int n = Mathf.Clamp(Tune.I("run.startTomeChoices", 3), 1, startOffer.Count);
        startOffer.RemoveRange(n, startOffer.Count - n);
    }

    // The starting tome screen. Returns true once a tome is picked.
    public bool DrawStart()
    {
        Header("Choose your tome", "Your first spell. More come as you level up.");
        var col = UI.Column(0.24f, 0.72f);
        float h = 140f * UI.S, gap = 18f * UI.S, y = col.y;
        foreach (var id in startOffer)
        {
            var preview = Spell.Preview(id);
            if (UI.Card(new Rect(col.x, y, col.width, h), $"Tome: {preview.Name}", preview.Blurb, preview.Color, "START"))
            {
                Player.Learn(id);
                return true;
            }
            y += h + gap;
        }
        return false;
    }

    public void AutoPickStart() => Player.Learn(startOffer[Random.Range(0, startOffer.Count)]);

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
        foreach (var id in Spell.AllIds) if (Player.GetSpell(id) == null && Grimoire.HasTome(id)) list.Add(id);
        return list;
    }

    protected static int MaxSpells => Tune.I("runes.maxSpells", 5);

    protected static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    protected static readonly Rune[] AllRunes = (Rune[])Enum.GetValues(typeof(Rune));

    protected void Header(string title, string subtitle, string note = null)
    {
        UI.Dim();
        var col = UI.Column(0.1f, 0.14f);
        UI.Text(new Rect(col.x, col.y, col.width, 60f * UI.S), title, 48, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.4f));
        UI.Text(new Rect(col.x, col.y + 64f * UI.S, col.width, 36f * UI.S), subtitle, 26, TextAnchor.UpperCenter, new Color(0.85f, 0.85f, 0.95f), false);
        if (note != null)
            UI.Text(new Rect(col.x, col.y + 102f * UI.S, col.width, 30f * UI.S), $"<i>{note}</i>", 20, TextAnchor.UpperCenter, new Color(0.7f, 0.7f, 0.8f), false);
    }
}
