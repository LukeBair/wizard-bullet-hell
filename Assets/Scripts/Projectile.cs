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
    int damage;

    public static Projectile Spawn(Vector2 position, Vector2 velocity, Team team, Color color, float radius, int damage = 1)
    {
        var go = new GameObject(team == Team.Player ? "Spell" : "Bullet");
        go.transform.position = position;
        var p = go.AddComponent<Projectile>();
        p.Team = team;
        p.Velocity = velocity;
        p.Radius = radius;
        p.damage = damage;

        // Enemy bullets look bigger than their hitbox, which is what makes bullet hell feel fair.
        float visual = team == Team.Enemy ? 1.6f : 1.2f;
        go.transform.localScale = Vector3.one * radius * 2f * visual;
        SpriteFactory.Add(go, SpriteFactory.Circle, color, team == Team.Enemy ? 30 : 20);
        SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, new Color(color.r, color.g, color.b, 0.5f), Vector2.zero, 2.2f, 19);
        if (velocity.sqrMagnitude > 0.001f)
            go.transform.up = velocity;
        return p;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        var gm = GameManager.Instance;
        transform.position += (Vector3)(Velocity * Time.deltaTime);

        Vector2 pos = transform.position;
        var b = gm.Bounds;
        if (pos.x < b.xMin - 1f || pos.x > b.xMax + 1f || pos.y < b.yMin - 1f || pos.y > b.yMax + 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (Team == Team.Player)
        {
            foreach (var enemy in Enemy.All)
            {
                if (!enemy.IsAlive) continue;
                float r = Radius + enemy.Radius;
                if (((Vector2)enemy.transform.position - pos).sqrMagnitude < r * r)
                {
                    enemy.TakeDamage(damage);
                    Destroy(gameObject);
                    return;
                }
            }
        }
        else
        {
            var player = gm.Player;
            if (player != null && player.IsAlive)
            {
                float r = Radius + Player.HitRadius;
                if (((Vector2)player.transform.position - pos).sqrMagnitude < r * r && player.TakeHit())
                    Destroy(gameObject);
            }
        }
    }
}
