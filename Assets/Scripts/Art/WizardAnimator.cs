using UnityEngine;

// Plays the player's sprite sheet: idle or walk on loop, with cast and hit as short one-shots
// over them, and death held on its last frame. Runs on its own so death keeps playing after
// the Player stops updating.
public class WizardAnimator : MonoBehaviour
{
    const float CastGap = 0.5f;   // casts come constantly; don't restart the swing more often than this

    SpriteSheet sheet;
    SpriteRenderer sr;
    SpriteSheet.Clip clip;
    string clipName;
    float t;
    bool oneShot;
    float lastCast = -10f;

    public bool Moving { get; set; }

    // Which way the sheet's clips face: "" (front), "_side" (right; the Player mirrors it for left),
    // "_up" (back), and on the 3D sheets "_diag" (front three-quarter) and "_updiag" (back
    // three-quarter), both facing right. Clips without that variant fall back to the closest one.
    public string Facing { get; set; } = "";

    // True when the sheet has the diagonal clips (the 3D-rendered sheets).
    public bool HasDiagonals => sheet != null && sheet["walk_diag"] != null;

    // The facing for a move direction: three-way (front, side, back) on sheets without diagonals,
    // five-way with them. `current` is kept a few degrees past each boundary so a wobbling stick
    // doesn't restart the walk cycle.
    public static string FacingFor(Vector2 dir, bool diagonals, string current)
    {
        float ax = Mathf.Abs(dir.x), ay = Mathf.Abs(dir.y);
        if (!diagonals) return ax > ay ? "_side" : dir.y > 0f ? "_up" : "";
        float angle = Mathf.Atan2(ay, ax) * Mathf.Rad2Deg;   // 0 = sideways, 90 = straight up or down
        const float Stick = 7f;
        bool vertical = dir.y > 0f;
        string Pick(float a) => a < 22.5f ? "_side" : a < 67.5f ? (vertical ? "_updiag" : "_diag") : (vertical ? "_up" : "");
        float lo, hi;
        switch (current)
        {
            case "_side": lo = 0f; hi = 22.5f; break;
            case "_diag": case "_updiag": lo = 22.5f; hi = 67.5f; break;
            default: lo = 67.5f; hi = 90f; break;
        }
        bool sameSide = current == "_side" || ((current == "_up" || current == "_updiag") == vertical);
        if (sameSide && angle > lo - Stick && angle < hi + Stick) return current;
        return Pick(angle);
    }

    public static WizardAnimator Attach(SpriteRenderer sr, SpriteSheet sheet)
    {
        var a = sr.gameObject.AddComponent<WizardAnimator>();
        a.sr = sr;
        a.sheet = sheet;
        a.Play("idle");
        return a;
    }

    // Swap to another sheet mid-run (the Wizard art setting) and restart the current loop on it.
    public void SetSheet(SpriteSheet newSheet)
    {
        if (newSheet == null || newSheet == sheet || clipName == "death") return;
        sheet = newSheet;
        Play(Moving ? "walk" : "idle");
    }

    // The clip name for this facing, falling back to the nearest facing the sheet has: a diagonal the
    // sheet lacks becomes side or back, and anything missing becomes the front clip.
    string Faced(string name)
    {
        if (sheet[name + Facing] != null) return name + Facing;
        if (Facing == "_diag" && sheet[name + "_side"] != null) return name + "_side";
        if (Facing == "_updiag" && sheet[name + "_up"] != null) return name + "_up";
        return name;
    }

    void Play(string name, bool once = false)
    {
        name = Faced(name);
        var c = sheet[name];
        if (c == null) return;
        clip = c;
        clipName = name;
        t = 0f;
        oneShot = once;
        sr.sprite = c.Frames[0];
    }

    public void Cast()
    {
        if (clipName == "hit" || clipName == "death" || Time.time - lastCast < CastGap) return;
        lastCast = Time.time;
        Play("cast", true);
    }

    public void Hit()
    {
        if (clipName != "death") Play("hit", true);
    }

    public void Die() => Play("death", true);

    void Update()
    {
        if (clip == null) return;
        t += Time.deltaTime;
        if (t >= clip.Length)
        {
            if (clipName == "death") t = clip.Length - 0.0001f;   // hold the last frame
            else if (oneShot) { Play(Moving ? "walk" : "idle"); return; }
            else t %= clip.Length;
        }
        else if (!oneShot && clipName != Faced(Moving ? "walk" : "idle")) Play(Moving ? "walk" : "idle");

        float acc = 0f;
        for (int i = 0; i < clip.Frames.Length; i++)
        {
            acc += clip.Durations[i];
            if (t < acc) { sr.sprite = clip.Frames[i]; return; }
        }
        sr.sprite = clip.Frames[clip.Frames.Length - 1];
    }
}
