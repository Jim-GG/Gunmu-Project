using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ModernDroneSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Modern_01_DroneEscape.unity";
    private const string DronePrefabPath = "Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab";

    [MenuItem("零界/现代关卡/配置第一关无人机遭遇")]
    private static void ConfigureDroneEncounter()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
        if (!player || !prefab)
        {
            Debug.LogError("找不到 Player 或 Drone_MCU7A 预制体。", player);
            return;
        }

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        PlayerLook look = player.GetComponent<PlayerLook>();
        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        PlayerChargedPunch punch = player.GetComponent<PlayerChargedPunch>();
        if (!movement || !look)
        {
            Debug.LogError("Player 缺少 PlayerMovement 或 PlayerLook。", player);
            return;
        }

        if (!combat) combat = Undo.AddComponent<PlayerCombatController>(player);
        if (!punch) punch = Undo.AddComponent<PlayerChargedPunch>(player);

        if (GameObject.Find("Modern Drone Encounter"))
        {
            Debug.Log("现代无人机遭遇已配置。", player);
            return;
        }

        Transform cameraPivot = player.transform.Find("CameraPivot");
        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        Vector3 forward = player.transform.forward;
        Vector3 dronePosition = player.transform.position + forward * 5f + Vector3.up * 3f;

        GameObject encounter = new GameObject("Modern Drone Encounter");
        Undo.RegisterCreatedObjectUndo(encounter, "创建现代无人机遭遇");
        GameObject patrolStart = new GameObject("无人机巡逻起点");
        GameObject patrolEnd = new GameObject("无人机巡逻终点");
        patrolStart.transform.position = dronePosition - forward * 4f;
        patrolEnd.transform.position = dronePosition + forward * 4f;
        patrolStart.transform.SetParent(encounter.transform, true);
        patrolEnd.transform.SetParent(encounter.transform, true);

        GameObject drone = new GameObject("攻击型巡逻无人机");
        Undo.RegisterCreatedObjectUndo(drone, "创建攻击无人机");
        drone.transform.SetParent(encounter.transform, true);
        drone.transform.position = dronePosition;
        Rigidbody rigidbody = Undo.AddComponent<Rigidbody>(drone);
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;
        SphereCollider collider = Undo.AddComponent<SphereCollider>(drone);
        collider.radius = 0.8f;

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        visual.name = "MCU7A 无人机模型";
        visual.transform.SetParent(drone.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * 1.5f;
        foreach (Collider modelCollider in visual.GetComponentsInChildren<Collider>(true))
            modelCollider.enabled = false;
        foreach (Rigidbody modelBody in visual.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(modelBody);
        foreach (MonoBehaviour component in visual.GetComponentsInChildren<MonoBehaviour>(true))
            component.enabled = false;

        ModernCombatDrone ai = Undo.AddComponent<ModernCombatDrone>(drone);
        ai.patrolA = patrolStart.transform;
        ai.patrolB = patrolEnd.transform;
        ai.targetPlayer = combat;
        ai.playerMovement = movement;
        ai.playerLook = look;
        ai.playerPunch = punch;
        ai.playerCamera = playerCamera ? playerCamera.transform : cameraPivot;

        GameObject seat = new GameObject("无人机驾驶位置");
        seat.transform.SetParent(drone.transform, false);
        seat.transform.localPosition = Vector3.up * 1.1f;
        ai.seat = seat.transform;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("已在第一关加入无人机巡逻、攻击与接管飞行。", drone);
    }
}
