using UnityEngine;

// A telegraphed area attack: a warning circle fills up on the ground, then it bursts and hurts the
// wizard if they're still inside. Optionally a lobbed shell flies in from `from` while it fills.
public class Hazard : MonoBehaviour
{
    float radius, delay, timer, damage;
    Color color;
    Vector2 from;
    bool lobbed;
    Transform fill, shell;
    SpriteRenderer edge;

    public static Hazard Spawn(Vector2 at, float radius, float delay, float damage, Color color, Vector2? lobFrom = null)
    {
        var go = new GameObject("Hazard");
        go.transform.position = at;
        var h = go.AddComponent<Hazard>();
        h.radius = radius;
        h.delay = h.timer = delay;
        h.damage = damage;
        h.color = color;
        h.edge = SpriteFactory.Child(go.transform, "Edge", SpriteFactory.Ring, new Color(color.r, color.g, color.b, 0.8f), Vector2.zero, radius * 2f, 2);
        h.fill = SpriteFactory.Child(go.transform, "Fill", SpriteFactory.Circle, new Color(color.r, color.g, color.b, 0.3f), Vector2.zero, 0.01f, 1).transform;
        if (lobFrom.HasValue)
        {
            h.lobbed = true;
            h.from = lobFrom.Value;
            var s = SpriteFactory.Child(go.transform, "Shell", SpriteFactory.Circle, Color.Lerp(color, Color.white, 0.3f), Vector2.zero, 0.35f, 83);
            SpriteFactory.Child(s.transform, "Glow", SpriteFactory.Glow, new Color(color.r, color.g, color.b, 0.6f), Vector2.zero, 2.2f, 82);
            h.shell = s.transform;
        }
        return h;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        timer -= Time.deltaTime;
        float t = 1f - Mathf.Clamp01(timer / delay);

        fill.localScale = Vector3.one * radius * 2f * t;
        edge.color = new Color(color.r, color.g, color.b, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
        if (lobbed)
        {
            // Arc in from the thrower, landing as the circle fills.
            Vector2 at = transform.position;
            Vector2 p = Vector2.Lerp(from, at, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 2.5f;
            shell.position = p;
        }

        if (timer > 0f) return;

        Vector2 pos = transform.position;
        Fx.Burst(pos, color, radius * 2.4f);
        Fx.Ring(pos, color, radius);
        var player = gm.Player;
        if (player != null && player.IsAlive)
        {
            float r = radius + Player.HitRadius;
            if ((player.Position - pos).sqrMagnitude < r * r) player.TakeDamage(damage);
        }
        Destroy(gameObject);
    }
}
