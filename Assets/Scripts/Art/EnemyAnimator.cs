using UnityEngine;

// Loops an enemy's walk sheet and mirrors it to face the way it's drifting. Sheets with eight facings
// (walk_s, walk_se, ... from the 3D pipeline) pick the facing nearest the drift direction instead of
// mirroring, and can also carry cast_<facing> (played once by PlayCast) and death_<facing> (played by
// PlayDeath on a corpse left behind). Each enemy starts at a random point in the loop so a crowd doesn't
// step in lockstep.
public class EnemyAnimator : MonoBehaviour
{
    // Counter-clockwise from east, matching Atan2 sectors of 45 degrees.
    static readonly string[] Facings = { "e", "ne", "n", "nw", "w", "sw", "s", "se" };

    SpriteSheet sheet;
    SpriteSheet.Clip clip;
    SpriteSheet.Clip[] walk, cast;   // per facing; null for one-facing sheets
    SpriteSheet.Clip oneShot;        // playing once (cast, death), then back to the loop
    bool flip = true, dying;
    int facingIndex = 6;
    SpriteRenderer sr;
    Transform root;
    Vector3 lastPos;
    float t, fade;

    public static EnemyAnimator Attach(SpriteRenderer sr, SpriteSheet sheet, string clipName = "walk", bool flip = true)
    {
        var clip = sheet?[clipName];
        if (clip == null) return null;
        var a = sr.gameObject.AddComponent<EnemyAnimator>();
        a.sheet = sheet;
        a.sr = sr;
        a.clip = clip;
        a.flip = flip;
        a.root = sr.transform.parent;
        a.lastPos = a.root.position;
        a.walk = Facing(sheet, clipName);
        a.cast = Facing(sheet, "cast");
        if (a.walk != null) a.clip = a.walk[a.facingIndex];
        a.t = Random.value * a.clip.Length;
        sr.sprite = a.clip.Frames[0];
        return a;
    }

    static SpriteSheet.Clip[] Facing(SpriteSheet sheet, string name)
    {
        if (sheet[name + "_s"] == null) return null;
        var c = new SpriteSheet.Clip[Facings.Length];
        for (int i = 0; i < Facings.Length; i++) c[i] = sheet[name + "_" + Facings[i]] ?? sheet[name + "_s"];
        return c;
    }

    // The cast, once, in the current facing (ignored while one is already playing).
    public void PlayCast()
    {
        if (cast == null || dying || oneShot != null) return;
        oneShot = cast[facingIndex];
        t = 0f;
    }

    // If the sheet has a death clip: unhook the body from the dying enemy so it plays the death in its
    // current facing where it fell, fades out and cleans itself up. Returns false if there's no clip.
    public bool PlayDeath()
    {
        var death = Facing(sheet, "death");
        if (death == null) return false;
        dying = true;
        oneShot = death[facingIndex];
        t = 0f;
        fade = 0.6f;
        transform.SetParent(null, true);
        root = null;
        return true;
    }

    void Update()
    {
        if (root != null)
        {
            float dx = root.position.x - lastPos.x, dy = root.position.y - lastPos.y;
            lastPos = root.position;
            if (walk != null)
            {
                if (dx * dx + dy * dy > 1e-6f)
                {
                    float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    int i = ((Mathf.RoundToInt(deg / 45f) % 8) + 8) % 8;
                    // a little hysteresis so a wobbling heading doesn't flicker between two facings
                    float off = Mathf.Abs(Mathf.DeltaAngle(deg, facingIndex * 45f));
                    if (i != facingIndex && off > 30f) { facingIndex = i; clip = walk[i]; }
                }
            }
            else if (flip && Mathf.Abs(dx) > 0.0005f) sr.flipX = dx < 0f;
        }

        if (oneShot != null)
        {
            t += Time.deltaTime;
            if (t < oneShot.Length) { sr.sprite = Frame(oneShot, t); return; }
            if (!dying) { oneShot = null; t = 0f; }
            else
            {
                // hold the last frame and fade
                sr.sprite = oneShot.Frames[oneShot.Frames.Length - 1];
                fade -= Time.deltaTime;
                var c = sr.color; c.a = Mathf.Clamp01(fade / 0.6f); sr.color = c;
                if (fade <= 0f) Destroy(gameObject);
                return;
            }
        }

        t = (t + Time.deltaTime) % clip.Length;
        sr.sprite = Frame(clip, t);
    }

    static Sprite Frame(SpriteSheet.Clip c, float time)
    {
        float acc = 0f;
        for (int i = 0; i < c.Frames.Length; i++)
        {
            acc += c.Durations[i];
            if (time < acc) return c.Frames[i];
        }
        return c.Frames[c.Frames.Length - 1];
    }
}
