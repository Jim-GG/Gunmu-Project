using UnityEngine;
using UnityEngine.UI;

/// <summary>Small billboard health bar above the crystal enemy.</summary>
public class EnemyWorldHealthBar : MonoBehaviour
{
    private CrystalEnemy enemy;
    private Camera targetCamera;
    private Canvas canvas;
    private RectTransform fill;
    private Text nameText;
    private Text valueText;
    private Font font;
    private Texture2D whiteTexture;
    private Sprite whiteSprite;
    private float displayedHealth;

    public static EnemyWorldHealthBar Attach(CrystalEnemy owner, Transform parent)
    {
        GameObject go = new GameObject("EnemyWorldHealthBar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 2.42f, 0f);

        EnemyWorldHealthBar bar = go.AddComponent<EnemyWorldHealthBar>();
        bar.enemy = owner;
        bar.Build();
        return bar;
    }

    private void Build()
    {
        font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
            18);
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        canvas = gameObject.AddComponent<Canvas>();
        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(260f, 58f);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        transform.localScale = Vector3.one * 0.007f;

        CreateImage("Panel", transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.015f, 0.025f, 0.035f, 0.8f));
        CreateImage("Accent", transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(3f, 0f), new Color(1f, 0.25f, 0.22f, 1f));

        nameText = CreateText("Name", transform, "结晶异形体", 16, new Color(0.93f, 0.96f, 0.96f), TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, 1f), new Vector2(12f, -7f), new Vector2(150f, 22f), FontStyle.Bold);
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

        valueText = CreateText("Value", transform, "100 / 100", 13, new Color(0.62f, 0.7f, 0.72f), TextAnchor.UpperRight,
            new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(110f, 20f));

        RectTransform track = CreateImage("Track", transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-24f, 11f), new Color(0.1f, 0.13f, 0.15f, 0.95f));

        fill = CreateImage("Fill", track, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(-4f, -4f), new Color(0.95f, 0.28f, 0.24f));
        displayedHealth = enemy != null ? enemy.Health : 100f;
        targetCamera = Camera.main;
        if (targetCamera != null)
        {
            canvas.worldCamera = targetCamera;
        }
    }

    private void LateUpdate()
    {
        if (enemy == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera != null)
            {
                canvas.worldCamera = targetCamera;
            }
        }

        bool visible = enemy.IsAlive
            && targetCamera != null
            && (DemoManager.Instance == null || DemoManager.Instance.IsPlaying);

        canvas.enabled = visible;
        if (!visible)
        {
            return;
        }

        // Billboard the world-space canvas toward the active camera.
        transform.rotation = targetCamera.transform.rotation;

        // Keep roughly constant screen size instead of becoming enormous in melee range.
        float distance = Vector3.Distance(targetCamera.transform.position, transform.position);
        transform.localScale = Vector3.one * Mathf.Clamp(distance * 0.00085f, 0.0016f, 0.0105f);

        float target = enemy.Health;
        displayedHealth = Mathf.MoveTowards(displayedHealth, target, Time.deltaTime * 120f);
        float ratio = enemy.MaxHealth > 0f ? Mathf.Clamp01(displayedHealth / enemy.MaxHealth) : 0f;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        fill.gameObject.SetActive(ratio > 0.001f);

        valueText.text = Mathf.CeilToInt(Mathf.Max(0f, enemy.Health)) + " / " + Mathf.RoundToInt(enemy.MaxHealth);
        Color stateColor = enemy.IsTelegraphing
            ? new Color(1f, 0.3f, 0.27f)
            : new Color(0.9f, 0.55f, 0.5f);
        nameText.color = stateColor;
    }

    private void OnDestroy()
    {
        if (whiteSprite != null)
        {
            Destroy(whiteSprite);
        }
        if (whiteTexture != null)
        {
            Destroy(whiteTexture);
        }
    }

    private RectTransform CreateImage(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = go.GetComponent<Image>();
        image.sprite = whiteSprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string content,
        int size,
        Color color,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
