using UnityEngine;

// Loops an enemy's walk sheet and mirrors it to face the way it's drifting. Each enemy starts at a
// random point in the loop so a crowd doesn't step in lockstep.
public class EnemyAnimator : MonoBehaviour
{
    SpriteSheet.Clip clip;
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
        a.t = Random.value * clip.Length;
        sr.sprite = clip.Frames[0];
    }

    void Update()
    {
        float dx = root.position.x - lastPos.x;
        lastPos = root.position;
        if (Mathf.Abs(dx) > 0.0005f) sr.flipX = dx < 0f;

        t = (t + Time.deltaTime) % clip.Length;
        float acc = 0f;
        for (int i = 0; i < clip.Frames.Length; i++)
        {
            acc += clip.Durations[i];
            if (t < acc) { sr.sprite = clip.Frames[i]; return; }
        }
    }
}
