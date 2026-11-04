using UnityEngine;
using UnityEngine.Rendering;

// Walk-in portal used after a level is solved. The portal is only a marked
// position: a glowing ring, a light column, a floating beacon and a screen-edge
// waypoint. No physical door is required.
public class LevelPortal : MonoBehaviour
{
    [Header("通关来源")]
    [SerializeField, InspectorName("通关机关（第3–6关）")] private PuzzleSwitch puzzleSwitch;
    [SerializeField, InspectorName("关卡出口（第1–2关，仅提示）")] private PuzzleRouteProgress routeProgress;

    [Header("路线")]
    [SerializeField, InspectorName("下一场景（留空表示最后一关）")] private string nextSceneName = "";
    [SerializeField, InspectorName("下一关序号（0 表示最后一关）")] private int nextLevelNumber = 1;
    [SerializeField, InspectorName("由本组件触发转场")] private bool ownsTrigger = true;
    [SerializeField, Min(0f), InspectorName("机关触发后出现延迟")] private float appearDelay = .55f;

    [Header("表现")]
    [SerializeField, InspectorName("传送环颜色")] private Color ringColor = new Color(.28f, .95f, .76f, 1f);
    [SerializeField, InspectorName("传送环材质")] private Material ringMaterial;
    [SerializeField, InspectorName("光柱材质")] private Material beamMaterial;

    private Transform visualRoot;
    private Transform outerRing;
    private Transform innerRing;
    private Transform beacon;
    private Transform diamond;
    private Light glow;
    private MaterialPropertyBlock ringBlock;
    private Material beamInstance;
    private Material mistInstance;
    private Camera cachedCamera;
    private PlayerMovement cachedPlayer;
    private bool opened;
    private float openedAt;
    private float openClock;
    private float bannerUntil;

    private bool SourceActive
    {
        get
        {
            if (puzzleSwitch != null) return puzzleSwitch.IsActivated;
            if (routeProgress != null) return routeProgress.IsOpen;
            return false;
        }
    }

    private void Start()
    {
        // Create the walk-in trigger first: visuals must never be able to block
        // the level transition if a material or renderer fails.
        BuildTrigger();
        BuildVisuals();
    }

    private void Update()
    {
        if (LevelFlow.IsTransitioning) return;

        if (!SourceActive)
        {
            if (opened) ClosePortal();
            openClock = 0f;
            return;
        }

        if (!opened)
        {
            openClock += Time.deltaTime;
            if (openClock >= appearDelay) OpenPortal();
            return;
        }

        AnimatePortal();
    }

