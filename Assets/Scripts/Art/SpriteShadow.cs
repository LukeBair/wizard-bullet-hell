using UnityEngine;

// A cast shadow for a sprite: its own silhouette in translucent black, laid on the ground from the
// sprite's feet and stretched away from the sun (Lighting.ShadowDirection). Props get one baked when
// the arena is built; characters keep theirs live so it follows their animation frames.
public class SpriteShadow : MonoBehaviour
{
    SpriteRenderer src, sr;
    bool live;

    public static SpriteShadow Add(SpriteRenderer source, bool live)
    {
        if (source == null || source.sprite == null || !Lighting.Active || !Tune.B("lighting.shadows", true)) return null;
        var go = new GameObject("CastShadow");
        go.transform.SetParent(source.transform.parent, true);
        var s = go.AddComponent<SpriteShadow>();
        s.src = source;
        s.live = live;
        s.sr = SpriteFactory.Add(go, source.sprite, new Color(0f, 0f, 0f, Tune.F("lighting.shadowAlpha", 0.32f)), -44);
        s.Apply();
        if (!live) s.enabled = false;   // baked: placed once
        return s;
    }

    void LateUpdate()
    {
        if (src == null) { Destroy(gameObject); return; }
        if (live) Apply();
    }

    void Apply()
    {
        var sprite = src.sprite;
        if (sprite == null) return;
        sr.sprite = sprite;
        sr.flipX = src.flipX;
        sr.enabled = src.enabled && src.gameObject.activeInHierarchy;

        Vector2 u = Lighting.ShadowDirection;
        float len = Tune.F("lighting.shadowLength", 0.55f);
        var b = sprite.bounds;                                    // sprite space, relative to the pivot
        Vector2 feet = new Vector2(b.center.x, b.min.y);
        Vector3 scale = src.transform.lossyScale;
        float angle = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg - 90f;   // the sprite's up axis points along u
        var rot = Quaternion.Euler(0f, 0f, angle);
        var shadowScale = new Vector3(scale.x, scale.y * len, 1f);

        Vector3 worldFeet = src.transform.TransformPoint(feet);
        Vector3 offset = rot * Vector3.Scale(shadowScale, feet);
        transform.SetPositionAndRotation(worldFeet - offset, rot);
        var parent = transform.parent;
        Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(shadowScale.x / ps.x, shadowScale.y / ps.y, 1f);
    }
}
