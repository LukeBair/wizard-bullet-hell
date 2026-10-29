using UnityEngine;

// Periodic heal. II: much more often, a little less each time. III: also builds a revive charge over a
// long time; dying with it full brings you back at full health.
public class Rejuvenate : Spell
{
    public override string Name => "Rejuvenate";
    public override Element Element => Element.Light;
    public override string Blurb => $"Heals you {Pct(T("damage", 0.06f))} of your max health every {Secs(T("cooldown", 9f))}.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 1 ? 0.06f : 0.05f;   // fraction of max health per pulse
    protected override float BaseCooldown => Tier == 1 ? 9f : 3f;

    public override string TierName(int tier) => tier == 2 ? "Swift Mending" : tier == 3 ? "Phoenix Rite" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Heals far more often (every {Secs(T("cooldown_t2", 3f))}), {Pct(T("damage_t2", 0.05f))} per pulse."
                  : $"Swift Mending, plus a revive that charges over {T("reviveChargeTime_t3", 90f):0}s: if you die with it full, you come back at full health.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} healing";
            case Rune.Haste: return Tier == 3 ? $"Heals and charges the revive {Pct(HastePerRune)} faster" : $"Heals {Pct(HastePerRune)} more often";
            case Rune.Split: return "+1 extra pulse each cast";
            case Rune.Area: return "Heals more the lower your health";
            case Rune.Pierce: return $"+{T("maxHealthPerPierce", 10f):0} max health";
            case Rune.Echo: return "Heals again a moment later";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool HitsFoes => false;

    float reviveCharge;
    float ReviveTime => T("reviveChargeTime_t3", 90f) * HasteFactor;
    public bool ReviveReady => Tier == 3 && reviveCharge >= ReviveTime;
    public override float CooldownProgress => Tier == 3 ? Mathf.Clamp01(reviveCharge / ReviveTime) : base.CooldownProgress;

    protected override bool Ready() => true;

    protected override void OnRunesChanged()
    {
        if (Owner == null) return;
        // Pierce means nothing for a heal, so it buys max health instead.
        float bonus = T("maxHealthPerPierce", 10f) * RuneCount(Rune.Pierce);
        Owner.MaxHealth = Player.BaseMaxHealth + bonus;
    }

    protected override void OnTick(float dt)
    {
        if (Owner != null && Owner.Health < Owner.MaxHealth * Tune.F("meta.feats.Rejuvenate.below", 0.25f))
            Grimoire.Feat(SpellId.Rejuvenate, dt);   // tier III feat: time spent at low health, then win
        if (Tier < 3 || ReviveReady) return;
        reviveCharge += dt;
        if (ReviveReady)
        {
            GameManager.Instance.Toast("Phoenix Rite is ready");
            Fx.Ring(Owner.Position, Color, 1.5f, 0.5f);
        }
    }

    // Called by the Player when a hit would kill it.
    public bool TryRevive()
    {
        if (!ReviveReady) return false;
        reviveCharge = 0f;
        Owner.Revive();
        GameManager.Instance.Toast("Phoenix Rite: revived!");
        Fx.Burst(Owner.Position, Color, 8f);
        Fx.Ring(Owner.Position, Color, 4f, 0.6f);
        Sfx.Play("spells.Rejuvenate.revive", Sound.LevelUp, 0.9f, 0f);
        Haptics.Notification(Haptics.Notify.Success);
        // Clear some space so the revive isn't instantly wasted.
        foreach (var e in EnemiesWithin(Owner.Position, 4f))
            e.TakeDamage(0f, (e.Position - Owner.Position).normalized * 15f);
        return true;
    }

    protected override void Cast()
    {
        for (int i = 0; i < Count; i++) Owner.StartCoroutine(Pulse(i * 0.4f));
    }

