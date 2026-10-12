using UnityEngine;

// Periodic heal.
public class Rejuvenate : Spell
{
    public override string Name => "Rejuvenate";
    public override Element Element => Element.Light;
    public override string Blurb => "Heals you 6% of your health every few seconds.";
    protected override float BaseDamage => 0.06f;   // fraction of max health per pulse
    protected override float BaseCooldown => 9f;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return "+30% healing";
            case Rune.Split: return "+1 extra pulse each cast";
            case Rune.Area: return "Also heals a bit more when you're low";
            case Rune.Pierce: return "+10 max health";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => true;

    protected override void OnRunesChanged()
    {
        if (Owner == null) return;
        // Pierce means nothing for a heal, so it buys max health instead.
        float bonus = 10f * RuneCount(Rune.Pierce);
        Owner.MaxHealth = Player.BaseMaxHealth + bonus;
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
        Owner.Heal(amount);
        Fx.Ring(Owner.Position, Color, 1.2f, 0.4f);
        Fx.Burst(Owner.Position, Color, 2f);
        DamageText.Spawn(Owner.Position + Vector2.up * 0.6f, amount, new Color(0.5f, 1f, 0.5f));
        Sfx.Play(Sound.Gem, 0.5f, 0f);
    }
}

// Vines burst under a group of foes and hold them in place. Rooted foes can still shoot,
// but can't hurt you by touch.
public class Overgrowth : Spell
{
    public override string Name => "Overgrowth";
    public override Element Element => Element.Nature;
    public override string Blurb => "Vines trap foes in place. Trapped foes can't hurt you by touch.";
    protected override float BaseDamage => 6f;
    protected override float BaseCooldown => 5f;
    protected override float PotencyPerRune => 0.25f;
    float Radius => 1.8f * Area;
    float RootTime => 2.2f * Potency;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return "+25% damage and trap time";
            case Rune.Split: return "+1 patch of vines";
            case Rune.Pierce: return "Trapped foes take +15% damage from everything";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        Sfx.Play(Sound.Burrow, 0.5f);
        var used = new System.Collections.Generic.List<Enemy>();
        for (int i = 0; i < Count; i++)
        {
            var target = Enemy.Nearest(Owner.Position, Range, used) ?? Enemy.Nearest(Owner.Position, Range);
            if (target == null) return;
            Vector2 at = target.Position;
            Fx.Ring(at, Color, Radius, 0.5f);
            Fx.Burst(at, new Color(0.2f, 0.6f, 0.15f, 0.6f), Radius * 2f);
            foreach (var e in EnemiesWithin(at, Radius))
            {
                used.Add(e);
                e.Root(RootTime, 0.15f * RuneCount(Rune.Pierce));
                Hit(e, Damage);
            }
        }
    }
}

