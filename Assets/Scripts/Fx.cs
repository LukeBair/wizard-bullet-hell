using UnityEngine;

// Short-lived visual effects: bursts, shockwave rings and lightning bolts. Purely cosmetic.
public class Fx : MonoBehaviour
{
    float life, maxLife, startScale, endScale;
    bool pulse;   // telegraphs: blink in place instead of growing and fading
    SpriteRenderer sr;
    LineRenderer line;
    Color color;

    static Material lineMaterial;

    public static void Burst(Vector2 pos, Color c, float size) => Make(pos, SpriteFactory.Glow, c, size * 0.4f, size, 0.3f);

    public static void Ring(Vector2 pos, Color c, float radius, float duration = 0.35f) =>
        Make(pos, SpriteFactory.Ring, c, radius * 0.5f, radius * 2f, duration);

    // Warning lane for a charge: a blinking strip from `from` along `dir`.
    public static void Telegraph(Vector2 from, Vector2 dir, float length, float width, Color c, float duration)
    {
        var go = new GameObject("Telegraph");
        go.transform.position = from + dir.normalized * length * 0.5f;
        go.transform.up = dir;
        go.transform.localScale = new Vector3(width, length, 1f);
        var fx = go.AddComponent<Fx>();
        fx.sr = SpriteFactory.Add(go, SpriteFactory.Square, c, 3);
        fx.color = c;
        fx.life = fx.maxLife = duration;
        fx.pulse = true;
    }

    public static void Bolt(Vector2 a, Vector2 b, Color c)
    {
        var go = new GameObject("Bolt");
        var fx = go.AddComponent<Fx>();
        fx.life = fx.maxLife = 0.18f;
        fx.color = c;
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        var lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial;
        lr.sortingOrder = 90;
        lr.widthMultiplier = 0.12f;
        const int segments = 6;
        lr.positionCount = segments + 1;
        Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float jitter = (i == 0 || i == segments) ? 0f : Random.Range(-0.3f, 0.3f);
            lr.SetPosition(i, Vector2.Lerp(a, b, t) + perp * jitter);
        }
        lr.startColor = lr.endColor = c;
        fx.line = lr;
    }

    static void Make(Vector2 pos, Sprite sprite, Color c, float from, float to, float duration)
    {
        var go = new GameObject("Fx");
        go.transform.position = pos;
        var fx = go.AddComponent<Fx>();
        fx.sr = SpriteFactory.Add(go, sprite, c, 85);
        fx.color = c;
        fx.life = fx.maxLife = duration;
        fx.startScale = from;
        fx.endScale = to;
        go.transform.localScale = Vector3.one * from;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        float t = 1f - life / maxLife;
        var c = color;
        c.a *= 1f - t;
        if (sr != null && pulse)
        {
            c = color;
            c.a *= 0.5f + 0.5f * Mathf.Sin(t * 30f);
            sr.color = c;
        }
        else if (sr != null)
        {
            sr.color = c;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t));
        }
        if (line != null) line.startColor = line.endColor = c;
    }
}
