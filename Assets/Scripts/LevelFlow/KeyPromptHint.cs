using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>单步提示的完成条件。</summary>
public enum KeyPromptGoal
{
    [InspectorName("按下指定按键")] PressKey,
    [InspectorName("按下鼠标左键")] MouseLeftPressed,
    [InspectorName("进入指定区域")] EnterArea,
    [InspectorName("已装备拳套")] GauntletEquipped,
    [InspectorName("按钮已被按下")] ButtonPressed,
    [InspectorName("已锁定动能方块")] MomentumLocked,
    [InspectorName("目标已达成（ILevelExitGate）")] GateSatisfied,
    [InspectorName("已坐上大炮")] CannonLoaded,
    [InspectorName("已被弹性墙弹射")] BouncyWallReflected,
    [InspectorName("（不启用）")] None,
}

/// <summary>一条按键提示：显示内容 + 开始显示的范围 + 完成条件。</summary>
[System.Serializable]
public sealed class KeyPromptStep
{
    [Header("显示范围")]
    [SerializeField, InspectorName("靠近该物体后开始显示（之后一直显示到完成为止）")] private Transform focus;
    [SerializeField, Min(0f), InspectorName("开始显示的半径（米）")] private float radius = 6f;

    [Header("提示内容")]
    [SerializeField, InspectorName("图标前文字")] private string prefixText = "按下";
    [SerializeField, InspectorName("按键图标")] private Texture2D icon;
    [SerializeField, InspectorName("图标后文字")] private string suffixText;
    [SerializeField, InspectorName("图标画成白色线稿（黑板线图标用）")] private bool whitenIcon = true;

    [Header("完成条件")]
    [SerializeField, InspectorName("完成方式")] private KeyPromptGoal goal = KeyPromptGoal.PressKey;
    [SerializeField, InspectorName("附加完成条件（任一满足即可，不启用 = None）")] private KeyPromptGoal extraGoal = KeyPromptGoal.None;
    [SerializeField, InspectorName("按键（完成方式 = 按下指定按键）")] private Key key = Key.None;
    [SerializeField, InspectorName("区域中心（完成方式 = 进入指定区域）")] private Transform areaTarget;
    [SerializeField, Min(0.1f), InspectorName("区域半径（米）")] private float areaRadius = 3f;
    [SerializeField, InspectorName("目标按钮（完成方式 = 按钮已被按下）")] private PuzzleButton button;
    [SerializeField, InspectorName("目标大炮（完成方式 = 已坐上大炮）")] private Cannon cannon;
    [SerializeField, InspectorName("目标弹性墙（完成方式 = 已被弹性墙弹射）")] private BouncyWall bouncyWall;
    [SerializeField, InspectorName("目标门禁（完成方式 = 目标已达成）")] private MonoBehaviour gate;
    [SerializeField, InspectorName("末尾显示进度 n/m（需引用 DemoTutorialGate）")] private bool showProgress;

    public Transform Focus => focus;
    public float Radius => radius;
    public string PrefixText => prefixText;
    public Texture2D Icon => icon;
    public string SuffixText => suffixText;
    public bool WhitenIcon => whitenIcon;
    public bool ShowProgress => showProgress;

    /// <summary>末尾的进度文字；未引用 DemoTutorialGate 时为空。</summary>
    public string ProgressText
    {
        get
        {
            DemoTutorialGate progressGate = gate as DemoTutorialGate;
            if (progressGate == null) return string.Empty;
            return "｜进度：" + progressGate.CompletedCount + "/" + progressGate.TotalCount;
        }
    }
    public bool HasContent => icon != null || !string.IsNullOrEmpty(prefixText) || !string.IsNullOrEmpty(suffixText);

    /// <summary>这一帧是否已经满足完成条件（附加条件任一满足即可）。</summary>
    public bool IsSatisfied(PlayerGauntletEquipment gauntlet, PlayerMovement movement, Transform player)
    {
        if (IsGoalSatisfied(goal, gauntlet, movement, player)) return true;
        return extraGoal != KeyPromptGoal.None && IsGoalSatisfied(extraGoal, gauntlet, movement, player);
    }

