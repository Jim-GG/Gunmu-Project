using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 跷跷板 + 高空起重机机关（重力势能转化）：
/// 玩家触发后，起重臂重物自由下落到跷跷板一端，板身翻转到设定角度，并把另一端的方块竖直弹射升空。
///
/// 可移植性说明：
/// - 板身 / 重物 / 被弹射方块都可以在 Inspector 里直接拖引用（留空时才按常见名字自动查找）。
/// - 落点高度默认**按几何自动推算**（重物底面落在板身对应位置的顶面上），不再依赖写死的世界 Y 坐标，
///   因此整套机关可以自由移动、旋转、缩放。
/// - 翻转轴、倾角、下落加速度、弹射速度、弹射方向都可配置；弹射时还会触发 onCatapult 事件，
///   方便接音效、粒子或其它机关。
/// </summary>
[DisallowMultipleComponent]
public class SeesawMechanism : MonoBehaviour, IPuzzleResettable, IPuzzleTriggerable
{
    [Header("引用（留空自动查找）")]
    [SerializeField, InspectorName("跷跷板板身")] private Transform seesawPlank;
    [SerializeField, InspectorName("起重臂重物")] private Transform craneWeightBox;
    [SerializeField, InspectorName("被弹射的方块")] private KineticBox launchBox;
    [SerializeField, InspectorName("备用刚体（可选）")] private Rigidbody launchRigidbody;

    [Header("跷跷板")]
    [SerializeField, InspectorName("板身长度（米，0 = 用场景缩放）"), Min(0f)] private float plankLength = 0f;
    [SerializeField, InspectorName("初始倾角（度）")] private float initialTiltDeg = 16f;
    [SerializeField, InspectorName("翻转后倾角（度）")] private float flippedTiltDeg = -16f;
    [SerializeField, InspectorName("翻转速度（度/秒）"), Min(1f)] private float flipSpeedDegPerSec = 180f;
    [SerializeField, InspectorName("翻转轴（板身父空间）")] private Vector3 tiltAxisLocal = Vector3.forward;

    [Header("起重机")]
    [SerializeField, InspectorName("重物下落加速度（m/s²）"), Min(0f)] private float craneDropGravity = 28f;
    [SerializeField, InspectorName("重物初始高度（米，相对落点；0 = 用场景位置）"), Min(0f)] private float weightDropHeight = 0f;
    [SerializeField, InspectorName("手动指定落点高度")] private bool useManualImpactHeight = false;
    [SerializeField, InspectorName("手动落点高度（世界 Y）")] private float manualImpactHeight = 2.25f;
    [SerializeField, InspectorName("落点高度微调（米）")] private float impactHeightOffset = 0f;

    [Header("弹射")]
    [SerializeField, InspectorName("弹射速度（m/s）"), Min(0f)] private float launchUpwardSpeed = 22.5f;
    [SerializeField, InspectorName("弹射方向（机关局部坐标）")] private Vector3 launchDirectionLocal = Vector3.up;
    [SerializeField, InspectorName("弹射时触发")] private UnityEvent onCatapult = new UnityEvent();

    private enum State
    {
        Ready,
        CraneDropping,
        SeesawFlipping,
        Completed
    }

    private State currentState = State.Ready;
    private float currentTiltDeg;
    private float weightDropSpeed;
    private Vector3 craneWeightInitialPos;
    private bool hasWeightInitialPos;

    private Quaternion plankAuthoredRotation = Quaternion.identity;
    private float plankAuthoredTiltDeg;
    private bool hasAuthoredRotation;
    private Vector3 tiltAxis = Vector3.forward;
    private float impactCenterY;
    private Collider plankCollider;
    private Collider weightCollider;
    private bool geometryReady;

    public bool IsReady => currentState == State.Ready;
    /// <summary>IPuzzleTriggerable：待机时才能被按钮触发。</summary>
    public bool CanTrigger => IsReady;
    public bool IsDropping => currentState == State.CraneDropping;
    public bool IsCompleted => currentState == State.Completed;
    public float CurrentTiltDeg => currentTiltDeg;
    public float ImpactCenterY => impactCenterY;

