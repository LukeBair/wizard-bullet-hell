using System.Collections.Generic;
using UnityEngine;

// Variant B, hands-on: each level you take a rune stone, then choose which spell to socket it into.
// While holding a rune, every spell shows what that rune would do to it. Each spell has 4 sockets,
// and three matching runes in one spell Resonate for a free extra copy.
public class RuneSystem : UpgradeSystem
{
    public override UpgradeMode Mode => UpgradeMode.Runes;
    public override string Name => "Rune Sockets";
    public override string Pitch => "Take a rune, then slot it into the spell of your choice.";

    const int Sockets = 4;

    readonly Dictionary<Spell, List<Rune>> sockets = new Dictionary<Spell, List<Rune>>();
    readonly List<Rune> runeOffer = new List<Rune>();
    SpellId? tome;           // a new spell offered alongside the runes
    Rune? held;              // chosen rune waiting for a socket

    public override void Begin(Player player)
    {
        base.Begin(player);
        sockets.Clear();
    }

    List<Rune> SocketsOf(Spell s)
    {
        if (!sockets.TryGetValue(s, out var list)) sockets[s] = list = new List<Rune>();
        return list;
    }

    public override void Open()
    {
        held = null;
        runeOffer.Clear();
        var pool = new List<Rune>(AllRunes);
        Shuffle(pool);
        runeOffer.AddRange(pool.GetRange(0, 3));

        // Every other level (and whenever nothing has room) a spell tome joins the offer.
        var unlearned = UnlearnedSpells();
        bool anyRoom = Player.Spells.Exists(s => SocketsOf(s).Count < Sockets);
        tome = null;
        if (Player.Spells.Count < MaxSpells && unlearned.Count > 0 && (Player.Level % 2 == 0 || !anyRoom))
            tome = unlearned[Random.Range(0, unlearned.Count)];
    }

    public override void AutoPick()
    {
        if (tome.HasValue && Random.value < 0.5f) { Player.Learn(tome.Value); return; }
        var open = Player.Spells.FindAll(s => SocketsOf(s).Count < Sockets);
        if (open.Count == 0) { if (tome.HasValue) Player.Learn(tome.Value); else Player.Heal(999f); return; }
        Socket(open[Random.Range(0, open.Count)], runeOffer[Random.Range(0, runeOffer.Count)]);
    }

    void Socket(Spell spell, Rune rune)
    {
        var list = SocketsOf(spell);
        list.Add(rune);
        spell.AddRune(rune);
        if (CountOf(list, rune) == 3)
        {
            spell.AddRune(rune);   // resonance: the third matching rune counts double
            GameManager.Instance.Toast($"{spell.Name} resonates with {Spell.RuneName(rune)}!");
        }
    }

    public override bool Draw()
    {
        return held == null ? DrawRuneChoice() : DrawSocketChoice();
    }

    bool DrawRuneChoice()
    {
        Header($"Level {Player.Level}", "Take a rune stone");
        var col = UI.Column(0.24f, 0.72f);
        float h = 150f * UI.S, gap = 20f * UI.S, y = col.y;
        bool anyRoom = Player.Spells.Exists(s => SocketsOf(s).Count < Sockets);

        foreach (var r in runeOffer)
        {
            var rect = new Rect(col.x, y, col.width, h);
            if (UI.Card(rect, $"{Spell.RuneName(r)} Rune", RuneSummary(r), Spell.RuneColor(r), "RUNE", anyRoom))
            {
                held = r;
                Sfx.Play(Sound.Select, 0.6f);
                Haptics.Selection();
            }
            y += h + gap;
        }

        if (tome.HasValue)
        {
            var preview = Spell.Preview(tome.Value);
            var rect = new Rect(col.x, y, col.width, h);
            if (UI.Card(rect, $"Tome: {preview.Name}", preview.Blurb + " (4 empty sockets)", preview.Color, "NEW SPELL"))
            {
                Player.Learn(tome.Value);
                return true;
            }
            y += h + gap;
        }

        if (!anyRoom && !tome.HasValue)
        {
            if (UI.Card(new Rect(col.x, y, col.width, h), "Rest", "All sockets are full. Heal fully instead.", new Color(1f, 0.4f, 0.55f)))
            {
                Player.Heal(999f);
                return true;
            }
        }
        return false;
    }

    bool DrawSocketChoice()
    {
        var rune = held.Value;
        Header($"{Spell.RuneName(rune)} Rune", "Tap a spell to socket it");
        var col = UI.Column(0.22f, 0.74f);
        float h = 170f * UI.S, gap = 18f * UI.S, y = col.y;
        float s = UI.S;

        foreach (var spell in Player.Spells)
        {
            var list = SocketsOf(spell);
            bool room = list.Count < Sockets;
            bool resonates = room && CountOf(list, rune) == 2;
            string effect = room ? spell.RuneEffect(rune) + (resonates ? "  <color=#ffd966>RESONANCE x2!</color>" : "") : "No empty sockets";
            var rect = new Rect(col.x, y, col.width, h);
            bool tapped = UI.Card(rect, spell.Name, effect, spell.Color, $"LV {spell.Level}", room);

            // Socket row
            float d = 44f * s;
            for (int i = 0; i < Sockets; i++)
            {
                var c = new Vector2(rect.x + 44f * s + i * (d + 14f * s), rect.yMax - 38f * s);
                UI.Circle(c, d * 0.5f, new Color(1f, 1f, 1f, 0.15f));
                if (i < list.Count)
                {
                    UI.Circle(c, d * 0.42f, Spell.RuneColor(list[i]));
                    UI.Text(new Rect(c.x - d, c.y - d * 0.5f, d * 2f, d), Spell.RuneName(list[i]).Substring(0, 1), 22, TextAnchor.MiddleCenter, Color.black);
                }
                else if (i == list.Count && room)
                {
                    float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                    UI.Circle(c, d * 0.42f, new Color(Spell.RuneColor(rune).r, Spell.RuneColor(rune).g, Spell.RuneColor(rune).b, pulse));
                }
            }

            if (tapped)
            {
                Socket(spell, rune);
                return true;
            }
            y += h + gap;
        }

        if (UI.Button(new Rect(col.x, y + 10f * s, col.width, 80f * s), "Back", new Color(0.6f, 0.6f, 0.7f)))
            held = null;
        return false;
    }

    static int CountOf(List<Rune> list, Rune r)
    {
        int n = 0;
        foreach (var x in list) if (x == r) n++;
        return n;
    }

    static string RuneSummary(Rune r)
    {
        switch (r)
        {
            case Rune.Power: return "More damage.";
            case Rune.Haste: return "Faster casting.";
            case Rune.Split: return "More projectiles, bolts, pulses or wisps.";
            case Rune.Pierce: return "Pass through, chain further or push harder.";
            case Rune.Area: return "Bigger explosions, auras and orbits.";
            case Rune.Echo: return "The spell casts a second time.";
            case Rune.Ember: return "Hits set foes on fire.";
            default: return "Hits chill and slow foes.";
        }
    }
}
