using System.Collections.Generic;
using UnityEngine;

public enum GameState { Title, Grimoire, TomePick, Playing, LevelUp, GameOver, Victory }

// Owns the run: title screen, camera, starting tome, level-up flow, zone changes, game over, victory
// and the HUD.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static float UiScale => Mathf.Min(Screen.width / 720f, Screen.height / 1280f);

    const string BestKey = "best_time_Runes";
    const string WinsKey = "wins";
    const float FadeTime = 0.6f;
    const float CameraSize = 7.5f;
    static readonly Color GoldColor = new Color(1f, 0.85f, 0.4f);

    public GameState State { get; private set; }
    public Player Player { get; private set; }
    public Arena Arena { get; private set; }
    public Director Director { get; private set; }
    public UpgradeSystem Upgrades { get; private set; }
    public int Score { get; private set; }
    public static bool Autoplay;
    public float ViewRadius => cam.orthographicSize * Mathf.Sqrt(1f + cam.aspect * cam.aspect);
    // How far spells look for targets: about the screen's half-height, a little past the edge.
    public float TargetRange => cam.orthographicSize + 1f;

    Camera cam;
    Vector2 camPos;
    float shake;
    int pendingLevelUps;
    float gameOverTime;
    List<Biome> zones;
    int zoneIndex;
    float shiftAt = -1f;     // when the next zone change happens (game time), or -1
    float fadeInUntil;
    float runGold;           // gold picked up this run, banked when the run ends
    int bankedGold = -1;     // what the last run paid out (-1 = not banked yet)
    int grimoirePage;
    GameState grimoireReturn;   // where Back goes: the title, or the death / victory screen
    readonly List<(string text, float until)> toasts = new List<(string, float)>();

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;

        SetupCamera();
        Director = gameObject.AddComponent<Director>();
        gameObject.AddComponent<Joystick>();
        gameObject.AddComponent<Sfx>();
        gameObject.AddComponent<ArtOverlay>();
        if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        BuildWorld();
        State = GameState.Title;
        StartCoroutine(Tune.FetchFromMac());
    }

    // Going to the background may be the last we see of this run: keep its log on the device.
    void OnApplicationPause(bool paused) { if (paused) Telemetry.SaveToDevice(); }

    void OnEnable() => Enemy.Died += OnEnemyDied;
    void OnDisable() => Enemy.Died -= OnEnemyDied;

    void OnEnemyDied(Enemy e)
    {
        if (State != GameState.Playing) return;
        runGold += e.Def.Tier == EnemyTier.Boss ? Tune.F("meta.goldPerBoss", 25f)
                 : e.Def.Tier == EnemyTier.Medium ? Tune.F("meta.goldPerMedium", 1f)
                 : Tune.F("meta.goldPerKill", 0.1f);
    }

    // Pays out the run's gold once: pickups plus time survived, plus the win bonus.
    void BankGold(bool won)
    {
        if (bankedGold >= 0) return;
        float total = runGold + Director.Elapsed / 60f * Tune.F("meta.goldPerMinute", 5f) + (won ? Tune.F("meta.goldWin", 50f) : 0f);
        bankedGold = Mathf.FloorToInt(total);
        Grimoire.AddGold(bankedGold);
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = CameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    void BuildWorld(Biome biome = null)
    {
        ClearField();
        Clear<XpGem>();
        if (Player != null) Destroy(Player.gameObject);
        if (Arena != null) Destroy(Arena.gameObject);

        Arena = Arena.Create(biome ?? Biome.Random());
        Player = Player.Create(Vector2.zero);
        camPos = Vector2.zero;
    }

    // Everything that lives in the play field, apart from the wizard and the arena itself.
    static void ClearField()
    {
        Clear<Projectile>();
        Clear<Enemy>();
        Clear<Fx>();
        Clear<Hazard>();
        Clear<DamageText>();
        Clear<Vortex>();
        Clear<WaterBeam>();
        Clear<WaterPillar>();
    }

    static void Clear<T>() where T : MonoBehaviour
    {
        foreach (var o in FindObjectsByType<T>()) Destroy(o.gameObject);
    }

    public void StartRun()
    {
        zones = Biome.RunZones();
        zoneIndex = 0;
        shiftAt = -1f;
        BuildWorld(zones[0]);
        Score = 0;
        runGold = 0f;
        bankedGold = -1;
        pendingLevelUps = 0;
        toasts.Clear();
        Director.ResetRun(Arena.Biome);
        Upgrades = new RuneSystem();
        Upgrades.Begin(Player);
        Telemetry.BeginRun(this, zones);
        State = GameState.TomePick;
        Time.timeScale = 0f;
        Joystick.Blocked = true;   // released once the starting tap lifts
    }

    void BeginPlaying()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;
        Toast(Arena.Biome.Name);
    }

    // Called by the Director when a boss dies. The last boss wins the run; the others move it on to
    // the next zone after a short pause to collect the loot.
    public void BossDefeated(bool final)
    {
        Grimoire.RecordBossKill(Player);
        if (final) { Win(); return; }
        Toast("+1 rune socket on every spell");
        Director.Holding = true;
        shiftAt = Time.time + Tune.F("run.zoneShiftDelay", 3f);
    }

    void ShiftZone()
    {
        shiftAt = -1f;
        if (zoneIndex + 1 >= zones.Count) zones.Add(Biome.Random());   // runs with more bosses than zones
        var next = zones[++zoneIndex];
        XpGem.CollectAll(Player);
        ClearField();
        Destroy(Arena.gameObject);
        Arena = Arena.Create(next);
        Player.transform.position = Vector3.zero;
        camPos = Vector2.zero;
        Director.EnterZone(next);
        Telemetry.Zone(zoneIndex + 1, next.Name);
        Director.Holding = false;
        fadeInUntil = Time.time + FadeTime;
        Toast(next.Name);
        Sfx.Play(Sound.Warning, 0.5f, 0f);
    }

    void Win()
    {
        State = GameState.Victory;
        gameOverTime = Time.unscaledTime;
        Grimoire.RecordWin(Player);
        BankGold(true);
        Telemetry.EndRun(this, "win", bankedGold);
        PlayerPrefs.SetInt(WinsKey, PlayerPrefs.GetInt(WinsKey, 0) + 1);
        if (Director.Elapsed > PlayerPrefs.GetFloat(BestKey, 0f)) PlayerPrefs.SetFloat(BestKey, Director.Elapsed);
        PlayerPrefs.Save();
        if (Player != null) Particles.LevelUp(Player.Position);
        StartCoroutine(Tune.FetchFromMac());
    }

    public void AddScore(int points)
    {
        if (State == GameState.Playing) Score += points;
    }

    public void QueueLevelUp() => pendingLevelUps++;

    public void Toast(string text) => toasts.Add((text, Time.unscaledTime + 2.5f));

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    public void GameOver()
    {
        State = GameState.GameOver;
        gameOverTime = Time.unscaledTime;
        BankGold(false);
        Telemetry.EndRun(this, "death", bankedGold);
        StartCoroutine(Tune.FetchFromMac());   // pick up tuner edits before the next try
        if (Director.Elapsed > PlayerPrefs.GetFloat(BestKey, 0f))
        {
            PlayerPrefs.SetFloat(BestKey, Director.Elapsed);
            PlayerPrefs.Save();
        }
    }

    void Update()
    {
        if (State == GameState.Playing && shiftAt > 0f && Time.time >= shiftAt) ShiftZone();
        if (State == GameState.Playing || State == GameState.LevelUp) Telemetry.Update(this);

        if (Autoplay && State == GameState.TomePick)
        {
            Upgrades.AutoPickStart();
            BeginPlaying();
        }

        if (State == GameState.Playing && pendingLevelUps > 0)
        {
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            Joystick.Blocked = true;
            Upgrades.Open();
            Sfx.Play(Sound.LevelUp, 0.8f, 0f);
            if (Player != null) Particles.LevelUp(Player.Position);
            Haptics.Notification(Haptics.Notify.Success);
        }

        // Autoplay soak test: random upgrade picks, the wizard kites on its own.
        if (Autoplay && State == GameState.LevelUp)
        {
            Upgrades.AutoPick();
            if (--pendingLevelUps > 0) Upgrades.Open();
            else { State = GameState.Playing; Time.timeScale = 1f; }
        }

        if (Joystick.Blocked && State == GameState.Playing && !Input.GetMouseButton(0) && Input.touchCount == 0)
            Joystick.Blocked = false;

        if (Player != null)
            camPos = Vector2.Lerp(camPos, Player.Position, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        float h = cam.orthographicSize, w = h * cam.aspect, lim = Arena.HalfSize + 1f;
        Vector2 p = new Vector2(Mathf.Clamp(camPos.x, -lim + w, lim - w), Mathf.Clamp(camPos.y, -lim + h, lim - h));
        if (shake > 0f)
        {
            shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime);
            p += Random.insideUnitCircle * shake * 0.6f;
        }
        cam.transform.position = new Vector3(p.x, p.y, -10f);

#if UNITY_EDITOR
        if (State == GameState.Playing && Input.GetKeyDown(KeyCode.L)) QueueLevelUp();
#endif
    }

    // ---------------------------------------------------------------- HUD and menus

    void OnGUI()
    {
        GUI.depth = 0;
        switch (State)
        {
            case GameState.Title: DrawTitle(); break;
            case GameState.Grimoire: DrawGrimoire(); break;
            case GameState.TomePick:
                if (Upgrades.DrawStart())
                {
                    Sfx.Play(Sound.Select, 0.8f);
                    Haptics.Selection();
                    BeginPlaying();
                }
                break;
            case GameState.Playing: DrawHud(); DrawZoneFade(); break;
            case GameState.LevelUp:
                DrawHud();
                if (Upgrades.Draw())
                {
                    Sfx.Play(Sound.Select, 0.8f);
                    Haptics.Selection();
                    pendingLevelUps--;
                    if (pendingLevelUps > 0) Upgrades.Open();
                    else { State = GameState.Playing; Time.timeScale = 1f; }
                }
                break;
            case GameState.GameOver: DrawHud(); DrawGameOver(); break;
            case GameState.Victory: DrawHud(); DrawVictory(); break;
        }
        DrawToasts();
    }

    void DrawTitle()
    {
        float s = UiScale;
        UI.Dim(0.55f);
        var col = UI.Column(0.18f, 0.8f);
        UI.Text(new Rect(col.x, col.y, col.width, 90f * s), "Wizard Bullet Hell", 60, TextAnchor.UpperCenter, new Color(0.8f, 0.65f, 1f));
        float best = PlayerPrefs.GetFloat(BestKey, 0f);
        int wins = PlayerPrefs.GetInt(WinsKey, 0);
        if (best > 0f)
            UI.Text(new Rect(col.x, col.y + 90f * s, col.width, 50f * s), wins > 0 ? $"Wins: {wins}  ·  Best: {FormatTime(best)}" : $"Best: {FormatTime(best)}", 28, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.4f));

        if (UI.Button(new Rect(col.x + col.width * 0.15f, col.y + 220f * s, col.width * 0.7f, 120f * s), "Play", new Color(0.6f, 0.45f, 1f)))
        {
            Sfx.Play(Sound.Select, 0.8f);
            Haptics.Selection();
            StartRun();
        }
        if (UI.Button(new Rect(col.x + col.width * 0.15f, col.y + 355f * s, col.width * 0.7f, 80f * s), $"Grimoire  ·  {Grimoire.Gold} gold", GoldColor))
            OpenGrimoire();
        DrawStylePicker(new Rect(col.x, col.y + 455f * s, col.width, 110f * s));

        string tuning = Tune.Fetching ? "Tuning: checking Mac..." : $"Tuning: {Tune.Source}";
        UI.Text(new Rect(col.x, col.y + 720f * s, col.width, 40f * s), tuning, 20, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.5f), false);
        if (!Tune.Fetching && UI.Button(new Rect(col.x + col.width * 0.3f, col.y + 765f * s, col.width * 0.4f, 64f * s), "Reload tuning", new Color(0.5f, 0.5f, 0.6f)))
            StartCoroutine(Tune.FetchFromMac());
        UI.Text(new Rect(col.x, col.y + 585f * s, col.width, 140f * s),
            "Drag anywhere to move. Spells cast on their own.\nLevel up to socket runes into your spells.\nBeat the bosses for more sockets and a new zone.",
            24, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.75f), false);
    }

    // Between runs: spend gold on new tomes, and on evolutions whose feat is done.
    // Four spells per page; each row has a tome button and one button per evolution tier.
    void DrawGrimoire()
    {
        float s = UiScale;
        UI.Dim(0.75f);
        var col = UI.Column(0.08f, 0.88f);
        UI.Text(new Rect(col.x, col.y, col.width, 60f * s), "Grimoire", 52, TextAnchor.UpperCenter, new Color(0.8f, 0.65f, 1f));
        UI.Text(new Rect(col.x, col.y + 62f * s, col.width, 40f * s), $"{Grimoire.Gold} gold", 30, TextAnchor.UpperCenter, GoldColor);

        const int perPage = 4;
        var ids = Spell.AllIds;
        int pages = (ids.Length + perPage - 1) / perPage;
        grimoirePage = Mathf.Clamp(grimoirePage, 0, pages - 1);
        float rowH = 190f * s, y = col.y + 115f * s, gap = 10f * s;
        for (int i = grimoirePage * perPage; i < Mathf.Min(ids.Length, (grimoirePage + 1) * perPage); i++)
        {
            DrawGrimoireRow(ids[i], new Rect(col.x, y, col.width, rowH));
            y += rowH + 14f * s;
        }

        float by = col.y + 115f * s + perPage * (rowH + 14f * s) + 6f * s, bw = (col.width - 2f * gap) / 3f, bh = 80f * s;
        if (grimoirePage > 0 && UI.Button(new Rect(col.x, by, bw, bh), "‹ Prev", new Color(0.6f, 0.6f, 0.7f))) grimoirePage--;
        if (UI.Button(new Rect(col.x + bw + gap, by, bw, bh), "Back", new Color(0.6f, 0.45f, 1f))) State = grimoireReturn;
        if (grimoirePage < pages - 1 && UI.Button(new Rect(col.x + 2f * (bw + gap), by, bw, bh), "Next ›", new Color(0.6f, 0.6f, 0.7f))) grimoirePage++;
        UI.Text(new Rect(col.x, by + bh + 8f * s, col.width, 30f * s), $"Page {grimoirePage + 1} / {pages}", 20, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.5f), false);
    }

    void OpenGrimoire()
    {
        Sfx.Play(Sound.Select, 0.8f);
        Haptics.Selection();
        grimoirePage = 0;
        grimoireReturn = State;
        State = GameState.Grimoire;
    }

    void DrawGrimoireRow(SpellId id, Rect r)
    {
        float s = UiScale, gap = 10f * s;
        var spell = Spell.Preview(id);
        bool owned = Grimoire.HasTome(id);
        UI.Box(r, new Color(1f, 1f, 1f, 0.06f));
        UI.Text(new Rect(r.x + 14f * s, r.y + 8f * s, r.width - 28f * s, 40f * s), spell.Name, 30, TextAnchor.UpperLeft, owned ? spell.Color : new Color(0.6f, 0.6f, 0.65f));
        if (spell.CanEvolve)   // what the evolutions are called, e.g. "II Inferno · III Sunfall"
            UI.Text(new Rect(r.x + 14f * s, r.y + 14f * s, r.width - 28f * s, 30f * s), $"II {spell.TierName(2)}  ·  III {spell.TierName(3)}", 20, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.6f), false);

        int tiers = spell.CanEvolve ? 2 : 0;
        float bw = (r.width - 28f * s - gap * tiers) / (1 + tiers), bh = 70f * s, bx = r.x + 14f * s, by = r.y + 52f * s;
        var dim = new Color(0.4f, 0.4f, 0.45f);

        string tomeLabel = owned ? "Tome owned" : $"Tome  {Grimoire.TomeCost}g";
        bool canBuyTome = !owned && Grimoire.Gold >= Grimoire.TomeCost;
        if (UI.Button(new Rect(bx, by, bw, bh), tomeLabel, owned ? spell.Color : canBuyTome ? GoldColor : dim) && canBuyTome && Grimoire.BuyTome(id))
            Bought();

        string nextFeat = owned ? "" : "Buy the tome to add it to your runs.";
        for (int tier = 2; tier <= 1 + tiers; tier++)
        {
            var rect = new Rect(bx + (tier - 1) * (bw + gap), by, bw, bh);
            string roman = tier == 2 ? "II" : "III";
            if (Grimoire.HasEvolution(id, tier))
            {
                UI.Button(rect, $"{roman} owned", spell.Color);
                continue;
            }
            bool can = Grimoire.CanBuyEvolution(id, tier);
            bool featDone = Grimoire.FeatDone(id, tier);
            if (UI.Button(rect, featDone ? $"{roman}  {Grimoire.EvolutionCost(tier)}g" : $"{roman}  locked", can ? GoldColor : dim) && can && Grimoire.BuyEvolution(id, tier))
                Bought();
            if (nextFeat.Length == 0 && !featDone) nextFeat = Grimoire.FeatText(id, tier);
            else if (nextFeat.Length == 0 && tier == 3 && !Grimoire.HasEvolution(id, 2)) nextFeat = "Unlock II first.";
        }
        if (!spell.CanEvolve && owned) nextFeat = "This spell doesn't evolve.";
        UI.Text(new Rect(r.x + 14f * s, by + bh + 8f * s, r.width - 28f * s, 50f * s), nextFeat, 20, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f), false);
    }

    void Bought()
    {
        Sfx.Play(Sound.LevelUp, 0.6f, 0f);
        Haptics.Notification(Haptics.Notify.Success);
    }

    // Art style buttons; picking one rebuilds the world behind the title so it previews right away.
    void DrawStylePicker(Rect r)
    {
        float s = UiScale;
        UI.Text(new Rect(r.x, r.y, r.width, 36f * s), "Art style", 22, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.6f), false);
        var styles = ArtStyle.All;
        float gap = 10f * s, w = (r.width - gap * (styles.Length - 1)) / styles.Length;
        for (int i = 0; i < styles.Length; i++)
        {
            bool on = styles[i] == ArtStyle.Current;
            var accent = on ? new Color(1f, 0.85f, 0.4f) : new Color(0.45f, 0.45f, 0.55f);
            if (UI.Button(new Rect(r.x + i * (w + gap), r.y + 40f * s, w, 70f * s), ArtStyle.Label(styles[i]), accent) && !on)
            {
                Sfx.Play(Sound.Select, 0.8f);
                Haptics.Selection();
                ArtStyle.Current = styles[i];
                BuildWorld();
            }
        }
    }

    void DrawHud()
    {
        if (Player == null || Upgrades == null) return;
        float s = UiScale;
        Rect safe = Screen.safeArea;
        float top = Screen.height - safe.yMax;
        float left = safe.xMin + 20f * s, right = safe.xMax - 20f * s;

        // XP bar across the top
        float frac = Player.Xp / (float)Player.XpToNext;
        UI.Bar(new Rect(safe.xMin, top, safe.width, 16f * s), frac, ArtStyle.XpColor);
        top += 22f * s;

        UI.Text(new Rect(left, top, 300f * s, 44f * s), $"LV {Player.Level}", 34);
        UI.Text(new Rect(left + 130f * s, top + 8f * s, 200f * s, 30f * s), $"{Mathf.FloorToInt(runGold)} gold", 24, TextAnchor.UpperLeft, GoldColor);
        UI.Text(new Rect(safe.center.x - 150f * s, top, 300f * s, 44f * s), FormatTime(Director.Elapsed), 34, TextAnchor.UpperCenter);
        UI.Text(new Rect(safe.center.x - 150f * s, top + 40f * s, 300f * s, 30f * s), $"Zone {zoneIndex + 1}/{zones.Count}  ·  Threat {Director.Threat + 1}  ·  Foes x{Director.HealthScale:0.##}", 20, TextAnchor.UpperCenter, new Color(1f, 0.6f, 0.5f));
        if (State == GameState.Playing && UI.Button(new Rect(right - 120f * s, top, 120f * s, 56f * s), "Menu", new Color(0.6f, 0.6f, 0.7f)))
        {
            BankGold(false);
            Telemetry.EndRun(this, "quit", bankedGold);
            State = GameState.Title;
            Time.timeScale = 1f;
        }
        top += 52f * s;

        // Health bar
        float hpW = 260f * s;
        UI.Bar(new Rect(left, top, hpW, 20f * s), Player.Health / Player.MaxHealth, ArtStyle.HealthColor);
        UI.Text(new Rect(left + hpW + 10f * s, top - 6f * s, 200f * s, 30f * s), Mathf.CeilToInt(Mathf.Max(0f, Player.Health)).ToString(), 22);
        top += 32f * s;

        Upgrades.DrawHud(new Rect(left, top, 400f * s, 200f * s));

        // Boss bar
        var boss = Director.Boss;
        if (boss != null)
        {
            float bw = safe.width * 0.8f, bx = safe.center.x - bw * 0.5f, by = top + 70f * s;
            UI.Text(new Rect(bx, by, bw, 34f * s), boss.Def.Name, 26, TextAnchor.UpperCenter, boss.Def.Accent);
            UI.Bar(new Rect(bx, by + 36f * s, bw, 16f * s), boss.Health / boss.MaxHealth, boss.Def.Accent);
        }

        // Spell icons along the bottom with cooldown sweep
        float d = 92f * s, gap = 12f * s;
        float total = Player.Spells.Count * d + (Player.Spells.Count - 1) * gap;
        float x = safe.center.x - total * 0.5f, y = Screen.height - safe.yMin - d - 30f * s;
        foreach (var spell in Player.Spells)
        {
            var slot = UI.Slot(new Rect(x, y, d, d));
            UI.Box(new Rect(slot.x, slot.y + slot.height * (1f - spell.CooldownProgress), slot.width, slot.height * spell.CooldownProgress), new Color(spell.Color.r, spell.Color.g, spell.Color.b, 0.45f));
            UI.Text(new Rect(x, y + 6f * s, d, 40f * s), Initials(spell.Name), 30, TextAnchor.UpperCenter, spell.Color);
            string tier = spell.CanEvolve ? (spell.Tier == 1 ? "I" : spell.Tier == 2 ? "II" : "III") + " · " : "";
            UI.Text(new Rect(x, y + d - 36f * s, d, 32f * s), $"{tier}Lv{spell.Level}", 20, TextAnchor.UpperCenter);
            x += d + gap;
        }
    }

    void DrawGameOver()
    {
        float s = UiScale;
        UI.Dim(0.6f);
        var col = UI.Column(0.25f, 0.6f);
        float best = PlayerPrefs.GetFloat(BestKey, 0f);
        UI.Text(new Rect(col.x, col.y, col.width, 300f * s),
            $"The wizard has fallen\n\n<size={Mathf.RoundToInt(30 * s)}>Survived {FormatTime(Director.Elapsed)}  ·  Level {Player.Level}\nBest: {FormatTime(best)}\n+{bankedGold} gold  (you have {Grimoire.Gold})</size>",
            48, TextAnchor.UpperCenter);
        if (Time.unscaledTime - gameOverTime < 0.8f) return;  // so a finger still dragging doesn't tap a button
        if (UI.Button(new Rect(col.x, col.y + 330f * s, col.width, 100f * s), "Try again", new Color(0.6f, 0.45f, 1f)))
            StartRun();
        if (UI.Button(new Rect(col.x, col.y + 450f * s, col.width, 100f * s), $"Grimoire  ·  {Grimoire.Gold} gold", GoldColor))
            OpenGrimoire();
        if (UI.Button(new Rect(col.x, col.y + 570f * s, col.width, 100f * s), "Title screen", new Color(0.6f, 0.6f, 0.7f)))
        {
            State = GameState.Title;
            StartCoroutine(Tune.FetchFromMac());
        }
    }

    // Fades to black just before a zone change and back in after it.
    void DrawZoneFade()
    {
        float a = 0f;
        if (shiftAt > 0f) a = 1f - Mathf.Clamp01((shiftAt - Time.time) / FadeTime);
        else if (Time.time < fadeInUntil) a = Mathf.Clamp01((fadeInUntil - Time.time) / FadeTime);
        if (a > 0f) UI.Dim(a);
    }

    void DrawVictory()
    {
        float s = UiScale;
        UI.Dim(0.6f);
        var col = UI.Column(0.25f, 0.6f);
        UI.Text(new Rect(col.x, col.y, col.width, 300f * s),
            $"Victory!\n\n<size={Mathf.RoundToInt(30 * s)}>The wizard survived every zone\n{FormatTime(Director.Elapsed)}  ·  Level {Player.Level}  ·  Wins: {PlayerPrefs.GetInt(WinsKey, 0)}\n+{bankedGold} gold  (you have {Grimoire.Gold})</size>",
            56, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.4f));
        if (Time.unscaledTime - gameOverTime < 0.8f) return;
        if (UI.Button(new Rect(col.x, col.y + 330f * s, col.width, 100f * s), "Play again", new Color(0.6f, 0.45f, 1f)))
            StartRun();
        if (UI.Button(new Rect(col.x, col.y + 450f * s, col.width, 100f * s), $"Grimoire  ·  {Grimoire.Gold} gold", GoldColor))
            OpenGrimoire();
        if (UI.Button(new Rect(col.x, col.y + 570f * s, col.width, 100f * s), "Title screen", new Color(0.6f, 0.6f, 0.7f)))
        {
            State = GameState.Title;
            StartCoroutine(Tune.FetchFromMac());
        }
    }

    void DrawToasts()
    {
        toasts.RemoveAll(t => Time.unscaledTime > t.until);
        float s = UiScale;
        var col = UI.Column(0.3f, 0.2f);
        for (int i = 0; i < toasts.Count; i++)
        {
            float a = Mathf.Clamp01(toasts[i].until - Time.unscaledTime);
            UI.Text(new Rect(col.x, col.y + i * 48f * s, col.width, 48f * s), toasts[i].text, 34, TextAnchor.UpperCenter, new Color(1f, 0.9f, 0.5f, a));
        }
    }

    static string Initials(string name)
    {
        var parts = name.Split(' ');
        return parts.Length > 1 ? $"{parts[0][0]}{parts[1][0]}" : name.Substring(0, 2);
    }

    static string FormatTime(float t) => $"{(int)t / 60}:{(int)t % 60:00}";
}
