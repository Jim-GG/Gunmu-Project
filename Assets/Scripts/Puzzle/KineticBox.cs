using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzzle cube that accepts:
/// - Strict 6-direction cardinal impulses from the Player's Kinetic Dash (ApplyCardinalImpulse).
/// - Free/diagonal 3D impulses from scripted level mechanisms such as the Giant Pendulum (ApplyFreeImpulse).
/// Supports remote Momentum Lock (动量锁定), scalar impulse accumulation, direction override, and Bullet Time highlighting.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class KineticBox : MonoBehaviour, IPuzzleResettable
{
    [Header("物理参数")]
    [SerializeField, Min(0f)] private float groundLinearDamping = 0.8f;
    [SerializeField, Min(0f), InspectorName("空中阻尼（离地时）")] private float airborneLinearDamping = 0.08f;
    [SerializeField, Min(0f)] private float launchedLinearDamping = 0.02f;
    [SerializeField, Min(0f)] private float stopSpeedThreshold = 0.15f;
    [SerializeField, InspectorName("自动设置刚体参数（冻结旋转/插值/连续检测）")] private bool applyPhysicsDefaults = true;
    [SerializeField, InspectorName("弹射后低阻尼持续时间（秒）"), Min(0f)] private float highSpeedLaunchDuration = 1.5f;
    [SerializeField, InspectorName("锁定可累积的最高速度（m/s）"), Min(0f)] private float maxStoredSpeed = 40f;

    [Header("滑行物理材质")]
    [SerializeField, InspectorName("滑行材质（留空 = 按下面参数自动生成）")] private PhysicsMaterial surfaceMaterial;
    [SerializeField, InspectorName("摩擦系数"), Min(0f)] private float surfaceFriction = 0.05f;
    [SerializeField, InspectorName("弹性"), Min(0f)] private float surfaceBounciness = 0f;

    [Header("判定阈值")]
    [SerializeField, InspectorName("冲量间隔（秒）"), Min(0f)] private float impulseInterval = 0.1f;
    [SerializeField, InspectorName("反射间隔（秒）"), Min(0f)] private float reflectionInterval = 0.12f;
    [SerializeField, InspectorName("撞玩家判定的最低速度"), Min(0f)] private float minPlayerImpactSpeed = 3.5f;
    [SerializeField, InspectorName("方块之间传力的最低速度"), Min(0f)] private float minBoxTransferSpeed = 3.5f;
    [SerializeField, InspectorName("前瞻距离余量（米）"), Min(0f)] private float castMargin = 0.02f;
    [SerializeField, InspectorName("传力横向容差（米）"), Min(0f)] private float boxTransferLateralTolerance = 0.9f;
    [SerializeField, InspectorName("传力纵向容差（米）"), Min(0f)] private float boxTransferVerticalTolerance = 1.0f;
    [SerializeField, InspectorName("对锁定方块的纵向容差（米）"), Min(0f)] private float boxTransferLockedVerticalTolerance = 2.0f;
    [SerializeField, InspectorName("玩家碰撞体尺寸")] private Vector3 playerBoundsSize = new Vector3(0.95f, 1.9f, 0.95f);
    [SerializeField, InspectorName("玩家中心高度偏移（米）"), Min(0f)] private float playerCenterHeight = 0.9f;
    [SerializeField, InspectorName("玩家扫掠余量（米）"), Min(0f)] private float playerSweepMargin = 0.4f;

    [Header("外观：静态状态")]
    [SerializeField, InspectorName("静态材质（留空 = 用场景里的材质）")] private Material appearanceMaterial;
    [SerializeField, InspectorName("静态贴图（留空 = 用材质自带贴图）")] private Texture appearanceTexture;
    [SerializeField, InspectorName("静态状态染色")] private bool tintStaticAppearance = true;

    [Header("外观：特殊状态（子弹时间 / 动量锁定）")]
    [SerializeField, InspectorName("特殊状态材质（留空 = 直接在静态材质上染色）")] private Material stateMaterial;
    [SerializeField, InspectorName("发光强度"), Min(0f)] private float stateEmissionIntensity = 1.5f;
    [SerializeField, InspectorName("静态颜色")] private Color normalColor = new Color(0.95f, 0.75f, 0.15f, 1f);
    [SerializeField, InspectorName("子弹时间高亮色（可锁定）")] private Color bulletTimeHighlightColor = new Color(1f, 0.52f, 0.12f, 1f);
    [SerializeField, InspectorName("子弹时间瞄准色（准心对准）")] private Color bulletTimeHoverColor = new Color(1f, 0.96f, 0.82f, 1f);
    [SerializeField, InspectorName("锁定基础色")] private Color lockedBaseColor = new Color(0.85f, 0.15f, 1.0f, 1f);
    [SerializeField, InspectorName("锁定充能色")] private Color lockedChargedColor = new Color(1.0f, 0.15f, 0.05f, 1f);

    [Header("动量箭头")]
    [SerializeField, InspectorName("箭头物体（可拖场景里已摆好的箭头，留空则自动生成）")] private Transform momentumArrow;
    [SerializeField, InspectorName("箭头预制体（留空 = 内置方块箭头）")] private GameObject momentumArrowPrefab;

    [Header("撞击特效")]
    [SerializeField, InspectorName("撞击特效预制体（留空 = 不生成）")] private GameObject impactEffectPrefab;
    [SerializeField, InspectorName("特效存活时间（秒）"), Min(0f)] private float impactEffectLifetime = 1.6f;
    [SerializeField, InspectorName("撞击时触发")] private UnityEvent onImpact = new UnityEvent();

    private Rigidbody rb;
    private BoxCollider boxCollider;
    private Renderer boxRenderer;
    private Material baseMaterial;
    private PhysicsMaterial generatedSurfaceMaterial;
    private MaterialPropertyBlock propBlock;
    private PlayerMovement cachedPlayer;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 lastImpulseDirection;
    private float lastImpulseTime = -10f;
    private float lastBouncyWallTime = -10f;
    private float lastGroundContactTime = -10f;
    private Vector3 recentLinearVelocity;

    // Momentum Lock & trajectory mode state
    private bool isMomentumLocked;
    private float storedSpeed;
    private Vector3 storedDirection;
    private float highSpeedLaunchTimer;
    private bool isFreeTrajectory;

    // Bullet time visual state
    private bool inBulletTime;
    private bool isHoveredInBulletTime;

    // Runtime 3D direction indicator arrow
    private Transform arrowRoot;
    private Renderer[] arrowRenderers;

    public Rigidbody Body => rb;
    public BoxCollider Collider => boxCollider;
    public bool IsMomentumLocked => isMomentumLocked;
    public float StoredSpeed => storedSpeed;
    public Vector3 StoredDirection => storedDirection;
    /// <summary>最近一次撞击点（弹性墙反射等），onImpact 事件的监听者可以读它。</summary>
    public Vector3 LastImpactPoint { get; private set; }
    /// <summary>最近一次撞击法线（世界空间）。</summary>
    public Vector3 LastImpactNormal { get; private set; }
    public Vector3 PreImpactVelocity => rb != null && rb.linearVelocity.sqrMagnitude >= recentLinearVelocity.sqrMagnitude
        ? rb.linearVelocity
        : recentLinearVelocity;

    private void Awake()
    {
        GameActions.PuzzlePresentation.Ensure(gameObject);
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        boxRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();

        if (boxRenderer != null)
        {
            baseMaterial = appearanceMaterial != null ? appearanceMaterial : boxRenderer.sharedMaterial;
            if (baseMaterial != null)
                boxRenderer.sharedMaterial = baseMaterial;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        // 保持立方体严格对齐轴向（六面方向才稳定）。不需要这套行为时可在 Inspector 关掉。
        if (applyPhysicsDefaults)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = groundLinearDamping;
        }

        ApplySurfaceMaterial();

        CreateDirectionArrowIndicator();
        UpdateVisuals();
        WarnIfEmissionUnsupported();
    }

    /// <summary>
    /// 滑行物理材质：指定了资源就用资源，否则按参数生成一份（与本实例一起销毁）。
    /// </summary>
    private void ApplySurfaceMaterial()
    {
        PhysicsMaterial material = surfaceMaterial;
        if (material == null)
        {
            generatedSurfaceMaterial = new PhysicsMaterial("KineticBoxSlide")
            {
                dynamicFriction = surfaceFriction,
                staticFriction = surfaceFriction,
                bounciness = surfaceBounciness,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            material = generatedSurfaceMaterial;
        }

        boxCollider.sharedMaterial = material;
    }

    /// <summary>
    /// 只有开启“自动设置刚体参数”时才覆写阻尼，否则完全交给设计师在 Rigidbody 上调。
    /// </summary>
    private void SetDamping(float value)
    {
        if (applyPhysicsDefaults)
            rb.linearDamping = value;
    }

    private void OnDestroy()
    {
        if (generatedSurfaceMaterial != null)
            Destroy(generatedSurfaceMaterial);
    }
    /// <summary>
    /// 特殊状态靠 _EmissionColor 发光，而 URP Lit 需要材质上开启 _EMISSION 关键字才会真的亮。
    /// 这里只做一次性提示，不静默失败。
    /// </summary>
    private void WarnIfEmissionUnsupported()
    {
        Material material = stateMaterial != null ? stateMaterial : baseMaterial;
        if (material == null)
            return;

        if (!material.HasProperty("_EmissionColor"))
        {
            Debug.LogWarning("[KineticBox] 特殊状态材质没有 _EmissionColor 属性，子弹时间/动量锁定的发光会失效。", this);
            return;
        }

        if (!material.IsKeywordEnabled("_EMISSION"))
        {
            Debug.LogWarning("[KineticBox] 特殊状态材质未开启 Emission，发光不会显示。可在材质 Inspector 勾选 Emission，或直接使用 Assets/Materials/KineticBox_State.mat。", this);
        }
    }

    private void Start()
    {
        cachedPlayer = FindFirstObjectByType<PlayerMovement>();
    }

    private void CreateDirectionArrowIndicator()
    {
        Transform root = momentumArrow;

        if (root == null && momentumArrowPrefab != null)
        {
            GameObject instance = Instantiate(momentumArrowPrefab, transform, false);
            instance.name = "MomentumArrow";
            root = instance.transform;
        }

        if (root != null)
        {
            // 箭头约定：朝 +Z、长度 1 个单位（脚本会沿 Z 按累积速度缩放）。
            root.SetParent(transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            arrowRoot = root;
        }
        else
        {
            arrowRoot = CreateProceduralArrow();
        }

        arrowRenderers = arrowRoot.GetComponentsInChildren<Renderer>(true);
        arrowRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 没有指定箭头资源时的内置箭头（两根方块：杆 + 头）。
    /// </summary>
    private Transform CreateProceduralArrow()
    {
        GameObject rootObj = new GameObject("MomentumArrowRoot");
        Transform root = rootObj.transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "ArrowShaft";
        Destroy(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(root, false);
        shaft.transform.localPosition = new Vector3(0f, 0f, 0.75f);
        shaft.transform.localScale = new Vector3(0.18f, 0.18f, 1.1f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "ArrowHead";
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(root, false);
        head.transform.localPosition = new Vector3(0f, 0f, 1.35f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.38f, 0.22f, 0.38f);

        return root;
    }

    private void FixedUpdate()
    {
        if (isMomentumLocked)
        {
            recentLinearVelocity = Vector3.zero;
            return;
        }

        if (highSpeedLaunchTimer > 0f)
        {
            highSpeedLaunchTimer -= Time.fixedDeltaTime;
            SetDamping(launchedLinearDamping);
        }
        else
        {
            // 离地时用很小的空气阻尼（否则坠落像羽毛），贴地滑行时才用高阻尼把方块刹住。
            SetDamping(IsOnGround() ? groundLinearDamping : airborneLinearDamping);
        }

        // Enforce strict 6-direction horizontal snapping when driven by Player's 6-axis dash
        if (!isFreeTrajectory)
        {
            Vector3 vel = rb.linearVelocity;
            Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
            if (horizontal.sqrMagnitude > 0.0001f)
            {
                if (horizontal.magnitude < stopSpeedThreshold && Mathf.Abs(vel.y) < stopSpeedThreshold)
                {
                    rb.linearVelocity = Vector3.zero;
                }
                else
                {
                    Vector3 snappedHorizDir = SnapToCardinalDirection(horizontal.normalized, allowVertical: false);
                    Vector3 lockedHoriz = snappedHorizDir * horizontal.magnitude;
                    rb.linearVelocity = new Vector3(lockedHoriz.x, vel.y, lockedHoriz.z);
                }
            }
        }

        recentLinearVelocity = rb.linearVelocity;

        // Check BouncyWall reflection ahead of impact so PhysX does not dampen velocity first
        CheckBouncyWallReflection();

        // Check Box-to-Box impulse transfer!
        CheckBoxToBoxImpulseTransfer();

        // Check Moving Box -> Player collision (supports stationary guarding player parrying incoming flying/sliding boxes!)
        CheckMovingBoxPlayerCollision();
    }

    private void CheckBouncyWallReflection()
    {
        if (Time.unscaledTime - lastBouncyWallTime < reflectionInterval)
            return;

        Vector3 vel = recentLinearVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f)
            return;

        float castDist = speed * Time.fixedDeltaTime + castMargin;
        if (Physics.BoxCast(boxCollider.bounds.center, boxCollider.bounds.extents * 0.92f, vel.normalized,
            out RaycastHit hit, transform.rotation, castDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            BouncyWall bouncy = hit.collider.GetComponentInParent<BouncyWall>();
            if (bouncy != null)
            {
                ReflectOffBouncyWall(bouncy, hit.point, hit.normal, vel);
            }
        }
    }

    private void ReflectOffBouncyWall(BouncyWall bouncy, Vector3 hitPoint, Vector3 hitNormal, Vector3 incomingVel)
    {
        if (Time.unscaledTime - lastBouncyWallTime < reflectionInterval)
            return;

        // 阈值与倍率统一由弹性墙决定，保证方块与玩家被弹射的结果一致。
        if (!bouncy.TryReflect(incomingVel, hitNormal, out Vector3 reflectedVel))
            return;

        lastBouncyWallTime = Time.unscaledTime;
        lastImpulseTime = -10f; // Allow immediate impulse application

        ApplyFreeImpulse(reflectedVel.normalized, reflectedVel.magnitude);
        bouncy.NotifyReflected(hitPoint, hitNormal, reflectedVel);
        SpawnImpactEffect(hitPoint, hitNormal);
        Debug.Log($"[KineticBox] Reflected off BouncyWall '{bouncy.name}' with velocity {reflectedVel} ({reflectedVel.magnitude:F1} m/s)!", this);
    }

    /// <summary>
    /// 撞击表现：可选的特效预制体 + onImpact 事件（不再硬依赖 Demo 层）。
    /// </summary>
    private void SpawnImpactEffect(Vector3 point, Vector3 normal)
    {
        LastImpactPoint = point;
        LastImpactNormal = normal;

        if (impactEffectPrefab != null)
        {
            Quaternion rotation = normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            GameObject effect = Instantiate(impactEffectPrefab, point + normal * 0.02f, rotation);
            if (impactEffectLifetime > 0f)
                Destroy(effect, impactEffectLifetime);
        }

        onImpact.Invoke();
    }

    private void CheckMovingBoxPlayerCollision()
    {
        Vector3 vel = recentLinearVelocity;
        float speed = vel.magnitude;
        if (speed < minPlayerImpactSpeed)
            return;

        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<PlayerMovement>();
        if (cachedPlayer == null || !cachedPlayer.isActiveAndEnabled || cachedPlayer.IsIgnoringBox(boxCollider))
            return;

        Vector3 playerCenter = cachedPlayer.transform.position + Vector3.up * playerCenterHeight;
        Vector3 toPlayer = playerCenter - boxCollider.bounds.center;
        if (Vector3.Dot(vel.normalized, toPlayer.normalized) < -0.15f)
            return;

        Bounds expandedBox = boxCollider.bounds;
        expandedBox.Expand(speed * Time.fixedDeltaTime * 2f + playerSweepMargin);

        Bounds playerBounds = new Bounds(playerCenter, playerBoundsSize);
        if (!expandedBox.Intersects(playerBounds))
            return;

        Vector3 contactPoint = boxCollider.ClosestPoint(playerCenter);
        Vector3 normalTowardsPlayer = (playerCenter - boxCollider.bounds.center);
        normalTowardsPlayer = normalTowardsPlayer.sqrMagnitude > 0.001f ? normalTowardsPlayer.normalized : vel.normalized;

        cachedPlayer.ResolveBoxCollision(this, contactPoint, normalTowardsPlayer, vel);
    }

    /// <summary>最近是否踩在东西上：区分“贴地滑行”与“空中坠落”两种阻尼。</summary>
    private bool IsOnGround()
    {
        return Time.time - lastGroundContactTime < 0.15f;
    }

    private void OnCollisionStay(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                lastGroundContactTime = Time.time;
                return;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isMomentumLocked)
            return;

        BouncyWall bouncy = collision.collider.GetComponentInParent<BouncyWall>();
        if (bouncy != null)
        {
            Vector3 vel = PreImpactVelocity;
            if (vel.magnitude > 0.01f)
            {
                ContactPoint cp = collision.GetContact(0);
                ReflectOffBouncyWall(bouncy, cp.point, cp.normal, vel);
                return;
            }
        }

        if (collision.collider.TryGetComponent(out PlayerMovement pm) && !pm.IsIgnoringBox(boxCollider))
        {
            Vector3 vel = PreImpactVelocity;
            if (vel.magnitude >= minPlayerImpactSpeed)
            {
                ContactPoint cp = collision.GetContact(0);
                pm.ResolveBoxCollision(this, cp.point, -cp.normal, vel);
            }
        }
    }

    /// <summary>
    /// Applies a small damped rebound to the box when parried by the player, preventing the box from flying too far away.
    /// </summary>
    public void ApplyGentleGuardRebound(Vector3 reboundDirection, float reboundSpeed)
    {
        if (isMomentumLocked)
            return;

        highSpeedLaunchTimer = 0f;
        isFreeTrajectory = false;
        SetDamping(groundLinearDamping);

        Vector3 dir = reboundDirection.sqrMagnitude > 0.001f
            ? SnapToCardinalDirection(reboundDirection.normalized, allowVertical: true)
            : Vector3.zero;

        rb.linearVelocity = dir * Mathf.Max(0f, reboundSpeed);
        recentLinearVelocity = rb.linearVelocity;
    }

    /// <summary>
    /// Stops horizontal and upward motion upon a plastic (unguarded) collision.
    /// </summary>
    public void StopPlasticMotion()
    {
        if (isMomentumLocked)
            return;

        highSpeedLaunchTimer = 0f;
        isFreeTrajectory = false;
        recentLinearVelocity = Vector3.zero;
        SetDamping(groundLinearDamping);
        rb.linearVelocity = new Vector3(0f, Mathf.Min(0f, rb.linearVelocity.y), 0f);
    }

    private void CheckBoxToBoxImpulseTransfer()
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
        float speed = horizVel.magnitude;
        if (speed < minBoxTransferSpeed)
            return;

        Vector3 moveDir = horizVel.normalized;
        KineticBox[] boxes = FindObjectsByType<KineticBox>(FindObjectsSortMode.None);
        for (int i = 0; i < boxes.Length; i++)
        {
            KineticBox other = boxes[i];
            if (other == null || other == this)
                continue;

            Vector3 toOther = other.transform.position - transform.position;
            Vector3 horizToOther = new Vector3(toOther.x, 0f, toOther.z);
            float forwardDist = Vector3.Dot(horizToOther, moveDir);
            float lateralDist = Vector3.Cross(moveDir, horizToOther).magnitude;
            float verticalDist = Mathf.Abs(toOther.y);

            // Forgiving vertical window (±2.0m) when striking a Momentum-Locked airborne box so locking slightly high/low still connects!
            float maxVertTolerance = other.IsMomentumLocked ? boxTransferLockedVerticalTolerance : boxTransferVerticalTolerance;
            float hitRadius = (boxCollider.bounds.extents.x + other.boxCollider.bounds.extents.x) + speed * Time.fixedDeltaTime + castMargin;

            if (forwardDist > 0f && forwardDist <= hitRadius && lateralDist <= boxTransferLateralTolerance && verticalDist <= maxVertTolerance)
            {
                // 本帧还没真的贴上（判定前瞻会比接触早一帧），把方块推到接触面再停住，避免看上去“隔空碰撞”。
                float surfaceGap = forwardDist - (boxCollider.bounds.extents.x + other.boxCollider.bounds.extents.x);
                if (surfaceGap > 0f)
                {
                    float maxSnap = speed * Time.fixedDeltaTime + castMargin;
                    Vector3 snapped = rb.position + moveDir * Mathf.Min(surfaceGap, maxSnap);
                    rb.position = snapped;
                }

                // Transfer impulse strictly along the 6 cardinal axes of the struck box, and stop the Cue Ball box!
                // 方块之间的传力（任意方块撞任意方块）
                other.ApplyCardinalImpulse(-moveDir, moveDir, speed, allowVerticalAxis: false);
                rb.linearVelocity = Vector3.zero;
                recentLinearVelocity = Vector3.zero;
                isFreeTrajectory = false;
                Debug.Log($"[KineticBox] 方块 '{name}' 以 {speed:F1} m/s 撞上 '{other.name}'，沿 {moveDir} 传递动量，自身停下！", this);
                return;
            }
        }
    }

    /// <summary>
    /// Locks the box in space, preserving its current speed magnitude (Rule 1A).
    /// </summary>
    public void LockMomentum()
    {
        if (isMomentumLocked)
            return;

        Vector3 currentVel = rb.linearVelocity;
        storedSpeed = currentVel.magnitude;
        storedDirection = currentVel.sqrMagnitude > 0.01f
            ? (isFreeTrajectory ? currentVel.normalized : SnapToCardinalDirection(currentVel.normalized, allowVertical: true))
            : Vector3.zero;

        isMomentumLocked = true;
        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Lock);
        recentLinearVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        UpdateVisuals();
        Debug.Log($"[KineticBox] Momentum Locked at {transform.position}! Initial stored speed: {storedSpeed:F1} m/s", this);
    }

    /// <summary>
    /// Releases the Momentum Lock and launches the box with accumulated speed along the last hit direction.
    /// </summary>
    public void ReleaseMomentum()
    {
        if (!isMomentumLocked)
            return;

        isMomentumLocked = false;
        rb.isKinematic = false;

        if (storedDirection.sqrMagnitude > 0.01f && storedSpeed > 0.01f)
        {
            Vector3 launchVel = storedDirection.normalized * storedSpeed;
            highSpeedLaunchTimer = highSpeedLaunchDuration;
            SetDamping(launchedLinearDamping);
            rb.linearVelocity = launchVel;
            recentLinearVelocity = launchVel;

            Debug.Log($"[KineticBox] Momentum Released! Launching along {storedDirection} at {storedSpeed:F1} m/s", this);
        }

        storedSpeed = 0f;
        storedDirection = Vector3.zero;
        UpdateVisuals();

        // If the released box immediately collides with the player, resolve guard/plastic collision right away!
        CheckMovingBoxPlayerCollision();
        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Release);
    }

    public void SetBulletTimeHighlight(bool bulletTimeActive, bool hovered)
    {
        if (inBulletTime == bulletTimeActive && isHoveredInBulletTime == hovered)
            return;

        inBulletTime = bulletTimeActive;
        isHoveredInBulletTime = hovered;
        UpdateVisuals();
    }

    /// <summary>
    /// Used by the Player's Kinetic Dash: snaps impulse strictly to one of the cube's 6 cardinal directions.
    /// </summary>
    public Vector3 ApplyCardinalImpulse(Vector3 hitNormal, Vector3 fallbackDirection, float impulseSpeed, bool allowVerticalAxis = true)
    {
        if (Time.unscaledTime - lastImpulseTime < impulseInterval)
            return lastImpulseDirection;

        lastImpulseTime = Time.unscaledTime;
        isFreeTrajectory = false;

        Vector3 rawPushDir = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : fallbackDirection;
        Vector3 cardinalDir = SnapToCardinalDirection(rawPushDir, allowVertical: allowVerticalAxis);
        lastImpulseDirection = cardinalDir;

        if (isMomentumLocked)
        {
            storedSpeed = Mathf.Min(storedSpeed + impulseSpeed, maxStoredSpeed);
            storedDirection = cardinalDir;
            UpdateVisuals();
            Debug.Log($"[KineticBox] Locked Box absorbed 6-Axis player impulse! Stored speed: {storedSpeed:F1} m/s, Dir: {storedDirection}", this);
        }
        else
        {
            rb.linearVelocity = cardinalDir * impulseSpeed;
            recentLinearVelocity = rb.linearVelocity;
            Debug.Log($"[KineticBox] Applied 6-Axis Impulse along {cardinalDir} with speed {impulseSpeed:F1} m/s", this);
        }

        return cardinalDir;
    }

    /// <summary>
    /// Used by scripted mechanisms (e.g., Giant Pendulum, Bouncy Wall): applies an arbitrary/diagonal 3D impulse without 6-axis snapping.
    /// </summary>
    public Vector3 ApplyFreeImpulse(Vector3 worldDirection, float impulseSpeed)
    {
        if (Time.unscaledTime - lastImpulseTime < impulseInterval)
            return lastImpulseDirection;

        lastImpulseTime = Time.unscaledTime;
        isFreeTrajectory = true;

        Vector3 dir = worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : transform.forward;
        lastImpulseDirection = dir;

        if (isMomentumLocked)
        {
            storedSpeed = Mathf.Min(storedSpeed + impulseSpeed, maxStoredSpeed);
            storedDirection = dir;
            UpdateVisuals();
            Debug.Log($"[KineticBox] Locked Box absorbed diagonal mechanism impulse! Stored speed: {storedSpeed:F1} m/s, Diagonal Dir: {storedDirection}", this);
        }
        else
        {
            highSpeedLaunchTimer = highSpeedLaunchDuration;
            SetDamping(launchedLinearDamping);
            rb.linearVelocity = dir * impulseSpeed;
            recentLinearVelocity = rb.linearVelocity;
            Debug.Log($"[KineticBox] Applied Diagonal Impulse along {dir} with speed {impulseSpeed:F1} m/s", this);
        }

        return dir;
    }

    /// <summary>
    /// Snaps an arbitrary world direction to the closest of the cube's 6 cardinal axes (±Right, ±Up, ±Forward).
    /// </summary>
    public Vector3 SnapToCardinalDirection(Vector3 worldDirection, bool allowVertical = true)
    {
        Vector3[] axes = allowVertical
            ? new Vector3[]
            {
                transform.right,
                -transform.right,
                transform.up,
                -transform.up,
                transform.forward,
                -transform.forward
            }
            : new Vector3[]
            {
                transform.right,
                -transform.right,
                transform.forward,
                -transform.forward
            };

        Vector3 bestAxis = transform.forward;
        float bestDot = -Mathf.Infinity;

        for (int i = 0; i < axes.Length; i++)
        {
            float dot = Vector3.Dot(worldDirection.normalized, axes[i]);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestAxis = axes[i];
            }
        }

        return bestAxis;
    }

    public void SetNormalColor(Color c)
    {
        normalColor = c;
        UpdateVisuals();
    }

    public void ResetBox()
    {
        isMomentumLocked = false;
        isFreeTrajectory = false;
        storedSpeed = 0f;
        storedDirection = Vector3.zero;
        highSpeedLaunchTimer = 0f;
        recentLinearVelocity = Vector3.zero;
        rb.isKinematic = false;
        SetDamping(groundLinearDamping);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        lastImpulseTime = -10f;
        lastBouncyWallTime = -10f;
        UpdateVisuals();
    }

    /// <summary>统一重置接口（由 PuzzleResetUtility 调用，等价于 ResetBox）。</summary>
    public void ResetPuzzle()
    {
        ResetBox();
    }

    private void UpdateVisuals()
    {
        if (boxRenderer == null)
            return;

        Color targetColor = normalColor;
        bool isSpecialState = isMomentumLocked || inBulletTime;
        if (isMomentumLocked)
        {
            float chargeT = Mathf.Clamp01(storedSpeed / 20f);
            targetColor = Color.Lerp(lockedBaseColor, lockedChargedColor, chargeT);
            if (isHoveredInBulletTime)
                targetColor = Color.Lerp(targetColor, Color.white, 0.25f);
        }
        else if (inBulletTime)
        {
            targetColor = isHoveredInBulletTime ? bulletTimeHoverColor : bulletTimeHighlightColor;
        }

        // 特殊状态优先用专用材质做“纯色发光”；没指定时退化为直接在静态材质上染色。
        Material targetMaterial = isSpecialState && stateMaterial != null ? stateMaterial : baseMaterial;
        if (targetMaterial != null && boxRenderer.sharedMaterial != targetMaterial)
            boxRenderer.sharedMaterial = targetMaterial;

        propBlock.Clear();
        if (isSpecialState || tintStaticAppearance)
        {
            propBlock.SetColor("_BaseColor", targetColor);
            propBlock.SetColor("_Color", targetColor);
        }
        if (!isSpecialState && appearanceTexture != null)
        {
            propBlock.SetTexture("_BaseMap", appearanceTexture);
            propBlock.SetTexture("_MainTex", appearanceTexture);
        }
        if (isSpecialState)
            propBlock.SetColor("_EmissionColor", targetColor * stateEmissionIntensity);
        boxRenderer.SetPropertyBlock(propBlock);

        // Update 3D Arrow Indicator (rotates to match exact 3D storedDirection, including diagonal upward vectors!)
        if (arrowRoot != null)
        {
            bool showArrow = isMomentumLocked && storedDirection.sqrMagnitude > 0.01f && storedSpeed > 0.1f;
            arrowRoot.gameObject.SetActive(showArrow);
            if (showArrow)
            {
                arrowRoot.rotation = Quaternion.LookRotation(storedDirection, Vector3.up);
                float lengthScale = Mathf.Clamp(0.6f + storedSpeed * 0.06f, 0.7f, 2.2f);
                arrowRoot.localScale = new Vector3(1f, 1f, lengthScale);

                foreach (Renderer r in arrowRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_BaseColor", targetColor);
                    propBlock.SetColor("_Color", targetColor);
                    propBlock.SetColor("_EmissionColor", targetColor * stateEmissionIntensity);
                    r.SetPropertyBlock(propBlock);
                }
            }
        }
    }
}
