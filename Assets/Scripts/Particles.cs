using UnityEngine;

// Particle sparks layered over the Fx flashes: hit sparks, death debris, spell trails, pickup
// glints, level-up fountains, combo bursts, heals, status embers and boss muzzle flashes.
// Purely cosmetic, and Dusk only: the other art styles show none of this.
//
// Everything is emitted into two shared ParticleSystems (one on game time, one on unscaled time
// for effects that play while the level-up screen pauses the game), so a burst costs no
// GameObjects. Particles are square pixels snapped to Dusk's palette that shrink in steps instead
// of fading; spell trails keep the spell's tint, and level-up and pickup glints are gold and white. Counts, speeds, sizes and lifetimes are in tuning.json under fx.*.
public static class Particles
{
    static ParticleSystem world, unscaled;
    static int hitFrame, hitsThisFrame;

    static bool Enabled => ArtStyle.Current == ArtStyleId.Dusk && Tune.B("fx.enabled", true);
    static float F(string key, float fallback) => Tune.F(Tune.Key("fx.", key), fallback);

    // ------------------------------------------------------------------ effects

    // Debris thrown out with every Fx.Burst flash; count and speed grow with the flash.
    public static void Burst(Vector2 pos, Color c, float size)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(size * F("burst.perUnit", 2.5f)), (int)F("burst.min", 2f), (int)F("burst.max", 24f));
        Spray(pos, c, n, size * F("burst.speed", 1.4f) + 1f, F("burst.size", 0.12f), F("burst.life", 0.4f));
    }

    // Sparks flung out from the edge of an Fx.Ring shockwave.
    public static void Ring(Vector2 pos, Color c, float radius)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(radius * F("ring.perUnit", 5f)), 4, (int)F("ring.max", 32f));
        float speed = radius * F("ring.speed", 3f);
        for (int i = 0; i < n; i++)
        {
            float a = (i + Random.value * 0.6f) / n * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Emit(pos + dir * radius * 0.3f, dir * speed * Random.Range(0.7f, 1.1f), Pick(c), F("ring.size", 0.1f), F("ring.life", 0.35f));
        }
    }

    // A few sparks where a spell lands, kicked along the hit direction.
    public static void Hit(Vector2 pos, Color c, Vector2 push)
    {
        if (hitFrame != Time.frameCount) { hitFrame = Time.frameCount; hitsThisFrame = 0; }
        if (++hitsThisFrame > F("hit.maxPerFrame", 12f)) return;
        int n = (int)F("hit.count", 3f);
        float cone = push == Vector2.zero ? 180f : F("hit.cone", 60f);
        float baseAngle = push == Vector2.zero ? 0f : Mathf.Atan2(push.y, push.x) * Mathf.Rad2Deg;
        for (int i = 0; i < n; i++)
        {
            float a = (baseAngle + Random.Range(-cone, cone)) * Mathf.Deg2Rad;
            var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * F("hit.speed", 5f) * Random.Range(0.5f, 1f);
            Emit(pos, v, Pick(c), F("hit.size", 0.09f), F("hit.life", 0.2f));
        }
    }

    // Sparks at the end of a lightning bolt.
    public static void Zap(Vector2 pos, Color c) =>
        Spray(pos, c, (int)F("bolt.count", 4f), F("bolt.speed", 5f), F("bolt.size", 0.08f), F("bolt.life", 0.18f));

    // One trail mote behind a moving spell, in the spell's own tint so it matches the projectile.
    public static void Trail(Vector2 pos, Color c, float radius)
    {
        var v = Random.insideUnitCircle * F("trail.speed", 0.6f);
        var tint = Random.value < F("highlightShare", 0.25f) ? Color.Lerp(c, Color.white, 0.5f) : c;
        Emit(pos + Random.insideUnitCircle * radius * 0.5f, v, tint, radius * F("trail.size", 0.9f), F("trail.life", 0.25f));
    }

    // Body debris when an enemy dies, in the colours its Dusk sprite is drawn in; much bigger for bosses.
    public static void Death(Vector2 pos, EnemyLook look, float radius, EnemyTier tier)
    {
        var colors = ArtStyle.DebrisColors(look);
        Color body = colors[0], accent = colors[1];
        float mult = tier == EnemyTier.Boss ? F("death.bossMultiplier", 6f) : tier == EnemyTier.Medium ? F("death.mediumMultiplier", 1.8f) : 1f;
        int n = Mathf.RoundToInt(F("death.count", 7f) * mult);
        float speed = F("death.speed", 4f) * (0.6f + radius);
        float size = F("death.size", 0.14f) * Mathf.Clamp(radius / 0.4f, 1f, 2.5f);
        float life = F("death.life", 0.55f) * (tier == EnemyTier.Boss ? 2f : 1f);
        for (int i = 0; i < n; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            Emit(pos + dir * radius * 0.4f, dir * speed * Random.Range(0.3f, 1f), i % 3 == 0 ? accent : body, size * Random.Range(0.6f, 1.2f), life * Random.Range(0.7f, 1.2f));
        }
        if (tier == EnemyTier.Boss) Fountain(pos, accent, (int)F("death.bossMotes", 40f), radius, false, true);
    }

    // White glints, with a few in the gem's own colour, when the wizard swallows an XP gem.
    public static void Pickup(Vector2 pos, Color c)
    {
        int n = (int)F("pickup.count", 4f);
        for (int i = 0; i < n; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            Emit(pos, dir * F("pickup.speed", 2.5f) * Random.Range(0.3f, 1f), i % 2 == 0 ? Color.white : c, F("pickup.size", 0.08f), F("pickup.life", 0.3f) * Random.Range(0.7f, 1.2f));
        }
    }

    // Gold and white motes rising around the wizard. Plays on unscaled time behind the level-up cards.
    public static void LevelUp(Vector2 pos) =>
        Fountain(pos, Gold, (int)F("levelUp.count", 36f), 0.6f, true, true);

    static readonly Color Gold = new Color(1f, 0.8f, 0.25f);

    // Evolving a spell: a spiral of its colour. Plays on unscaled time behind the cards.
    public static void Evolve(Vector2 pos, Color c)
    {
        int n = (int)F("evolve.count", 60f);
        float speed = F("evolve.speed", 7f);
        for (int i = 0; i < n; i++)
        {
            float a = i * 0.45f;
            float s = speed * (0.35f + 0.65f * i / n);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Emit(pos, dir * s, i % 4 == 0 ? Style(Color.white) : Style(c), F("evolve.size", 0.16f), F("evolve.life", 0.9f), true);
        }
    }

    // Element combos get their own two-colour bursts so players learn to spot them.
    public static void Combo(Interaction i, Vector2 pos)
    {
        Color a, b;
        switch (i)
        {
            case Interaction.Steam: a = new Color(0.95f, 0.95f, 0.95f); b = new Color(1f, 0.5f, 0.2f); break;
            case Interaction.Conductive: a = new Color(1f, 0.95f, 0.4f); b = new Color(0.25f, 0.55f, 1f); break;
            case Interaction.DeepFreeze: a = new Color(0.7f, 0.95f, 1f); b = new Color(0.25f, 0.55f, 1f); break;
            case Interaction.Firebugs: a = new Color(1f, 0.6f, 0.2f); b = new Color(0.6f, 0.75f, 0.2f); break;
            default: a = new Color(0.7f, 0.3f, 0.9f); b = new Color(0.6f, 0.75f, 0.2f); break;
        }
        int n = (int)F("combo.count", 18f) * (i == Interaction.Steam ? 2 : 1);
        float speed = F("combo.speed", 6f);
        for (int k = 0; k < n; k++)
        {
            var dir = Random.insideUnitCircle.normalized;
            float s = speed * Random.Range(0.4f, 1f);
            // Steam drifts up; the rest burst outward.
            var v = i == Interaction.Steam ? dir * s * 0.5f + Vector2.up * s * 0.4f : dir * s;
            Emit(pos, v, Style(k % 2 == 0 ? a : b), F("combo.size", 0.13f), F("combo.life", 0.55f) * Random.Range(0.7f, 1.2f));
        }
    }

    // Muzzle flash on boss bullets, so big volleys read as attacks before the bullets spread out.
    public static void Muzzle(Vector2 pos, Vector2 dir, Color c)
    {
        int n = (int)F("bossShot.count", 2f);
        for (int i = 0; i < n; i++)
            Emit(pos, (dir.normalized + Random.insideUnitCircle * 0.5f) * F("bossShot.speed", 3f), Style(c), F("bossShot.size", 0.12f), F("bossShot.life", 0.25f));
    }

    // Rubble thrown up by a ground slam.
    public static void Slam(Vector2 pos, Color c, float radius) =>
        Spray(pos, c, (int)F("slam.count", 30f), F("slam.speed", 8f), F("slam.size", 0.18f), F("slam.life", 0.6f), radius * 0.5f);

    // Ambient particles on enemies with statuses; call every frame. Each status emits at its own
    // fx.status.<name>Rate per second: rising embers when burning, frost flecks when chilled, drips
    // when wet, ants scurrying over infested foes, and thorn and leaf chips on foes trapped by vines.
    public static void Status(Vector2 pos, float radius, bool burning, bool chilled, bool wet, bool infested, bool rooted, float dt)
    {
        if (!Enabled) return;
        float size = F("status.size", 0.08f), life = F("status.life", 0.6f);
        if (burning && Random.value < F("status.burnRate", 8f) * dt)
        {
            var at = pos + new Vector2(Random.Range(-radius, radius) * 0.7f, Random.Range(-radius, radius) * 0.3f);
            Emit(at, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.2f, 2.2f)), Style(Random.value < 0.3f ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.5f, 0.2f)), size, life * Random.Range(0.6f, 1.2f));
        }
        if (chilled && Random.value < F("status.chillRate", 4f) * dt)
            Emit(pos + Random.insideUnitCircle * radius, new Vector2(Random.Range(-0.2f, 0.2f), -0.5f), Style(new Color(0.7f, 0.95f, 1f)), size, life);
        if (wet && Random.value < F("status.wetRate", 3f) * dt)
            Emit(pos + Random.insideUnitCircle * radius * 0.7f, new Vector2(0f, -1.4f), Style(new Color(0.25f, 0.55f, 1f)), size, life * 0.6f);
        if (infested && Random.value < F("status.antRate", 10f) * dt)
        {
            // Ants run in short straight dashes across the body.
            var dir = Random.insideUnitCircle.normalized;
            Emit(pos - dir * radius * 0.6f, dir * radius * 3f, Style(new Color(0.15f, 0.25f, 0.2f)), F("ants.size", 0.05f), 0.35f);
        }
        if (rooted && Random.value < F("status.thornRate", 5f) * dt)
        {
            var at = pos + Random.insideUnitCircle.normalized * radius * 0.8f;
            Emit(at, (at - pos).normalized * 0.8f, Style(Random.value < 0.35f ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.55f, 0.3f)), size, life);
        }
    }

    // An infested foe bursts: a swarm of ants pours out in every direction. Firebugs are lit.
    public static void Ants(Vector2 pos, float radius, bool fire)
    {
        int n = (int)F("ants.count", 24f);
        for (int i = 0; i < n; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            var c = fire ? (i % 3 == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.5f, 0.2f)) : new Color(0.15f, 0.25f, 0.2f);
            Emit(pos + dir * 0.2f, dir * radius * F("ants.speed", 2.5f) * Random.Range(0.5f, 1f), Style(c), F("ants.size", 0.05f) * (fire ? 2f : 1f), F("ants.life", 0.6f) * Random.Range(0.7f, 1.2f));
        }
    }

    // Crackling sparks scattered over a lightning splash.
    public static void Crackle(Vector2 pos, float radius, Color c)
    {
        int n = Mathf.RoundToInt(F("crackle.perUnit", 10f) * radius);
        for (int i = 0; i < n; i++)
        {
            var at = pos + Random.insideUnitCircle * radius;
            Emit(at, Random.insideUnitCircle.normalized * F("crackle.speed", 4f), i % 2 == 0 ? Style(Color.white) : Style(c), F("crackle.size", 0.08f), F("crackle.life", 0.2f) * Random.Range(0.6f, 1.4f));
        }
    }

    // Frost Nova: ice shards fly out to the edge and snow drifts down inside the ring.
    public static void Frost(Vector2 pos, float radius)
    {
        int n = Mathf.RoundToInt(F("frost.perUnit", 8f) * radius);
        for (int i = 0; i < n; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            Emit(pos + dir * 0.3f, dir * radius * F("frost.speed", 3.5f) * Random.Range(0.6f, 1f), Style(new Color(0.7f, 0.95f, 1f)), F("frost.size", 0.1f), F("frost.life", 0.45f));
            Emit(pos + Random.insideUnitCircle * radius, new Vector2(Random.Range(-0.2f, 0.2f), -0.6f), Style(Color.white), F("frost.size", 0.1f) * 0.5f, F("frost.snowLife", 0.9f) * Random.Range(0.6f, 1.2f));
        }
    }

    // Healing: sage and cream motes rise around the wizard, more for bigger heals.
    public static void Heal(Vector2 pos, float amount)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(amount * F("heal.perHp", 1f)), (int)F("heal.min", 6f), (int)F("heal.max", 30f));
        for (int i = 0; i < n; i++)
        {
            var at = pos + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.5f, 0.3f));
            Emit(at, new Vector2(0f, F("heal.speed", 2.2f) * Random.Range(0.5f, 1f)), Style(i % 3 == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(0.5f, 1f, 0.45f)), F("heal.size", 0.1f), F("heal.life", 0.8f) * Random.Range(0.7f, 1.2f));
        }
    }

    // ------------------------------------------------------------------ building blocks

    static void Spray(Vector2 pos, Color c, int n, float speed, float size, float life, float spawnRadius = 0f)
    {
        for (int i = 0; i < n; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            Emit(pos + dir * spawnRadius * Random.value, dir * speed * Random.Range(0.3f, 1f), Pick(c), size * Random.Range(0.7f, 1.2f), life * Random.Range(0.7f, 1.2f));
        }
    }

    // exact: use the colour and pure white as given instead of snapping them to the palette.
    static void Fountain(Vector2 pos, Color c, int n, float radius, bool paused, bool exact = false)
    {
        float speed = F("levelUp.speed", 5f);
        for (int i = 0; i < n; i++)
        {
            float a = (i + Random.value) / n * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var v = dir * speed * 0.35f + Vector2.up * speed * Random.Range(0.4f, 1f);
            Emit(pos + dir * radius, v, exact ? (i % 3 == 0 ? Color.white : c) : (i % 3 == 0 ? Style(Color.white) : Style(c)), F("levelUp.size", 0.14f), F("levelUp.life", 0.9f) * Random.Range(0.8f, 1.2f), paused);
        }
    }

    // Mostly the given colour, with a share of brighter highlights.
    static Color Pick(Color c) =>
        Random.value < F("highlightShare", 0.25f) ? Style(Color.Lerp(c, Color.white, 0.6f)) : Style(c);

    static Color Style(Color c) => ArtStyle.ParticleColor(c);

    static void Emit(Vector2 pos, Vector2 velocity, Color c, float size, float life, bool unscaledTime = false)
    {
        if (!Enabled) return;
        // Effects fired while the game is paused (evolving from the level-up cards) play anyway.
        var ps = Get(unscaledTime || Time.timeScale == 0f);
        const float pixel = 1f / 20f;   // Dusk's pixels per unit
        size = Mathf.Max(pixel, Mathf.Round(size / pixel) * pixel);
        c.a = 1f;
        var p = new ParticleSystem.EmitParams
        {
            position = new Vector3(pos.x, pos.y, 0f),
            velocity = velocity * Tuning.Pace,
            startColor = c,
            startSize = size,
            startLifetime = life,
            applyShapeToPosition = false,
        };
        ps.Emit(p, 1);
    }

    // ------------------------------------------------------------------ systems

    static ParticleSystem Get(bool unscaledTime)
    {
        if (world == null) world = Build("Particles", false);
        if (unscaled == null) unscaled = Build("Particles (unscaled)", true);
        return unscaledTime ? unscaled : world;
    }

    static ParticleSystem Build(string name, bool unscaledTime)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;   // stays playing; emission is off, so it only shows what Emit() adds
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = (int)F("maxParticles", 1500f);
        main.useUnscaledTime = unscaledTime;
        main.startSpeed = 0f;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 100f;
        drag.drag = F("drag", 4f);
        drag.multiplyDragByParticleSize = false;
        drag.multiplyDragByParticleVelocity = false;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        // Shrink in hard steps, like hand-animated pixel sparks; no fading, so colours stay in palette.
        var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.45f, 0.66f), new Keyframe(0.75f, 0.33f), new Keyframe(1f, 0.33f));
        for (int i = 0; i < curve.length; i++)
        {
            var k = curve[i];
            k.inTangent = k.outTangent = float.PositiveInfinity;
            curve.MoveKey(i, k);
        }
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = 76;   // above your spells, below enemy bullets so threats stay readable
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = SpriteFactory.Square.texture;
        r.material = mat;

        ps.Play();
        return ps;
    }
}
