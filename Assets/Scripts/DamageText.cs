using UnityEngine;

// Small floating damage numbers so you can see what each spell is actually doing.
public class DamageText : MonoBehaviour
{
    const int MaxLive = 60;
    static int live;
    static Font builtin;

    float life;
    TextMesh text;
    MeshRenderer mr;
    Color color;

    // Finished numbers are parked and reused instead of building a new TextMesh per hit.
    static readonly System.Collections.Generic.Stack<DamageText> pool = new System.Collections.Generic.Stack<DamageText>();

    public static void Spawn(Vector2 pos, float amount, Color color)
    {
        if (live >= MaxLive || amount < 0.5f || !Settings.DamageNumbers) return;
        if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var font = UI.Font != null ? UI.Font : builtin;

        DamageText d = null;
        while (d == null && pool.Count > 0) d = pool.Pop();
        if (d == null)
        {
            var go = new GameObject("Dmg");
            d = go.AddComponent<DamageText>();
            d.text = go.AddComponent<TextMesh>();
            d.text.fontSize = 48;
            d.text.anchor = TextAnchor.MiddleCenter;
            d.mr = go.GetComponent<MeshRenderer>();
            d.mr.sortingOrder = 95;
        }
        else d.gameObject.SetActive(true);

        // The UI font can change with the art style, so set it on reused numbers too.
        if (d.text.font != font)
        {
            d.text.font = font;
            d.mr.sharedMaterial = font.material;
        }
        d.text.characterSize = 0.05f * UI.FontScale;
        d.text.fontStyle = font == builtin ? FontStyle.Bold : FontStyle.Normal;
        d.transform.position = pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f);
        d.text.text = Number(Mathf.RoundToInt(amount));
        d.color = color;
        d.text.color = color;
        d.life = 0.6f;
        live++;
    }

    static readonly string[] numbers = new string[1000];
    static string Number(int n) => n >= 0 && n < numbers.Length ? numbers[n] ??= n.ToString() : n.ToString();

    void Release()
    {
        live--;
        gameObject.SetActive(false);
        pool.Push(this);
    }

    void OnDestroy()
    {
        if (gameObject.activeSelf) live--;   // parked ones were already counted out
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Release(); return; }
        transform.position += Vector3.up * 1.5f * Time.deltaTime;
        var c = color;
        c.a = Mathf.Clamp01(life / 0.3f);
        text.color = c;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCount()
    {
        live = 0;
        pool.Clear();
    }
}
