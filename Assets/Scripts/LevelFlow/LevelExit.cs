using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 关卡出口触发区：玩家进入后自动加载配置的下一关。
/// - 下一关场景可在编辑器里拖拽选择，运行时用场景名跳转。
/// - 留空下一关 = 视为最终关：不跳场景，只把游戏状态置为 LevelCompleted。
/// - 前置门禁：任意实现了 <see cref="ILevelExitGate"/> 的组件（解谜开关 / 战斗清场 / 以后的新条件），
///   全部满足才放行。留空 = 无条件放行。
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelExit : MonoBehaviour
{
    [Header("出口配置")]
    [SerializeField, InspectorName("下一关场景名（留空 = 最终关）")] private string nextSceneName;

#if UNITY_EDITOR
    [SerializeField, InspectorName("下一关场景（拖拽，仅编辑器）")] private UnityEditor.SceneAsset editorNextScene;
#endif

    [SerializeField, InspectorName("前置门禁（需实现 ILevelExitGate，可留空）")] private MonoBehaviour[] gates;

    [SerializeField, InspectorName("到达出口时触发")] private UnityEvent onReached = new UnityEvent();

    private readonly List<ILevelExitGate> resolvedGates = new List<ILevelExitGate>();
    private bool loading;
    private bool valid = true;

    public string NextSceneName => nextSceneName;
    public bool IsFinalLevel => string.IsNullOrWhiteSpace(nextSceneName);

    /// <summary>当前门禁是否全部满足（调试 / 表现层可用）。</summary>
    public bool IsUnlocked
    {
        get
        {
            foreach (ILevelExitGate gate in resolvedGates)
                if (gate != null && !gate.IsSatisfied) return false;
            return true;
        }
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        CollectGates();
        ValidateTarget();
    }

    private void CollectGates()
    {
        resolvedGates.Clear();
        if (gates == null) return;
        foreach (MonoBehaviour gate in gates)
        {
            if (gate == null) continue;
            if (gate is ILevelExitGate exitGate) { resolvedGates.Add(exitGate); continue; }
            Debug.LogError($"[LevelExit] '{gate.name}' 上的 {gate.GetType().Name} 没有实现 ILevelExitGate，已忽略。", this);
        }
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
        // 也处理"玩家已在触发区里、之后才满足条件"的情况。
        TryExit(other);
    }

    private void TryExit(Collider other)
    {
        if (!valid || loading || GameplayGate.IsBlocked || other == null)
            return;

        if (other.GetComponentInParent<PlayerMovement>() == null)
            return;

        if (!IsUnlocked)
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
