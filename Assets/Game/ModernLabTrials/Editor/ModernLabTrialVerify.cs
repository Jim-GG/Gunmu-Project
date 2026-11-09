using System;
using System.Collections.Generic;
using System.Linq;
using FirstPerson;
using Resonator.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ModernLabTrials.Editor
{
    public static class ModernLabTrialVerify
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/ModernLabTrials/Scenes/MLT_01_CrystalIntake.unity",
            "Assets/Game/ModernLabTrials/Scenes/MLT_02_CrystalAscent.unity",
            "Assets/Game/ModernLabTrials/Scenes/MLT_03_ResonanceArena.unity",
            "Assets/Game/ModernLabTrials/Scenes/MLT_04_CoreBreach.unity",
        };

        private static int failures;

        [MenuItem("Tools/Modern Lab Trials/Verify Four Levels")]
        public static void VerifyMenu()
        {
            Verify();
        }

        public static void VerifyBatch()
        {
            try { Verify(); EditorApplication.Exit(failures == 0 ? 0 : 1); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(2); }
        }

        public static void Verify()
        {
            failures = 0;
            CheckBuildSettings();
            CheckPlayerPrefab();
            CheckDronePrefab();
            foreach (string path in Scenes) VerifyScene(path);
            Debug.Log(failures == 0 ? "MODERNLAB_VERIFY: PASS" : $"MODERNLAB_VERIFY: FAIL ({failures})");
        }

        private static void CheckBuildSettings()
        {
            foreach (string path in Scenes)
            {
                EditorBuildSettingsScene item = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == path);
                Expect(item != null && item.enabled, "BuildSettings: " + path);
            }
        }

        private static void CheckPlayerPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/ModernLabTrials/Prefabs/ModernLabPlayer.prefab");
            Expect(prefab != null, "ModernLabPlayer prefab exists");
            if (prefab == null) return;
            Expect(prefab.GetComponent<FirstPersonVitals>() != null, "Player: FirstPersonVitals");
            Expect(prefab.GetComponent<GauntletCombat>() != null, "Player: GauntletCombat");
            Expect(prefab.GetComponent<FirstPersonMotor>() != null, "Player: FirstPersonMotor");
            ResonatorTarget target = prefab.GetComponent<ResonatorTarget>();
            Expect(target != null, "Player: ResonatorTarget");
            if (target != null)
            {
                Expect(target.eyes != null, "Player: target eyes");
                Expect(target.vitals != null, "Player: target vitals");
                Expect(target.gauntlet != null, "Player: target gauntlet");
            }
            ResonatorPlayerBridge bridge = prefab.GetComponent<ResonatorPlayerBridge>();
            Expect(bridge != null, "Player: ResonatorPlayerBridge");
            if (bridge != null)
            {
                Expect(bridge.actor != null && bridge.motor != null && bridge.combat != null,
                    "Player: bridge references");
            }
            CrystalParkour.CrystalTraversal traversal = prefab.GetComponent<CrystalParkour.CrystalTraversal>();
            Expect(traversal != null, "Player: crystal traversal component");
            if (traversal != null)
            {
                Expect(traversal.motor != null && traversal.combat != null && traversal.aim != null,
                    "Player: crystal traversal references");
                Expect(!traversal.respawnKeyEnabled, "Player: traversal R key disabled");
            }
        }

        private static void CheckDronePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/ModernLabTrials/Prefabs/ModernLabDrone.prefab");
            Expect(prefab != null, "ModernLabDrone prefab exists");
            if (prefab == null) return;
            NeutralDrone drone = prefab.GetComponent<NeutralDrone>();
            Expect(drone != null, "Drone: NeutralDrone component");
            Expect(prefab.GetComponent<CrystalParkour.TraversalDrone>() == null,
                "Drone: old route component removed");
            Expect(prefab.GetComponentInChildren<BoxCollider>(true) != null, "Drone: a collider exists");
        }

        private static void VerifyScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            string label = System.IO.Path.GetFileNameWithoutExtension(path);
            Debug.Log("--- " + label + " ---");

            TrialLevel level = Find<TrialLevel>(scene);
            Expect(level != null, label + ": TrialLevel");
            if (level == null) return;
            Expect(level.vitals != null && level.motor != null && level.combat != null,
                label + ": player components wired");
            Expect(level.spawn != null, label + ": spawn point");
            if (level.spawn != null)
            {
                Physics.SyncTransforms();
                bool floor = Physics.Raycast(level.spawn.position + Vector3.up * .5f, Vector3.down,
                    out RaycastHit hit, 4f);
                Expect(floor, label + ": spawn above floor" + (floor ? " (" + hit.collider.name + ")" : ""));
            }

            Expect(level.RequiredReceivers > 0 || level.receivers.Length == 0,
                label + ": receivers only on puzzle levels");
            foreach (ImpactReceiver receiver in level.receivers ?? Array.Empty<ImpactReceiver>())
            {
                Expect(receiver != null, label + ": receiver not null");
                if (receiver != null)
                {
                    Expect(receiver.GetComponent<Collider>() != null, label + ": receiver collider");
                    Expect(receiver.GetComponentInChildren<Light>(true) != null, label + ": receiver light");
                }
            }

            Expect(level.RequiredGrowths > 0 || level.growthSites.Length == 0,
                label + ": crystal growth only on puzzle levels");
            foreach (CrystalParkour.CrystalGrowthSite site in level.growthSites ?? Array.Empty<CrystalParkour.CrystalGrowthSite>())
            {
                Expect(site != null, label + ": growth site not null");
                if (site == null) continue;
                SerializedObject data = new SerializedObject(site);
                Expect(data.FindProperty("growthRoot").objectReferenceValue != null, label + ": growth root");
                Expect(data.FindProperty("strikeCollider").objectReferenceValue != null, label + ": growth strike collider");
            }

            Expect(level.enemies != null && level.enemies.Length > 0, label + ": enemies");
            Expect(level.RequiredEnemies > 0, label + ": enemy quota");
            Expect(level.enemies.Length == level.RequiredEnemies, label + ": every listed enemy counts");
            foreach (ResonatorEnemy enemy in level.enemies ?? Array.Empty<ResonatorEnemy>())
            {
                Expect(enemy != null, label + ": enemy not null");
                if (enemy != null)
                {
                    Expect(enemy.target != null, label + ": enemy target");
                    Expect(enemy.GetComponent<NavMeshAgent>() != null, label + ": enemy NavMeshAgent");
                }
            }

            Expect(level.drones != null && level.drones.Length > 0, label + ": drones");
            foreach (NeutralDrone drone in level.drones ?? Array.Empty<NeutralDrone>())
            {
                Expect(drone != null, label + ": drone not null");
                if (drone == null) continue;
                SerializedObject data = new SerializedObject(drone);
                Expect(data.FindProperty("home").objectReferenceValue != null, label + ": drone home");
                Expect(data.FindProperty("attackEnabled").boolValue, label + ": drone attack enabled");
                if (label.Contains("CrystalIntake"))
                    Expect(data.FindProperty("liftTop").objectReferenceValue != null, label + ": drone liftTop");
                SerializedProperty route = data.FindProperty("patrolPoints");
                Expect(route != null && route.arraySize > 0, label + ": drone patrol points");
                Expect(data.FindProperty("deck").objectReferenceValue != null, label + ": drone deck");
            }

            Expect(level.exitDoor != null, label + ": exit door");
            TrialExit exit = Find<TrialExit>(scene);
            Expect(exit != null, label + ": exit trigger");
            if (exit != null)
            {
                SerializedObject exitData = new SerializedObject(exit);
                Expect(exitData.FindProperty("level").objectReferenceValue == level,
                    label + ": exit bound to level");
                Expect(exit.GetComponent<Collider>() != null && exit.GetComponent<Collider>().isTrigger,
                    label + ": exit trigger collider");
            }

            int crates = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<ImpactCrate>(true)).Count();
            Expect(crates > 0, label + ": impact crates (" + crates + ")");

            NavMeshSurface surface = Find<NavMeshSurface>(scene);
            Expect(surface != null, label + ": NavMeshSurface");
            if (surface != null)
            {
                Expect(surface.navMeshData != null, label + ": navmesh data");
                if (surface.navMeshData != null)
                {
                    string dataPath = AssetDatabase.GetAssetPath(surface.navMeshData);
                    Expect(!string.IsNullOrEmpty(dataPath), label + ": navmesh asset path");
                }
            }

            int missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            Expect(missing == 0, label + ": no missing scripts (" + missing + ")");
            Debug.Log($"--- {label}: receivers={level.receivers?.Length} enemies={level.enemies?.Length} drones={level.drones?.Length} ---");
        }

        private static void Expect(bool condition, string message)
        {
            if (condition) Debug.Log("PASS: " + message);
            else { failures++; Debug.LogError("FAIL: " + message); }
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
}
