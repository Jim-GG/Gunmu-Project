using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PuzzleRouteBuilder
{
    private const string Request = "Library/InstallPuzzleRoute.request";
    private const string AssetsFolder = "Assets/Resources/DemoPresentation";
    private static bool editReady;
    private static double readyAfter;
    static PuzzleRouteBuilder()
    {
        editReady = !EditorApplication.isPlayingOrWillChangePlaymode;
        readyAfter = EditorApplication.timeSinceStartup + 2;
        EditorApplication.playModeStateChanged += s => { editReady = s == PlayModeStateChange.EnteredEditMode; readyAfter = EditorApplication.timeSinceStartup + 2; };
        EditorApplication.update += CheckRequest;
    }
    private static void CheckRequest()
    {
        if (!File.Exists(Request) || !editReady || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < readyAfter) return;
        File.Delete(Request);
        Install();
    }

    [MenuItem("Tools/零界/安装一二关出口与占位外观")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene previous = SceneManager.GetActiveScene();
        string backup = "Library/PuzzleRouteBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        try
        {
            BuildGuardSound();
            foreach (string name in new[] { "Level1_Billiards", "Level2_Pendulum" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                // Persist the user's open-scene work before making the authorized additions.
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                File.Copy(path, backup + "/" + name + ".unity", true);
                SceneManager.SetActiveScene(scene);
                string marker = "Demo_" + name + "_外观与路线";
                if (Find(scene, marker) == null)
                {
                    GameObject root = new GameObject(marker);
                    Undo.RegisterCreatedObjectUndo(root, "搭建解谜关卡出口");
                    StyleExisting(scene);
                    if (name == "Level1_Billiards") BuildFirst(scene, root.transform);
                    else BuildSecond(scene, root.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            File.WriteAllText("Library/PuzzleRouteInstalled.txt", "Installed " + DateTime.Now.ToString("O") + "\nBackup: " + backup);
            Debug.Log("第一关右侧出口、第二关填沟平台与后方门、两关工业占位外观及格挡音效已安装并保存。后续可以在场景层级中直接微调。");
        }
        finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }

    private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name)?.gameObject;
    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(AssetsFolder + "/" + name + ".mat");
    private static GameObject Part(Transform parent, string name, Vector3 pos, Vector3 size, string mat, bool collider = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Mat(mat);
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static void Set(SerializedObject s, string name, Object value) => s.FindProperty(name).objectReferenceValue = value;

    private static void StyleExisting(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = renderer.gameObject.name;
            string material = n.Contains("Ground") || n.Contains("Platform") || n.Contains("Floor") ? "Floor"
                : n.Contains("Crystal") ? "Energy"
                : n.Contains("Box") || n.Contains("Button") || n.Contains("Gate") ? "Yellow"
                : n.Contains("Gantry") || n.Contains("Arm") || n.Contains("Hammer") || n.Contains("Stopper") ? "Steel" : "Green";
            renderer.sharedMaterial = Mat(material);
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (KineticBox box in root.GetComponentsInChildren<KineticBox>(true))
        {
            Transform p = box.transform;
            for (int i = -1; i <= 1; i += 2)
            {
                Part(p, "载荷加固带", new Vector3(i * .3f, 0, 0), new Vector3(.065f, 1.015f, 1.015f), "Dark");
                Part(p, "冲量接口", new Vector3(0, i * .507f, 0), new Vector3(.22f, .012f, .22f), "Steel");
            }
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (PlayerMovement player in root.GetComponentsInChildren<PlayerMovement>(true))
            if (player.GetComponent<PunchPresentation>() == null) Undo.AddComponent<PunchPresentation>(player.gameObject);
    }

    private static void BuildFirst(Scene scene, Transform root)
    {
        Shell(root, -10, 10, -7, 11, 5.5f, true);
        // The exit is on the player's right (+X) from the starting direction.
        GameObject door = Part(root, "Level1ExitDoor", new Vector3(10, 1.5f, 1.5f), new Vector3(.4f, 3, 3.2f), "Yellow", true);
        Bulkhead(root, 10, -7, 11, 0, 5.5f, 1.5f, 3.2f);
        Part(root, "右侧出口通道地板", new Vector3(12, -.2f, 1.5f), new Vector3(4.5f, .4f, 3.8f), "Floor", true);
        Part(root, "右侧通道左墙", new Vector3(12, 2.75f, -.35f), new Vector3(4.5f, 5.5f, .3f), "Green", true);
        Part(root, "右侧通道右墙", new Vector3(12, 2.75f, 3.35f), new Vector3(4.5f, 5.5f, .3f), "Green", true);
        Part(root, "右侧通道顶板", new Vector3(12, 5.5f, 1.5f), new Vector3(4.5f, .3f, 3.8f), "Steel", true);
        Part(root, "右侧通道末端", new Vector3(14.1f, 2.75f, 1.5f), new Vector3(.3f, 5.5f, 3.8f), "Green", true);
        Route(scene, root, door, null, null, "Level2_Pendulum", new Vector3(12.3f, 1.5f, 1.5f), new Vector3(1.5f, 3, 3.1f));
        Part(root, "右侧出口指示灯", new Vector3(9.7f, 3.25f, 1.5f), new Vector3(.12f, .12f, 2.8f), "Energy");
    }

    private static void BuildSecond(Scene scene, Transform root)
    {
        // Taller enclosure leaves the entire original gantry and pendulum sweep clear.
        Shell(root, -12, 20, -9, 7, 12.5f, false);
        GameObject oldFloor = Find(scene, "TrenchFloor_EastExtension");
        Bounds trench = oldFloor.GetComponent<Renderer>().bounds;
        const float walkHeight = 4f;
        GameObject bridge = Part(root, "Level2TrenchFill", new Vector3(trench.center.x, -.55f, trench.center.z),
            new Vector3(trench.size.x, .5f, trench.size.z), "Steel", true);
        for (float x = trench.min.x + 1; x < trench.max.x; x += 2)
            Part(bridge.transform, "平台接缝", new Vector3((x - trench.center.x) / trench.size.x, .505f, 0), new Vector3(.005f, .015f, 1), "Yellow");
        GameObject receiver = Find(scene, "PuzzleSwitch_East");
        GameObject door = Part(root, "Level2ExitDoor", new Vector3(15.8f, walkHeight + 1.5f, 0), new Vector3(.4f, 3, 3.2f), "Yellow", true);
        Bulkhead(root, 15.8f, -9, 7, walkHeight, 12.5f, 0, 3.2f);
        Part(root, "门后停留平台", new Vector3(18.8f, walkHeight - .25f, 0), new Vector3(2f, .5f, 3.8f), "Floor", true);
        Part(root, "门后左护栏", new Vector3(18, walkHeight + .6f, -1.9f), new Vector3(3.6f, 1.2f, .15f), "Steel", true);
        Part(root, "门后右护栏", new Vector3(18, walkHeight + .6f, 1.9f), new Vector3(3.6f, 1.2f, .15f), "Steel", true);
        Route(scene, root, door, bridge, receiver.GetComponent<Collider>(), "", new Vector3(18.5f, 5.5f, 0), new Vector3(1, 3, 3.1f));
        Part(root, "后方门指示灯", new Vector3(15.5f, walkHeight + 3.25f, 0), new Vector3(.1f, .12f, 2.8f), "Energy");
        GameObject hammer = Find(scene, "PendulumHammer");
        if (hammer != null)
        {
            Part(hammer.transform, "摆锤护甲", new Vector3(0, 0, .51f), new Vector3(.8f, .85f, .04f), "Dark");
            for (int i = 0; i < 4; i++) Part(hammer.transform, "摆锤警示条", new Vector3(-.3f + i * .2f, -.32f, .54f), new Vector3(.1f, .1f, .02f), "Yellow");
        }
    }

    private static void Route(Scene scene, Transform root, GameObject door, GameObject bridge, Collider receiver, string next, Vector3 exitPos, Vector3 exitSize)
    {
        var sw = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PuzzleSwitch>(true)).First();
        var settings = new SerializedObject(sw); settings.FindProperty("allowPlayerActivation").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
        var progress = root.gameObject.AddComponent<PuzzleRouteProgress>();
        var data = new SerializedObject(progress);
        Set(data, "puzzleSwitch", sw); Set(data, "exitDoor", door.transform); Set(data, "bridge", bridge != null ? bridge.transform : null);
        Set(data, "receiverBlocker", receiver); data.FindProperty("nextSceneName").stringValue = next;
        data.FindProperty("exitHint").stringValue = bridge != null ? "沟壑已填平，前往接收屏幕后方的门。" : "右侧出口已开启，穿过通道进入第二关。";
        data.ApplyModifiedPropertiesWithoutUndo();
        var exit = new GameObject("PuzzleExitZone"); exit.transform.SetParent(root, false); exit.transform.position = exitPos;
        var trigger = exit.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = exitSize;
        var link = exit.AddComponent<PuzzleRouteExit>(); var linkData = new SerializedObject(link); Set(linkData, "route", progress); linkData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Bulkhead(Transform root, float x, float back, float front, float floor, float roof, float openingZ, float width)
    {
        float min = openingZ - width * .5f, max = openingZ + width * .5f;
        Part(root, "出口后隔墙", new Vector3(x, roof * .5f, (back + min) * .5f), new Vector3(.4f, roof, min - back), "Green", true);
        Part(root, "出口前隔墙", new Vector3(x, roof * .5f, (front + max) * .5f), new Vector3(.4f, roof, front - max), "Green", true);
        float top = floor + 3;
        Part(root, "出口门楣", new Vector3(x, (top + roof) * .5f, openingZ), new Vector3(.4f, roof - top, width), "Steel", true);
    }

    private static void Shell(Transform root, float left, float right, float back, float front, float roof, bool openRight)
    {
        float cx = (left + right) * .5f, cz = (back + front) * .5f;
        Part(root, "实验室左墙", new Vector3(left, roof * .5f, cz), new Vector3(.4f, roof, front - back + .4f), "Green", true);
        if (!openRight) Part(root, "实验室右墙", new Vector3(right, roof * .5f, cz), new Vector3(.4f, roof, front - back + .4f), "Green", true);
        Part(root, "实验室后墙", new Vector3(cx, roof * .5f, back), new Vector3(right - left, roof, .4f), "Green", true);
        Part(root, "实验室前墙", new Vector3(cx, roof * .5f, front), new Vector3(right - left, roof, .4f), "Green", true);
        Part(root, "实验室屋顶", new Vector3(cx, roof, cz), new Vector3(right - left + .4f, .3f, front - back + .4f), "Steel", true);
        for (float z = back + 2; z < front - 1; z += 4)
        {
            Part(root, "顶梁", new Vector3(cx, roof - .35f, z), new Vector3(right - left, .3f, .3f), "Steel");
            Part(root, "长条灯箱", new Vector3(cx, roof - .55f, z), new Vector3(4, .12f, .5f), "Lamp");
            foreach (float x in new[] { left + .25f, right - .25f })
                if (!openRight || x < cx) Part(root, "墙体加固肋", new Vector3(x, roof * .5f, z), new Vector3(.15f, roof - .3f, .25f), "Steel");
            var go = new GameObject("占位工业灯"); go.transform.SetParent(root, false); go.transform.position = new Vector3(cx, roof - 1, z);
            Light light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = roof + 6;
            light.intensity = roof > 8 ? 6f : 2.7f; light.color = new Color(.74f, .83f, .76f);
        }
        for (int i = 0; i < 3; i++) Part(root, "壁面管线", new Vector3(left + .35f, roof - 1 - i * .25f, cz), new Vector3(.09f, .09f, front - back), "Steel");
    }

    private static void BuildGuardSound()
    {
        string path = AssetsFolder + "/GuardClang.wav";
        if (File.Exists(path)) return;
        const int rate = 22050, samples = 7938;
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + samples * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(samples * 2);
            var random = new System.Random(27);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float clang = Mathf.Sin(t * Mathf.PI * 2 * 620) * Mathf.Exp(-t * 15)
                    + .36f * Mathf.Sin(t * Mathf.PI * 2 * 1733) * Mathf.Exp(-t * 23)
                    + .2f * ((float)random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 65);
                w.Write((short)(Mathf.Clamp(clang * .44f * Mathf.Min(1, t * 1400), -1, 1) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path);
    }
}