    private bool IsGoalSatisfied(KeyPromptGoal which, PlayerGauntletEquipment gauntlet, PlayerMovement movement, Transform player)
    {
        if (which == KeyPromptGoal.None) return false;

        if (which == KeyPromptGoal.GauntletEquipped)
            return gauntlet != null && gauntlet.HasGauntlet;

        if (which == KeyPromptGoal.MouseLeftPressed)
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        if (which == KeyPromptGoal.ButtonPressed)
            return button != null && button.HasBeenPressed;

        if (which == KeyPromptGoal.MomentumLocked)
            return movement != null && movement.HasLockedBox;

        if (which == KeyPromptGoal.CannonLoaded)
            return cannon != null && cannon.HasPlayer;

        if (which == KeyPromptGoal.BouncyWallReflected)
            return bouncyWall != null && bouncyWall.HasReflectedPlayer;

        if (which == KeyPromptGoal.GateSatisfied)
        {
            ILevelExitGate exitGate = gate as ILevelExitGate;
            return exitGate != null && exitGate.IsSatisfied;
        }

        if (which == KeyPromptGoal.EnterArea)
        {
            if (areaTarget == null || player == null) return false;
            float radius = Mathf.Max(0.1f, areaRadius);
            return (player.position + Vector3.up * 0.9f - areaTarget.position).sqrMagnitude <= radius * radius;
        }

        if (key == Key.None) return false;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key] != null && keyboard[key].wasPressedThisFrame;
    }
}

/// <summary>
/// 左上角的按键提示（IMGUI，沿用 <see cref="DemoTutorialHint"/> / <see cref="PlayerInteractor"/> 的原有样式：
/// GUI.Box 深色面板 + 中文字体标签，图标用 Assets/ButtonGizmos 的原图）。
/// 步骤按顺序播放：靠近 focus 后开始显示，之后一直显示到完成条件满足，再进入下一步。
/// 脚本里没有关卡坐标，位置由场景里的标记物体决定。
/// </summary>
public sealed class KeyPromptHint : MonoBehaviour, IPuzzleResettable
{
    [Header("引用")]
    [SerializeField, InspectorName("玩家（留空自动查找）")] private Transform player;
    [SerializeField, InspectorName("教程面板（留空自动查找，用于自动向下排开）")] private DemoTutorialHint tutorial;

    [Header("提示步骤（按顺序执行）")]
    [SerializeField, InspectorName("提示步骤（按顺序执行）")] private KeyPromptStep[] steps = new KeyPromptStep[0];

    [Header("界面")]
    [SerializeField, InspectorName("左上角边距")] private Vector2 margin = new Vector2(32f, 26f);
    [SerializeField, InspectorName("字号")] private int fontSize = 18;
    [SerializeField, Min(4f), InspectorName("图标尺寸")] private float iconSize = 26f;
    [SerializeField, Min(0f), InspectorName("左右内边距")] private float paddingX = 16f;
    [SerializeField, Min(0f), InspectorName("上下内边距")] private float paddingY = 8f;
    [SerializeField, Min(0f), InspectorName("文字与图标间距")] private float spacing = 8f;
    [SerializeField, InspectorName("自动排在教程面板下方")] private bool stackBelowTutorial = true;

    private PlayerMovement movement;
    private PlayerGauntletEquipment gauntlet;
    private bool[] completed;
    private bool[] revealed;

    private KeyPromptStep current;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;
    private int cachedFontSize;

    // 黑色线稿图标在深色面板上看不消，运行期刷白一份（不改、不复制资源）。
    private Texture2D whiteIconSource;
    private Texture2D whiteIconCache;

    private void Awake()
    {
        ResolveReferences();
        ResetProgress();
    }

    private void OnDisable()
    {
        current = null;
    }

    private void OnDestroy()
    {
        ReleaseWhiteIcon();
    }

    public void ResetPuzzle()
    {
        ResetProgress();
        current = null;
    }

    private void ResetProgress()
    {
        int count = steps != null ? steps.Length : 0;
        completed = new bool[count];
        revealed = new bool[count];
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked)
        {
            current = null;
            return;
        }

        ResolveReferences();
        if (completed == null || steps == null || completed.Length != steps.Length)
            ResetProgress();

