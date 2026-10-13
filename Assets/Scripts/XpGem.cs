using System.Collections.Generic;
using UnityEngine;

// Dropped by enemies. Flies to the wizard once inside the pickup radius.
public class XpGem : MonoBehaviour
{
    public static readonly List<XpGem> All = new List<XpGem>();

    int value;
    Color color;
    bool magnet;
    float speed;

    public static void Spawn(Vector2 pos, int value)
    {
        Telemetry.XpDropped(value);
        var go = new GameObject("Gem");
        go.transform.position = pos + Random.insideUnitCircle * 0.2f;
        var g = go.AddComponent<XpGem>();
        g.pos = go.transform.position;
        if (field == null) field = new GameObject("Gems").AddComponent<Field>();
        g.value = value;
        Color c = value >= 20 ? new Color(1f, 0.8f, 0.3f) : value >= 5 ? new Color(0.4f, 1f, 0.6f) : new Color(0.45f, 0.75f, 1f);
        g.color = c;
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

    // Zone change: every gem still on the ground goes straight to the wizard.
    public static void CollectAll(Player player)
    {
        foreach (var g in All.ToArray())
        {
            player.AddXp(g.value);
            Destroy(g.gameObject);
        }
    }

    // One driver updates every gem, instead of each of the hundreds left on the ground in a long run
    // paying for its own Update call. Idle gems only compare their cached position with the wizard's.
    Vector2 pos;

    class Field : MonoBehaviour
    {
        static readonly List<XpGem> scratch = new List<XpGem>();

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            var player = gm.Player;
            if (player == null || !player.IsAlive) return;

            Vector2 at = player.Position;
            float pickup = player.PickupRadius;
            scratch.Clear();
            foreach (var g in All) if (g.magnet || (at - g.pos).sqrMagnitude < pickup * pickup) scratch.Add(g);
            foreach (var g in scratch)
            {
                if (gm.State != GameState.Playing) break;   // a level-up opened: the rest wait, as before
                if (g != null) g.Fly(player, at);
            }
        }
    }
    static Field field;

    void Fly(Player player, Vector2 at)
    {
        magnet = true;
        Vector2 to = at - pos;
        float dist = to.magnitude;
        speed += 30f * Time.deltaTime;
        if (dist < 0.4f)
        {
            player.AddXp(value);
            Sfx.Play(Sound.Gem, 0.3f, 0.15f);
            Particles.Pickup(pos, color);
            Destroy(gameObject);
            return;
        }
        pos += to / dist * Mathf.Min(speed * Time.deltaTime, dist);
        transform.position = pos;
    }
}
