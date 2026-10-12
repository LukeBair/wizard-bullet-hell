using UnityEngine;

// Small floating damage numbers so you can see what each spell is actually doing.
public class DamageText : MonoBehaviour
{
    const int MaxLive = 60;
    static int live;
    static Font font;

    float life;
    TextMesh text;
    Color color;

    public static void Spawn(Vector2 pos, float amount, Color color)
    {
        if (live >= MaxLive || amount < 0.5f) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var go = new GameObject("Dmg");
        go.transform.position = pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f);
        var d = go.AddComponent<DamageText>();
        d.text = go.AddComponent<TextMesh>();
        d.text.font = font;
        d.text.text = Mathf.RoundToInt(amount).ToString();
        d.text.fontSize = 48;
        d.text.characterSize = 0.05f;
        d.text.anchor = TextAnchor.MiddleCenter;
        d.text.fontStyle = FontStyle.Bold;
        d.color = color;
        d.text.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 95;
        d.life = 0.6f;
        live++;
    }

    void OnDestroy() => live--;

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        transform.position += Vector3.up * 1.5f * Time.deltaTime;
        var c = color;
        c.a = Mathf.Clamp01(life / 0.3f);
        text.color = c;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCount() => live = 0;
}
