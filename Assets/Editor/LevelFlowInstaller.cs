using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One-click installer for the six-level chain. It only adds the shared flow
// objects, the walk-in portals and the missing enclosure walls; puzzle logic
// stays in the existing scripts.
public static class LevelFlowInstaller
{
    private const string ScenesFolder = "Assets/Scenes/";
    private const string MaterialsFolder = "Assets/Resources/DemoPresentation/";
    private static readonly string[] LevelScenes =
    {
        "Level1_Billiards",
        "Level2_Pendulum",
        "Level3_Seesaw",
        "Level4_MomentumRedirect",
        "Level5_ChainReaction",
        "Level6_BouncyWall",
    };

    private struct Setup
    {
        public string Scene;
        public string NextScene;
        public int NextLevelNumber;
        public Vector3 Anchor;
        public bool OwnsTrigger;
        public string Hint;
    }

    private static readonly Setup[] Setups =
    {
        new Setup { Scene = "Level1_Billiards", NextScene = "Level2_Pendulum", NextLevelNumber = 2,
            Anchor = new Vector3(12.55f, .045f, 1.5f), OwnsTrigger = false,
            Hint = "通道已开启！走入右侧光柱进入第 2 关。" },
        new Setup { Scene = "Level2_Pendulum", NextScene = "Level3_Seesaw", NextLevelNumber = 3,
            Anchor = new Vector3(18.8f, 4.045f, 0f), OwnsTrigger = false,
            Hint = "通道已开启！走入光柱进入第 3 关。" },
        new Setup { Scene = "Level3_Seesaw", NextScene = "Level4_MomentumRedirect", NextLevelNumber = 4,
            Anchor = new Vector3(1.25f, 18.045f, -6.3f), OwnsTrigger = true, Hint = "" },
        new Setup { Scene = "Level4_MomentumRedirect", NextScene = "Level5_ChainReaction", NextLevelNumber = 5,
            Anchor = new Vector3(-2.2f, 7.645f, 14.9f), OwnsTrigger = true, Hint = "" },
        new Setup { Scene = "Level5_ChainReaction", NextScene = "Level6_BouncyWall", NextLevelNumber = 6,
            Anchor = new Vector3(21.2f, 7.645f, -2.4f), OwnsTrigger = true, Hint = "" },
        new Setup { Scene = "Level6_BouncyWall", NextScene = "", NextLevelNumber = 0,
            Anchor = new Vector3(48f, 24.045f, -65f), OwnsTrigger = true, Hint = "" },
    };

    [MenuItem("Tools/零界/安装六关通道与围封")]
    public static void InstallMenu()
    {
        Install();
    }

    [MenuItem("Tools/零界/创建通道光柱材质")]
    public static void CreatePortalBeamMaterialMenu()
    {
        CreatePortalBeamMaterial();
        AssetDatabase.SaveAssets();
    }

