using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-built HUD, feedback and end-of-demo screens.</summary>
public class DemoHUD : MonoBehaviour
{
    private static readonly Color TextColor = new Color(0.92f, 0.96f, 0.96f);
    private static readonly Color MutedColor = new Color(0.55f, 0.64f, 0.68f);
    private static readonly Color TealColor = new Color(0.38f, 0.95f, 0.84f);
    private static readonly Color AmberColor = new Color(1f, 0.67f, 0.28f);
    private static readonly Color DangerColor = new Color(1f, 0.29f, 0.31f);
    private static readonly Color HealthRed = new Color(1f, 0.2f, 0.22f);
    private static readonly Color ChargeBlue = new Color(0.25f, 0.6f, 1f);
    private static readonly Color PanelColor = new Color(0.02f, 0.035f, 0.045f, 0.78f);
    private static readonly Color TrackColor = new Color(0.12f, 0.17f, 0.19f, 0.9f);

    private PlayerCombat player;
    private CrystalEnemy enemy;
    private Canvas canvas;
    private Font font;
    private Texture2D whiteTexture;
    private Sprite whiteSprite;

    private RectTransform healthFill;
    private RectTransform energyFill;
    private RectTransform enemyFill;
    private Image healthFillImage;
    private Image energyFillImage;
    private Image enemyFillImage;
    private Text healthValueText;
    private Text energyValueText;
    private Text enemyNameText;
    private Text enemyValueText;
    private Text blockText;
    private Text warningText;
    private Text toastText;
    private Text chargeHintText;
    private Text objectiveText;
    private GameObject enemyGroup;
    private GameObject warningGroup;
    private GameObject winPanel;
    private GameObject deathPanel;
    private Text winStatsText;
    private Image damageOverlay;
    private Image flashOverlay;
    private Image warningOverlay;

    private float displayHealth;
    private float displayEnergy;
    private float displayEnemyHealth;
    private float toastTimer;
    private float damageAlpha;
    private float flashAlpha;
    private bool initialized;
    private bool winShown;
    private bool deathShown;

    private void Awake()
    {
        BuildUi();
    }

    public void Initialize(PlayerCombat playerCombat, CrystalEnemy crystalEnemy)
    {
        if (initialized)
        {
            return;
        }

        player = playerCombat;
        enemy = crystalEnemy;

        if (player != null)
        {
            displayHealth = player.Health;
            displayEnergy = player.Energy;
            player.BlockSucceeded += OnBlockSucceeded;
            player.ShockReleased += OnShockReleased;
            player.Damaged += OnDamaged;
        }

        if (enemy != null)
        {
            displayEnemyHealth = enemy.Health;
        }

        initialized = true;
        SetBar(healthFill, 1f);
        SetBar(energyFill, 0f);
        SetBar(enemyFill, 1f);
        UpdateTexts();
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.BlockSucceeded -= OnBlockSucceeded;
            player.ShockReleased -= OnShockReleased;
            player.Damaged -= OnDamaged;
        }

        if (whiteSprite != null)
        {
            Destroy(whiteSprite);
        }
        if (whiteTexture != null)
        {
            Destroy(whiteTexture);
        }
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        float dt = Time.deltaTime;

        displayHealth = Mathf.MoveTowards(displayHealth, player != null ? player.Health : 0f, dt * 180f);
        displayEnergy = Mathf.MoveTowards(displayEnergy, player != null ? player.Energy : 0f, dt * 220f);

        if (enemy != null)
        {
            displayEnemyHealth = Mathf.MoveTowards(displayEnemyHealth, enemy.Health, dt * 160f);
            SetBar(enemyFill, enemy.MaxHealth > 0f ? displayEnemyHealth / enemy.MaxHealth : 0f);
        }

        SetBar(healthFill, player != null && player.MaxHealth > 0f ? displayHealth / player.MaxHealth : 0f);
        SetBar(energyFill, player != null && player.MaxEnergy > 0f ? displayEnergy / player.MaxEnergy : 0f);

        if (healthFillImage != null && player != null)
        {
            float ratio = Mathf.Clamp01(displayHealth / player.MaxHealth);
            bool low = ratio <= 0.3f;
            float pulse = low ? 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
            healthFillImage.color = new Color(HealthRed.r * pulse, HealthRed.g * pulse, HealthRed.b * pulse, 1f);
        }

