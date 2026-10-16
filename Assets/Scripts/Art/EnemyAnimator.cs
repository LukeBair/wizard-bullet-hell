using UnityEngine;

// Loops an enemy's walk sheet and mirrors it to face the way it's drifting. Sheets with eight facings
// (walk_s, walk_se, ... from the 3D pipeline) pick the facing nearest the drift direction instead of
// mirroring. Each enemy starts at a random point in the loop so a crowd doesn't step in lockstep.
public class EnemyAnimator : MonoBehaviour
{
    // Counter-clockwise from east, matching Atan2 sectors of 45 degrees.
    static readonly string[] Facings = { "e", "ne", "n", "nw", "w", "sw", "s", "se" };

    SpriteSheet.Clip clip;
    SpriteSheet.Clip[] facing;   // null for one-facing sheets
    int facingIndex = 6;
    SpriteRenderer sr;
    Transform root;
    Vector3 lastPos;
    float t;

    public static void Attach(SpriteRenderer sr, SpriteSheet sheet)
    {
        var clip = sheet?["walk"];
        if (clip == null) return;
        var a = sr.gameObject.AddComponent<EnemyAnimator>();
        a.sr = sr;
        a.clip = clip;
        a.root = sr.transform.parent;
        a.lastPos = a.root.position;
        if (sheet["walk_s"] != null)
        {
            a.facing = new SpriteSheet.Clip[Facings.Length];
            for (int i = 0; i < Facings.Length; i++) a.facing[i] = sheet["walk_" + Facings[i]] ?? clip;
            a.clip = a.facing[a.facingIndex];
        }
        a.t = Random.value * a.clip.Length;
        sr.sprite = a.clip.Frames[0];
    }

    void Update()
    {
        float dx = root.position.x - lastPos.x, dy = root.position.y - lastPos.y;
        lastPos = root.position;
        if (facing != null)
        {
            if (dx * dx + dy * dy > 1e-6f)
            {
                float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                int i = ((Mathf.RoundToInt(deg / 45f) % 8) + 8) % 8;
                // a little hysteresis so a wobbling heading doesn't flicker between two facings
                float off = Mathf.Abs(Mathf.DeltaAngle(deg, facingIndex * 45f));
                if (i != facingIndex && off > 30f) { facingIndex = i; clip = facing[i]; }
            }
        }
        else if (Mathf.Abs(dx) > 0.0005f) sr.flipX = dx < 0f;

        t = (t + Time.deltaTime) % clip.Length;
        float acc = 0f;
        for (int i = 0; i < clip.Frames.Length; i++)
        {
            acc += clip.Durations[i];
            if (t < acc) { sr.sprite = clip.Frames[i]; return; }
        }
    }
}
