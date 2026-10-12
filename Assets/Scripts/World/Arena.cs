using System.Collections.Generic;
using UnityEngine;

// The play field: a square clearing with a wall of trees around it and trees scattered inside.
// Trees are circle obstacles that push the wizard and walking enemies out (spells fly over them).
public class Arena : MonoBehaviour
{
    public const float HalfSize = 22f;

    public Biome Biome { get; private set; }
    public Rect Bounds => new Rect(-HalfSize, -HalfSize, HalfSize * 2f, HalfSize * 2f);

    readonly List<Vector3> obstacles = new List<Vector3>();  // x, y, radius

    public static Arena Create(Biome biome)
    {
        var a = new GameObject("Arena").AddComponent<Arena>();
        a.Biome = biome;
        a.Build();
        return a;
    }

    void Build()
    {
        var b = Biome;
        SpriteFactory.Child(transform, "Ground", SpriteFactory.Square, b.Ground, Vector2.zero, HalfSize * 2f + 30f, -100);

        for (int i = 0; i < 500; i++)
        {
            var p = new Vector2(Random.Range(-HalfSize, HalfSize), Random.Range(-HalfSize, HalfSize));
            SpriteFactory.Child(transform, "Speck", SpriteFactory.Circle, b.GroundSpeck, p, Random.Range(0.08f, 0.35f), -99);
        }
        for (int i = 0; i < 90; i++)
        {
            var p = new Vector2(Random.Range(-HalfSize, HalfSize), Random.Range(-HalfSize, HalfSize));
            var c = b.Flowers[Random.Range(0, b.Flowers.Length)];
            SpriteFactory.Child(transform, "Flower", SpriteFactory.Circle, c, p, Random.Range(0.08f, 0.14f), -98);
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
    }

    void AddTree(Vector2 p, float r, bool solid)
    {
        var b = Biome;
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.3f), p + new Vector2(0.25f, -0.3f), r * 2.1f, -45);
        SpriteFactory.Child(transform, "Canopy", SpriteFactory.Circle, b.ObstacleShade, p, r * 2f, -40);
        SpriteFactory.Child(transform, "Leaves", SpriteFactory.Circle, b.ObstacleColor, p + new Vector2(-0.12f, 0.15f) * r, r * 1.6f, -39);
        SpriteFactory.Child(transform, "Light", SpriteFactory.Circle, b.ObstacleColor * 1.35f, p + new Vector2(-0.3f, 0.35f) * r, r * 0.7f, -38);
        if (solid) obstacles.Add(new Vector3(p.x, p.y, r * 0.75f)); // trunk is smaller than the canopy
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
