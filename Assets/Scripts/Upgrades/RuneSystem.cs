using System.Collections.Generic;
using UnityEngine;

// Rune Sockets: each level you take a rune stone, then choose which spell to socket it into.
// While holding a rune, every spell shows what that rune would do to it. Every spell starts a run
// with 2 sockets and gains one more each time a boss dies. Three matching runes in one spell
// Resonate for a free extra copy.
// Split and Echo are rare. Spell tomes appear every other level. A spell whose sockets are full
// can Evolve once that evolution is unlocked in the Grimoire
// (every such spell gets its own Evolve card): its sockets empty for a stronger base form (up to
// tier III), keeping part of the runes' power.
public class RuneSystem : UpgradeSystem
{
    public override string Name => "Rune Sockets";

    static int Sockets => Mathf.Max(1, Tune.I("runes.startSockets", 2)
        + Tune.I("runes.socketsPerBoss", 1) * GameManager.Instance.Director.BossesDefeated);

    // Rarity: runes.<Rune>.weight. Rare runes default to a quarter of the common weight.
    static float Weight(Rune r) => Tune.F($"runes.{r}.weight", Spell.IsRare(r) ? 2.5f : 10f);
    // spells.<SpellId>.tomeWeight: relative chance a spell's tome is offered mid-run (default 1), so
    // build-carrying defensives can be rarer finds.
    static float TomeWeight(SpellId id) => Mathf.Max(0f, Tune.F($"spells.{id}.tomeWeight", 1f));

    static SpellId WeightedTome(List<SpellId> pool)
    {
        float total = 0f;
        foreach (var id in pool) total += TomeWeight(id);
        if (total <= 0f) return pool[Random.Range(0, pool.Count)];
        float roll = Random.value * total;
        foreach (var id in pool)
        {
            roll -= TomeWeight(id);
            if (roll <= 0f) return id;
        }
        return pool[pool.Count - 1];
    }

    readonly Dictionary<Spell, List<Rune>> sockets = new Dictionary<Spell, List<Rune>>();
    readonly List<Rune> runeOffer = new List<Rune>();
    SpellId? tome;           // a new spell offered alongside the runes
    readonly List<Spell> evolves = new List<Spell>();   // full spells that may evolve (every one is offered)
    Spell evolve => evolves.Count > 0 ? evolves[0] : null;
    Rune? held;              // chosen rune waiting for a socket

    public override void Begin(Player player)
    {
        base.Begin(player);
        sockets.Clear();
    }

    public override IReadOnlyList<Rune> SocketedRunes(Spell spell) => SocketsOf(spell);
    public override int SocketCount => Sockets;

    List<Rune> SocketsOf(Spell s)
    {
        if (!sockets.TryGetValue(s, out var list)) sockets[s] = list = new List<Rune>();
        return list;
    }

    public override void Open()
    {
        held = null;
        runeOffer.Clear();

        // Weighted draw without repeats: rare runes show up about a quarter as often.
        var pool = new List<Rune>(AllRunes);
        while (runeOffer.Count < 3 && pool.Count > 0)
        {
            float total = 0f;
            foreach (var r in pool) total += Weight(r);
            float roll = Random.value * total;
            foreach (var r in pool)
            {
                roll -= Weight(r);
                if (roll > 0f) continue;
                runeOffer.Add(r);
                pool.Remove(r);
                break;
            }
        }

        var unlearned = UnlearnedSpells();
        bool anyRoom = Player.Spells.Exists(s => SocketsOf(s).Count < Sockets);
        tome = null;
        if (Player.Spells.Count < MaxSpells && unlearned.Count > 0 && (Player.Level % Mathf.Max(1, Tune.I("runes.tomeEveryLevels", 2)) == 0 || !anyRoom))
            tome = WeightedTome(unlearned);

        // Tier III also waits for tier3Bosses boss kills, so it can't land before the first boss.
        int bosses = GameManager.Instance.Director.BossesDefeated;
        var ready = Player.Spells.FindAll(s => s.CanEvolve && s.Tier < Spell.MaxTier && Grimoire.HasEvolution(s.Id, s.Tier + 1) && SocketsOf(s).Count >= Sockets
            && (s.Tier + 1 < 3 || bosses >= Tune.I("runes.tier3Bosses", 1)));
        evolves.Clear();
        evolves.AddRange(ready);
        Telemetry.Offer(runeOffer, tome, evolves);
    }

