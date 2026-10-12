using UnityEngine;

public enum ObstacleStyle { Tree, Pillar, Rock }

// Everything that makes one setting look different. Biomes are cosmetic for now (same enemies),
// except that each one decides which boss arrives first. One is picked at random per run.
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
    };
}
