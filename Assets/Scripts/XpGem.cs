using System.Collections.Generic;
using UnityEngine;

// Dropped by enemies. Flies to the wizard once inside the pickup radius.
public class XpGem : MonoBehaviour
{
    public static readonly List<XpGem> All = new List<XpGem>();

    int value;
    bool magnet;
    float speed;

    public static void Spawn(Vector2 pos, int value)
    {
        var go = new GameObject("Gem");
        go.transform.position = pos + Random.insideUnitCircle * 0.2f;
        var g = go.AddComponent<XpGem>();
        g.value = value;
        Color c = value >= 20 ? new Color(1f, 0.8f, 0.3f) : value >= 5 ? new Color(0.4f, 1f, 0.6f) : new Color(0.45f, 0.75f, 1f);
        float size = value >= 20 ? 0.5f : value >= 5 ? 0.38f : 0.26f;
        SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(c.r, c.g, c.b, 0.4f), Vector2.zero, size * 2.2f, 8);
        SpriteFactory.Child(go.transform, "Gem", ArtStyle.Gem, c, Vector2.zero, size, 9);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Vacuum: every gem on the map flies in (boss reward).
    public static void MagnetAll()
    {
        foreach (var g in All) g.magnet = true;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        var player = gm.Player;
        if (player == null || !player.IsAlive) return;

        Vector2 to = player.Position - (Vector2)transform.position;
        float dist = to.magnitude;
        if (!magnet && dist < player.PickupRadius) magnet = true;
        if (!magnet) return;

        speed += 30f * Time.deltaTime;
        if (dist < 0.4f)
        {
            player.AddXp(value);
            Sfx.Play(Sound.Gem, 0.3f, 0.15f);
            Destroy(gameObject);
            return;
        }
        transform.position += (Vector3)(to / dist * Mathf.Min(speed * Time.deltaTime, dist));
    }
}
