using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalParkour;
using FirstPerson;
using Resonator.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using ModernLabTrials;

namespace ModernLabTrials.Editor
{
    // Generates four playable modern-lab scenes from ModernLab prefabs, the
    // Resonator enemy and the numbered MCU-7A drone. Runs from a request file so
    // it can be used while the editor is already open.
    [InitializeOnLoad]
    public static class ModernLabTrialBuilder
    {
        private const string Root = "Assets/Game/ModernLabTrials";
        private const string ScenesFolder = Root + "/Scenes";
        private const string PrefabsFolder = Root + "/Prefabs";
        private const string NavMeshFolder = Root + "/NavMesh";
        private const string ModernLab = "Assets/Game/Environment/ModernLab/Prefabs/";
        private const string FloorPrefab = ModernLab + "Lab_Floor_00.prefab";
        private const string WallPrefab = ModernLab + "Lab_WallWhite_19.prefab";
        private const string CeilingPrefab = ModernLab + "Lab_Ceiling_29.prefab";
        private const string AccentPrefab = ModernLab + "Lab_AccentWall_31.prefab";
        private const string RailPrefab = ModernLab + "Lab_Rail_01.prefab";
        private const string StairsPrefab = ModernLab + "Lab_Stairs_06.prefab";
        private const string CratePrefab = ModernLab + "Props/ML_Crate_1m.prefab";
        private const string ExitSignPrefab = ModernLab + "Props/ML_Exit_Door.prefab";
        private const string FloorMaterialPath = "Assets/Game/Environment/ModernLab/Materials/Lab_White_Matte.mat";
        private const string DefaultEnemyTuning = "Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset";
        private const string EnemyTuningPath = "Assets/Game/ModernLabTrials/Data/MLT_ResonatorTuning.asset";
        private const string PlayerSource = "Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab";
        private const string DroneSource = "Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab";
        private static readonly string PlayerPrefab = PrefabsFolder + "/ModernLabPlayer.prefab";
        private static readonly string DronePrefab = PrefabsFolder + "/ModernLabDrone.prefab";
        private const string Request = "Library/BuildModernLabTrials.request";

        private static bool ready;
        private static double readyAt;

        static ModernLabTrialBuilder()
        {
            ready = !EditorApplication.isPlayingOrWillChangePlaymode;
            readyAt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.playModeStateChanged += state =>
            {
                ready = state == PlayModeStateChange.EnteredEditMode;
                readyAt = EditorApplication.timeSinceStartup + 2;
            };
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!File.Exists(Request) || !ready || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < readyAt) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/Modern Lab Trials/Build Four Levels")]
        public static void BuildMenu()
        {
            Build();
        }

        public static void BuildBatch()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Build()
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                const string baseScene = "Assets/Game/Environment/ModernLab/Scenes/ModernLabModuleReview.unity";
                if (!File.Exists(baseScene))
                    throw new InvalidOperationException("Missing ModernLab review scene.");
                EditorSceneManager.OpenScene(baseScene, OpenSceneMode.Single);
            }
            EnsureFolders();
            RemoveObsoleteScenes();
            BuildPlayerPrefab();
            BuildDronePrefab();

