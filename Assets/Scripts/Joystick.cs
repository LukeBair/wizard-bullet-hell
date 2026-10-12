using UnityEngine;

// Floating joystick: touch anywhere, drag to steer, lift to stop. WASD / arrow keys also work in the editor.
public class Joystick : MonoBehaviour
{
    public static Vector2 Direction { get; private set; }

    const float RadiusPx = 90f;   // at 720p reference width

    bool active;
    Vector2 origin, current;

    public static bool Blocked;   // set while a menu is open so taps don't also steer

    void Update()
    {
        float radius = RadiusPx * GameManager.UiScale;
        Vector2 keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        bool down, held;
        Vector2 screen;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            screen = t.position;
            down = t.phase == TouchPhase.Began;
            held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
        }
        else
        {
            screen = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
        }

        if (Blocked) { active = false; held = false; }
        if (down && !Blocked) { active = true; origin = screen; }
        if (!held) active = false;
        current = screen;

        if (GameManager.Autoplay) { Direction = AutopilotDirection(); return; }

        if (active)
        {
            Vector2 d = current - origin;
            if (d.magnitude > radius) origin = current - d.normalized * radius;  // drag the base along
            d = current - origin;
            Direction = d.magnitude < radius * 0.15f ? Vector2.zero : Vector2.ClampMagnitude(d / radius, 1f);
        }
        else
        {
            Direction = Vector2.ClampMagnitude(keys, 1f);
        }
    }

    // Flee nearby enemies and bullets, drift toward gems and the arena centre.
    static Vector2 AutopilotDirection()
    {
        var gm = GameManager.Instance;
        if (gm.Player == null) return Vector2.zero;
        Vector2 p = gm.Player.Position, v = Vector2.zero;
        foreach (var e in Enemy.All)
        {
            Vector2 d = p - e.Position;
            float r = d.magnitude;
            if (r < 5f + e.Radius) v += d / (r * r + 0.01f) * (e.IsBoss ? 6f : 1f);
        }
        foreach (var b in Projectile.All)
        {
            if (b.Team != Team.Enemy) continue;
            Vector2 d = p - (Vector2)b.transform.position;
            float r = d.magnitude;
            if (r < 2.5f) v += d / (r * r + 0.01f) * 0.6f;
        }
        XpGem nearest = null; float best = 64f;
        foreach (var g in XpGem.All)
        {
            float sq = ((Vector2)g.transform.position - p).sqrMagnitude;
            if (sq < best) { best = sq; nearest = g; }
        }
        if (nearest != null) v += ((Vector2)nearest.transform.position - p).normalized * 0.25f;
        v += -p * 0.01f;
        return Vector2.ClampMagnitude(v * 3f, 1f);
    }

    void OnGUI()
    {
        if (!active || GameManager.Instance.State != GameState.Playing) return;
        float radius = RadiusPx * GameManager.UiScale;
        var tex = SpriteFactory.Circle.texture;
        Vector2 o = new Vector2(origin.x, Screen.height - origin.y);
        Vector2 k = o + new Vector2(current.x - origin.x, -(current.y - origin.y));
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(new Rect(o.x - radius, o.y - radius, radius * 2f, radius * 2f), tex);
        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        float kr = radius * 0.45f;
        GUI.DrawTexture(new Rect(k.x - kr, k.y - kr, kr * 2f, kr * 2f), tex);
        GUI.color = Color.white;
    }
}
