using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 升降机平台：被按钮/开关（<see cref="IPuzzleTriggerable"/>）触发后，将平台（及站在上面的玩家、动能方块、子物体按钮）平滑升起或降回。
/// - 支持单向升起或往返切换（再次按按钮降回）。
/// - 自动携带站在平台顶面上的玩家（CharacterController）与动能方块（KineticBox），避免穿模或抖动。
/// - 实现 <see cref="IPuzzleResettable"/>，按 R 重置时回到初始位置。
/// </summary>
[DisallowMultipleComponent]
public class ElevatorPlatform : MonoBehaviour, IPuzzleTriggerable, IPuzzleResettable
{
    [Header("目标与行程")]
    [SerializeField, InspectorName("升降平台（留空 = 自身）")] private Transform platform;
    [SerializeField, InspectorName("升起位移（世界方向）")] private Vector3 travelOffset = new Vector3(0f, 10f, 0f);
    [SerializeField, Min(0.1f), InspectorName("单程用时（秒）")] private float travelDuration = 2.2f;
    [SerializeField, InspectorName("允许往返切换（再次触发降回）")] private bool allowToggle = true;

    [Header("乘员携带")]
    [SerializeField, InspectorName("携带平台上的玩家")] private bool carryPlayer = true;
    [SerializeField, Min(0.1f), InspectorName("乘员检测高度（米）")] private float passengerCheckHeight = 2.2f;

    [Header("事件")]
    [SerializeField, InspectorName("到达顶部时触发")] private UnityEvent onArrivedTop = new UnityEvent();
    [SerializeField, InspectorName("回到初始位置时触发")] private UnityEvent onArrivedBottom = new UnityEvent();

    private Vector3 startPosition;
    private Collider platformCollider;
    private CharacterController cachedPlayerController;
    private readonly List<KineticBox> carriedBoxesBuffer = new List<KineticBox>();
    private float progress; // 0 = bottom, 1 = top
    private int moveDirection; // 0 = idle, +1 = rising, -1 = descending
    private bool reachedTopOnce;

    public bool IsMoving => moveDirection != 0;
    public bool IsAtTop => progress >= 0.999f && moveDirection == 0;
    public bool IsAtBottom => progress <= 0.001f && moveDirection == 0;

    public bool CanTrigger
    {
        get
        {
            if (platform == null || IsMoving)
                return false;

            if (allowToggle)
                return true;

            return !reachedTopOnce;
        }
    }

    private void Awake()
    {
        if (platform == null)
            platform = transform;

        startPosition = platform.position;
        platformCollider = platform.GetComponentInChildren<Collider>();
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked || platform == null || moveDirection == 0)
            return;

        float step = Time.deltaTime / Mathf.Max(travelDuration, 0.01f);
        progress = Mathf.Clamp01(progress + moveDirection * step);

        float smoothT = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 oldPos = platform.position;
        Vector3 newPos = startPosition + travelOffset * smoothT;
        Vector3 delta = newPos - oldPos;

        if (delta.sqrMagnitude > 1e-9f)
        {
            // 先检测站在平台上的玩家与动能方块，再同步平台与乘员位移
            bool shouldCarryPlayer = carryPlayer && IsPlayerOnPlatform();
            CollectBoxesOnPlatform(carriedBoxesBuffer);

            platform.position = newPos;

            for (int i = 0; i < carriedBoxesBuffer.Count; i++)
            {
                KineticBox box = carriedBoxesBuffer[i];
                if (box == null || box.IsMomentumLocked)
                    continue;

                box.transform.position += delta;
                if (box.Body != null && !box.Body.isKinematic)
                {
                    Vector3 v = box.Body.linearVelocity;
                    if (v.y < 0f)
                    {
                        v.y = 0f;
                        box.Body.linearVelocity = v;
                    }
                }
            }

            Physics.SyncTransforms();

            if (shouldCarryPlayer && cachedPlayerController != null && cachedPlayerController.enabled)
            {
                cachedPlayerController.Move(delta);
            }
        }

        if (moveDirection > 0 && progress >= 1f)
        {
            progress = 1f;
            moveDirection = 0;
            reachedTopOnce = true;
            onArrivedTop.Invoke();
        }
        else if (moveDirection < 0 && progress <= 0f)
        {
            progress = 0f;
            moveDirection = 0;
            onArrivedBottom.Invoke();
        }
    }

    public void Trigger()
    {
        if (!CanTrigger)
            return;

        if (progress < 0.5f)
        {
            moveDirection = 1;
        }
        else if (allowToggle)
        {
            moveDirection = -1;
        }
    }

    public void ResetPuzzle()
    {
        moveDirection = 0;
        progress = 0f;
        reachedTopOnce = false;

        if (platform != null)
        {
            platform.position = startPosition;
            Physics.SyncTransforms();
        }
    }

    private Bounds GetPlatformBounds()
    {
        return platformCollider != null
            ? platformCollider.bounds
            : new Bounds(platform.position, platform.lossyScale);
    }

    private bool IsPlayerOnPlatform()
    {
        if (cachedPlayerController == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
                cachedPlayerController = pm.GetComponent<CharacterController>();
        }

        if (cachedPlayerController == null || !cachedPlayerController.enabled)
            return false;

        Bounds platBounds = GetPlatformBounds();
        Vector3 playerPos = cachedPlayerController.transform.position;
        float horizontalMargin = 0.35f;

        bool withinXZ =
            playerPos.x >= platBounds.min.x - horizontalMargin &&
            playerPos.x <= platBounds.max.x + horizontalMargin &&
            playerPos.z >= platBounds.min.z - horizontalMargin &&
            playerPos.z <= platBounds.max.z + horizontalMargin;

        if (!withinXZ)
            return false;

        float surfaceY = platBounds.max.y;
        return playerPos.y >= surfaceY - 0.45f && playerPos.y <= surfaceY + passengerCheckHeight;
    }

    private void CollectBoxesOnPlatform(List<KineticBox> results)
    {
        results.Clear();
        Bounds platBounds = GetPlatformBounds();

        float surfaceY = platBounds.max.y;
        float minY = surfaceY - 0.35f;
        float maxY = surfaceY + passengerCheckHeight;
        Vector3 boxCenter = new Vector3(platBounds.center.x, (minY + maxY) * 0.5f, platBounds.center.z);
        Vector3 halfExtents = new Vector3(
            platBounds.extents.x + 0.15f,
            (maxY - minY) * 0.5f,
            platBounds.extents.z + 0.15f);

        Collider[] hits = Physics.OverlapBox(
            boxCenter,
            halfExtents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(platform))
                continue;

            KineticBox box = hit.GetComponentInParent<KineticBox>();
            if (box == null || box.IsMomentumLocked || results.Contains(box))
                continue;

            results.Add(box);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (platform == null)
            platform = transform;
    }
#endif
}