    System.Collections.IEnumerator Pulse(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float missing = 1f - Owner.Health / Owner.MaxHealth;
        float amount = Owner.MaxHealth * Damage * (1f + missing * 0.5f * (Area - 1f) * 5f);
        Owner.Heal(amount, source: Id.ToString());
        if (!SpellVfx.Heal(Owner.transform))
        {
            Fx.Ring(Owner.Position, Color, 1.2f, 0.4f);
            Fx.Burst(Owner.Position, Color, 2f);
        }
        DamageText.Spawn(Owner.Position + Vector2.up * 0.6f, amount, new Color(0.5f, 1f, 0.5f));
        Sfx.Play("spells.Rejuvenate.pulse", Sound.Gem, 0.5f, 0f);
    }
}

// Vines burst under a group of foes and hold them in place. Rooted foes can still shoot,
// but can't hurt you by touch. II: thorns bleed trapped foes and hurt foes that bump into them.
// III: foes that bump into trapped foes get trapped too, and trapped foes that die sprout fresh vines.
public class Overgrowth : Spell
{
    public override string Name => "Overgrowth";
    public override Element Element => Element.Nature;
    public override string Blurb => "Vines trap foes in place. Trapped foes can't hurt you by touch.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 6f;
    protected override float BaseCooldown => 5f;
    protected override float PotencyPerRune => 0.25f;
    float Radius => TT("radius", 1.8f) * Area;
    float RootTime => TT("rootTime", 2.2f) * Potency;
    float ThornDps => Tier >= 2 ? TT("thornDps", 8f) * Potency : 0f;

    public override string TierName(int tier) => tier == 2 ? "Thornweave" : tier == 3 ? "Strangling Bramble" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Thorns: trapped foes take damage over time, and foes that run into them get hurt."
                  : "Thorns, foes that touch a trapped foe get trapped too, and trapped foes that die burst into a fresh patch of vines.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return Tier >= 2 ? $"+{Pct(PotencyStep)} damage, thorns and trap time" : $"+{Pct(PotencyStep)} damage and trap time";
            case Rune.Split: return "+1 patch of vines";
            case Rune.Pierce: return "Trapped foes take +15% damage from everything";
            case Rune.Area: return $"+{Pct(AreaStep)} vine radius";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        Sfx.Play("spells.Overgrowth.cast", Sound.Burrow, 0.5f);
        var used = new System.Collections.Generic.List<Enemy>();
        for (int i = 0; i < Count; i++)
        {
            var target = Enemy.Nearest(Owner.Position, Range, used) ?? Enemy.Nearest(Owner.Position, Range);
            if (target == null) return;
            foreach (var e in Vines(target.Position, Radius)) used.Add(e);
        }
    }

    System.Collections.Generic.List<Enemy> Vines(Vector2 at, float radius)
    {
        if (!SpellVfx.Vines(at, radius))
        {
            Fx.Ring(at, Color, radius, 0.5f);
            Fx.Burst(at, new Color(0.2f, 0.6f, 0.15f, 0.6f), radius * 2f);
        }
        var caught = EnemiesWithin(at, radius);
        foreach (var e in caught)
        {
            e.Root(RootTime, 0.15f * RuneCount(Rune.Pierce), ThornDps, Tier == 3, Tier == 3 ? new System.Action<Vector2>(DeathBloom) : null);
            e.VineTrapped = true;
            Hit(e, Damage);
        }
        return caught;
    }

    // Strangling Bramble: a trapped foe that dies sprouts a smaller patch of vines where it fell.
    void DeathBloom(Vector2 at)
    {
        if (Owner == null) return;
        Vines(at, Radius * T("deathBloomScale_t3", 0.75f));
    }
}

// Passive ward: when you'd take a hit while it's ready, you take nothing and turn ethereal.
// II: phases for 2s longer, and foes you pass through take heavy damage. III: recharges much faster.
// The recharge starts when the phase ends.
public class Veil : Spell
{
    public override string Name => "Ethereal Veil";
    public override Element Element => Element.Arcane;
    public override string Blurb => $"Every {Secs(T("cooldown", 12f))}, the next hit is ignored and you phase out for {T("damage", 1.5f):0.#}s.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => Tier == 1 ? 1.5f : 3.5f;   // seconds of invulnerability
    protected override float BaseCooldown => Tier == 3 ? 4f : 12f;
    protected override float PotencyPerRune => 0.25f;

