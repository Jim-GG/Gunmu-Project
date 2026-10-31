using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneSplitterEditor
{
    private const string Level1Path = "Assets/Scenes/Level1_Billiards.unity";
    private const string Level2Path = "Assets/Scenes/Level2_Pendulum.unity";
    private const string Level3Path = "Assets/Scenes/Level3_Seesaw.unity";
    private const string Level4Path = "Assets/Scenes/Level4_MomentumRedirect.unity";
    private const string Level5Path = "Assets/Scenes/Level5_ChainReaction.unity";
    private const string SessionKey = "Gunmu.ScenesSplitDone_v6";

    static SceneSplitterEditor()
    {
        EditorApplication.delayCall += RunOnceOnLoad;
    }

    private static void RunOnceOnLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureBuildSettings();

        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        AssetDatabase.Refresh();

        CleanSceneFile(Level1Path, keepRoot: "BilliardPuzzle_Level", removeRoot: "PendulumPuzzle_Level2", playerPos: new Vector3(0f, 0.1f, 0f));
        CleanSceneFile(Level2Path, keepRoot: "PendulumPuzzle_Level2", removeRoot: "BilliardPuzzle_Level", playerPos: new Vector3(0f, 4.1f, -4.5f));
        BuildLevel3Scene(Level3Path);
        BuildLevel4Scene(Level4Path);
        BuildLevel5Scene(Level5Path);

        EnsureBuildSettings();

        // Open Level 5 by default so the user can test it immediately
        EditorSceneManager.OpenScene(Level5Path, OpenSceneMode.Single);
        Debug.Log("[SceneSplitterEditor] Built Level5_ChainReaction.unity and registered all 5 scenes in Build Settings!");
    }

    private static void CleanSceneFile(string scenePath, string keepRoot, string removeRoot, Vector3 playerPos)
    {
        if (!System.IO.File.Exists(scenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == removeRoot || root.name == "RedCube" || root.name == "SeesawPuzzle_Level3" || root.name == "RedirectPuzzle_Level4" || root.name == "ChainPuzzle_Level5")
            {
                Object.DestroyImmediate(root);
            }
            else if (root.name == keepRoot)
            {
                root.SetActive(true);
                root.transform.position = Vector3.zero;

                Transform sw = root.transform.Find("PuzzleSwitch_East");
                if (sw != null)
                {
                    sw.localPosition = new Vector3(13f, 4.5f, 0f);
                    sw.localScale = new Vector3(0.6f, 4.6f, 2.6f);
                }

                Transform gate = root.transform.Find("VictoryGate_East");
                if (gate != null)
                {
                    gate.localPosition = new Vector3(13f, 7.5f, 0f);
                }

                Transform pivot = root.transform.Find("PendulumPivot");
                if (pivot != null)
                {
                    pivot.localPosition = new Vector3(-3.8f, 9.5f, 0f);
                }

                Transform topBar = root.transform.Find("Gantry_TopBar");
                if (topBar != null)
                {
                    topBar.localPosition = new Vector3(-3.8f, 9.8f, 0.7f);
                }

                Transform westPillar = root.transform.Find("Gantry_WestPillar");
                if (westPillar != null)
                {
                    westPillar.localPosition = new Vector3(-9.8f, 5f, 1.4f);
                }

                Transform eastPillar = root.transform.Find("Gantry_EastPillar");
                if (eastPillar != null)
                {
                    eastPillar.localPosition = new Vector3(2.2f, 5f, 1.4f);
                }
            }
            else if (root.name == "Player")
            {
                root.transform.position = playerPos;
                root.transform.rotation = Quaternion.identity;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildLevel3Scene(string scenePath)
    {
        if (!System.IO.File.Exists(scenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "BilliardPuzzle_Level" ||
                root.name == "PendulumPuzzle_Level2" ||
                root.name == "SeesawPuzzle_Level3" ||
                root.name == "RedirectPuzzle_Level4" ||
                root.name == "ChainPuzzle_Level5" ||
                root.name == "RedCube")
            {
                Object.DestroyImmediate(root);
            }
            else if (root.name == "Ground")
            {
                root.transform.position = Vector3.zero;
                root.transform.localScale = new Vector3(6f, 1f, 6f);
            }
            else if (root.name == "Player")
            {
                root.transform.position = new Vector3(-1.5f, 8.1f, -4.8f);
                root.transform.rotation = Quaternion.identity;
            }
        }

        GameObject levelRoot = new GameObject("SeesawPuzzle_Level3");

        CreateBox("ChamberWall_North", levelRoot.transform, new Vector3(0f, 12f, 10f), new Vector3(36f, 24f, 1f), new Color(0.22f, 0.24f, 0.28f));
        CreateBox("ChamberWall_West", levelRoot.transform, new Vector3(-18f, 12f, 0f), new Vector3(1f, 24f, 22f), new Color(0.22f, 0.24f, 0.28f));
        CreateBox("ChamberWall_East", levelRoot.transform, new Vector3(18f, 12f, 0f), new Vector3(1f, 24f, 22f), new Color(0.22f, 0.24f, 0.28f));

        CreateBox("SpawnPlatform_Pillar", levelRoot.transform, new Vector3(0f, 3.8f, -5.8f), new Vector3(4f, 7.6f, 4f), new Color(0.3f, 0.32f, 0.36f));
        CreateBox("SpawnPlatform", levelRoot.transform, new Vector3(0f, 7.6f, -5.5f), new Vector3(9f, 0.8f, 5f), new Color(0.45f, 0.48f, 0.52f));

        CreateBox("HighGoalPlatform", levelRoot.transform, new Vector3(-1.8f, 17.6f, -6.5f), new Vector3(9f, 0.8f, 5f), new Color(0.35f, 0.55f, 0.65f));
        CreateBox("VictoryGate_East", levelRoot.transform, new Vector3(-1.8f, 20.2f, -7.8f), new Vector3(4f, 1.5f, 0.6f), new Color(0.2f, 0.9f, 0.4f));

        GameObject goalSwitch = CreateBox("PuzzleSwitch_HighGoal", levelRoot.transform, new Vector3(-1.8f, 18.25f, -6.2f), new Vector3(3.6f, 0.5f, 2.8f), new Color(1f, 0.35f, 0.1f));
        goalSwitch.AddComponent<PuzzleSwitch>();

        GameObject seesawRoot = new GameObject("SeesawRoot");
        seesawRoot.transform.SetParent(levelRoot.transform, false);
        seesawRoot.transform.localPosition = Vector3.zero;

        CreateBox("SeesawFulcrum", seesawRoot.transform, new Vector3(0f, 0.6f, -1.6f), new Vector3(0.9f, 1.2f, 1.5f), new Color(0.3f, 0.32f, 0.35f));
        GameObject plank = CreateBox("SeesawPlank", seesawRoot.transform, new Vector3(0f, 1.35f, -1.6f), new Vector3(6.2f, 0.32f, 1.4f), new Color(0.75f, 0.45f, 0.2f));
        plank.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);

        GameObject launchBoxObj = CreateBox("KineticBox_L3", levelRoot.transform, new Vector3(-2.2f, 1.15f, -1.6f), new Vector3(1.1f, 1.1f, 1.1f), new Color(0.95f, 0.75f, 0.15f));
        launchBoxObj.AddComponent<Rigidbody>();
        launchBoxObj.AddComponent<KineticBox>();

        CreateBox("CraneTower", levelRoot.transform, new Vector3(5.2f, 9.5f, -1.6f), new Vector3(0.9f, 19f, 0.9f), new Color(0.28f, 0.3f, 0.35f));
        CreateBox("CraneBoom", levelRoot.transform, new Vector3(3.5f, 18.6f, -1.6f), new Vector3(4.4f, 0.7f, 0.9f), new Color(0.28f, 0.3f, 0.35f));
        CreateBox("CraneCableVisual", levelRoot.transform, new Vector3(2.2f, 17.5f, -1.6f), new Vector3(0.15f, 1.6f, 0.15f), new Color(0.5f, 0.5f, 0.5f));

        CreateBox("CraneWeightBox", levelRoot.transform, new Vector3(2.2f, 16.0f, -1.6f), new Vector3(1.15f, 1.15f, 1.15f), new Color(0.8f, 0.2f, 0.22f));

        seesawRoot.AddComponent<SeesawMechanism>();

        CreateBox("ButtonPedestal_L3", levelRoot.transform, new Vector3(1.6f, 8.25f, -3.6f), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.35f));
        GameObject btnObj = CreateBox("CraneButton", levelRoot.transform, new Vector3(1.6f, 8.65f, -3.6f), new Vector3(0.45f, 0.35f, 0.45f), new Color(1f, 0.55f, 0.1f));
        btnObj.GetComponent<BoxCollider>().isTrigger = true;
        btnObj.AddComponent<PendulumButton>();

        GameObject crystalObj = CreateBox("DashCrystal_L3", levelRoot.transform, new Vector3(-3.2f, 8.9f, -4.2f), new Vector3(0.45f, 0.45f, 0.45f), new Color(0.1f, 0.9f, 1f));
        crystalObj.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        crystalObj.GetComponent<BoxCollider>().isTrigger = true;
        crystalObj.AddComponent<DashCrystal>();

        GameObject airCrystal = CreateBox("DashCrystal_Apex", levelRoot.transform, new Vector3(-2.2f, 18.8f, -2.6f), new Vector3(0.5f, 0.5f, 0.5f), new Color(0.1f, 0.9f, 1f));
        airCrystal.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        airCrystal.GetComponent<BoxCollider>().isTrigger = true;
        airCrystal.AddComponent<DashCrystal>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildLevel4Scene(string scenePath)
    {
        if (!System.IO.File.Exists(scenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "BilliardPuzzle_Level" ||
                root.name == "PendulumPuzzle_Level2" ||
                root.name == "SeesawPuzzle_Level3" ||
                root.name == "RedirectPuzzle_Level4" ||
                root.name == "ChainPuzzle_Level5" ||
                root.name == "RedCube")
            {
                Object.DestroyImmediate(root);
            }
            else if (root.name == "Ground")
            {
                root.transform.position = new Vector3(0f, 0f, 6f);
                root.transform.localScale = new Vector3(6f, 1f, 8f);
            }
            else if (root.name == "Player")
            {
                root.transform.position = new Vector3(-2.2f, 8.1f, -4.6f);
                root.transform.rotation = Quaternion.identity;
            }
        }

        GameObject levelRoot = new GameObject("RedirectPuzzle_Level4");

        CreateBox("CanyonWall_West", levelRoot.transform, new Vector3(-18f, 12f, 6f), new Vector3(1f, 24f, 36f), new Color(0.22f, 0.24f, 0.28f));
        CreateBox("CanyonWall_East", levelRoot.transform, new Vector3(18f, 12f, 6f), new Vector3(1f, 24f, 36f), new Color(0.22f, 0.24f, 0.28f));
        CreateBox("CanyonWall_NorthBack", levelRoot.transform, new Vector3(0f, 12f, 22f), new Vector3(36f, 24f, 1f), new Color(0.22f, 0.24f, 0.28f));

        CreateBox("SpawnPlatform_Pillar", levelRoot.transform, new Vector3(0f, 3.8f, -5.8f), new Vector3(4f, 7.6f, 4f), new Color(0.3f, 0.32f, 0.36f));
        CreateBox("SpawnPlatform", levelRoot.transform, new Vector3(0f, 7.6f, -5.5f), new Vector3(9f, 0.8f, 5f), new Color(0.45f, 0.48f, 0.52f));
        CreateBox("SpawnEdgeCurb", levelRoot.transform, new Vector3(-2.2f, 8.18f, -3.05f), new Vector3(2.4f, 0.35f, 0.2f), new Color(0.6f, 0.55f, 0.2f));

        CreateBox("OppositePlatform_Pillar", levelRoot.transform, new Vector3(-2.2f, 3.6f, 16.5f), new Vector3(4f, 7.2f, 4f), new Color(0.3f, 0.32f, 0.36f));
        CreateBox("OppositePlatform_North", levelRoot.transform, new Vector3(-2.2f, 7.2f, 16.5f), new Vector3(10f, 0.8f, 6f), new Color(0.35f, 0.55f, 0.65f));

        CreateBox("VictoryGate_East", levelRoot.transform, new Vector3(-2.2f, 11.4f, 16.8f), new Vector3(4.2f, 1.4f, 0.8f), new Color(0.2f, 0.9f, 0.4f));
        GameObject northSwitch = CreateBox("PuzzleSwitch_East", levelRoot.transform, new Vector3(-2.2f, 8.0f, 16.5f), new Vector3(3.6f, 4.4f, 0.6f), new Color(1f, 0.35f, 0.1f));
        northSwitch.AddComponent<PuzzleSwitch>();

        GameObject seesawRoot = new GameObject("SeesawRoot");
        seesawRoot.transform.SetParent(levelRoot.transform, false);
        seesawRoot.transform.localPosition = Vector3.zero;

        CreateBox("SeesawFulcrum", seesawRoot.transform, new Vector3(0f, 0.6f, -1.85f), new Vector3(0.9f, 1.2f, 1.5f), new Color(0.3f, 0.32f, 0.35f));
        GameObject plank = CreateBox("SeesawPlank", seesawRoot.transform, new Vector3(0f, 1.35f, -1.85f), new Vector3(6.2f, 0.32f, 1.4f), new Color(0.75f, 0.45f, 0.2f));
        plank.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);

        GameObject launchBoxObj = CreateBox("KineticBox_L4", levelRoot.transform, new Vector3(-2.2f, 1.15f, -1.85f), new Vector3(1.1f, 1.1f, 1.1f), new Color(0.95f, 0.75f, 0.15f));
        launchBoxObj.AddComponent<Rigidbody>();
        launchBoxObj.AddComponent<KineticBox>();

        CreateBox("CraneTower", levelRoot.transform, new Vector3(5.2f, 9.5f, -1.85f), new Vector3(0.9f, 19f, 0.9f), new Color(0.28f, 0.3f, 0.35f));
        CreateBox("CraneBoom", levelRoot.transform, new Vector3(3.5f, 18.6f, -1.85f), new Vector3(4.4f, 0.7f, 0.9f), new Color(0.28f, 0.3f, 0.35f));
        CreateBox("CraneCableVisual", levelRoot.transform, new Vector3(2.2f, 17.5f, -1.85f), new Vector3(0.15f, 1.6f, 0.15f), new Color(0.5f, 0.5f, 0.5f));

        CreateBox("CraneWeightBox", levelRoot.transform, new Vector3(2.2f, 16.0f, -1.85f), new Vector3(1.15f, 1.15f, 1.15f), new Color(0.8f, 0.2f, 0.22f));

        seesawRoot.AddComponent<SeesawMechanism>();

        CreateBox("ButtonPedestal_L4", levelRoot.transform, new Vector3(1.6f, 8.25f, -3.6f), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.35f));
        GameObject btnObj = CreateBox("CraneButton", levelRoot.transform, new Vector3(1.6f, 8.65f, -3.6f), new Vector3(0.45f, 0.35f, 0.45f), new Color(1f, 0.55f, 0.1f));
        btnObj.GetComponent<BoxCollider>().isTrigger = true;
        btnObj.AddComponent<PendulumButton>();

        GameObject crystalObj = CreateBox("DashCrystal_L4", levelRoot.transform, new Vector3(-3.8f, 8.9f, -4.2f), new Vector3(0.45f, 0.45f, 0.45f), new Color(0.1f, 0.9f, 1f));
        crystalObj.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        crystalObj.GetComponent<BoxCollider>().isTrigger = true;
        crystalObj.AddComponent<DashCrystal>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildLevel5Scene(string scenePath)
    {
        if (!System.IO.File.Exists(scenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "BilliardPuzzle_Level" ||
                root.name == "PendulumPuzzle_Level2" ||
                root.name == "SeesawPuzzle_Level3" ||
                root.name == "RedirectPuzzle_Level4" ||
                root.name == "ChainPuzzle_Level5" ||
                root.name == "RedCube")
            {
                Object.DestroyImmediate(root);
            }
            else if (root.name == "Ground")
            {
                root.transform.position = new Vector3(4f, 0f, 0f);
                root.transform.localScale = new Vector3(8f, 1f, 6f);
            }
            else if (root.name == "Player")
            {
                root.transform.position = new Vector3(-6.5f, 8.1f, -8.2f);
                root.transform.rotation = Quaternion.identity;
            }
        }

        GameObject levelRoot = new GameObject("ChainPuzzle_Level5");

        // 1. Player Spawn Platform (Y = 8.0m, Z in [-10.5, -5.0])
        CreateBox("SpawnPlatform_Pillar", levelRoot.transform, new Vector3(-2.5f, 3.8f, -7.8f), new Vector3(12f, 7.6f, 4.5f), new Color(0.3f, 0.32f, 0.36f));
        CreateBox("SpawnPlatform", levelRoot.transform, new Vector3(-2.5f, 7.6f, -7.8f), new Vector3(15f, 0.8f, 5.6f), new Color(0.45f, 0.48f, 0.52f));

        // 2. Barrier Wall at Z = -5.0m (blocks the 2.0m tall player from crossing, but has a 1.3m low slot at X = -6.5m for the Cue Ball!)
        CreateBox("Barrier_West", levelRoot.transform, new Vector3(-8.6f, 9.2f, -5.0f), new Vector3(2.8f, 2.4f, 0.35f), new Color(0.28f, 0.32f, 0.38f));
        CreateBox("Barrier_CenterEast", levelRoot.transform, new Vector3(-0.35f, 8.65f, -5.0f), new Vector3(10.9f, 1.3f, 0.35f), new Color(0.28f, 0.32f, 0.38f));
        CreateBox("Barrier_SlotTopBar", levelRoot.transform, new Vector3(-3.0f, 9.5f, -5.0f), new Vector3(14f, 0.4f, 0.35f), new Color(0.28f, 0.32f, 0.38f));

        // 3. Cue Ball Guide Track (North from slot Z=-5.0 to Z=-2.4, then East from X=-6.5 to X=-0.8 at Y=8.0m)
        CreateBox("CueTrack_NorthLeg", levelRoot.transform, new Vector3(-6.5f, 7.6f, -3.5f), new Vector3(2.0f, 0.8f, 3.4f), new Color(0.38f, 0.42f, 0.46f));
        CreateBox("CueTrack_EastRunway", levelRoot.transform, new Vector3(-3.6f, 7.6f, -2.4f), new Vector3(5.8f, 0.8f, 1.8f), new Color(0.38f, 0.42f, 0.46f));
        // North Alignment Stopper so when player dashes CueBall North (+Z), it stops at exact (-6.5, 8.5, -2.4) in front of the Pendulum!
        CreateBox("CueBall_NorthStopper", levelRoot.transform, new Vector3(-6.5f, 8.6f, -1.7f), new Vector3(1.8f, 1.2f, 0.4f), new Color(0.25f, 0.3f, 0.38f));

        // 4. The "Cue Ball" Box (母球 - Pearl White KineticBox starting in front of the player at (-6.5, 8.5, -6.2))
        GameObject cueBallObj = CreateBox("CueBallBox", levelRoot.transform, new Vector3(-6.5f, 8.5f, -6.2f), new Vector3(1.0f, 1.0f, 1.0f), new Color(0.95f, 0.96f, 1.0f));
        cueBallObj.AddComponent<Rigidbody>();
        KineticBox cueBallComp = cueBallObj.AddComponent<KineticBox>();
        cueBallComp.SetNormalColor(new Color(0.95f, 0.96f, 1.0f));

        // 5. Giant Pendulum suspended directly above (-6.5, 8.5, -2.4) -> swings West to East (+X) to launch CueBallBox Eastward!
        CreateBox("PendulumGantry_West", levelRoot.transform, new Vector3(-11.5f, 12.0f, -1.2f), new Vector3(0.6f, 8.5f, 0.6f), new Color(0.25f, 0.26f, 0.3f));
        CreateBox("PendulumGantry_East", levelRoot.transform, new Vector3(-2.5f, 12.0f, -1.2f), new Vector3(0.6f, 8.5f, 0.6f), new Color(0.25f, 0.26f, 0.3f));
        CreateBox("PendulumGantry_Top", levelRoot.transform, new Vector3(-7.0f, 16.0f, -1.8f), new Vector3(10.0f, 0.5f, 1.8f), new Color(0.25f, 0.26f, 0.3f));

        GameObject pendulumPivot = new GameObject("PendulumPivot");
        pendulumPivot.transform.SetParent(levelRoot.transform, false);
        pendulumPivot.transform.localPosition = new Vector3(-6.5f, 15.7f, -2.4f);

        CreateBox("PendulumArm", pendulumPivot.transform, new Vector3(0f, -3.6f, 0f), new Vector3(0.25f, 7.2f, 0.25f), new Color(0.5f, 0.52f, 0.55f));
        CreateBox("PendulumHammer", pendulumPivot.transform, new Vector3(0f, -7.2f, 0f), new Vector3(1.1f, 2.2f, 1.0f), new Color(0.85f, 0.2f, 0.25f));
        pendulumPivot.AddComponent<PendulumMechanism>();

        // 6. Seesaw & Overhead Crane at the bottom of the chasm (West end at X = 0.0, East end at X = +4.4, Z = -2.4)
        GameObject seesawRoot = new GameObject("SeesawRoot");
        seesawRoot.transform.SetParent(levelRoot.transform, false);
        seesawRoot.transform.localPosition = Vector3.zero;

        CreateBox("SeesawFulcrum", seesawRoot.transform, new Vector3(2.2f, 0.6f, -2.4f), new Vector3(0.9f, 1.2f, 1.5f), new Color(0.3f, 0.32f, 0.35f));
        GameObject plank = CreateBox("SeesawPlank", seesawRoot.transform, new Vector3(2.2f, 1.35f, -2.4f), new Vector3(6.2f, 0.32f, 1.4f), new Color(0.75f, 0.45f, 0.2f));
        plank.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);

        // Target Box (目标方块 - Gold KineticBox on the West end of the seesaw at (0.0, 1.15, -2.4))
        GameObject targetBoxObj = CreateBox("KineticBox_Target", levelRoot.transform, new Vector3(0.0f, 1.15f, -2.4f), new Vector3(1.1f, 1.1f, 1.1f), new Color(0.95f, 0.75f, 0.15f));
        targetBoxObj.AddComponent<Rigidbody>();
        targetBoxObj.AddComponent<KineticBox>();

        // Overhead Crane & Heavy Drop Weight above the East end (X = +4.4, Z = -2.4)
        CreateBox("CraneTower", levelRoot.transform, new Vector3(7.4f, 9.5f, -2.4f), new Vector3(0.9f, 19f, 0.9f), new Color(0.28f, 0.3f, 0.35f));
        CreateBox("CraneBoom", levelRoot.transform, new Vector3(5.7f, 18.6f, -2.4f), new Vector3(4.4f, 0.7f, 0.9f), new Color(0.28f, 0.3f, 0.35f));
        CreateBox("CraneCableVisual", levelRoot.transform, new Vector3(4.4f, 17.5f, -2.4f), new Vector3(0.15f, 1.6f, 0.15f), new Color(0.5f, 0.5f, 0.5f));
        CreateBox("CraneWeightBox", levelRoot.transform, new Vector3(4.4f, 16.0f, -2.4f), new Vector3(1.15f, 1.15f, 1.15f), new Color(0.8f, 0.2f, 0.22f));

        seesawRoot.AddComponent<SeesawMechanism>();

        // 7. Distant East Target Platform & Switch (X = +19.5, Y = 8.0, Z = -2.4)
        CreateBox("EastGoalPlatform_Pillar", levelRoot.transform, new Vector3(20.0f, 3.6f, -2.4f), new Vector3(4f, 7.2f, 4f), new Color(0.3f, 0.32f, 0.36f));
        CreateBox("EastGoalPlatform", levelRoot.transform, new Vector3(20.0f, 7.2f, -2.4f), new Vector3(6f, 0.8f, 6f), new Color(0.35f, 0.55f, 0.65f));
        CreateBox("VictoryGate_East", levelRoot.transform, new Vector3(18.5f, 11.4f, -2.4f), new Vector3(0.8f, 1.4f, 4.2f), new Color(0.2f, 0.9f, 0.4f));
        GameObject eastSwitch = CreateBox("PuzzleSwitch_East", levelRoot.transform, new Vector3(18.5f, 8.0f, -2.4f), new Vector3(0.6f, 4.4f, 3.6f), new Color(1f, 0.35f, 0.1f));
        eastSwitch.AddComponent<PuzzleSwitch>();

        // 8. Two Distinct Buttons on SpawnPlatform:
        // Left Button (PendulumButton) at X = -8.2, Z = -6.2 -> Releases Giant Pendulum
        CreateBox("PendulumBtnPedestal", levelRoot.transform, new Vector3(-8.2f, 8.25f, -6.2f), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.35f));
        GameObject pendBtn = CreateBox("PendulumButton", levelRoot.transform, new Vector3(-8.2f, 8.65f, -6.2f), new Vector3(0.45f, 0.35f, 0.45f), new Color(0.85f, 0.25f, 0.35f));
        pendBtn.GetComponent<BoxCollider>().isTrigger = true;
        pendBtn.AddComponent<PendulumButton>();

        // Right Button (CraneButton) at X = 0.0, Z = -6.2 -> Releases Crane & Seesaw
        CreateBox("CraneBtnPedestal", levelRoot.transform, new Vector3(0.0f, 8.25f, -6.2f), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.35f));
        GameObject craneBtn = CreateBox("CraneButton", levelRoot.transform, new Vector3(0.0f, 8.65f, -6.2f), new Vector3(0.45f, 0.35f, 0.45f), new Color(1f, 0.55f, 0.1f));
        craneBtn.GetComponent<BoxCollider>().isTrigger = true;
        craneBtn.AddComponent<PendulumButton>();

        // Energy Crystal on SpawnPlatform
        GameObject crystalObj = CreateBox("DashCrystal_L5", levelRoot.transform, new Vector3(-3.5f, 8.9f, -6.8f), new Vector3(0.45f, 0.45f, 0.45f), new Color(0.1f, 0.9f, 1f));
        crystalObj.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        crystalObj.GetComponent<BoxCollider>().isTrigger = true;
        crystalObj.AddComponent<DashCrystal>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string matPath = $"Assets/Materials/L3_{name}_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null)
                    sh = Shader.Find("Standard");
                mat = new Material(sh);
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }

            r.sharedMaterial = mat;
        }

        return go;
    }

    private static void EnsureBuildSettings()
    {
        string[] desiredPaths = { Level1Path, Level2Path, Level3Path, Level4Path, Level5Path };
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool modified = false;

        foreach (string p in desiredPaths)
        {
            if (!System.IO.File.Exists(p))
                continue;

            bool exists = list.Exists(s => s.path == p);
            if (!exists)
            {
                list.Add(new EditorBuildSettingsScene(p, true));
                modified = true;
            }
        }

        if (modified)
        {
            EditorBuildSettings.scenes = list.ToArray();
        }
    }

    [MenuItem("Tools/Puzzle Levels/Open Level 1 - Billiards (台球)")]
    public static void OpenLevel1()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(Level1Path, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Puzzle Levels/Open Level 2 - Pendulum (大摆锤与动量锁定)")]
    public static void OpenLevel2()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(Level2Path, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Puzzle Levels/Open Level 3 - Seesaw Elevator (跷跷板与高空跳台)")]
    public static void OpenLevel3()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(Level3Path, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Puzzle Levels/Open Level 4 - Momentum Redirect (动量偏转飞跃峡谷)")]
    public static void OpenLevel4()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(Level4Path, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Puzzle Levels/Open Level 5 - Chain Reaction (母球传力连锁)")]
    public static void OpenLevel5()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(Level5Path, OpenSceneMode.Single);
    }
}
