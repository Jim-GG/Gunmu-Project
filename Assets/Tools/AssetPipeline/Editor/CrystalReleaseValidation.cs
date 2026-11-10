using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad] public static class CrystalReleaseValidation
    {
        const string Request = "Docs/Integration/audit.request";
        static CrystalReleaseValidation() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            File.Delete(Request);
            var reports = new List<object>();
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Scene scene = default;
                bool alreadyOpen = SceneManager.GetSceneByPath(entry.path).isLoaded;
                try
                {
                    scene = alreadyOpen ? SceneManager.GetSceneByPath(entry.path) : EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                    var missing = new List<string>();
                    var broken = new List<string>();
                    var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                    foreach (var go in objects)
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0) missing.Add(go.name);
                        foreach (var c in go.GetComponents<Component>().Where(c => c != null))
                        {
                            var so = new SerializedObject(c); var it = so.GetIterator();
                            while (it.Next(true))
                                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                                    broken.Add(go.name + ":" + c.GetType().Name + ":" + it.propertyPath);
                        }
                    }
                    reports.Add(new { scene = entry.path, objects = objects.Length, missingScripts = missing, brokenReferences = broken, dependencies = AssetDatabase.GetDependencies(entry.path, true).Length });
                }
                catch (Exception e) { reports.Add(new {scene = entry.path, error = e.ToString()}); }
                finally { if (!alreadyOpen && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            // Verify damage bridge using the real compiled component implementations.
            var probe = new GameObject("CrystalReleaseDamageProbe");
            object damage;
            try
            {
                var combat = probe.AddComponent<PlayerCombatController>();
                typeof(PlayerCombatController).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(combat, null);
                var target = probe.AddComponent<Resonator.AI.ResonatorTarget>(); target.combat = combat;
                GameplayGate.IsBlocked = false;
                int before = combat.Health;
                var result = target.ReceiveStrike(1, Vector3.back);
                damage = new {before, after = combat.Health, result = result.ToString(), passed = combat.Health == before - 1};
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            File.WriteAllText("Docs/Integration/unity-scene-audit.json", JsonConvert.SerializeObject(new {unity = Application.unityVersion, time = DateTimeOffset.Now, reports, damage}, Formatting.Indented));
        }
    }
}