    public override string TierName(int tier) => tier == 2 ? "Phantom Stride" : tier == 3 ? "Flickerstep" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? $"Phase out for {T("damage_t2", 3.5f):0.#}s, and foes you pass through while phased take {T("passDamage_t2", 60f):0} damage."
                  : $"Recharges dramatically faster ({Secs(T("cooldown_t3", 4f))} after each phase ends).";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return Tier >= 2 ? $"+{Pct(PhaseStep)} phase time, +{Pct(PotencyStep)} pass-through damage" : $"+{Pct(PhaseStep)} phase time";
            case Rune.Haste: return $"Recharges {Pct(HastePerRune)} faster";
            case Rune.Area: return "Phasing out knocks nearby foes back";
            case Rune.Split: return "Phasing out also fires a ring of 8 arcane bolts";
            case Rune.Pierce: return "Split's arcane bolts pass through +1 enemy";
            case Rune.Echo: return "+1 stored charge";
            default:
                // Veil only touches foes with Split's bolts, and with pass-through hits once evolved.
                if (IsStatusRune(r)) return base.RuneEffect(r) + (Tier >= 2 ? " (bolts and pass-through only)" : " (only Split's bolts hit foes)");
                return base.RuneEffect(r);
        }
    }

    // Potency adds only a little phase time (it was far too strong at the general potency rate);
    // the general rate still scales Phantom Stride's pass-through damage.
    float PhaseStep => TT("phasePerPotency", 0.05f);
    float PhaseTime => TT("damage", BaseDamage) * (1f + PhaseStep * RuneCount(Rune.Potency));

    int charges = 1;
    int MaxCharges => 1 + Echoes;
    readonly System.Collections.Generic.HashSet<Enemy> passed = new System.Collections.Generic.HashSet<Enemy>();
    protected override bool EchoRecasts => false;
    protected override bool Overloadable => false;
    protected override bool Ready() => charges < MaxCharges;
    protected override void Cast() => charges++;
    public override float CooldownProgress => charges > 0 ? 1f : base.CooldownProgress;

    protected override void OnTick(float dt)
    {
        if (Owner == null || !Owner.IsPhased) return;
        // The recharge only starts once the phase ends (otherwise Flickerstep was never hittable).
        if (charges < MaxCharges) ResetCooldown();

        // Tier II+: anything you pass through while phased takes one big hit per phase.
        if (Tier < 2) return;
        foreach (var e in EnemiesWithin(Owner.Position, Player.BodyRadius + 0.2f))
        {
            if (!passed.Add(e)) continue;
            Fx.Burst(e.Position, Color, 2f);
            Hit(e, TT("passDamage", 60f) * Potency);
        }
    }

    // Called by the Player when it is about to take damage.
    public bool TryAbsorb()
    {
        if (charges <= 0) return false;
        charges--;
        ResetCooldown();
        passed.Clear();
        Owner.Phase(PhaseTime);
        Sfx.Play("spells.Veil.absorb", Sound.Teleport, 0.7f);
        if (!SpellVfx.Veil(Owner.transform, PhaseTime)) Fx.Ring(Owner.Position, Color, 1.5f, 0.4f);
        if (RuneCount(Rune.Area) > 0)
            foreach (var e in EnemiesWithin(Owner.Position, 2f * Area))
                e.TakeDamage(0f, (e.Position - Owner.Position).normalized * 10f);
        int bolts = Mathf.RoundToInt(RuneCount(Rune.Split) * 8);
        for (int i = 0; i < bolts; i++)
        {
            float a = i * 360f / bolts * Mathf.Deg2Rad;
            var p = Shoot(Owner.Position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 9f, 0.14f);
            p.Lifetime = 1.2f;
            p.OnHit = (proj, e) => Hit(e, 15f);
        }
        return true;
    }
}

// A water beam toward the nearest foe for a moment. Soaks foes (Wet) and shoves them back.
// II: a wide cone that pushes harder. III: the cone, plus pillars of water erupting under foes.
public class TidalLance : Spell
{
    public override string Name => "Tidal Lance";
    public override Element Element => Element.Water;
    public override string Blurb => "Sprays a beam of water that soaks and shoves foes.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 5f;   // per tick
    protected override float BaseCooldown => 5.5f;

