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

    // Sunspots: standing in light makes you Sunlit (faster, quicker casting, harder hitting) for a while.
    float sunTimer, sunPower;
    public bool IsSunlit => sunTimer > 0f;
    float SunlitPower => IsSunlit ? sunPower : 0f;
    public float MoveMultiplier => 1f + Tune.F("spells.Sunspots.moveBuff", 0.2f) * SunlitPower;
    public float CastSpeedMultiplier => 1f + Tune.F("spells.Sunspots.castBuff", 0.15f) * SunlitPower;
    public float DamageMultiplier => 1f + Tune.F("spells.Sunspots.damageBuff", 0.2f) * SunlitPower;
    SpriteRenderer sunGlow;

    // Ward rune: each charge blocks one enemy bullet. Max charges = Ward runes socketed across all spells.
    int wards;
    float wardTimer;
    SpriteRenderer wardRing;
    public bool IsPhased => phaseTimer > 0f;
    float[] baseAlpha;
    SpriteRenderer[] renderers;
    Transform visual;
    SpriteRenderer hitbox;
    WizardAnimator anim;   // null when the style has no animated wizard

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
        var sheet = SpriteSheet.Load("player_sheet");
        var art = ArtStyle.Get("player");
        if (sheet != null && sheet["idle"] != null)
            anim = WizardAnimator.Attach(SpriteFactory.Child(visual, "Sprite", sheet["idle"].Frames[0], Color.white, new Vector2(0f, 0.1f), 0.85f, 50), sheet);
        else if (art != null)
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

        // Status visuals, kept out of `renderers` so phasing and blinking leave them alone.
        sunGlow = SpriteFactory.Child(transform, "Sunlit", SpriteFactory.Glow, new Color(1f, 0.9f, 0.5f, 0f), Vector2.zero, 2.4f, 47);
        wardRing = SpriteFactory.Child(transform, "Ward", SpriteFactory.Ring, new Color(0.75f, 0.9f, 1f, 0f), Vector2.zero, 1.3f, 60);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!IsAlive || gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

        Vector2 input = Joystick.Direction;
        if (anim != null) anim.Moving = input.sqrMagnitude > 0.01f;
        if (input.sqrMagnitude > 0.01f)
        {
            Facing = input.normalized;
            Vector2 pos = Position + input * MoveSpeed * MoveMultiplier * Tuning.Pace * dt;
            transform.position = gm.Arena.PushOut(pos, BodyRadius);
            if (Mathf.Abs(input.x) > 0.1f) visual.localScale = new Vector3(Mathf.Sign(input.x), 1f, 1f);
        }

        foreach (var spell in Spells) spell.Tick(dt);

        if (sunTimer > 0f) sunTimer -= dt;
        if (wardTimer > 0f) wardTimer -= dt;
        if (wards > MaxWards) wards = MaxWards;
        sunGlow.color = new Color(1f, 0.9f, 0.5f, IsSunlit ? 0.3f + 0.08f * Mathf.Sin(Time.time * 6f) : 0f);
        wardRing.color = new Color(0.75f, 0.9f, 1f, wards > 0 ? 0.35f + 0.1f * wards : 0f);

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
            if (p.Team == Team.Enemy) nearest = Mathf.Min(nearest, (p.Position - Position).sqrMagnitude);
        float a = Mathf.Clamp01((3f - Mathf.Sqrt(nearest)) / 1.5f);
        var c = hitbox.color;
        c.a = a * (phaseTimer > 0f ? 0.5f : 1f);
        hitbox.color = c;
    }

    // A spell just fired: swing the staff.
    public void CastAnim() => anim?.Cast();

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

    public void Heal(float amount, bool fx = true)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
        if (fx && amount > 0f) Particles.Heal(Position, amount);
    }

    public void Sunlight(float power, float duration)
    {
        sunPower = IsSunlit ? Mathf.Max(sunPower, power) : power;
        sunTimer = Mathf.Max(sunTimer, duration);
    }

    int MaxWards
    {
        get
        {
            float n = 0f;
            foreach (var s in Spells) n += s.RuneCount(Rune.Ward);
            return Mathf.CeilToInt(n - 0.01f);
        }
    }

    // Called by a spell with a Ward rune each time it casts.
    public void GainWard()
    {
        if (wardTimer > 0f || wards >= MaxWards) return;
        wards++;
        wardTimer = Tune.F("runes.Ward.cooldown", 3f);
        Fx.Ring(Position, new Color(0.75f, 0.9f, 1f), 0.7f, 0.3f);
    }

    // An enemy bullet is about to hit: a ward eats it. Returns true if blocked.
    public bool BlockBullet()
    {
        if (invulnTimer > 0f || wards <= 0) return false;
        wards--;
        Fx.Burst(Position, new Color(0.75f, 0.9f, 1f), 1.5f);
        Sfx.Play(Sound.Select, 0.5f, 0.2f);
        return true;
    }

    // Rejuvenate III: back to full health instead of dying.
    public void Revive()
    {
        Health = MaxHealth;
        foreach (var r in renderers) r.enabled = true;
        Phase(2f);
    }

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
        anim?.Hit();

        if (!IsAlive && GetSpell<Rejuvenate>() is Rejuvenate rejuv && rejuv.TryRevive()) return;

        if (!IsAlive)
        {
            if (anim != null)
            {
                // Keep the sprite up for the death animation; hide the aura, hitbox and the rest.
                foreach (var r in renderers) r.enabled = r.gameObject == anim.gameObject || r.name == "Shadow";
                anim.Die();
                Fx.Burst(Position, ArtStyle.ParticleColor(new Color(0.6f, 0.4f, 1f)), 1.5f);
            }
            else
            {
                foreach (var r in renderers) r.enabled = false;
                Fx.Burst(Position, new Color(0.6f, 0.4f, 1f), 3f);
            }
            Haptics.Notification(Haptics.Notify.Error);
            GameManager.Instance.GameOver();
        }
    }
}
