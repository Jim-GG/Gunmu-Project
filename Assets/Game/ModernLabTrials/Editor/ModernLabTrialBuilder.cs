using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            BuildPlayerPrefab();
            BuildDronePrefab();

            BuildLevel01();
            BuildLevel02();
            BuildLevel03();
            BuildLevel04();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MODERNLAB_TRIALS_BUILD: four levels built. Open MLT_01_ImpactCalibration and press Play.");
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
            LevelContext context = Begin("MLT_01_ImpactCalibration", "ML-01", "冲击校准",
                "用冲量把载荷送进接收器，再清除守卫；最后借无人机升上出口高台。", "MLT_02_CrosswindGallery",
                new Vector3(0f, .1f, -7f), 0f);
            Room room = BuildRoom(context.World, 4, 3, 8f, null);
            BuildMezzanine(context.World, new Rect(-9f, 3f, 18f, 6f), 4f);
            AddRail(context.World, new Vector3(-5.5f, 4f, 3f), 7f, true);
            AddRail(context.World, new Vector3(5.5f, 4f, 3f), 7f, true);
            AddRail(context.World, new Vector3(-9f, 4f, 6f), 6f, false);
            AddStairs(context.World, new Vector3(9f, 0f, -4f), 180f);
            AddLighting(context.World, room, new Color(.42f, .64f, .9f));
            BakeNavMesh(context, "MLT_01_ImpactCalibration");

            ImpactReceiver receiver = AddReceiver(context.World, room, Side.East, 0f, 1.5f);
            AddCrate(context.World, new Vector3(4f, .55f, 0f), receiver);
            AddCrate(context.World, new Vector3(1f, .55f, -2f), receiver);
            AddEnemy(context, new Vector3(-5f, .1f, -1f));
            AddEnemy(context, new Vector3(5f, .1f, 5f));

            NeutralDrone drone = AddDrone(context.World, new Vector3(0f, .35f, 1.4f),
                new Vector3(0f, 4.35f, 1.4f), new[]
                {
                    new Vector3(-3f, .35f, 0f), new Vector3(3f, .35f, 0f),
                }, "MCU-7A / 01");
            ExitAssembly exit = AddExit(context.World, new Vector3(0f, 4f, 8.2f), 180f, context.Level, false);

            context.Level.receivers = new[] { receiver };
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_01_ImpactCalibration");
        }

        private static void BuildLevel02()
        {
            LevelContext context = Begin("MLT_02_CrosswindGallery", "ML-02", "横风长廊",
                "长廊中央被切开：借编号无人机越过缺口，同时用碰撞打开两侧接收器并清场。", "MLT_03_VerticalShaft",
                new Vector3(-15f, .1f, -2f), 90f);
            Room room = BuildRoom(context.World, 6, 2, 8f,
                center => Mathf.Abs(center.x) < 6.1f);
            AddRail(context.World, new Vector3(-6f, 0f, -5.7f), 12f, false);
            AddRail(context.World, new Vector3(6f, 0f, -5.7f), 12f, false);
            AddRail(context.World, new Vector3(-6f, 0f, 5.7f), 12f, false);
            AddRail(context.World, new Vector3(6f, 0f, 5.7f), 12f, false);
            AddStairs(context.World, new Vector3(-16f, 0f, 4f), 0f);
            AddLighting(context.World, room, new Color(.34f, .7f, .92f));
            BakeNavMesh(context, "MLT_02_CrosswindGallery");

            ImpactReceiver northA = AddReceiver(context.World, room, Side.North, -10f, 1.5f);
            ImpactReceiver northB = AddReceiver(context.World, room, Side.North, 10f, 1.5f);
            AddCrate(context.World, new Vector3(-11f, .55f, -.5f), northA);
            AddCrate(context.World, new Vector3(11f, .55f, -.5f), northB);
            AddEnemy(context, new Vector3(-13f, .1f, 2.5f));
            AddEnemy(context, new Vector3(13f, .1f, 2.5f));
            AddEnemy(context, new Vector3(15f, .1f, -2.5f));

            NeutralDrone drone = AddDrone(context.World, new Vector3(-6f, .6f, 0f), null,
                new[]
                {
                    new Vector3(-7f, .6f, 0f), new Vector3(7f, .6f, 0f),
                }, "MCU-7A / 02");
            ExitAssembly exit = AddExit(context.World, new Vector3(17f, 0f, 0f), -90f, context.Level, false);

            context.Level.receivers = new[] { northA, northB };
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_02_CrosswindGallery");
        }

        private static void BuildLevel03()
        {
            LevelContext context = Begin("MLT_03_VerticalShaft", "ML-03", "垂直竖井",
                "三层竖井只留下断梯；用无人机把实验员送上西侧高台，并从三个方向完成碰撞校准。", "MLT_04_SynthesisCore",
                new Vector3(0f, .1f, -7f), 0f);
            Room room = BuildRoom(context.World, 4, 4, 8f, null);
            BakeNavMesh(context, "MLT_03_VerticalShaft");
            AddLighting(context.World, room, new Color(.48f, .74f, 1f));

            ImpactReceiver north = AddReceiver(context.World, room, Side.North, -4f, 1.5f);
            ImpactReceiver east = AddReceiver(context.World, room, Side.East, -4f, 1.5f);
            ImpactReceiver south = AddReceiver(context.World, room, Side.South, 4f, 1.5f);
            AddCrate(context.World, new Vector3(-4f, .55f, 8f), north);
            AddCrate(context.World, new Vector3(8f, .55f, -4f), east);
            AddCrate(context.World, new Vector3(4f, .55f, -8f), south);
            AddEnemy(context, new Vector3(-8f, .1f, -4f));
            AddEnemy(context, new Vector3(0f, .1f, 5f));
            AddEnemy(context, new Vector3(8f, .1f, 4f));

            BuildMezzanine(context.World, new Rect(-12f, -10f, 6f, 20f), 4f);
            AddRail(context.World, new Vector3(-6f, 4f, -6.5f), 7f, false);
            AddRail(context.World, new Vector3(-6f, 4f, 6.5f), 7f, false);
            AddRail(context.World, new Vector3(-9f, 4f, -10f), 6f, true);
            AddRail(context.World, new Vector3(-9f, 4f, 10f), 6f, true);
            AddStairs(context.World, new Vector3(9f, 0f, -8f), 180f);
            AddStairs(context.World, new Vector3(9f, 0f, 8f), 0f);

            NeutralDrone drone = AddDrone(context.World, new Vector3(0f, .35f, 0f),
                new Vector3(-4.4f, 4.35f, 0f), new[]
                {
                    new Vector3(-2f, .35f, -3f), new Vector3(2f, .35f, 3f),
                }, "MCU-7A / 03");
            ExitAssembly exit = AddExit(context.World, new Vector3(-9f, 4f, 8f), 180f, context.Level, false);

            context.Level.receivers = new[] { north, east, south };
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_03_VerticalShaft");
        }

        private static void BuildLevel04()
        {
            LevelContext context = Begin("MLT_04_SynthesisCore", "ML-04", "合成核心",
                "中央塌陷成巨坑；两侧碰撞接收器与四名共鸣者共同封锁核心。借无人机越过深渊，完成全部记录。", "",
                new Vector3(0f, .1f, -11f), 0f);
            context.Level.finalLevel = true;
            Room room = BuildRoom(context.World, 5, 4, 8f,
                center => Mathf.Abs(center.x) < 6.1f && Mathf.Abs(center.z) < 6.1f);
            BakeNavMesh(context, "MLT_04_SynthesisCore");
            AddLighting(context.World, room, new Color(.55f, .78f, 1f));

            ImpactReceiver west = AddReceiver(context.World, room, Side.West, 0f, 1.5f);
            ImpactReceiver east = AddReceiver(context.World, room, Side.East, 0f, 1.5f);
            AddCrate(context.World, new Vector3(-10.5f, .55f, -1f), west);
            AddCrate(context.World, new Vector3(10.5f, .55f, -1f), east);
            AddEnemy(context, new Vector3(-11f, .1f, -8f));
            AddEnemy(context, new Vector3(11f, .1f, -8f));
            AddEnemy(context, new Vector3(-11f, .1f, 8f));
            AddEnemy(context, new Vector3(11f, .1f, 8f));

            BuildMezzanine(context.World, new Rect(-6f, -6f, 12f, 12f), 5f);
            AddRail(context.World, new Vector3(-6f, 5f, 0f), 8f, false);
            AddRail(context.World, new Vector3(6f, 5f, 0f), 8f, false);
            AddRail(context.World, new Vector3(0f, 5f, 6f), 8f, true);
            AddStairs(context.World, new Vector3(-12f, 0f, -10f), 0f);

            NeutralDrone drone = AddDrone(context.World, new Vector3(0f, .35f, -9f),
                new Vector3(0f, 5.35f, -7.2f), new[]
                {
                    new Vector3(-6f, .35f, -9f), new Vector3(6f, .35f, -9f),
                }, "MCU-7A / 04");
            ExitAssembly exit = AddExit(context.World, new Vector3(0f, 5f, 2.8f), 180f, context.Level, true);

            context.Level.receivers = new[] { west, east };
            context.Level.enemies = context.Enemies.ToArray();
            context.Level.drones = new[] { drone };
            context.Level.exitDoor = exit.Door;
            context.Level.exitPoint = exit.Point;
            context.Level.spawn = context.Spawn;
            Finish(context, "MLT_04_SynthesisCore");
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
            level.firstSceneName = "MLT_01_ImpactCalibration";
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
                "Assets/Game/ModernLabTrials/Scenes/MLT_01_ImpactCalibration.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_02_CrosswindGallery.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_03_VerticalShaft.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_04_SynthesisCore.unity",
            };
            var list = mine.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (!mine.Contains(existing.path))
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
        }

        private static void AddRail(Transform world, Vector3 position, float length, bool alongX)
        {
            Quaternion rotation = alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
            GameObject rail = Visual(world, RailPrefab, position, rotation, "Rail");
            float stretch = Mathf.Max(.2f, length / 6f);
            Vector3 scale = rail.transform.localScale;
            rail.transform.localScale = alongX ? new Vector3(stretch, scale.y, scale.z) : new Vector3(scale.x, scale.y, stretch);
            AddBox(world, "RailCollider", position + Vector3.up * .6f,
                alongX ? new Vector3(length, 1.2f, .12f) : new Vector3(.12f, 1.2f, length));
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
            Vector3[] route, string number)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefab);
            GameObject droneObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world.gameObject.scene);
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
            return drone;
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