    public override string TierName(int tier) => tier == 2 ? "Torrent" : tier == 3 ? "Monsoon" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "Sprays a cone instead of a beam, slowly pushing foes back."
                  : $"The cone, plus every {Secs(T("pillarEvery_t3", 2.5f))} pillars of water erupt under foes for {T("pillarDuration_t3", 2f):0.#}s, leaving them Wet for {T("pillarWet_t3", 5f):0.#}s.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return Tier == 3 ? "+1 beam and +1 water pillar" : "+1 beam at an angle";
            case Rune.Pierce: return "Spray lasts 30% longer";
            case Rune.Area: return $"+{Pct(AreaStep * T("areaScale", 0.5f))} spray size";
            default: return base.RuneEffect(r);
        }
    }

    float pillarTimer = 2f;
    // Area runes grow the spray at a reduced rate so a stacked cone doesn't cover the screen.
    float SprayScale => 1f + T("areaScale", 0.5f) * (Area - 1f);

    protected override void Cast()
    {
        var target = Enemy.Nearest(Owner.Position, Range);
        if (target == null) return;
        Sfx.Play("spells.TidalLance.cast", Sound.FireballCast, 0.5f, 0.2f);
        float duration = TT("duration", 1.2f) * (1f + 0.3f * RuneCount(Rune.Pierce));
        float cone = Tier >= 2 ? TT("coneAngle", 35f) : 0f;
        float push = Tier >= 2 ? TT("push", 12f) : T("push", 5f);
        for (int i = 0; i < Count; i++)
        {
            float offset = (i - (Count - 1) * 0.5f) * (Tier >= 2 ? cone * 1.6f : 25f);
            WaterBeam.Create(this, Owner, offset, duration, TT("length", 6.5f) * SprayScale, T("width", 0.55f) * SprayScale, cone, push, (e, p) => Hit(e, Damage, p));
        }
    }

    protected override void OnTick(float dt)
    {
        if (Tier < 3 || Owner == null) return;
        pillarTimer -= dt;
        if (pillarTimer > 0f) return;
        pillarTimer = T("pillarEvery_t3", 2.5f) * Tuning.CastRate;
        var used = new System.Collections.Generic.List<Enemy>();
        for (int i = 0; i < Count; i++)
        {
            var e = Enemy.Nearest(Owner.Position, Range, used);
            if (e == null) return;
            used.Add(e);
            WaterPillar.Create(e.Position, T("pillarRadius_t3", 1.3f) * Area, T("pillarDuration_t3", 2f), Color, (x, p) => Hit(x, Damage * T("pillarDamage_t3", 1.5f), p), T("pillarWet_t3", 5f));
        }
    }
}

// A whirlwind a few steps away that drags foes into it and passes their conditions around: every
// tick, each foe in the eye catches whatever the others are suffering from (burn, chill, wet,
// bugs, curse, wither) and takes reaction damage for each one it catches. Mixing burn and chill this way
// sets off Steam, wet and chill set off Deep Freeze.
// II: the whirlwind travels outward, away from you. III: it ticks twice as fast and its reactions hit harder.
public class Cyclone : Spell
{
    public override string Name => "Cyclone";
    public override Element Element => Element.Wind;
    public override string Blurb => "Summons a whirlwind that drags foes together and passes their conditions between them, dealing reaction damage.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 2.5f;   // per tick
    protected override float BaseCooldown => 6f;

