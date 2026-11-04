using UnityEngine;
using UnityEngine.SceneManagement;

// Configurable scene routing. Environment positions stay in the scene, not gameplay code.
public class PuzzleRouteProgress : MonoBehaviour
{
    [SerializeField, InspectorName("通关机关")] private PuzzleSwitch puzzleSwitch;
    [SerializeField, InspectorName("出口门")] private Transform exitDoor;
    [SerializeField, InspectorName("开门偏移")] private Vector3 openOffset = new Vector3(0, 3.5f, 0);
    [SerializeField, InspectorName("填沟平台（可留空）")] private Transform bridge;
    [SerializeField, InspectorName("平台升起高度")] private float bridgeRise = 4.3f;
    [SerializeField, Min(.05f), InspectorName("填沟用时")] private float bridgeDuration = 1.4f;
    [SerializeField, Min(.05f), InspectorName("开门用时")] private float doorDuration = .8f;
    [SerializeField, InspectorName("成功后取消阻挡的接收屏幕")] private Collider receiverBlocker;
    [SerializeField, InspectorName("下一场景（留空表示阶段终点）")] private string nextSceneName;
    [SerializeField, InspectorName("出口提示")] private string exitHint = "出口在右侧，穿过门进入第二关。";
    private Vector3 doorClosed, bridgeDown;
    private float elapsed;
    private bool reached, loading, valid;
    public bool IsOpen => valid && puzzleSwitch.IsActivated && elapsed >= TotalDuration;
    private float TotalDuration => (bridge != null ? bridgeDuration : 0f) + doorDuration;

    private void Awake()
    {
        if (puzzleSwitch == null || exitDoor == null)
        {
            Debug.LogError("关卡出口缺少通关机关或出口门引用。", this);
            enabled = false; return;
        }
        doorClosed = exitDoor.position;
        if (bridge != null) bridgeDown = bridge.position;
        valid = true;
        ApplyClosedState();
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked) return;
        if (!valid) return;
        if (!puzzleSwitch.IsActivated)
        {
            if (elapsed > 0f || reached) ApplyClosedState();
            return;
        }
        // Once solved, opening feedback remains readable even when Q slowed the puzzle.
        elapsed = Mathf.Min(TotalDuration, elapsed + Time.unscaledDeltaTime);
        float bridgeTime = bridge != null ? bridgeDuration : 0f;
        if (bridge != null)
        {
            bridge.position = bridgeDown + Vector3.up * bridgeRise * Mathf.SmoothStep(0, 1, elapsed / bridgeDuration);
            LiftPlayerAboveDeck();
        }
        if (receiverBlocker != null) receiverBlocker.enabled = false;
        exitDoor.position = doorClosed + openOffset * Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - bridgeTime) / doorDuration));
    }

    private void ApplyClosedState()
    {
        elapsed = 0f; reached = false;
        exitDoor.position = doorClosed;
        if (bridge != null) bridge.position = bridgeDown;
        if (receiverBlocker != null) receiverBlocker.enabled = true;
    }

    private void LiftPlayerAboveDeck()
    {
        // Raising the deck must not trap a player who solved the puzzle from inside the trench.
        if (elapsed > bridgeDuration || !bridge.TryGetComponent(out BoxCollider deck)) return;
        Bounds b = deck.bounds;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            Vector3 pos = player.transform.position;
            if (pos.x < b.min.x || pos.x > b.max.x || pos.z < b.min.z || pos.z > b.max.z
                || pos.y >= b.max.y + .03f || pos.y < bridgeDown.y - 1f) continue;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled) continue;
            controller.enabled = false;
            player.transform.position = new Vector3(pos.x, b.max.y + .04f, pos.z);
            controller.enabled = true;
        }
    }

    public void ReachExit(PlayerMovement player)
    {
        if (GameplayGate.IsBlocked || !IsOpen || loading || player == null) return;
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            reached = true;
            GameStateManager.NotifyLevelCompleted();
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError("无法进入下一关，请在构建场景列表中勾选：" + nextSceneName, this); return;
        }
        loading = true;
        Time.timeScale = 1; Time.fixedDeltaTime = .02f;
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        if (!valid || !puzzleSwitch.IsActivated) return;
        ChineseGameUI.EnsureFont();
        GUI.Box(new Rect(16, 106, Mathf.Min(700, Screen.width - 32), 38), reached
            ? "第二关已完成！当前制作到这里，后续接大战斗区域。"
            : IsOpen ? exitHint : bridge != null && elapsed < bridgeDuration ? "解谜成功！填沟平台正在升起……" : "解谜成功！出口门正在开启……");
    }
}
