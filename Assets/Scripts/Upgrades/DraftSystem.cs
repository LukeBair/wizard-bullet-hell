using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// Variant A, the genre baseline: pick 1 of 3 cards. A card is a new spell, or one rune on a spell you own.
public class DraftSystem : UpgradeSystem
{
    public override UpgradeMode Mode => UpgradeMode.Draft;
    public override string Name => "Spell Draft";
    public override string Pitch => "Pick 1 of 3 upgrade cards each level.";

    class Card
    {
        public string Title, Body, Tag;
        public Color Color;
        public Action Apply;
    }

    readonly List<Card> offer = new List<Card>();

    public override void Open()
    {
        var pool = new List<Card>();

        if (Player.Spells.Count < MaxSpells)
        {
            foreach (var id in UnlearnedSpells())
            {
                var preview = Spell.Preview(id);
                var spellId = id;
                pool.Add(new Card { Title = preview.Name, Body = preview.Blurb, Tag = "NEW SPELL", Color = preview.Color, Apply = () => Player.Learn(spellId) });
            }
        }

        foreach (var spell in Player.Spells)
        {
            foreach (var rune in AllRunes)
            {
                var s = spell;
                var r = rune;
                pool.Add(new Card
                {
                    Title = $"{s.Name}",
                    Body = $"<b>{Spell.RuneName(r)}:</b> {s.RuneEffect(r)}",
                    Tag = $"LV {s.Level} → {s.Level + 1}",
                    Color = s.Color,
                    Apply = () => s.AddRune(r),
                });
            }
        }

        pool.Add(new Card { Title = "Vitality", Body = "+20 max health and heal fully.", Tag = "WIZARD", Color = new Color(1f, 0.4f, 0.55f),
            Apply = () => { Player.MaxHealth += 20f; Player.Heal(999f); } });
        pool.Add(new Card { Title = "Swift Boots", Body = "+10% move speed and +30% pickup range.", Tag = "WIZARD", Color = new Color(0.6f, 0.9f, 0.7f),
            Apply = () => { Player.MoveSpeed *= 1.1f; Player.PickupRadius *= 1.3f; } });

        // New spells are rarer than rune cards but always at least one when you have room.
        offer.Clear();
        Shuffle(pool);
        var newSpell = pool.Find(c => c.Tag == "NEW SPELL");
        if (newSpell != null && Random.value < 0.6f) offer.Add(newSpell);
        foreach (var c in pool)
        {
            if (offer.Count >= 3) break;
            if (offer.Contains(c) || offer.Exists(o => o.Title == c.Title)) continue;  // one card per spell
            offer.Add(c);
        }
    }

    public override void AutoPick() => offer[Random.Range(0, offer.Count)].Apply();

    public override bool Draw()
    {
        Header($"Level {Player.Level}", "Choose an upgrade");
        var col = UI.Column(0.24f, 0.7f);
        float h = 190f * UI.S, gap = 24f * UI.S;
        for (int i = 0; i < offer.Count; i++)
        {
            var c = offer[i];
            var r = new Rect(col.x, col.y + i * (h + gap), col.width, h);
            if (UI.Card(r, c.Title, c.Body, c.Color, c.Tag))
            {
                c.Apply();
                return true;
            }
        }
        return false;
    }
}
