using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// 用通用组件从零搭建三个新关卡（可重复执行；同名场景会被覆盖）。
/// 菜单：Tools > Levels > 一键重建三个关卡
///
/// 关卡链路：Level1_PunchThrough → Level2_Combat → Level3_Billiards（最终关）
/// 每个关卡只放"可配置组件 + 基本体块"，所有空间数值集中在各 Build 方法里，方便微调。
/// </summary>
public static class NewLevelBuilder
{
    const string SceneFolder = "Assets/Scenes";
    const string MatFolder = "Assets/Resources/DemoPresentation/";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string EnemyPrefabPath = "Assets/Resonator/AI/Prefabs/ResonatorAgent.prefab";

    const string Level1 = "Level1_PunchThrough";
    const string Level2 = "Level2_Combat";
    const string Level3 = "Level3_Billiards";

    [MenuItem("Tools/Levels/一键重建三个关卡", false, 1)]
    public static void BuildAll()
    {
        BuildLevel1();
        BuildLevel2();
        BuildLevel3();
        ApplyBuildSettings();
        Debug.Log("[NewLevelBuilder] 三个关卡已重建并写入 Build Settings。");
    }

    [MenuItem("Tools/Levels/只重建 1 拾拳套 → 轰开方块", false, 20)]
    public static void BuildLevel1()
    {
        if (!BeginScene()) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting(1.15f);
        Transform env = Group("Environment");
        Transform game = Group("Gameplay");

        // 房间 20 x 30
        Floor(env, new Vector3(0f, -0.5f, 3f), new Vector2(20f, 30f), "Floor");
        Wall(env, new Vector3(-10.3f, 2f, 3f), new Vector3(0.6f, 4f, 30f), "Wall_West");
        Wall(env, new Vector3(10.3f, 2f, 3f), new Vector3(0.6f, 4f, 30f), "Wall_East");
        Wall(env, new Vector3(0f, 2f, -12.3f), new Vector3(20.6f, 4f, 0.6f), "Wall_South");
        Wall(env, new Vector3(0f, 2f, 18.3f), new Vector3(20.6f, 4f, 0.6f), "Wall_North");

        // 隔断墙：中间 1.6 米宽的通道被动能方块堵住
        Wall(env, new Vector3(-5.4f, 1.5f, 2.5f), new Vector3(9.2f, 3f, 0.4f), "Barrier_West");
        Wall(env, new Vector3(5.4f, 1.5f, 2.5f), new Vector3(9.2f, 3f, 0.4f), "Barrier_East");
        Wall(env, new Vector3(0f, 3.2f, 2.5f), new Vector3(1.6f, 1.6f, 0.4f), "Barrier_Lintel");

        // 挡路的动能方块（一拳轰开）
        GameObject box = Cube(game, "BlockingBox", new Vector3(0f, 0.5f, 2.5f), 1f, Mat("Yellow"));
        MakeKineticBox(box);

        // 拳套拾取台
        Cube(game, "GauntletStand", new Vector3(-4f, 0.4f, -7f), 1f, Mat("Steel"));
        GameObject pickup = Cube(game, "GauntletPickup", new Vector3(-4f, 1.05f, -7f), 0.5f, Mat("Energy"));
        pickup.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        pickup.AddComponent<GauntletPickup>();

        // 出口
        GameObject goal = Cube(env, "GoalMarker", new Vector3(0f, 1.5f, 16.8f), 3f, Mat("Energy"));
        goal.transform.localScale = new Vector3(6f, 3f, 0.4f);
        MakeExit(game, "ExitZone", new Vector3(0f, 1.5f, 14f), new Vector3(5f, 3f, 2f), Level2, null);

        Player(game, new Vector3(0f, 0.1f, -9f), 0f, equipped: false, energy: 3);
        Save(scene, Level1);
    }

