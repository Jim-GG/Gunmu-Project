using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Attach to the same Player object as CharacterController and PlayerLook.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("地面与空中移动")]
    [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
    [SerializeField, Min(1f)] private float groundAcceleration = 45f;
    [SerializeField, Min(1f)] private float groundDeceleration = 35f;
    [SerializeField, Min(0f)] private float airAcceleration = 6f;
    [SerializeField, Min(0f)] private float airDrag = 0.25f;
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
    [SerializeField, Min(1f), InspectorName("撞墙格挡充能最低速度")] private float externalSpeedRechargeThreshold = 9f;
    [SerializeField, Min(1f), InspectorName("物体碰撞判定最低相对速度")] private float minObjectCollisionSpeed = 3.5f;

    [Header("动量锁定与子弹时间")]
    [SerializeField, Range(0.05f, 0.5f)] private float bulletTimeScale = 0.12f;
    [SerializeField, Min(5f)] private float maxLockDistance = 60f;

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

    // Cached puzzle references for HUD & quick reset (R key)
    private PuzzleSwitch cachedSwitch;
    private PendulumMechanism cachedPendulum;
    private SeesawMechanism cachedSeesaw;
    private Collider ignoredBoxCollider;
    private float ignoreBoxTimer;
    private float lastCollisionResolveTime = -10f;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private bool hasRecordedSpawn;

    public bool IsDashing => dashTimer > 0f;
    public bool IsBulletTimeActive => isBulletTimeActive;
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
    }

    private void Start()
    {
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;
        hasRecordedSpawn = true;
        cachedSwitch = FindFirstObjectByType<PuzzleSwitch>();
        cachedPendulum = FindFirstObjectByType<PendulumMechanism>();
        cachedSeesaw = FindFirstObjectByType<SeesawMechanism>();
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
        if (DemoSession.IsGameplayBlocked || PunchPresentation.IsStopped(gameObject)) return;
        if (!controller.enabled)
            return;

        Keyboard keyboard = Keyboard.current;

        // Quick puzzle reset with R key
        if (GetComponent<RoomRestartController>() == null && keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            ResetPuzzleState();
            return;
        }

        // Scene number shortcuts are development-only and cannot skip the packaged demo.
        if (Application.isEditor)
        {
            // Quick scene switch between Level 1..6 (Keys 1..6)
            if (keyboard != null && keyboard.digit1Key.wasPressedThisFrame)
            {
                SetBulletTime(false);
                SceneManager.LoadScene("Level1_Billiards");
                return;
            }
            if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame)
            {
                SetBulletTime(false);
                SceneManager.LoadScene("Level2_Pendulum");
                return;
            }
            if (keyboard != null && keyboard.digit3Key.wasPressedThisFrame)
            {
                SetBulletTime(false);
                SceneManager.LoadScene("Level3_Seesaw");
                return;
            }
            if (keyboard != null && keyboard.digit4Key.wasPressedThisFrame)
            {
                SetBulletTime(false);
                SceneManager.LoadScene("Level4_MomentumRedirect");
                return;
            }
            if (keyboard != null && keyboard.digit5Key.wasPressedThisFrame)
            {
                SetBulletTime(false);
                SceneManager.LoadScene("Level5_ChainReaction");
                return;
            }
            if (keyboard != null && keyboard.digit6Key.wasPressedThisFrame)
            {
                SetBulletTime(false);
                SceneManager.LoadScene("Level6_BouncyWall");
                return;
            }
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
        UpdateHoveredBox();

        // Right mouse is reserved for guarding; Q toggles bullet time.
        if (cursorLocked && keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            SetBulletTime(!isBulletTimeActive);
        }

        // 3. Handle Momentum Lock / Release inputs
        PlayerInteractor interactor = GetComponent<PlayerInteractor>();
        bool interactionOwnsE = interactor != null && interactor.isActiveAndEnabled && interactor.ConsumesInteractionKey;
        if (cursorLocked && !interactionOwnsE && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            HandleLockOrReleaseAction();
        }

        UpdateBoxesHighlightState();

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

        if (Physics.Raycast(cameraPivot.position, cameraPivot.forward, out RaycastHit hit, maxLockDistance) &&
            hit.collider.TryGetComponent(out KineticBox rayBox))
        {
            hoveredBox = rayBox;
            return;
        }

        if (Physics.SphereCast(cameraPivot.position, 0.45f, cameraPivot.forward, out RaycastHit sphereHit, maxLockDistance) &&
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
        UpdateBoxesHighlightState();
    }

    private void UpdateBoxesHighlightState()
    {
        if (allBoxes == null || allBoxes.Length == 0)
            allBoxes = FindObjectsByType<KineticBox>(FindObjectsSortMode.None);

        for (int i = 0; i < allBoxes.Length; i++)
        {
            if (allBoxes[i] != null)
            {
                bool hovered = allBoxes[i] == hoveredBox;
                allBoxes[i].SetBulletTimeHighlight(isBulletTimeActive, hovered);
            }
        }
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
            float speedIntoWall = -Vector3.Dot(incomingVel, hit.normal);
            if (incomingVel.magnitude >= 3.5f && speedIntoWall > 1.0f && Time.unscaledTime - lastCollisionResolveTime >= 0.12f)
            {
                lastCollisionResolveTime = Time.unscaledTime;
                bool isGuardingBouncy = chargedPunch != null &&
                    (chargedPunch.IsGuardingSurface(hit.normal) || chargedPunch.IsGuardingTowardsPoint(bouncyWall.transform.position));

                if (isGuardingBouncy)
                {
                    chargedPunch.RefillEnergyByGuard(hit.point, hit.normal);
                    Vector3 reflectedVel = bouncyWall.ComputeReflectedVelocity(incomingVel, hit.normal);
                    currentHorizontalVelocity = new Vector3(reflectedVel.x, 0f, reflectedVel.z);
                    verticalSpeed = reflectedVel.y;
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
            float intoWallSpeed = -Vector3.Dot(currentHorizontalVelocity, horizNormal);
            float totalSpeed = CurrentVelocity.magnitude;

            // High-speed wall impact: both guarding and unguarded stop the player, but guarding refills energy to full!
            if (intoWallSpeed >= 4.5f || (totalSpeed >= externalSpeedRechargeThreshold && intoWallSpeed > 2.0f))
            {
                currentHorizontalVelocity = Vector3.zero;
                if (verticalSpeed > 0f)
                    verticalSpeed = 0f;

                if (Time.unscaledTime - lastCollisionResolveTime >= 0.15f)
                {
                    lastCollisionResolveTime = Time.unscaledTime;
                    if (chargedPunch != null && chargedPunch.IsGuardingSurface(hit.normal))
                    {
                        chargedPunch.RefillEnergyByGuard(hit.point, hit.normal);
                    }
                    else if (chargedPunch != null)
                    {
                        chargedPunch.NotifyPlasticCollision();
                    }
                }
                return;
            }

            // Normal low-speed walking against a wall: slide along wall by removing into-wall component
            float intoWall = Vector3.Dot(currentHorizontalVelocity, horizNormal);
            if (intoWall < 0f)
            {
                currentHorizontalVelocity -= horizNormal * intoWall;
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

        foreach (KineticBox box in FindObjectsByType<KineticBox>(FindObjectsSortMode.None))
            box.ResetBox();

        foreach (DashCrystal crystal in FindObjectsByType<DashCrystal>(FindObjectsSortMode.None))
            crystal.ResetCrystal();

        foreach (PuzzleSwitch sw in FindObjectsByType<PuzzleSwitch>(FindObjectsSortMode.None))
            sw.ResetSwitch();

        foreach (PendulumMechanism p in FindObjectsByType<PendulumMechanism>(FindObjectsSortMode.None))
            p.ResetPendulum();

        foreach (SeesawMechanism s in FindObjectsByType<SeesawMechanism>(FindObjectsSortMode.None))
            s.ResetMechanism();
    }

    private void OnGUI()
    {
        if (DemoSession.IsGameplayBlocked) return;
        ChineseGameUI.EnsureFont();
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        if (isBulletTimeActive)
        {
            GUI.color = new Color(0f, 1f, 0.95f, 0.9f);
            GUIStyle btStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btStyle.normal.textColor = new Color(0.1f, 1f, 0.95f);
            string btHint = hoveredBox != null
                ? (hoveredBox.IsMomentumLocked ? "⏱ 子弹时间 | [E] 释放目标方块" : "⏱ 子弹时间 | [E] 锁定高亮方块！")
                : "⏱ 子弹时间已开启 | 瞄准高亮方块按 [E] 锁定或释放";
            GUI.Box(new Rect(cx - 260f, 20f, 520f, 40f), btHint, btStyle);
        }

        bool hasEnergy = chargedPunch != null && chargedPunch.StoredEnergy > 0;
        GUI.color = hoveredBox != null ? Color.green : (hasEnergy ? Color.cyan : Color.gray);
        float size = hoveredBox != null ? 12f : 8f;
        GUI.Box(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), GUIContent.none);

        // The combat tutorial has its own objective; do not display Level 1's puzzle solution here.
        GUI.color = Color.white;
        if (SceneManager.GetActiveScene().name == "Demo_IntroCombat") return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(12, 12, 8, 8)
        };
        style.normal.textColor = Color.white;

        string chargeText = "左键按住蓄力、松开冲撞出拳 | 蓝色晶体+3格，右键格挡碰撞回满";

        string lockText = currentLockedBox != null && currentLockedBox.IsMomentumLocked
            ? $"🔒 已锁定方块 (蓄力速率: {currentLockedBox.StoredSpeed:F1} m/s) | 按 [E] 远程释放"
            : "🔓 动量锁定: 未锁定 | 按 [Q] 进入子弹时间高亮并锁定方块";

        bool solved = cachedSwitch != null && cachedSwitch.IsActivated;
        string sceneName = SceneManager.GetActiveScene().name;
        string puzzleText;
        if (solved)
        {
            puzzleText = sceneName == "Level1_Billiards" ? "解谜成功！从右侧出口进入第二关。"
                : sceneName == "Level2_Pendulum" ? "解谜成功！等待沟壑填平，穿过接收屏幕后方的门。"
                : "★ 机关已触发！解谜成功！（按 1~6 切换关卡，按 R 重置）";
        }
        else if (sceneName == "Level6_BouncyWall")
        {
            puzzleText = "第6关流程：①站上橙色击球区，面向往复摆动的大摆锤按住右键格挡，直接继承摆锤动量飞越深渊 → ②空中转向青色【弹性墙】按住右键格挡反射向右斜上方 → ③空中左键蓄力冲撞拐弯落上目标平台！（按1~6切关，R重置）";
        }
        else if (sceneName == "Level5_ChainReaction")
        {
            puzzleText = "第5关流程：①用1格蓄力拳击打白色【母球】穿过窄缝至大摆锤前 → ②右侧按钮[F]释放起重机弹起黄方块，Q减速后按E锁在轨道高度 → ③左侧按钮[F]释放摆锤击飞母球传力给黄方块 → ④按[E]释放黄方块！（按1~6切关，R重置）";
        }
        else if (sceneName == "Level4_MomentumRedirect")
        {
            puzzleText = "第4关流程：①按[F]释放起重机弹起方块 → ②方块升至平台高度时Q减速、E锁定 → ③朝对面平台【1格蓄力拳】击打方块将向上动量偏转为向前 → ④按[E]释放飞跃峡谷！（按1~6切关，R重置）";
        }
        else if (cachedSeesaw != null)
        {
            puzzleText = "第3关流程：①按[F]释放起重机重物砸向跷跷板 → ②Q减速、E锁定飞上来的方块 → ③迎着方块按住右键格挡并按[E]释放弹射升空 → ④高空松开左键冲撞到头顶平台（按1~6切关，R重置）";
        }
        else if (cachedPendulum != null)
        {
            puzzleText = "第2关：1格蓄力拳将载荷打入沟中 → Q减速、E锁定 → F释放摆锤 → E释放载荷命中东侧接收器。成功后填沟开门。";
        }
        else
        {
            puzzleText = "第1关：用【1格蓄力拳】将载荷打入低矮缝隙，触发橙色接收器；成功后从右侧门进入第二关。";
        }

        GUI.color = Color.white;
        GUI.Box(new Rect(16f, 16f, 820f, 82f), $"{chargeText}   |   {lockText}\n{puzzleText}", style);
    }
}
