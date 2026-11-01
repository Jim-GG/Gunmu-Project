using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time installation into the open tutorial scene; later edits are left alone.
[InitializeOnLoad]
public static class DemoPresentationBuilder
{
    private const string Folder = "Assets/Resources/DemoPresentation";
    private const string Marker = "Demo_实验室外观";
    private static bool editReady;
    private static double readyAfter;
    static DemoPresentationBuilder()
    {
        editReady = !EditorApplication.isPlayingOrWillChangePlaymode;
        readyAfter = EditorApplication.timeSinceStartup + 2;
        EditorApplication.playModeStateChanged += state =>
        {
            editReady = state == PlayModeStateChange.EnteredEditMode;
            readyAfter = EditorApplication.timeSinceStartup + 2;
        };
        EditorApplication.update += AutoInstall;
    }
    private static void AutoInstall()
    {
        if (!editReady || EditorApplication.timeSinceStartup < readyAfter || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Demo_IntroCombat") return;
        EditorApplication.update -= AutoInstall;
        if (GameObject.Find(Marker) == null || !File.Exists(Folder + "/HeavyImpact.wav")) Install();
    }

    [MenuItem("Tools/零界/安装第一场景演示外观")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Demo_IntroCombat") { Debug.LogWarning("请先打开 Demo_IntroCombat 场景。"); return; }
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        BuildAssets();
        if (GameObject.Find(Marker) == null) BuildRoom();
        EnsureConsole();
        foreach (PlayerChargedPunch p in Object.FindObjectsByType<PlayerChargedPunch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (p.gameObject.scene == scene && p.GetComponent<PunchPresentation>() == null) Undo.AddComponent<PunchPresentation>(p.gameObject);
        foreach (TutorialEnemy e in Object.FindObjectsByType<TutorialEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.gameObject.scene == scene && e.GetComponent<TutorialEnemyVisual>() == null) Undo.AddComponent<TutorialEnemyVisual>(e.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("零界演示外观已安装：封闭实验室、可替换拳套/共鸣者预制体、材质与粒子资源。场景已保存。");
        File.WriteAllText("Library/DemoPresentationInstalled.txt", System.DateTime.Now.ToString("O"));
    }

    private static Material Get(string name) => AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
    private static Material Material(string name, Color color, float metal = 0f, bool glow = false, bool worn = true)
    {
        string path = Folder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metal); mat.SetFloat("_Smoothness", .25f);
        if (worn)
        {
            string texturePath = Folder + "/" + name + "_表面.png";
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var random = new System.Random(name.Sum(c => (int)c));
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float noise = .76f + (float)random.NextDouble() * .24f;
                bool seam = x < 2 || y < 2 || (x % 32 == 0 && y % 13 < 5);
                tex.SetPixel(x, y, new Color(noise, noise, noise) * (seam ? .5f : 1f));
            }
            tex.Apply(); File.WriteAllBytes(texturePath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.wrapMode = TextureWrapMode.Repeat; importer.SaveAndReimport();
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        }
        if (glow) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 2f); }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void BuildAssets()
    {
        Material("Steel", new Color(.22f, .25f, .24f), .75f);
        Material("Green", new Color(.22f, .29f, .21f), .5f);
        Material("Yellow", new Color(.69f, .47f, .1f), .45f);
        Material("Dark", new Color(.045f, .055f, .055f), .3f);
        Material("Floor", new Color(.29f, .3f, .27f), .25f);
        Material("Skin", new Color(.45f, .39f, .32f));
        Material("Cloth", new Color(.24f, .28f, .22f));
        Material("Crystal", new Color(.52f, .24f, .15f), .7f, true, false);
        Material("Energy", new Color(.12f, .65f, .85f), .3f, true, false);
        Material("Lamp", new Color(.65f, .75f, .65f), 0f, true, false);
        BuildParticleMaterial("Spark", false); BuildParticleMaterial("Mist", true);
        BuildSound("MechanicalSwing", false); BuildSound("HeavyImpact", true);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/GauntletVisual.prefab") == null) BuildGauntlet();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ResonatorVisual.prefab") == null) BuildEnemy();
    }

    private static void BuildSound(string name, bool impact)
    {
        string path = Folder + "/" + name + ".wav";
        if (File.Exists(path)) return;
        const int rate = 22050;
        int samples = (int)(rate * (impact ? .28f : .18f));
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
            writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            var random = new System.Random(17); float filtered = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .18f);
                float envelope = Mathf.Min(1f, t * 450f) * Mathf.Exp(-t * (impact ? 19f : 26f));
                float wave = impact ? .6f * Mathf.Sin(t * 2 * Mathf.PI * (90f - 130f * t)) + filtered * .6f
                    : filtered * .8f + .2f * Mathf.Sin(t * 2 * Mathf.PI * 180f);
                writer.Write((short)(Mathf.Clamp(wave * envelope * .75f, -1, 1) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path);
    }

    private static void BuildParticleMaterial(string name, bool soft)
    {
        string path = Folder + "/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - radius), soft ? 1.5f : .5f);
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply(); string texPath = Folder + "/" + name + ".png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
        AssetDatabase.CreateAsset(mat, path);
    }

    private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, string material, bool solid = false)
    {
        GameObject go = GameObject.CreatePrimitive(type); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Get(material);
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static void Box(Transform p, string name, Vector3 pos, Vector3 scale, string mat, bool solid = false) => Part(p, name, PrimitiveType.Cube, pos, scale, mat, solid);

    private static void BuildGauntlet()
    {
        GameObject root = new GameObject("冲量压缩拳套_可替换"); Transform p = root.transform;
        Box(p, "前臂装甲", new Vector3(0, 0, -.11f), new Vector3(.19f, .15f, .4f), "Green");
        Box(p, "底部护板", new Vector3(0, -.085f, -.1f), new Vector3(.22f, .04f, .44f), "Steel");
        Box(p, "打击板", new Vector3(0, 0, .17f), new Vector3(.26f, .23f, .12f), "Steel");
        Box(p, "橡胶缓冲", new Vector3(0, 0, .095f), new Vector3(.23f, .19f, .08f), "Dark");
        Box(p, "压力杆", new Vector3(0, .13f, -.15f), new Vector3(.17f, .035f, .04f), "Yellow");
        for (int side = -1; side <= 1; side += 2)
        {
            Box(p, "液压导轨", new Vector3(side * .105f, 0, -.11f), new Vector3(.025f, .025f, .4f), "Steel");
            Box(p, "主储能缸", new Vector3(side * .103f, .055f, -.1f), new Vector3(.035f, .045f, .23f), "Energy");
        }
        for (int i = 0; i < 4; i++)
            Box(p, "警示条", new Vector3((i - 1.5f) * .05f, .119f, .17f), new Vector3(.025f, .008f, .1f), i % 2 == 0 ? "Yellow" : "Dark");
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/GauntletVisual.prefab"); Object.DestroyImmediate(root);
    }

    private static void BuildEnemy()
    {
        GameObject root = new GameObject("普通共鸣者_可替换"); Transform p = root.transform;
        Part(p, "旧工装躯干", PrimitiveType.Capsule, new Vector3(0, .05f, 0), new Vector3(.64f, .48f, .36f), "Cloth");
        Part(p, "头部", PrimitiveType.Sphere, new Vector3(0, .73f, .06f), new Vector3(.31f, .38f, .31f), "Skin");
        Box(p, "面部阴影", new Vector3(0, .75f, .215f), new Vector3(.22f, .065f, .035f), "Dark");
        Box(p, "腰带", new Vector3(0, -.28f, 0), new Vector3(.49f, .085f, .33f), "Dark");
        for (int side = -1; side <= 1; side += 2)
        {
            Part(p, "工装裤腿", PrimitiveType.Capsule, new Vector3(side * .15f, -.58f, 0), new Vector3(.23f, .36f, .25f), "Cloth");
            Box(p, "靴子", new Vector3(side * .15f, -.89f, .065f), new Vector3(.24f, .18f, .36f), "Dark");
            var pivot = new GameObject(side < 0 ? "左臂轴" : "右臂轴"); pivot.transform.SetParent(p, false);
            pivot.transform.localPosition = new Vector3(side * .37f, .39f, 0);
            Part(pivot.transform, "袖子", PrimitiveType.Capsule, new Vector3(side * .04f, -.24f, 0), new Vector3(.21f, .28f, .22f), "Cloth");
            Part(pivot.transform, "手掌", PrimitiveType.Sphere, new Vector3(side * .05f, -.56f, .04f), new Vector3(.19f, .22f, .18f), "Skin");
        }
        for (int i = 0; i < 7; i++)
        {
            var crystal = Part(p, "局部阿比森纳斯结晶", PrimitiveType.Cube,
                new Vector3(-.22f + i * .022f, .22f + i * .075f, .16f), new Vector3(.075f, .18f + i * .015f, .075f), "Crystal");
            crystal.transform.localRotation = Quaternion.Euler(15 + i * 7, 30 + i * 19, -25 + i * 8);
        }
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ResonatorVisual.prefab"); Object.DestroyImmediate(root);
    }

    private static void EnsureConsole()
    {
        GameObject terminal = GameObject.Find("CalibrationTerminal");
        if (terminal != null && terminal.transform.Find("终端面板装饰") == null)
        {
            var console = new GameObject("终端面板装饰"); console.transform.SetParent(terminal.transform, false);
            console.transform.localScale = new Vector3(1f / terminal.transform.lossyScale.x, 1f / terminal.transform.lossyScale.y, 1f / terminal.transform.lossyScale.z);
            Box(console.transform, "显示器外框", new Vector3(0, .2f, .34f), new Vector3(.95f, .65f, .12f), "Steel");
            Box(console.transform, "校准屏幕", new Vector3(0, .22f, .41f), new Vector3(.75f, .42f, .025f), "Energy");
            for (int i = 0; i < 4; i++)
                Box(console.transform, "控制按钮", new Vector3(-.3f + i * .2f, -.25f, .34f), new Vector3(.1f, .09f, .07f), i == 0 ? "Yellow" : "Dark");
        }
    }

    private static void BuildRoom()
    {
        GameObject root = new GameObject(Marker); Undo.RegisterCreatedObjectUndo(root, "安装实验室外观"); Transform p = root.transform;
        // Derive enclosure from the user's floor/markers, leaving their layout untouched.
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 0, 24f));
        GameObject floor = GameObject.Find("Plane");
        if (floor != null && floor.TryGetComponent(out Renderer renderer)) bounds = renderer.bounds;
        foreach (var encounter in Object.FindObjectsByType<TutorialEncounter>(FindObjectsSortMode.None))
        {
            var data = new SerializedObject(encounter);
            var points = data.FindProperty("spawnPoints");
            for (int i = 0; i < points.arraySize; i++)
                if (points.GetArrayElementAtIndex(i).objectReferenceValue is Transform t) bounds.Encapsulate(t.position);
        }
        GameObject door = GameObject.Find("TutorialExitDoor");
        GameObject zone = GameObject.Find("TutorialExitZone");
        if (door != null) bounds.Encapsulate(door.transform.position);
        if (zone != null) bounds.Encapsulate(zone.transform.position + Vector3.forward * 2f);
        float left = bounds.min.x - .5f, right = bounds.max.x + .5f;
        float back = bounds.min.z - .5f, front = bounds.max.z + 2f;
        float y = floor != null ? floor.transform.position.y : 0f;
        float cx = (left + right) * .5f, cz = (back + front) * .5f;
        Box(p, "防坠落实体地基", new Vector3(cx, y - .21f, cz), new Vector3(right - left + 1, .4f, front - back + 1), "Floor", true);
        Box(p, "左墙", new Vector3(left, y + 3, cz), new Vector3(.5f, 6, front - back + .5f), "Green", true);
        Box(p, "右墙", new Vector3(right, y + 3, cz), new Vector3(.5f, 6, front - back + .5f), "Green", true);
        Box(p, "后墙", new Vector3(cx, y + 3, back), new Vector3(right - left, 6, .5f), "Green", true);
        Box(p, "前墙", new Vector3(cx, y + 3, front), new Vector3(right - left, 6, .5f), "Green", true);
        Box(p, "顶板", new Vector3(cx, y + 6, cz), new Vector3(right - left + .5f, .4f, front - back + .5f), "Steel", true);
        if (floor != null && floor.TryGetComponent(out Renderer oldFloor)) oldFloor.sharedMaterial = Get("Floor");
        for (float z = back + 2; z < front - 1; z += 4)
        {
            foreach (float x in new[] { left + .32f, right - .32f })
            {
                Box(p, "墙体肋骨", new Vector3(x, y + 2.8f, z), new Vector3(.16f, 5.6f, .25f), "Steel");
                Box(p, "防撞警示", new Vector3(x, y + .65f, z), new Vector3(.2f, .4f, .29f), "Yellow");
            }
            Box(p, "顶梁", new Vector3(cx, y + 5.65f, z), new Vector3(right - left, .3f, .24f), "Steel");
            Box(p, "旧式灯箱", new Vector3(cx, y + 5.35f, z), new Vector3(2.6f, .09f, .4f), "Lamp");
            var lamp = new GameObject("实验室顶灯"); lamp.transform.SetParent(p);
            lamp.transform.position = new Vector3(cx, y + 4.8f, z);
            var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.range = 10f;
            light.intensity = 2.5f; light.color = new Color(.72f, .84f, .72f); light.shadows = LightShadows.None;
        }
        for (int i = 0; i < 3; i++)
        {
            var pipe = Part(p, "壁面液压管线", PrimitiveType.Cylinder,
                new Vector3(left + .42f, y + 3.5f + i * .27f, cz), new Vector3(.11f, (front - back) * .5f, .11f), "Steel");
            pipe.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        foreach (string name in new[] { "CalibrationTerminal", "GauntletStand" })
        {
            var go = GameObject.Find(name); if (go != null && go.TryGetComponent(out Renderer r)) r.sharedMaterial = Get("Green");
        }
        GameObject pickup = GameObject.Find("GauntletPickup");
        if (pickup != null)
        {
            if (pickup.TryGetComponent(out Renderer r)) r.enabled = false;
            GameObject display = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/GauntletVisual.prefab"));
            display.name = "拳套展示模型"; display.transform.SetParent(pickup.transform, false);
            // Cancel placeholder cube scaling so the model retains a sensible physical size.
            Vector3 s = pickup.transform.lossyScale;
            display.transform.localScale = new Vector3(1f / Mathf.Max(.01f, s.x), 1f / Mathf.Max(.01f, s.y), 1f / Mathf.Max(.01f, s.z));
        }
        if (door != null)
        {
            if (door.TryGetComponent(out Renderer dr)) dr.sharedMaterial = Get("Yellow");
            // Physical bulkhead closes off the room until the existing encounter opens the door.
            Bounds d = door.GetComponent<Renderer>() != null ? door.GetComponent<Renderer>().bounds : new Bounds(door.transform.position, new Vector3(3, 3, .4f));
            if (Mathf.Abs(door.transform.forward.z) > .9f)
            {
                float min = Mathf.Clamp(d.min.x, left, right), max = Mathf.Clamp(d.max.x, left, right);
                if (min > left) Box(p, "出口左隔墙", new Vector3((left + min) * .5f, y + 3, d.center.z), new Vector3(min - left, 6, .4f), "Green", true);
                if (max < right) Box(p, "出口右隔墙", new Vector3((max + right) * .5f, y + 3, d.center.z), new Vector3(right - max, 6, .4f), "Green", true);
                float top = Mathf.Max(y + 3f, d.max.y);
                if (top < y + 6) Box(p, "出口上隔墙", new Vector3(d.center.x, (top + y + 6) * .5f, d.center.z), new Vector3(d.size.x, y + 6 - top, .4f), "Steel", true);
            }
        }
    }
}
