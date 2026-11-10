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
    [SerializeField, InspectorName("仅在按住时激活（离开即复位）")] private bool holdToActivate = false;
    [SerializeField, Min(0f), InspectorName("松开后保持时长（秒）")] private float holdReleaseDelay = 1.0f;
    [SerializeField, Min(0.01f), InspectorName("按住接触判定余量（米）")] private float holdContactMargin = 0.12f;

    [Header("触发目标")]
    [SerializeField, InspectorName("激活时触发的机关（需实现 IPuzzleTriggerable）")] private MonoBehaviour[] targets;

    [Header("事件")]
    [SerializeField, InspectorName("开关被激活时")] private UnityEvent onActivated = new UnityEvent();
    [SerializeField, InspectorName("开关被复位时")] private UnityEvent onReset = new UnityEvent();

    private Renderer switchRenderer;
    private Collider switchCollider;
    private MaterialPropertyBlock propBlock;
    private readonly List<IPuzzleTriggerable> resolvedTargets = new List<IPuzzleTriggerable>();
    private Vector3 doorInitialPos;
    private bool isActivated;
    private float releaseTimer;

    public bool IsActivated => isActivated;
    public bool AllowPlayerActivation => allowPlayerActivation;
    public bool HoldToActivate => holdToActivate;

    /// <summary>ILevelExitGate：开关激活后，挂着它的出口才能放行。</summary>
    public bool IsSatisfied => isActivated;

    private void Awake()
    {
        switchRenderer = GetComponent<Renderer>();
        switchCollider = GetComponent<Collider>();
        propBlock = new MaterialPropertyBlock();
        PuzzleTriggerUtility.Collect(targets, resolvedTargets, this);

        if (doorOrIndicatorToMove != null)
            doorInitialPos = doorOrIndicatorToMove.position;

        UpdateVisuals();
    }

    private void Update()
    {
        if (!holdToActivate || GameplayGate.IsBlocked)
            return;

        bool pressedNow = CheckHoldOverlap();
        if (pressedNow)
        {
            releaseTimer = holdReleaseDelay;
            if (!isActivated)
                Activate();
        }
        else if (isActivated)
        {
            releaseTimer -= Time.deltaTime;
            if (releaseTimer <= 0f)
                ResetSwitch();
        }
    }

    private bool CheckHoldOverlap()
    {
        if (switchCollider == null)
            return false;

        Bounds bounds = switchCollider.bounds;
        Vector3 halfExtents = bounds.extents + Vector3.one * holdContactMargin;
        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            halfExtents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit == switchCollider || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<KineticBox>() != null)
                return true;

            if (allowPlayerActivation && hit.GetComponentInParent<PlayerMovement>() != null)
                return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<KineticBox>() != null || (allowPlayerActivation && other.GetComponentInParent<PlayerMovement>() != null))
        {
            releaseTimer = holdReleaseDelay;
            Activate();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<KineticBox>() != null || (allowPlayerActivation && collision.collider.GetComponentInParent<PlayerMovement>() != null))
        {
            releaseTimer = holdReleaseDelay;
            Activate();
        }
    }

    public void Activate()
    {
        if (isActivated)
            return;

        isActivated = true;
        releaseTimer = holdReleaseDelay;
        UpdateVisuals();

        if (doorOrIndicatorToMove != null)
            doorOrIndicatorToMove.position = doorInitialPos + activatedOffset;

        Debug.Log("[PuzzleSwitch] Switch Activated! Billiard Puzzle Solved!", this);

        // 和按钮一样：激活时触发挂在上面的机关（摆锤 / 跷跷板 / 以后的任何 IPuzzleTriggerable）。
        PuzzleTriggerUtility.TriggerAll(resolvedTargets);
        onActivated.Invoke();
        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Mechanism);
    }

    public void ResetSwitch()
    {
        isActivated = false;
        releaseTimer = 0f;
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
