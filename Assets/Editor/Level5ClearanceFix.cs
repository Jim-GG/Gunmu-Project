using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scene-only repair: keep the puzzle rules and preserve collision on visible structures.
public static class Level5ClearanceFix
{
    [MenuItem("Tools/Null Boundary/Lower Level 5 Receiver")]
    public static void LowerReceiver()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name != "Level5_ChainReaction")
        {
            Debug.LogWarning("Stop Play and open Level5_ChainReaction first.");
            return;
        }
        var all = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform receiver = all.FirstOrDefault(t => t.name == "PuzzleSwitch_East");
        Transform gate = all.FirstOrDefault(t => t.name == "VictoryGate_East");
        Transform platform = all.FirstOrDefault(t => t.name == "EastGoalPlatform");
        Transform pillar = all.FirstOrDefault(t => t.name == "EastGoalPlatform_Pillar");
        if (receiver == null || gate == null || platform == null || pillar == null)
        {
            Debug.LogError("Receiver assembly not found; no changes made.");
            return;
        }
        Undo.RecordObjects(new Object[] { receiver, gate, platform, pillar }, "Lower Level 5 receiver");
        // Absolute height makes this command safe to repeat without cumulative lowering.
        float deltaY = 7f - receiver.position.y;
        receiver.position += Vector3.up * deltaY;
        gate.position += Vector3.up * deltaY;
        platform.position += Vector3.up * deltaY;
        // Keep the support's bottom fixed while lowering its top with the platform.
        float worldHeight = Mathf.Abs(pillar.lossyScale.y);
        float newHeight = Mathf.Max(0.2f, worldHeight + deltaY);
        pillar.position += Vector3.up * ((newHeight - worldHeight) * 0.5f);
        Vector3 scale = pillar.localScale;
        scale.y *= newHeight / worldHeight;
        pillar.localScale = scale;
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = receiver.gameObject;
        Debug.Log("Receiver center set to Y=7 m; platform and gate lowered together, support shortened. Save and test.");
    }

    [MenuItem("Tools/Null Boundary/Fix Level 5 Clearance")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name != "Level5_ChainReaction")
        {
            Debug.LogWarning("Stop Play and open Level5_ChainReaction before applying this fix.");
            return;
        }
        var all = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform target = all.FirstOrDefault(t => t.name == "KineticBox_Target");
        Transform seesaw = all.FirstOrDefault(t => t.name == "SeesawRoot");
        Transform weight = all.FirstOrDefault(t => t.name == "CraneWeightBox");
        Transform cable = all.FirstOrDefault(t => t.name == "CraneCableVisual");
        Transform tower = all.FirstOrDefault(t => t.name == "CraneTower");
        Transform boom = all.FirstOrDefault(t => t.name == "CraneBoom");
        Transform runway = all.FirstOrDefault(t => t.name == "CueTrack_EastRunway");
        if (target == null || seesaw == null || weight == null || cable == null
            || tower == null || boom == null || runway == null)
        {
            Debug.LogError("Required Level 5 objects were not found. No objects changed.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Level 5 launch and flight paths");
        Undo.RecordObjects(new Object[] { target, seesaw, weight, cable, tower, boom },
            "Clear Level 5 paths");

        // Use an absolute target coordinate so running the menu twice does not shift it twice.
        float shiftX = 1.5f - target.position.x;
        Vector3 shift = new Vector3(shiftX, 0f, 0f);
        target.position += shift;
        seesaw.position += shift;
        weight.position += shift;
        cable.position += shift;

        // Put the tower beside, rather than across, the eastward flight corridor.
        Vector3 towerPosition = tower.position;
        towerPosition.z = target.position.z + 4f;
        tower.position = towerPosition;

        // Reconnect the visible boom from the relocated tower to the existing drop point.
        Vector3 towerTop = new Vector3(tower.position.x, boom.position.y, tower.position.z);
        Vector3 dropTop = new Vector3(weight.position.x, boom.position.y, weight.position.z);
        Vector3 span = dropTop - towerTop;
        boom.position = (towerTop + dropTop) * 0.5f;
        boom.rotation = Quaternion.LookRotation(span.normalized, Vector3.up);
        Vector3 parentScale = boom.parent == null ? Vector3.one : boom.parent.lossyScale;
        boom.localScale = new Vector3(0.9f / Mathf.Abs(parentScale.x),
            0.7f / Mathf.Abs(parentScale.y), span.magnitude / Mathf.Abs(parentScale.z));

        Physics.SyncTransforms();
        float targetHalfWidth = target.GetComponent<Collider>().bounds.extents.x;
        float runwayEdge = runway.GetComponent<Collider>().bounds.max.x;
        float launchClearance = target.position.x - targetHalfWidth - runwayEdge;
        float towerSideClearance = Mathf.Abs(tower.position.z - target.position.z)
            - tower.GetComponent<Collider>().bounds.extents.z
            - target.GetComponent<Collider>().bounds.extents.z;
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = target.gameObject;
        Debug.Log($"Level 5 geometry adjusted. Launch-to-runway gap: {launchClearance:F2} m; "
            + $"flight-to-tower side gap: {towerSideClearance:F2} m. "
            + "Colliders retained. Save the scene, then test rise, cue-ball transfer and exit flight.");
    }
}
