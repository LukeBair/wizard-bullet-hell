using UnityEngine;

// A cast shadow for a sprite: its own silhouette in translucent black, sheared along the ground away
// from the sun (Lighting.ShadowDirection). The row at the sprite's feet stays exactly where it is and
// everything above it leans over by its height, the way a real shadow falls, so both feet, every
// root of a tree and the base of a rock stay touching their shadow. Drawn as a 4-vertex mesh because
// a Transform can rotate and scale but not shear. Props get one baked when the arena is built;
// characters keep theirs live so it follows their animation frames (rebuilt only when the frame,
// flip or sprite transform changes).
public class SpriteShadow : MonoBehaviour
{
    SpriteRenderer src;
    MeshRenderer mr;
    Mesh mesh;
    MaterialPropertyBlock block;
    bool live;
    readonly Vector3[] verts = new Vector3[4];
    readonly Vector2[] uvs = new Vector2[4];
    static readonly int[] Tris = { 0, 2, 1, 2, 3, 1 };
    static readonly int MainTex = Shader.PropertyToID("_MainTex");

    public static bool On => Lighting.Active && Tune.B("lighting.shadows", true);

    public static SpriteShadow Add(SpriteRenderer source, bool live)
    {
        if (source == null || source.sprite == null || !On || Lighting.Unlit == null) return null;
        var go = new GameObject("CastShadow");
        go.transform.SetParent(source.transform.parent, false);
        var s = go.AddComponent<SpriteShadow>();
        s.src = source;
        s.live = live;
        s.mesh = new Mesh { name = "shadow" };
        s.mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = s.mesh;
        s.mr = go.AddComponent<MeshRenderer>();
        s.mr.sharedMaterial = Lighting.Unlit;
        s.mr.sortingOrder = -44;
        s.block = new MaterialPropertyBlock();
        var c = new Color(0f, 0f, 0f, Tune.F("lighting.shadowAlpha", 0.55f));
        s.mesh.vertices = s.verts;
        s.mesh.colors = new[] { c, c, c, c };
        s.mesh.uv = s.uvs;
        s.mesh.triangles = Tris;
        s.Apply();
        if (!live) s.enabled = false;   // baked: placed once
        return s;
    }

    // The sun's lean in world units per unit of height, re-read only when the tuning changes.
    static Vector2 sunLean;
    static int sunLeanVersion = -1;
    static Vector2 SunLean
    {
        get
        {
            if (sunLeanVersion != Tune.Version)
            {
                sunLeanVersion = Tune.Version;
                sunLean = Lighting.ShadowDirection * Tune.F("lighting.shadowLength", 0.55f);
            }
            return sunLean;
        }
    }

    // What the mesh was last built from; a live shadow rebuilds only when one of these changes
    // (a new animation frame, a flip, a bob or squash of the sprite), not every frame.
    Sprite lastSprite;
    bool lastFlip;
    Vector3 lastPos, lastScale;
    Quaternion lastRot;
    Vector2 lastLean;
    Texture2D lastTex;

    void LateUpdate()
    {
        if (src == null) { Destroy(gameObject); return; }
        if (!live) return;
        mr.enabled = src.sprite != null && src.enabled && src.gameObject.activeInHierarchy;
        if (!mr.enabled) return;
        var t = src.transform;
        Vector2 lean = SunLean * t.lossyScale.y;
        if (src.sprite == lastSprite && src.flipX == lastFlip && lean == lastLean
            && t.localPosition == lastPos && t.localScale == lastScale && t.localRotation == lastRot) return;
        Apply();
    }

    void Apply()
    {
        var sprite = src.sprite;
        mr.enabled = sprite != null && src.enabled && src.gameObject.activeInHierarchy;
        if (sprite == null) return;

        Vector2 lean = SunLean * src.transform.lossyScale.y;
        lastSprite = sprite;
        lastFlip = src.flipX;
        lastLean = lean;
        lastPos = src.transform.localPosition;
        lastScale = src.transform.localScale;
        lastRot = src.transform.localRotation;
        var b = sprite.bounds;                                       // sprite space, relative to the pivot
        float foot = b.min.y + SpriteSheet.FootOffset(sprite);
        var t = src.transform;
        int i = 0;
        for (int row = 0; row < 2; row++)
        {
            float y = row == 0 ? b.min.y : b.max.y;
            for (int col = 0; col < 2; col++)
            {
                float x = col == 0 ? b.min.x : b.max.x;
                // the point on the ground under this column, then leaned over by its height above the feet
                Vector3 ground = t.TransformPoint(new Vector3(src.flipX ? -x : x, foot, 0f));
                Vector3 world = ground + (Vector3)(lean * (y - foot));
                verts[i++] = transform.InverseTransformPoint(world);
            }
        }
        var tex = sprite.texture;
        var r = sprite.textureRect;
        float u0 = r.xMin / tex.width, u1 = r.xMax / tex.width, v0 = r.yMin / tex.height, v1 = r.yMax / tex.height;
        uvs[0] = new Vector2(u0, v0); uvs[1] = new Vector2(u1, v0);
        uvs[2] = new Vector2(u0, v1); uvs[3] = new Vector2(u1, v1);
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        if (tex == lastTex) return;   // sheet frames share one texture, so this is rarely set again
        lastTex = tex;
        mr.GetPropertyBlock(block);
        block.SetTexture(MainTex, tex);
        mr.SetPropertyBlock(block);
    }
}
