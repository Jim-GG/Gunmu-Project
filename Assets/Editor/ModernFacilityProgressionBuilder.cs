using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>在保留的 MomentumRedirect 灰盒上，装配第一现代关完整流程与场景跳转。</summary>
public static class ModernFacilityProgressionBuilder
{
    private const string FirstScene = "Assets/Scenes/Modern_01_DroneEscape.unity";
    private const string SecondScene = "Assets/Scenes/Modern_02_CrystalTraversal.unity";
    private const string MomentumRootName = "RedirectPuzzle_Level4";
    private const float FloorY = 7.6f;
    private const float PuzzleOffsetZ = 90f;

    [MenuItem("零界/现代关卡/修复共鸣者生成引用")]
    public static void RepairResonatorEncounter()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != FirstScene || EditorApplication.isPlaying)
            throw new System.InvalidOperationException("请在停止运行的第一现代关内执行修复。");
        GameObject player = GameObject.Find("Player");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");
        if (!player || !player.GetComponent<PlayerCombatController>() || !prefab || !prefab.GetComponent<Resonator.AI.ResonatorEnemy>())
            throw new System.InvalidOperationException("玩家或完整共鸣者预制体缺失，未修改场景。");
        var target = player.GetComponent<Resonator.AI.ResonatorTarget>();
        if (!target) target = Undo.AddComponent<Resonator.AI.ResonatorTarget>(player);
        target.combat = player.GetComponent<PlayerCombatController>();
        target.eyes = player.GetComponentInChildren<Camera>(true)?.transform;
        EditorUtility.SetDirty(target);
        GameObject floor = GameObject.Find("共鸣者交战室地面");
        if (!floor || !floor.GetComponent<Collider>()) throw new System.InvalidOperationException("找不到交战室地面。");
        Physics.SyncTransforms();
        float spawnHeight = floor.GetComponent<Collider>().bounds.max.y + .05f;
        var encounters = Object.FindObjectsByType<ResonatorEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(e => e.gameObject.scene == scene).ToArray();
        if (encounters.Length == 0) throw new System.InvalidOperationException("场景中没有共鸣者遭遇组件。");
        foreach (var encounter in encounters)
        {
            var data = new SerializedObject(encounter);
            data.FindProperty("enemyPrefab").objectReferenceValue = prefab;
            data.FindProperty("player").objectReferenceValue = target.combat;
            data.FindProperty("equipment").objectReferenceValue = player.GetComponent<PlayerGauntletEquipment>();
            var points = data.FindProperty("spawnPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (!point) throw new System.InvalidOperationException("共鸣者出生点为空。");
                Vector3 position = point.position;
                position.y = spawnHeight;
                point.position = position;
                EditorUtility.SetDirty(point);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            encounter.enabled = true;
            encounter.gameObject.SetActive(true);
        }
        var surface = GameObject.Find("Modern Facility Progression").GetComponent<NavMeshSurface>();
        if (surface)
        {
            surface.BuildNavMesh();
            if (surface.navMeshData && !AssetDatabase.Contains(surface.navMeshData))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes/ModernNavigation"))
                    AssetDatabase.CreateFolder("Assets/Scenes", "ModernNavigation");
                AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ModernNavigation/Modern01.asset"));
            }
            EditorUtility.SetDirty(surface);
        }
        foreach (var encounter in encounters)
        {
            var points = new SerializedObject(encounter).FindProperty("spawnPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = (Transform)points.GetArrayElementAtIndex(i).objectReferenceValue;
                if (!NavMesh.SamplePosition(point.position, out var hit, 1f, NavMesh.AllAreas))
                    throw new System.InvalidOperationException("出生点没有可用导航网格：" + point.name);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("场景保存失败。");
        AssetDatabase.SaveAssets();
        Debug.Log("MODERN_RESONATOR_REPAIR_OK：完整 AI 预制体、玩家血量目标、地面出生点与导航网格已修复并保存。");
    }

    [MenuItem("零界/现代关卡/修复玩家战斗和拳套")]
    public static void RepairPlayerCombat()
    {
        Scene scene = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
        GameObject oldPlayer = GameObject.Find("Player");
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (!oldPlayer || !playerPrefab)
        {
            Debug.LogError("找不到第一关现有 Player 或 Assets/Prefabs/Player.prefab，未修改场景。", oldPlayer);
            return;
        }

        Vector3 spawnPosition = oldPlayer.transform.position;
        Quaternion spawnRotation = oldPlayer.transform.rotation;
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = "Player";
        player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        PlayerGauntletEquipment equipment = player.GetComponent<PlayerGauntletEquipment>();
        SerializedObject equipmentData = new SerializedObject(equipment);
        equipmentData.FindProperty("startsEquipped").boolValue = true;
        equipmentData.ApplyModifiedPropertiesWithoutUndo();

        PlayerChargedPunch punch = player.GetComponent<PlayerChargedPunch>();
        SerializedObject punchData = new SerializedObject(punch);
        punchData.FindProperty("initialEnergy").intValue = 3;
        punchData.ApplyModifiedPropertiesWithoutUndo();

        RoomRestartController restart = player.GetComponent<RoomRestartController>();
        if (restart)
        {
            SerializedObject restartData = new SerializedObject(restart);
            restartData.FindProperty("fallDistance").floatValue = 22f;
            restartData.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerCombatController health = player.GetComponent<PlayerCombatController>();
        if (health)
        {
            SerializedObject healthData = new SerializedObject(health);
            healthData.FindProperty("maxHealth").intValue = 100;
            healthData.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (ModernCombatDrone drone in Object.FindObjectsByType<ModernCombatDrone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            drone.targetPlayer = health;
            drone.playerMovement = player.GetComponent<PlayerMovement>();
            drone.playerLook = player.GetComponent<PlayerLook>();
            drone.playerPunch = punch;
            drone.playerCamera = player.GetComponentInChildren<Camera>(true)?.transform;
            EditorUtility.SetDirty(drone);
        }
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");
        foreach (ResonatorEncounter encounter in Object.FindObjectsByType<ResonatorEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject encounterData = new SerializedObject(encounter);
            encounterData.FindProperty("player").objectReferenceValue = health;
            encounterData.FindProperty("equipment").objectReferenceValue = equipment;
            if (enemyPrefab) encounterData.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            encounterData.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.DestroyObjectImmediate(oldPlayer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("玩家已换成苏联关共用 Player 完整预制体：100 点生命、拳套模型、开局装备、三格冲量、格挡和出拳系统；敌人与无人机已重新绑定。", player);
    }

    [MenuItem("零界/现代关卡/修复第一关无人机与封闭舱室")]
    public static void RepairFirstDroneAndRoom()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != FirstScene)
            scene = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        Transform progression = GameObject.Find("Modern Facility Progression")?.transform;
        if (!player || !progression)
        {
            Debug.LogError("找不到第一关玩家或流程根物件，未修改场景。", player);
            return;
        }

        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ModernFacility/ModernMetal.mat");
        Material edge = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ModernFacility/ModernEdge.mat");
        // Seal the starter bay overhead. The existing vent remains the intended route out.
        EnsureRoomCube(progression, "起始舱室 / 封闭顶板", new Vector3(0, 20.25f, 5), new Vector3(14, .5f, 28), metal);

        ModernCombatDrone[] drones = Object.FindObjectsByType<ModernCombatDrone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ModernCombatDrone firstDrone = null;
        foreach (ModernCombatDrone drone in drones)
        {
            if (!drone) continue;
            drone.targetPlayer = player.GetComponent<PlayerCombatController>();
            drone.playerMovement = player.GetComponent<PlayerMovement>();
            drone.playerLook = player.GetComponent<PlayerLook>();
            drone.playerPunch = player.GetComponent<PlayerChargedPunch>();
            drone.playerCamera = player.GetComponentInChildren<Camera>(true)?.transform;

            Transform visual = drone.transform.Find("MCU7A 无人机模型");
            if (visual) visual.localScale = Vector3.one * .7f;
            SphereCollider sphere = drone.GetComponent<SphereCollider>();
            if (sphere) sphere.radius = .5f;
            drone.attackAltitude = 4f;
            drone.diveSpeed = 8f;
            drone.attackWindup = .9f;
            drone.detectionRange = 16f;

            if (drone.gameObject.name == "攻击型巡逻无人机") firstDrone = drone;
            EditorUtility.SetDirty(drone);
        }

        if (firstDrone)
        {
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            firstDrone.transform.position = player.transform.position + forward * 3.1f + Vector3.up * 1.45f;
            firstDrone.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            Rigidbody body = firstDrone.GetComponent<Rigidbody>();
            if (body)
            {
                body.position = firstDrone.transform.position;
                body.rotation = firstDrone.transform.rotation;
            }

            Transform encounterRoot = firstDrone.transform.parent;
            Transform patrolA = encounterRoot ? encounterRoot.Find("无人机巡逻起点") : null;
            Transform patrolB = encounterRoot ? encounterRoot.Find("无人机巡逻终点") : null;
            if (patrolA) patrolA.position = firstDrone.transform.position + Vector3.left * 1.2f;
            if (patrolB) patrolB.position = firstDrone.transform.position + Vector3.right * 1.2f;

            // This invisible collision seal contains a punched drone; piloting temporarily opens it.
            GameObject gate = GameObject.Find("起始舱室 / 无人机防飞出碰撞");
            if (!gate)
            {
                gate = new GameObject("起始舱室 / 无人机防飞出碰撞");
                gate.transform.SetParent(progression, true);
                gate.transform.position = new Vector3(0, 16.05f, 19f);
                BoxCollider collider = gate.AddComponent<BoxCollider>();
                collider.size = new Vector3(3.9f, 4.2f, .5f);
            }
            firstDrone.pilotGate = gate.GetComponent<Collider>();
            EditorUtility.SetDirty(firstDrone);
        }

        // Give the small flight route a sealed ceiling and side walls as well.
        EnsureRoomCube(progression, "无人机穿行风道 / 左封闭壁", new Vector3(-2.15f, 16.2f, 24), new Vector3(.3f, 5.5f, 10), edge);
        EnsureRoomCube(progression, "无人机穿行风道 / 右封闭壁", new Vector3(2.15f, 16.2f, 24), new Vector3(.3f, 5.5f, 10), edge);
        EnsureRoomCube(progression, "无人机穿行风道 / 封闭顶板", new Vector3(0, 18.05f, 24), new Vector3(4, .3f, 10), metal);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("第一关已保存：起始舱室封顶，无人机模型缩小并移到拳套可击范围，攻击前红灯预警，AI 从高处俯冲；驾驶通风口时只传递水平朝向。", firstDrone);
    }

    private static GameObject EnsureRoomCube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject obj = GameObject.Find(name);
        if (!obj)
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, true);
        }
        obj.transform.position = position;
        obj.transform.localScale = size;
        if (material && obj.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;
        return obj;
    }

    [MenuItem("零界/现代关卡/填补第一小节风道落地区双缺口")]
    public static void FillFirstSectionLandingHoles()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != FirstScene)
            scene = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
        Transform progression = GameObject.Find("Modern Facility Progression")?.transform;
        GameObject landing = GameObject.Find("风道出口落地区");
        if (!progression || !landing)
        {
            Debug.LogError("找不到现代流程根物件或风道出口落地区，未填补。", landing);
            return;
        }

        float top = landing.transform.position.y + landing.transform.lossyScale.y * .5f;
        float z = landing.transform.position.z;
        float y = top + .025f;
        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ModernFacility/ModernMetal.mat");
        EnsureRoomCube(progression, "第一小节 / 风道落地区左侧缺口填板", new Vector3(-4.1f, y, z), new Vector3(4.8f, .05f, 5.8f), metal);
        EnsureRoomCube(progression, "第一小节 / 风道落地区右侧缺口填板", new Vector3(4.1f, y, z), new Vector3(4.8f, .05f, 5.8f), metal);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("第一小节风道落地区左右两个缺口已补齐并保存。", landing);
    }

    [MenuItem("零界/现代关卡/封闭第一现代关并拆除第二至第三节桥")]
    public static void EncloseModernFirstSceneAndRemoveBridge()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != FirstScene)
            scene = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);

        Transform parent = GameObject.Find("Modern Facility Progression")?.transform;
        if (!parent) parent = new GameObject("Modern Facility Progression").transform;

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(r => r.gameObject.scene == scene && Mathf.Abs(r.bounds.center.x) < 500f
                && Mathf.Abs(r.bounds.center.y) < 500f && Mathf.Abs(r.bounds.center.z) < 500f)
            .ToArray();
        if (renderers.Length == 0)
        {
            Debug.LogError("场景里没有可用于计算封闭范围的 Renderer。", parent);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        float minX = bounds.min.x - 8f;
        float maxX = bounds.max.x + 8f;
        float minZ = bounds.min.z - 10f;
        float maxZ = bounds.max.z + 10f;
        float floorY = Mathf.Min(-4f, bounds.min.y - 8f);
        float ceilingY = bounds.max.y + 8f;
        float centerY = (floorY + ceilingY) * .5f;
        float width = maxX - minX;
        float length = maxZ - minZ;

        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ModernFacility/ModernMetal.mat");
        Material edge = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ModernFacility/ModernEdge.mat");
        EnsureRoomCube(parent, "现代设施大场景 / 左封闭墙", new Vector3(minX, centerY, (minZ + maxZ) * .5f), new Vector3(.8f, ceilingY - floorY, length), edge);
        EnsureRoomCube(parent, "现代设施大场景 / 右封闭墙", new Vector3(maxX, centerY, (minZ + maxZ) * .5f), new Vector3(.8f, ceilingY - floorY, length), edge);
        EnsureRoomCube(parent, "现代设施大场景 / 后封闭墙", new Vector3((minX + maxX) * .5f, centerY, minZ), new Vector3(width, ceilingY - floorY, .8f), edge);
        EnsureRoomCube(parent, "现代设施大场景 / 前封闭墙", new Vector3((minX + maxX) * .5f, centerY, maxZ), new Vector3(width, ceilingY - floorY, .8f), edge);
        EnsureRoomCube(parent, "现代设施大场景 / 连续顶棚", new Vector3((minX + maxX) * .5f, ceilingY, (minZ + maxZ) * .5f), new Vector3(width, .8f, length), metal);
        EnsureRoomCube(parent, "现代设施大场景 / 跌落安全底板", new Vector3((minX + maxX) * .5f, floorY, (minZ + maxZ) * .5f), new Vector3(width, .8f, length), metal);
        RenderSettings.skybox = null;

        Transform[] candidates = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene
                && (t.name.ToLowerInvariant().Contains("bridge") || t.name.Contains("桥"))
                && t.position.z >= 45f && t.position.z <= 90f)
            .ToArray();
        int removed = 0;
        foreach (Transform transform in candidates)
        {
            if (!transform) continue;
            bool hasMatchingAncestor = candidates.Any(candidate => candidate && candidate != transform && transform.IsChildOf(candidate));
            if (hasMatchingAncestor) continue;
            Undo.DestroyObjectImmediate(transform.gameObject);
            removed++;
        }

        // The generated seven-step service stair is the only walkable link
        // from section two to the puzzle platform. Remove it so the wrecked,
        // piloted drone is the intended crossing mechanic.
        string[] sectionLinkNames = { "上行检修阶梯", "阶梯紫色导向灯", "阶梯侧栏左", "阶梯侧栏右" };
        GameObject[] sectionLinks = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && sectionLinkNames.Any(prefix => t.name.StartsWith(prefix)))
            .Select(t => t.gameObject)
            .Distinct()
            .ToArray();
        foreach (GameObject link in sectionLinks)
            if (link) Undo.DestroyObjectImmediate(link);
        removed += sectionLinks.Length;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"现代第一关已加封闭顶棚、四面墙和防坠底板，关闭 Skybox；已移除 {removed} 个第二至第三节的步桥对象，改为由玩家接管无人机飞越。", parent);
    }

    [MenuItem("零界/现代关卡/组装第一关流程与场景顺序")]
    public static void Build()
    {
        Scene first = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
        if (GameObject.Find("Modern Facility Progression"))
        {
            Debug.Log("第一关流程已经组装过；为避免重复放置，本次未重复生成。", GameObject.Find("Modern Facility Progression"));
            WireSceneFlow();
            return;
        }

        GameObject player = GameObject.Find("Player");
        Transform momentum = GameObject.Find(MomentumRootName)?.transform;
        if (!player || !momentum)
        {
            Debug.LogError("第一关缺少 Player 或 MomentumRedirect 场景根物件，未执行组装。", player);
            return;
        }

        GameObject root = new GameObject("Modern Facility Progression");
        Undo.RegisterCreatedObjectUndo(root, "装配现代关流程");
        momentum.position += Vector3.forward * PuzzleOffsetZ;
        player.transform.SetPositionAndRotation(new Vector3(-2.2f, FloorY + 0.5f, -4.6f), Quaternion.identity);

        EnsureMaterialFolder();
        Material metal = MakeMaterial("Assets/Materials/ModernFacility/ModernMetal.mat", new Color(.12f, .16f, .19f), .78f, .84f);
        Material edge = MakeMaterial("Assets/Materials/ModernFacility/ModernEdge.mat", new Color(.24f, .29f, .32f), .68f, .76f);
        Material purple = MakeMaterial("Assets/Materials/ModernFacility/PurpleIndicator.mat", new Color(.38f, .06f, .62f), .3f, .35f, new Color(.52f, .1f, .95f));

        BuildRoom(root.transform, metal, edge, purple);
        if (!player.GetComponent<Resonator.AI.ResonatorTarget>()) Undo.AddComponent<Resonator.AI.ResonatorTarget>(player);
        if (!player.GetComponent<Resonator.AI.ResonatorPlayerBridge>()) Undo.AddComponent<Resonator.AI.ResonatorPlayerBridge>(player);
        BuildCombatAndCage(root.transform, metal, edge, purple, player);
        BuildStairApproach(root.transform, metal, edge, purple);
        BuildPuzzleExit(root.transform, metal, edge, purple);
        BuildNavMesh(root.transform);
        EditorSceneManager.MarkSceneDirty(first);
        EditorSceneManager.SaveScene(first);

        WireSceneFlow();
        Debug.Log("第一现代关已装配：首架无人机、两名共鸣者与尸体机关、笼中无人机、MomentumRedirect 及第二关出口。", root);
    }

    private static Material MakeMaterial(string path, Color color, float metallic, float smoothness, Color? emission = null)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildRoom(Transform parent, Material metal, Material edge, Material purple)
    {
        // 初始舱室：地面与两侧金属墙。玩家沿 Z 轴前进，窄风道洞口在前墙上方。
        Cube(parent, "现代设施 / 起始舱室地面", new Vector3(0, FloorY, 5), new Vector3(14, .8f, 28), metal);
        Cube(parent, "起始舱室 / 左侧检修墙", new Vector3(-7, 14, 5), new Vector3(.6f, 12, 28), metal);
        Cube(parent, "起始舱室 / 右侧检修墙", new Vector3(7, 14, 5), new Vector3(.6f, 12, 28), metal);
        Cube(parent, "起始舱室 / 后墙", new Vector3(0, 14, -9), new Vector3(14, 12, .6f), metal);
        Cube(parent, "风道门框 / 左侧", new Vector3(-4.5f, 15, 19), new Vector3(5, 10, .8f), edge);
        Cube(parent, "风道门框 / 右侧", new Vector3(4.5f, 15, 19), new Vector3(5, 10, .8f), edge);
        Cube(parent, "风道门框 / 下沿", new Vector3(0, 10.9f, 19), new Vector3(4, 5.8f, .8f), metal);
        Cube(parent, "风道门框 / 上沿", new Vector3(0, 19.2f, 19), new Vector3(4, 1.8f, .8f), metal);
        Cube(parent, "无人机穿行风道", new Vector3(0, 13.2f, 24), new Vector3(4, .45f, 10), metal);
        Cube(parent, "风道出口落地区", new Vector3(0, FloorY, 22.5f), new Vector3(14, .8f, 7), metal);
        Cube(parent, "风道紫色导向灯", new Vector3(0, 13.8f, 18.45f), new Vector3(1.7f, .12f, .1f), purple);
        Sign(parent, "教学提示 / 首架无人机", new Vector3(0, 11.5f, 10), "击毁无人机后按 E 接管\n右键飞行，鼠标指向方向", purple);
        AddLight(parent, "舱室紫色指示灯", new Vector3(0, 21.1f, 14), purple, 4f, 10f);

        // 入场小房间与左右可读通道。
        Cube(parent, "共鸣者交战室地面", new Vector3(0, FloorY, 38), new Vector3(16, .8f, 24), metal);
        Cube(parent, "交战室左墙", new Vector3(-8, 14, 38), new Vector3(.7f, 12, 24), metal);
        Cube(parent, "交战室右墙", new Vector3(8, 14, 38), new Vector3(.7f, 12, 24), metal);
        Cube(parent, "交战室顶梁 A", new Vector3(0, 21, 33), new Vector3(16, .35f, .4f), edge);
        Cube(parent, "交战室顶梁 B", new Vector3(0, 21, 43), new Vector3(16, .35f, .4f), edge);
        Cube(parent, "右侧压力机关平台", new Vector3(5.6f, FloorY + .15f, 40.8f), new Vector3(2.5f, .3f, 3f), edge);
        Cube(parent, "压力机关紫色核心", new Vector3(5.6f, 8.65f, 40.8f), new Vector3(1.1f, .7f, 1.1f), purple);
        Sign(parent, "提示 / 右侧机关", new Vector3(4.6f, 10.4f, 40.6f), "把倒下的共鸣者\n击飞到右侧机关", purple);
    }

    private static void BuildCombatAndCage(Transform parent, Material metal, Material edge, Material purple, GameObject player)
    {
        GameObject cage = new GameObject("无人机笼与压力机关");
        cage.transform.SetParent(parent, false);
        Vector3 cageCenter = new Vector3(0, 10.8f, 45.5f);
        GameObject door = Cube(cage.transform, "可开启笼门", cageCenter + new Vector3(0, 0, -1.2f), new Vector3(4, 4.8f, .35f), edge);
        GameObject cageTop = Cube(cage.transform, "笼顶横梁", cageCenter + new Vector3(0, 2.5f, 0), new Vector3(4.8f, .4f, 3f), metal);
        Cube(cage.transform, "笼左侧", cageCenter + new Vector3(-2.3f, 0, 0), new Vector3(.3f, 4.8f, 3f), edge);
        Cube(cage.transform, "笼右侧", cageCenter + new Vector3(2.3f, 0, 0), new Vector3(.3f, 4.8f, 3f), edge);
        Cube(cage.transform, "笼后侧", cageCenter + new Vector3(0, 0, 1.35f), new Vector3(4.8f, 4.8f, .3f), edge);
        cageTop.transform.localScale = new Vector3(4.8f, .4f, 3f);

        GameObject triggerObject = new GameObject("尸体触发 / 右侧压力机关");
        triggerObject.transform.SetParent(parent, false);
        triggerObject.transform.position = new Vector3(5.6f, 8.9f, 40.8f);
        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.5f, 2.2f, 2.5f);
        ModernCageMechanism mechanism = triggerObject.AddComponent<ModernCageMechanism>();
        SerializedObject mechanismData = new SerializedObject(mechanism);
        mechanismData.FindProperty("cageDoor").objectReferenceValue = door;
        mechanismData.FindProperty("indicator").objectReferenceValue = GameObject.Find("压力机关紫色核心")?.GetComponent<Renderer>();
        mechanismData.ApplyModifiedPropertiesWithoutUndo();

        GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab");
        if (dronePrefab)
        {
            GameObject secondDrone = new GameObject("笼中巡逻无人机 / 待释放");
            secondDrone.transform.SetParent(parent, false);
            secondDrone.transform.position = cageCenter + new Vector3(0, .5f, 0);
            Rigidbody droneBody = secondDrone.AddComponent<Rigidbody>();
            droneBody.useGravity = false;
            droneBody.isKinematic = true;
            SphereCollider droneCollider = secondDrone.AddComponent<SphereCollider>();
            droneCollider.radius = .75f;
            GameObject droneVisual = (GameObject)PrefabUtility.InstantiatePrefab(dronePrefab, SceneManager.GetActiveScene());
            droneVisual.name = "MCU7A 无人机模型";
            droneVisual.transform.SetParent(secondDrone.transform, false);
            droneVisual.transform.localPosition = Vector3.zero;
            droneVisual.transform.localScale = Vector3.one * 1.5f;
            DisableVisualScripts(droneVisual);
            ModernCombatDrone droneAI = secondDrone.AddComponent<ModernCombatDrone>();
            droneAI.targetPlayer = player.GetComponent<PlayerCombatController>();
            droneAI.playerMovement = player.GetComponent<PlayerMovement>();
            droneAI.playerLook = player.GetComponent<PlayerLook>();
            droneAI.playerPunch = player.GetComponent<PlayerChargedPunch>();
            droneAI.playerCamera = player.GetComponentInChildren<Camera>(true)?.transform;
            GameObject patrolA = Empty(parent, "笼中无人机 / 巡逻点 A", cageCenter + new Vector3(-4f, 1.8f, 7f));
            GameObject patrolB = Empty(parent, "笼中无人机 / 巡逻点 B", cageCenter + new Vector3(4f, 3f, 10f));
            droneAI.patrolA = patrolA.transform;
            droneAI.patrolB = patrolB.transform;
            GameObject seat = Empty(droneAI.transform, "无人机驾驶位置", Vector3.zero);
            seat.transform.localPosition = Vector3.up * 1.1f;
            droneAI.seat = seat.transform;
            droneAI.detectionRange = 14f;
            droneAI.maxHealth = 3f;
            droneAI.pilotDuration = 20f;
            secondDrone.SetActive(false);
            SerializedObject serialized = new SerializedObject(mechanism);
            serialized.FindProperty("cagedDrone").objectReferenceValue = secondDrone;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogError("没找到 MCU7A 无人机预制体；笼中无人机未生成。", cage);

        GameObject encounter = new GameObject("两名共鸣者 / 机关解锁遭遇");
        encounter.transform.SetParent(parent, false);
        GameObject spawnA = Empty(encounter.transform, "共鸣者出生点 A", new Vector3(-3.2f, FloorY + .1f, 36f));
        GameObject spawnB = Empty(encounter.transform, "共鸣者出生点 B", new Vector3(2.8f, FloorY + .1f, 36f));
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");
        if (enemyPrefab)
        {
            ResonatorEncounter encounterComponent = encounter.AddComponent<ResonatorEncounter>();
            SerializedObject data = new SerializedObject(encounterComponent);
            data.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerCombatController>();
            data.FindProperty("equipment").objectReferenceValue = player.GetComponent<PlayerGauntletEquipment>();
            data.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            data.FindProperty("spawnPoints").arraySize = 2;
            data.FindProperty("spawnPoints").GetArrayElementAtIndex(0).objectReferenceValue = spawnA.transform;
            data.FindProperty("spawnPoints").GetArrayElementAtIndex(1).objectReferenceValue = spawnB.transform;
            data.FindProperty("enemyCount").intValue = 2;
            data.FindProperty("startDelay").floatValue = 1.5f;
            data.FindProperty("spawnInterval").floatValue = 1.2f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogError("没找到共鸣者预制体；两名敌人遭遇未生成。", encounter);
    }

    private static void BuildStairApproach(Transform parent, Material metal, Material edge, Material purple)
    {
        // 从笼区向上通风井前进；阶梯作为无人机飞行导航标记，楼梯边缘用紫色灯条。
        for (int i = 0; i < 7; i++)
        {
            float z = 51f + i * 4.2f;
            float y = FloorY + i * .55f;
            Cube(parent, $"上行检修阶梯 {i + 1:00}", new Vector3(0, y, z), new Vector3(7.2f, .55f, 4.4f), metal);
            Cube(parent, $"阶梯紫色导向灯 {i + 1:00}", new Vector3(0, y + .3f, z - 2.12f), new Vector3(6f, .08f, .08f), purple);
            Cube(parent, $"阶梯侧栏左 {i + 1:00}", new Vector3(-3.9f, y + .65f, z), new Vector3(.22f, 1.1f, 4.4f), edge);
            Cube(parent, $"阶梯侧栏右 {i + 1:00}", new Vector3(3.9f, y + .65f, z), new Vector3(.22f, 1.1f, 4.4f), edge);
        }
        Cube(parent, "通风区入口平台", new Vector3(0, 8.1f, 82.5f), new Vector3(9, .8f, 8), metal);
        Sign(parent, "旧式动量装置", new Vector3(0, 10.5f, 80), "前方：动量校准\n利用方块与拳套冲刺", purple);
        AddLight(parent, "上行风道指示灯", new Vector3(0, 17f, 65), purple, 3f, 12f);
    }

    private static void BuildPuzzleExit(Transform parent, Material metal, Material edge, Material purple)
    {
        Cube(parent, "动量机关后 / 通风出口踏板", new Vector3(0, FloorY, 114), new Vector3(12, .8f, 18), metal);
        Cube(parent, "通风出口左墙", new Vector3(-6.2f, 14, 114), new Vector3(.4f, 12, 18), edge);
        Cube(parent, "通风出口右墙", new Vector3(6.2f, 14, 114), new Vector3(.4f, 12, 18), edge);
        GameObject exit = Empty(parent, "出口 / 前往现代设施第二关", new Vector3(0, 9f, 121f));
        BoxCollider trigger = exit.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(8, 5, 2);
        LevelExit levelExit = exit.AddComponent<LevelExit>();
        SerializedObject data = new SerializedObject(levelExit);
        data.FindProperty("nextSceneName").stringValue = "Modern_02_CrystalTraversal";
        data.ApplyModifiedPropertiesWithoutUndo();
        Sign(parent, "第一关出口", new Vector3(0, 11.4f, 119), "动量机关完成后\n进入晶体设施", purple);
    }

    private static void BuildNavMesh(Transform parent)
    {
        NavMeshSurface surface = parent.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
    }

    private static void WireSceneFlow()
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        List<EditorBuildSettingsScene> scenes = existing.ToList();
        string boss = "Assets/Game/Scene/UDK7MBossArena_ThreePhase.unity";
        scenes.RemoveAll(scene => scene.path == FirstScene || scene.path == SecondScene);
        int bossIndex = scenes.FindIndex(scene => scene.path == boss);
        if (bossIndex < 0) bossIndex = scenes.Count;
        scenes.Insert(bossIndex, new EditorBuildSettingsScene(FirstScene, true));
        scenes.Insert(bossIndex + 1, new EditorBuildSettingsScene(SecondScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Scene soviet = EditorSceneManager.OpenScene("Assets/Scenes/Soviet_Multi.unity", OpenSceneMode.Single);
        foreach (LevelExit exit in Object.FindObjectsByType<LevelExit>(FindObjectsSortMode.None))
        {
            SerializedObject data = new SerializedObject(exit);
            data.FindProperty("nextSceneName").stringValue = "Modern_01_DroneEscape";
            UnityEditor.SceneAsset firstSceneAsset = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(FirstScene);
            if (data.FindProperty("editorNextScene") != null)
                data.FindProperty("editorNextScene").objectReferenceValue = firstSceneAsset;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(soviet);
        EditorSceneManager.SaveScene(soviet);

        Scene second = EditorSceneManager.OpenScene(SecondScene, OpenSceneMode.Single);
        GameObject finish = GameObject.Find("Demo complete checkpoint");
        if (finish)
        {
            LevelExit levelExit = finish.GetComponent<LevelExit>() ?? Undo.AddComponent<LevelExit>(finish);
            SerializedObject data = new SerializedObject(levelExit);
            data.FindProperty("nextSceneName").stringValue = "UDK7MBossArena_ThreePhase";
            UnityEditor.SceneAsset bossSceneAsset = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(boss);
            if (data.FindProperty("editorNextScene") != null)
                data.FindProperty("editorNextScene").objectReferenceValue = bossSceneAsset;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(second);
            EditorSceneManager.SaveScene(second);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("场景顺序已设为：苏联关卡 → Modern_01 → Modern_02 → 三阶段 Boss。第二现代关终点已连接 Boss 场景。");
        EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
    }

    private static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/ModernFacility"))
            AssetDatabase.CreateFolder("Assets/Materials", "ModernFacility");
    }

    private static void DisableVisualScripts(GameObject root)
    {
        foreach (Collider col in root.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
    }

    private static GameObject Empty(Transform parent, string name, Vector3 position)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        return obj;
    }

    private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.localScale = size;
        if (material) obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static void Sign(Transform parent, string name, Vector3 position, string message, Material material)
    {
        GameObject sign = new GameObject(name);
        sign.transform.SetParent(parent, false);
        sign.transform.position = position;
        TextMesh text = sign.AddComponent<TextMesh>();
        text.text = message;
        text.fontSize = 50;
        text.characterSize = .055f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = material ? material.color : Color.white;
        text.richText = false;
    }

    private static void AddLight(Transform parent, string name, Vector3 position, Material material, float intensity, float range)
    {
        GameObject fixture = Cube(parent, name + " / 灯牌", position, new Vector3(1.8f, .12f, .12f), material);
        Light light = fixture.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = material ? material.color : Color.magenta;
        light.intensity = intensity;
        light.range = range;
    }
}
