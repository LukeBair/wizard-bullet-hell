using UnityEngine;

// The wizard. Drag anywhere on screen to move (relative drag, so your finger never covers him). Fires automatically.
public class Player : MonoBehaviour
{
    public const float HitRadius = 0.15f;   // tiny hitbox, shown as the white dot
    public const int MaxHealth = 5;

    const float FireInterval = 0.11f;
    const float SpellSpeed = 18f;
    const float InvulnTime = 1.5f;
    const float EdgeMargin = 0.4f;

    static readonly Color SpellColor = new Color(0.45f, 0.9f, 1f);

    public int Health { get; private set; }
    public bool IsAlive => Health > 0;

    float fireTimer;
    float invulnTimer;
    bool dragging;
    Vector2 dragOffset;
    SpriteRenderer[] renderers;

    public static Player Create(Vector2 position)
    {
        var go = new GameObject("Wizard");
        go.transform.position = position;
        var p = go.AddComponent<Player>();
        p.Health = MaxHealth;
        p.BuildVisual();
        return p;
    }

    void BuildVisual()
    {
        var robe = new Color(0.45f, 0.25f, 0.85f);
        SpriteFactory.Child(transform, "Aura", SpriteFactory.Glow, new Color(0.6f, 0.4f, 1f, 0.35f), Vector2.zero, 1.8f, 9);
        SpriteFactory.Child(transform, "Robe", SpriteFactory.Triangle, robe, new Vector2(0f, -0.1f), 0.8f, 10);
        SpriteFactory.Child(transform, "Head", SpriteFactory.Circle, new Color(1f, 0.85f, 0.7f), new Vector2(0f, 0.3f), 0.3f, 11);
        SpriteFactory.Child(transform, "Hat", SpriteFactory.Triangle, robe * 0.7f + new Color(0, 0, 0, 0.3f), new Vector2(0f, 0.6f), 0.5f, 12);
        SpriteFactory.Child(transform, "Hitbox", SpriteFactory.Circle, Color.white, Vector2.zero, HitRadius * 2f, 13);
        renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (!IsAlive || GameManager.Instance.IsGameOver) return;

        HandleInput();

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            fireTimer = FireInterval;
            Fire();
        }

        if (invulnTimer > 0f)
        {
            invulnTimer -= Time.deltaTime;
            bool visible = invulnTimer <= 0f || Mathf.FloorToInt(invulnTimer * 12f) % 2 == 0;
            foreach (var r in renderers) r.enabled = visible;
        }
    }

    void HandleInput()
    {
        Vector2 screenPos;
        bool began;

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { dragging = false; return; }
            screenPos = t.position;
            began = t.phase == TouchPhase.Began;
        }
        else if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            began = Input.GetMouseButtonDown(0);
        }
        else
        {
            dragging = false;
            return;
        }

        Vector2 world = Camera.main.ScreenToWorldPoint(screenPos);
        if (began || !dragging)
        {
            dragging = true;
            dragOffset = (Vector2)transform.position - world;
        }

        var b = GameManager.Instance.Bounds;
        Vector2 target = world + dragOffset;
        target.x = Mathf.Clamp(target.x, b.xMin + EdgeMargin, b.xMax - EdgeMargin);
        target.y = Mathf.Clamp(target.y, b.yMin + EdgeMargin, b.yMax - EdgeMargin);
        transform.position = target;
    }

    void Fire()
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.6f);
        Projectile.Spawn(origin + new Vector2(-0.15f, 0f), new Vector2(-1f, SpellSpeed), Team.Player, SpellColor, 0.12f);
        Projectile.Spawn(origin + new Vector2(0.15f, 0f), new Vector2(1f, SpellSpeed), Team.Player, SpellColor, 0.12f);
    }

    // Returns true if the hit landed (false while invulnerable).
    public bool TakeHit()
    {
        if (!IsAlive || invulnTimer > 0f) return false;

        Health--;
        invulnTimer = InvulnTime;
        GameManager.Instance.Shake(0.25f);

        if (!IsAlive)
        {
            foreach (var r in renderers) r.enabled = false;
            GameManager.Instance.GameOver();
        }
        return true;
    }
}
