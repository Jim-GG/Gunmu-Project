using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChargedPunchSetup
{
    [MenuItem("Tools/Null Boundary/Add Charged Punch To Current Scene")]
    public static void AddToScene()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play before configuring the component."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (PlayerMovement player in root.GetComponentsInChildren<PlayerMovement>(true))
        {
            PlayerChargedPunch punch = player.GetComponent<PlayerChargedPunch>();
            if (punch == null) punch = Undo.AddComponent<PlayerChargedPunch>(player.gameObject);
            Selection.activeGameObject = player.gameObject;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Charged punch component ready in Inspector. Save scene after tuning.");
    }
}
