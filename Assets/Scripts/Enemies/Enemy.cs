using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// One enemy. Its EnemyDef says how it looks, moves and attacks; this class runs the behavior,
// status effects, contact damage and death drops.
//
// Statuses: burn, chill (50% slow), frostbite (stacking slow that shatters at 100%), wet, root,
// curse mark, infest, bless, charm. Several combine (see Interactions).
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();
    public static event Action<Enemy> Died;

    public EnemyDef Def { get; private set; }
    public float Radius => Def.Radius;
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool IsAlive => Health > 0f;
    public bool IsBoss => Def.Tier == EnemyTier.Boss;
    public bool IsChilled => chillTimer > 0f;
    public bool IsBurning => burnTimer > 0f;
    public bool IsWet => wetTimer > 0f;
    public bool IsRooted => rootTimer > 0f;
    public bool VineTrapped;   // rooted by Overgrowth (not a Stone Spikes pin), for its tier III feat
    public bool IsCharmed => charmTimer > 0f;
    public bool IsInfested => infestTimer > 0f;
    public float BurnDps => IsBurning ? burnDps : 0f;
    public float InfestDamage => IsInfested ? infestDamage : 0f;
    public float CurseBonus => curseBonus;
    // Cached, since crowds read each other's positions thousands of times a frame; MoveTo keeps it in step with the transform.
    public Vector2 Position { get; private set; }
    public bool Hidden { get; private set; }   // burrowed or banished: can't be hit or targeted

    // Banish: the foe is gone (hidden and frozen) for a while, then returns where it was, briefly
    // dazed. eraseOnReturn (Oblivion) makes it die instead of returning, without a bug burst.
    public bool IsBanished => banishTimer > 0f;
    public static int BanishedCount()
    {
        int n = 0;
        foreach (var e in All) if (e.IsAlive && e.IsBanished) n++;
        return n;
    }
    float banishTimer, dazeTimer;
    bool eraseOnReturn;
    Action<Enemy> onReturn;
    // Wither: the foe deals less damage and shoots less often for a while.
    float witherTimer, witherAmount, witherSlow;
    float Weak => witherTimer > 0f ? 1f - witherAmount : 1f;
    public float WitherAmount => witherTimer > 0f ? witherAmount : 0f;
    public float WitherSlow => witherTimer > 0f ? witherSlow : 0f;
    float FireRate => witherTimer > 0f ? 1f - witherSlow : 1f;
    public int TelemetryId, TelemetryRun;   // set by Telemetry when a run is being logged
    public float BornAt;
    public string LastHitBy;   // what last hurt it, for telemetry kill credit

    // Bigger enemies shrug off more frostbite.
    float FrostResist => Def.Tier == EnemyTier.Chaff ? Tune.F("status.frostbiteTakenChaff", 1f)
                       : Def.Tier == EnemyTier.Medium ? Tune.F("status.frostbiteTakenMedium", 0.55f)
                       : Tune.F("status.frostbiteTakenBoss", 0.2f);

    float fireTimer;
    float stateTimer;
    int state;
    int attackIndex;
    float spinAngle;
    float phase;
    Vector2 lockedDir;
    Vector2 knockback;
    Vector2 targetPos;     // who we're attacking: the wizard, or another enemy when charmed
    float flash;
    float chillTimer, wetTimer, rootTimer, rootVuln;
    float thornDps, thornTick;   // evolved Overgrowth: trapped foes bleed, and hurt (or trap) foes that bump them
    bool thornSpread;
    Action<Vector2> rootDeathBloom;   // Strangling Bramble: a trapped foe that dies sprouts new vines
    float shatterSpread, shatterRadius;
    static int shatterChain;
    float burnTimer, burnDps;
    float frostbite;
    float curseBonus;
    float infestTimer, infestDamage;
    float blessChance;
    float charmTimer, charmPower, charmBurst, charmContact;
    Enemy aggro;           // a charmed foe that hit this one: we fight it instead of the wizard for a while
    float aggroTimer;
    public static Enemy Acting;   // the enemy whose Update is running, so its hazards know who made them
    float steamCooldown;
    // Elemental gauge (combos.gauge): each burn, chill or wet application adds a unit (up to combos.gaugeMax),
    // and a reaction uses one unit of each side, so fire x2 then ice x1 leaves one fire, and 1 + 1 leaves nothing.
    int burnUnits, chillUnits, wetUnits;
    static bool Gauge => Tune.B("combos.gauge", true);
    static int GaugeMax => Mathf.Max(1, Tune.I("combos.gaugeMax", 3));
    float dodgeTimer;
    float shardTimer;
    SpriteRenderer body;
    SpriteRenderer[] renderers;
    SpriteRenderer curseMark, vines, charmAura, bugs;
    readonly List<Transform> books = new List<Transform>();
    EnemyAnimator anim;   // set when the body plays a sprite sheet
    Color bodyColor, shownColor;
    Enemy[] self;

    // cause is for telemetry: what made it appear (the Director passes trickle, swarm, boss...).
    public static Enemy Spawn(EnemyDef def, Vector2 position, float healthScale, string cause = "summon")
    {
        var go = new GameObject(def.Name);
        go.transform.position = position;
        var e = go.AddComponent<Enemy>();
        e.Position = position;
        e.Def = def;
        e.MaxHealth = e.Health = def.Health * healthScale;
        e.phase = Random.value * 10f;
        e.fireTimer = def.FireInterval * Tuning.CastRate * Random.Range(0.5f, 1f);
        e.stateTimer = Random.Range(2f, 4f);
        e.BuildVisual();
        Telemetry.Spawned(e, cause);
        return e;
    }

    // Charmed foes that can be hit (alive, above ground), gathered once per frame for enemy shots and hazards.
    static readonly List<Enemy> thralls = new List<Enemy>();
    static int thrallsFrame = -1;
    public static List<Enemy> Thralls()
    {
        if (thrallsFrame == Time.frameCount) return thralls;
        thrallsFrame = Time.frameCount;
        thralls.Clear();
        foreach (var e in All) if (e.IsAlive && e.IsCharmed && !e.Hidden) thralls.Add(e);
        return thralls;
    }

    public static Enemy Nearest(Vector2 from, float maxRange, ICollection<Enemy> exclude = null)
    {
        Enemy best = null;
        float bestSq = maxRange * maxRange;
        foreach (var e in All)
        {
            if (!e.IsAlive || e.Hidden || e.IsCharmed || (exclude != null && exclude.Contains(e))) continue;
            float sq = (e.Position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
    void OnDestroy() { if (IsAlive) Telemetry.Despawned(this); }

    void MoveTo(Vector2 p)
    {
        Position = p;
        transform.position = p;
    }

    // ------------------------------------------------------------------ visuals

    void BuildVisual()
    {
        var d = Def;
        float s = d.Radius * 2f;
        bodyColor = d.Color;
        int o = d.Tier == EnemyTier.Boss ? 40 : 20;
        SpriteFactory.Child(transform, "Shadow", SpriteFactory.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.05f, -0.35f) * s, s, -44);

        var art = ArtStyle.Enemy(d.Look);
        var sheet = ArtStyle.EnemySheet(d.Look, d.Name);
        if (sheet?["walk"] != null)
        {
            bodyColor = Color.white;
            body = SpriteFactory.Child(transform, "Body", sheet["walk"].Frames[0], bodyColor, Vector2.zero, ArtStyle.EnemySheetScale(d.Look, d.Radius, d.Name), 0);   // depth-sorted
            anim = EnemyAnimator.Attach(body, sheet);
        }
        else if (art != null)
        {
            bodyColor = Color.white;
            body = SpriteFactory.Child(transform, "Body", art, bodyColor, Vector2.zero, ArtStyle.EnemyScale(d.Look, d.Radius), 0);
        }
        else
        {
            switch (d.Look)
            {
                case EnemyLook.Bat:
                case EnemyLook.Pixie:
                    Wing(s, -1, o); Wing(s, 1, o);
                    break;
                case EnemyLook.Beast:
                    Horn(s, -1, o, 0.32f, 25f, d.Color * 0.8f);
                    Horn(s, 1, o, 0.32f, 25f, d.Color * 0.8f);
                    SpriteFactory.Child(transform, "Spines", SpriteFactory.Triangle, d.Accent, new Vector2(0f, -0.35f) * s, s * 0.4f, o - 1).transform.localRotation = Quaternion.Euler(0, 0, 180f);
                    break;
                case EnemyLook.Goblin:
                    Horn(s, -1, o, 0.45f, 70f, d.Color);
                    Horn(s, 1, o, 0.45f, 70f, d.Color);
                    break;
                case EnemyLook.Mole:
                    Horn(s, -1, o, 0.3f, 40f, d.Accent);
                    Horn(s, 1, o, 0.3f, 40f, d.Accent);
                    break;
                case EnemyLook.Treant:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        SpriteFactory.Child(transform, "Leaf", SpriteFactory.Circle, d.Accent * 0.8f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.42f, s * 0.5f, o - 1);
                    }
                    break;
                case EnemyLook.Golem:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                        var c = SpriteFactory.Child(transform, "Crystal", SpriteFactory.Diamond, d.Accent, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.45f, s * 0.35f, o - 1);
                        c.transform.localScale = new Vector3(s * 0.25f, s * 0.45f, 1f);
                        c.transform.localRotation = Quaternion.Euler(0, 0, i * 60f + 30f - 90f);
                    }
                    break;
            }

            body = SpriteFactory.Child(transform, "Body", SpriteFactory.Circle, bodyColor, Vector2.zero, s, o);

            if (d.Look == EnemyLook.Mushroom)
            {
                SpriteFactory.Child(transform, "Cap", SpriteFactory.Circle, d.Accent, new Vector2(0f, 0.35f) * s, s, o + 2).transform.localScale = new Vector3(s * 1.3f, s * 0.7f, 1f);
                for (int i = -1; i <= 1; i++)
                    SpriteFactory.Child(transform, "Spot", SpriteFactory.Circle, Color.white, new Vector2(i * 0.3f, 0.45f + (i == 0 ? 0.1f : 0f)) * s, s * 0.15f, o + 3);
            }
            if (d.Look == EnemyLook.Pixie)
                SpriteFactory.Child(transform, "Sparkle", SpriteFactory.Glow, new Color(d.Accent.r, d.Accent.g, d.Accent.b, 0.6f), Vector2.zero, s * 2.2f, o - 3);

            if (d.Look == EnemyLook.Wizard || d.Look == EnemyLook.Witch || d.Look == EnemyLook.Headmaster)
            {
                var hat = d.Look == EnemyLook.Headmaster ? d.Color * 0.7f + new Color(0, 0, 0, 0.3f) : d.Accent * 0.6f + new Color(0, 0, 0, 0.4f);
                SpriteFactory.Child(transform, "Brim", SpriteFactory.Circle, hat, new Vector2(0f, 0.3f) * s, s * 0.85f, o + 2).transform.localScale = new Vector3(s * 1.1f, s * 0.3f, 1f);
                SpriteFactory.Child(transform, "Hat", SpriteFactory.Triangle, hat, new Vector2(0f, 0.6f) * s, s * 0.7f, o + 3);
                if (d.Look == EnemyLook.Headmaster)
                    SpriteFactory.Child(transform, "Beard", SpriteFactory.Triangle, new Color(0.9f, 0.9f, 0.95f), new Vector2(0f, -0.3f) * s, s * 0.5f, o + 2).transform.localRotation = Quaternion.Euler(0, 0, 180f);
            }

            Color eye = d.Tier == EnemyTier.Chaff ? Color.white : d.Accent;
            SpriteFactory.Child(transform, "EyeL", SpriteFactory.Circle, eye, new Vector2(-0.17f, 0.05f) * s, s * 0.17f, o + 1);
            SpriteFactory.Child(transform, "EyeR", SpriteFactory.Circle, eye, new Vector2(0.17f, 0.05f) * s, s * 0.17f, o + 1);
            SpriteFactory.Child(transform, "PupilL", SpriteFactory.Circle, Color.black, new Vector2(-0.17f, 0.03f) * s, s * 0.08f, o + 2);
            SpriteFactory.Child(transform, "PupilR", SpriteFactory.Circle, Color.black, new Vector2(0.17f, 0.03f) * s, s * 0.08f, o + 2);
        }

        if (body != null) Lighting.MakeLit(body);

        // Status markers, hidden until needed.
        curseMark = SpriteFactory.Child(transform, "Curse", SpriteFactory.Diamond, new Color(0.7f, 0.3f, 0.9f), new Vector2(0f, 0.75f) * s, 0.3f, o + 5);
        vines = SpriteFactory.Child(transform, "Vines", SpriteFactory.Ring, new Color(0.25f, 0.7f, 0.2f, 0.9f), Vector2.zero, s * 1.3f, o + 4);
        charmAura = SpriteFactory.Child(transform, "Charm", SpriteFactory.Glow, new Color(1f, 0.4f, 0.75f, 0.7f), Vector2.zero, s * 2f, o - 3);
        bugs = SpriteFactory.Child(transform, "Bugs", SpriteFactory.Ring, new Color(0.55f, 0.7f, 0.15f, 0.9f), Vector2.zero, s * 0.9f, o + 4);

        if (d.Look == EnemyLook.Headmaster)
            for (int i = 0; i < 3; i++) AddBook();

        renderers = GetComponentsInChildren<SpriteRenderer>();
        curseMark.enabled = vines.enabled = charmAura.enabled = bugs.enabled = false;
    }

    void AddBook()
    {
        var book = new GameObject("Book").transform;
        book.SetParent(transform, false);
        SpriteFactory.Child(book, "Glow", SpriteFactory.Glow, new Color(Def.Accent.r, Def.Accent.g, Def.Accent.b, 0.5f), Vector2.zero, 1.1f, 44);
        var tome = ArtStyle.TomeSheet;
        if (tome?["spin"] != null)
        {
            // the 3D tome, spinning; its frames are 2.4 units wide, so this makes it about a unit across
            var sr = SpriteFactory.Child(book, "Tome", tome["spin"].Frames[0], Color.white, Vector2.zero, 0.5f, 0);
            Lighting.MakeLit(sr);
            Lighting.Point(book, Def.Accent, 2.2f, 0.7f);
            EnemyAnimator.Attach(sr, tome, "spin", false);
        }
        else
        {
            SpriteFactory.Child(book, "Cover", SpriteFactory.Square, new Color(0.55f, 0.15f, 0.2f), Vector2.zero, 0.45f, 45);
            SpriteFactory.Child(book, "Pages", SpriteFactory.Square, new Color(0.95f, 0.9f, 0.8f), new Vector2(0.05f, 0f), 0.35f, 46);
        }
        books.Add(book);
    }

    void SetHidden(bool hidden)
    {
        Hidden = hidden;
        foreach (var r in renderers) r.enabled = !hidden;
        if (!hidden) curseMark.enabled = vines.enabled = charmAura.enabled = bugs.enabled = false;
    }

    void Wing(float s, int side, int order)
    {
        var w = SpriteFactory.Child(transform, "Wing", SpriteFactory.Triangle, Def.Accent, new Vector2(side * 0.55f, 0.05f) * s, s * 0.8f, order - 1);
        w.transform.localRotation = Quaternion.Euler(0, 0, side * 70f);
    }

    void Horn(float s, int side, int order, float size, float angle, Color c)
    {
        var h = SpriteFactory.Child(transform, "Horn", SpriteFactory.Triangle, c, new Vector2(side * 0.32f, 0.4f) * s, s * size, order - 1);
        h.transform.localRotation = Quaternion.Euler(0, 0, -side * angle);
    }

    // ------------------------------------------------------------------ update

    // Spatial grid for crowd separation, rebuilt once a frame, so 200 foes don't each check all 200 others.
    // Cells are wider than any two non-boss foes plus a frame of movement, so the 3x3 neighbourhood covers every push.
    const float GridCell = 2f;
    static readonly Dictionary<int, List<Enemy>> grid = new Dictionary<int, List<Enemy>>();
    static readonly Stack<List<Enemy>> spareCells = new Stack<List<Enemy>>();
    static int gridFrame = -1;

    static int Cell(float v) => Mathf.FloorToInt(v / GridCell);
    static int CellKey(int x, int y) => (x & 0xFFFF) | (y << 16);

    static void BuildGrid()
    {
        if (gridFrame == Time.frameCount) return;
        gridFrame = Time.frameCount;
        foreach (var cell in grid.Values)
        {
            cell.Clear();
            spareCells.Push(cell);
        }
        grid.Clear();
        foreach (var e in All)
        {
            if (e.IsBanished) continue;   // banished foes aren't there to bump into
            int key = CellKey(Cell(e.Position.x), Cell(e.Position.y));
            if (!grid.TryGetValue(key, out var cell)) grid[key] = cell = spareCells.Count > 0 ? spareCells.Pop() : new List<Enemy>();
            cell.Add(e);
        }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;
        var player = gm.Player;
        if (player == null) return;
        Telemetry.Actor = Def.Name;   // bullets and hazards made below remember who fired them
        Acting = this;

        float dt = Time.deltaTime;
        if (banishTimer > 0f)
        {
            banishTimer -= dt;
            if (banishTimer <= 0f) Return();
            return;
        }
        if (dazeTimer > 0f) { dazeTimer -= dt; fireTimer = Mathf.Max(fireTimer, dazeTimer); }
        if (witherTimer > 0f) witherTimer -= dt;
        Vector2 pos = Position;

        // Pick a target: the wizard, or the nearest other enemy while charmed.
        Enemy prey = null;
        if (IsCharmed)
        {
            prey = Nearest(pos, 30f, self ??= new[] { this });
            targetPos = prey != null ? prey.Position : pos;
        }
        else if (Provoked)
        {
            aggroTimer -= dt;
            targetPos = aggro.Position;
        }
        else targetPos = player.Position;

        Vector2 toTarget = targetPos - pos;
        float dist = toTarget.magnitude;
        Vector2 dir = dist > 0.001f ? toTarget / dist : Vector2.up;
        float speed = Def.Speed * (IsChilled ? 1f - Tune.F("status.chillSlow", 0.5f) : 1f) * (1f - Tune.F("status.frostbiteMaxSlow", 0.6f) * Mathf.Clamp01(frostbite));
        if (IsRooted) speed = 0f;

        Vector2 move = Behave(dir, dist, speed, dt);
        if (IsRooted) move = Vector2.zero;

        // Light separation so crowds spread out instead of stacking into one blob.
        Enemy thorns = null;
        if (!IsBoss)
        {
            // Only foes in this and the neighbouring grid cells can be close enough to push.
            BuildGrid();
            int cx = Cell(pos.x), cy = Cell(pos.y);
            for (int gx = cx - 1; gx <= cx + 1; gx++)
            for (int gy = cy - 1; gy <= cy + 1; gy++)
            {
                if (!grid.TryGetValue(CellKey(gx, gy), out var cell)) continue;
                foreach (var other in cell)
                {
                    if (other == this) continue;
                    Vector2 d = pos - other.Position;
                    float r = (Radius + other.Radius) * 0.9f;
                    float sq = d.sqrMagnitude;
                    if (sq < r * r && sq > 0.0001f)
                    {
                        move += d / Mathf.Sqrt(sq) * 2f;
                        if (other.thornDps > 0f && other.IsRooted && !IsRooted) thorns = other;
                    }
                }
            }
        }
        thornTick -= dt;
        if (thorns != null && thornTick <= 0f)
        {
            // Bumped into a thorn-trapped foe.
            thornTick = 0.5f;
            if (thorns.thornSpread) Root(thorns.rootTimer, thorns.rootVuln, thorns.thornDps, true, thorns.rootDeathBloom);
            using (Telemetry.By("Thorns")) TakeDamage(thorns.thornDps * Tune.F("status.thornBumpFactor", 1f), default, new Color(0.5f, 0.85f, 0.3f));
            if (!IsAlive) return;
        }

        pos += (move + knockback) * dt * Tuning.Pace;
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, 30f * dt);
        if (Def.Look != EnemyLook.Bat) pos = gm.Arena.PushOut(pos, Radius * 0.8f);
        MoveTo(pos);

        UpdateStatus(dt);
        if (!IsAlive) return;

        // Contact damage. Rooted and charmed enemies never hurt the wizard by touch.
        float hit = Radius * 0.85f + Player.BodyRadius;
        if (!Hidden && !IsRooted && !IsCharmed && (player.Position - pos).sqrMagnitude < hit * hit)
            player.TakeDamage(Def.ContactDamage * Weak, Def.Name, "contact");

        if (IsCharmed && prey != null)
        {
            charmContact -= dt;
            float r = Radius + prey.Radius;
            if (charmContact <= 0f && (prey.Position - pos).sqrMagnitude < r * r)
            {
                charmContact = 0.5f;
                using (Telemetry.By("Enthrall")) prey.TakeDamage(Def.ContactDamage * 2f * charmPower, (prey.Position - pos).normalized * 4f, new Color(1f, 0.5f, 0.8f));
                if (!prey.IsAlive) Grimoire.Feat(SpellId.Enthrall);
                else prey.Provoke(this);
            }
        }
        // A provoked foe fights back by touch too.
        else if (Provoked && !Hidden && !IsRooted)
        {
            charmContact -= dt;
            float r = Radius + aggro.Radius;
            if (charmContact <= 0f && (aggro.Position - pos).sqrMagnitude < r * r)
            {
                charmContact = 0.5f;
                using (Telemetry.By("Foes on thralls")) aggro.TakeDamage(Def.ContactDamage, (aggro.Position - pos).normalized * 4f);
            }
        }
    }

    Vector2 Behave(Vector2 dir, float dist, float speed, float dt)
    {
        switch (Def.Behavior)
        {
            case EnemyBehavior.Chase:
            case EnemyBehavior.Splitter:
                return dir * speed;

            case EnemyBehavior.Flutter:
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                return (dir + perp * Mathf.Sin(Time.time * 5f + phase)).normalized * speed;
            }

            case EnemyBehavior.Lunge:
            {
                // 0 = stalk, 1 = wind up (telegraph), 2 = dash, 3 = recover
                stateTimer -= dt;
                switch (state)
                {
                    case 0:
                        if (dist < 5f && stateTimer <= 0f && !IsRooted)
                        {
                            state = 1; stateTimer = 0.7f; lockedDir = dir;
                            // Warning lane showing exactly where the charge will go.
                            Fx.Telegraph(Position, lockedDir, Def.Speed * 5f * 0.35f * Tuning.Pace + Radius, Radius * 1.6f, new Color(1f, 0.25f, 0.2f, 0.45f), 0.7f);
                        }
                        return dir * speed;
                    case 1:
                        flash = Mathf.Max(flash, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
                        MoveTo(Position + Random.insideUnitCircle * 0.04f);   // shiver
                        if (stateTimer <= 0f) { state = 2; stateTimer = 0.35f; }
                        return Vector2.zero;
                    case 2:
                        if (stateTimer <= 0f) { state = 3; stateTimer = 0.6f; }
                        if (Random.value < 0.5f) Fx.Burst(Position - lockedDir * Radius, Def.Color * 0.8f, Radius * 1.5f);   // dust trail
                        return lockedDir * Def.Speed * 5f;
                    default:
                        if (stateTimer <= 0f) { state = 0; stateTimer = 1.4f; }
                        return dir * speed * 0.3f;
                }
            }

            case EnemyBehavior.Caster:
                TickFire(dt, dist, () => Fan(Position, AngleTo(targetPos), Def.Bullets, Def.Spread, Def.BulletSpeed));
                return KeepRange(dir, dist, speed);

            case EnemyBehavior.Blinker:
            {
                stateTimer -= dt;
                if (stateTimer <= 0f && dist < 12f && !IsCharmed && !IsRooted)
                {
                    stateTimer = Random.Range(4f, 6f);
                    Blink(Def.PreferredRange);
                    fireTimer = 0.35f;  // fire shortly after appearing
                }
                TickFire(dt, dist, () => { Ring(Position, Def.Bullets, Def.BulletSpeed, spinAngle); spinAngle += 15f; });
                return KeepRange(dir, dist, speed);
            }

            case EnemyBehavior.Lobber:
                // Lead the target a little so standing still isn't the only answer.
                TickFire(dt, dist, () =>
                {
                    Vector2 lead = IsCharmed ? Vector2.zero : Joystick.Direction * GameManager.Instance.Player.MoveSpeed * Tuning.Pace * 0.6f;
                    Hazard.Spawn(targetPos + lead + Random.insideUnitCircle * 0.5f, 1.3f, 1.2f, Def.BulletDamage * Weak, Def.Accent, Position, IsCharmed);
                });
                return KeepRange(dir, dist, speed);

            case EnemyBehavior.Trickster:
            {
                dodgeTimer -= dt;
                if (dodgeTimer <= 0f && !IsRooted && SpellIncoming())
                {
                    dodgeTimer = 2.5f;
                    Vector2 side = new Vector2(-dir.y, dir.x) * (Random.value < 0.5f ? -1f : 1f);
                    TeleportTo(Position + side * Random.Range(2f, 3f) - dir * 0.5f);
                }
                TickFire(dt, dist, () =>
                {
                    float a = AngleTo(targetPos);
                    for (int i = 0; i < Def.Bullets; i++)
                    {
                        var p = Shoot(Position, a + (i - (Def.Bullets - 1) * 0.5f) * Def.Spread, Def.BulletSpeed);
                        p.WaveAmplitude = 0.6f;
                        p.WaveFrequency = 5f + i;
                    }
                });
                return KeepRange(dir, dist, speed) + new Vector2(-dir.y, dir.x) * Mathf.Sin(Time.time * 3f + phase) * speed * 0.5f;
            }

            case EnemyBehavior.Burrower:
            {
                // 0 = walk, 1 = tunnelling (hidden), 2 = about to erupt (warning circle), 3 = recover
                stateTimer -= dt;
                switch (state)
                {
                    case 0:
                        // A charmed burrower tunnels too, faster, so it erupts under other foes within the charm.
                        if (stateTimer <= 0f && dist < 9f && !IsRooted)
                        {
                            state = 1; stateTimer = IsCharmed ? 1.2f : 2.2f;
                            SetHidden(true);
                            Fx.Burst(Position, Def.Color, 1.5f);
                            Sfx.Play("enemies.burrow", Sound.Burrow, 0.5f);
                        }
                        return dir * speed * 0.4f;
                    case 1:
                        if (Random.value < 0.3f) Fx.Burst(Position, Def.Color * 0.9f, 0.8f);   // dirt churning above it
                        if (dist < 0.8f || stateTimer <= 0f)
                        {
                            state = 2; stateTimer = 0.8f;
                            Hazard.Spawn(Position, 1.5f, 0.8f, Def.ContactDamage * Weak, Def.Accent, null, IsCharmed);
                        }
                        return dir * Def.Speed;
                    case 2:
                        if (stateTimer <= 0f)
                        {
                            state = 3; stateTimer = 2.5f;
                            SetHidden(false);
                            Ring(Position, Def.Bullets, Def.BulletSpeed, Random.value * 360f);
                        }
                        return Vector2.zero;
                    default:
                        if (stateTimer <= 0f) { state = 0; stateTimer = 1.5f; }
                        return dir * speed * 0.3f;
                }
            }

            case EnemyBehavior.BossTreant: return BossTreant(dir, speed, dt);
            case EnemyBehavior.BossWitch: return BossWitch(dir, dist, speed, dt);
            case EnemyBehavior.BossHeadmaster: return BossHeadmaster(dir, dist, speed, dt);
            case EnemyBehavior.BossGolem: return BossGolem(dir, dist, speed, dt);
        }
        return Vector2.zero;
    }

    // ------------------------------------------------------------------ bosses

    Vector2 BossTreant(Vector2 dir, float speed, float dt)
    {
        // Below SummonBelow of its health a fifth attack joins the cycle: a brood of a Spore Lobber,
        // a Mother Slime and Moss Slimes (counts from tuning).
        fireTimer -= dt * FireRate;
        if (fireTimer <= 0f)
        {
            fireTimer = Def.FireInterval * Tuning.CastRate * (Health < MaxHealth * 0.5f ? 0.7f : 1f);
            var roster = GameManager.Instance.Arena.Biome.Roster;
            bool brood = Health < MaxHealth * Def.SummonBelow;
            switch (attackIndex++ % (brood ? 5 : 4))
            {
                case 0: Ring(Position, Def.Bullets, Def.BulletSpeed, spinAngle += 10f); break;
                case 1: Summon(roster.Chaff[0], Def.SummonCount); break;
                case 2:
                    for (int i = 0; i < 3; i++)
                        Fan(Position, AngleTo(targetPos), 5, 12f, Def.BulletSpeed * (1f + i * 0.25f));
                    break;
                case 3:
                    for (int i = 0; i < 5; i++)
                        Hazard.Spawn(targetPos + Random.insideUnitCircle * 3f, 1.4f, 1.4f, 20f, Def.Accent, Position);
                    break;
                default:
                    var defs = new System.Collections.Generic.List<EnemyDef>();
                    void Add(EnemyDef d, int n) { if (d != null) for (int i = 0; i < n; i++) defs.Add(d); }
                    Add(System.Array.Find(roster.Medium, d => d.Name == "Spore Lobber"), Def.BroodLobbers);
                    Add(System.Array.Find(roster.Medium, d => d.Name == "Mother Slime"), Def.BroodMothers);
                    Add(roster.Chaff[0], Def.BroodSlimes);
                    Summon(defs);
                    break;
            }
        }
        return dir * speed;
    }

    Vector2 BossWitch(Vector2 dir, float dist, float speed, float dt)
    {
        // Spiral for a while, then aimed fans / homing orbs, then blink somewhere else.
        stateTimer -= dt;
        if (stateTimer <= 0f)
        {
            state = (state + 1) % 3;
            stateTimer = state == 0 ? 4f : state == 1 ? 3f : 0.6f;
            if (state == 2) Blink(Def.PreferredRange);
        }
        fireTimer -= dt * FireRate;
        if (fireTimer <= 0f)
        {
            anim?.PlayCast();
            if (state == 0)
            {
                fireTimer = Def.FireInterval * Tuning.CastRate;
                Ring(Position, Def.Bullets, Def.BulletSpeed, spinAngle);
                spinAngle += 13f;
            }
            else if (state == 1)
            {
                fireTimer = 0.7f * Tuning.CastRate;
                if (attackIndex++ % 2 == 0) Fan(Position, AngleTo(targetPos), 7, 10f, 5f);
                else
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Shoot(Position, AngleTo(targetPos) + 90f * i + 45f, 3f);
                        p.Homing = 90f;
                        p.HomingTime = 2.5f;
                    }
            }
            else fireTimer = 0.2f;
        }
        return state == 2 ? Vector2.zero : KeepRange(dir, dist, speed);
    }

    // Headmaster Vexley: spellbooks orbit him and fire volleys; he sets "detention" grids of
    // telegraphed circles around you and casts slow rings with a single gap to slip through.
    Vector2 BossHeadmaster(Vector2 dir, float dist, float speed, float dt)
    {
        if (Health < MaxHealth * 0.5f && books.Count < 5) { AddBook(); AddBook(); renderers = GetComponentsInChildren<SpriteRenderer>(); }
        spinAngle += 70f * dt;
        for (int i = 0; i < books.Count; i++)
        {
            float a = (spinAngle + i * 360f / books.Count) * Mathf.Deg2Rad;
            books[i].localPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f;
        }

        fireTimer -= dt * FireRate;
        if (fireTimer <= 0f)
        {
            fireTimer = Def.FireInterval * Tuning.CastRate;
            anim?.PlayCast();
            switch (attackIndex++ % 4)
            {
                case 0:
                case 2:
                    // Every book fires a short aimed burst.
                    foreach (var b in books)
                    {
                        Vector2 from = b.position;
                        float a = Mathf.Atan2(targetPos.y - from.y, targetPos.x - from.x) * Mathf.Rad2Deg;
                        for (int k = -1; k <= 1; k++) Shoot(from, a + k * 8f, 4.2f);
                    }
                    break;
                case 1:
                    // Detention: a 3x3 grid of warning circles centred on the wizard, one cell left safe.
                    int safe = Random.Range(0, 9);
                    for (int gx = -1; gx <= 1; gx++)
                    for (int gy = -1; gy <= 1; gy++)
                    {
                        int cell = (gx + 1) * 3 + gy + 1;
                        if (cell == safe) continue;
                        Hazard.Spawn(targetPos + new Vector2(gx, gy) * 2f, 1.15f, 1.4f, 22f, Def.Accent);
                    }
                    break;
                default:
                {
                    // A slow ring with one gap facing a random side.
                    float gap = Random.value * 360f;
                    for (int i = 0; i < Def.Bullets; i++)
                    {
                        float a = i * 360f / Def.Bullets;
                        if (Mathf.Abs(Mathf.DeltaAngle(a, gap)) < 22f) continue;
                        Shoot(Position, a, Def.BulletSpeed);
                    }
                    if (Random.value < 0.5f) Blink(Def.PreferredRange);
                    break;
                }
            }
        }
        return KeepRange(dir, dist, speed);
    }

    // Crystal Golem: lumbers toward you, then a long telegraphed charge that leaves crystal shards
    // hanging in the air, a ground slam that throws shards outward, and lines of erupting spikes.
    Vector2 BossGolem(Vector2 dir, float dist, float speed, float dt)
    {
        stateTimer -= dt;
        switch (state)
        {
            case 0:   // walk
                if (stateTimer <= 0f)
                {
                    anim?.PlayCast();   // the slam wind-up
                    int attack = attackIndex++ % 3;
                    if (attack == 0)
                    {
                        state = 1; stateTimer = 1.1f; lockedDir = dir;
                        Fx.Telegraph(Position, lockedDir, 11f, Radius * 2f, new Color(0.5f, 0.95f, 1f, 0.35f), 1.1f);
                        Sfx.Play("boss.golemSlam", Sound.Warning, 0.4f, 0f);
                    }
                    else if (attack == 1)
                    {
                        state = 3; stateTimer = 1f;
                        Hazard.Spawn(Position, 3.2f, 1f, 30f, Def.Accent);
                    }
                    else
                    {
                        // Spike line marching toward the wizard.
                        for (int i = 0; i < 7; i++)
                            StartCoroutine(SpikeLater(Position + dir * (1.5f + i * 1.3f), 0.15f * i));
                        stateTimer = Def.FireInterval * Tuning.CastRate;
                    }
                }
                return dir * speed;
            case 1:   // charge wind-up
                flash = Mathf.Max(flash, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
                if (stateTimer <= 0f) { state = 2; stateTimer = 11f / (9f * Tuning.Pace); }
                return Vector2.zero;
            case 2:   // charging, dropping shards
                shardTimer -= dt;
                if (shardTimer <= 0f)
                {
                    shardTimer = 0.12f;
                    var p = Shoot(Position + Random.insideUnitCircle * 0.5f, Random.value * 360f, 0.05f);
                    p.Lifetime = 3.5f;
                }
                if (stateTimer <= 0f) { state = 0; stateTimer = Def.FireInterval * Tuning.CastRate; }
                return lockedDir * 9f;
            default:  // slam
                if (stateTimer <= 0f)
                {
                    Ring(Position, Def.Bullets, Def.BulletSpeed, Random.value * 360f);
                    GameManager.Instance.Shake(0.4f);
                    Particles.Slam(Position, Def.Color, Radius);
                    state = 0; stateTimer = Def.FireInterval * Tuning.CastRate;
                }
                return Vector2.zero;
        }
    }

    System.Collections.IEnumerator SpikeLater(Vector2 at, float delay)
    {
        yield return new WaitForSeconds(delay);
        Telemetry.Actor = Def.Name;
        Hazard.Spawn(at, 0.85f, 0.8f, 18f, Def.Accent);
    }

    // ------------------------------------------------------------------ attack helpers

    Vector2 KeepRange(Vector2 dir, float dist, float speed)
    {
        float range = IsCharmed ? 2.5f : Def.PreferredRange;
        if (dist > range + 1f) return dir * speed;
        if (dist < range - 1.5f) return -dir * speed;
        return new Vector2(-dir.y, dir.x) * speed * 0.5f * Mathf.Sign(Mathf.Sin(phase));
    }

    void TickFire(float dt, float dist, Action fire)
    {
        fireTimer -= dt * FireRate;
        if (fireTimer > 0f) return;
        fireTimer = Def.FireInterval * Tuning.CastRate;
        if (dist < 11f && (!IsCharmed || dist > 0.01f)) { anim?.PlayCast(); fire(); }
    }

    void Blink(float range)
    {
        var gm = GameManager.Instance;
        for (int i = 0; i < 8; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector2 p = gm.Player.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * range;
            if (!gm.Arena.Inside(p, -1f) || gm.Arena.Blocked(p, Radius)) continue;
            TeleportTo(p);
            return;
        }
    }

    void TeleportTo(Vector2 p)
    {
        var arena = GameManager.Instance.Arena;
        p = arena.PushOut(p, Radius);
        Fx.Burst(Position, Def.Accent, Radius * 3f);
        Fx.Ring(Position, Def.Accent, Radius * 1.5f);
        Sfx.Play("enemies.teleport", Sound.Teleport, 0.35f);
        MoveTo(p);
        Fx.Burst(p, Def.Accent, Radius * 3f);
    }

    // A wizard spell heading our way and about to arrive.
    bool SpellIncoming()
    {
        foreach (var p in Projectile.All)
        {
            if (p.Team != Team.Player) continue;
            Vector2 to = Position - p.Position;
            if (to.sqrMagnitude < 2.5f * 2.5f && Vector2.Dot(to, p.Velocity) > 0f) return true;
        }
        return false;
    }

    void Summon(EnemyDef def, int count)
    {
        var defs = new System.Collections.Generic.List<EnemyDef>();
        for (int i = 0; i < count; i++) defs.Add(def);
        Summon(defs);
    }

    // Spawns the given enemies evenly around this one.
    void Summon(System.Collections.Generic.List<EnemyDef> defs)
    {
        for (int i = 0; i < defs.Count; i++)
        {
            float a = i * Mathf.PI * 2f / defs.Count;
            Spawn(defs[i], Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Radius + 0.8f), GameManager.Instance.Director.HealthScale);
        }
        Fx.Burst(Position, Def.Accent, Radius * 4f);
    }

    float AngleTo(Vector2 target) => Mathf.Atan2(target.y - Position.y, target.x - Position.x) * Mathf.Rad2Deg;

    void Fan(Vector2 from, float centerDeg, int n, float spread, float speed)
    {
        for (int i = 0; i < n; i++)
            Shoot(from, centerDeg + (i - (n - 1) * 0.5f) * spread, speed);
    }

    void Ring(Vector2 from, int n, float speed, float offset)
    {
        for (int i = 0; i < n; i++)
            Shoot(from, offset + i * 360f / n, speed);
    }

    Projectile Shoot(Vector2 from, float angleDeg, float speed)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 v = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
        Projectile p;
        if (IsCharmed)
        {
            // A charmed enemy's shots are ours now.
            p = Projectile.Spawn(from, v, Team.Player, new Color(1f, 0.5f, 0.8f), 0.14f);
            p.Damage = Def.BulletDamage * 1.5f * charmPower;
            p.OnHit = (proj, e) => { if (!e.IsAlive) Grimoire.Feat(SpellId.Enthrall); else e.Provoke(this); };
        }
        else
        {
            p = Projectile.Spawn(from, v, Team.Enemy, Def.Accent, 0.14f);
            p.Damage = Def.BulletDamage * Weak;
        }
        p.Lifetime = 6f;
        if (IsBoss) Particles.Muzzle(from, v, ArtStyle.EnemyBullet(Def.Accent));
        Sfx.Play("enemies.shoot", Sound.EnemyShoot, 0.2f, 0.15f);
        return p;
    }

    // ------------------------------------------------------------------ statuses

    void UpdateStatus(float dt)
    {
        if (burnTimer > 0f)
        {
            burnTimer -= dt;
            Credit(burnDps * dt, "Burn");
            Health -= burnDps * dt;
            if (Health <= 0f) { Die(); return; }
        }
        if (chillTimer > 0f) chillTimer -= dt;
        if (wetTimer > 0f) wetTimer -= dt;
        if (rootTimer > 0f)
        {
            rootTimer -= dt;
            if (thornDps > 0f)
            {
                Credit(thornDps * dt, "Thorns");
                Health -= thornDps * dt;
                if (Health <= 0f) { Die(); return; }
            }
            if (rootTimer <= 0f) { thornDps = 0f; thornSpread = false; rootDeathBloom = null; VineTrapped = false; }
        }
        if (infestTimer > 0f) infestTimer -= dt;
        if (steamCooldown > 0f) steamCooldown -= dt;
        if (frostbite > 0f) frostbite = Mathf.Max(0f, frostbite - 0.2f * dt);
        if (charmTimer > 0f)
        {
            charmTimer -= dt;
            if (charmTimer <= 0f) EndCharm();
        }
        if (flash > 0f) flash -= dt * 6f;

        if (!Hidden)
        {
            Show(curseMark, curseBonus > 0f);
            if (curseBonus > 0f) curseMark.transform.localScale = Vector3.one * (0.25f + 0.05f * curseBonus * 2f);
            Show(vines, IsRooted);
            Show(charmAura, IsCharmed);
            Show(bugs, IsInfested);
            Particles.Status(Position, Radius, IsBurning, IsChilled, IsWet, IsInfested, IsRooted, dt);
            if (IsInfested) bugs.transform.Rotate(0f, 0f, 500f * dt);
        }

        Color c = bodyColor;
        if (IsWet) c = Color.Lerp(c, new Color(0.25f, 0.45f, 0.9f), 0.35f);
        if (IsChilled || frostbite > 0.05f) c = Color.Lerp(c, new Color(0.55f, 0.85f, 1f), Mathf.Max(IsChilled ? 0.55f : 0f, Mathf.Clamp01(frostbite)));
        if (IsBurning) c = Color.Lerp(c, new Color(1f, 0.5f, 0.15f), 0.35f + 0.25f * Mathf.Sin(Time.time * 20f));
        if (IsCharmed) c = Color.Lerp(c, new Color(1f, 0.5f, 0.8f), 0.4f);
        c = Color.Lerp(c, ArtStyle.HitFlash, Mathf.Clamp01(flash));
        if (c != shownColor) body.color = shownColor = c;
    }

    // Renderer toggles and tints only cross into the engine when they actually change.
    static void Show(SpriteRenderer r, bool on)
    {
        if (r.enabled != on) r.enabled = on;
    }

    public void TakeDamage(float amount, Vector2 push = default, Color? numberColor = null)
    {
        if (!IsAlive || Hidden) return;
        if (IsRooted) amount *= 1f + rootVuln;
        if (amount > 0f) Credit(amount, Telemetry.DamageBy ?? "Other");
        Health -= amount;
        if (amount > 0f)
        {
            flash = 1f;
            DamageText.Spawn(Position + Vector2.up * Radius, amount, numberColor ?? Color.white);
            Particles.Hit(Position, numberColor ?? Color.white, push);
            Sfx.Play("enemies.hit", Sound.EnemyHit, 0.25f, 0.15f);
        }
        if (!IsBoss && !IsRooted) knockback += push;
        if (Health <= 0f) Die();
    }

    void Credit(float amount, string by)
    {
        LastHitBy = by;
        Telemetry.Dealt(Mathf.Min(amount, Health), by);
    }

    public void Pull(Vector2 velocity, float dt)
    {
        if (IsRooted || Hidden) return;
        MoveTo(Position + velocity * dt * Tuning.Pace);
    }

    public void ApplyBurn(float dps, float duration)
    {
        // Gauge: fire meeting ice reacts (Steam) and the two cancel one unit for one unit.
        if (Gauge && IsChilled && steamCooldown <= 0f) { Steam(); UseChill(); return; }
        bool had = burnTimer > 0f;
        if (!had) Sfx.Play("status.burn", Sound.Sizzle, 0.5f, 0.2f);
        burnDps = Mathf.Max(had ? burnDps : 0f, dps);
        burnTimer = Mathf.Max(burnTimer, duration);
        burnUnits = Mathf.Min(GaugeMax, (had ? burnUnits : 0) + 1);
        if (!Gauge && IsChilled) Steam();
    }

    public void ApplyChill(float duration)
    {
        if (Gauge && IsBurning && steamCooldown <= 0f) { Steam(); UseBurn(); return; }
        bool had = chillTimer > 0f;
        if (!had) Sfx.Play("status.chill", Sound.Chill, 0.5f, 0.2f);
        chillTimer = Mathf.Max(chillTimer, duration);
        chillUnits = Mathf.Min(GaugeMax, (had ? chillUnits : 0) + 1);
        if (!Gauge && IsBurning) Steam();
    }

    public void Banish(float duration, bool erase, Action<Enemy> returned)
    {
        if (!IsAlive || Hidden || IsBanished) return;
        if (IsBoss) { Daze(Tune.F("spells.Banish.bossDaze", 1f)); return; }
        banishTimer = duration;
        eraseOnReturn = erase;
        onReturn = returned;
        knockback = Vector2.zero;
        Particles.Death(Position, Def.Look, Radius, Def.Tier);
        Fx.Ring(Position, new Color(0.55f, 0.25f, 0.75f), Radius * 2f, 0.3f);
        SetHidden(true);
    }

    void Return()
    {
        if (eraseOnReturn)
        {
            infestTimer = 0f;   // erased foes leave nothing behind, not even bugs
            Fx.Burst(Position, new Color(0.55f, 0.25f, 0.75f), Radius * 2f);
            Die();
            onReturn?.Invoke(this);
            return;
        }
        SetHidden(false);
        Fx.Ring(Position, new Color(0.55f, 0.25f, 0.75f), Radius * 2f, 0.3f);
        Daze(Tune.F("spells.Banish.returnDaze", 0.5f));
        onReturn?.Invoke(this);
        onReturn = null;
    }

    // A dazed foe can't shoot.
    public void Daze(float duration)
    {
        dazeTimer = Mathf.Max(dazeTimer, duration);
        fireTimer = Mathf.Max(fireTimer, duration);
    }

    public void Wither(float amount, float slow, float duration)
    {
        if (witherTimer <= 0f) Sfx.Play("status.wither", Sound.Wither, 0.5f, 0.2f);
        witherAmount = witherTimer > 0f ? Mathf.Max(witherAmount, amount) : amount;
        witherSlow = witherTimer > 0f ? Mathf.Max(witherSlow, slow) : slow;
        witherTimer = Mathf.Max(witherTimer, duration);
    }

    // Water meeting fire (a Soak rune on Comet Trail): Steam, the same as fire meeting ice.
    public void Scald()
    {
        if (!IsBurning || !IsWet || steamCooldown > 0f) return;
        Steam();
        ConsumeWet();
    }

    public void ApplyWet(float duration)
    {
        if (wetTimer <= 0f) Sfx.Play("status.wet", Sound.Splash, 0.5f, 0.2f);
        wetUnits = Mathf.Min(GaugeMax, (wetTimer > 0f ? wetUnits : 0) + 1);
        wetTimer = Mathf.Max(wetTimer, duration);
    }

    // A reaction used up one unit of wet (Conductive, Deep Freeze). The last unit dries the foe off.
    public void ConsumeWet()
    {
        if (!Gauge || wetTimer <= 0f) return;
        if (--wetUnits <= 0) { wetUnits = 0; wetTimer = 0f; }
    }

    void UseBurn()
    {
        if (--burnUnits <= 0) { burnUnits = 0; burnTimer = 0f; burnDps = 0f; }
    }

    void UseChill()
    {
        if (--chillUnits <= 0) { chillUnits = 0; chillTimer = 0f; }
    }

    public void Root(float duration, float vulnerability, float thornsDps = 0f, bool spreads = false, Action<Vector2> deathBloom = null)
    {
        if (deathBloom != null) rootDeathBloom = deathBloom;
        rootTimer = Mathf.Max(rootTimer, IsBoss ? duration * Tune.F("status.rootBossFactor", 0.25f) : duration);
        rootVuln = Mathf.Max(rootVuln, vulnerability);
        thornDps = Mathf.Max(thornDps, thornsDps);
        thornSpread |= spreads;
    }

    // Stacking slow from evolved Frost Nova. At 100% the enemy shatters.
    // spread > 0 (Shatterstorm): when this foe shatters, foes near it gain that much frostbite,
    // so shatters can chain through a pack (up to status.shatterChainDepth links).
    public void AddFrostbite(float amount, float spread = 0f, float spreadRadius = 0f)
    {
        frostbite += amount * FrostResist;
        shatterSpread = Mathf.Max(shatterSpread, spread);
        shatterRadius = Mathf.Max(shatterRadius, spreadRadius);
        if (frostbite < 1f) return;
        frostbite = 0f;
        if (shatterSpread > 0f && shatterChain < Tune.I("status.shatterChainDepth", 3))
        {
            shatterChain++;
            foreach (var e in Spell.EnemiesWithin(Position, shatterRadius))
                if (e != this) e.AddFrostbite(shatterSpread, shatterSpread, shatterRadius);
            shatterChain--;
        }
        Grimoire.Feat(SpellId.FrostNova);
        float dmg = Health * (IsBoss ? Tune.F("status.shatterBoss", 0.1f) : Tune.F("status.shatter", 0.3f));
        Fx.Burst(Position, new Color(0.7f, 0.95f, 1f), Radius * 4f);
        Fx.Ring(Position, Color.white, Radius * 2f);
        Particles.Frost(Position, Radius * 2f);
        Sfx.Play("combos.shatter", Sound.Lightning, 0.5f, 0.3f);
        using (Telemetry.By("Shatter")) TakeDamage(dmg, default, new Color(0.7f, 0.95f, 1f));
    }

    public void MarkCurse(float bonus) => curseBonus = Mathf.Max(curseBonus, bonus);

    public float ConsumeCurse()
    {
        float b = curseBonus;
        curseBonus = 0f;
        if (b > 0f) Fx.Burst(Position, new Color(0.7f, 0.3f, 0.9f), Radius * 2.5f);
        return b;
    }

    public void Infest(float burstDamage, float duration)
    {
        infestDamage = Mathf.Max(infestDamage * (IsInfested ? 1f : 0f), burstDamage);
        infestTimer = Mathf.Max(infestTimer, duration);
    }

    public void Bless(float chance) => blessChance = Mathf.Max(blessChance, chance);

    bool Provoked => aggro != null && aggro.IsAlive && aggro.IsCharmed && aggroTimer > 0f;

    // A charmed foe hit this one: it turns on that thrall (Enthrall's aggro) for spells.Enthrall.aggroTime.
    // Bosses ignore thralls.
    public void Provoke(Enemy by)
    {
        if (IsBoss || IsCharmed || by == null || by == this || !by.IsCharmed) return;
        aggro = by;
        aggroTimer = Tune.F("spells.Enthrall.aggroTime", 4f);
    }

    public void Charm(float duration, float power, float burstOnEnd)
    {
        if (IsBoss) return;
        aggro = null;
        // Newly charmed foes act at once instead of finishing a cooldown or a wind-up aimed at the wizard,
        // which often outlasted a short charm.
        if (!IsCharmed)
        {
            fireTimer = Mathf.Min(fireTimer, 0.3f);
            if (Def.Behavior == EnemyBehavior.Lunge || Def.Behavior == EnemyBehavior.Burrower)
            {
                if (state == 3 || (Def.Behavior == EnemyBehavior.Lunge && state == 1)) state = 0;
                if (state == 0) stateTimer = 0f;
            }
        }
        charmTimer = duration;
        charmPower = power;
        charmBurst = burstOnEnd;
    }

    void EndCharm()
    {
        if (charmBurst > 0f)
        {
            Fx.Burst(Position, new Color(1f, 0.4f, 0.75f), 3f);
            foreach (var e in Spell.EnemiesWithin(Position, 1.8f))
                if (e != this) using (Telemetry.By("Enthrall")) e.TakeDamage(charmBurst, (e.Position - Position).normalized * 5f, new Color(1f, 0.5f, 0.8f));
        }
        charmBurst = 0f;
    }

    void Steam()
    {
        if (steamCooldown > 0f) return;
        steamCooldown = 1f;
        Interactions.Trigger(Interaction.Steam, Position);
        float dmg = Mathf.Min(Tune.F("combos.steamMaxDamage", 60f), Tune.F("combos.steamBaseDamage", 10f) + MaxHealth * Tune.F("combos.steamHealthFraction", 0.08f));
        Fx.Burst(Position, new Color(0.95f, 0.95f, 0.95f, 0.8f), 3f);
        Sfx.Play("combos.steam", Sound.FireballCast, 0.35f, 0.3f);
        using (Telemetry.By("Steam"))
        foreach (var e in Spell.EnemiesWithin(Position, 1.6f))
            e.TakeDamage(dmg, (e.Position - Position).normalized * 3f, new Color(0.95f, 0.95f, 0.95f));
    }

    void InfestBurst()
    {
        bool fire = IsBurning, hex = curseBonus > 0f;
        float radius = fire ? Tune.F("combos.firebugsRadius", 2f) : Tune.F("runes.Infest.burstRadius", 1.4f);
        if (fire) Interactions.Trigger(Interaction.Firebugs, Position);
        if (hex) Interactions.Trigger(Interaction.HexSwarm, Position);
        if (!fire && !hex) EffectCounter.Count("Bug Burst", Position, new Color(0.6f, 0.75f, 0.2f));
        var c = fire ? new Color(1f, 0.6f, 0.2f) : new Color(0.6f, 0.75f, 0.2f);
        Fx.Burst(Position, c, radius * 2f);
        Fx.Ring(Position, c, radius, 0.3f);
        Particles.Ants(Position, radius, fire);
        Sfx.Play("status.infestBurst", Sound.EnemyDie, 0.5f, 0.3f);
        foreach (var e in Spell.EnemiesWithin(Position, radius))
        {
            if (e == this) continue;
            if (fire) e.ApplyBurn(infestDamage * 0.4f, 2.5f);
            if (hex) e.MarkCurse(curseBonus);
            using (Telemetry.By(fire ? "Firebugs" : hex ? "Hex Swarm" : "Bug Burst")) e.TakeDamage(infestDamage, (e.Position - Position).normalized * 3f, c);
        }
    }

    // drops.extra: a list of bonus drops, each { name, tier: Any|Chaff|Medium|Boss, chance, xp, heal }.
    void DropExtras()
    {
        foreach (var drop in Tune.List("drops.extra"))
        {
            string tier = Tune.Str(drop, "tier", "Any");
            if (tier != "Any" && tier != Def.Tier.ToString()) continue;
            if (Random.value >= Tune.Num(drop, "chance", 0f)) continue;
            int xp = Mathf.RoundToInt(Tune.Num(drop, "xp", 0f));
            if (xp > 0) XpGem.Spawn(Position + Random.insideUnitCircle * 0.4f, xp);
            float heal = Tune.Num(drop, "heal", 0f);
            if (heal > 0f)
            {
                GameManager.Instance.Player.Heal(heal, source: "Drop");
                DamageText.Spawn(Position, heal, new Color(0.5f, 1f, 0.5f));
            }
        }
    }

    void Die()
    {
        if (!enabled) return;
        Health = 0f;
        enabled = false;
        var gm = GameManager.Instance;
        gm.AddScore(Def.Xp * 10);

        bool blessed = blessChance > 0f && Random.value < blessChance;
        int xp = Mathf.Max(1, Mathf.RoundToInt(Def.Xp * Tune.F("drops.xpMultiplier", 1f)));
        if (blessed) xp = Mathf.Max(Tune.I("runes.Bless.minGem", 20), Mathf.RoundToInt(xp * Tune.F("runes.Bless.gemMultiplier", 4f)));
        XpGem.Spawn(Position, xp);
        if (blessed) Fx.Ring(Position, new Color(1f, 0.95f, 0.6f), 1f, 0.4f);
        DropExtras();

        Fx.Burst(Position, Def.Color, Radius * 2.5f, false);   // Particles.Death throws the debris
        Particles.Death(Position, Def.Look, Radius, Def.Tier);
        if (!IsBoss) Sfx.Play("enemies.die", Sound.EnemyDie, Def.Tier == EnemyTier.Medium ? 0.6f : 0.35f, 0.2f);
        if (IsInfested) InfestBurst();
        if (IsRooted) rootDeathBloom?.Invoke(Position);
        if (IsRooted && VineTrapped) Grimoire.Feat(SpellId.Overgrowth);
        Telemetry.Killed(this);   // before Died: a boss kill can end the run
        Died?.Invoke(this);
        if (Def.Behavior == EnemyBehavior.Splitter)
        {
            var small = gm.Arena.Biome.Roster.Chaff[0];
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + phase;
                var child = Spawn(small, Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f, gm.Director.HealthScale, "split");
                child.knockback = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f;
            }
        }
        anim?.PlayDeath();   // a sheet with a death clip leaves the body behind to play it
        Destroy(gameObject);
    }
}
