using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LabDemoSetup
{
    [MenuItem("Tools/Null Boundary/Configure Lab Demo")]
    public static void Configure()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name.ToLowerInvariant() != "labdemo")
        {
            Debug.LogWarning("Stop Play and open the labdemo scene before configuring it.");
            return;
        }
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        PlayerHealth player = objects.Select(t => t.GetComponent<PlayerHealth>()).FirstOrDefault(c => c != null);
        CalibrationTerminal terminal = objects.Select(t => t.GetComponent<CalibrationTerminal>()).FirstOrDefault(c => c != null);
        DemoEnemyHealth enemy = objects.Select(t => t.GetComponent<DemoEnemyHealth>()).FirstOrDefault(c => c != null);
        if (player == null || terminal == null || enemy == null || player.GetComponent<PlayerInteractor>() == null
            || player.GetComponent<PlayerGauntlet>() == null)
        {
            Debug.LogError("Lab setup needs Player with health/interactor/gauntlet, a terminal, and EnemyDummy.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Lab Demo");
        foreach (var old in objects.Select(t => t.GetComponent<DemoRoundManager>()).Where(c => c != null))
        {
            Undo.RecordObject(old.gameObject, "Disable old round manager");
            old.gameObject.SetActive(false);
        }
        Transform root = objects.FirstOrDefault(t => t.name == "LabDemoSetup");
        if (root == null)
        {
            GameObject created = new GameObject("LabDemoSetup");
            Undo.RegisterCreatedObjectUndo(created, "Create lab root");
            root = created.transform;
        }
        LabDemoFlow flow = root.GetComponent<LabDemoFlow>();
        if (flow == null) flow = Undo.AddComponent<LabDemoFlow>(root.gameObject);
        GameObject stand = Box(root, "GauntletStand", new Vector3(-3f, 0.4f, 4f), new Vector3(1.4f, 0.8f, 0.8f));
        GameObject item = Box(root, "GauntletPickup", new Vector3(-3f, 1.05f, 4f), new Vector3(0.65f, 0.4f, 0.4f));
        GauntletPickup pickup = item.GetComponent<GauntletPickup>();
        if (pickup == null) pickup = Undo.AddComponent<GauntletPickup>(item);
        GameObject door = Box(root, "LabExitDoor", new Vector3(0f, 1.5f, 10f), new Vector3(3f, 3f, 0.4f));
        Box(root, "ExitWallLeft", new Vector3(-8.25f, 1.5f, 10f), new Vector3(13.5f, 3f, 0.4f));
        Box(root, "ExitWallRight", new Vector3(8.25f, 1.5f, 10f), new Vector3(13.5f, 3f, 0.4f));
        Box(root, "SideWallLeft", new Vector3(-14.75f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));
        Box(root, "SideWallRight", new Vector3(14.75f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));
        Box(root, "BackWall", new Vector3(0f, 1.5f, -14.75f), new Vector3(30f, 3f, 0.5f));
        Box(root, "FarWall", new Vector3(0f, 1.5f, 14.75f), new Vector3(30f, 3f, 0.5f));
        GameObject zoneObject = Box(root, "LabExitZone", new Vector3(0f, 1f, 12f), new Vector3(3f, 2f, 1f));
        Undo.RecordObject(zoneObject.GetComponent<BoxCollider>(), "Set exit trigger");
        zoneObject.GetComponent<BoxCollider>().isTrigger = true;
        Undo.RecordObject(zoneObject.GetComponent<MeshRenderer>(), "Hide trigger mesh");
        zoneObject.GetComponent<MeshRenderer>().enabled = false;
        LabExitZone zone = zoneObject.GetComponent<LabExitZone>();
        if (zone == null) zone = Undo.AddComponent<LabExitZone>(zoneObject);
        Undo.RecordObject(flow, "Connect lab flow");
        Undo.RecordObject(pickup, "Connect pickup");
        Undo.RecordObject(zone, "Connect exit");
        flow.Configure(player, terminal, pickup, enemy, door);
        pickup.Configure(terminal, flow);
        zone.Configure(flow);
        Undo.RecordObject(enemy.gameObject, "Initialize enemy inactive");
        enemy.gameObject.SetActive(false);
        // Clear the old test obstacle from the direct route to the exit.
        Transform wall = objects.FirstOrDefault(t => t.name == "TestWall");
        if (wall != null)
        {
            Undo.RecordObject(wall, "Move test wall aside");
            wall.position = new Vector3(6f, 1.5f, 5f);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Lab configured and saved. Play: calibrate terminal, pick up gauntlet, defeat enemy, exit, retry.");
    }

    private static GameObject Box(Transform root, string name, Vector3 position, Vector3 scale)
    {
        Transform found = root.Find(name);
        GameObject box;
        if (found != null)
        {
            box = found.gameObject;
            Undo.RecordObject(box.transform, "Configure " + name);
        }
        else
        {
            box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Undo.RegisterCreatedObjectUndo(box, "Create " + name);
            Undo.SetTransformParent(box.transform, root, "Parent " + name);
        }
        box.transform.position = position;
        box.transform.localScale = scale;
        return box;
    }
}