    public override void AutoPick()
    {
        if (evolve != null && Random.value < 0.5f) { Evolve(evolve); return; }
        if (tome.HasValue && Random.value < 0.5f) { LearnTome(tome.Value); return; }
        var open = Player.Spells.FindAll(s => SocketsOf(s).Count < Sockets);
        if (open.Count == 0)
        {
            if (evolve != null) Evolve(evolve);
            else if (tome.HasValue) LearnTome(tome.Value);
            else Rest();
            return;
        }
        Socket(open[Random.Range(0, open.Count)], runeOffer[Random.Range(0, runeOffer.Count)]);
    }

    void Socket(Spell spell, Rune rune)
    {
        var list = SocketsOf(spell);
        list.Add(rune);
        spell.AddRune(rune);
        bool resonates = CountOf(list, rune) == Tune.I("runes.resonanceCount", 3);
        Telemetry.PickRune(rune, spell, resonates);
        if (resonates)
        {
            spell.AddRune(rune);   // resonance: the third matching rune counts double
            GameManager.Instance.Toast($"{spell.Name} resonates with {Spell.RuneName(rune)}!");
        }
    }

    void Evolve(Spell spell)
    {
        SocketsOf(spell).Clear();
        spell.Evolve();
        Telemetry.PickEvolve(spell);
        GameManager.Instance.Toast($"{spell.Name} evolved: {spell.Title}!");
        Fx.Ring(Player.Position, spell.Color, 3f, 0.6f);
        Fx.Burst(Player.Position, spell.Color, 5f);
    }

    void LearnTome(SpellId id)
    {
        Player.Learn(id);
        Telemetry.PickTome(id);
    }

    void Rest()
    {
        Player.Heal(999f, source: "Rest");
        Telemetry.PickRest();
    }

    public override bool Draw() => held == null ? DrawRuneChoice() : DrawSocketChoice();

    bool DrawRuneChoice()
    {
        Header($"Level {Player.Level}", "Take a rune stone", InteractionNote);
        var col = UI.Column(0.24f, 0.72f);
        // Shrink cards a little if several evolutions plus a tome all need room.
        int cards = runeOffer.Count + evolves.Count + (tome.HasValue ? 1 : 0);
        float gap = 16f * UI.S;
        float h = Mathf.Min(170f * UI.S, (col.height - gap * cards) / Mathf.Max(1, cards)), y = col.y;
        bool anyRoom = Player.Spells.Exists(s => SocketsOf(s).Count < Sockets);

        foreach (var r in runeOffer)
        {
            var rect = new Rect(col.x, y, col.width, h);
            bool rare = Weight(r) < 5f;
            if (UI.Card(rect, $"{Spell.RuneName(r)} Rune", RuneSummary(r), Spell.RuneColor(r), rare ? "★ RARE" : "RUNE", anyRoom))
            {
                held = r;
                Sfx.Play(Sound.Select, 0.6f);
                Haptics.Selection();
            }
            y += h + gap;
        }

        foreach (var spell in evolves)
        {
            var rect = new Rect(col.x, y, col.width, h);
            int next = spell.Tier + 1;
            int keep = Mathf.RoundToInt(Mathf.Clamp01(Tune.F("runes.evolveRetain", 0.5f)) * 100f);
            if (UI.Card(rect, $"{spell.Name} → {RomanTier(next)}: {spell.TierName(next)}", spell.TierDescription(next) + $" <i>(empties its sockets, keeps {keep}% of their power)</i>", new Color(1f, 0.85f, 0.35f), "EVOLVE"))
            {
                Evolve(spell);
                return true;
            }
            y += h + gap;
        }

        if (tome.HasValue)
        {
            var preview = Spell.Preview(tome.Value);
            var rect = new Rect(col.x, y, col.width, h);
            if (UI.Card(rect, $"Tome: {preview.Name}", preview.Blurb, preview.Color, "NEW SPELL"))
            {
                LearnTome(tome.Value);
                return true;
            }
            y += h + gap;
        }

        if (!anyRoom && !tome.HasValue && evolve == null)
        {
            if (UI.Card(new Rect(col.x, y, col.width, h), "Rest", "All sockets are full. Heal fully instead.", new Color(1f, 0.4f, 0.55f)))
            {
                Rest();
                return true;
            }
        }
        return false;
    }