    public override string TierName(int tier) => tier == 2 ? "Wandering Gale" : tier == 3 ? "Tempest" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "The whirlwind travels outward away from you, sweeping foes along."
                  : $"Travels, hits twice as often, and each condition it passes deals {Pct(T("passDamage_t3", 4f) / T("passDamage", 2f) - 1f)} more reaction damage.";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return $"+{Pct(PotencyStep)} damage and pull strength";
            case Rune.Split: return "+1 whirlwind";
            case Rune.Pierce: return "Lasts 30% longer";
            case Rune.Ember: case Rune.Frost: case Rune.Infest: case Rune.Curse:
                return base.RuneEffect(r) + ", then the whirlwind passes it around";
            case Rune.Area: return $"+{Pct(AreaStep)} pull radius";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var target = Enemy.Nearest(Owner.Position, Range);
        Vector2 dir = target != null || Joystick.MouseAim ? Joystick.Aim(Owner.Position, target != null ? target.Position : Owner.Position) : Random.insideUnitCircle.normalized;
        // Land just past the nearest foe so a chasing pack gets grabbed by its front, not its middle
        // (with the mouse: at the cursor, within the same limits).
        float dist = T("distance", 4.5f);
        if (Joystick.MouseAim)
            dist = Mathf.Clamp(Vector2.Distance(Owner.Position, Joystick.Cursor), T("minDistance", 2f), dist);
        else if (target != null)
            dist = Mathf.Clamp(Vector2.Distance(Owner.Position, target.Position) + T("frontOffset", 1f), T("minDistance", 2f), dist);
        Sfx.Play("spells.Cyclone.cast", Sound.Burrow, 0.4f, 0.3f);
        float tick = Tier == 3 ? T("tickEvery_t3", 0.125f) : T("tickEvery", 0.25f);
        float speed = Tier >= 2 ? TT("travelSpeed", 2.5f) : 0f;
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * 70f) * dir;
            Vector2 at = GameManager.Instance.Arena.PushOut(Owner.Position + d * dist, 0.5f);
            int reactions = 0;   // conditions passed by this one whirlwind, for the signature feat
            Vortex.Create(at, T("radius", 3f) * Area, TT("duration", 2.5f) * (1f + 0.3f * RuneCount(Rune.Pierce)), T("pull", 9f) * Potency, Color, e => Hit(e, Damage), d * speed, tick,
                (eye, foes) => { reactions += Swirl(eye, foes); Grimoire.FeatMax(SpellId.Cyclone, reactions); });
        }
    }

    // Pools the strongest of each condition among the foes in the eye, then hands every foe the ones
    // it doesn't have yet. Each condition caught deals reaction damage. Returns how many were passed.
    int Swirl(Vector2 eye, System.Collections.Generic.List<Enemy> foes)
    {
        float burn = 0f, bugs = 0f, curse = 0f, wither = 0f, witherSlow = 0f;
        bool chill = false, wet = false;
        foreach (var e in foes)
        {
            if (!e.IsAlive) continue;
            burn = Mathf.Max(burn, e.BurnDps);
            bugs = Mathf.Max(bugs, e.InfestDamage);
            curse = Mathf.Max(curse, e.CurseBonus);
            wither = Mathf.Max(wither, e.WitherAmount);
            witherSlow = Mathf.Max(witherSlow, e.WitherSlow);
            chill |= e.IsChilled;
            wet |= e.IsWet;
        }
        if (burn <= 0f && bugs <= 0f && curse <= 0f && wither <= 0f && !chill && !wet) return 0;

        float lasts = T("passDuration", 2f);
        float reaction = Damage * (Tier == 3 ? T("passDamage_t3", 4f) : T("passDamage", 2f));
        int bolts = 0, passed = 0;
        foreach (var e in foes)
        {
            if (!e.IsAlive) continue;
            bool wasWet = e.IsWet, wasChilled = e.IsChilled;
            int caught = 0;
            if (burn > 0f && !e.IsBurning) { e.ApplyBurn(burn, lasts); caught++; }   // + chilled: Steam
            if (chill && !e.IsChilled) { e.ApplyChill(lasts); caught++; }           // + burning: Steam
            if (wet && !e.IsWet) { e.ApplyWet(lasts); caught++; }
            if (bugs > 0f && !e.IsInfested) { e.Infest(bugs, lasts); caught++; }
            if (curse > 0f && e.CurseBonus <= 0f) { e.MarkCurse(curse); caught++; }
            if (wither > 0f && e.WitherAmount <= 0f) { e.Wither(wither, witherSlow, lasts); caught++; }
            passed += caught;
            if (caught == 0 || !e.IsAlive) continue;

            // Wet meeting chill in the storm freezes solid (Deep Freeze), whichever one was passed on.
            if ((wet && chill) && (!wasWet || !wasChilled))
            {
                e.AddFrostbite(T("deepFreezeFrostbite", 0.35f));
                Interactions.Trigger(Interaction.DeepFreeze, e.Position);
                e.ConsumeWet();
            }
            if (bolts++ < 6) SpellVfx.Spark(eye, e.Position, Color.Lerp(Color, Color.white, 0.3f));
            using (Telemetry.By(Id.ToString())) e.TakeDamage(reaction * caught, default, Color.Lerp(Color, Color.white, 0.4f));
        }
        return passed;
    }
}

