using System.Collections.Generic;
using UnityEngine;

public enum BulletPattern { Fan, Aimed, Ring, Spiral }

// An imp that flies in, hovers, and fires one bullet pattern on a timer.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public float Radius { get; private set; }
    public bool IsAlive => health > 0;

    int health;
    int points;
    float difficulty;
    BulletPattern pattern;
    float fireInterval;
    float fireTimer;
    float spinAngle;
    Vector2 hoverPoint;
    bool arrived;
    float bobPhase;
    float flash;
    SpriteRenderer body;
    Color baseColor;

    public static Enemy Spawn(Vector2 from, Vector2 hoverPoint, BulletPattern pattern, int health, float difficulty, bool boss = false)
    {
        var go = new GameObject(boss ? "Boss" : "Imp");
        go.transform.position = from;
        var e = go.AddComponent<Enemy>();
        e.pattern = pattern;
        e.health = health;
        e.points = health * 10 * (boss ? 3 : 1);
        e.difficulty = difficulty;
        e.hoverPoint = hoverPoint;
        e.bobPhase = Random.value * Mathf.PI * 2f;
        e.Radius = boss ? 1.1f : 0.45f;
        e.fireInterval = IntervalFor(pattern) / Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(difficulty / 15f));
        e.fireTimer = 0.6f + Random.value * 0.6f;
        e.BuildVisual();
        return e;
    }

    static float IntervalFor(BulletPattern p)
    {
        switch (p)
        {
            case BulletPattern.Fan: return 1.4f;
            case BulletPattern.Aimed: return 1.1f;
            case BulletPattern.Ring: return 1.8f;
            default: return 0.12f; // Spiral fires small bursts continuously
        }
    }

    static Color ColorFor(BulletPattern p)
    {
        switch (p)
        {
            case BulletPattern.Fan: return new Color(1f, 0.45f, 0.3f);
            case BulletPattern.Aimed: return new Color(1f, 0.8f, 0.25f);
            case BulletPattern.Ring: return new Color(1f, 0.35f, 0.75f);
            default: return new Color(0.5f, 1f, 0.45f);
        }
    }

    void BuildVisual()
    {
        baseColor = ColorFor(pattern);
        float size = Radius * 2f;
        SpriteFactory.Child(transform, "Glow", SpriteFactory.Glow, new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f), Vector2.zero, size * 1.8f, 4);
        body = SpriteFactory.Child(transform, "Body", SpriteFactory.Circle, baseColor * 0.6f + new Color(0, 0, 0, 0.4f), Vector2.zero, size, 5);
        var hornL = SpriteFactory.Child(transform, "HornL", SpriteFactory.Triangle, baseColor, new Vector2(-0.3f, 0.45f) * size, size * 0.35f, 6);
        var hornR = SpriteFactory.Child(transform, "HornR", SpriteFactory.Triangle, baseColor, new Vector2(0.3f, 0.45f) * size, size * 0.35f, 6);
        hornL.transform.localRotation = Quaternion.Euler(0, 0, 20f);
        hornR.transform.localRotation = Quaternion.Euler(0, 0, -20f);
        SpriteFactory.Child(transform, "EyeL", SpriteFactory.Circle, Color.white, new Vector2(-0.18f, 0.05f) * size, size * 0.18f, 7);
        SpriteFactory.Child(transform, "EyeR", SpriteFactory.Circle, Color.white, new Vector2(0.18f, 0.05f) * size, size * 0.18f, 7);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.IsGameOver) return;

        if (!arrived)
        {
            transform.position = Vector2.MoveTowards(transform.position, hoverPoint, 5f * Time.deltaTime);
            arrived = (Vector2)transform.position == hoverPoint;
        }
        else
        {
            float x = hoverPoint.x + Mathf.Sin(Time.time * 0.8f + bobPhase) * 1.2f;
            float y = hoverPoint.y + Mathf.Sin(Time.time * 1.3f + bobPhase) * 0.25f;
            x = Mathf.Clamp(x, gm.Bounds.xMin + Radius, gm.Bounds.xMax - Radius);
            transform.position = new Vector2(x, y);

            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = fireInterval;
                Fire(gm);
            }
        }

        if (flash > 0f)
        {
            flash -= Time.deltaTime * 8f;
            body.color = Color.Lerp(baseColor * 0.6f + new Color(0, 0, 0, 0.4f), Color.white, Mathf.Clamp01(flash));
        }

        // Touching an imp hurts too.
        var player = gm.Player;
        if (player != null && player.IsAlive)
        {
            float r = Radius + Player.HitRadius;
            if (((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude < r * r)
                player.TakeHit();
        }
    }

    void Fire(GameManager gm)
    {
        Vector2 pos = transform.position;
        Color c = baseColor;
        float speedBoost = 1f + difficulty * 0.03f;

        switch (pattern)
        {
            case BulletPattern.Fan:
            {
                int n = 5 + Mathf.Min((int)difficulty / 3, 4);
                for (int i = 0; i < n; i++)
                {
                    float a = -90f + Mathf.Lerp(-35f, 35f, n == 1 ? 0.5f : i / (float)(n - 1));
                    Shoot(pos, a, 3.8f * speedBoost, c);
                }
                break;
            }
            case BulletPattern.Aimed:
            {
                Vector2 target = gm.Player != null ? (Vector2)gm.Player.transform.position : pos + Vector2.down;
                float baseAngle = Mathf.Atan2(target.y - pos.y, target.x - pos.x) * Mathf.Rad2Deg;
                for (int i = -1; i <= 1; i++)
                    Shoot(pos, baseAngle + i * 12f, 5.5f * speedBoost, c);
                break;
            }
            case BulletPattern.Ring:
            {
                int n = 12 + Mathf.Min((int)difficulty, 12);
                for (int i = 0; i < n; i++)
                    Shoot(pos, spinAngle + i * 360f / n, 3.2f * speedBoost, c);
                spinAngle += 360f / n / 2f; // offset next ring so gaps move
                break;
            }
            case BulletPattern.Spiral:
            {
                const int arms = 3;
                for (int i = 0; i < arms; i++)
                    Shoot(pos, spinAngle + i * 360f / arms, 3.4f * speedBoost, c);
                spinAngle += 11f;
                break;
            }
        }
    }

    static void Shoot(Vector2 from, float angleDeg, float speed, Color color)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Projectile.Spawn(from, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed, Team.Enemy, color, 0.12f);
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;
        health -= amount;
        flash = 1f;
        if (health <= 0)
        {
            GameManager.Instance.AddScore(points);
            Destroy(gameObject);
        }
    }
}
