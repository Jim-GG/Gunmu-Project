using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>当前游戏所处的大状态。</summary>
public enum GameState
{
    /// <summary>预留：开始界面 / 载入中。当前最小骨架会直接跳过。</summary>
    Starting,
    /// <summary>正常游玩（时间正常、接受输入）。</summary>
    Running,
    /// <summary>暂停（时间冻结、拦截玩法输入）。</summary>
    Paused,
    /// <summary>玩家死亡（时间冻结，等待重生/重开）。</summary>
    Dead,
    /// <summary>关卡完成（时间冻结，等待下一关或结算）。</summary>
    LevelCompleted
}

/// <summary>
/// 最小游戏状态骨架：
/// - 唯一负责 Time.timeScale 与 <see cref="GameplayGate"/> 的地方，玩法脚本只读 GameplayGate。
/// - 不包含菜单 UI、关卡跳转、存档；这些等正式流程定了再加。
/// - 自动常驻（不依赖任何场景），新场景载入后一律回到 Running。
/// </summary>
[DefaultExecutionOrder(-10000)]
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public static GameState State { get; private set; } = GameState.Starting;
    public static string LastDeathReason { get; private set; }

    /// <summary>状态切换事件（HUD、音效、镜头等挂在这里）。</summary>
    public static event Action<GameState> StateChanged;

    /// <summary>当前是否应拦截玩法输入。</summary>
    public static bool BlocksGameplay => State != GameState.Running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        State = GameState.Starting;
        LastDeathReason = null;
        StateChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        new GameObject("GameStateManager").AddComponent<GameStateManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 最小骨架：没有开始界面，直接进 Running。
        ApplyState(GameState.Running, true);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
        GameplayGate.IsBlocked = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (State == GameState.Running)
            RequestPause();
        else if (State == GameState.Paused)
            RequestResume();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (loadMode != LoadSceneMode.Single)
            return;

        // 新场景一律从 Running 开始（暂停/死亡不会跨场景残留）。
        ApplyState(GameState.Running, true);
    }

    public static void RequestPause()
    {
        if (State == GameState.Running)
            ApplyState(GameState.Paused);
    }

    public static void RequestResume()
    {
        if (State != GameState.Running)
            ApplyState(GameState.Running);
    }

    public static void NotifyPlayerDied(string reason = null)
    {
        LastDeathReason = reason;
        ApplyState(GameState.Dead);
    }

    public static void NotifyLevelCompleted()
    {
        ApplyState(GameState.LevelCompleted);
    }

    public static void SetState(GameState next)
    {
        ApplyState(next);
    }

    /// <summary>最小骨架的“重生”：直接重载当前场景（后续可换成检查点/重生点）。</summary>
    public static void RestartCurrentRoom()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static void ApplyState(GameState next, bool force = false)
    {
        if (!force && State == next)
            return;

        State = next;

        bool running = next == GameState.Running;
        Time.timeScale = running ? 1f : 0f;
        GameplayGate.IsBlocked = !running;

        StateChanged?.Invoke(next);
        Debug.Log($"[GameState] → {next}");
    }
}
