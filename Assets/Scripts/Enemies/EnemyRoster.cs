using UnityEngine;

public enum EnemyTier { Chaff, Medium, Boss }

// How an enemy moves and attacks. Melee types hurt on contact; casters shoot bullet patterns.
public enum EnemyBehavior
{
    Chase, Flutter, Lunge, Caster, Blinker,
    Lobber,     // lobs telegraphed AoE spore bombs at where you're standing
    Trickster,  // teleports away when your spells get close, fires weaving shots
    Burrower,   // tunnels underground (can't be hit), then erupts under you
    Splitter,   // slow blob that bursts into smaller slimes when killed
    BossTreant, BossWitch,
    BossHeadmaster,  // orbiting spellbook turrets, detention grids of AoE, gapped bullet rings
    BossGolem,       // long telegraphed charges that leave crystal shards, ground slams, spike lines
}

// What an enemy looks like (all placeholder shapes for now).
public enum EnemyLook { Blob, Bat, Beast, Goblin, Wizard, Mushroom, Pixie, Mole, Treant, Witch, Headmaster, Golem }

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
    public bool EarlyGame = true;   // false = only appears after the first swarm
    public int MaxAlive;            // mediums: the Director won't pick it while this many are alive (0 = no cap)

    // Elder Treant: chaff per summon; below SummonBelow of its health it also summons a brood
    public int SummonCount = 5;
    public float SummonBelow = 0f;
    public int BroodLobbers, BroodMothers, BroodSlimes;
}

// The enemies that appear in one biome, split by tier. The Director picks from these.
public class EnemyRoster
{
    public EnemyDef[] Chaff;
    public EnemyDef[] Medium;
    public EnemyDef[] Bosses;

    // Same enemies everywhere for now; the biome only decides which boss comes first.
    public static EnemyRoster Standard(string firstBoss)
    {
        var r = Forest;
        var list = new System.Collections.Generic.List<EnemyDef>(r.Bosses);
        int i = list.FindIndex(b => b.Name == firstBoss);
        if (i > 0) { var first = list[i]; list.RemoveAt(i); list.Insert(0, first); }
        r.Bosses = list.ToArray();
        // Numbers come from the tuner: enemies.<Name>.<Field> (Health, Speed, Xp, BulletDamage...).
        foreach (var group in new[] { r.Chaff, r.Medium, r.Bosses })
            foreach (var d in group) Tune.Apply(d, "enemies." + d.Name);
        return r;
    }