        current = CurrentStep();
    }

    /// <summary>返回第一条未完成的步骤；沿途已满足的步骤会被标记完成。</summary>
    private KeyPromptStep CurrentStep()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            if (completed[i]) continue;

            KeyPromptStep step = steps[i];
            if (step == null)
            {
                completed[i] = true;
                continue;
            }

            if (step.IsSatisfied(gauntlet, movement, player))
            {
                completed[i] = true;
                continue;
            }

            if (!step.HasContent) continue;
            if (!IsRevealed(step, i)) return null;
            return step;
        }
        return null;
    }

    /// <summary>靠近过一次就记住，之后走远也不收回。</summary>
    private bool IsRevealed(KeyPromptStep step, int index)
    {
        if (step.Focus == null) return true;
        if (!revealed[index] && PlayerInRange(step)) revealed[index] = true;
        return revealed[index];
    }

    private bool PlayerInRange(KeyPromptStep step)
    {
        if (step.Focus == null) return true;
        if (player == null) return false;
        float radius = Mathf.Max(0.1f, step.Radius);
        return (player.position + Vector3.up * 0.9f - step.Focus.position).sqrMagnitude <= radius * radius;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            PlayerMovement found = FindFirstObjectByType<PlayerMovement>();
            if (found != null) player = found.transform;
        }
        if (player != null)
        {
            if (movement == null) movement = player.GetComponent<PlayerMovement>();
            if (gauntlet == null) gauntlet = player.GetComponent<PlayerGauntletEquipment>();
        }
        if (tutorial == null) tutorial = FindFirstObjectByType<DemoTutorialHint>();
    }

    private void OnGUI()
    {
        if (current == null || GameplayGate.IsBlocked) return;

        ChineseGameUI.EnsureFont();
        EnsureStyles();

        string prefix = current.PrefixText ?? string.Empty;
        string suffix = current.SuffixText ?? string.Empty;
        if (current.ShowProgress) suffix += current.ProgressText;
        Texture icon = ResolveIcon(current.Icon, current.WhitenIcon);

        GUIContent prefixContent = new GUIContent(prefix);
        GUIContent suffixContent = new GUIContent(suffix);
        Vector2 prefixSize = string.IsNullOrEmpty(prefix) ? Vector2.zero : labelStyle.CalcSize(prefixContent);
        Vector2 suffixSize = string.IsNullOrEmpty(suffix) ? Vector2.zero : labelStyle.CalcSize(suffixContent);
        float iconWidth = icon != null ? iconSize : 0f;

        float contentWidth = prefixSize.x + suffixSize.x;
        if (iconWidth > 0f)
            contentWidth += iconWidth
                + (prefixSize.x > 0f ? spacing : 0f)
                + (suffixSize.x > 0f ? spacing : 0f);

        float rowHeight = Mathf.Max(iconSize, Mathf.Max(prefixSize.y, suffixSize.y));
        float width = Mathf.Min(Screen.width - margin.x * 2f, contentWidth + paddingX * 2f);
        float height = rowHeight + paddingY * 2f;
        float top = margin.y + TutorialOffset();

        Rect panel = new Rect(margin.x, top, width, height);
        GUI.Box(panel, GUIContent.none);

        float x = panel.x + paddingX;
        float midY = panel.y + height * 0.5f;

        if (prefixSize.x > 0f)
        {
            GUI.Label(new Rect(x, midY - prefixSize.y * 0.5f, prefixSize.x, prefixSize.y), prefixContent, labelStyle);
            x += prefixSize.x + (iconWidth > 0f ? spacing : 0f);
        }

        if (icon != null)
        {
            GUI.DrawTexture(new Rect(x, midY - iconSize * 0.5f, iconSize, iconSize), icon, ScaleMode.ScaleToFit, true);
            x += iconWidth + (suffixSize.x > 0f ? spacing : 0f);
        }

        if (suffixSize.x > 0f)
            GUI.Label(new Rect(x, midY - suffixSize.y * 0.5f, suffixSize.x, suffixSize.y), suffixContent, labelStyle);
    }

    /// <summary>有教程面板时自动排到它下面，避免两块面板叠在一起。</summary>
    private float TutorialOffset()
    {
        if (!stackBelowTutorial) return 0f;
        if (tutorial == null || !tutorial.isActiveAndEnabled) return 0f;
        return tutorial.PanelHeight + 8f;
    }

    /// <summary>按需把图标刷成白色（保留 alpha），缓存一份。</summary>
    private Texture2D ResolveIcon(Texture2D source, bool whiten)
    {
        if (source == null) return null;
        if (!whiten) return source;
        if (whiteIconSource == source && whiteIconCache != null) return whiteIconCache;

        ReleaseWhiteIcon();
        whiteIconSource = source;
        whiteIconCache = Whiten(source);
        return whiteIconCache;
    }

    private void ReleaseWhiteIcon()
    {
        if (whiteIconCache != null) Destroy(whiteIconCache);
        whiteIconCache = null;
        whiteIconSource = null;
    }

    private static Texture2D Whiten(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = readable.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        readable.SetPixels32(pixels);
        readable.Apply();
        return readable;
    }

    private void EnsureStyles()
    {
        if (labelStyle != null && cachedFontSize == fontSize) return;
        cachedFontSize = fontSize;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        boxStyle = new GUIStyle(GUI.skin.box);
    }
}
