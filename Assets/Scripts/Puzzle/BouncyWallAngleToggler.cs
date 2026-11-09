using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 让一面弹性墙在两个预设角度之间来回切换（通常由旁边的 <see cref="PuzzleButton"/> 触发）。
/// 两种角度直接用欧拉角填，方便在 Inspector 里对着一面墙慢慢试。
/// </summary>
public class BouncyWallAngleToggler : MonoBehaviour, IPuzzleTriggerable, IPuzzleResettable
{
    [Header("目标")]
    [SerializeField, InspectorName("弹性墙转轴（要旋转的那个 Transform）")] private Transform wallPivot;
    [SerializeField, InspectorName("状态 A 欧拉角（例如：斜着把方块弹向右侧）")] private Vector3 stateARotation = new Vector3(0f, 135f, 0f);
    [SerializeField, InspectorName("状态 B 欧拉角（例如：平放，把玩家弹起来）")] private Vector3 stateBRotation = new Vector3(90f, 0f, 0f);
    [SerializeField, InspectorName("初始就是状态 A")] private bool startInStateA = false;

    [Header("切换")]
    [SerializeField, Min(0f), InspectorName("旋转过渡时长（秒）")] private float rotateDuration = 0.25f;

    [Header("事件")]
    [SerializeField, InspectorName("切换时")] private UnityEvent onSwitched = new UnityEvent();

    private bool inStateA;
    private Quaternion fromRotation = Quaternion.identity;
    private Quaternion toRotation = Quaternion.identity;
    private float rotateTimer;

    public bool IsStateA => inStateA;
    public bool CanTrigger => wallPivot != null;

    private void Awake()
    {
        inStateA = startInStateA;
        if (wallPivot == null)
            return;

        wallPivot.localRotation = Quaternion.Euler(inStateA ? stateARotation : stateBRotation);
        fromRotation = wallPivot.localRotation;
        toRotation = fromRotation;
    }

    private void Update()
    {
        if (rotateTimer <= 0f || wallPivot == null)
            return;

        rotateTimer -= Time.deltaTime;
        float duration = Mathf.Max(rotateDuration, 0.0001f);
        float t = 1f - Mathf.Clamp01(rotateTimer / duration);
        wallPivot.localRotation = Quaternion.Slerp(fromRotation, toRotation, Mathf.SmoothStep(0f, 1f, t));

        if (rotateTimer <= 0f)
            wallPivot.localRotation = toRotation;
    }

    /// <summary>翻到另一个状态。</summary>
    public void Trigger()
    {
        if (wallPivot == null)
            return;

        inStateA = !inStateA;
        fromRotation = wallPivot.localRotation;
        toRotation = Quaternion.Euler(inStateA ? stateARotation : stateBRotation);

        if (rotateDuration <= 0.0001f)
        {
            rotateTimer = 0f;
            wallPivot.localRotation = toRotation;
        }
        else
        {
            rotateTimer = rotateDuration;
        }

        onSwitched.Invoke();
        Debug.Log($"[BouncyWallAngleToggler] 弹性墙切到 {(inStateA ? "A（斜放）" : "B（平放）")} 状态", this);
    }

    /// <summary>复位到初始状态。</summary>
    public void ResetPuzzle()
    {
        inStateA = startInStateA;
        rotateTimer = 0f;
        if (wallPivot != null)
            wallPivot.localRotation = Quaternion.Euler(inStateA ? stateARotation : stateBRotation);
    }
}
