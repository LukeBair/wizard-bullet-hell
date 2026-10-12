using System.Collections.Generic;
using UnityEngine;

public enum GameState { Title, Playing, LevelUp, GameOver }

// Owns the run: title screen (pick an upgrade system), camera, level-up flow, game over and the HUD.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static float UiScale => Mathf.Min(Screen.width / 720f, Screen.height / 1280f);

    const string ModeKey = "upgrade_mode";
    const float CameraSize = 7.5f;

    public GameState State { get; private set; }
    public Player Player { get; private set; }
    public Arena Arena { get; private set; }
    public Director Director { get; private set; }
    public UpgradeSystem Upgrades { get; private set; }
    public int Score { get; private set; }
    public static bool Autoplay;
    public float ViewRadius => cam.orthographicSize * Mathf.Sqrt(1f + cam.aspect * cam.aspect);

    Camera cam;
    Vector2 camPos;
    float shake;
    int pendingLevelUps;
    float gameOverTime;
    UpgradeMode mode;
    readonly List<(string text, float until)> toasts = new List<(string, float)>();

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        mode = (UpgradeMode)PlayerPrefs.GetInt(ModeKey, 0);

        SetupCamera();
        Director = gameObject.AddComponent<Director>();
        gameObject.AddComponent<Joystick>();
        gameObject.AddComponent<Sfx>();
        if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        BuildWorld();
        State = GameState.Title;
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

    void BuildWorld()
    {
        Clear<Projectile>();
        Clear<Enemy>();
        Clear<XpGem>();
        Clear<Fx>();
        Clear<Hazard>();
        Clear<DamageText>();
        if (Player != null) Destroy(Player.gameObject);
        if (Arena != null) Destroy(Arena.gameObject);

        Arena = Arena.Create(Biome.Forest);
        Player = Player.Create(Vector2.zero);
        camPos = Vector2.zero;
    }

    static void Clear<T>() where T : MonoBehaviour
    {
        foreach (var o in FindObjectsByType<T>()) Destroy(o.gameObject);
    }

    public void StartRun(UpgradeMode m)
    {
        mode = m;
        PlayerPrefs.SetInt(ModeKey, (int)m);
        BuildWorld();
        Score = 0;
        pendingLevelUps = 0;
        toasts.Clear();
        Director.ResetRun(Arena.Biome.Roster);
        Upgrades = UpgradeSystem.Create(m);
        Upgrades.Begin(Player);
        State = GameState.Playing;
        Time.timeScale = 1f;
        Joystick.Blocked = true;   // released once the starting tap lifts
        Toast(Arena.Biome.Name);
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
        string key = $"best_time_{mode}";
        if (Director.Elapsed > PlayerPrefs.GetFloat(key, 0f))
        {
            PlayerPrefs.SetFloat(key, Director.Elapsed);
            PlayerPrefs.Save();
        }
    }

    void Update()
    {
        if (State == GameState.Playing && pendingLevelUps > 0)
        {
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            Joystick.Blocked = true;
            Upgrades.Open();
            Sfx.Play(Sound.LevelUp, 0.8f, 0f);
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
            case GameState.Playing: DrawHud(); break;
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
        }
        DrawToasts();
    }

    void DrawTitle()
    {
        float s = UiScale;
        UI.Dim(0.55f);
        var col = UI.Column(0.08f, 0.9f);
        UI.Text(new Rect(col.x, col.y, col.width, 90f * s), "Wizard Bullet Hell", 60, TextAnchor.UpperCenter, new Color(0.8f, 0.65f, 1f));
        UI.Text(new Rect(col.x, col.y + 90f * s, col.width, 50f * s), "Choose an upgrade system to test", 28, TextAnchor.UpperCenter, new Color(0.85f, 0.85f, 0.95f), false);

        float y = col.y + 170f * s, h = 200f * s;
        foreach (UpgradeMode m in System.Enum.GetValues(typeof(UpgradeMode)))
        {
            var sys = UpgradeSystem.Create(m);
            float best = PlayerPrefs.GetFloat($"best_time_{m}", 0f);
            string tag = best > 0f ? $"BEST {FormatTime(best)}" : (m == mode ? "LAST PLAYED" : null);
            var accent = m == UpgradeMode.Draft ? new Color(0.5f, 0.85f, 1f) : m == UpgradeMode.Runes ? new Color(1f, 0.75f, 0.35f) : new Color(0.6f, 1f, 0.5f);
            if (UI.Card(new Rect(col.x, y, col.width, h), sys.Name, sys.Pitch, accent, tag))
            {
                Sfx.Play(Sound.Select, 0.8f);
                Haptics.Selection();
                StartRun(m);
            }
            y += h + 24f * s;
        }
        UI.Text(new Rect(col.x, y + 10f * s, col.width, 80f * s), "Drag anywhere to move. Spells cast on their own.", 24, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.7f), false);
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
        UI.Box(new Rect(safe.xMin, top, safe.width, 16f * s), new Color(0f, 0f, 0f, 0.6f));
        UI.Box(new Rect(safe.xMin, top, safe.width * frac, 16f * s), new Color(0.45f, 0.75f, 1f));
        top += 22f * s;

        UI.Text(new Rect(left, top, 300f * s, 44f * s), $"LV {Player.Level}", 34);
        UI.Text(new Rect(safe.center.x - 150f * s, top, 300f * s, 44f * s), FormatTime(Director.Elapsed), 34, TextAnchor.UpperCenter);
        UI.Text(new Rect(safe.center.x - 150f * s, top + 40f * s, 300f * s, 30f * s), $"Threat {Director.Threat + 1}  ·  Foes x{Director.HealthScale:0.##}", 20, TextAnchor.UpperCenter, new Color(1f, 0.6f, 0.5f));
        if (State == GameState.Playing && UI.Button(new Rect(right - 120f * s, top, 120f * s, 56f * s), "Menu", new Color(0.6f, 0.6f, 0.7f)))
        {
            State = GameState.Title;
            Time.timeScale = 1f;
        }
        top += 52f * s;

        // Health bar
        float hpW = 260f * s;
        UI.Box(new Rect(left, top, hpW, 20f * s), new Color(0f, 0f, 0f, 0.6f));
        UI.Box(new Rect(left, top, hpW * Mathf.Clamp01(Player.Health / Player.MaxHealth), 20f * s), new Color(1f, 0.35f, 0.45f));
        UI.Text(new Rect(left + hpW + 10f * s, top - 6f * s, 200f * s, 30f * s), Mathf.CeilToInt(Mathf.Max(0f, Player.Health)).ToString(), 22);
        top += 32f * s;

        Upgrades.DrawHud(new Rect(left, top, 400f * s, 200f * s));

        // Boss bar
        var boss = Director.Boss;
        if (boss != null)
        {
            float bw = safe.width * 0.8f, bx = safe.center.x - bw * 0.5f, by = top + 70f * s;
            UI.Text(new Rect(bx, by, bw, 34f * s), boss.Def.Name, 26, TextAnchor.UpperCenter, boss.Def.Accent);
            UI.Box(new Rect(bx, by + 36f * s, bw, 16f * s), new Color(0f, 0f, 0f, 0.6f));
            UI.Box(new Rect(bx, by + 36f * s, bw * Mathf.Clamp01(boss.Health / boss.MaxHealth), 16f * s), boss.Def.Accent);
        }

        // Spell icons along the bottom with cooldown sweep
        float d = 92f * s, gap = 16f * s;
        float total = Player.Spells.Count * d + (Player.Spells.Count - 1) * gap;
        float x = safe.center.x - total * 0.5f, y = Screen.height - safe.yMin - d - 30f * s;
        foreach (var spell in Player.Spells)
        {
            UI.Box(new Rect(x, y, d, d), new Color(0f, 0f, 0f, 0.55f));
            UI.Box(new Rect(x, y + d * (1f - spell.CooldownProgress), d, d * spell.CooldownProgress), new Color(spell.Color.r, spell.Color.g, spell.Color.b, 0.45f));
            UI.Text(new Rect(x, y + 6f * s, d, 40f * s), Initials(spell.Name), 30, TextAnchor.UpperCenter, spell.Color);
            UI.Text(new Rect(x, y + d - 36f * s, d, 32f * s), $"Lv{spell.Level}", 20, TextAnchor.UpperCenter);
            x += d + gap;
        }
    }

    void DrawGameOver()
    {
        float s = UiScale;
        UI.Dim(0.6f);
        var col = UI.Column(0.25f, 0.6f);
        float best = PlayerPrefs.GetFloat($"best_time_{mode}", 0f);
        UI.Text(new Rect(col.x, col.y, col.width, 300f * s),
            $"The wizard has fallen\n\n<size={Mathf.RoundToInt(30 * s)}>Survived {FormatTime(Director.Elapsed)}  ·  Level {Player.Level}\nBest with {Upgrades.Name}: {FormatTime(best)}</size>",
            48, TextAnchor.UpperCenter);
        if (Time.unscaledTime - gameOverTime < 0.8f) return;  // so a finger still dragging doesn't tap a button
        if (UI.Button(new Rect(col.x, col.y + 330f * s, col.width, 100f * s), $"Try again ({Upgrades.Name})", new Color(0.6f, 0.45f, 1f)))
            StartRun(mode);
        if (UI.Button(new Rect(col.x, col.y + 450f * s, col.width, 100f * s), "Change upgrade system", new Color(0.6f, 0.6f, 0.7f)))
            State = GameState.Title;
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
