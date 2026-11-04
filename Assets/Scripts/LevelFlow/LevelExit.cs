using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 关卡出口触发区：玩家进入后自动加载配置的下一关。
/// - 下一关场景可拖拽选择（编辑器内），运行时用场景名跳转。
/// - 留空下一关 = 视为最终关：不跳场景，只把游戏状态置为 LevelCompleted。
/// - 可选前置开关：指定后必须该开关已激活才生效（用于“解谜完成才能离开”）。
/// 注意：这是最简出口；需要填沟/开门/过场时用 PuzzleRouteProgress。
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelExit : MonoBehaviour
{
    [Header("出口配置")]
    [SerializeField, InspectorName("下一关场景名（留空 = 最终关）")] private string nextSceneName;

#if UNITY_EDITOR
    [SerializeField, InspectorName("下一关场景（拖拽，仅编辑器）")] private UnityEditor.SceneAsset editorNextScene;
#endif

    [SerializeField, InspectorName("前置开关（可留空）")] private PuzzleSwitch requiredSwitch;
    [SerializeField, InspectorName("到达出口时触发")] private UnityEvent onReached = new UnityEvent();

    private bool loading;
    private bool valid = true;

    public string NextSceneName => nextSceneName;
    public bool IsFinalLevel => string.IsNullOrWhiteSpace(nextSceneName);

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        ValidateTarget();
    }

    private void ValidateTarget()
    {
        valid = true;
        if (IsFinalLevel)
            return;

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[LevelExit] 目标场景 '{nextSceneName}' 未加入 Build Settings（且已勾选），无法跳转。", this);
            valid = false;
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (editorNextScene != null && editorNextScene.name != nextSceneName)
            nextSceneName = editorNextScene.name;
#endif
    }

    private void OnTriggerEnter(Collider other)
    {
        TryExit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 也处理“玩家已在触发区里、之后才满足条件”的情况。
        TryExit(other);
    }

    private void TryExit(Collider other)
    {
        if (!valid || loading || GameplayGate.IsBlocked || other == null)
            return;

        if (other.GetComponentInParent<PlayerMovement>() == null)
            return;

        if (requiredSwitch != null && !requiredSwitch.IsActivated)
            return;

        loading = true;
        onReached.Invoke();

        if (IsFinalLevel)
        {
            Debug.Log("[LevelExit] 到达最终关出口（未配置下一关）。", this);
            GameStateManager.NotifyLevelCompleted();
            return;
        }

        if (!GameStateManager.LoadLevel(nextSceneName))
        {
            Debug.LogError($"[LevelExit] 加载 '{nextSceneName}' 失败。", this);
            loading = false;
        }
    }
}
