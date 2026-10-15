using System.Collections.Generic;
using UnityEngine;

public enum ObstacleStyle { Tree, Pillar, Rock }

// Everything that makes one setting look different. A run passes through four zones: the first three
// in random order, each ending with its own boss, then the Witch's Hollow for the final boss. Every
// zone has the same enemies, but some medium enemies show up more often in their home zone.
public class Biome
{
    public string Name;
    public Color Ground;
    public Color GroundSpeck;
    public Color ObstacleColor;
    public Color ObstacleShade;
    public Color[] Flowers;        // small ground details: flowers, candles, crystals
    public ObstacleStyle Style;
    public int ObstacleCount;
    public Vector2 ObstacleSize;   // min/max radius
    public EnemyRoster Roster;
    public string[] Favored = new string[0];   // medium enemies that are more common here
    public bool Final;                         // the last zone of a run
    public string[] Music = new string[0];     // takes of this zone's track in Resources/Music/; none = silence
    string track;

    // The take this zone plays, picked once per visit (each run and zone change makes a new Biome).
    public string Track => track ??= Music.Length == 0 ? null : Music[UnityEngine.Random.Range(0, Music.Length)];

    // The zones of one run: the three regular ones shuffled, then the final zone.
    public static List<Biome> RunZones()
    {
        var zones = new List<Biome> { Forest, Academy, Caves };
        for (int i = zones.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (zones[i], zones[j]) = (zones[j], zones[i]);
        }
        zones.Add(Hollow);
        return zones;
    }

    public static Biome Random()
    {
        switch (UnityEngine.Random.Range(0, 3))
        {
            case 0: return Forest;
            case 1: return Academy;
            default: return Caves;
        }
    }

    public static Biome Forest => new Biome
    {
        Name = "Whispering Forest",
        Ground = new Color(0.13f, 0.22f, 0.12f),
        GroundSpeck = new Color(0.2f, 0.32f, 0.16f),
        ObstacleColor = new Color(0.09f, 0.3f, 0.14f),
        ObstacleShade = new Color(0.05f, 0.17f, 0.08f),
        Flowers = new[] { new Color(1f, 0.85f, 0.4f), new Color(0.9f, 0.5f, 0.9f), new Color(0.6f, 0.8f, 1f) },
        Style = ObstacleStyle.Tree,
        ObstacleCount = 34,
        ObstacleSize = new Vector2(0.7f, 1.4f),
        Roster = EnemyRoster.Standard("Elder Treant"),
        Favored = new[] { "Thornback Wolf", "Root Burrower", "Mother Slime" },
        Music = new[] { "music_forest_1", "music_forest_2" },
    };

    public static Biome Academy => new Biome
    {
        Name = "Arcane Academy",
        Ground = new Color(0.2f, 0.18f, 0.24f),
        GroundSpeck = new Color(0.26f, 0.23f, 0.31f),
        ObstacleColor = new Color(0.5f, 0.45f, 0.55f),
        ObstacleShade = new Color(0.3f, 0.26f, 0.34f),
        Flowers = new[] { new Color(1f, 0.85f, 0.45f), new Color(1f, 0.7f, 0.3f) },   // candle flames
        Style = ObstacleStyle.Pillar,
        ObstacleCount = 26,
        ObstacleSize = new Vector2(0.7f, 1.0f),
        Roster = EnemyRoster.Standard("Headmaster Vexley"),
        Favored = new[] { "Goblin Hexer", "Rogue Apprentice", "Pixie Trickster" },
        Music = new[] { "music_wizard_school_1", "music_wizard_school_2" },
    };

    public static Biome Caves => new Biome
    {
        Name = "Crystal Caves",
        Ground = new Color(0.12f, 0.12f, 0.16f),
        GroundSpeck = new Color(0.17f, 0.17f, 0.22f),
        ObstacleColor = new Color(0.35f, 0.33f, 0.4f),
        ObstacleShade = new Color(0.2f, 0.19f, 0.24f),
        Flowers = new[] { new Color(0.5f, 0.95f, 1f), new Color(0.75f, 0.55f, 1f) },   // glowing crystals
        Style = ObstacleStyle.Rock,
        ObstacleCount = 30,
        ObstacleSize = new Vector2(0.6f, 1.5f),
        Roster = EnemyRoster.Standard("Crystal Golem"),
        Favored = new[] { "Root Burrower", "Spore Lobber", "Mother Slime" },
        Music = new[] { "music_caves_1", "music_caves_2" },
    };

    // Final zone: a dark swamp wood, home of the Hedge Witch.
    public static Biome Hollow => new Biome
    {
        Name = "Witch's Hollow",
        Ground = new Color(0.1f, 0.12f, 0.1f),
        GroundSpeck = new Color(0.16f, 0.18f, 0.13f),
        ObstacleColor = new Color(0.2f, 0.22f, 0.16f),
        ObstacleShade = new Color(0.1f, 0.12f, 0.08f),
        Flowers = new[] { new Color(0.6f, 1f, 0.5f), new Color(0.75f, 0.4f, 0.9f) },   // will-o'-wisps
        Style = ObstacleStyle.Tree,
        ObstacleCount = 38,
        ObstacleSize = new Vector2(0.6f, 1.3f),
        Roster = EnemyRoster.Standard("The Hedge Witch"),
        Favored = new[] { "Spore Lobber", "Pixie Trickster", "Goblin Hexer" },
        Music = new[] { "music_witchs_hollow_1", "music_witchs_hollow_2" },
        Final = true,
    };
}