    public static EnemyRoster Forest => new EnemyRoster
    {
        Chaff = new[]
        {
            new EnemyDef { Name = "Moss Slime", Tier = EnemyTier.Chaff, Behavior = EnemyBehavior.Chase, Look = EnemyLook.Blob,
                Health = 20, Speed = 1.7f, Radius = 0.35f, ContactDamage = 8, Xp = 1,
                Color = new Color(0.45f, 0.85f, 0.35f), Accent = new Color(0.25f, 0.55f, 0.2f) },
            new EnemyDef { Name = "Dusk Bat", Tier = EnemyTier.Chaff, Behavior = EnemyBehavior.Flutter, Look = EnemyLook.Bat,
                Health = 14, Speed = 3.1f, Radius = 0.28f, ContactDamage = 6, Xp = 1,
                Color = new Color(0.45f, 0.3f, 0.6f), Accent = new Color(0.25f, 0.15f, 0.35f) },
        },
        Medium = new[]
        {
            new EnemyDef { Name = "Thornback Wolf", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Lunge, Look = EnemyLook.Beast,
                Health = 100, Speed = 2.4f, Radius = 0.5f, ContactDamage = 15, Xp = 5,
                Color = new Color(0.55f, 0.5f, 0.45f), Accent = new Color(0.85f, 0.3f, 0.25f) },
            new EnemyDef { Name = "Goblin Hexer", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Caster, Look = EnemyLook.Goblin,
                Health = 70, Speed = 1.8f, Radius = 0.42f, ContactDamage = 8, Xp = 5,
                Color = new Color(0.55f, 0.75f, 0.3f), Accent = new Color(1f, 0.55f, 0.2f),
                FireInterval = 2.2f, Bullets = 3, Spread = 14f, BulletSpeed = 4.5f, PreferredRange = 6f },
            new EnemyDef { Name = "Rogue Apprentice", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Blinker, Look = EnemyLook.Wizard,
                Health = 85, Speed = 1.6f, Radius = 0.42f, ContactDamage = 8, Xp = 7,
                Color = new Color(0.85f, 0.25f, 0.35f), Accent = new Color(1f, 0.4f, 0.7f),
                FireInterval = 3f, Bullets = 12, BulletSpeed = 3.4f, PreferredRange = 5f },
            new EnemyDef { Name = "Spore Lobber", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Lobber, Look = EnemyLook.Mushroom,
                Health = 80, Speed = 1.3f, Radius = 0.45f, ContactDamage = 8, Xp = 6, EarlyGame = false,
                Color = new Color(0.9f, 0.85f, 0.75f), Accent = new Color(0.85f, 0.25f, 0.3f),
                FireInterval = 2.8f, BulletDamage = 18, PreferredRange = 7f },
            new EnemyDef { Name = "Pixie Trickster", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Trickster, Look = EnemyLook.Pixie,
                Health = 41, Speed = 2.6f, Radius = 0.3f, ContactDamage = 6, Xp = 6, EarlyGame = false,
                Color = new Color(1f, 0.75f, 0.95f), Accent = new Color(0.4f, 1f, 0.9f),
                FireInterval = 1.8f, Bullets = 3, Spread = 20f, BulletSpeed = 4f, PreferredRange = 5f },
            new EnemyDef { Name = "Root Burrower", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Burrower, Look = EnemyLook.Mole,
                Health = 110, Speed = 3.6f, Radius = 0.5f, ContactDamage = 15, Xp = 7, EarlyGame = false,
                Color = new Color(0.5f, 0.35f, 0.25f), Accent = new Color(0.95f, 0.75f, 0.4f),
                Bullets = 10, BulletSpeed = 3.5f, BulletDamage = 20 },
            new EnemyDef { Name = "Mother Slime", Tier = EnemyTier.Medium, Behavior = EnemyBehavior.Splitter, Look = EnemyLook.Blob,
                Health = 90, Speed = 1.2f, Radius = 0.65f, ContactDamage = 12, Xp = 4,
                Color = new Color(0.3f, 0.75f, 0.4f), Accent = new Color(0.15f, 0.45f, 0.2f) },
        },
        Bosses = new[]
        {
            new EnemyDef { Name = "Elder Treant", Tier = EnemyTier.Boss, Behavior = EnemyBehavior.BossTreant, Look = EnemyLook.Treant,
                Health = 2400, Speed = 1.1f, Radius = 1.3f, ContactDamage = 25, Xp = 60,
                Color = new Color(0.45f, 0.32f, 0.2f), Accent = new Color(0.4f, 0.85f, 0.35f),
                FireInterval = 2.6f, Bullets = 18, BulletSpeed = 3.2f },
            new EnemyDef { Name = "The Hedge Witch", Tier = EnemyTier.Boss, Behavior = EnemyBehavior.BossWitch, Look = EnemyLook.Witch,
                Health = 3200, Speed = 1.8f, Radius = 0.9f, ContactDamage = 20, Xp = 80,
                Color = new Color(0.3f, 0.15f, 0.4f), Accent = new Color(0.5f, 1f, 0.6f),
                FireInterval = 0.12f, Bullets = 3, BulletSpeed = 3.6f, PreferredRange = 6f },
            new EnemyDef { Name = "Headmaster Vexley", Tier = EnemyTier.Boss, Behavior = EnemyBehavior.BossHeadmaster, Look = EnemyLook.Headmaster,
                Health = 2800, Speed = 1.4f, Radius = 0.9f, ContactDamage = 20, Xp = 70,
                Color = new Color(0.2f, 0.25f, 0.55f), Accent = new Color(1f, 0.8f, 0.3f),
                FireInterval = 2.4f, Bullets = 36, BulletSpeed = 2.6f, PreferredRange = 6.5f },
            new EnemyDef { Name = "Crystal Golem", Tier = EnemyTier.Boss, Behavior = EnemyBehavior.BossGolem, Look = EnemyLook.Golem,
                Health = 3600, Speed = 0.9f, Radius = 1.4f, ContactDamage = 30, Xp = 80,
                Color = new Color(0.4f, 0.4f, 0.48f), Accent = new Color(0.5f, 0.95f, 1f),
                FireInterval = 3f, Bullets = 20, BulletSpeed = 3f, BulletDamage = 14 },
        },
    };
}