    [MenuItem("Tools/Levels/只重建 2 战斗", false, 21)]
    public static void BuildLevel2()
    {
        if (!BeginScene()) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting(1.1f);
        Transform env = Group("Environment");
        Transform game = Group("Gameplay");

        Floor(env, new Vector3(0f, -0.5f, 0f), new Vector2(30f, 30f), "Floor");
        Wall(env, new Vector3(-15.3f, 2.5f, 0f), new Vector3(0.6f, 5f, 30f), "Wall_West");
        Wall(env, new Vector3(15.3f, 2.5f, 0f), new Vector3(0.6f, 5f, 30f), "Wall_East");
        Wall(env, new Vector3(0f, 2.5f, -15.3f), new Vector3(30.6f, 5f, 0.6f), "Wall_South");
        Wall(env, new Vector3(0f, 2.5f, 15.3f), new Vector3(30.6f, 5f, 0.6f), "Wall_North");

        // 给敌人一点遮挡物
        foreach (Vector3 p in new[] { new Vector3(-6f, 2.5f, -2f), new Vector3(6f, 2.5f, -2f), new Vector3(-6f, 2.5f, 6f), new Vector3(6f, 2.5f, 6f) })
            Wall(env, p, new Vector3(2f, 5f, 2f), "Pillar");

        // 清场后才会消失的门 + 出口
        GameObject door = Cube(env, "CombatExitDoor", new Vector3(0f, 2f, 12f), 1f, Mat("Green"));
        door.transform.localScale = new Vector3(6f, 4f, 0.5f);

        GameObject encounterObject = new GameObject("CombatEncounter");
        encounterObject.transform.SetParent(game);
        ResonatorEncounter encounter = encounterObject.AddComponent<ResonatorEncounter>();

        Transform spawnRoot = new GameObject("EnemySpawnPoints").transform;
        spawnRoot.SetParent(game);
        spawnRoot.position = new Vector3(0f, 0f, 9f);
        Transform[] spawns = new Transform[3];
        Vector3[] spawnPositions = { new Vector3(-9f, 0f, 8f), new Vector3(0f, 0f, 10f), new Vector3(9f, 0f, 8f) };
        for (int i = 0; i < spawns.Length; i++)
        {
            Transform point = new GameObject("EnemySpawn0" + (i + 1)).transform;
            point.SetParent(spawnRoot);
            point.position = spawnPositions[i];
            spawns[i] = point;
        }

        MakeExit(game, "ExitZone", new Vector3(0f, 1.5f, 13.5f), new Vector3(5f, 3f, 2f), Level3, encounter);

        GameObject player = Player(game, new Vector3(0f, 0.1f, -12f), 0f, equipped: true, energy: 2);

        // 遭遇战配置
        SerializedObject so = new SerializedObject(encounter);
        so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerCombatController>();
        so.FindProperty("equipment").objectReferenceValue = player.GetComponent<PlayerGauntletEquipment>();
        so.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        SerializedProperty points = so.FindProperty("spawnPoints");
        points.arraySize = spawns.Length;
        for (int i = 0; i < spawns.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
        so.FindProperty("exitDoor").objectReferenceValue = door;
        so.FindProperty("enemyCount").intValue = spawns.Length;
        so.ApplyModifiedPropertiesWithoutUndo();

        BakeNavMesh(scene, Level2);
        Save(scene, Level2);
    }

    [MenuItem("Tools/Levels/只重建 3 台球解谜", false, 22)]
    public static void BuildLevel3()
    {
        if (!BeginScene()) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting(1.05f);
        Transform env = Group("Environment");
        Transform game = Group("Gameplay");

        Floor(env, new Vector3(0f, -0.5f, 0f), new Vector2(24f, 28f), "Floor");
        Wall(env, new Vector3(-12.3f, 2f, 0f), new Vector3(0.6f, 4f, 28f), "Wall_West");
        Wall(env, new Vector3(12.3f, 2f, 0f), new Vector3(0.6f, 4f, 28f), "Wall_East");
        Wall(env, new Vector3(0f, 2f, -14.3f), new Vector3(24.6f, 4f, 0.6f), "Wall_South");
        Wall(env, new Vector3(0f, 2f, 14.3f), new Vector3(24.6f, 4f, 0.6f), "Wall_North");

        // 隔断墙 + 只容方块通过的窄缝（1.9 x 1.25）
        Wall(env, new Vector3(-6.475f, 1.5f, 2f), new Vector3(11.05f, 3f, 0.4f), "GapWall_West");
        Wall(env, new Vector3(6.475f, 1.5f, 2f), new Vector3(11.05f, 3f, 0.4f), "GapWall_East");
        Wall(env, new Vector3(0f, 2.125f, 2f), new Vector3(1.9f, 1.75f, 0.4f), "GapWall_Lintel");

        // 定位挡条：把方块横向顶到这里，中心正好落在窄缝中线上
        Wall(env, new Vector3(0.7f, 0.5f, -3f), new Vector3(0.4f, 1f, 4f), "AlignCurb");

        // 目标方块
        GameObject box = Cube(game, "PuzzleBox", new Vector3(-3.5f, 0.5f, -3f), 1f, Mat("Yellow"));
        MakeKineticBox(box);

        // 充能晶体（初始 0 能量，必须先来这里）
        GameObject crystal = Cube(game, "DashCrystal", new Vector3(-9f, 1.2f, -8f), 0.5f, Mat("Crystal"));
        crystal.transform.localScale = new Vector3(0.45f, 0.8f, 0.45f);
        crystal.AddComponent<DashCrystal>();

        // 门 + 门后出口（门由开关抬起）
        Wall(env, new Vector3(-5.8f, 2f, -12.5f), new Vector3(0.4f, 4f, 3f), "AlcoveWest");
        Wall(env, new Vector3(-2.2f, 2f, -12.5f), new Vector3(0.4f, 4f, 3f), "AlcoveEast");
        GameObject door = Cube(env, "PuzzleExitDoor", new Vector3(-4f, 1.5f, -11f), 1f, Mat("Green"));
        door.transform.localScale = new Vector3(3f, 3f, 0.4f);

        // 开关（在窄缝另一侧，被方块撞到就激活）
        GameObject switchObject = Cube(game, "PuzzleSwitch", new Vector3(0f, 0.6f, 7f), 1f, Mat("Steel"));
        switchObject.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
        switchObject.GetComponent<BoxCollider>().isTrigger = true;
        PuzzleSwitch puzzleSwitch = switchObject.AddComponent<PuzzleSwitch>();

        MakeExit(game, "ExitZone", new Vector3(-4f, 1.5f, -12.8f), new Vector3(2.4f, 2.6f, 1.4f), string.Empty, puzzleSwitch);

        SerializedObject so = new SerializedObject(puzzleSwitch);
        so.FindProperty("doorOrIndicatorToMove").objectReferenceValue = door.transform;
        so.FindProperty("activatedOffset").vector3Value = new Vector3(0f, 3.4f, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();

        Player(game, new Vector3(-9f, 0.1f, -10f), 0f, equipped: true, energy: 0);
        Save(scene, Level3);
    }

    [MenuItem("Tools/Levels/写入 Build Settings（三个关卡）", false, 40)]
    public static void ApplyBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SceneFolder + "/" + Level1 + ".unity", true),
            new EditorBuildSettingsScene(SceneFolder + "/" + Level2 + ".unity", true),
            new EditorBuildSettingsScene(SceneFolder + "/" + Level3 + ".unity", true)
        };
    }

    // ---------- 通用搭建工具 ----------

    private static bool BeginScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[NewLevelBuilder] 请先退出播放模式。"); return false; }
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[NewLevelBuilder] 当前场景有未保存修改，请先保存后再执行。");
            return false;
        }
        return true;
    }

    private static Material Mat(string name)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + name + ".mat");
        if (mat == null) Debug.LogWarning("[NewLevelBuilder] 缺少材质 " + MatFolder + name + ".mat");
        return mat;
    }

    private static Transform Group(string name) => new GameObject(name).transform;

    private static void SetupLighting(float intensity)
    {
        GameObject light = new GameObject("Directional Light");
        Light component = light.AddComponent<Light>();
        component.type = LightType.Directional;
        component.intensity = intensity;
        component.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.42f);
        RenderSettings.fog = false;
    }

    private static GameObject Floor(Transform parent, Vector3 center, Vector2 size, string name)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, 1f, size.y);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = Mat("Floor");
        return go;
    }

    private static GameObject Wall(Transform parent, Vector3 center, Vector3 size, string name)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = size;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = Mat("Dark");
        return go;
    }

    private static GameObject Cube(Transform parent, string name, Vector3 center, float size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = Vector3.one * size;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        return go;
    }

    /// <summary>动能方块：自动挂 Rigidbody/BoxCollider，并接上会发光的特殊状态材质。</summary>
    private static void MakeKineticBox(GameObject go)
    {
        KineticBox box = go.AddComponent<KineticBox>();
        Material state = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KineticBox_State.mat");
        if (state == null) return;
        SerializedObject so = new SerializedObject(box);
        SerializedProperty material = so.FindProperty("stateMaterial");
        if (material != null) material.objectReferenceValue = state;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Player(Transform parent, Vector3 position, float yaw, bool equipped, int energy)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[NewLevelBuilder] 找不到 " + PlayerPrefabPath);
            return null;
        }
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        player.name = "Player";
        player.transform.SetParent(parent);
        player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        PlayerGauntletEquipment equipment = player.GetComponent<PlayerGauntletEquipment>();
        if (equipment != null)
        {
            SerializedObject so = new SerializedObject(equipment);
            so.FindProperty("startsEquipped").boolValue = equipped;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PlayerChargedPunch punch = player.GetComponent<PlayerChargedPunch>();
        if (punch != null)
        {
            SerializedObject so = new SerializedObject(punch);
            so.FindProperty("initialEnergy").intValue = Mathf.Clamp(energy, 0, 3);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return player;
    }

    private static GameObject MakeExit(Transform parent, string name, Vector3 center, Vector3 size, string nextScene, MonoBehaviour gate)
    {
        GameObject exit = Cube(parent, name, center, 1f, null);
        exit.transform.localScale = size;
        exit.GetComponent<BoxCollider>().isTrigger = true;
        LevelExit levelExit = exit.AddComponent<LevelExit>();
        SerializedObject so = new SerializedObject(levelExit);
        so.FindProperty("nextSceneName").stringValue = nextScene;
        if (gate != null)
        {
            SerializedProperty gates = so.FindProperty("gates");
            gates.arraySize = 1;
            gates.GetArrayElementAtIndex(0).objectReferenceValue = gate;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Renderer renderer = exit.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        return exit;
    }

    private static void BakeNavMesh(Scene scene, string sceneName)
    {
        GameObject nav = new GameObject("Navigation");
        NavMeshSurface surface = nav.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string path = AssetDatabase.GenerateUniqueAssetPath(SceneFolder + "/" + sceneName + "_NavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(nav);
    }

    private static void Save(Scene scene, string sceneName)
    {
        string path = SceneFolder + "/" + sceneName + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        Debug.Log("[NewLevelBuilder] 已生成 " + path);
    }
}