    bool DrawSocketChoice()
    {
        var rune = held.Value;
        Header($"{Spell.RuneName(rune)} Rune", "Tap a spell to socket it", InteractionNote);
        var col = UI.Column(0.22f, 0.74f);
        float s = UI.S;
        float h = 172f * s, gap = 12f * s, y = col.y;

        foreach (var spell in Player.Spells)
        {
            var list = SocketsOf(spell);
            bool room = list.Count < Sockets;
            bool resonates = room && CountOf(list, rune) == Tune.I("runes.resonanceCount", 3) - 1;
            string effect = room ? spell.RuneEffect(rune) + (resonates ? "  <color=#ffd966>RESONANCE x2!</color>" : "") : "No empty sockets";
            string kept = spell.RetainedSummary();
            if (kept.Length > 0) effect += $"\n<size={Mathf.RoundToInt(18 * s)}><color=#ffd966>Kept: {kept}</color></size>";
            var rect = new Rect(col.x, y, col.width, h);
            string tag = spell.CanEvolve ? $"TIER {RomanTier(spell.Tier)}" : null;
            bool tapped = UI.Card(rect, spell.Title, effect, spell.Color, tag, room);

            // Socket row
            float d = 40f * s;
            for (int i = 0; i < Sockets; i++)
            {
                var c = new Vector2(rect.x + 44f * s + i * (d + 12f * s), rect.yMax - 32f * s);
                UI.Circle(c, d * 0.5f, new Color(1f, 1f, 1f, 0.15f));
                if (i < list.Count)
                {
                    UI.Circle(c, d * 0.42f, Spell.RuneColor(list[i]));
                    UI.Text(new Rect(c.x - d, c.y - d * 0.5f, d * 2f, d), Spell.RuneName(list[i]).Substring(0, 1), 20, TextAnchor.MiddleCenter, Color.black);
                }
                else if (i == list.Count && room)
                {
                    float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                    var rc = Spell.RuneColor(rune);
                    UI.Circle(c, d * 0.42f, new Color(rc.r, rc.g, rc.b, pulse));
                }
            }

            if (tapped)
            {
                Socket(spell, rune);
                return true;
            }
            y += h + gap;
        }

        if (UI.Button(new Rect(col.x, y + 6f * s, col.width, 76f * s), "Back", new Color(0.6f, 0.6f, 0.7f)))
            held = null;
        return false;
    }

    const string InteractionNote = "Runes may interact strangely with some spells.";

    static string RomanTier(int t) => t == 1 ? "I" : t == 2 ? "II" : "III";

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
            case Rune.Potency: return "More effective: damage, healing or duration, depending on the spell.";
            case Rune.Haste: return $"+{Tune.F("runes.Haste.castSpeedPerRune", 0.15f) * 100f:0}% cast speed: casts, heals or recharges more often.";
            case Rune.Split: return "More projectiles, bolts, pulses, wisps or beams.";
            case Rune.Pierce: return "Pass through, chain further, push harder or last longer. Varies by spell.";
            case Rune.Area: return "Bigger explosions, auras and orbits.";
            case Rune.Echo: return "The spell casts a second time. A few spells get a counter-spinning set of orbiters or an extra charge instead.";
            case Rune.Ember: return "Hits set foes on fire.";
            case Rune.Frost: return "Hits chill and slow foes.";
            case Rune.Curse: return $"Marks foes it hits: the next spell to hit them deals +{Tune.F("runes.Curse.bonusPerRune", 0.5f) * 100f:0}%.";
            case Rune.Infest: return $"Infests foes for {Tune.F("runes.Infest.duration", 1f):0.#}s. If they die, bugs burst out and hurt others.";
            case Rune.Overload: return $"Every {Tune.I("runes.Overload.every", 4)}th cast is overloaded: +{Tune.F("runes.Overload.bonusPerRune", 1.5f) * 100f:0}% damage or effect.";
            case Rune.Siphon: return "Hits heal you a little, up to a few HP a second.";
            case Rune.Ward: return "Casting grants a ward that blocks one enemy bullet.";
            default: return "Foes it hits may drop a big XP gem when they die.";
        }
    }
}
