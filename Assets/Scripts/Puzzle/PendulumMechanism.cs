using UnityEngine;

/// <summary>
/// 巨型摆锤机关（真实单摆运动）：
/// - 摆角由重力驱动积分：θ'' = -(g / L)·sin(相对平衡位置的摆角) - 阻尼·θ'
///   释放瞬间从静止松手，锤头自然加速、在最低点最快，越过最低点后减速上扬，不再像以前那样匀速转动。
/// - 冲击方向取锤头“当下的真实运动切线”（含往返方向的正负），冲击大小由“冲击速度”参数统一控制，
///   因此不会因为改了物理曲线就改变谜题数值。
/// - 支点 / 锤头碰撞体 / 摆动轴 / 摆长均可配置；摆长默认按锤头到支点的实际距离自动测量，
///   整个装置可以自由移动、旋转、缩放，不再依赖固定的世界坐标或固定朝向。
/// </summary>
[DisallowMultipleComponent]
public class PendulumMechanism : MonoBehaviour, IPuzzleResettable, IPuzzleTriggerable
{
    [Header("引用（留空自动查找）")]
    [SerializeField, InspectorName("支点（留空 = 本物体）")] private Transform pivotTransform;
    [SerializeField, InspectorName("锤头碰撞体（留空 = 子物体 PendulumHammer）")] private Collider hammerCollider;

    [Header("摆动平面")]
    [SerializeField, InspectorName("摆动轴（支点父空间）")] private Vector3 swingAxisLocal = Vector3.forward;
    [SerializeField, InspectorName("摆长（0 = 自动测量）"), Min(0f)] private float armLength = 0f;

    [Header("物理参数")]
    [SerializeField, InspectorName("重力倍率（1 = 真实重力）"), Min(0f)] private float gravityScale = 6f;
    [SerializeField, InspectorName("角阻尼（1/s，0 = 无损耗）"), Min(0f)] private float angularDamping = 0f;

    [Header("行程")]
    [SerializeField, InspectorName("起始角（度）")] private float startAngleDeg = -65f;
    [SerializeField, InspectorName("最大摆角（度）")] private float maxSwingAngleDeg = 65f;
    [SerializeField, InspectorName("自动往复摆动")] private bool autoSwing = false;
    [SerializeField, InspectorName("复位归位速度（度/秒）"), Min(1f)] private float returnSpeedDegPerSec = 120f;

    [Header("命中")]
    [SerializeField, InspectorName("冲击速度（m/s）"), Min(0f)] private float impulseSpeedToBox = 18.5f;
    [SerializeField, InspectorName("按实际锤速缩放冲击")] private bool scaleImpulseWithHammerSpeed = false;
    [SerializeField, InspectorName("命中后回弹比例"), Range(0f, 1f)] private float impactReboundRatio = 0.25f;
    [SerializeField, InspectorName("命中冷却（秒）"), Min(0.05f)] private float impactCooldownSeconds = 0.35f;
    [SerializeField, InspectorName("锤头判定余量"), Min(1f)] private float hitPadding = 1.1f;
    [SerializeField, InspectorName("命中判定层")] private LayerMask hitMask = ~0;

    private enum State
    {
        Held,       // 静止停在起始角，等待触发
        Swinging,   // 重力驱动摆动
        Returning   // 非自动模式一次行程结束后回到起始角
    }

    private State state = State.Held;
    private float currentAngleDeg;
    private float omegaRad;
    private int directionSign;
    private int reversalCount;
    private float impactCooldown;
    private bool hasStruckThisSwing;

    private bool geometryReady;
    private float armLengthResolved = 1f;
    private Vector3 armDirectionP = Vector3.down;   // 摆动平面内，θ = 0（支点无旋转）时的锤头方向
    private Vector3 swingAxis = Vector3.forward;
    private Vector3 torqueAxis = Vector3.right;     // up × axis，重力力矩方向
    private float equilibriumAngleDeg;
    private float referenceHammerSpeed = 1f;

    public bool IsReady => state == State.Held;
    /// <summary>IPuzzleTriggerable：待机时才能被按钮触发。</summary>
    public bool CanTrigger => IsReady;
    public bool IsSwinging => state == State.Swinging;
    public bool IsAutoSwing => autoSwing;
    public float CurrentAngleDeg => currentAngleDeg;
    public float AngularSpeedDeg => Mathf.Abs(omegaRad) * Mathf.Rad2Deg;
    public float HammerSpeed => Mathf.Abs(omegaRad) * armLengthResolved;
    public float ArmLength => armLengthResolved;

