using UnityEngine;

public enum EnemyTier { Chaff, Medium, Boss }

// How an enemy moves and attacks. Melee types hurt on contact; casters shoot bullet patterns.
public enum EnemyBehavior { Chase, Flutter, Lunge, Caster, Blinker, BossTreant, BossWitch }

// What an enemy looks like (all placeholder shapes for now).
public enum EnemyLook { Blob, Bat, Beast, Goblin, Wizard, Treant, Witch }

public class EnemyDef
{
    public string Name;
    public EnemyTier Tier;
    public EnemyBehavior Behavior;
    public EnemyLook Look;
    public float Health;
    public float Speed;
    public float Radius;
    public float ContactDamage;
    public int Xp;
    public Color Color;
    public Color Accent;

    // Caster settings
    public float FireInterval = 2f;
    public int Bullets = 3;
    public float Spread = 20f;      // degrees between bullets in a fan
    public float BulletSpeed = 4.5f;
    public float BulletDamage = 10f;
    public float PreferredRange = 6f;
}

// The enemies that appear in one biome, split by tier. The Director picks from these.
public class EnemyRoster
{
    public EnemyDef[] Chaff;
    public EnemyDef[] Medium;
    public EnemyDef[] Bosses;

    public static EnemyRoster Forest => new EnemyRoster
    {
        Chaff = new[]
        {
            new EnemyDef { Name = "Moss Slime", Tier = EnemyTier.Chaff, Behavior = EnemyBehavior.Chase, Look = EnemyLook.Blob,
                Health = 6, Speed = 1.7f, Radius = 0.35f, ContactDamage = 8, Xp = 1,
                Color = new Color(0.45f, 0.85f, 0.35f), Accent = new Color(0.25f, 0.55f, 0.2f) },
            new EnemyDef { Name = "Dusk Bat", Tier = EnemyTier.Chaff, Behavior = EnemyBehavior.Flutter, Look = EnemyLook.Bat,
                Health = 4, Speed = 3.1f, Radius = 0.28f, ContactDamage = 6, Xp = 1,
                Color = new Color(0.45f, 0.3f, 0.6f), Accent = new Color(0.25f, 0.15f, 0.35f) },
        },
        Medium = new[]
        {
            new EnemyDef { Name = "Thornback Wolf", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Lunge, Look = EnemyLook.Beast,
                Health = 40, Speed = 2.4f, Radius = 0.5f, ContactDamage = 15, Xp = 5,
                Color = new Color(0.55f, 0.5f, 0.45f), Accent = new Color(0.85f, 0.3f, 0.25f) },
            new EnemyDef { Name = "Goblin Hexer", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Caster, Look = EnemyLook.Goblin,
                Health = 28, Speed = 1.8f, Radius = 0.42f, ContactDamage = 8, Xp = 5,
                Color = new Color(0.55f, 0.75f, 0.3f), Accent = new Color(1f, 0.55f, 0.2f),
                FireInterval = 2.2f, Bullets = 3, Spread = 14f, BulletSpeed = 4.5f, PreferredRange = 6f },
            new EnemyDef { Name = "Rogue Apprentice", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Blinker, Look = EnemyLook.Wizard,
                Health = 34, Speed = 1.6f, Radius = 0.42f, ContactDamage = 8, Xp = 7,
                Color = new Color(0.85f, 0.25f, 0.35f), Accent = new Color(1f, 0.4f, 0.7f),
                FireInterval = 3f, Bullets = 12, BulletSpeed = 3.4f, PreferredRange = 5f },
        },
        Bosses = new[]
        {
            new EnemyDef { Name = "Elder Treant", Tier = EnemyTier.Boss, Behavior = EnemyBehavior.BossTreant, Look = EnemyLook.Treant,
                Health = 900, Speed = 1.1f, Radius = 1.3f, ContactDamage = 25, Xp = 60,
                Color = new Color(0.45f, 0.32f, 0.2f), Accent = new Color(0.4f, 0.85f, 0.35f),
                FireInterval = 2.6f, Bullets = 18, BulletSpeed = 3.2f },
            new EnemyDef { Name = "The Hedge Witch", Tier = EnemyTier.Boss, Behavior = EnemyBehavior.BossWitch, Look = EnemyLook.Witch,
                Health = 1200, Speed = 1.8f, Radius = 0.9f, ContactDamage = 20, Xp = 80,
                Color = new Color(0.3f, 0.15f, 0.4f), Accent = new Color(0.5f, 1f, 0.6f),
                FireInterval = 0.12f, Bullets = 3, BulletSpeed = 3.6f, PreferredRange = 6f },
        },
    };
}
