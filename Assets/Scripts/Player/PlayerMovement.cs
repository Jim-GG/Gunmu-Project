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

    [Header("蓄力拳冲撞位移")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField, Min(1f)] private float dashSpeed = 19f;
    [SerializeField, Range(0.2f, 1f)] private float dashEndMomentumRetention = 0.72f;
    [SerializeField, Min(0f)] private float hitRecoilBackSpeed = 4.5f;
    [SerializeField, Min(0f)] private float hitRecoilUpSpeed = 3.5f;

    [Header("动量锁定与子弹时间")]
    [SerializeField, Range(0.05f, 0.5f)] private float bulletTimeScale = 0.12f;
    [SerializeField, Min(5f)] private float maxLockDistance = 60f;

    private CharacterController controller;
    private PlayerChargedPunch chargedPunch;

    // Persistent world-space velocity components (preserves inertia in mid-air!)
    private Vector3 currentHorizontalVelocity;
    private float verticalSpeed;

    // Punch lunge state
    private float dashTimer;
    private Vector3 dashDirection;
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

    public bool IsDashing => dashTimer > 0f;
    public bool IsBulletTimeActive => isBulletTimeActive;

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
            // Quick scene switch between Level 1..5 (Keys 1, 2, 3, 4, 5)
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
            frameVelocity = dt > 0f ? dashDirection * (dashSpeed * stepTime / dt) : Vector3.zero;

            // Continuously sync persistent velocity so when dash ends naturally, momentum carries over seamlessly!
            Vector3 retainedVel = dashDirection * (dashSpeed * dashEndMomentumRetention);
            currentHorizontalVelocity = new Vector3(retainedVel.x, 0f, retainedVel.z);
            verticalSpeed = retainedVel.y;
        }
        else
        {
            if (grounded)
            {
                // On ground: responsive acceleration toward target walk velocity, and crisp deceleration when stopping or sliding from a dash
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

        CollisionFlags collisions = controller.Move(frameVelocity * dt);

        // Natural end of lunge: exit lunge state while preserving retained momentum!
        if (punchLunging && dashTimer <= 0f)
            punchLunging = false;

        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            verticalSpeed = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            verticalSpeed = -2f;
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

        dashTimer = Mathf.Max(0f, distance) / Mathf.Max(0.01f, dashSpeed);
        punchLunging = dashTimer > 0f;

        Vector3 retainedVel = dashDirection * (dashSpeed * dashEndMomentumRetention);
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

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsPunchLunging && chargedPunch != null)
            chargedPunch.ResolveLungeContact(hit.collider, hit.point, hit.normal);

        if (hit.collider.TryGetComponent(out PuzzleSwitch sw) && sw.AllowPlayerActivation)
        {
            sw.Activate();
        }

        // If hitting a vertical wall in mid-air or on ground, remove the velocity component pushing into the wall
        if (Mathf.Abs(hit.normal.y) < 0.5f)
        {
            Vector3 horizNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            float intoWall = Vector3.Dot(currentHorizontalVelocity, horizNormal);
            if (intoWall < 0f)
            {
                currentHorizontalVelocity -= horizNormal * intoWall;
            }
        }
    }

    public void LaunchWithReleasedBox(Vector3 boxLaunchVelocity, Collider boxCollider)
    {
        if (boxCollider != null)
        {
            if (ignoredBoxCollider != null)
                Physics.IgnoreCollision(controller, ignoredBoxCollider, false);

            ignoredBoxCollider = boxCollider;
            ignoreBoxTimer = 0.45f;
            Physics.IgnoreCollision(controller, boxCollider, true);
        }

        if (boxLaunchVelocity.y > 1f)
        {
            verticalSpeed = Mathf.Max(boxLaunchVelocity.y * 1.35f, 23.5f);
        }
        else
        {
            verticalSpeed = boxLaunchVelocity.y;
        }

        currentHorizontalVelocity = new Vector3(boxLaunchVelocity.x, 0f, boxLaunchVelocity.z);
        Debug.Log($"[PlayerMovement] Player catapulted by released KineticBox! Velocity: ({currentHorizontalVelocity.x:F1}, {verticalSpeed:F1}, {currentHorizontalVelocity.z:F1}) m/s", this);
    }

    public bool TryRechargeDash(string sourceReason)
    {
        // Compatibility with existing scene crystals. No other recharge sources allowed.
        return sourceReason == "Crystal" && chargedPunch != null && chargedPunch.AddEnergy(3);
    }

    public void TeleportTo(Vector3 targetPosition)
    {
        SetBulletTime(false);
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

        string chargeText = "左键按住蓄力、松开冲撞出拳 | 蓝色晶体+3格，成功格挡+1格 | Shift无位移";

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
                : "机关已触发！解谜成功！（按 R 重置）";
        }
        else if (sceneName == "Level5_ChainReaction")
        {
            puzzleText = "第5关流程：①用1格蓄力拳击打白色【母球】穿过窄缝至大摆锤前 → ②右侧按钮[F]释放起重机弹起黄方块，Q减速后按E锁在轨道高度 → ③左侧按钮[F]释放摆锤击飞母球传力给黄方块 → ④按[E]释放黄方块！（按1~5切关，R重置）";
        }
        else if (sceneName == "Level4_MomentumRedirect")
        {
            puzzleText = "第4关流程：①按[F]释放起重机弹起方块 → ②方块升至平台高度时Q减速、E锁定 → ③朝对面平台【1格蓄力拳】击打方块将向上动量偏转为向前 → ④按[E]释放飞跃峡谷！（按1~5切关，R重置）";
        }
        else if (cachedSeesaw != null)
        {
            puzzleText = "第3关流程：①按[F]释放起重机重物砸向跷跷板 → ②Q减速、E锁定飞上来的方块 → ③跳上方块按[E]释放弹射升空 → ④高空松开左键冲撞到头顶平台（按1~4切关，R重置）";
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
