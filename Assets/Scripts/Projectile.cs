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
    public float Homing;             // degrees per second it can turn toward its target (nearest enemy, or the wizard)
    public float HomingTime = 99f;   // stops steering after this long, so enemy homing shots can be dodged
    public float WaveAmplitude;      // sideways weave, for wavy enemy shots
    public float WaveFrequency = 6f;
    public float Knockback = 2f;
    public Action<Projectile, Enemy> OnHit;   // spell-specific effects (explode, burn, chain...)
    public Action<Projectile> OnExpire;       // runs if it times out without being used up
    public string Source;                     // enemy bullets: who fired it, for telemetry

    float age, trailTimer;
    Color color;
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    static readonly List<Enemy> scratch = new List<Enemy>();
    SpriteRenderer body, glow, core;
    UnityEngine.Rendering.Universal.Light2D spellLight;
    // Only the first few live spells get a Light2D (each one is a full extra light pass on the
    // GPU); the rest stay unlit and lean on bloom. Cap: lighting.maxSpellLights.
    bool lit;
    static int litCount;

    // Spent projectiles are parked here and reused, since bosses and split spells fire hundreds a minute.
    static readonly Stack<Projectile>[] pool = { new Stack<Projectile>(), new Stack<Projectile>() };

    public Vector2 Position { get; private set; }

    public static Projectile Spawn(Vector2 position, Vector2 velocity, Team team, Color color, float radius)
    {
        Projectile p = null;
        var free = pool[(int)team];
        while (p == null && free.Count > 0) p = free.Pop();   // skips any destroyed while parked
        if (p == null)
        {
            var go = new GameObject(team == Team.Player ? "Spell" : "Bullet");
            go.SetActive(false);
            p = go.AddComponent<Projectile>();
            // Your spells depth-sort with characters and props (passing behind a tree hides them);
            // enemy bullets always draw on top so they can never hide.
            if (team == Team.Player) go.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 0;
            p.Team = team;
            // the orb is a child so painted skins can rotate and rescale it without touching the projectile's own size
            p.body = SpriteFactory.Child(go.transform, "Body", ArtStyle.Round, color, Vector2.zero, 1f, team == Team.Enemy ? 81 : 71);
            p.glow = SpriteFactory.Child(go.transform, "Glow", SpriteFactory.Glow, color, Vector2.zero, team == Team.Enemy ? Tune.F("bullets.enemyGlow", 1.4f) : 2.4f, team == Team.Enemy ? 80 : 70);
            // Enemy bullets get a hot white core so they never read as your own spells.
            if (team == Team.Enemy) p.core = SpriteFactory.Child(go.transform, "Core", ArtStyle.Round, new Color(0.8f, 0.8f, 0.8f), Vector2.zero, 0.5f, 82);
        }
        else p.ResetState();

        var t = p.transform;
        t.position = position;
        p.Position = position;
        p.Velocity = velocity;
        p.Radius = radius;
        p.color = color;
        if (team == Team.Enemy) p.Source = Telemetry.Actor;

        // Enemy bullets look bigger than their hitbox, which is what makes bullet hell feel fair.
        float visual = team == Team.Enemy ? 1.6f : 1.3f;
        t.localScale = Vector3.one * radius * 2f * visual;
        if (team == Team.Enemy) color = ArtStyle.EnemyBullet(color);
        p.body.sprite = ArtStyle.Round;
        p.body.color = color;
        p.glow.color = new Color(color.r, color.g, color.b, 0.6f);
        // your spells light up whatever they fly past (enemy bullets are too many; bloom does them)
        if (team == Team.Player && litCount < Tune.I("lighting.maxSpellLights", 6))
        {
            if (p.spellLight == null) p.spellLight = Lighting.Point(t, color, 1f, Tune.F("lighting.spellIntensity", 1f));
            if (p.spellLight != null)
            {
                p.spellLight.enabled = true;
                p.spellLight.color = color;
                Lighting.SetRadius(p.spellLight, Tune.F("lighting.spellRadius", 2.8f));
                p.lit = true;
                litCount++;
            }
        }
        else if (p.spellLight != null) p.spellLight.enabled = false;
        if (p.core != null) p.core.sprite = ArtStyle.Round;
        p.skin = false;   // pooled: a reused projectile starts as the plain orb again
        p.body.transform.localRotation = Quaternion.identity;
        p.body.transform.localScale = Vector3.one;
        p.glow.enabled = true;
        p.gameObject.SetActive(true);
        return p;
    }

    void ResetState()
    {
        Damage = 10f;
        Lifetime = 3f;
        Pierce = 0;
        Homing = 0f;
        HomingTime = 99f;
        WaveAmplitude = 0f;
        WaveFrequency = 6f;
        Knockback = 2f;
        OnHit = null;
        OnExpire = null;
        Source = null;
        age = trailTimer = 0f;
        hit.Clear();
        enabled = true;
    }

    bool skin;
    float skinScale, skinPhase;

    // Draws the projectile as a painted effect (SpellVfx) instead of the plain orb: the sprite points
    // its head along the flight (head drawn on the left of the image), `length` world units long.
    public void SetSkin(Sprite sprite, float length)
    {
        skin = true;
        skinPhase = UnityEngine.Random.value * 10f;
        body.sprite = sprite;
        body.color = Color.white;
        glow.enabled = false;
        float unitsLong = sprite.rect.width / sprite.pixelsPerUnit;
        skinScale = length / (unitsLong * Mathf.Max(0.0001f, transform.localScale.x));
        body.transform.localScale = Vector3.one * skinScale;
        Orient();
    }

    void Orient()
    {
        if (Velocity.sqrMagnitude < 1e-6f) return;
        float a = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg + 180f;
        body.transform.localRotation = Quaternion.Euler(0f, 0f, a);
        // a little flicker so the energy feels alive
        float pulse = 1f + 0.08f * Mathf.Sin((age + skinPhase) * 38f);
        body.transform.localScale = new Vector3(skinScale * pulse, skinScale * (2f - pulse), 1f);
    }

    // Takes the projectile out of play (use instead of Destroy) and parks it for reuse.
    public void Release()
    {
        if (!gameObject.activeSelf) return;
        OnHit = null;
        OnExpire = null;
        enabled = false;   // so loops holding a copy of All (Mirror Ward, Stone Spikes) skip it
        gameObject.SetActive(false);
        pool[(int)Team].Push(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPool()
    {
        foreach (var free in pool) free.Clear();
        litCount = 0;
    }

    void OnEnable() => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        if (!lit) return;
        lit = false;
        litCount--;
        if (spellLight != null) spellLight.enabled = false;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

        Lifetime -= dt;
        Vector2 pos = Position;
        if (Lifetime <= 0f || !gm.Arena.Inside(pos, 3f))
        {
            OnExpire?.Invoke(this);
            Release();
            return;
        }

        age += dt;
        if (skin) Orient();
        if (Homing > 0f && age < HomingTime)
        {
            Vector2? target = null;
            if (Team == Team.Player)
            {
                var e = Enemy.Nearest(pos, 8f, hit);
                if (e != null) target = e.Position;
            }
            else if (gm.Player != null) target = gm.Player.Position;

            if (target.HasValue)
            {
                float speed = Velocity.magnitude;
                Vector3 want = (target.Value - pos).normalized * speed;
                Velocity = Vector3.RotateTowards(Velocity, want, Homing * Mathf.Deg2Rad * dt, 0f);
            }
        }

        pos += Velocity * dt * Tuning.Pace;
        if (WaveAmplitude > 0f)
        {
            Vector2 side = new Vector2(-Velocity.y, Velocity.x).normalized;
            pos += side * WaveAmplitude * WaveFrequency * Mathf.Cos(age * WaveFrequency) * dt * Tuning.Pace;
        }
        Position = pos;
        transform.position = pos;

        if (Team == Team.Player)
        {
            trailTimer -= dt;
            if (trailTimer <= 0f)
            {
                trailTimer = 1f / Tune.F("fx.trail.rate", 30f);
                Particles.Trail(pos, color, Radius);
            }

            scratch.Clear();
            scratch.AddRange(Enemy.All);
            foreach (var enemy in scratch)
            {
                if (!enemy.IsAlive || enemy.Hidden || enemy.IsCharmed || hit.Contains(enemy)) continue;
                float r = Radius + enemy.Radius;
                if ((enemy.Position - pos).sqrMagnitude >= r * r) continue;

                hit.Add(enemy);
                using (Telemetry.By(Damage > 0f ? "Enthrall" : Telemetry.DamageBy)) enemy.TakeDamage(Damage, Velocity.normalized * Knockback);   // only charmed shots carry damage
                OnHit?.Invoke(this, enemy);
                if (Pierce-- <= 0) { Release(); return; }
            }
        }
        else
        {
            // Enemy shots hit thralls (charmed foes) in their way, so provoked foes can fight back.
            foreach (var thrall in Enemy.Thralls())
            {
                if (!thrall.IsAlive) continue;
                float tr = Radius + thrall.Radius;
                if ((thrall.Position - pos).sqrMagnitude >= tr * tr) continue;
                using (Telemetry.By("Foes on thralls")) thrall.TakeDamage(Damage, Velocity.normalized * Knockback);
                Release();
                return;
            }
            var player = gm.Player;
            if (player != null && player.IsAlive)
            {
                float r = Radius + Player.HitRadius;
                if ((player.Position - pos).sqrMagnitude < r * r)
                {
                    player.TakeDamage(Damage, Source, "bullet");   // a ward may still block it
                    Release();
                }
            }
        }
    }
}