    public static void CreatePortalBeamMaterialBatch()
    {
        try
        {
            CreatePortalBeamMaterial();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // Create a real Resources material so the URP Unlit shader is included in
    // player builds instead of relying on a runtime Shader.Find.
    public static Material CreatePortalBeamMaterial()
    {
        string path = MaterialsFolder + "PortalBeam.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("找不到 URP Unlit Shader，无法创建通道光柱材质。");
            return null;
        }
        Material material = new Material(shader) { name = "PortalBeam" };
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetColor("_BaseColor", new Color(.28f, .95f, .76f, .18f));
        material.SetColor("_Color", new Color(.28f, .95f, .76f, .18f));
        material.SetColor("_EmissionColor", new Color(.45f, 1.8f, 1.4f, 1f));
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        Debug.Log("已创建通道光柱材质：" + path);
        return material;
    }

    // Batch entry point: Unity.exe -batchmode -projectPath ... -executeMethod LevelFlowInstaller.InstallBatch
    public static void InstallBatch()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请先退出播放模式，再安装六关通道。");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string backup = "Library/LevelFlowBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy("ProjectSettings/EditorBuildSettings.asset", backup + "/EditorBuildSettings.asset", true);
        foreach (string name in LevelScenes)
        {
            string path = ScenesFolder + name + ".unity";
            if (File.Exists(path)) File.Copy(path, backup + "/" + name + ".unity", true);
        }
        AssetDatabase.Refresh();

        Material green = Mat("Green");
        Material steel = Mat("Steel");
        Material energy = Mat("Energy");
        Material mist = Mat("Mist");
        Material floor = Mat("Floor");
        Material dark = Mat("Dark");
        if (green == null || steel == null || energy == null || mist == null || floor == null || dark == null)
        {
            Debug.LogError("缺少 DemoPresentation 占位材质，安装中止。请检查 " + MaterialsFolder);
            return;
        }

        int portalCount = 0;
        foreach (Setup setup in Setups)
        {
            string path = ScenesFolder + setup.Scene + ".unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            PuzzleSwitch puzzleSwitch = Find<PuzzleSwitch>(scene);
            PuzzleRouteProgress route = Find<PuzzleRouteProgress>(scene);
            PlayerMovement player = Find<PlayerMovement>(scene);
            if (player != null && player.GetComponent<RoomRestartController>() == null)
                Undo.AddComponent<RoomRestartController>(player.gameObject);

            if (route != null)
            {
                SerializedObject routeData = new SerializedObject(route);
                if (!string.IsNullOrEmpty(setup.NextScene))
                    routeData.FindProperty("nextSceneName").stringValue = setup.NextScene;
                if (!string.IsNullOrEmpty(setup.Hint))
                    routeData.FindProperty("exitHint").stringValue = setup.Hint;
                routeData.ApplyModifiedPropertiesWithoutUndo();
            }

            LevelPortal portal = Find<LevelPortal>(scene);
            if (portal == null)
            {
                GameObject portalObject = new GameObject("关卡通道");
                Undo.RegisterCreatedObjectUndo(portalObject, "创建关卡通道");
                portal = Undo.AddComponent<LevelPortal>(portalObject);
            }
            portal.transform.position = setup.Anchor;
            portal.transform.rotation = Quaternion.identity;

            SerializedObject portalData = new SerializedObject(portal);
            portalData.FindProperty("puzzleSwitch").objectReferenceValue = setup.OwnsTrigger ? puzzleSwitch : null;
            portalData.FindProperty("routeProgress").objectReferenceValue = setup.OwnsTrigger ? null : route;
            portalData.FindProperty("nextSceneName").stringValue = setup.NextScene;
            portalData.FindProperty("nextLevelNumber").intValue = setup.NextLevelNumber;
            portalData.FindProperty("ownsTrigger").boolValue = setup.OwnsTrigger;
            portalData.FindProperty("ringMaterial").objectReferenceValue = energy;
            portalData.FindProperty("beamMaterial").objectReferenceValue = mist;
            portalData.ApplyModifiedPropertiesWithoutUndo();

            Physics.SyncTransforms();
            bool groundHit = Physics.Raycast(setup.Anchor + Vector3.up * .6f, Vector3.down, out RaycastHit hit, 6f);
            if (!groundHit)
                Debug.LogWarning($"{setup.Scene}: 通道锚点 {setup.Anchor} 下方没有检测到地面碰撞体，请检查。");
            if (setup.OwnsTrigger && puzzleSwitch == null)
                Debug.LogError($"{setup.Scene}: 找不到 PuzzleSwitch，通道无法开启。");

            if (setup.Scene != "Level1_Billiards" && setup.Scene != "Level2_Pendulum")
                BuildEnclosure(scene, setup.Scene, green, steel, energy, floor, dark);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            portalCount++;
            Debug.Log($"{setup.Scene}: 通道已配置，锚点 {setup.Anchor}，下一场景 '{setup.NextScene}'，地面命中={groundHit}。");
        }

        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText("Library/LevelFlowInstalled.txt",
            "Installed " + DateTime.Now.ToString("O") + "\nBackup: " + backup + "\nPortals: " + portalCount);
        Debug.Log($"六关通道安装完成：{portalCount} 个通道。备份位于 {backup}。");
    }

