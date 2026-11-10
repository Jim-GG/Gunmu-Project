using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the same Player object as CharacterController and PlayerLook.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("地面与空中移动")]
    [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
    [SerializeField, Min(1f)] private float groundAcceleration = 45f;
    [SerializeField, Min(1f)] private float groundDeceleration = 35f;
    [SerializeField, Min(0f)] private float airAcceleration = 6f;
    [SerializeField, Min(0f)] private float airDrag = 0.05f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.3f;
    [SerializeField, Min(0.1f)] private float gravity = 20f;

    [Header("蓄力拳冲撞与格挡碰撞物理")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField, Min(1f)] private float dashSpeed = 19f;
    [SerializeField, Range(0.2f, 1f)] private float dashEndMomentumRetention = 0.72f;
    [SerializeField, Min(0f)] private float hitRecoilBackSpeed = 4.5f;
    [SerializeField, Min(0f)] private float hitRecoilUpSpeed = 3.5f;
    [SerializeField, Range(0.5f, 2.5f), InspectorName("格挡继承动量倍率")] private float guardPlayerLaunchMultiplier = 1.15f;
    [SerializeField, Range(0f, 0.6f), InspectorName("未格挡受击动量保留比例")] private float unguardedIncomingMomentumRetention = 0.25f;
    [SerializeField, Min(0f), InspectorName("格挡平地撞击离地微跃速度")] private float guardHorizontalLaunchUpSpeed = 5.5f;
    [SerializeField, Range(0f, 1f), InspectorName("格挡时方块反弹速度比例")] private float guardBoxReboundRatio = 0.2f;
    [SerializeField, Min(0f), InspectorName("格挡时方块最大反弹速度")] private float guardBoxMaxReboundSpeed = 3.5f;
    [SerializeField, Min(1f), InspectorName("高速碰撞判定最低速度")] private float externalSpeedRechargeThreshold = 9f;
    [SerializeField, Min(0f), InspectorName("撞墙特效最低垂直速度")] private float wallImpactEffectMinSpeed = 9f;
    [SerializeField, Min(1f), InspectorName("物体碰撞判定最低相对速度")] private float minObjectCollisionSpeed = 3.5f;

    [Header("动量锁定与子弹时间")]
    [SerializeField, Range(0.05f, 0.5f)] private float bulletTimeScale = 0.12f;
    [SerializeField, Min(5f)] private float maxLockDistance = 60f;
    [SerializeField, InspectorName("开局即拥有动量遥控（关掉 = 要先捡到 MomentumRemotePickup）")] private bool startsWithMomentumRemote = true;

    private CharacterController controller;
    private PlayerChargedPunch chargedPunch;

    // Persistent world-space velocity components (preserves inertia in mid-air!)
    private Vector3 currentHorizontalVelocity;
    private float verticalSpeed;
    private Vector3 preMoveVelocity;

    // Punch lunge state
    private float dashTimer;
    private Vector3 dashDirection;
    private float activeLungeSpeed;
    private float activeLungeRetention;
    private bool punchLunging;
    public bool IsPunchLunging => punchLunging && dashTimer > 0f;

    // Bullet time & Momentum Lock state
    private bool isBulletTimeActive;
    private KineticBox currentLockedBox;
    private KineticBox hoveredBox;
    private KineticBox[] allBoxes;
    private int lastBoxScanFrame = -1000;
    private const int BoxScanIntervalFrames = 20;

    private Collider ignoredBoxCollider;
    private float ignoreBoxTimer;
    private float lastCollisionResolveTime = -10f;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private bool hasRecordedSpawn;

    public bool IsDashing => dashTimer > 0f;
    public bool IsBulletTimeActive => isBulletTimeActive;
    public bool HasMomentumRemote { get; private set; }
    /// <summary>当前是否有被锁定的动能方块（教学提示等只读查询用）。</summary>
    public bool HasLockedBox => currentLockedBox != null;
    /// <summary>是否曾经靠格挡被方块反冲/弹开过（教学提示等只读查询用）。</summary>
    public bool HasGuardedBoxBounce { get; private set; }

    public void EquipMomentumRemote() => HasMomentumRemote = true;
    public Vector3 CurrentVelocity => new Vector3(currentHorizontalVelocity.x, verticalSpeed, currentHorizontalVelocity.z);
    public bool IsIgnoringBox(Collider col) => col != null && ignoreBoxTimer > 0f && ignoredBoxCollider == col;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            Transform found = transform.Find("CameraPivot");
            if (found != null)
                cameraPivot = found;
        }

        chargedPunch = GetComponent<PlayerChargedPunch>();
        if (chargedPunch == null)
            chargedPunch = gameObject.AddComponent<PlayerChargedPunch>();

        // 默认开局就能用子弹时间/锁定（Soviet_1、TheCatwalk、Traffic 等关卡依赖它）；
        // 需要“先捡遥控器”的关卡在场景里把这个开关关掉。
        HasMomentumRemote = startsWithMomentumRemote;
    }

    private void Start()
    {
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;
        hasRecordedSpawn = true;
        allBoxes = FindObjectsByType<KineticBox>(FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        currentHorizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        dashTimer = 0f;
        punchLunging = false;
        SetBulletTime(false);
    }

    private void OnDisable()
    {
        SetBulletTime(false);
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked || PunchPresentation.IsStopped(gameObject)) return;
        if (!controller.enabled)
            return;

        Keyboard keyboard = Keyboard.current;

        // Quick puzzle reset with R key
        if (GetComponent<RoomRestartController>() == null && keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            ResetPuzzleState();
            return;
        }

        if (ignoreBoxTimer > 0f)
        {
            ignoreBoxTimer -= Time.deltaTime;
            if (ignoreBoxTimer <= 0f && ignoredBoxCollider != null)
            {
                Physics.IgnoreCollision(controller, ignoredBoxCollider, false);
                ignoredBoxCollider = null;
            }
        }

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

        // 1. Update crosshair target detection for KineticBox
        if (HasMomentumRemote) UpdateHoveredBox();

        // Right mouse is reserved for guarding; Q toggles bullet time.
        if (HasMomentumRemote && cursorLocked && keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            SetBulletTime(!isBulletTimeActive);
        }

        // 3. Handle Momentum Lock / Release inputs
        PlayerInteractor interactor = GetComponent<PlayerInteractor>();
        bool interactionOwnsE = interactor != null && interactor.isActiveAndEnabled && interactor.ConsumesInteractionKey;
        if (HasMomentumRemote && cursorLocked && !interactionOwnsE && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            HandleLockOrReleaseAction();
        }

        if (HasMomentumRemote) UpdateBoxesHighlightState();

        float dt = Time.deltaTime;
        bool grounded = controller.isGrounded;

        if (grounded && verticalSpeed < 0f && dashTimer <= 0f)
            verticalSpeed = -2f;

        Vector2 input = Vector2.zero;
        if (keyboard != null && cursorLocked)
        {
            input.x = (keyboard.dKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed ? 1f : 0f);

            input = Vector2.ClampMagnitude(input, 1f);

            if (grounded && keyboard.spaceKey.wasPressedThisFrame && dashTimer <= 0f)
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        // World-space wish direction from player input
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;

        Vector3 frameVelocity;

        if (dashTimer > 0f)
        {
            // Clamp the last movement step so distance is not rounded up by a full frame.
            float stepTime = Mathf.Min(dt, dashTimer);
            dashTimer -= stepTime;
            float lungeSpd = activeLungeSpeed > 0.1f ? activeLungeSpeed : dashSpeed;
            float lungeRet = activeLungeRetention > 0.01f ? activeLungeRetention : dashEndMomentumRetention;
            frameVelocity = dt > 0f ? dashDirection * (lungeSpd * stepTime / dt) : Vector3.zero;

            // Continuously sync persistent velocity so when dash ends naturally, momentum carries over seamlessly!
            Vector3 retainedVel = dashDirection * (lungeSpd * lungeRet);
            currentHorizontalVelocity = new Vector3(retainedVel.x, 0f, retainedVel.z);
            verticalSpeed = retainedVel.y;
        }
        else
        {
            // When launched upward (verticalSpeed > 0.5f), immediately use air momentum preservation so ground friction does not brake launch speed!
            if (grounded && verticalSpeed <= 0.5f)
            {
                Vector3 targetGroundVel = wishDir * moveSpeed;
                float rate = (wishDir.sqrMagnitude > 0.01f && currentHorizontalVelocity.magnitude <= moveSpeed + 0.5f)
                    ? groundAcceleration
                    : groundDeceleration;

                currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetGroundVel, rate * dt);
            }
            else
            {
                // In air: preserve horizontal momentum! Only apply very slight air drag and small micro-adjustment from WASD
                if (airDrag > 0f)
                {
                    currentHorizontalVelocity = Vector3.MoveTowards(
                        currentHorizontalVelocity,
                        Vector3.zero,
                        currentHorizontalVelocity.magnitude * airDrag * dt);
                }

                if (wishDir.sqrMagnitude > 0.001f)
                {
                    // Allow small air control without letting the player overwrite high launch/dash velocity or infinitely accelerate forward
                    float speedAlongWish = Vector3.Dot(currentHorizontalVelocity, wishDir.normalized);
                    if (speedAlongWish < moveSpeed)
                    {
                        currentHorizontalVelocity += wishDir * (airAcceleration * dt);
                    }
                    else
                    {
                        // Only allow slight lateral steering perpendicular to current velocity when already above moveSpeed
                        Vector3 lateralWish = Vector3.ProjectOnPlane(wishDir, currentHorizontalVelocity.normalized);
                        currentHorizontalVelocity += lateralWish * (airAcceleration * 0.75f * dt);
                    }
                }
            }

            verticalSpeed -= gravity * dt;
            frameVelocity = currentHorizontalVelocity + Vector3.up * verticalSpeed;
        }

        preMoveVelocity = new Vector3(currentHorizontalVelocity.x, verticalSpeed, currentHorizontalVelocity.z);
        CollisionFlags collisions = controller.Move(frameVelocity * dt);

        // Natural end of lunge: exit lunge state while preserving retained momentum!
        if (punchLunging && dashTimer <= 0f)
            punchLunging = false;

        bool bouncedThisFrame = (Time.unscaledTime - lastCollisionResolveTime) < 0.02f;
        if (!bouncedThisFrame)
        {
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
                verticalSpeed = -2f;
        }
    }

    private void UpdateHoveredBox()
    {
        hoveredBox = null;
        if (cameraPivot == null)
            return;

        if (Physics.Raycast(cameraPivot.position, cameraPivot.forward, out RaycastHit hit, maxLockDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.collider.TryGetComponent(out KineticBox rayBox))
        {
            hoveredBox = rayBox;
            return;
        }

        if (Physics.SphereCast(cameraPivot.position, 0.45f, cameraPivot.forward, out RaycastHit sphereHit,
            maxLockDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            sphereHit.collider.TryGetComponent(out KineticBox sphereBox))
        {
            hoveredBox = sphereBox;
        }
    }

    private void HandleLockOrReleaseAction()
    {
        if (hoveredBox != null)
        {
            if (hoveredBox.IsMomentumLocked)
            {
                hoveredBox.ReleaseMomentum();
                if (currentLockedBox == hoveredBox)
                    currentLockedBox = null;
            }
            else
            {
                if (currentLockedBox != null && currentLockedBox != hoveredBox)
                    currentLockedBox.ReleaseMomentum();

                hoveredBox.LockMomentum();
                currentLockedBox = hoveredBox;
            }

            SetBulletTime(false);
            return;
        }

        if (currentLockedBox != null && currentLockedBox.IsMomentumLocked)
        {
            currentLockedBox.ReleaseMomentum();
            currentLockedBox = null;
            SetBulletTime(false);
        }
    }

    private void SetBulletTime(bool active)
    {
        isBulletTimeActive = active;
        Time.timeScale = active ? bulletTimeScale : 1.0f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 进入子弹时间时立即重扫一次，保证刚生成 / 刚回收过方块也能正确高亮。
        if (active)
            RefreshBoxCacheIfNeeded(true);

        UpdateBoxesHighlightState();
    }

    private void UpdateBoxesHighlightState()
    {
        RefreshBoxCacheIfNeeded();

        for (int i = 0; i < allBoxes.Length; i++)
        {
            if (allBoxes[i] != null)
            {
                bool hovered = allBoxes[i] == hoveredBox;
                allBoxes[i].SetBulletTimeHighlight(isBulletTimeActive, hovered);
            }
        }
    }

    /// <summary>
    /// 重扫场景里的方块。不能用“只在 Start 扫一次”的缓存：
    /// 运输管道（KineticBoxDropper）会在运行时不断生成/销毁方块，
    /// 旧缓存里全是已销毁的 null，新方块永远进不来，子弹时间就再也不高亮了。
    /// 所以：有空槽、或距上次扫描超过 <see cref="BoxScanIntervalFrames"/> 帧、或被强制要求时重扫。
    /// </summary>
    private void RefreshBoxCacheIfNeeded(bool force = false)
    {
        bool needsScan = force || allBoxes == null || allBoxes.Length == 0 ||
                         Time.frameCount - lastBoxScanFrame >= BoxScanIntervalFrames;

        if (!needsScan)
        {
            for (int i = 0; i < allBoxes.Length; i++)
            {
                if (allBoxes[i] == null)
                {
                    needsScan = true;
                    break;
                }
            }
        }

        if (!needsScan)
            return;

        allBoxes = FindObjectsByType<KineticBox>(FindObjectsSortMode.None);
        lastBoxScanFrame = Time.frameCount;
    }

    public void BeginPunchLunge(float distance)
    {
        Vector3 lookDir = cameraPivot != null ? cameraPivot.forward : transform.forward;

        if (controller.isGrounded && lookDir.y < 0.25f && lookDir.y > -0.55f)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(lookDir, Vector3.up).normalized;
            dashDirection = flatForward.sqrMagnitude > 0.01f ? flatForward : transform.forward;
        }
        else
        {
            dashDirection = lookDir.normalized;
        }

        float preLungeSpeed = CurrentVelocity.magnitude;
        bool highSpeedAerialTurn = !controller.isGrounded && preLungeSpeed > dashSpeed;
        activeLungeSpeed = highSpeedAerialTurn ? preLungeSpeed : dashSpeed;
        activeLungeRetention = highSpeedAerialTurn ? Mathf.Max(dashEndMomentumRetention, 0.9f) : dashEndMomentumRetention;

        dashTimer = Mathf.Max(0f, distance) / Mathf.Max(0.01f, activeLungeSpeed);
        punchLunging = dashTimer > 0f;

        Vector3 retainedVel = dashDirection * (activeLungeSpeed * activeLungeRetention);
        currentHorizontalVelocity = new Vector3(retainedVel.x, 0f, retainedVel.z);
        verticalSpeed = retainedVel.y;
    }

    public void StopPunchLunge()
    {
        if (!punchLunging && dashTimer <= 0f) return;
        punchLunging = false;
        dashTimer = 0f;
        currentHorizontalVelocity = Vector3.zero;
        verticalSpeed = Mathf.Min(0f, verticalSpeed);
    }

    public void ApplyBoxHitRecoil(Vector3 appliedImpulseDirection)
    {
        punchLunging = false;
        dashTimer = 0f;
        Vector3 recoilHoriz = -new Vector3(appliedImpulseDirection.x, 0f, appliedImpulseDirection.z).normalized * hitRecoilBackSpeed;
        currentHorizontalVelocity = recoilHoriz;
        verticalSpeed = hitRecoilUpSpeed;
    }

    public void LiftAfterCombatPunch(float upwardSpeed)
    {
        // Called only for a confirmed enemy impact, so puzzle launches retain their trajectory.
        verticalSpeed = Mathf.Max(verticalSpeed, Mathf.Max(0f, upwardSpeed));
    }

    /// <summary>
    /// 外部装置（大炮 / 弹射器等）直接把玩家当弹体发射：接管当前速度，保留原有惯性规则。
    /// </summary>
    public void LaunchAsProjectile(Vector3 velocity)
    {
        punchLunging = false;
        dashTimer = 0f;
        activeLungeSpeed = 0f;
        currentHorizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        verticalSpeed = velocity.y;
    }

    /// <summary>
    /// Unified handler for any incoming high-speed impact (e.g., Giant Pendulum hammer or incoming box):
    /// - Guarding toward the impact: Player fully inherits the incoming momentum + refills gauntlet energy to 3/3.
    /// - Not guarding: Plastic collision (player only retains a small damped fraction of the incoming velocity and does not refill energy).
    /// </summary>
    public bool ResolveIncomingImpact(Vector3 sourceCenter, Vector3 contactPoint, Vector3 surfaceNormalOnPlayer, Vector3 incomingVelocity)
    {
        if (Time.unscaledTime - lastCollisionResolveTime < 0.12f)
            return false;

        lastCollisionResolveTime = Time.unscaledTime;
        punchLunging = false;
        dashTimer = 0f;

        bool isGuardingSource = chargedPunch != null &&
            (chargedPunch.IsGuardingTowardsPoint(sourceCenter) || chargedPunch.IsGuardingSurface(surfaceNormalOnPlayer));

        if (isGuardingSource)
        {
            chargedPunch.RefillEnergyByGuard(contactPoint, surfaceNormalOnPlayer);
            Vector3 launchVel = incomingVelocity * guardPlayerLaunchMultiplier;
            currentHorizontalVelocity = new Vector3(launchVel.x, 0f, launchVel.z);
            verticalSpeed = Mathf.Abs(launchVel.y) > 0.5f ? launchVel.y : Mathf.Max(verticalSpeed, guardHorizontalLaunchUpSpeed);
            return true;
        }
        else
        {
            Vector3 plasticVel = incomingVelocity * unguardedIncomingMomentumRetention;
            currentHorizontalVelocity = new Vector3(plasticVel.x, 0f, plasticVel.z);
            verticalSpeed = Mathf.Max(0f, plasticVel.y);
            if (chargedPunch != null)
                chargedPunch.NotifyPlasticCollision();
            return false;
        }
    }

    private void IgnoreBoxBriefly(Collider boxCol, float duration = 0.22f)
    {
        if (boxCol == null) return;
        if (ignoredBoxCollider != null && ignoredBoxCollider != boxCol)
            Physics.IgnoreCollision(controller, ignoredBoxCollider, false);

        ignoredBoxCollider = boxCol;
        ignoreBoxTimer = duration;
        Physics.IgnoreCollision(controller, boxCol, true);
    }

    /// <summary>
    /// Handles collision between the Player and an UNLOCKED moving KineticBox:
    /// (Momentum-locked boxes are treated as static walls/floors, never entering this method!)
    /// - Guarding (Right-Click + facing object): Elastic collision (player inherits momentum, box only rebounds gently) + refill energy to full (3/3).
    /// - Not guarding: Plastic collision (both lose speed) + no energy refill.
    /// </summary>
    public void ResolveBoxCollision(KineticBox box, Vector3 contactPoint, Vector3 surfaceNormalOnPlayer, Vector3 boxVelocity)
    {
        // Locked boxes are treated strictly as static geometry (walls/platforms), never as dynamic colliding boxes.
        if (box == null || box.IsMomentumLocked || IsPunchLunging || IsIgnoringBox(box.Collider))
            return;

        if (Time.unscaledTime - lastCollisionResolveTime < 0.15f)
            return;

        float boxSpeed = boxVelocity.magnitude;
        Vector3 playerVel = preMoveVelocity.sqrMagnitude > CurrentVelocity.sqrMagnitude ? preMoveVelocity : CurrentVelocity;
        float playerSpeed = playerVel.magnitude;

        // If the box itself is stationary/slow on the ground:
        // - Standing/walking on top of it (surfaceNormalOnPlayer.y > 0.5f) is just standing on a platform.
        // - Walking into its side at normal walk speed (< externalSpeedRechargeThreshold) is just touching a stationary box.
        if (boxSpeed < minObjectCollisionSpeed)
        {
            if (surfaceNormalOnPlayer.y > 0.5f)
                return;
            if (playerSpeed < externalSpeedRechargeThreshold)
                return;
        }

        if (boxSpeed >= playerSpeed)
        {
            // Incoming moving box hits player: use the unified incoming impact handler!
            bool guarded = ResolveIncomingImpact(box.transform.position, contactPoint, surfaceNormalOnPlayer, boxVelocity);
            if (guarded)
            {
                HasGuardedBoxBounce = true;
                IgnoreBoxBriefly(box.Collider, 0.25f);
                float gentleRebound = Mathf.Min(boxSpeed * guardBoxReboundRatio, guardBoxMaxReboundSpeed);
                box.ApplyGentleGuardRebound(-boxVelocity, gentleRebound);
            }
            else
            {
                box.StopPlasticMotion();
            }
        }
        else
        {
            // Fast-moving player slams into a slower unlocked box
            lastCollisionResolveTime = Time.unscaledTime;
            bool isGuardingBox = chargedPunch != null &&
                (chargedPunch.IsGuardingTowardsPoint(box.transform.position) || chargedPunch.IsGuardingSurface(surfaceNormalOnPlayer));

            if (isGuardingBox)
            {
                HasGuardedBoxBounce = true;
                chargedPunch.RefillEnergyByGuard(contactPoint, surfaceNormalOnPlayer);
                IgnoreBoxBriefly(box.Collider, 0.25f);

                Vector3 reflected = Vector3.Reflect(playerVel, surfaceNormalOnPlayer) * guardPlayerLaunchMultiplier;
                currentHorizontalVelocity = new Vector3(reflected.x, 0f, reflected.z);
                verticalSpeed = Mathf.Abs(reflected.y) > 0.5f ? reflected.y : Mathf.Max(verticalSpeed, guardHorizontalLaunchUpSpeed);

                float gentlePush = Mathf.Min(playerSpeed * guardBoxReboundRatio, guardBoxMaxReboundSpeed);
                box.ApplyGentleGuardRebound(-surfaceNormalOnPlayer, gentlePush);
            }
            else
            {
                box.StopPlasticMotion();
                currentHorizontalVelocity = Vector3.zero;
                if (verticalSpeed > 0f)
                    verticalSpeed = 0f;
                if (chargedPunch != null)
                    chargedPunch.NotifyPlasticCollision();
            }
        }
    }

    private void ResolveOtherRigidbodyCollision(Rigidbody body, Collider col, Vector3 contactPoint, Vector3 surfaceNormalOnPlayer)
    {
        if (body == null || body.isKinematic || IsPunchLunging)
            return;

        if (Time.unscaledTime - lastCollisionResolveTime < 0.15f)
            return;

        Vector3 bodyVel = body.linearVelocity;
        float bodySpeed = bodyVel.magnitude;
        Vector3 playerVel = preMoveVelocity.sqrMagnitude > CurrentVelocity.sqrMagnitude ? preMoveVelocity : CurrentVelocity;
        float playerSpeed = playerVel.magnitude;

        if (bodySpeed < minObjectCollisionSpeed)
        {
            if (surfaceNormalOnPlayer.y > 0.5f || playerSpeed < externalSpeedRechargeThreshold)
                return;
        }

        Vector3 effectiveIncoming = bodySpeed >= playerSpeed ? bodyVel : Vector3.Reflect(playerVel, surfaceNormalOnPlayer);
        bool guarded = ResolveIncomingImpact(body.worldCenterOfMass, contactPoint, surfaceNormalOnPlayer, effectiveIncoming);
        if (guarded)
        {
            float refSpeed = Mathf.Max(bodySpeed, playerSpeed);
            float gentleRebound = Mathf.Min(refSpeed * guardBoxReboundRatio, guardBoxMaxReboundSpeed);
            body.linearVelocity = -effectiveIncoming.normalized * gentleRebound;
        }
        else
        {
            body.linearVelocity = new Vector3(0f, Mathf.Min(0f, body.linearVelocity.y), 0f);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsPunchLunging && chargedPunch != null)
        {
            chargedPunch.ResolveLungeContact(hit.collider, hit.point, hit.normal);
            return;
        }

        if (hit.collider.TryGetComponent(out PuzzleSwitch sw) && sw.AllowPlayerActivation)
        {
            sw.Activate();
        }

        // 0. Check collision with BouncyWall (弹性墙)
        BouncyWall bouncyWall = hit.collider.GetComponentInParent<BouncyWall>();
        if (bouncyWall != null)
        {
            Vector3 incomingVel = preMoveVelocity.sqrMagnitude > CurrentVelocity.sqrMagnitude ? preMoveVelocity : CurrentVelocity;
            // 阈值与倍率统一由弹性墙决定：方块和玩家的反弹结果因此完全一致。
            if (bouncyWall.TryReflect(incomingVel, hit.normal, out Vector3 reflectedVel) &&
                Time.unscaledTime - lastCollisionResolveTime >= 0.12f)
            {
                lastCollisionResolveTime = Time.unscaledTime;
                bool isGuardingBouncy = chargedPunch != null &&
                    (chargedPunch.IsGuardingSurface(hit.normal) || chargedPunch.IsGuardingTowardsPoint(bouncyWall.transform.position));

                if (isGuardingBouncy)
                {
                    chargedPunch.RefillEnergyByGuard(hit.point, hit.normal);
                    currentHorizontalVelocity = new Vector3(reflectedVel.x, 0f, reflectedVel.z);
                    verticalSpeed = reflectedVel.y;
                    bouncyWall.NotifyReflectedPlayer(hit.point, hit.normal, reflectedVel);
                }
                else
                {
                    currentHorizontalVelocity = Vector3.zero;
                    if (verticalSpeed > 0f)
                        verticalSpeed = 0f;
                    if (chargedPunch != null)
                        chargedPunch.NotifyPlasticCollision();
                }
            }
            return;
        }

        // 1. Check collision with UNLOCKED KineticBox (Momentum-Locked boxes fall through and are treated as static walls/platforms!)
        KineticBox box = hit.collider.GetComponentInParent<KineticBox>();
        if (box != null && !box.IsMomentumLocked)
        {
            ResolveBoxCollision(box, hit.point, hit.normal, box.PreImpactVelocity);
            return;
        }

        // 2. Check collision with other movable (non-kinematic) Rigidbodies
        if (box == null && hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
        {
            ResolveOtherRigidbodyCollision(hit.collider.attachedRigidbody, hit.collider, hit.point, hit.normal);
            return;
        }

        // 3. Static geometry (including Walls and Momentum-Locked boxes):
        // Standing on top (Mathf.Abs(hit.normal.y) >= 0.5f) is normal ground support.
        // Hitting a vertical side (Mathf.Abs(hit.normal.y) < 0.5f) is a Wall collision:
        if (Mathf.Abs(hit.normal.y) < 0.5f)
        {
            Vector3 horizNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            float intoWall = Vector3.Dot(currentHorizontalVelocity, horizNormal);
            float intoWallSpeed = -intoWall;

            // 只清除朝墙的法向分量，保留沿墙的切向速度：不做强行停住（垂直速度也不动，竖直墙不应阻断上升）。
            if (intoWall < 0f)
                currentHorizontalVelocity -= horizNormal * intoWall;

            // 撞击反馈（音效 + 震屏）：不影响移动，只按撞墙前的法向速度判定。
            if (intoWallSpeed >= wallImpactEffectMinSpeed &&
                Time.unscaledTime - lastCollisionResolveTime >= 0.15f)
            {
                lastCollisionResolveTime = Time.unscaledTime;
                if (chargedPunch != null)
                    chargedPunch.NotifyPlasticCollision();
            }
        }
    }

    public bool TryRechargeDash(string sourceReason)
    {
        // Compatibility with existing scene crystals.
        return sourceReason == "Crystal" && chargedPunch != null && chargedPunch.AddEnergy(3);
    }

    public void TeleportTo(Vector3 targetPosition)
    {
        SetBulletTime(false);
        initialSpawnPosition = targetPosition;
        hasRecordedSpawn = true;
        controller.enabled = false;
        transform.position = targetPosition;
        controller.enabled = true;
        ResetPuzzleState();
    }

    private void ResetPuzzleState()
    {
        StopPunchLunge();
        if (chargedPunch != null)
            chargedPunch.ResetPunchState();
        SetBulletTime(false);
        currentLockedBox = null;
        dashTimer = 0f;
        verticalSpeed = 0f;
        currentHorizontalVelocity = Vector3.zero;

        if (hasRecordedSpawn)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(initialSpawnPosition, initialSpawnRotation);
            controller.enabled = true;
        }

        // 统一重置：所有可复位机关都实现了 IPuzzleResettable，新增道具不需要再改这里。
        PuzzleResetUtility.ResetAll();
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        ChineseGameUI.EnsureFont();
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        bool hasEnergy = chargedPunch != null && chargedPunch.StoredEnergy > 0;
        GUI.color = hoveredBox != null ? Color.green : (hasEnergy ? Color.cyan : Color.gray);
        float size = hoveredBox != null ? 12f : 8f;
        GUI.Box(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), GUIContent.none);
        GUI.color = Color.white;
    }
}
