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

    // Which way the sheet's clips face: "" (front), "_side" (right; the Player mirrors it for left)
    // or "_up" (back). Clips without that variant fall back to the front one.
    public string Facing { get; set; } = "";

    public static WizardAnimator Attach(SpriteRenderer sr, SpriteSheet sheet)
    {
        var a = sr.gameObject.AddComponent<WizardAnimator>();
        a.sr = sr;
        a.sheet = sheet;
        a.Play("idle");
        return a;
    }

    string Faced(string name) => sheet[name + Facing] != null ? name + Facing : name;

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