    private void Awake()
    {
        ResetPendulum();
    }

    private void OnValidate()
    {
        if (maxSwingAngleDeg < startAngleDeg + 1f)
            maxSwingAngleDeg = startAngleDeg + 1f;
    }

    [ContextMenu("立即释放摆锤")]
    private void DebugRelease()
    {
        TriggerRelease();
    }

    [ContextMenu("复位摆锤")]
    private void DebugReset()
    {
        ResetPendulum();
    }

    private void Update()
    {
        if (impactCooldown > 0f)
            impactCooldown -= Time.deltaTime;

        Simulate(Time.deltaTime);
    }

    /// <summary>
    /// 推进摆锤状态一步（dt 为秒）。抽成独立方法便于在编辑器里以固定步长复现摆动曲线。
    /// </summary>
    private void Simulate(float dt)
    {
        if (dt <= 0f || !geometryReady)
            return;

        if (state == State.Returning)
        {
            currentAngleDeg = Mathf.MoveTowards(currentAngleDeg, startAngleDeg, returnSpeedDegPerSec * dt);
            ApplyAngle(currentAngleDeg);
            if (Mathf.Abs(currentAngleDeg - startAngleDeg) < 0.01f)
                HoldAtStart();
            return;
        }

        if (state != State.Swinging)
            return;

        float gravity = Mathf.Abs(Physics.gravity.magnitude) * gravityScale;
        float damping = autoSwing ? 0f : angularDamping;

        // 分步积分：单步转角控制在 3 度以内，保证高速下也不会跳过于夸张的角度。
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(omegaRad) * Mathf.Rad2Deg * dt / 3f), 1, 8);
        float h = dt / steps;

        for (int i = 0; i < steps; i++)
        {
            // 锤头方向（摆动平面内）：u(θ) = AngleAxis(θ) * u0
            Vector3 u = Quaternion.AngleAxis(currentAngleDeg, swingAxis) * armDirectionP;
            // u·(up × axis) 就是相对平衡位置的正弦量，因此这里的角加速度等价于 -(g/L)·sin(摆角)
            float gravityTerm = Vector3.Dot(u, torqueAxis);
            float angularAcceleration = -(gravity / armLengthResolved) * gravityTerm - damping * omegaRad;

            omegaRad += angularAcceleration * h;
            currentAngleDeg += omegaRad * Mathf.Rad2Deg * h;

            if (currentAngleDeg > maxSwingAngleDeg)
            {
                // 上扬到设定上限：视为撞到机械限位，速度归零后由重力带回。
                currentAngleDeg = maxSwingAngleDeg;
                if (omegaRad > 0f)
                    omegaRad = 0f;
            }

            TrackDirection();
            if (state != State.Swinging)
                break;
        }

        ApplyAngle(currentAngleDeg);
        CheckHammerHit();
    }

    /// <summary>
    /// 记录转向次数：非自动模式下，一次完整行程（上去、回来、再反向）后回到起始角待机。
    /// </summary>
    private void TrackDirection()
    {
        if (Mathf.Abs(omegaRad) < 0.02f)
            return;

        int sign = omegaRad > 0f ? 1 : -1;
        if (directionSign != 0 && sign != directionSign)
        {
            reversalCount++;
            if (!autoSwing && reversalCount >= 2)
            {
                BeginReturn();
                return;
            }
        }

        directionSign = sign;
    }

    private void BeginReturn()
    {
        state = State.Returning;
        omegaRad = 0f;
        directionSign = 0;
        reversalCount = 0;
        hasStruckThisSwing = false;
    }

    private void HoldAtStart()
    {
        state = State.Held;
        omegaRad = 0f;
        directionSign = 0;
        reversalCount = 0;
        currentAngleDeg = startAngleDeg;
        ApplyAngle(currentAngleDeg);
    }

    private void CheckHammerHit()
    {
        if (hammerCollider == null || impactCooldown > 0f)
            return;

        // 非自动模式每次释放只结算一次命中，避免回摆时重复打在同一目标上。
        if (!autoSwing && hasStruckThisSwing)
            return;

        // 接近转向点时锤头几乎静止，不产生冲击。
        if (Mathf.Abs(omegaRad) < 0.05f)
            return;

        Vector3 tangentDirection = ComputeTangentDirection();
        if (tangentDirection.sqrMagnitude < 0.5f)
            return;

        float impulseSpeed = ResolveImpulseSpeed();

        Bounds b = hammerCollider.bounds;
        Collider[] hits = Physics.OverlapBox(
            b.center, b.extents * Mathf.Max(1f, hitPadding), hammerCollider.transform.rotation,
            hitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform) ||
                (pivotTransform != null && hit.transform.IsChildOf(pivotTransform)))
                continue;

            KineticBox box = hit.GetComponentInParent<KineticBox>();
            if (box != null)
            {
                hasStruckThisSwing = true;
                box.ApplyFreeImpulse(tangentDirection, impulseSpeed);
                RegisterImpact();
                Debug.Log($"[PendulumMechanism] 锤头在 {currentAngleDeg:F1}° 命中方块，切线方向 {tangentDirection}，速度 {impulseSpeed:F1} m/s", this);
                return;
            }

            PlayerMovement player = hit.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                hasStruckThisSwing = true;
                Vector3 contactPoint = b.ClosestPoint(player.transform.position + Vector3.up * 0.9f);
                player.ResolveIncomingImpact(b.center, contactPoint, tangentDirection, tangentDirection * impulseSpeed);
                RegisterImpact();
                Debug.Log($"[PendulumMechanism] 锤头在 {currentAngleDeg:F1}° 命中玩家，切线方向 {tangentDirection}，速度 {impulseSpeed:F1} m/s", this);
                return;
            }
        }
    }

    private void RegisterImpact()
    {
        impactCooldown = impactCooldownSeconds;

        // 自动往复模式保持原有摆幅与节奏；单次行程模式让锤头小幅回弹，避免追着刚抛出的物体跑。
        if (!autoSwing)
            omegaRad = -omegaRad * impactReboundRatio;
    }

    /// <summary>
    /// 锤头当下的真实运动切线方向（世界空间，已带往返方向符号）。
    /// </summary>
    private Vector3 ComputeTangentDirection()
    {
        Vector3 u = Quaternion.AngleAxis(currentAngleDeg, swingAxis) * armDirectionP;
        Vector3 tangent = Vector3.Cross(swingAxis, u);       // du/dθ，指向 θ 增大的方向
        if (omegaRad < 0f)
            tangent = -tangent;

        Transform parent = pivotTransform != null ? pivotTransform.parent : null;
        return parent != null ? parent.TransformDirection(tangent) : tangent;
    }

    private float ResolveImpulseSpeed()
    {
        if (!scaleImpulseWithHammerSpeed)
            return impulseSpeedToBox;

        float scale = Mathf.Clamp(HammerSpeed / referenceHammerSpeed, 0f, 2f);
        return impulseSpeedToBox * scale;
    }

    public void TriggerRelease()
    {
        if (state != State.Held)
            return;

        state = State.Swinging;
        omegaRad = 0f;
        directionSign = 0;
        reversalCount = 0;
        impactCooldown = 0f;
        hasStruckThisSwing = false;
        Debug.Log("[PendulumMechanism] 释放摆锤：松手后由重力驱动摆动。", this);
    }

    public void ResetPendulum()
    {
        ResolveGeometry();

        omegaRad = 0f;
        directionSign = 0;
        reversalCount = 0;
        impactCooldown = 0f;
        hasStruckThisSwing = false;
        currentAngleDeg = startAngleDeg;
        state = autoSwing ? State.Swinging : State.Held;
        ApplyAngle(currentAngleDeg);
    }

    /// <summary>统一重置接口（由 PuzzleResetUtility 调用，等价于 ResetPendulum）。</summary>
    public void ResetPuzzle()
    {
        ResetPendulum();
    }

    /// <summary>IPuzzleTriggerable：等价于触发释放。</summary>
    public void Trigger()
    {
        TriggerRelease();
    }

    private void ResolveGeometry()
    {
        geometryReady = false;

        if (pivotTransform == null)
            pivotTransform = transform;

        if (hammerCollider == null)
        {
            Transform hammerChild = transform.Find("PendulumHammer");
            if (hammerChild != null)
                hammerCollider = hammerChild.GetComponentInChildren<Collider>();
        }

        if (hammerCollider == null)
        {
            Debug.LogError("[PendulumMechanism] 没有锤头碰撞体，摆锤不会命中任何目标。请指定锤头碰撞体。", this);
            return;
        }

        swingAxis = swingAxisLocal.sqrMagnitude > 1e-6f ? swingAxisLocal.normalized : Vector3.forward;

        // 锤头在支点局部空间中的方向：与当前摆角无关，θ = 0 时它同时也是摆动平面内的参考方向。
        Vector3 armLocal = pivotTransform.InverseTransformPoint(hammerCollider.transform.position);
        armLocal -= swingAxis * Vector3.Dot(armLocal, swingAxis);
        if (armLocal.sqrMagnitude < 1e-6f)
        {
            Debug.LogError("[PendulumMechanism] 锤头与支点重合或正好落在摆动轴上，无法确定摆动平面。", this);
            return;
        }
        armDirectionP = armLocal.normalized;

        armLengthResolved = armLength > 0.05f
            ? armLength
            : Mathf.Max(0.05f, Vector3.Distance(pivotTransform.position, hammerCollider.transform.position));

        // 把重力投影到摆动平面，得到真正的“向下”，于是支点被旋转摆放时平衡角也能算对。
        Transform parent = pivotTransform.parent;
        Vector3 downP = parent != null ? parent.InverseTransformDirection(Vector3.down) : Vector3.down;
        downP -= swingAxis * Vector3.Dot(downP, swingAxis);
        if (downP.sqrMagnitude < 1e-6f)
        {
            Debug.LogError("[PendulumMechanism] 摆动轴与重力平行，重力无法驱动摆动。请检查摆动轴设置。", this);
            return;
        }

        downP.Normalize();
        equilibriumAngleDeg = Vector3.SignedAngle(armDirectionP, downP, swingAxis);
        torqueAxis = Vector3.Cross(-downP, swingAxis);   // up × axis

        float gravity = Mathf.Abs(Physics.gravity.magnitude) * gravityScale;
        float swingSpan = Mathf.Abs(startAngleDeg - equilibriumAngleDeg) * Mathf.Deg2Rad;
        referenceHammerSpeed = Mathf.Max(1f, Mathf.Sqrt(2f * gravity * armLengthResolved * (1f - Mathf.Cos(swingSpan))));

        geometryReady = true;
    }

    private void ApplyAngle(float angleDeg)
    {
        if (pivotTransform == null)
            return;

        pivotTransform.localRotation = Quaternion.AngleAxis(angleDeg, swingAxis);
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = pivotTransform != null ? pivotTransform : transform;
        Transform parent = pivot.parent;

        Vector3 pivotPos = pivot.position;
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 1f);
        Gizmos.DrawWireSphere(pivotPos, 0.35f);

        Vector3 axisP = swingAxisLocal.sqrMagnitude > 1e-6f ? swingAxisLocal.normalized : Vector3.forward;
        Vector3 axisWorld = parent != null ? parent.TransformDirection(axisP) : axisP;

        Transform hammer = hammerCollider != null ? hammerCollider.transform : transform.Find("PendulumHammer");
        if (hammer == null)
            return;

        Vector3 armLocal = pivot.InverseTransformPoint(hammer.position);
        armLocal -= axisP * Vector3.Dot(armLocal, axisP);
        if (armLocal.sqrMagnitude < 1e-6f)
            return;

        float length = armLength > 0.05f ? armLength : armLocal.magnitude;
        Vector3 u0 = armLocal.normalized;

        // 摆动平面与摆动轴提示
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(pivotPos - axisWorld * length * 0.6f, pivotPos + axisWorld * length * 0.6f);

        // 从起始角到最大上扬角的可达弧线
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        DrawArc(pivotPos, u0, axisP, length, startAngleDeg, maxSwingAngleDeg, parent);

        // 起始角与平衡位置
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
        DrawArm(pivotPos, u0, axisP, length, startAngleDeg, parent);
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        DrawArm(pivotPos, u0, axisP, length, equilibriumAngleDeg, parent);
    }

    private static void DrawArc(Vector3 center, Vector3 u0, Vector3 axis, float radius, float fromDeg, float toDeg, Transform parent)
    {
        const int segments = 24;
        Vector3 previous = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(fromDeg, toDeg, i / (float)segments);
            Vector3 point = center + ArcPoint(u0, axis, radius, angle, parent);
            if (i > 0)
                Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }

    private static void DrawArm(Vector3 center, Vector3 u0, Vector3 axis, float radius, float angleDeg, Transform parent)
    {
        Vector3 end = center + ArcPoint(u0, axis, radius, angleDeg, parent);
        Gizmos.DrawLine(center, end);
        Gizmos.DrawWireSphere(end, radius * 0.05f);
    }

    private static Vector3 ArcPoint(Vector3 u0, Vector3 axis, float radius, float angleDeg, Transform parent)
    {
        Vector3 localDirection = Quaternion.AngleAxis(angleDeg, axis) * u0;
        Vector3 worldDirection = parent != null ? parent.TransformDirection(localDirection) : localDirection;
        return worldDirection * radius;
    }
}
