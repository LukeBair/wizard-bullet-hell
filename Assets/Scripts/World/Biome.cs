using UnityEngine;

// Everything that makes one setting look and play different. Forest is the only one so far;
// a Wizarding School or Caves biome is a new entry here plus its enemy roster in EnemyRoster.
public class Biome
{
    public string Name;
    public Color Ground;
    public Color GroundSpeck;
    public Color ObstacleColor;
    public Color ObstacleShade;
    public Color[] Flowers;
    public int ObstacleCount;
    public Vector2 ObstacleSize;   // min/max radius
    public EnemyRoster Roster;

    public static Biome Forest => new Biome
    {
        Name = "Whispering Forest",
        Ground = new Color(0.13f, 0.22f, 0.12f),
        GroundSpeck = new Color(0.2f, 0.32f, 0.16f),
        ObstacleColor = new Color(0.09f, 0.3f, 0.14f),
        ObstacleShade = new Color(0.05f, 0.17f, 0.08f),
        Flowers = new[] { new Color(1f, 0.85f, 0.4f), new Color(0.9f, 0.5f, 0.9f), new Color(0.6f, 0.8f, 1f) },
        ObstacleCount = 34,
        ObstacleSize = new Vector2(0.7f, 1.4f),
        Roster = EnemyRoster.Forest,
    };
}
