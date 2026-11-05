using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Switch inside the narrow gap that activates when struck by a KineticBox.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PuzzleSwitch : MonoBehaviour, IPuzzleResettable, ILevelExitGate
{
    [SerializeField] private Color inactiveColor = new Color(1f, 0.35f, 0.1f, 1f);
    [SerializeField] private Color activeColor = new Color(0.15f, 1f, 0.35f, 1f);
    [SerializeField] private Transform doorOrIndicatorToMove;
    [SerializeField] private Vector3 activatedOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField, InspectorName("允许玩家碰触触发")] private bool allowPlayerActivation = true;

    [Header("触发目标")]
    [SerializeField, InspectorName("激活时触发的机关（需实现 IPuzzleTriggerable）")] private MonoBehaviour[] targets;

    [Header("事件")]
    [SerializeField, InspectorName("开关被激活时")] private UnityEvent onActivated = new UnityEvent();
    [SerializeField, InspectorName("开关被复位时")] private UnityEvent onReset = new UnityEvent();

    private Renderer switchRenderer;
    private MaterialPropertyBlock propBlock;
    private readonly List<IPuzzleTriggerable> resolvedTargets = new List<IPuzzleTriggerable>();
    private Vector3 doorInitialPos;
    private bool isActivated;

    public bool IsActivated => isActivated;
    public bool AllowPlayerActivation => allowPlayerActivation;

    /// <summary>ILevelExitGate：开关激活后，挂着它的出口才能放行。</summary>
    public bool IsSatisfied => isActivated;

    private void Awake()
    {
        switchRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        PuzzleTriggerUtility.Collect(targets, resolvedTargets, this);

        if (doorOrIndicatorToMove != null)
            doorInitialPos = doorOrIndicatorToMove.position;

        UpdateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<KineticBox>() != null || (allowPlayerActivation && other.GetComponentInParent<PlayerMovement>() != null))
            Activate();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<KineticBox>() != null || (allowPlayerActivation && collision.collider.GetComponentInParent<PlayerMovement>() != null))
            Activate();
    }

    public void Activate()
    {
        if (isActivated)
            return;

        isActivated = true;
        UpdateVisuals();

        if (doorOrIndicatorToMove != null)
            doorOrIndicatorToMove.position = doorInitialPos + activatedOffset;

        Debug.Log("[PuzzleSwitch] Switch Activated! Billiard Puzzle Solved!", this);

        // 和按钮一样：激活时触发挂在上面的机关（摆锤 / 跷跷板 / 以后的任何 IPuzzleTriggerable）。
        PuzzleTriggerUtility.TriggerAll(resolvedTargets);
        onActivated.Invoke();
    }

    public void ResetSwitch()
    {
        isActivated = false;
        UpdateVisuals();

        if (doorOrIndicatorToMove != null)
            doorOrIndicatorToMove.position = doorInitialPos;

        onReset.Invoke();
    }

    /// <summary>统一重置接口（由 PuzzleResetUtility 调用）。</summary>
    public void ResetPuzzle()
    {
        ResetSwitch();
    }

    private void UpdateVisuals()
    {
        if (switchRenderer == null)
            return;

        Color targetColor = isActivated ? activeColor : inactiveColor;
        switchRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", targetColor);
        propBlock.SetColor("_Color", targetColor);
        switchRenderer.SetPropertyBlock(propBlock);
    }
}