        if (energyFillImage != null)
        {
            float ratio = Mathf.Clamp01(displayEnergy / player.MaxEnergy);
            Color energyColor = Color.Lerp(
                new Color(0.18f, 0.4f, 0.82f),
                new Color(0.35f, 0.72f, 1f),
                Mathf.SmoothStep(0f, 1f, ratio));
            if (ratio > 0.98f)
            {
                energyColor = Color.Lerp(energyColor, Color.white, 0.24f + 0.16f * Mathf.Sin(Time.time * 7f));
            }
            energyFillImage.color = energyColor;
        }

        if (enemyFillImage != null)
        {
            enemyFillImage.color = enemy != null && enemy.IsTelegraphing ? DangerColor : new Color(0.72f, 0.32f, 0.35f);
        }

        UpdateTexts();
        UpdateWarnings();
        UpdateOverlays(dt);
    }

    private void UpdateTexts()
    {
        if (player == null)
        {
            return;
        }

        healthValueText.text = Mathf.CeilToInt(Mathf.Max(0f, player.Health)) + " / " + Mathf.RoundToInt(player.MaxHealth);
        energyValueText.text = Mathf.CeilToInt(Mathf.Clamp(player.Energy, 0f, player.MaxEnergy)) + " / " + Mathf.RoundToInt(player.MaxEnergy);

        if (enemy != null)
        {
            enemyValueText.text = Mathf.CeilToInt(Mathf.Max(0f, enemy.Health)) + " / " + Mathf.RoundToInt(enemy.MaxHealth);
            enemyNameText.color = enemy.IsTelegraphing ? DangerColor : TextColor;
            if (!enemy.IsAlive)
            {
                enemyNameText.text = "结晶异形体  //  已清除";
                enemyValueText.text = "0 / " + Mathf.RoundToInt(enemy.MaxHealth);
            }
        }

        blockText.gameObject.SetActive(player.IsBlocking);
        blockText.color = TealColor;

        if (player.IsAtFullCharge)
        {
            chargeHintText.text = "储能已满 — 按 左键 释放冲击";
            chargeHintText.color = Color.Lerp(ChargeBlue, Color.white, 0.3f + 0.2f * Mathf.Sin(Time.time * 5f));
        }
        else if (player.IsBlocking)
        {
            chargeHintText.text = "格挡中 — 撞击成功即可储能";
            chargeHintText.color = ChargeBlue;
        }
        else
        {
            chargeHintText.text = "按住 右键 格挡敌人撞击以储能";
            chargeHintText.color = MutedColor;
        }

        if (enemy != null && !enemy.IsAlive)
        {
            objectiveText.text = "目标已清除";
            objectiveText.color = TealColor;
        }
    }

    private void UpdateWarnings()
    {
        bool warning = enemy != null
            && enemy.IsTelegraphing
            && DemoManager.Instance != null
            && DemoManager.Instance.IsPlaying;

        warningGroup.SetActive(warning);
        if (warning)
        {
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 9f));
            warningText.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, pulse);
            warningOverlay.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.035f + 0.025f * pulse);
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            toastText.gameObject.SetActive(true);
            Color c = toastText.color;
            c.a = Mathf.Clamp01(toastTimer < 0.35f ? toastTimer / 0.35f : 1f);
            toastText.color = c;
        }
        else
        {
            toastText.gameObject.SetActive(false);
        }
    }

    private void UpdateOverlays(float dt)
    {
        if (damageAlpha > 0f)
        {
            damageAlpha = Mathf.Max(0f, damageAlpha - dt * 1.5f);
        }
        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.Max(0f, flashAlpha - dt * 2.2f);
        }

        damageOverlay.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, damageAlpha);
        flashOverlay.color = new Color(0.5f, 1f, 0.95f, flashAlpha);
        warningOverlay.gameObject.SetActive(warningGroup.activeSelf);
    }

    private void OnBlockSucceeded(float gained)
    {
        string text = gained > 0.1f
            ? "格挡成功   ·   储能 +" + Mathf.RoundToInt(gained)
            : "格挡成功   ·   储能已满";
        ShowToast(text, TealColor, 1.7f);
        flashAlpha = Mathf.Max(flashAlpha, 0.045f);
    }

    private void OnShockReleased(float spent)
    {
        ShowToast("冲量释放", AmberColor, 1.5f);
        flashAlpha = Mathf.Max(flashAlpha, 0.17f);
        damageAlpha = 0f;
    }

    private void OnDamaged(float damage)
    {
        ShowToast("受到撞击  ·  -" + Mathf.RoundToInt(damage) + " 生命", DangerColor, 1.8f);
        damageAlpha = Mathf.Max(damageAlpha, 0.32f);
    }

    private void ShowToast(string text, Color color, float duration)
    {
        toastText.text = text;
        toastText.color = color;
        toastTimer = duration;
        toastText.gameObject.SetActive(true);
    }

    public void ShowWin(int blocks, int shocks, int remainingHealth)
    {
        if (winShown || deathShown)
        {
            return;
        }

        winShown = true;
        warningGroup.SetActive(false);
        winStatsText.text = "格挡成功  " + blocks + "   ·   冲量释放  " + shocks + "   ·   剩余生命  " + remainingHealth;
        winPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowDeath()
    {
        if (deathShown || winShown)
        {
            return;
        }

        deathShown = true;
        warningGroup.SetActive(false);
        deathPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void BuildUi()
    {
        font = CreateFont();
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "DemoHUD_White",
            hideFlags = HideFlags.HideAndDontSave
        };
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        GameObject canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("HUDRoot", canvasObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);

        BuildObjective(root);
        BuildEnemyBar(root);
        enemyGroup.SetActive(false); // Enemy vitals live above the enemy only.
        BuildCrosshair(root);
        BuildPlayerStatus(root);
        BuildControls(root);
        BuildWarnings(root);
        BuildEndScreens(root);

        damageOverlay = CreateImage("DamageOverlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
        flashOverlay = CreateImage("FlashOverlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();

        // End screens must cover both the normal HUD and the impact overlays.
        winPanel.transform.SetAsLastSibling();
        deathPanel.transform.SetAsLastSibling();
        winPanel.SetActive(false);
        deathPanel.SetActive(false);
    }

    private void BuildObjective(RectTransform root)
    {
        RectTransform group = CreateRect("Objective", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -32f), new Vector2(500f, 80f), Color.clear);
        CreateImage("Accent", group, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(3f, 52f), TealColor);
        CreateText("Eyebrow", group, "测试目标  /  OBJECTIVE", 15, MutedColor, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -3f), new Vector2(400f, 20f));
        objectiveText = CreateText("ObjectiveText", group, "击败结晶异形体", 24, TextColor, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -27f), new Vector2(440f, 30f), FontStyle.Bold);
    }

    private void BuildEnemyBar(RectTransform root)
    {
        enemyGroup = CreateRect("EnemyGroup", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 64f), Color.clear).gameObject;
        CreateImage("EnemyPlate", enemyGroup.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.035f, 0.045f, 0.5f));

        enemyNameText = CreateText("EnemyName", enemyGroup.transform, "结晶异形体  //  CRYSTAL HOST", 17, TextColor, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(-140f, 22f), FontStyle.Bold);
        enemyValueText = CreateText("EnemyValue", enemyGroup.transform, "100 / 100", 14, MutedColor, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -10f), new Vector2(120f, 20f));

        RectTransform track = CreateImage("EnemyTrack", enemyGroup.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-32f, 9f), TrackColor);
        enemyFill = CreateImage("EnemyFill", track, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-2f, -2f), new Color(0.72f, 0.32f, 0.35f));
        enemyFillImage = enemyFill.GetComponent<Image>();
        SetupFill(enemyFill);
    }

    private void BuildCrosshair(RectTransform root)
    {
        RectTransform group = CreateRect("Crosshair", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f), Color.clear);
        Color crossColor = new Color(0.8f, 0.92f, 0.92f, 0.72f);
        CreateImage("Top", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 13f), new Vector2(2f, 9f), crossColor);
        CreateImage("Bottom", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -13f), new Vector2(2f, 9f), crossColor);
        CreateImage("Left", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-13f, 0f), new Vector2(9f, 2f), crossColor);
        CreateImage("Right", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(13f, 0f), new Vector2(9f, 2f), crossColor);
        CreateImage("Dot", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f), crossColor);

        blockText = CreateText("BlockState", root, "●  格挡中", 18, TealColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(300f, 26f), FontStyle.Bold);
        blockText.gameObject.SetActive(false);
    }

    private void BuildPlayerStatus(RectTransform root)
    {
        RectTransform panel = CreateImage(
            "PlayerStatusPanel",
            root,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 120f),
            new Vector2(620f, 154f),
            new Color(0.012f, 0.022f, 0.03f, 0.95f));

        // Blue charge rail sits above the red health rail.
        CreateImage("ChargeAccent", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f), ChargeBlue);
        CreateImage("HealthAccent", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f), HealthRed);

        CreateText("ChargeLabel", panel, "蓄力  /  CHARGE", 15, ChargeBlue, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(320f, 22f), FontStyle.Bold);
        energyValueText = CreateText("ChargeValue", panel, "0 / 100", 24, TextColor, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -5f), new Vector2(180f, 30f), FontStyle.Bold);

        RectTransform chargeTrack = CreateImage(
            "ChargeTrack", panel,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -40f), new Vector2(-40f, 16f), TrackColor);
        energyFill = CreateImage("ChargeFill", chargeTrack, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(-4f, -4f), ChargeBlue);
        energyFillImage = energyFill.GetComponent<Image>();
        SetupFill(energyFill);

        for (int i = 1; i < 5; i++)
        {
            CreateImage("ChargeTick_" + i, chargeTrack, new Vector2(i / 5f, 0f), new Vector2(i / 5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 0f), new Color(0.02f, 0.035f, 0.05f, 0.85f));
        }

        CreateText("HealthLabel", panel, "生命  /  VITALS", 15, HealthRed, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -70f), new Vector2(320f, 22f), FontStyle.Bold);
        healthValueText = CreateText("HealthValue", panel, "100 / 100", 22, TextColor, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -66f), new Vector2(180f, 28f), FontStyle.Bold);

        RectTransform healthTrack = CreateImage(
            "HealthTrack", panel,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -98f), new Vector2(-40f, 18f), TrackColor);
        healthFill = CreateImage("HealthFill", healthTrack, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(-4f, -4f), HealthRed);
        healthFillImage = healthFill.GetComponent<Image>();
        SetupFill(healthFill);

        chargeHintText = CreateText("ChargeHint", panel, "按住 右键 格挡敌人撞击以储能", 13, MutedColor, TextAnchor.LowerLeft,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 8f), new Vector2(-40f, 18f));
    }

    private void BuildHealth(RectTransform root)
    {
        RectTransform panel = CreateImage("HealthPanel", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(42f, 46f), new Vector2(430f, 104f), PanelColor);
        CreateText("HealthLabel", panel, "生命  /  VITALS", 14, MutedColor, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(240f, 20f));
        healthValueText = CreateText("HealthValue", panel, "100", 30, TextColor, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -4f), new Vector2(120f, 36f), FontStyle.Bold);

        RectTransform track = CreateImage("HealthTrack", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 22f), new Vector2(394f, 16f), TrackColor);
        healthFill = CreateImage("HealthFill", track, Vector2.zero, new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(-4f, -4f), TealColor);
        healthFillImage = healthFill.GetComponent<Image>();
        SetupFill(healthFill);

        CreateText("HealthHint", panel, "低生命时仍可格挡并反击", 12, new Color(MutedColor.r, MutedColor.g, MutedColor.b, 0.75f), TextAnchor.LowerLeft,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 7f), new Vector2(300f, 16f));
    }

    private void BuildEnergy(RectTransform root)
    {
        RectTransform panel = CreateImage("EnergyPanel", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-42f, 46f), new Vector2(500f, 124f), PanelColor);
        CreateText("EnergyLabel", panel, "冲量储能  /  IMPULSE CHARGE", 14, MutedColor, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(320f, 20f));
        energyValueText = CreateText("EnergyValue", panel, "0 / 100", 24, TextColor, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -6f), new Vector2(160f, 30f), FontStyle.Bold);

        RectTransform track = CreateImage("EnergyTrack", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 38f), new Vector2(464f, 24f), TrackColor);
        energyFill = CreateImage("EnergyFill", track, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(-2f, -4f), TealColor);
        energyFillImage = energyFill.GetComponent<Image>();
        SetupFill(energyFill);

        for (int i = 1; i < 5; i++)
        {
            CreateImage("EnergyTick_" + i, track, new Vector2(i / 5f, 0f), new Vector2(i / 5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 24f), new Color(0.02f, 0.03f, 0.04f, 0.7f));
        }

        chargeHintText = CreateText("ChargeHint", panel, "按住 右键 格挡敌人撞击以储能", 14, MutedColor, TextAnchor.LowerLeft,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(18f, 10f), new Vector2(-36f, 20f));
    }

    private void BuildControls(RectTransform root)
    {
        RectTransform panel = CreateImage("ControlsPanel", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1120f, 28f), new Color(0.02f, 0.03f, 0.04f, 0.55f));
        CreateText("Controls", panel, "WASD 移动    空格 跳跃    右键 格挡    左键 出拳 / 释放冲击    Esc 解锁鼠标    R 重新开始", 13, new Color(MutedColor.r, MutedColor.g, MutedColor.b, 0.95f), TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    private void BuildWarnings(RectTransform root)
    {
        warningGroup = CreateRect("WarningGroup", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(800f, 34f), Color.clear).gameObject;
        warningText = CreateText("WarningText", warningGroup.transform, "⚠  撞击预警   ·   按住右键格挡", 23, DangerColor, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, FontStyle.Bold);
        warningGroup.SetActive(false);

        toastText = CreateText("Toast", root, "", 20, TealColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(800f, 32f), FontStyle.Bold);
        toastText.gameObject.SetActive(false);

        warningOverlay = CreateImage("WarningOverlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0f)).GetComponent<Image>();
    }

    private void BuildEndScreens(RectTransform root)
    {
        winPanel = CreateRect("WinPanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.01f, 0.018f, 0.024f, 0.93f)).gameObject;
        RectTransform winContent = CreateRect("WinContent", winPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f), Color.clear);

        CreateImage("WinAccent", winContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(72f, 3f), TealColor);
        CreateText("WinEyebrow", winContent, "PLAYTEST COMPLETE  /  ZERO BOUNDARY", 15, TealColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(800f, 24f));
        CreateText("WinTitle", winContent, "测试完成", 64, TextColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 52f), new Vector2(800f, 90f), FontStyle.Bold);
        CreateText("WinSubtitle", winContent, "威胁已清除  /  THREAT NEUTRALIZED", 18, MutedColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(800f, 28f));

        winStatsText = CreateText("WinStats", winContent, "", 16, TealColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(800f, 26f));
        CreateText("WinRestart", winContent, "按  R  重新开始测试", 18, TextColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(800f, 28f), FontStyle.Bold);

        deathPanel = CreateRect("DeathPanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.005f, 0.008f, 0.9f)).gameObject;
        RectTransform deathContent = CreateRect("DeathContent", deathPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 300f), Color.clear);
        CreateText("DeathEyebrow", deathContent, "VITAL SIGNAL LOST  /  ZERO BOUNDARY", 15, DangerColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(800f, 24f));
        CreateText("DeathTitle", deathContent, "生命信号中断", 56, TextColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 35f), new Vector2(800f, 80f), FontStyle.Bold);
        CreateText("DeathRestart", deathContent, "按  R  重新开始测试", 18, MutedColor, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(800f, 28f), FontStyle.Bold);
    }

    private void SetupFill(RectTransform fill)
    {
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        fill.gameObject.GetComponent<Image>().raycastTarget = false;
    }

    private void SetBar(RectTransform fill, float ratio)
    {
        if (fill == null)
        {
            return;
        }

        ratio = Mathf.Clamp01(ratio);
        fill.gameObject.SetActive(ratio > 0.001f);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
    }

    private RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        RectTransform rect = CreateImage(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, color);
        return rect;
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

    private static Font CreateFont()
    {
        Font result = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
            24);
        if (result != null)
        {
            return result;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
