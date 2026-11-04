using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Edit-mode install verification. Run with:
// Unity.exe -batchmode -nographics -projectPath ... -executeMethod LevelFlowVerify.VerifyBatch
public static class LevelFlowVerify
{
    private static readonly string[] Levels =
    {
        "Level1_Billiards", "Level2_Pendulum", "Level3_Seesaw",
        "Level4_MomentumRedirect", "Level5_ChainReaction", "Level6_BouncyWall",
    };
    private static readonly string[] ExpectedNext =
    {
        "Level2_Pendulum", "Level3_Seesaw", "Level4_MomentumRedirect",
        "Level5_ChainReaction", "Level6_BouncyWall", "",
    };
    private static readonly Vector3[] ExpectedAnchors =
    {
        new Vector3(12.55f, .045f, 1.5f),
        new Vector3(18.8f, 4.045f, 0f),
        new Vector3(1.25f, 18.045f, -6.3f),
        new Vector3(-2.2f, 7.645f, 14.9f),
        new Vector3(21.2f, 7.645f, -2.4f),
        new Vector3(48f, 24.045f, -65f),
    };

    private static int failures;

    [MenuItem("Tools/零界/校验六关通道与围封")]
    public static void VerifyMenu()
    {
        Verify();
    }

    public static void VerifyBatch()
    {
        try
        {
            Verify();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(2);
        }
    }

    public static void Verify()
    {
        failures = 0;
        CheckBuildSettings();
        for (int i = 0; i < Levels.Length; i++)
        {
            string path = "Assets/Scenes/" + Levels[i] + ".unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            VerifyLevel(scene, i);
        }
        Debug.Log(failures == 0
            ? "LEVELFLOW_VERIFY: PASS (all six levels)"
            : $"LEVELFLOW_VERIFY: FAIL ({failures} checks)");
    }

    private static void VerifyLevel(Scene scene, int index)
    {
        string level = Levels[index];
        Debug.Log($"--- {level} ---");
        LevelPortal portal = Find<LevelPortal>(scene);
        Expect(portal != null, $"{level}: LevelPortal 存在");
        if (portal == null) return;

        Expect(Vector3.Distance(portal.transform.position, ExpectedAnchors[index]) < .01f,
            $"{level}: 通道锚点 {portal.transform.position} (期望 {ExpectedAnchors[index]})");

        SerializedObject data = new SerializedObject(portal);
        Expect(data.FindProperty("nextSceneName").stringValue == ExpectedNext[index],
            $"{level}: nextSceneName = '{data.FindProperty("nextSceneName").stringValue}'");
        Expect(data.FindProperty("nextLevelNumber").intValue == (index == 5 ? 0 : index + 2),
            $"{level}: nextLevelNumber = {data.FindProperty("nextLevelNumber").intValue}");
        Expect(data.FindProperty("ownsTrigger").boolValue == (index >= 2),
            $"{level}: ownsTrigger = {data.FindProperty("ownsTrigger").boolValue}");
        Expect(data.FindProperty("ringMaterial").objectReferenceValue != null, $"{level}: 传送环材质");
        Expect(data.FindProperty("beamMaterial").objectReferenceValue != null, $"{level}: 光柱材质");

        if (index < 2)
        {
            PuzzleRouteProgress route = Find<PuzzleRouteProgress>(scene);
            Expect(route != null, $"{level}: PuzzleRouteProgress 存在");
            Expect(data.FindProperty("routeProgress").objectReferenceValue == route, $"{level}: 通道绑定出口管理器");
            Expect(data.FindProperty("puzzleSwitch").objectReferenceValue == null, $"{level}: 通道不绑定机关");
            if (route != null)
            {
                SerializedObject routeData = new SerializedObject(route);
                Expect(routeData.FindProperty("nextSceneName").stringValue == ExpectedNext[index],
                    $"{level}: 出口 nextSceneName = '{routeData.FindProperty("nextSceneName").stringValue}'");
            }
        }
        else
        {
            PuzzleSwitch puzzleSwitch = Find<PuzzleSwitch>(scene);
            Expect(puzzleSwitch != null, $"{level}: PuzzleSwitch 存在");
            Expect(data.FindProperty("puzzleSwitch").objectReferenceValue == puzzleSwitch, $"{level}: 通道绑定通关机关");
            Expect(data.FindProperty("routeProgress").objectReferenceValue == null, $"{level}: 通道不绑定出口管理器");
        }

        PlayerMovement player = Find<PlayerMovement>(scene);
        Expect(player != null, $"{level}: Player 存在");
        if (player != null && index >= 2)
            Expect(player.GetComponent<RoomRestartController>() != null, $"{level}: Player 有 RoomRestartController");

        Physics.SyncTransforms();
        bool ground = Physics.Raycast(ExpectedAnchors[index] + Vector3.up * .6f, Vector3.down,
            out RaycastHit hit, 6f);
        Expect(ground, $"{level}: 通道下方有碰撞体" + (ground ? $" ({hit.collider.name})" : ""));
        if (ground && hit.collider != null)
            Expect(hit.collider.GetComponentInParent<LevelPortal>() == null, $"{level}: 地面命中不是通道自身触发器");

        if (index >= 2)
        {
            GameObject enclosure = FindByName(scene, "关卡围封");
            Expect(enclosure != null, $"{level}: 关卡围封根物体");
            if (enclosure != null)
            {
                int walls = enclosure.GetComponentsInChildren<Collider>(true)
                    .Count(c => c.GetComponent<MeshRenderer>() != null && c.gameObject.name.StartsWith("围封墙_"));
                int expectedWalls = index >= 4 ? 4 : 1;
                Expect(walls >= expectedWalls, $"{level}: 围封墙数量 {walls} (期望 >= {expectedWalls})");
            }
            GameObject groundObject = FindByName(scene, "Ground");
            Expect(groundObject != null && groundObject.GetComponent<Collider>() != null,
                $"{level}: Ground 带碰撞体");
            if (groundObject != null)
            {
                Collider groundCollider = groundObject.GetComponent<Collider>();
                Bounds bounds = groundCollider.bounds;
                Vector3 anchor = ExpectedAnchors[index];
                bool coversXZ = anchor.x >= bounds.min.x && anchor.x <= bounds.max.x
                    && anchor.z >= bounds.min.z && anchor.z <= bounds.max.z;
                Expect(coversXZ, $"{level}: 通道在地面 Plane 的 XZ 范围内");
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            Expect(missing == 0, $"{level}: 无 Missing Script ({root.name}: {missing})");
        }
    }

    private static void CheckBuildSettings()
    {
        string[] expected = { "Demo_IntroCombat", "Level1_Billiards", "Level2_Pendulum",
            "Level3_Seesaw", "Level4_MomentumRedirect", "Level5_ChainReaction", "Level6_BouncyWall" };
        for (int i = 0; i < expected.Length; i++)
        {
            string path = "Assets/Scenes/" + expected[i] + ".unity";
            EditorBuildSettingsScene scene = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == path);
            Expect(scene != null && scene.enabled, $"构建列表启用 {expected[i]}");
            if (scene != null)
            {
                int index = Array.FindIndex(EditorBuildSettings.scenes, s => s.path == path);
                Expect(index == i, $"构建列表顺序 {expected[i]} = {index}");
            }
        }
    }

    private static void Expect(bool condition, string message)
    {
        if (condition) Debug.Log("PASS: " + message);
        else
        {
            failures++;
            Debug.LogError("FAIL: " + message);
        }
    }

    private static GameObject FindByName(Scene scene, string name)
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