// Turns the toughest non-boss foe in range against its friends for a few seconds.
// II: a burst that charms every foe around the target. III: charmed foes stay yours for good.
public class Enthrall : Spell
{
    public override string Name => "Enthrall";
    public override Element Element => Element.Shadow;
    public override string Blurb => $"The toughest foe nearby fights for you for {T("damage", 3f):0.#}s. Bosses are immune.";
    public override bool CanEvolve => true;
    protected override float BaseDamage => 3f;   // seconds
    protected override float BaseCooldown => 9f;

    public override string TierName(int tier) => tier == 2 ? "Mass Hysteria" : tier == 3 ? "Eternal Thrall" : Name;
    public override string TierDescription(int tier) =>
        tier == 2 ? "A burst around the toughest foe charms every foe caught in it."
                  : $"The burst, and charmed foes stay on your side permanently (up to {T("maxThralls_t3", 12f):0} at once).";

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return Tier == 3 ? $"Charmed foes hit {Pct(PotencyStep)} harder" : $"+{Pct(PotencyStep)} charm time";
            case Rune.Split: return Tier >= 2 ? "+1 burst" : "Charms +1 more foe";
            case Rune.Pierce: return "Charmed foes hit 25% harder";
            case Rune.Area: return Tier >= 2 ? $"+{Pct(AreaStep)} burst radius" : "Charmed foes burst on expiry, hurting neighbours";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool HitsFoes => false;   // charmed foes do the damage, outside Hit()

    protected override bool Ready() => Pick(null) != null && (Tier < 3 || Thralls() < T("maxThralls_t3", 12f));

    static int Thralls()
    {
        int n = 0;
        foreach (var e in Enemy.All) if (e.IsCharmed) n++;
        return n;
    }

    Enemy Pick(System.Collections.Generic.ICollection<Enemy> exclude)
    {
        Enemy best = null;
        float r2 = Range * Range;
        foreach (var e in Enemy.All)
        {
            if (!e.IsAlive || e.Hidden || e.IsBoss || e.IsCharmed) continue;
            if (exclude != null && exclude.Contains(e)) continue;
            if ((e.Position - Owner.Position).sqrMagnitude > r2) continue;
            if (best == null || e.MaxHealth > best.MaxHealth) best = e;
        }
        return best;
    }

    protected override void Cast()
    {
        var used = new System.Collections.Generic.List<Enemy>();
        float duration = Tier == 3 ? float.PositiveInfinity : Damage;   // Damage already includes Potency
        float power = (1f + 0.25f * RuneCount(Rune.Pierce)) * (Tier == 3 ? Potency : 1f);
        float burst = Tier == 1 && RuneCount(Rune.Area) > 0 ? 25f * Area : 0f;
        for (int i = 0; i < Count; i++)
        {
            var e = Pick(used);
            if (e == null) return;
            Sfx.Play("spells.Enthrall.cast", Sound.Teleport, 0.5f, 0.2f);
            if (Tier == 1)
            {
                used.Add(e);
                Fx.Ring(e.Position, Color, e.Radius * 2f, 0.4f);
                e.Charm(duration, power, burst);
                continue;
            }
            // Burst around the target.
            float radius = TT("burstRadius", 2.5f) * Area;
            Fx.Ring(e.Position, Color, radius, 0.5f);
            Fx.Burst(e.Position, Color, radius * 2f);
            foreach (var x in EnemiesWithin(e.Position, radius))
            {
                if (x.IsBoss) continue;
                if (Tier == 3 && Thralls() >= T("maxThralls_t3", 12f)) break;
                used.Add(x);
                x.Charm(duration, power, 0f);
            }
            used.Add(e);
        }
    }
}