    private static void BuildEnclosure(Scene scene, string sceneName, Material green, Material steel,
        Material energy, Material floor, Material dark)
    {
        GameObject previous = Find(scene, "关卡围封");
        if (previous != null) Object.DestroyImmediate(previous);

        GameObject root = new GameObject("关卡围封");
        Undo.RegisterCreatedObjectUndo(root, "创建关卡围封");
        Transform parent = root.transform;
        Vector3 roomCenter;

        if (sceneName == "Level3_Seesaw")
        {
            roomCenter = new Vector3(0f, 0f, -.5f);
            AddWall(parent, "围封墙_南", new Vector3(0f, 12f, -11.35f), new Vector3(38f, 24f, 1f),
                roomCenter, green, steel, energy, dark);
        }
        else if (sceneName == "Level4_MomentumRedirect")
        {
            roomCenter = new Vector3(0f, 0f, 5.5f);
            AddWall(parent, "围封墙_南", new Vector3(0f, 12f, -12.35f), new Vector3(38f, 24f, 1f),
                roomCenter, green, steel, energy, dark);
        }
        else if (sceneName == "Level5_ChainReaction")
        {
            roomCenter = new Vector3(5.5f, 0f, -5f);
            AddWall(parent, "围封墙_南", new Vector3(5.5f, 11f, -13.5f), new Vector3(42f, 22f, 1f),
                roomCenter, green, steel, energy, dark);
            AddWall(parent, "围封墙_北", new Vector3(5.5f, 11f, 3.5f), new Vector3(42f, 22f, 1f),
                roomCenter, green, steel, energy, dark);
            AddWall(parent, "围封墙_西", new Vector3(-15.5f, 11f, -5f), new Vector3(1f, 22f, 17f),
                roomCenter, green, steel, energy, dark);
            AddWall(parent, "围封墙_东", new Vector3(26.5f, 11f, -5f), new Vector3(1f, 22f, 17f),
                roomCenter, green, steel, energy, dark);
            ResizeGround(scene, new Vector3(5.5f, 0f, -5f), new Vector3(4.4f, 1f, 1.9f), floor);
        }
        else if (sceneName == "Level6_BouncyWall")
        {
            roomCenter = new Vector3(-1.5f, 0f, -33f);
            AddWall(parent, "围封墙_南", new Vector3(-1.5f, 17f, -82.5f), new Vector3(155f, 34f, 1f),
                roomCenter, green, steel, energy, dark);
            AddWall(parent, "围封墙_北", new Vector3(-1.5f, 17f, 16.5f), new Vector3(155f, 34f, 1f),
                roomCenter, green, steel, energy, dark);
            AddWall(parent, "围封墙_西", new Vector3(-78.5f, 17f, -33f), new Vector3(1f, 34f, 99f),
                roomCenter, green, steel, energy, dark);
            AddWall(parent, "围封墙_东", new Vector3(75.5f, 17f, -33f), new Vector3(1f, 34f, 99f),
                roomCenter, green, steel, energy, dark);
            ResizeGround(scene, new Vector3(-1.5f, 0f, -33f), new Vector3(15.7f, 1f, 10.1f), floor);
        }
        else
        {
            Object.DestroyImmediate(root);
            return;
        }

        Physics.SyncTransforms();
        Debug.Log($"{sceneName}: 围封已生成。");
    }

    private static void ResizeGround(Scene scene, Vector3 position, Vector3 scale, Material floorMaterial)
    {
        GameObject ground = Find(scene, "Ground");
        if (ground == null)
        {
            Debug.LogWarning("找不到 Ground，跳过地面缩放。");
            return;
        }
        ground.transform.position = position;
        ground.transform.localScale = scale;
        MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
        if (renderer != null && floorMaterial != null) renderer.sharedMaterial = floorMaterial;
    }

    private static void AddWall(Transform parent, string name, Vector3 center, Vector3 size, Vector3 roomCenter,
        Material wallMaterial, Material capMaterial, Material stripMaterial, Material baseMaterial)
    {
        Cube(parent, name, center, size, wallMaterial, true);
        Cube(parent, name + "_顶沿", new Vector3(center.x, center.y + size.y * .5f + .25f, center.z),
            new Vector3(size.x + .3f, .5f, size.z + .3f), capMaterial, false);
        Cube(parent, name + "_基座", new Vector3(center.x, .3f, center.z),
            new Vector3(size.x + .25f, .6f, size.z + .25f), baseMaterial, false);

        if (size.z > size.x)
        {
            float direction = Mathf.Sign(roomCenter.x - center.x);
            Cube(parent, name + "_能源灯带",
                new Vector3(center.x + direction * (size.x * .5f + .025f), 2.35f, center.z),
                new Vector3(.06f, .16f, size.z * .92f), stripMaterial, false);
        }
        else
        {
            float direction = Mathf.Sign(roomCenter.z - center.z);
            Cube(parent, name + "_能源灯带",
                new Vector3(center.x, 2.35f, center.z + direction * (size.z * .5f + .025f)),
                new Vector3(size.x * .92f, .16f, .06f), stripMaterial, false);
        }
    }

    private static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size,
        Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        if (!keepCollider)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }
        return go;
    }

    private static void ConfigureBuildSettings()
    {
        string[] ordered = new[]
        {
            "Demo_IntroCombat",
            "Level1_Billiards",
            "Level2_Pendulum",
            "Level3_Seesaw",
            "Level4_MomentumRedirect",
            "Level5_ChainReaction",
            "Level6_BouncyWall",
        };
        var scenes = ordered
            .Select(name => new EditorBuildSettingsScene(ScenesFolder + name + ".unity", true))
            .ToList();
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (!scenes.Any(scene => scene.path == existing.path))
                scenes.Add(new EditorBuildSettingsScene(existing.path, false));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("构建场景列表已更新：教学关 + 六关全部启用。");
    }

    private static Material Mat(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + name + ".mat");
    }

    private static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform.gameObject;
        return null;
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
