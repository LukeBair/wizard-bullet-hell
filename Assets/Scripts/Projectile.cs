using System;
using System.Collections.Generic;
using UnityEngine;

public enum Team { Player, Enemy }

// One class for both the wizard's spells and enemy bullets. Collision is a simple circle check, no physics.
public class Projectile : MonoBehaviour
{
    public static readonly List<Projectile> All = new List<Projectile>();

    public Team Team { get; private set; }
    public Vector2 Velocity;
    public float Radius { get; private set; }
    public float Damage = 10f;
    public float Lifetime = 3f;
    public int Pierce;               // extra enemies it can pass through
    public float Homing;             // degrees per second it can turn toward the nearest enemy
    public float Knockback = 2f;
    public Action<Projectile, Enemy> OnHit;   // spell-specific effects (explode, burn, chain...)

    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    static readonly List<Enemy> scratch = new List<Enemy>();

    public static Projectile Spawn(Vector2 position, Vector2 velocity, Team team, Color color, float radius)
    {
        var go = new GameObject(team == Team.Player ? "Spell" : "Bullet");
        go.transform.position = position;
        var p = go.AddComponent<Projectile>();
        p.Team = team;
        p.Velocity = velocity;
        p.Radius = radius;

        // Enemy bullets look bigger than their hitbox, which is what makes bullet hell feel fair.
        float visual = team == Team.Enemy ? 1.6f : 1.3f;
        go.transform.localScale = Vector3.one * radius * 2f * visual;
        SpriteFactory.Add(go, SpriteFactory.Circle, color, team == Team.Enemy ? 81 : 71);
        SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(color.r, color.g, color.b, 0.6f), Vector2.zero, 2.4f, team == Team.Enemy ? 80 : 70);
        // Enemy bullets get a hot white core so they never read as your own spells.
        if (team == Team.Enemy) SpriteFactory.Child(go.transform, "Core", SpriteFactory.Circle, Color.white, Vector2.zero, 0.5f, 82);
        return p;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

        Lifetime -= dt;
        Vector2 pos = transform.position;
        if (Lifetime <= 0f || !gm.Arena.Inside(pos, 3f)) { Destroy(gameObject); return; }

        if (Homing > 0f)
        {
            var target = Enemy.Nearest(pos, 8f, hit);
            if (target != null)
            {
                float speed = Velocity.magnitude;
                Vector3 want = (target.Position - pos).normalized * speed;
                Velocity = Vector3.RotateTowards(Velocity, want, Homing * Mathf.Deg2Rad * dt, 0f);
            }
        }

        pos += Velocity * dt;
        transform.position = pos;

        if (Team == Team.Player)
        {
            scratch.Clear();
            scratch.AddRange(Enemy.All);
            foreach (var enemy in scratch)
            {
                if (!enemy.IsAlive || hit.Contains(enemy)) continue;
                float r = Radius + enemy.Radius;
                if ((enemy.Position - pos).sqrMagnitude >= r * r) continue;

                hit.Add(enemy);
                enemy.TakeDamage(Damage, Velocity.normalized * Knockback);
                OnHit?.Invoke(this, enemy);
                if (Pierce-- <= 0) { Destroy(gameObject); return; }
            }
        }
        else
        {
            var player = gm.Player;
            if (player != null && player.IsAlive)
            {
                float r = Radius + Player.HitRadius;
                if ((player.Position - pos).sqrMagnitude < r * r)
                {
                    player.TakeDamage(Damage);
                    Destroy(gameObject);
                }
            }
        }
    }
}