// Passive ward: when you'd take a hit while it's ready, you take nothing and turn ethereal.
public class Veil : Spell
{
    public override string Name => "Ethereal Veil";
    public override Element Element => Element.Arcane;
    public override string Blurb => "When ready, the next hit is ignored and you phase out for 1.5s.";
    protected override float BaseDamage => 1.5f;   // seconds of invulnerability
    protected override float BaseCooldown => 12f;
    protected override float PotencyPerRune => 0.25f;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return "+25% phase time";
            case Rune.Haste: return "Recharges 15% faster";
            case Rune.Area: return "Phasing out knocks nearby foes back";
            case Rune.Split: return "Phasing out also fires a ring of arcane bolts";
            case Rune.Echo: return "Holds two charges";
            default: return base.RuneEffect(r);
        }
    }

    int charges = 1;
    int MaxCharges => 1 + Echoes;
    protected override bool EchoRecasts => false;
    protected override bool Ready() => charges < MaxCharges;
    protected override void Cast() => charges++;
    public override float CooldownProgress => charges > 0 ? 1f : base.CooldownProgress;

    // Called by the Player when it is about to take damage.
    public bool TryAbsorb()
    {
        if (charges <= 0) return false;
        charges--;
        ResetCooldown();
        Owner.Phase(Damage);
        Sfx.Play(Sound.Teleport, 0.7f);
        Fx.Ring(Owner.Position, Color, 1.5f, 0.4f);
        if (RuneCount(Rune.Area) > 0)
            foreach (var e in EnemiesWithin(Owner.Position, 2f * Area))
                e.TakeDamage(0f, (e.Position - Owner.Position).normalized * 10f);
        int bolts = RuneCount(Rune.Split) * 8;
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
public class TidalLance : Spell
{
    public override string Name => "Tidal Lance";
    public override Element Element => Element.Water;
    public override string Blurb => "Sprays a beam of water that soaks and shoves foes.";
    protected override float BaseDamage => 5f;   // per tick
    protected override float BaseCooldown => 5.5f;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Split: return "+1 beam at an angle";
            case Rune.Pierce: return "Beam lasts 30% longer";
            case Rune.Area: return $"+{AreaPerRune * 100f:0}% beam length and width";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var target = Enemy.Nearest(Owner.Position, Range);
        if (target == null) return;
        Sfx.Play(Sound.FireballCast, 0.5f, 0.2f);
        float duration = 1.2f * (1f + 0.3f * RuneCount(Rune.Pierce));
        for (int i = 0; i < Count; i++)
        {
            float offset = (i - (Count - 1) * 0.5f) * 25f;
            WaterBeam.Create(this, Owner, offset, duration, 6.5f * Area, 0.55f * Area, (e, push) => Hit(e, Damage, push));
        }
    }
}

// A whirlwind a few steps away that drags foes into it.
public class Cyclone : Spell
{
    public override string Name => "Cyclone";
    public override Element Element => Element.Wind;
    public override string Blurb => "Summons a whirlwind that drags foes into it.";
    protected override float BaseDamage => 4f;   // per tick
    protected override float BaseCooldown => 6f;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return "+30% damage and pull strength";
            case Rune.Split: return "+1 whirlwind";
            case Rune.Pierce: return "Lasts 30% longer";
            case Rune.Area: return $"+{AreaPerRune * 100f:0}% pull radius";
            default: return base.RuneEffect(r);
        }
    }

    protected override void Cast()
    {
        var target = Enemy.Nearest(Owner.Position, Range);
        Vector2 dir = target != null ? (target.Position - Owner.Position).normalized : Random.insideUnitCircle.normalized;
        Sfx.Play(Sound.Burrow, 0.4f, 0.3f);
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) * 0.5f) * 70f) * dir;
            Vector2 at = GameManager.Instance.Arena.PushOut(Owner.Position + d * 4.5f, 0.5f);
            Vortex.Create(at, 3f * Area, 2.5f * (1f + 0.3f * RuneCount(Rune.Pierce)), 9f * Potency, Color, e => Hit(e, Damage));
        }
    }
}

// Turns the toughest non-boss foe in range against its friends for a few seconds.
public class Enthrall : Spell
{
    public override string Name => "Enthrall";
    public override Element Element => Element.Shadow;
    public override string Blurb => "The toughest foe nearby fights for you for 3s. Bosses resist.";
    protected override float BaseDamage => 3f;   // seconds
    protected override float BaseCooldown => 9f;

    public override string RuneEffect(Rune r)
    {
        switch (r)
        {
            case Rune.Potency: return "+30% charm time";
            case Rune.Split: return "Charms +1 more foe";
            case Rune.Pierce: return "Charmed foes hit 25% harder";
            case Rune.Area: return "Charmed foes burst on expiry, hurting neighbours";
            default: return base.RuneEffect(r);
        }
    }

    protected override bool Ready() => Pick(null) != null;

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
        for (int i = 0; i < Count; i++)
        {
            var e = Pick(used);
            if (e == null) return;
            used.Add(e);
            Fx.Bolt(Owner.Position, e.Position, Color);
            Fx.Ring(e.Position, Color, e.Radius * 2f, 0.4f);
            Sfx.Play(Sound.Teleport, 0.5f, 0.2f);
            e.Charm(Damage * Potency, 1f + 0.25f * RuneCount(Rune.Pierce), RuneCount(Rune.Area) > 0 ? 25f * Area : 0f);
        }
    }
}
