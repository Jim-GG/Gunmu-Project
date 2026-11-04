using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Persistent demo flow and replaceable placeholder UI. Public methods can later
// be connected directly to Canvas/UI Toolkit buttons instead of this OnGUI view.
[DefaultExecutionOrder(-10000)]
public class DemoSession : MonoBehaviour
{
    public enum ScreenMode { Start, Playing, Paused, Dead, Completed, Loading }
    public static DemoSession Instance { get; private set; }
    public static bool IsGameplayBlocked => Instance != null && Instance.IsDemoScene && Instance.mode != ScreenMode.Playing;
    [SerializeField, InspectorName("Demo 起始场景")] private string firstSceneName = "Demo_IntroCombat";
    [SerializeField, InspectorName("显示占位界面")] private bool drawPlaceholderUI = true;
    [SerializeField, InspectorName("游戏名称")] private string gameTitle = "零界";
    [SerializeField, InspectorName("背景颜色")] private Color backgroundColor = new Color(.035f, .055f, .06f, 1);
    [SerializeField, InspectorName("强调颜色")] private Color accentColor = new Color(.28f, .8f, .72f, 1);
    private ScreenMode mode = ScreenMode.Start;
    private ScreenMode requestedMode = ScreenMode.Start;
    private bool hasLoadedScene, loadingScene;
    private string deathReason = "生命耗尽，请重新挑战。";
    private PlayerCombatController playerHealth;
    private readonly Dictionary<Behaviour, bool> savedControls = new Dictionary<Behaviour, bool>();
    public ScreenMode Mode => mode;
    public bool IsDemoScene => IsSupported(SceneManager.GetActiveScene().name);
    private bool IsSupported(string name) => name == firstSceneName || name == "Level1_Billiards" || name == "Level2_Pendulum";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStatic() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("DemoUI/DemoSession");
        if (prefab != null) Instantiate(prefab);
        else new GameObject("DemoSession").AddComponent<DemoSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameplayGate.IsBlocked = IsGameplayBlocked;
    }

    private void Start()
    {
        // Enter Play Mode can reuse the Editor's open scene without emitting
        // sceneLoaded. Initialize that first room explicitly in that case.
        if (!hasLoadedScene) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (loadMode != LoadSceneMode.Single) return;
        savedControls.Clear(); loadingScene = false;
        playerHealth = FindFirstObjectByType<PlayerCombatController>();
        if (!IsSupported(scene.name))
        {
            mode = ScreenMode.Playing; RestoreTime(); return;
        }
        // Opening an individual puzzle in the Editor still starts at its own menu;
        // the Start button always begins the complete demo from the tutorial.
        ScreenMode destination = hasLoadedScene ? requestedMode : ScreenMode.Start;
        hasLoadedScene = true; requestedMode = ScreenMode.Playing;
        if (destination == ScreenMode.Playing)
        {
            mode = ScreenMode.Loading;
            PauseWorld();
            StartCoroutine(ResumeNextFrame());
        }
        else { mode = ScreenMode.Start; PauseWorld(); }
    }

    private void Update()
    {
        // 玩法拦截状态对外只暴露中立的 GameplayGate，道具层不再直接引用本类。
        GameplayGate.IsBlocked = IsGameplayBlocked;

        if (!IsDemoScene) return;
        if (mode == ScreenMode.Playing && playerHealth != null && playerHealth.IsDefeated)
        { ShowDeath("生命耗尽，请重新挑战。"); return; }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
            // A puzzle panel owns its own Escape action.
            if (mode == ScreenMode.Playing && (interactor == null || !interactor.IsUIBusy)) PauseGame();
            else if (mode == ScreenMode.Paused) ResumeGame();
            return;
        }
        if (mode == ScreenMode.Start && keyboard.enterKey.wasPressedThisFrame) StartGame();
        else if (mode == ScreenMode.Dead && keyboard.rKey.wasPressedThisFrame) RestartRoom();
        else if (mode == ScreenMode.Completed && keyboard.rKey.wasPressedThisFrame) ReplayDemo();
        if (IsGameplayBlocked && !loadingScene)
        {
            Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }

    private void PauseWorld()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        // Disabled behaviours still belong to the player's object, so retain each
        // original enabled state (in particular the not-yet-owned gauntlet).
        if (player == null && playerHealth != null) player = playerHealth.GetComponent<PlayerMovement>();
        if (player == null)
            foreach (PlayerMovement candidate in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.gameObject.activeInHierarchy) { player = candidate; break; }
        if (player != null)
        {
            foreach (Behaviour control in new Behaviour[] { player, player.GetComponent<PlayerLook>(), player.GetComponent<PlayerChargedPunch>(),
                player.GetComponent<PlayerInteractor>(), player.GetComponent<PunchPresentation>(), player.GetComponent<RoomRestartController>() })
            {
                if (control == null || savedControls.ContainsKey(control)) continue;
                savedControls.Add(control, control.enabled); control.enabled = false;
            }
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    private static void RestoreTime() { Time.timeScale = 1f; Time.fixedDeltaTime = .02f; }

    private IEnumerator ResumeNextFrame()
    {
        // Prevent the menu click/Enter from also charging a punch or operating a terminal.
        yield return null;
        foreach (KeyValuePair<Behaviour, bool> item in savedControls)
            if (item.Key != null) item.Key.enabled = item.Value;
        savedControls.Clear();
        RestoreTime(); mode = ScreenMode.Playing;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    public void StartGame()
    {
        if (mode != ScreenMode.Start || loadingScene) return;
        if (SceneManager.GetActiveScene().name == firstSceneName)
        { mode = ScreenMode.Loading; StartCoroutine(ResumeNextFrame()); }
        else LoadRoom(firstSceneName, ScreenMode.Playing);
    }
    public void PauseGame()
    {
        if (mode != ScreenMode.Playing) return;
        mode = ScreenMode.Paused; PauseWorld();
    }
    public void ResumeGame()
    {
        if (mode != ScreenMode.Paused) return;
        mode = ScreenMode.Loading; StartCoroutine(ResumeNextFrame());
    }
    public void ShowDeath(string reason = "生命耗尽，请重新挑战。")
    {
        if (!IsDemoScene || mode != ScreenMode.Playing) return;
        deathReason = reason; mode = ScreenMode.Dead; PauseWorld();
    }
    public void CompleteDemo()
    {
        if (mode != ScreenMode.Playing || SceneManager.GetActiveScene().name != "Level2_Pendulum") return;
        mode = ScreenMode.Completed; PauseWorld();
    }
    public void RestartRoom()
    {
        if (!IsDemoScene || loadingScene || mode == ScreenMode.Loading || mode == ScreenMode.Start) return;
        LoadRoom(SceneManager.GetActiveScene().name, ScreenMode.Playing);
    }
    public void ReplayDemo() { if (!loadingScene) LoadRoom(firstSceneName, ScreenMode.Playing); }
    public void ReturnToMenu() { if (!loadingScene) LoadRoom(firstSceneName, ScreenMode.Start); }

    private void LoadRoom(string sceneName, ScreenMode destination)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        { Debug.LogError("场景未加入构建列表，无法载入：" + sceneName, this); return; }
        loadingScene = true; mode = ScreenMode.Loading; requestedMode = destination;
        hasLoadedScene = true;
        PauseWorld(); RestoreTime();
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        RestoreTime(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded; Instance = null; RestoreTime();
        GameplayGate.IsBlocked = false;
    }

    private void OnGUI()
    {
        if (!drawPlaceholderUI || !IsGameplayBlocked) return;
        ChineseGameUI.EnsureFont();
        int oldDepth = GUI.depth; Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color;
        GUI.depth = -10000;
        GUI.color = backgroundColor; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 720 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
        Rect panel = new Rect(340, 62, 600, 596);
        GUI.color = new Color(.085f, .12f, .125f); GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = accentColor; GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 5), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUIStyle subtitle = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleCenter };
        GUIStyle paragraph = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        GUI.Label(new Rect(370, 91, 540, 70), gameTitle, title);
        string heading = mode == ScreenMode.Start ? "冲量实验 · 玩法 Demo" : mode == ScreenMode.Dead ? "挑战失败"
            : mode == ScreenMode.Completed ? "Demo 已完成" : mode == ScreenMode.Paused ? "游戏已暂停" : "正在载入……";
        GUI.Label(new Rect(380, 169, 520, 42), heading, subtitle);
        string message = mode == ScreenMode.Dead ? deathReason + "\n重新挑战会恢复当前房间和人物的初始状态。"
            : mode == ScreenMode.Completed ? "你已完成教学战斗与两段解谜。\n感谢试玩！是否从头重新游玩？"
            : mode == ScreenMode.Paused ? "可以继续，也可以重置当前房间或返回主菜单。"
            : "终端获取拳套 → 教学战斗 → 两段解谜\n格挡储能，蓄力反击，用冲量打开前路。";
        GUI.Label(new Rect(380, 220, 520, 72), message, paragraph);
        if (mode == ScreenMode.Start)
        {
            if (GUI.Button(new Rect(430, 320, 420, 54), "开始游戏", button)) StartGame();
            if (GUI.Button(new Rect(430, 388, 420, 54), "退出游戏", button)) QuitGame();
        }
        else if (mode == ScreenMode.Dead || mode == ScreenMode.Completed)
        {
            if (GUI.Button(new Rect(430, 320, 420, 54), mode == ScreenMode.Dead ? "重新挑战本房间" : "重新游玩", button))
            { if (mode == ScreenMode.Dead) RestartRoom(); else ReplayDemo(); }
            if (GUI.Button(new Rect(430, 388, 420, 54), "返回主菜单", button)) ReturnToMenu();
            if (GUI.Button(new Rect(430, 456, 420, 54), "退出游戏", button)) QuitGame();
        }
        else if (mode == ScreenMode.Paused)
        {
            if (GUI.Button(new Rect(430, 308, 420, 50), "继续游戏", button)) ResumeGame();
            if (GUI.Button(new Rect(430, 370, 420, 50), "重置当前房间", button)) RestartRoom();
            if (GUI.Button(new Rect(430, 432, 420, 50), "返回主菜单", button)) ReturnToMenu();
            if (GUI.Button(new Rect(430, 494, 420, 50), "退出游戏", button)) QuitGame();
        }
        GUI.Label(new Rect(370, 563, 540, 66), "WASD 移动｜左键蓄力｜右键格挡\nE 交互/锁定｜F 机关｜Q 子弹时间｜R 重置｜Esc 暂停", paragraph);
        GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth;
    }
}
