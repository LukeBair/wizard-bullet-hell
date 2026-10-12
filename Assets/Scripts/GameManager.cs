using System.Collections.Generic;
using UnityEngine;

// Owns the camera, the run (score, game over, restart) and the HUD.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const string BestScoreKey = "best_score";
    static readonly Color Background = new Color(0.06f, 0.04f, 0.12f);

    public Player Player { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public bool IsGameOver { get; private set; }
    public int Wave => spawner != null ? spawner.Wave : 0;
    public Rect Bounds { get; private set; }  // visible world area

    Camera cam;
    WaveSpawner spawner;
    float gameOverTime;
    float shake;
    GUIStyle style;

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        SetupCamera();
        UpdateBounds();
        CreateStarfield();
        spawner = gameObject.AddComponent<WaveSpawner>();
        StartRun();
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
        cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
    }

    void UpdateBounds()
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Bounds = new Rect(-w, -h, w * 2f, h * 2f);
    }

    void CreateStarfield()
    {
        var root = new GameObject("Stars").transform;
        for (int i = 0; i < 60; i++)
        {
            var pos = new Vector2(Random.Range(-6f, 6f), Random.Range(-9f, 9f));
            float a = Random.Range(0.15f, 0.6f);
            SpriteFactory.Child(root, "Star", SpriteFactory.Circle, new Color(0.8f, 0.8f, 1f, a), pos, Random.Range(0.04f, 0.1f), -10);
        }
    }

    public void StartRun()
    {
        foreach (var p in new List<Projectile>(Projectile.All)) Destroy(p.gameObject);
        foreach (var e in new List<Enemy>(Enemy.All)) Destroy(e.gameObject);
        if (Player != null) Destroy(Player.gameObject);

        Score = 0;
        IsGameOver = false;
        Player = Player.Create(new Vector2(0f, Bounds.yMin + 2.5f));
        spawner.ResetWaves();
    }

    public void AddScore(int points)
    {
        if (!IsGameOver) Score += points;
    }

    public void GameOver()
    {
        IsGameOver = true;
        gameOverTime = Time.time;
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.Save();
        }
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    void Update()
    {
        UpdateBounds();

        if (shake > 0f)
        {
            shake = Mathf.Max(0f, shake - Time.deltaTime);
            Vector2 offset = Random.insideUnitCircle * shake * 0.6f;
            cam.transform.position = new Vector3(offset.x, offset.y, -10f);
        }
        else
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        // Short delay so a finger still dragging when you die doesn't restart instantly.
        bool tapped = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (IsGameOver && tapped && Time.time - gameOverTime > 0.8f)
            StartRun();
    }

    void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 720f, Screen.height / 1280f);
        if (style == null)
            style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        style.fontSize = Mathf.RoundToInt(40 * scale);
        style.normal.textColor = Color.white;

        Rect safe = Screen.safeArea;
        float top = Screen.height - safe.yMax + 16f * scale;  // GUI y runs top-down, safeArea bottom-up
        float left = safe.xMin + 20f * scale;
        float right = safe.xMax - 20f * scale;
        float lineH = 52f * scale;

        style.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(left, top, 500f * scale, lineH), $"Score {Score}", style);
        style.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(right - 500f * scale, top, 500f * scale, lineH), $"Wave {Wave}", style);

        // Health pips
        if (Player != null)
        {
            float pip = 26f * scale, gap = 10f * scale;
            for (int i = 0; i < Player.MaxHealth; i++)
            {
                GUI.color = i < Player.Health ? new Color(1f, 0.35f, 0.5f) : new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(new Rect(left + i * (pip + gap), top + lineH + 6f * scale, pip, pip), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        if (IsGameOver)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, Screen.height * 0.4f),
                $"The wizard has fallen\n\nScore {Score}\nBest {BestScore}\nWave {Wave}\n\nTap to try again", style);
        }
    }
}