    private void Awake()
    {
        ResolveReferences();
        ResetMechanism();
    }

    [ContextMenu("释放起重机重物")]
    private void DebugRelease()
    {
        TriggerCraneRelease();
    }

    [ContextMenu("复位跷跷板")]
    private void DebugReset()
    {
        ResetMechanism();
    }

    private void ResolveReferences()
    {
        geometryReady = false;

        if (seesawPlank == null)
            seesawPlank = FindNamed("SeesawPlank");

        if (craneWeightBox == null)
            craneWeightBox = FindNamed("CraneWeightBox");

        if (launchBox == null)
        {
            Transform boxTransform = FindLaunchBoxTransform();
            if (boxTransform != null)
                launchBox = boxTransform.GetComponent<KineticBox>();
        }

        if (launchRigidbody == null && launchBox == null)
        {
            Transform boxTransform = FindLaunchBoxTransform();
            if (boxTransform != null)
                launchRigidbody = boxTransform.GetComponent<Rigidbody>();
        }

        tiltAxis = tiltAxisLocal.sqrMagnitude > 1e-6f ? tiltAxisLocal.normalized : Vector3.forward;

        if (seesawPlank != null)
        {
            if (!hasAuthoredRotation)
            {
                plankAuthoredRotation = seesawPlank.localRotation;
                Vector3 reference = Vector3.ProjectOnPlane(Vector3.right, tiltAxis).normalized;
                if (reference.sqrMagnitude < 1e-4f)
                    reference = Vector3.ProjectOnPlane(Vector3.forward, tiltAxis).normalized;
                plankAuthoredTiltDeg = Vector3.SignedAngle(reference, plankAuthoredRotation * reference, tiltAxis);
                hasAuthoredRotation = true;
            }
            plankCollider = seesawPlank.GetComponentInChildren<Collider>();
        }

        if (craneWeightBox != null)
            weightCollider = craneWeightBox.GetComponentInChildren<Collider>();

        if (craneWeightBox != null && !hasWeightInitialPos)
        {
            craneWeightInitialPos = craneWeightBox.position;
            hasWeightInitialPos = true;
        }

        if (seesawPlank == null)
            Debug.LogError("[SeesawMechanism] 没有找到跷跷板板身，板身不会翻转。请指定板身引用。", this);
        if (craneWeightBox == null)
            Debug.LogError("[SeesawMechanism] 没有找到起重臂重物，机关无法触发。请指定重物引用。", this);

        geometryReady = seesawPlank != null && craneWeightBox != null;
    }

    /// <summary>
    /// 先在自身子物体里找，再在同级（父物体的子物体）里找，最后在全场景里找唯一同名物体。
    /// </summary>
    private Transform FindNamed(string targetName)
    {
        Transform found = transform.Find(targetName);
        if (found != null)
            return found;

        if (transform.parent != null)
        {
            found = transform.parent.Find(targetName);
            if (found != null)
                return found;
        }

        foreach (Transform candidate in transform.root.GetComponentsInChildren<Transform>(true))
            if (candidate.name == targetName)
                return candidate;

        return null;
    }

    private Transform FindLaunchBoxTransform()
    {
        Transform found = FindNamed("KineticBox_Target");
        if (found == null) found = FindNamed("KineticBox_L4");
        if (found == null) found = FindNamed("KineticBox_L3");
        if (found == null) found = FindNamed("KineticBox_L5");
        return found;
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
    }

    /// <summary>
    /// 推进机关状态一步（dt 为秒）。抽成独立方法便于在编辑器里以固定步长复现下落与翻转过程。
    /// </summary>
    private void Simulate(float dt)
    {
        if (!geometryReady || dt <= 0f)
            return;

        if (currentState == State.CraneDropping)
        {
            weightDropSpeed += craneDropGravity * dt;
            Vector3 pos = craneWeightBox.position;
            pos.y -= weightDropSpeed * dt;

            if (pos.y <= impactCenterY)
            {
                pos.y = impactCenterY;
                craneWeightBox.position = pos;
                TriggerSeesawFlipAndCatapult();
            }
            else
            {
                craneWeightBox.position = pos;
            }

            return;
        }

        if (currentState != State.SeesawFlipping)
            return;

        currentTiltDeg = Mathf.MoveTowards(currentTiltDeg, flippedTiltDeg, flipSpeedDegPerSec * dt);
        ApplyPlankTilt(currentTiltDeg);
        CarryWeightOnPlank();

        if (Mathf.Approximately(currentTiltDeg, flippedTiltDeg))
            currentState = State.Completed;
    }