            BuildLevel01();
            BuildLevel02();
            BuildLevel03();
            BuildLevel04();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MODERNLAB_TRIALS_BUILD: four levels built. Open MLT_01_CrystalIntake and press Play.");
        }

        // One-shot entry point used by the batch pipeline: rebuild, verify and
        // capture previews in a single editor launch.
        public static void BuildVerifyCaptureBatch()
        {
            try
            {
                Build();
                ModernLabTrialVerify.Verify();
                ModernLabTrialPreview.Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/Game", "ModernLabTrials");
            CreateFolder(Root, "Scenes");
            CreateFolder(Root, "Prefabs");
            CreateFolder(Root, "NavMesh");
            CreateFolder(Root, "Materials");
            CreateFolder(Root, "Data");
            CreateFolder(Root, "Runtime");
            CreateFolder(Root, "Editor");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        // The first pass built four impact-calibration scenes; the client asked
        // for two puzzle and two combat levels instead, so the old scenes and
        // their baked NavMesh assets are retired.
        private static void RemoveObsoleteScenes()
        {
            string[] obsolete =
            {
                ScenesFolder + "/MLT_01_ImpactCalibration.unity",
                ScenesFolder + "/MLT_02_CrosswindGallery.unity",
                ScenesFolder + "/MLT_03_VerticalShaft.unity",
                ScenesFolder + "/MLT_04_SynthesisCore.unity",
                NavMeshFolder + "/MLT_01_ImpactCalibration.asset",
                NavMeshFolder + "/MLT_02_CrosswindGallery.asset",
                NavMeshFolder + "/MLT_03_VerticalShaft.asset",
                NavMeshFolder + "/MLT_04_SynthesisCore.asset",
            };
            foreach (string path in obsolete)
            {
                if (!File.Exists(path)) continue;
                if (AssetDatabase.DeleteAsset(path)) Debug.Log("MODERNLAB_TRIALS: removed obsolete " + path);
                else Debug.LogWarning("MODERNLAB_TRIALS: could not remove obsolete " + path + " (still open?)");
            }
        }

        // ------------------------------------------------------------------ prefabs

        private static void BuildPlayerPrefab()
        {
            if (File.Exists(PlayerPrefab)) AssetDatabase.DeleteAsset(PlayerPrefab);
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(temp);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSource);
                if (prefab == null) throw new InvalidOperationException("Missing FirstPersonPlayer prefab.");
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp);
                player.name = "ModernLabPlayer";
                ResonatorTarget target = player.AddComponent<ResonatorTarget>();
                ResonatorPlayerBridge bridge = player.AddComponent<ResonatorPlayerBridge>();
                FirstPersonVitals vitals = player.GetComponent<FirstPersonVitals>();
                GauntletCombat combat = player.GetComponent<GauntletCombat>();
                FirstPersonMotor motor = player.GetComponent<FirstPersonMotor>();
                Camera camera = player.GetComponentInChildren<Camera>(true);
                if (camera == null) throw new InvalidOperationException("Player camera missing.");
                target.eyes = camera.transform;
                target.vitals = vitals;
                target.gauntlet = combat;
                bridge.actor = target;
                bridge.motor = motor;
                bridge.combat = combat;
                bridge.attackOrigin = camera.transform;
                CrystalTraversal traversal = player.AddComponent<CrystalTraversal>();
                traversal.motor = motor;
                traversal.combat = combat;
                traversal.aim = camera.transform;
                traversal.respawnKeyEnabled = false;
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefab);
                Object.DestroyImmediate(player);
                Debug.Log("MODERNLAB_TRIALS: player prefab written.");
            }
            finally
            {
                EditorSceneManager.CloseScene(temp, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        private static void BuildDronePrefab()
        {
            if (File.Exists(DronePrefab)) AssetDatabase.DeleteAsset(DronePrefab);
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(temp);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DroneSource);
                if (prefab == null) throw new InvalidOperationException("Missing MCU7A drone prefab.");
                GameObject drone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp);
                drone.name = "ModernLabDrone";
                CrystalParkour.TraversalDrone old = drone.GetComponent<CrystalParkour.TraversalDrone>();
                BoxCollider deck = old != null ? old.deck : null;
                Transform left = old != null ? old.rotorLeft : null;
                Transform right = old != null ? old.rotorRight : null;
                if (old != null) Object.DestroyImmediate(old);
                NeutralDrone neutral = drone.AddComponent<NeutralDrone>();
                neutral.ConfigureModel(deck, left, right);
                PrefabUtility.SaveAsPrefabAsset(drone, DronePrefab);
                Object.DestroyImmediate(drone);
                Debug.Log("MODERNLAB_TRIALS: drone prefab written.");
            }
            finally
            {
                EditorSceneManager.CloseScene(temp, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        // ------------------------------------------------------------------ levels

        private static void BuildLevel01()
        {
            LevelContext context = Begin("MLT_01_CrystalIntake", "ML-01", "晶体取料",
                "无人机悬停在跳跃高度之上：敲裂地面让晶体平台长出来，踩上去借力升空，再完成冲击校准。",
                "MLT_02_CrystalAscent", new Vector3(0f, .1f, -7f), 0f);
            Room room = BuildRoom(context.World, 4, 3, 8f, null);
            BuildMezzanine(context.World, new Rect(-9f, 3f, 18f, 6f), 4f);
            AddRail(context.World, new Vector3(-6f, 4f, 3f), 6f, true);
            AddRail(context.World, new Vector3(6f, 4f, 3f), 6f, true);
            AddRail(context.World, new Vector3(-9f, 4f, 6f), 6f, false);
            AddLighting(context.World, room, new Color(.4f, .66f, .92f));
            BakeNavMesh(context, "MLT_01_CrystalIntake");

            ImpactReceiver east = AddReceiver(context.World, room, Side.East, 2f, 1.5f);
            AddCrate(context.World, new Vector3(8f, .55f, 1f), east);
            AddCrate(context.World, new Vector3(5f, .55f, 3.5f), east);
            CrystalGrowthSite growth = AddGrowthSite(context.World, new Vector3(0f, 0f, -.5f), 0f);
            AddEnemy(context, new Vector3(-7f, .1f, -2f));
            AddEnemy(context, new Vector3(6f, .1f, -3f));

            NeutralDrone drone = AddDrone(context.World, new Vector3(0f, 1.15f, .6f),
                new Vector3(0f, 4.4f, 1.7f), new[]
                {
                    new Vector3(-3.5f, 1.15f, .6f), new Vector3(3.5f, 1.15f, .6f),
                }, "MCU-7A / 01", 4.2f, 8f, false);
            ExitAssembly exit = AddExit(context.World, new Vector3(0f, 4f, 8.2f), 180f, context.Level, false);

            context.Level.receivers = new[] { east };
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.growthSites = new[] { growth };
            context.Level.requiredDroneRides = 1;
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_01_CrystalIntake");
        }

        private static void BuildLevel02()
        {
            LevelContext context = Begin("MLT_02_CrystalAscent", "ML-02", "晶体攀升",
                "中央裂谷把长廊切成两半：在两侧敲出晶体台阶登上巡航无人机，往返取证并清场。",
                "MLT_03_ResonanceArena", new Vector3(-15f, .1f, -2f), 90f);
            Room room = BuildRoom(context.World, 6, 2, 8f,
                center => Mathf.Abs(center.x) < 6.1f);
            AddRail(context.World, new Vector3(-6f, 0f, -5.7f), 12f, false);
            AddRail(context.World, new Vector3(6f, 0f, -5.7f), 12f, false);
            AddRail(context.World, new Vector3(-6f, 0f, 5.7f), 12f, false);
            AddRail(context.World, new Vector3(6f, 0f, 5.7f), 12f, false);
            AddRail(context.World, new Vector3(-12f, 0f, -5.7f), 12f, false);
            AddRail(context.World, new Vector3(12f, 0f, -5.7f), 12f, false);
            AddPitLiner(context.World, 6f, 12f);
            AddLighting(context.World, room, new Color(.34f, .7f, .92f));
            BakeNavMesh(context, "MLT_02_CrystalAscent");

            ImpactReceiver northA = AddReceiver(context.World, room, Side.North, -10f, 1.5f);
            ImpactReceiver northB = AddReceiver(context.World, room, Side.North, 10f, 1.5f);
            AddCrate(context.World, new Vector3(-11f, .55f, -.5f), northA);
            AddCrate(context.World, new Vector3(11f, .55f, -.5f), northB);
            CrystalGrowthSite west = AddGrowthSite(context.World, new Vector3(-7.6f, 0f, 2.4f), 90f);
            CrystalGrowthSite east = AddGrowthSite(context.World, new Vector3(7.6f, 0f, -2.4f), -90f);
            AddEnemy(context, new Vector3(-13f, .1f, 2.5f));
            AddEnemy(context, new Vector3(13f, .1f, 2.5f));
            AddEnemy(context, new Vector3(15f, .1f, -2.5f));

            NeutralDrone drone = AddDrone(context.World, new Vector3(-7f, 1.15f, 0f), null,
                new[]
                {
                    new Vector3(-7f, 1.15f, 0f), new Vector3(7f, 1.15f, 0f),
                }, "MCU-7A / 02", 3.8f, 9f, false);
            ExitAssembly exit = AddExit(context.World, new Vector3(17f, 0f, 0f), -90f, context.Level, false);

            context.Level.receivers = new[] { northA, northB };
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.growthSites = new[] { west, east };
            context.Level.requiredDroneRides = 1;
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_02_CrystalAscent");
        }

        private static void BuildLevel03()
        {
            LevelContext context = Begin("MLT_03_ResonanceArena", "ML-03", "共鸣竞技场",
                "战斗关：清空场地内的全部共鸣者。编号无人机在头顶巡逻开火，可借它的甲板重新站位。",
                "MLT_04_CoreBreach", new Vector3(0f, .1f, -10f), 0f);
            context.Level.levelKind = "战斗关";
            Room room = BuildRoom(context.World, 6, 4, 8f, null);
            AddPillar(context.World, new Vector3(-6f, 2f, -2f), new Vector3(1.4f, 4f, 1.4f));
            AddPillar(context.World, new Vector3(6f, 2f, -2f), new Vector3(1.4f, 4f, 1.4f));
            AddPillar(context.World, new Vector3(-6f, 2f, 4f), new Vector3(1.4f, 4f, 1.4f));
            AddPillar(context.World, new Vector3(6f, 2f, 4f), new Vector3(1.4f, 4f, 1.4f));
            AddLighting(context.World, room, new Color(.5f, .72f, .98f));
            BakeNavMesh(context, "MLT_03_ResonanceArena");

            AddCrate(context.World, new Vector3(-2f, .55f, -4f), null);
            AddCrate(context.World, new Vector3(2f, .55f, -4f), null);
            AddEnemy(context, new Vector3(-12f, .1f, -4f));
            AddEnemy(context, new Vector3(12f, .1f, -4f));
            AddEnemy(context, new Vector3(-8f, .1f, 5f));
            AddEnemy(context, new Vector3(8f, .1f, 5f));
            AddEnemy(context, new Vector3(2f, .1f, 1f));
            AddEnemy(context, new Vector3(-2f, .1f, 8f));

            NeutralDrone drone = AddDrone(context.World, new Vector3(0f, 1.2f, -3f), null,
                new[]
                {
                    new Vector3(-10f, 1.2f, 0f), new Vector3(10f, 1.2f, 2f),
                }, "MCU-7A / 03", 3f, 10f, true);
            ExitAssembly exit = AddExit(context.World, new Vector3(0f, 0f, 11.2f), 180f, context.Level, false);

            context.Level.receivers = Array.Empty<ImpactReceiver>();
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.growthSites = Array.Empty<CrystalGrowthSite>();
            context.Level.requiredDroneRides = 0;
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_03_ResonanceArena");
        }

        private static void BuildLevel04()
        {
            LevelContext context = Begin("MLT_04_CoreBreach", "ML-04", "核心突入",
                "终局战斗关：核心区八名共鸣者全部清除后出口才会开启。编号无人机火力更密集。", "",
                new Vector3(0f, .1f, -11f), 0f);
            context.Level.finalLevel = true;
            context.Level.levelKind = "战斗关";
            Room room = BuildRoom(context.World, 6, 4, 8f, null);
            AddPillar(context.World, new Vector3(-6f, 2f, -3f), new Vector3(1.6f, 4f, 1.6f));
            AddPillar(context.World, new Vector3(6f, 2f, -3f), new Vector3(1.6f, 4f, 1.6f));
            AddPillar(context.World, new Vector3(-6f, 2f, 4f), new Vector3(1.6f, 4f, 1.6f));
            AddPillar(context.World, new Vector3(6f, 2f, 4f), new Vector3(1.6f, 4f, 1.6f));
            AddPillar(context.World, new Vector3(0f, 1.4f, 1f), new Vector3(3.2f, 2.8f, 1.2f));
            AddLighting(context.World, room, new Color(.55f, .78f, 1f));
            BakeNavMesh(context, "MLT_04_CoreBreach");

            AddCrate(context.World, new Vector3(-3f, .55f, -6f), null);
            AddCrate(context.World, new Vector3(3f, .55f, -6f), null);
            AddEnemy(context, new Vector3(-12f, .1f, -6f));
            AddEnemy(context, new Vector3(12f, .1f, -6f));
            AddEnemy(context, new Vector3(-12f, .1f, 2f));
            AddEnemy(context, new Vector3(12f, .1f, 2f));
            AddEnemy(context, new Vector3(-9f, .1f, 8f));
            AddEnemy(context, new Vector3(9f, .1f, 8f));
            AddEnemy(context, new Vector3(-2f, .1f, 6f));
            AddEnemy(context, new Vector3(4f, .1f, -1f));

            NeutralDrone drone = AddDrone(context.World, new Vector3(0f, 1.2f, -8f), null,
                new[]
                {
                    new Vector3(-9f, 1.2f, 0f), new Vector3(9f, 1.2f, 3f),
                }, "MCU-7A / 04", 2.4f, 12f, true);
            ExitAssembly exit = AddExit(context.World, new Vector3(0f, 0f, 11.4f), 180f, context.Level, true);

            context.Level.receivers = Array.Empty<ImpactReceiver>();
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.growthSites = Array.Empty<CrystalGrowthSite>();
            context.Level.requiredDroneRides = 0;
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_04_CoreBreach");
        }

        // ------------------------------------------------------------------ context

        private sealed class LevelContext
        {
            public Scene Scene;
            public Scene PreviousScene;
            public Transform World;
            public GameObject Player;
            public ResonatorTarget PlayerTarget;
            public TrialLevel Level;
            public Transform Spawn;
            public readonly List<ResonatorEnemy> Enemies = new List<ResonatorEnemy>();
            public string SceneName;
        }

        private static LevelContext Begin(string sceneName, string code, string title, string subtitle, string next,
            Vector3 spawnPosition, float spawnYaw)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject world = new GameObject("ModernLabWorld");
            LevelContext context = new LevelContext
            {
                Scene = scene, World = world.transform, SceneName = sceneName, PreviousScene = previous,
            };
            GameObject spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(context.World, false);
            spawn.transform.position = spawnPosition;
            spawn.transform.rotation = Quaternion.Euler(0, spawnYaw, 0);
            context.Spawn = spawn.transform;

            GameObject levelObject = new GameObject("TrialLevel");
            levelObject.transform.SetParent(context.World, false);
            TrialLevel level = levelObject.AddComponent<TrialLevel>();
            level.levelCode = code;
            level.levelTitle = title;
            level.subtitle = subtitle;
            level.nextSceneName = next;
            level.firstSceneName = "MLT_01_CrystalIntake";
            level.spawn = spawn.transform;
            context.Level = level;

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
            player.name = "ModernLabPlayer";
            player.transform.position = spawn.transform.position;
            player.transform.rotation = spawn.transform.rotation;
            context.Player = player;
            context.PlayerTarget = player.GetComponent<ResonatorTarget>();
            level.vitals = player.GetComponent<FirstPersonVitals>();
            level.motor = player.GetComponent<FirstPersonMotor>();
            level.combat = player.GetComponent<GauntletCombat>();
            level.view = player.GetComponentInChildren<Camera>(true);
            return context;
        }

        private static void Finish(LevelContext context, string sceneName)
        {
            EditorSceneManager.MarkSceneDirty(context.Scene);
            string path = ScenesFolder + "/" + sceneName + ".unity";
            EditorSceneManager.SaveScene(context.Scene, path);
            Debug.Log($"MODERNLAB_TRIALS: built {sceneName} at {path}; enemies={context.Enemies.Count}");
            EditorSceneManager.CloseScene(context.Scene, true);
            if (context.PreviousScene.IsValid() && context.PreviousScene.isLoaded)
                SceneManager.SetActiveScene(context.PreviousScene);
        }

        private static void BakeNavMesh(LevelContext context, string sceneName)
        {
            NavMeshSurface surface = context.World.gameObject.AddComponent<NavMeshSurface>();
            context.World.gameObject.AddComponent<TrialNavMeshBootstrap>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
                throw new InvalidOperationException("NavMesh bake failed for " + sceneName);
            string navPath = NavMeshFolder + "/" + sceneName + ".asset";
            if (File.Exists(navPath)) AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        }

        private static void ConfigureBuildSettings()
        {
            string[] mine =
            {
                "Assets/Game/ModernLabTrials/Scenes/MLT_01_CrystalIntake.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_02_CrystalAscent.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_03_ResonanceArena.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_04_CoreBreach.unity",
            };
            var list = mine.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (!mine.Contains(existing.path) && File.Exists(existing.path))
                    list.Add(new EditorBuildSettingsScene(existing.path, existing.enabled));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("MODERNLAB_TRIALS: build settings updated.");
        }

        // ------------------------------------------------------------------ room

        private struct Room
        {
            public float minX, maxX, minZ, maxZ, height;
        }

        private static Room BuildRoom(Transform world, int tilesX, int tilesZ, float height,
            Func<Vector3, bool> skip)
        {
            float width = tilesX * 6f, depth = tilesZ * 6f;
            Room room = new Room
            {
                minX = -width * .5f, maxX = width * .5f,
                minZ = -depth * .5f, maxZ = depth * .5f,
                height = height,
            };
            for (int x = 0; x < tilesX; x++)
            for (int z = 0; z < tilesZ; z++)
            {
                Vector3 center = new Vector3(room.minX + 3f + x * 6f, 0f, room.minZ + 3f + z * 6f);
                if (skip != null && skip(center)) continue;
                GameObject floor = Visual(world, FloorPrefab, center, Quaternion.identity, "Floor");
                ApplyFloorMaterial(floor);
                AddFloorSurface(world, center, 0f);
                AddBox(world, "FloorCollider", center + Vector3.down * .5f, new Vector3(6f, 1f, 6f));
            }
            for (int x = 0; x < tilesX; x++)
            for (int z = 0; z < tilesZ; z++)
            {
                Vector3 center = new Vector3(room.minX + 3f + x * 6f, height, room.minZ + 3f + z * 6f);
                Visual(world, CeilingPrefab, center, Quaternion.identity, "Ceiling");
            }

            float wallY = height * .5f;
            for (float x = room.minX + 1.5f; x < room.maxX; x += 3f)
            {
                Visual(world, WallPrefab, new Vector3(x, wallY, room.minZ - .1f), Quaternion.identity, "WallS");
                Visual(world, WallPrefab, new Vector3(x, wallY, room.maxZ + .1f), Quaternion.Euler(0, 180, 0), "WallN");
                Visual(world, AccentPrefab, new Vector3(x, 1.5f, room.minZ + .16f), Quaternion.identity, "AccentS");
            }
            for (float z = room.minZ + 1.5f; z < room.maxZ; z += 3f)
            {
                Visual(world, WallPrefab, new Vector3(room.minX - .1f, wallY, z), Quaternion.Euler(0, 90, 0), "WallW");
                Visual(world, WallPrefab, new Vector3(room.maxX + .1f, wallY, z), Quaternion.Euler(0, -90, 0), "WallE");
            }
            AddBox(world, "WallSouth", new Vector3(0f, wallY, room.minZ - .3f),
                new Vector3(room.maxX - room.minX + .8f, height, .6f));
            AddBox(world, "WallNorth", new Vector3(0f, wallY, room.maxZ + .3f),
                new Vector3(room.maxX - room.minX + .8f, height, .6f));
            AddBox(world, "WallWest", new Vector3(room.minX - .3f, wallY, 0f),
                new Vector3(.6f, height, room.maxZ - room.minZ + .8f));
            AddBox(world, "WallEast", new Vector3(room.maxX + .3f, wallY, 0f),
                new Vector3(.6f, height, room.maxZ - room.minZ + .8f));
            AddRoomShell(world, room, height);
            return room;
        }

        private static void BuildMezzanine(Transform world, Rect area, float y)
        {
            for (float x = area.xMin + 3f; x < area.xMax; x += 6f)
            for (float z = area.yMin + 3f; z < area.yMax; z += 6f)
            {
                Vector3 center = new Vector3(x, y, z);
                GameObject mezzanine = Visual(world, FloorPrefab, center, Quaternion.identity, "MezzanineFloor");
                ApplyFloorMaterial(mezzanine);
                AddFloorSurface(world, center, y);
                AddBox(world, "MezzanineCollider", center + Vector3.down * .5f, new Vector3(6f, 1f, 6f));
            }
            AddMezzanineShell(world, area, y);
        }

        private static void AddRail(Transform world, Vector3 position, float length, bool alongX)
        {
            Quaternion rotation = alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
            GameObject rail = Visual(world, RailPrefab, position, rotation, "Rail");
            float stretch = Mathf.Max(.2f, length / 6f);
            Vector3 scale = rail.transform.localScale;
            rail.transform.localScale = alongX ? new Vector3(stretch, scale.y, scale.z) : new Vector3(scale.x, scale.y, stretch);
            rail.transform.position = position + Vector3.up * .95f;
            foreach (MeshRenderer railRenderer in rail.GetComponentsInChildren<MeshRenderer>(true))
            {
                railRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                railRenderer.receiveShadows = false;
            }
            AddBox(world, "RailCollider", position + Vector3.up * .6f,
                alongX ? new Vector3(length, 1.2f, .12f) : new Vector3(.12f, 1.2f, length));
            AddShellBox(world, "RailBar", position + Vector3.up * 1.05f,
                alongX ? new Vector3(length, .08f, .08f) : new Vector3(.08f, .08f, length), DarkSurfaceMaterial());
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float t = posts == 1 ? .5f : i / (float)(posts - 1);
                Vector3 post = position + (alongX ? Vector3.right : Vector3.forward) * (Mathf.Lerp(-length * .5f, length * .5f, t));
                AddShellBox(world, "RailPost", post + Vector3.up * .58f,
                    new Vector3(.09f, 1.16f, .09f), DarkSurfaceMaterial());
            }
        }

        private static Material wallSurfaceMaterial, ceilingSurfaceMaterial, darkSurfaceMaterial;

        private static Material UnlitSurfaceMaterial(string path, string name, Color color, string sourcePath)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source != null && source.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            }
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Material WallSurfaceMaterial()
        {
            if (wallSurfaceMaterial == null)
                wallSurfaceMaterial = UnlitSurfaceMaterial(
                    "Assets/Game/ModernLabTrials/Materials/MLT_WallSurface.mat", "MLT_WallSurface",
                    new Color(.68f, .73f, .76f, 1f),
                    "Assets/Game/Environment/ModernLab/Materials/Lab_White_Coursed.mat");
            return wallSurfaceMaterial;
        }

        private static Material CeilingSurfaceMaterial()
        {
            if (ceilingSurfaceMaterial == null)
                ceilingSurfaceMaterial = UnlitSurfaceMaterial(
                    "Assets/Game/ModernLabTrials/Materials/MLT_CeilingSurface.mat", "MLT_CeilingSurface",
                    new Color(.52f, .58f, .62f, 1f),
                    "Assets/Game/Environment/ModernLab/Materials/Lab_Ceiling_Acoustic.mat");
            return ceilingSurfaceMaterial;
        }

        private static Material DarkSurfaceMaterial()
        {
            if (darkSurfaceMaterial == null)
                darkSurfaceMaterial = UnlitSurfaceMaterial(
                    "Assets/Game/ModernLabTrials/Materials/MLT_DarkSurface.mat", "MLT_DarkSurface",
                    new Color(.34f, .38f, .41f, 1f),
                    "Assets/Game/Environment/ModernLab/Materials/Lab_Dark_Panel.mat");
            return darkSurfaceMaterial;
        }

        private static void AddShellBox(Transform world, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(world, false);
            box.transform.position = position;
            box.transform.localScale = size;
            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddRoomShell(Transform world, Room room, float height)
        {
            float width = room.maxX - room.minX, depth = room.maxZ - room.minZ;
            Material wall = WallSurfaceMaterial();
            AddShellBox(world, "ShellSouth", new Vector3(0f, height * .5f, room.minZ - .45f),
                new Vector3(width + 2f, height, .4f), wall);
            AddShellBox(world, "ShellNorth", new Vector3(0f, height * .5f, room.maxZ + .45f),
                new Vector3(width + 2f, height, .4f), wall);
            AddShellBox(world, "ShellWest", new Vector3(room.minX - .45f, height * .5f, 0f),
                new Vector3(.4f, height, depth + 2f), wall);
            AddShellBox(world, "ShellEast", new Vector3(room.maxX + .45f, height * .5f, 0f),
                new Vector3(.4f, height, depth + 2f), wall);
            AddShellBox(world, "ShellCeiling", new Vector3(0f, height + .25f, 0f),
                new Vector3(width + 2f, .4f, depth + 2f), CeilingSurfaceMaterial());
        }

        private static void AddMezzanineShell(Transform world, Rect area, float y)
        {
            AddShellBox(world, "MezzanineUnder", new Vector3(area.center.x, y - .12f, area.center.y),
                new Vector3(area.width, .2f, area.height), WallSurfaceMaterial());
            float[] xs = { area.xMin + .7f, area.xMax - .7f };
            float[] zs = { area.yMin + .7f, area.yMax - .7f };
            foreach (float x in xs)
            foreach (float z in zs)
                AddShellBox(world, "MezzanineColumn", new Vector3(x, y * .5f, z),
                    new Vector3(.28f, y, .28f), WallSurfaceMaterial());
        }

        private static void AddFloorSurface(Transform world, Vector3 center, float y)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "FloorSurface";
            plane.transform.SetParent(world, false);
            plane.transform.position = new Vector3(center.x, y + .012f, center.z);
            plane.transform.localScale = new Vector3(.6f, 1f, .6f);
            Collider collider = plane.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = FloorSurfaceMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static ResonatorTuning enemyTuning;

        private static ResonatorTuning ModernLabEnemyTuning()
        {
            if (enemyTuning != null) return enemyTuning;
            enemyTuning = AssetDatabase.LoadAssetAtPath<ResonatorTuning>(EnemyTuningPath);
            if (enemyTuning == null)
            {
                AssetDatabase.CopyAsset(DefaultEnemyTuning, EnemyTuningPath);
                AssetDatabase.SaveAssets();
                enemyTuning = AssetDatabase.LoadAssetAtPath<ResonatorTuning>(EnemyTuningPath);
            }
            if (enemyTuning != null)
            {
                enemyTuning.attackEnterDistance = 2.2f;
                enemyTuning.minimumSeparation = 1.5f;
                EditorUtility.SetDirty(enemyTuning);
            }
            return enemyTuning;
        }

        private static Material floorSurfaceMaterial;

        private static Material FloorSurfaceMaterial()
        {
            if (floorSurfaceMaterial != null) return floorSurfaceMaterial;
            const string path = "Assets/Game/ModernLabTrials/Materials/MLT_FloorSurface.mat";
            floorSurfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (floorSurfaceMaterial != null) return floorSurfaceMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            floorSurfaceMaterial = new Material(shader) { name = "MLT_FloorSurface" };
            floorSurfaceMaterial.SetColor("_BaseColor", new Color(.42f, .48f, .52f, 1f));
            Material checker = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/Environment/ModernLab/Materials/Lab_Floor_Checker.mat");
            if (checker != null && checker.HasProperty("_BaseMap"))
            {
                floorSurfaceMaterial.SetTexture("_BaseMap", checker.GetTexture("_BaseMap"));
                floorSurfaceMaterial.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            }
            AssetDatabase.CreateAsset(floorSurfaceMaterial, path);
            AssetDatabase.SaveAssets();
            return floorSurfaceMaterial;
        }

        private static void ApplyFloorMaterial(GameObject floor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material == null) return;
            foreach (MeshRenderer renderer in floor.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = material;
        }

        private static void AddStairs(Transform world, Vector3 position, float yaw)
        {
            Visual(world, StairsPrefab, position, Quaternion.Euler(0, yaw, 0), "Stairs");
        }

        private static void AddLighting(Transform world, Room room, Color tint)
        {
            RenderSettings.ambientLight = new Color(.34f, .4f, .46f);
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = false;
            GameObject sun = new GameObject("Lab Directional Light");
            sun.transform.SetParent(world, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light directional = sun.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(.72f, .82f, .92f);
            directional.intensity = 1.05f;
            directional.shadows = LightShadows.Soft;

            GameObject lowObject = new GameObject("Lab Low Fill");
            lowObject.transform.SetParent(world, false);
            lowObject.transform.position = new Vector3(0f, 2.1f, 0f);
            Light low = lowObject.AddComponent<Light>();
            low.type = LightType.Point;
            low.color = Color.Lerp(tint, Color.white, .35f);
            low.range = Mathf.Max(room.maxX - room.minX, room.maxZ - room.minZ) * 1.15f;
            low.intensity = 3.2f;
            low.shadows = LightShadows.None;

            GameObject probeObject = new GameObject("Lab Reflection Probe");
            probeObject.transform.SetParent(world, false);
            probeObject.transform.position = new Vector3(0f, room.height * .5f, 0f);
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = 128;
            probe.size = new Vector3(room.maxX - room.minX + 2f, room.height, room.maxZ - room.minZ + 2f);
            probe.nearClipPlane = .1f;
            probe.farClipPlane = 120f;
            probe.intensity = 1f;
            probe.importance = 1;

            GameObject fillObject = new GameObject("Lab Fill Light");
            fillObject.transform.SetParent(world, false);
            fillObject.transform.position = new Vector3(0f, 3.6f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = tint;
            fill.range = Mathf.Max(room.maxX - room.minX, room.maxZ - room.minZ) * .9f;
            fill.intensity = 3.6f;
            fill.shadows = LightShadows.None;

            float xStart = room.minX + 6f, xEnd = room.maxX - 6f;
            float zStart = room.minZ + 6f, zEnd = room.maxZ - 6f;
            for (float x = xStart; x <= xEnd; x += 8f)
            for (float z = zStart; z <= zEnd; z += 8f)
            {
                GameObject lightObject = new GameObject("Lab Ceiling Light");
                lightObject.transform.SetParent(world, false);
                lightObject.transform.position = new Vector3(x, room.height - 1.1f, z);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = tint;
                light.range = 12f;
                light.intensity = 2.4f;
                light.shadows = LightShadows.None;
            }
        }

        // ------------------------------------------------------------------ gameplay objects

        private static ImpactReceiver AddReceiver(Transform world, Room room, Side side, float along, float height)
        {
            Vector3 position;
            float yaw;
            switch (side)
            {
                case Side.North:
                    position = new Vector3(along, height, room.maxZ - .2f); yaw = 180f; break;
                case Side.South:
                    position = new Vector3(along, height, room.minZ + .2f); yaw = 0f; break;
                case Side.East:
                    position = new Vector3(room.maxX - .2f, height, along); yaw = -90f; break;
                default:
                    position = new Vector3(room.minX + .2f, height, along); yaw = 90f; break;
            }
            GameObject panel = Visual(world, AccentPrefab, position, Quaternion.Euler(0, yaw, 0), "ImpactReceiver");
            BoxCollider collider = panel.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.8f, 2.6f, .3f);
            collider.center = Vector3.zero;
            ImpactReceiver receiver = panel.AddComponent<ImpactReceiver>();
            GameObject lightObject = new GameObject("Receiver Light");
            lightObject.transform.SetParent(panel.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, .8f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 1.6f;
            receiver.Configure(light, panel.GetComponentsInChildren<Renderer>());
            return receiver;
        }

        private static void AddCrate(Transform world, Vector3 position, ImpactReceiver receiver)
        {
            GameObject crate = Prop(world, CratePrefab, position, Quaternion.identity, "ImpactCrate");
            Rigidbody body = crate.GetComponent<Rigidbody>();
            if (body == null) body = crate.AddComponent<Rigidbody>();
            crate.AddComponent<ImpactCrate>();
        }

        private static ResonatorEnemy AddEnemy(Transform world, Vector3 position, ResonatorTarget target)
        {
            Transform parent = world;
            GameObject enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"),
                world.gameObject.scene);
            enemyObject.name = "ResonatorAgent";
            enemyObject.transform.SetParent(parent, true);
            enemyObject.transform.position = position;
            ResonatorEnemy enemy = enemyObject.GetComponent<ResonatorEnemy>();
            enemy.initialActivity = RestActivity.RestlessIdle;
            enemy.tuning = ModernLabEnemyTuning();
            enemy.target = target;
            enemy.gameObject.SetActive(true);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            return enemy;
        }

        private static void AddEnemy(LevelContext context, Vector3 position)
        {
            ResonatorEnemy enemy = AddEnemy(context.World, position, context.PlayerTarget);
            if (enemy != null) context.Enemies.Add(enemy);
        }

        private static NeutralDrone AddDrone(Transform world, Vector3 home, Vector3? lift,
            Vector3[] route, string number, float attackCooldown = 3.6f, float attackDamage = 9f,
            bool approachPlayer = true)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefab);
            Vector3 droneLiftOffset = Vector3.up * .2f;
            home += droneLiftOffset;
            if (lift.HasValue) lift = lift.Value + droneLiftOffset;
            for (int i = 0; i < route.Length; i++) route[i] += droneLiftOffset;

            GameObject droneObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world.gameObject.scene);
            droneObject.transform.localScale = Vector3.one * .6f;
            droneObject.name = "NeutralDrone_" + number;
            droneObject.transform.SetParent(world, true);
            droneObject.transform.position = home;
            NeutralDrone drone = droneObject.GetComponent<NeutralDrone>();
            Transform homePoint = NewPoint(world, "DroneHome", home);
            Transform liftPoint = lift.HasValue ? NewPoint(world, "DroneLiftTop", lift.Value) : null;
            Transform[] points = new Transform[route.Length];
            for (int i = 0; i < route.Length; i++)
                points[i] = NewPoint(world, "DronePatrol" + i, route[i]);
            drone.Configure(homePoint, liftPoint, points, number);
            drone.ConfigureAttack(true, attackCooldown, attackDamage);
            drone.ConfigureApproach(approachPlayer);
            return drone;
        }

        private static CrystalGrowthSite AddGrowthSite(Transform world, Vector3 position, float yaw)
        {
            GameObject site = Prop(world, "Assets/Game/CrystalParkour/Prefabs/CrystalGrowth_Ground.prefab",
                position, Quaternion.Euler(0f, yaw, 0f), "CrystalGrowthSite");
            CrystalGrowthSite growth = site.GetComponent<CrystalGrowthSite>();
            if (growth == null)
                throw new InvalidOperationException("CrystalGrowth_Ground prefab is missing CrystalGrowthSite.");
            return growth;
        }

        private static void AddPillar(Transform world, Vector3 position, Vector3 size)
        {
            AddShellBox(world, "ArenaPillar", position, size, WallSurfaceMaterial());
            AddBox(world, "ArenaPillarCollider", position, size);
        }

        // The 12 m slot in ML-02 otherwise renders as an unlit black rectangle;
        // a dark liner turns it into a readable shaft and the two hazard lights
        // mark the edge during play.
        private static void AddPitLiner(Transform world, float halfWidth, float depth)
        {
            Material dark = DarkSurfaceMaterial();
            AddShellBox(world, "PitWallWest", new Vector3(-halfWidth - .1f, -5f, 0f), new Vector3(.3f, 10f, depth), dark);
            AddShellBox(world, "PitWallEast", new Vector3(halfWidth + .1f, -5f, 0f), new Vector3(.3f, 10f, depth), dark);
            AddShellBox(world, "PitWallSouth", new Vector3(0f, -5f, -depth * .5f), new Vector3(halfWidth * 2f, 10f, .3f), dark);
            AddShellBox(world, "PitWallNorth", new Vector3(0f, -5f, depth * .5f), new Vector3(halfWidth * 2f, 10f, .3f), dark);
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject hazard = new GameObject("Pit Hazard Light");
                hazard.transform.SetParent(world, false);
                hazard.transform.position = new Vector3(halfWidth * .62f * i, -1.2f, 0f);
                Light light = hazard.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, .42f, .28f);
                light.range = 11f;
                light.intensity = 1.5f;
                light.shadows = LightShadows.None;
            }
        }

        private static Transform NewPoint(Transform world, string name, Vector3 position)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(world, false);
            point.transform.position = position;
            return point.transform;
        }

        private static ExitAssembly AddExit(Transform world, Vector3 position, float yaw, TrialLevel level, bool final)
        {
            GameObject doorPanel = Visual(world, AccentPrefab, position, Quaternion.Euler(0, yaw, 0), "ExitDoor");
            BoxCollider doorCollider = doorPanel.AddComponent<BoxCollider>();
            doorCollider.size = new Vector3(2.9f, 2.9f, .3f);
            TrialDoor door = doorPanel.AddComponent<TrialDoor>();
            GameObject sign = Visual(world, ExitSignPrefab, position + Vector3.up * 2.1f,
                Quaternion.Euler(0, yaw, 0), "ExitSign");
            foreach (Collider c in sign.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            GameObject point = new GameObject("ExitTrigger");
            point.transform.SetParent(world, false);
            point.transform.position = position + new Vector3(0f, .1f, 0f);
            BoxCollider trigger = point.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3.4f, 3.2f, 3.4f);
            TrialExit exit = point.AddComponent<TrialExit>();
            exit.SetLevel(level);
            return new ExitAssembly { Door = door, Point = point.transform };
        }

        private struct ExitAssembly
        {
            public TrialDoor Door;
            public Transform Point;
        }

        private enum Side { North, South, East, West }

        // ------------------------------------------------------------------ prefab helpers

        private static GameObject Visual(Transform parent, string path, Vector3 position, Quaternion rotation, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            return go;
        }

        private static GameObject Prop(Transform parent, string path, Vector3 position, Quaternion rotation, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        private static BoxCollider AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }
    }
}