    private void OpenPortal()
    {
        opened = true;
        openedAt = Time.unscaledTime;
        bannerUntil = openedAt + 4.2f;
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
            visualRoot.localScale = Vector3.one * .02f;
        }
        if (glow != null) glow.intensity = 0f;
    }

    private void ClosePortal()
    {
        opened = false;
        openClock = 0f;
        bannerUntil = 0f;
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        if (glow != null) glow.intensity = 0f;
    }

    private void AnimatePortal()
    {
        float grow = Mathf.Clamp01((Time.unscaledTime - openedAt) / .45f);
        float eased = Mathf.SmoothStep(.02f, 1f, grow);
        visualRoot.localScale = Vector3.one * eased;

        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.6f);
        outerRing.Rotate(0f, 22f * Time.deltaTime, 0f, Space.Self);
        innerRing.Rotate(0f, -34f * Time.deltaTime, 0f, Space.Self);
        outerRing.localScale = Vector3.one * (1f + .035f * pulse);
        innerRing.localScale = Vector3.one * (1f - .025f * pulse);

        if (beacon != null)
        {
            Vector3 p = beacon.localPosition;
            p.y = 3.05f + .16f * Mathf.Sin(Time.unscaledTime * 2.2f);
            beacon.localPosition = p;
            beacon.Rotate(0f, 60f * Time.deltaTime, 0f, Space.Self);
        }
        if (diamond != null) diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);

        if (glow != null)
            glow.intensity = (2.6f + 1.2f * pulse) * grow;
    }

    private void BuildTrigger()
    {
        if (!ownsTrigger) return;
        BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.7f, 0f);
        trigger.size = new Vector3(2.6f, 3.4f, 2.6f);
    }

    private void BuildVisuals()
    {
        if (ringMaterial == null) ringMaterial = Resources.Load<Material>("DemoPresentation/Energy");
        if (ringMaterial == null)
        {
            Debug.LogError("LevelPortal 缺少传送环材质，跳过通道表现。", this);
            enabled = false;
            return;
        }

        Material beamSource = Resources.Load<Material>("DemoPresentation/PortalBeam");
        if (beamSource == null) beamSource = beamMaterial;
        if (beamSource == null) beamSource = ringMaterial;

        ringBlock = new MaterialPropertyBlock();
        visualRoot = new GameObject("通道表现").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.gameObject.SetActive(false);

        outerRing = CreateRing("传送环_外", 1.12f, 1.34f, .045f).transform;
        innerRing = CreateRing("传送环_内", .72f, .82f, .06f).transform;

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "通道光柱";
        DestroyCollider(beam);
        beam.transform.SetParent(visualRoot, false);
        beam.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        beam.transform.localScale = new Vector3(1.9f, 2.25f, 1.9f);
        beamInstance = new Material(beamSource) { name = "通道光柱材质" };
        beamInstance.SetOverrideTag("RenderType", "Transparent");
        beamInstance.renderQueue = (int)RenderQueue.Transparent;
        Color beamColor = ringColor;
        beamColor.a = .17f;
        SetColor(beamInstance, beamColor);
        MeshRenderer beamRenderer = beam.GetComponent<MeshRenderer>();
        beamRenderer.sharedMaterial = beamInstance;
        beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;

        if (beamMaterial != null)
        {
            GameObject mist = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mist.name = "光柱外雾";
            DestroyCollider(mist);
            mist.transform.SetParent(visualRoot, false);
            mist.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            mist.transform.localScale = new Vector3(2.7f, 2.2f, 2.7f);
            mistInstance = new Material(beamMaterial) { name = "通道光柱外雾" };
            Color mistColor = ringColor;
            mistColor.a = .22f;
            SetColor(mistInstance, mistColor);
            MeshRenderer mistRenderer = mist.GetComponent<MeshRenderer>();
            mistRenderer.sharedMaterial = mistInstance;
            mistRenderer.shadowCastingMode = ShadowCastingMode.Off;
            mistRenderer.receiveShadows = false;
        }

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "光柱核心";
        DestroyCollider(core);
        core.transform.SetParent(visualRoot, false);
        core.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        core.transform.localScale = new Vector3(.42f, 2.5f, .42f);
        MeshRenderer coreRenderer = core.GetComponent<MeshRenderer>();
        coreRenderer.sharedMaterial = ringMaterial;
        coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
        coreRenderer.receiveShadows = false;
        ApplyRingColor(coreRenderer);

        beacon = new GameObject("悬浮标记").transform;
        beacon.SetParent(visualRoot, false);
        beacon.localPosition = new Vector3(0f, 3.05f, 0f);
        GameObject diamondObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diamondObject.name = "光标菱形";
        DestroyCollider(diamondObject);
        diamondObject.transform.SetParent(beacon, false);
        diamondObject.transform.localScale = new Vector3(.34f, .34f, .34f);
        diamond = diamondObject.transform;
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        MeshRenderer diamondRenderer = diamondObject.GetComponent<MeshRenderer>();
        diamondRenderer.sharedMaterial = ringMaterial;
        diamondRenderer.shadowCastingMode = ShadowCastingMode.Off;
        diamondRenderer.receiveShadows = false;
        ApplyRingColor(diamondRenderer);

        GameObject lightObject = new GameObject("通道灯光");
        lightObject.transform.SetParent(visualRoot, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = ringColor;
        glow.range = 8f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
    }

    private GameObject CreateRing(string name, float inner, float outer, float y)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(visualRoot, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildRingMesh(inner, outer, 64);
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ApplyRingColor(renderer);
        return go;
    }

    private void ApplyRingColor(MeshRenderer renderer)
    {
        if (ringBlock == null) ringBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(ringBlock);
        ringBlock.SetColor("_BaseColor", ringColor);
        ringBlock.SetColor("_Color", ringColor);
        ringBlock.SetColor("_EmissionColor", ringColor * 2.4f);
        renderer.SetPropertyBlock(ringBlock);
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2f);
    }

    private void OnDestroy()
    {
        if (beamInstance != null) Destroy(beamInstance);
        if (mistInstance != null) Destroy(mistInstance);
    }

    private static void DestroyCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    // A flat double-sided ring in the XZ plane.
    private static Mesh BuildRingMesh(float inner, float outer, int segments)
    {
        Mesh mesh = new Mesh { name = "通道传送环" };
        Vector3[] vertices = new Vector3[segments * 2];
        Vector3[] normals = new Vector3[segments * 2];
        int[] triangles = new int[segments * 12];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(cos * inner, 0f, sin * inner);
            vertices[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
            normals[i * 2] = Vector3.up;
            normals[i * 2 + 1] = Vector3.up;
        }
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int t = i * 12;
            int a = i * 2, b = next * 2, c = i * 2 + 1, d = next * 2 + 1;
            triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
            triangles[t + 3] = b; triangles[t + 4] = d; triangles[t + 5] = c;
            triangles[t + 6] = c; triangles[t + 7] = b; triangles[t + 8] = a;
            triangles[t + 9] = c; triangles[t + 10] = d; triangles[t + 11] = b;
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryEnter(other);
    }

    private void TryEnter(Collider other)
    {
        if (!ownsTrigger || !opened || LevelFlow.IsTransitioning) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        LevelFlow.Go(nextSceneName, nextLevelNumber <= 0);
    }

    private void OnGUI()
    {
        if (!opened || LevelFlow.IsTransitioning || DemoSession.IsGameplayBlocked) return;
        ChineseGameUI.EnsureFont();

        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) cachedCamera = FindFirstObjectByType<Camera>();
        if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerMovement>();
        if (cachedCamera == null || cachedPlayer == null) return;

        DrawBanner();

        Vector3 worldPoint = transform.position + Vector3.up * 2.7f;
        Vector3 screenPoint = cachedCamera.WorldToScreenPoint(worldPoint);
        bool behind = screenPoint.z < 0f;
        if (behind)
        {
            screenPoint.x = Screen.width - screenPoint.x;
            screenPoint.y = Screen.height - screenPoint.y;
        }

        float marginX = 58f, marginTop = 74f, marginBottom = 96f;
        bool onScreen = !behind
            && screenPoint.x > marginX && screenPoint.x < Screen.width - marginX
            && screenPoint.y > marginTop && screenPoint.y < Screen.height - marginBottom;
        float x = Mathf.Clamp(screenPoint.x, marginX, Screen.width - marginX);
        float y = Mathf.Clamp(screenPoint.y, marginTop, Screen.height - marginBottom);

        float distance = Vector3.Distance(cachedPlayer.transform.position, transform.position);
        string label = nextLevelNumber <= 0 ? $"终点 {distance:0}m" : $"通道 {distance:0}m";

        Color oldColor = GUI.color;
        float pulse = .72f + .28f * Mathf.Sin(Time.unscaledTime * 4.2f);
        GUI.color = new Color(0f, 0f, 0f, .55f);
        GUI.DrawTexture(new Rect(x - 9f, y - 9f, 18f, 18f), Texture2D.whiteTexture);
        GUI.color = new Color(ringColor.r, ringColor.g, ringColor.b, pulse);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
        GUI.DrawTexture(new Rect(x - 7f, y - 7f, 14f, 14f), Texture2D.whiteTexture);
        GUI.matrix = oldMatrix;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        labelStyle.normal.textColor = Color.white;
        bool placeAbove = y > Screen.height * .62f;
        Rect labelRect = new Rect(x - 62f, placeAbove ? y - 36f : y + 12f, 124f, 24f);
        GUI.color = new Color(.02f, .05f, .05f, .7f);
        GUI.DrawTexture(labelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(labelRect, label, labelStyle);

        if (!onScreen)
        {
            GUI.color = new Color(ringColor.r, ringColor.g, ringColor.b, pulse * .8f);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (distance < 7f)
        {
            GUIStyle prompt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            prompt.normal.textColor = new Color(ringColor.r, ringColor.g, ringColor.b, .95f);
            string promptText = nextLevelNumber <= 0 ? "走入光柱，完成全部关卡" : $"走入光柱，进入第 {nextLevelNumber} 关";
            GUI.Label(new Rect(0f, Screen.height * .62f, Screen.width, 32f), promptText, prompt);
        }
        GUI.color = oldColor;
    }

    private void DrawBanner()
    {
        float remaining = bannerUntil - Time.unscaledTime;
        if (remaining <= 0f) return;
        float alpha = Mathf.Clamp01(remaining / .8f);

        float width = Mathf.Min(560f, Screen.width - 48f);
        Rect panel = new Rect((Screen.width - width) * .5f, Mathf.Max(160f, Screen.height * .22f), width, 74f);
        Color oldColor = GUI.color;
        GUI.color = new Color(.015f, .05f, .052f, .86f * alpha);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(ringColor.r, ringColor.g, ringColor.b, .95f * alpha);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3f), Texture2D.whiteTexture);
        GUI.color = new Color(ringColor.r, ringColor.g, ringColor.b, .9f * alpha);
        GUI.DrawTexture(new Rect(panel.x + 18f, panel.y + 14f, 4f, 46f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, alpha);

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 27,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
        };
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 34f, panel.y + 9f, panel.width - 48f, 36f), "通道已开启", title);

        GUIStyle subtitle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft,
        };
        subtitle.normal.textColor = new Color(.78f, .88f, .86f, 1f);
        string sub = nextLevelNumber <= 0
            ? "跟随屏幕光标走入光柱，完成全部六关"
            : $"跟随屏幕光标走入光柱，进入第 {nextLevelNumber} 关";
        GUI.Label(new Rect(panel.x + 34f, panel.y + 43f, panel.width - 48f, 22f), sub, subtitle);
        GUI.color = oldColor;
    }
}