    public void TriggerCraneRelease()
    {
        if (currentState != State.Ready || !geometryReady)
            return;

        RecalculateImpactHeight();
        weightDropSpeed = 0f;
        currentState = State.CraneDropping;
        Debug.Log($"[SeesawMechanism] 起重机释放！重物下落到 Y={impactCenterY:F2} 后砸向跷跷板。", this);
    }

    private void TriggerSeesawFlipAndCatapult()
    {
        currentState = State.SeesawFlipping;

        Vector3 launchDirection = transform.TransformDirection(
            launchDirectionLocal.sqrMagnitude > 1e-6f ? launchDirectionLocal.normalized : Vector3.up);

        if (launchBox != null)
        {
            launchBox.ApplyFreeImpulse(launchDirection, launchUpwardSpeed);
            Debug.Log($"[SeesawMechanism] 跷跷板翻转！方块沿 {launchDirection} 以 {launchUpwardSpeed:F1} m/s 弹射。", this);
        }
        else if (launchRigidbody != null)
        {
            launchRigidbody.linearVelocity = launchDirection * launchUpwardSpeed;
            Debug.Log($"[SeesawMechanism] 跷跷板翻转！刚体沿 {launchDirection} 以 {launchUpwardSpeed:F1} m/s 弹射。", this);
        }
        else
        {
            Debug.LogWarning("[SeesawMechanism] 没有指定被弹射的方块/刚体：板身仍会翻转，但不会弹出任何物体。", this);
        }

        onCatapult.Invoke();
    }

    public void ResetMechanism()
    {
        ResolveReferences();

        currentState = State.Ready;
        currentTiltDeg = initialTiltDeg;
        weightDropSpeed = 0f;

        ApplyPlankSize();
        ApplyPlankTilt(currentTiltDeg);
        RecalculateImpactHeight();
        ApplyWeightStartHeight();
    }

    /// <summary>统一重置接口（由 PuzzleResetUtility 调用，等价于 ResetMechanism）。</summary>
    public void ResetPuzzle()
    {
        ResetMechanism();
    }

    /// <summary>IPuzzleTriggerable：等价于释放起重机。</summary>
    public void Trigger()
    {
        TriggerCraneRelease();
    }

    /// <summary>
    /// 板身长度：设为大于 0 时直接拉伸板身（保留厚度与宽度，碰撞体跟着变），0 则用场景里已有的缩放。
    /// </summary>
    private void ApplyPlankSize()
    {
        if (seesawPlank == null || plankLength <= 0.05f)
            return;

        Vector3 scale = seesawPlank.localScale;
        seesawPlank.localScale = new Vector3(plankLength, scale.y, scale.z);
    }

    /// <summary>
    /// 重物初始高度：设为大于 0 时，重物从“落点高度 + 该值”处开始下落，0 则用场景里摆好的位置。
    /// </summary>
    private void ApplyWeightStartHeight()
    {
        if (craneWeightBox == null)
            return;

        Vector3 position = hasWeightInitialPos ? craneWeightInitialPos : craneWeightBox.position;
        if (weightDropHeight > 0.05f)
            position.y = impactCenterY + weightDropHeight;

        craneWeightBox.position = position;
    }

