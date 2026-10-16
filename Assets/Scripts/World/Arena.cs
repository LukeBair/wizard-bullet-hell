using System.Collections.Generic;
using UnityEngine;

// The play field: a square area walled in by obstacles (trees, pillars or rocks depending on the
// biome) with more scattered inside. Obstacles are circles that push the wizard and walking enemies
// out (spells fly over them).
public class Arena : MonoBehaviour
{
    public const float HalfSize = 22f;

    public Biome Biome { get; private set; }
    public Rect Bounds => new Rect(-HalfSize, -HalfSize, HalfSize * 2f, HalfSize * 2f);

    readonly List<Vector3> obstacles = new List<Vector3>();  // x, y, radius

    public static Arena Create(Biome biome)
    {
        var a = new GameObject("Arena").AddComponent<Arena>();
        ArtStyle.Restyle(biome);
        a.Biome = biome;
        a.Build();
        return a;
    }

    void Build()
    {
        var b = Biome;
        bool painted = BuildGround();
        if (!painted) SpriteFactory.Child(transform, "Ground", SpriteFactory.Square, b.Ground, Vector2.zero, HalfSize * 2f + 30f, -100);

        for (int i = 0; i < (painted ? 0 : 500); i++)
        {
            var p = new Vector2(Random.Range(-HalfSize, HalfSize), Random.Range(-HalfSize, HalfSize));
            SpriteFactory.Child(transform, "Speck", ArtStyle.Round, b.GroundSpeck, p, Random.Range(0.08f, 0.35f), -99);
        }
        if (b.Style == ObstacleStyle.Pillar) BuildFloorTiles();
        for (int i = 0; i < 90; i++)
        {
            var p = new Vector2(Random.Range(-HalfSize, HalfSize), Random.Range(-HalfSize, HalfSize));
            var c = b.Flowers[Random.Range(0, b.Flowers.Length)];
            switch (b.Style)
            {
                case ObstacleStyle.Rock:
                    // Glowing crystal clusters.
                    SpriteFactory.Child(transform, "CrystalGlow", SpriteFactory.Glow, new Color(c.r, c.g, c.b, 0.35f), p, 1.2f, -97);
                    if (i % 3 == 0) Lighting.Point(transform, c, 2.6f, 0.8f, p);
                    for (int k = 0; k < 3; k++)
                    {
                        var shard = SpriteFactory.Child(transform, "Crystal", SpriteFactory.Diamond, c, p + Random.insideUnitCircle * 0.2f, 0.25f, -96);
                        shard.transform.localScale = new Vector3(0.14f, Random.Range(0.25f, 0.45f), 1f);
                        shard.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-35f, 35f));
                    }
                    break;
                case ObstacleStyle.Pillar:
                    if (i % 3 != 0) break;   // fewer candles than flowers
                    SpriteFactory.Child(transform, "CandleGlow", SpriteFactory.Glow, new Color(1f, 0.8f, 0.4f, 0.3f), p, 1.4f, -95);
                    Lighting.Point(transform, new Color(1f, 0.75f, 0.4f), 3f, 0.85f, p);
                    SpriteFactory.Child(transform, "Candle", SpriteFactory.Square, new Color(0.95f, 0.9f, 0.8f), p, 0.12f, -94).transform.localScale = new Vector3(0.12f, 0.25f, 1f);
                    SpriteFactory.Child(transform, "Flame", SpriteFactory.Circle, c, p + new Vector2(0f, 0.17f), 0.1f, -93);
                    break;
                default:
                    if (UseForestProps && i % 6 == 0)
                    {
                        // glowing mushroom logs instead of every sixth flower
                        var shroom = ArtStyle.Prop(PropFile("mushrooms", Random.Range(0, 6)));
                        if (shroom != null)
                        {
                            SpriteFactory.Child(transform, "Mushrooms", shroom, Color.white, p, 1.1f / (shroom.rect.width / shroom.pixelsPerUnit), 0).spriteSortPoint = SpriteSortPoint.Pivot;
                            Lighting.Point(transform, new Color(0.45f, 0.75f, 1f), 2.4f, 0.7f, p + new Vector2(0f, 0.4f));
                            break;
                        }
                    }
                    SpriteFactory.Child(transform, "Flower", ArtStyle.Round, c, p, Random.Range(0.08f, 0.14f), -98);
                    break;
            }
        }

        // Border wall: a ring of big trees just outside the playable area.
        for (float t = -HalfSize - 2f; t <= HalfSize + 2f; t += 1.6f)
        {
            AddTree(new Vector2(t, HalfSize + 1.6f), 1.5f, false);
            AddTree(new Vector2(t, -HalfSize - 1.6f), 1.5f, false);
            AddTree(new Vector2(HalfSize + 1.6f, t), 1.5f, false);
            AddTree(new Vector2(-HalfSize - 1.6f, t), 1.5f, false);
        }

        // Scattered trees, kept away from the centre so the wizard starts in the open.
        int placed = 0, tries = 0;
        while (placed < b.ObstacleCount && tries++ < 500)
        {
            var p = new Vector2(Random.Range(-HalfSize + 2f, HalfSize - 2f), Random.Range(-HalfSize + 2f, HalfSize - 2f));
            float r = Random.Range(b.ObstacleSize.x, b.ObstacleSize.y);
            if (p.magnitude < 5f || Blocked(p, r + 2.5f)) continue;
            AddTree(p, r, true);
            placed++;
        }

        // Lighting: the floor and obstacles take light; glows, crystals and flames stay self-lit.
        if (Lighting.Active)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                if (!sr.name.Contains("Glow") && sr.name != "Crystal" && sr.name != "Flame" && sr.name != "Candle" && sr.name != "Mushrooms")
                    Lighting.MakeLit(sr);
            Lighting.Ambient(transform, b.ObstacleColor);
        }
    }

    // Stone floor: a faint grid of tiles plus a long rug down the middle.
    void BuildFloorTiles()
    {
        var line = new Color(0f, 0f, 0f, 0.18f);
        for (float t = -HalfSize; t <= HalfSize; t += 2f)
        {
            SpriteFactory.Child(transform, "TileH", SpriteFactory.Square, line, new Vector2(0f, t), 1f, -99).transform.localScale = new Vector3(HalfSize * 2f, 0.05f, 1f);
            SpriteFactory.Child(transform, "TileV", SpriteFactory.Square, line, new Vector2(t, 0f), 1f, -99).transform.localScale = new Vector3(0.05f, HalfSize * 2f, 1f);
        }
        var rug = SpriteFactory.Child(transform, "Rug", SpriteFactory.Square, new Color(0.45f, 0.12f, 0.18f), Vector2.zero, 1f, -98);
        rug.transform.localScale = new Vector3(4f, HalfSize * 2f, 1f);
        var trim = SpriteFactory.Child(transform, "RugTrim", SpriteFactory.Square, new Color(0.85f, 0.65f, 0.25f), Vector2.zero, 1f, -97);
        trim.transform.localScale = new Vector3(3.4f, HalfSize * 2f, 1f);
        var inner = SpriteFactory.Child(transform, "RugInner", SpriteFactory.Square, new Color(0.5f, 0.14f, 0.2f), Vector2.zero, 1f, -96);
        inner.transform.localScale = new Vector3(3.2f, HalfSize * 2f, 1f);
    }

    // Painted ground (tools/sprite-pipeline/ground_prep.py): a seamless base tile under soft-edged patches
    // of every ground texture, each at a random scale, rotation and strength, so no two runs and no two
    // spots on the floor repeat. Only with the 3D Character art; false if the zone has no textures.
    bool BuildGround()
    {
        if (!Settings.Enemy3D || ArtStyle.Current != ArtStyleId.Dusk) return false;
        string zone = Biome.Name == "Whispering Forest" ? "forest" : null;
        var baseTex = zone == null ? null : Resources.Load<Texture2D>($"Art/Dusk/ground_{zone}_base");
        if (baseTex == null) return false;
        float ppu = Tune.F("ground.pixelsPerUnit", 20f);
        baseTex.wrapMode = TextureWrapMode.Repeat;
        var tile = Sprite.Create(baseTex, new Rect(0, 0, baseTex.width, baseTex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        var ground = SpriteFactory.Child(transform, "Ground", tile, Color.white, Vector2.zero, 1f, -100);
        ground.drawMode = SpriteDrawMode.Tiled;
        ground.size = Vector2.one * (HalfSize * 2f + 30f);

        var patches = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; i < 8; i++)
            for (int v = 0; v < 2; v++)
            {
                var t = Resources.Load<Texture2D>($"Art/Dusk/ground_{zone}_patch_{i}_{v}");
                if (t != null) patches.Add(Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu));
            }
        if (patches.Count == 0) return true;
        float extent = HalfSize + 12f, size = baseTex.width / ppu;
        int count = Mathf.RoundToInt(extent * extent * 4f / (size * size) * Tune.F("ground.patchDensity", 2.2f));
        for (int i = 0; i < count; i++)
        {
            var p = new Vector2(Random.Range(-extent, extent), Random.Range(-extent, extent));
            var sr = SpriteFactory.Child(transform, "GroundPatch", patches[Random.Range(0, patches.Count)], new Color(1f, 1f, 1f, Random.Range(0.55f, 1f)), p, Random.Range(0.7f, 1.7f), -99);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            sr.flipX = Random.value < 0.5f;
        }
        return true;
    }

    // Whispering Forest props from the 3D pipeline: big obstacles are trees, smaller ones boulders and
    // shrine stones. The sprite's base sits on the collision circle; widths are in units of r.
    static readonly (string name, int variants, float width, float minR)[] ForestProps =
    {
        ("tree_oak", 6, 2.4f, 1.0f),
        ("boulders_a", 6, 2.3f, 0f), ("boulders_b", 6, 2.6f, 0f), ("boulders_c", 6, 2.3f, 0f),
        ("shrine_a", 6, 1.4f, 0f), ("shrine_b", 6, 1.5f, 0f),
    };

    // Each prop is rendered from six angles, in a sharp tier (128px Character art) and a chunky one (64px).
    static string PropFile(string name, int variant) => $"prop_{name}_{variant}_{(Settings.Enemy3DSharp ? "hi" : "lo")}";

    bool UseForestProps => Biome.Name == "Whispering Forest" && ArtStyle.Prop(PropFile("tree_oak", 0)) != null;

    bool AddProp(Vector2 p, float r, bool solid)
    {
        var options = System.Array.FindAll(ForestProps, f => solid ? (r >= 1.0f) == (f.minR >= 1.0f) : f.minR >= 1.0f);
        var pick = options[Random.Range(0, options.Length)];
        var sprite = ArtStyle.Prop(PropFile(pick.name, Random.Range(0, pick.variants)));
        if (sprite == null) return false;
        var basePos = p - new Vector2(0f, r * 0.5f);
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), basePos + new Vector2(0.15f, 0.05f), r * 2f, -45)
            .transform.localScale = new Vector3(r * 2.2f, r * 1.1f, 1f);
        float scale = pick.width * r / (sprite.rect.width / sprite.pixelsPerUnit);
        var sr = SpriteFactory.Child(transform, "Prop", sprite, Color.white, basePos, scale, 0);   // depth-sorted at its base
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        sr.flipX = Random.value < 0.5f;
        if (solid) obstacles.Add(new Vector3(p.x, p.y, r * 0.7f));
        return true;
    }

    void AddTree(Vector2 p, float r, bool solid)
    {
        var b = Biome;
        if (UseForestProps && AddProp(p, r, solid)) return;
        var art = ArtStyle.Get(ArtStyle.ObstacleSprite(b.Style));
        if (art != null)
        {
            SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.3f), p + new Vector2(0.25f, -0.3f), r * 2.1f, -45);
            var sr = SpriteFactory.Child(transform, "Obstacle", art, Color.white, p, r, -40);
            sr.flipX = Random.value < 0.5f;
            if (solid) obstacles.Add(new Vector3(p.x, p.y, r * (b.Style == ObstacleStyle.Tree ? 0.75f : 0.8f)));
            return;
        }
        if (b.Style == ObstacleStyle.Pillar) { AddPillar(p, r, solid); return; }
        if (b.Style == ObstacleStyle.Rock) { AddRock(p, r, solid); return; }
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.3f), p + new Vector2(0.25f, -0.3f), r * 2.1f, -45);
        SpriteFactory.Child(transform, "Canopy", SpriteFactory.Circle, b.ObstacleShade, p, r * 2f, -40);
        SpriteFactory.Child(transform, "Leaves", SpriteFactory.Circle, b.ObstacleColor, p + new Vector2(-0.12f, 0.15f) * r, r * 1.6f, -39);
        SpriteFactory.Child(transform, "Light", SpriteFactory.Circle, b.ObstacleColor * 1.35f, p + new Vector2(-0.3f, 0.35f) * r, r * 0.7f, -38);
        if (solid) obstacles.Add(new Vector3(p.x, p.y, r * 0.75f)); // trunk is smaller than the canopy
    }

    // Academy: round stone pillars with a capital.
    void AddPillar(Vector2 p, float r, bool solid)
    {
        var b = Biome;
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), p + new Vector2(0.25f, -0.3f), r * 2f, -45);
        SpriteFactory.Child(transform, "Base", SpriteFactory.Square, b.ObstacleShade, p, r * 1.9f, -41);
        SpriteFactory.Child(transform, "Column", SpriteFactory.Circle, b.ObstacleColor, p, r * 1.6f, -40);
        SpriteFactory.Child(transform, "Fluting", SpriteFactory.Ring, b.ObstacleShade, p, r * 1.2f, -39);
        SpriteFactory.Child(transform, "Light", SpriteFactory.Circle, b.ObstacleColor * 1.3f, p + new Vector2(-0.25f, 0.25f) * r, r * 0.5f, -38);
        if (solid) obstacles.Add(new Vector3(p.x, p.y, r * 0.85f));
    }

    // Caves: lumpy boulders, sometimes crowned with a crystal.
    void AddRock(Vector2 p, float r, bool solid)
    {
        var b = Biome;
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), p + new Vector2(0.25f, -0.3f), r * 2.1f, -45);
        for (int i = 0; i < 3; i++)
        {
            var off = new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.3f, 0.3f)) * r;
            SpriteFactory.Child(transform, "Rock", SpriteFactory.Circle, Color.Lerp(b.ObstacleShade, b.ObstacleColor, i / 2f), p + off, r * Random.Range(1.2f, 1.7f), -42 + i);
        }
        if (Random.value < 0.4f)
        {
            var c = b.Flowers[Random.Range(0, b.Flowers.Length)];
            SpriteFactory.Child(transform, "Glow", SpriteFactory.Glow, new Color(c.r, c.g, c.b, 0.4f), p, r * 2f, -38);
            var shard = SpriteFactory.Child(transform, "Crystal", SpriteFactory.Diamond, c, p + new Vector2(0.1f, 0.2f) * r, r * 0.5f, -37);
            shard.transform.localScale = new Vector3(r * 0.3f, r * 0.7f, 1f);
        }
        if (solid) obstacles.Add(new Vector3(p.x, p.y, r * 0.8f));
    }

    public bool Blocked(Vector2 p, float radius)
    {
        foreach (var o in obstacles)
        {
            float r = o.z + radius;
            if (((Vector2)o - p).sqrMagnitude < r * r) return true;
        }
        return false;
    }

    // Moves a circle out of any trees and back inside the arena.
    public Vector2 PushOut(Vector2 p, float radius)
    {
        foreach (var o in obstacles)
        {
            Vector2 d = p - (Vector2)o;
            float r = o.z + radius;
            float sq = d.sqrMagnitude;
            if (sq < r * r)
            {
                float len = Mathf.Sqrt(sq);
                p = (Vector2)o + (len > 0.0001f ? d / len : Vector2.up) * r;
            }
        }
        p.x = Mathf.Clamp(p.x, -HalfSize + radius, HalfSize - radius);
        p.y = Mathf.Clamp(p.y, -HalfSize + radius, HalfSize - radius);
        return p;
    }

    public bool Inside(Vector2 p, float margin = 0f) =>
        Mathf.Abs(p.x) < HalfSize + margin && Mathf.Abs(p.y) < HalfSize + margin;
}
