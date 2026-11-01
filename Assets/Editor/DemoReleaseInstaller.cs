using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DemoReleaseInstaller
{
    const string Request = "Library/InstallDemoRelease.request";
    static bool editReady;
    static double readyAfter;
    static DemoReleaseInstaller()
    {
        editReady = !EditorApplication.isPlayingOrWillChangePlaymode;
        readyAfter = EditorApplication.timeSinceStartup + 2;
        EditorApplication.playModeStateChanged += s => { editReady = s == PlayModeStateChange.EnteredEditMode; readyAfter = EditorApplication.timeSinceStartup + 2; };
        EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (!File.Exists(Request) || !editReady || EditorApplication.timeSinceStartup < readyAfter
            || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request); Install();
    }

    [MenuItem("Tools/零界/配置 Demo 菜单与打包入口")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene previous = SceneManager.GetActiveScene();
        string backup = "Library/DemoReleaseBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy("ProjectSettings/EditorBuildSettings.asset", backup + "/EditorBuildSettings.asset", true);
        try
        {
            const string uiFolder = "Assets/Resources/DemoUI";
            Directory.CreateDirectory(uiFolder); AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(uiFolder + "/DemoSession.prefab") == null)
            {
                GameObject ui = new GameObject("DemoSession"); ui.AddComponent<DemoSession>();
                PrefabUtility.SaveAsPrefabAsset(ui, uiFolder + "/DemoSession.prefab");
                UnityEngine.Object.DestroyImmediate(ui);
            }
            string[] names = { "Demo_IntroCombat", "Level1_Billiards", "Level2_Pendulum" };
            foreach (string name in names)
            {
                string path = "Assets/Scenes/" + name + ".unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                File.Copy(path, backup + "/" + name + ".unity", true);
                var players = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerMovement>(true));
                foreach (PlayerMovement player in players)
                {
                    if (player.GetComponent<RoomRestartController>() == null) Undo.AddComponent<RoomRestartController>(player.gameObject);
                    if (player.GetComponent<PlayerCombatController>() == null) Undo.AddComponent<PlayerCombatController>(player.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            string[] paths = names.Select(n => "Assets/Scenes/" + n + ".unity").ToArray();
            // Retain the other scene entries for development, disabling them in this demo build.
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true))
                .Concat(EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path)).Select(s => new EditorBuildSettingsScene(s.path, false))).ToArray();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/DemoReleaseInstalled.txt", DateTime.Now.ToString("O") + "\nBackup: " + backup);
            Debug.Log("Demo 菜单与重试已配置：教学场景为构建入口，后接第一关和第二关；其余场景保留为未勾选。资源位于 Resources/DemoUI。");
        }
        finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }
}