    /// <summary>
    /// 落点高度：默认让重物底面正好落在板身对应位置的顶面上（几何自动推算，可整体移动/旋转）。
    /// </summary>
    private void RecalculateImpactHeight()
    {
        float weightHalfHeight = 0.5f;
        if (weightCollider != null)
            weightHalfHeight = Mathf.Max(0.05f, weightCollider.bounds.extents.y);

        if (useManualImpactHeight || craneWeightBox == null)
        {
            impactCenterY = manualImpactHeight + impactHeightOffset;
            return;
        }

        float surfaceY = seesawPlank != null ? seesawPlank.position.y : craneWeightBox.position.y;
        if (plankCollider != null)
        {
            // 板身刚被旋转过，确保物理场景与变换同步后再做射线探测。
            Physics.SyncTransforms();
            Vector3 probe = new Vector3(craneWeightBox.position.x, plankCollider.bounds.max.y + 2f, craneWeightBox.position.z);
            if (plankCollider.Raycast(new Ray(probe, Vector3.down), out RaycastHit hit, 60f))
                surfaceY = hit.point.y;
            else
                surfaceY = plankCollider.bounds.max.y;
        }

        impactCenterY = surfaceY + weightHalfHeight + impactHeightOffset;
    }

    /// <summary>
    /// 翻转过程中让重物跟着板身一起下沉，避免出现"重物悬在半空"的假象。
    /// </summary>
    private void CarryWeightOnPlank()
    {
        if (craneWeightBox == null || plankCollider == null)
            return;

        float weightHalfHeight = weightCollider != null ? Mathf.Max(0.05f, weightCollider.bounds.extents.y) : 0.5f;
        Vector3 pos = craneWeightBox.position;
        Vector3 probe = new Vector3(pos.x, plankCollider.bounds.max.y + 1.5f, pos.z);
        if (plankCollider.Raycast(new Ray(probe, Vector3.down), out RaycastHit hit, 60f))
            pos.y = Mathf.Min(pos.y, hit.point.y + weightHalfHeight);
        else
            pos.y = Mathf.Min(pos.y, impactCenterY);

        craneWeightBox.position = pos;
    }

    private void ApplyPlankTilt(float angleDeg)
    {
        if (seesawPlank == null)
            return;

        seesawPlank.localRotation = plankAuthoredRotation *
            Quaternion.AngleAxis(angleDeg - plankAuthoredTiltDeg, tiltAxis);
    }

    private void OnDrawGizmosSelected()
    {
        Transform plank = seesawPlank != null ? seesawPlank : transform.Find("SeesawPlank");
        Transform weight = craneWeightBox != null ? craneWeightBox : transform.Find("CraneWeightBox");
        if (plank == null || weight == null)
            return;

        // 板身与翻转轴
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 1f);
        Vector3 axis = plank.parent != null
            ? plank.parent.TransformDirection(tiltAxisLocal.sqrMagnitude > 1e-6f ? tiltAxisLocal.normalized : Vector3.forward)
            : (tiltAxisLocal.sqrMagnitude > 1e-6f ? tiltAxisLocal.normalized : Vector3.forward);
        float plankLength = plank.localScale.x > 0.01f ? plank.localScale.x : 4f;
        Gizmos.DrawLine(plank.position - axis * plankLength * 0.25f, plank.position + axis * plankLength * 0.25f);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Vector3 halfLength = plank.right * (plankLength * 0.5f);
        Gizmos.DrawLine(plank.position - halfLength, plank.position + halfLength);

        // 重物下落到落点平面
        float halfH = weight.GetComponentInChildren<Collider>() != null
            ? Mathf.Max(0.05f, weight.GetComponentInChildren<Collider>().bounds.extents.y) : 0.5f;
        float targetY;
        if (useManualImpactHeight)
            targetY = manualImpactHeight + impactHeightOffset;
        else if (impactCenterY > 0.01f)
            targetY = impactCenterY;
        else
        {
            // 编辑模式（未运行 Awake）时用一个几何估算值，方便直接看到落点。
            Collider plankCol = plank.GetComponentInChildren<Collider>();
            targetY = plankCol != null ? plankCol.bounds.max.y : plank.position.y;
        }
        targetY += halfH;

        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.9f);
        Gizmos.DrawLine(new Vector3(weight.position.x, weight.position.y, weight.position.z),
            new Vector3(weight.position.x, targetY, weight.position.z));

        Vector3 marker = new Vector3(weight.position.x, targetY - halfH, weight.position.z);
        Gizmos.DrawWireCube(marker, new Vector3(1.2f, 0.05f, 1.2f));
    }
}
