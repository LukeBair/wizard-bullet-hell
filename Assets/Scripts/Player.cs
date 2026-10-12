using System.Collections.Generic;
using UnityEngine;

// The wizard. Moves freely with the floating joystick (or WASD), casts every spell it knows
// automatically off cooldown, and levels up from XP gems.
public class Player : MonoBehaviour
{
    public const float HitRadius = 0.15f;   // enemy bullets: tiny hitbox, shown as a dot when bullets get close
    public const float BodyRadius = 0.3f;   // contact with melee enemies and trees
    static float InvulnTime => Tune.F("player.invulnTime", 0.6f);

    public static float BaseMaxHealth => Tune.F("player.MaxHealth", 100f);
    public float MaxHealth = 100f;
    public float MoveSpeed = 4.6f;
    public float PickupRadius = 2.2f;

    public float Health { get; private set; }
    public bool IsAlive => Health > 0f;
    public Vector2 Position => transform.position;
    public Vector2 Facing { get; private set; } = Vector2.up;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    // Base curve (base + perLevel*L + L^2/quadDivisor), then x stepMultiplier for every stepEvery levels.
    public int XpToNext
    {
        get
        {
            float raw = Tune.F("xp.base", 3f) + Level * Tune.F("xp.perLevel", 2f) + Level * Level / Mathf.Max(1f, Tune.F("xp.quadDivisor", 5f));
            int steps = Level / Mathf.Max(1, Tune.I("xp.stepEvery", 5));
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Floor(raw) * Mathf.Pow(Tune.F("xp.stepMultiplier", 1.25f), steps)));
        }
    }

    public readonly List<Spell> Spells = new List<Spell>();

    float invulnTimer;
    float phaseTimer;
    public bool IsPhased => phaseTimer > 0f;
    float[] baseAlpha;
    SpriteRenderer[] renderers;
    Transform visual;
    SpriteRenderer hitbox;

    public static Player Create(Vector2 position)
    {
        var go = new GameObject("Wizard");
        go.transform.position = position;
        var p = go.AddComponent<Player>();
        Tune.Apply(p, "player");   // MaxHealth, MoveSpeed, PickupRadius
        p.Health = p.MaxHealth;
        p.BuildVisual();
        return p;
    }

    void BuildVisual()
    {
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        var robe = new Color(0.45f, 0.25f, 0.85f);
        // Flat shadow centred under the feet (the visual flips on x, so it must sit at x = 0).
        SpriteFactory.Child(visual, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -0.5f), 1f, 48)
            .transform.localScale = new Vector3(0.9f, 0.32f, 1f);
        SpriteFactory.Child(visual, "Aura", SpriteFactory.Glow, new Color(0.6f, 0.4f, 1f, 0.35f), Vector2.zero, 1.8f, 49);
        var art = ArtStyle.Get("player");
        if (art != null)
            SpriteFactory.Child(visual, "Sprite", art, Color.white, new Vector2(0f, 0.15f), 1f, 50);
        else
        {
            SpriteFactory.Child(visual, "Robe", SpriteFactory.Triangle, robe, new Vector2(0f, -0.1f), 0.8f, 50);
            SpriteFactory.Child(visual, "Head", SpriteFactory.Circle, new Color(1f, 0.85f, 0.7f), new Vector2(0f, 0.3f), 0.3f, 51);
            SpriteFactory.Child(visual, "Hat", SpriteFactory.Triangle, robe * 0.7f + new Color(0, 0, 0, 0.3f), new Vector2(0f, 0.6f), 0.5f, 52);
            SpriteFactory.Child(visual, "Staff", SpriteFactory.Square, new Color(0.6f, 0.4f, 0.2f), new Vector2(0.38f, 0f), 1f, 53).transform.localScale = new Vector3(0.07f, 0.8f, 1f);
            SpriteFactory.Child(visual, "Gem", SpriteFactory.Circle, new Color(0.5f, 0.95f, 1f), new Vector2(0.38f, 0.42f), 0.16f, 54);
        }
        hitbox = SpriteFactory.Child(visual, "Hitbox", ArtStyle.Round, new Color(1f, 1f, 1f, 0f), Vector2.zero, HitRadius * 2f, 55);
        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseAlpha[i] = renderers[i].color.a;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!IsAlive || gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

        Vector2 input = Joystick.Direction;
        if (input.sqrMagnitude > 0.01f)
        {
            Facing = input.normalized;
            Vector2 pos = Position + input * MoveSpeed * Tuning.Pace * dt;
            transform.position = gm.Arena.PushOut(pos, BodyRadius);
            if (Mathf.Abs(input.x) > 0.1f) visual.localScale = new Vector3(Mathf.Sign(input.x), 1f, 1f);
        }

        foreach (var spell in Spells) spell.Tick(dt);

        if (phaseTimer > 0f)
        {
            // Ethereal: see-through and shimmering, no blinking.
            phaseTimer -= dt;
            float a = phaseTimer > 0f ? 0.35f + 0.15f * Mathf.Sin(Time.time * 20f) : 1f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var c = renderers[i].color;
                c.a = baseAlpha[i] * a;
                renderers[i].color = c;
            }
        }
        else if (invulnTimer > 0f)
        {
            invulnTimer -= dt;
            bool visible = invulnTimer <= 0f || Mathf.FloorToInt(invulnTimer * 14f) % 2 == 0;
            foreach (var r in renderers) r.enabled = visible;
        }
        UpdateHitbox();
    }

    // The hitbox dot only fades in while an enemy bullet is close, so it doesn't sit on the sprite all the time.
    void UpdateHitbox()
    {
        float nearest = float.MaxValue;
        foreach (var p in Projectile.All)
            if (p.Team == Team.Enemy) nearest = Mathf.Min(nearest, ((Vector2)p.transform.position - Position).sqrMagnitude);
        float a = Mathf.Clamp01((3f - Mathf.Sqrt(nearest)) / 1.5f);
        var c = hitbox.color;
        c.a = a * (phaseTimer > 0f ? 0.5f : 1f);
        hitbox.color = c;
    }

    public T GetSpell<T>() where T : Spell
    {
        foreach (var s in Spells) if (s is T t) return t;
        return null;
    }

    public Spell GetSpell(SpellId id)
    {
        foreach (var s in Spells) if (s.Id == id) return s;
        return null;
    }

    public Spell Learn(SpellId id)
    {
        var existing = GetSpell(id);
        if (existing != null) return existing;
        var s = Spell.Create(id, this);
        Spells.Add(s);
        return s;
    }

    public void AddXp(int amount)
    {
        Xp += amount;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            GameManager.Instance.QueueLevelUp();
        }
    }

    public void Heal(float amount) => Health = Mathf.Min(MaxHealth, Health + amount);

    // Ethereal Veil: no damage for a while.
    public void Phase(float duration)
    {
        phaseTimer = Mathf.Max(phaseTimer, duration);
        invulnTimer = Mathf.Max(invulnTimer, duration);
        foreach (var r in renderers) r.enabled = true;
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || invulnTimer > 0f) return;
        var veil = GetSpell<Veil>();
        if (veil != null && veil.TryAbsorb()) return;

        Health -= amount;
        invulnTimer = InvulnTime;
        GameManager.Instance.Shake(0.2f);
        Sfx.Play(Sound.PlayerHurt, 0.8f);
        Haptics.Tap(Haptics.Impact.Heavy);

        if (!IsAlive)
        {
            foreach (var r in renderers) r.enabled = false;
            Fx.Burst(Position, new Color(0.6f, 0.4f, 1f), 3f);
            Haptics.Notification(Haptics.Notify.Error);
            GameManager.Instance.GameOver();
        }
    }
}
