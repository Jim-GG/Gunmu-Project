using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Each press starts a fresh charge. Release uses the last fully completed tier.
public class PlayerChargedPunch : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float minimumWindup = 0.15f;
    [SerializeField, Min(0.02f)] private float tierOneTime = 0.5f;
    [SerializeField, Min(0.03f)] private float tierTwoTime = 1.25f;
    [SerializeField, Min(0.04f)] private float tierThreeTime = 2.5f;
    [SerializeField, Min(0.1f), InspectorName("击打距离（距身体，米）")] private float reach = 1f;
    [SerializeField, Min(0f)] private float recovery = 0.3f;
    [Header("各档冲撞距离（米）")]
    [SerializeField, Min(0f)] private float tierOneLungeDistance = 2.5f;
    [SerializeField, Min(0f)] private float tierTwoLungeDistance = 4.5f;
    [SerializeField, Min(0f)] private float tierThreeLungeDistance = 6.5f;
    [SerializeField, Min(1)] private int basicDamage = 1;
    [SerializeField, Min(1)] private int tierOneDamage = 2;
    [SerializeField, Min(1)] private int tierTwoDamage = 4;
    [SerializeField, Min(1)] private int tierThreeDamage = 6;
    [SerializeField, Min(0f)] private float tierOneImpulse = 11f;
    [SerializeField, Min(0f)] private float tierTwoImpulse = 16f;
    [SerializeField, Min(0f)] private float tierThreeImpulse = 22f;
    [SerializeField, InspectorName("命中敌人时轻微腾空")] private bool liftOnEnemyHit = true;
    [SerializeField, InspectorName("一至三级自身腾空速度")] private Vector3 combatLiftSpeeds = new Vector3(2.2f, 3f, 3.8f);

    [Header("右键格挡与充能")]
    [SerializeField, Range(30f, 180f), InspectorName("格挡判定角度")] private float guardAngle = 120f;
    [SerializeField, Range(0, 3)] private int initialEnergy = 0;
    [SerializeField] private int storedEnergy;

    private bool attacking;
    private int attackTier;
    public int StoredEnergy => storedEnergy;
    public float VisualCharge => charging ? .3f + .7f * Mathf.Clamp01(elapsed / tierThreeTime) : 0f;

    // ---- 表现层订阅点（只读状态，不改玩法规则）----
    public event Action ChargeStarted;
    public event Action ChargeCanceled;
    /// <summary>蓄力中每帧广播已蓄力时间（秒）。</summary>
    public event Action<float> ChargeChanged;
    /// <summary>出拳瞬间广播档位：0 无蓄力，1~3 对应一至三段。</summary>
    public event Action<int> PunchReleased;
    public event Action GuardStarted;
    public event Action GuardEnded;
    /// <summary>成功格挡一次（已回充能）。</summary>
    public event Action GuardSucceeded;

    public bool IsCharging => charging;
    public bool IsAttacking => attacking;
    public float ChargeTime => elapsed;
    public float TierOneTime => tierOneTime;
    public float TierTwoTime => tierTwoTime;
    public float TierThreeTime => tierThreeTime;
    /// <summary>当前充能允许的蓄力上限（秒）。到达后松开即为最高可用档位。</summary>
    public float ChargeCap => storedEnergy == 3 ? tierThreeTime : storedEnergy == 2 ? tierTwoTime
        : storedEnergy == 1 ? tierOneTime : minimumWindup;
    public bool IsChargeCapped => charging && elapsed >= ChargeCap - 0.0001f;

    private PunchPresentation presentation;
    private PlayerMovement movement;
    private PlayerCombatController combat;
    private PlayerGauntletEquipment equipment;
    private PlayerInteractor interactor;
    private CharacterController controller;
    private Camera aimCamera;
    private Transform aim;

    public bool CanGuard => isActiveAndEnabled
        && !charging
        && !attacking
        && (equipment == null || equipment.HasGauntlet)
        && (combat == null || !combat.IsDefeated);

    public bool IsGuarding => CanGuard
        && Cursor.lockState == CursorLockMode.Locked
        && Mouse.current != null
        && Mouse.current.rightButton.isPressed;

    /// <summary>
    /// Checks whether the player is currently holding Right-Click guard and facing toward the given world position.
    /// </summary>
    public bool IsGuardingTowardsPoint(Vector3 targetWorldPoint)
    {
        if (!IsGuarding) return false;
        Vector3 origin = aim != null ? aim.position : transform.position;
        Vector3 toTarget = targetWorldPoint - origin;
        return IsDirectionWithinGuardCone(toTarget);
    }

    /// <summary>
    /// Checks whether the player is currently holding Right-Click guard and facing against a surface with the given normal.
    /// (surfaceNormal points outward from the wall/object toward the player.)
    /// </summary>
    public bool IsGuardingSurface(Vector3 surfaceNormal)
    {
        if (!IsGuarding) return false;
        return IsDirectionWithinGuardCone(-surfaceNormal);
    }

    private bool IsDirectionWithinGuardCone(Vector3 toObstacleDir)
    {
        if (toObstacleDir.sqrMagnitude < 0.0001f) return true;
        float halfAngle = guardAngle * 0.5f;

        Vector3 viewForward = aim != null ? aim.forward : transform.forward;
        if (Vector3.Angle(viewForward, toObstacleDir) <= halfAngle)
            return true;

        Vector3 flatForward = Vector3.ProjectOnPlane(viewForward, Vector3.up);
        Vector3 flatTarget = Vector3.ProjectOnPlane(toObstacleDir, Vector3.up);
        if (flatForward.sqrMagnitude > 0.001f && flatTarget.sqrMagnitude > 0.001f)
        {
            if (Vector3.Angle(flatForward, flatTarget) <= halfAngle)
                return true;
        }

        return false;
    }

    public bool AddEnergy(int amount)
    {
        if (equipment == null) equipment = GetComponent<PlayerGauntletEquipment>();
        if (equipment != null && !equipment.HasGauntlet) return false;
        if (amount <= 0 || storedEnergy >= 3) return false;
        storedEnergy = Mathf.Min(3, storedEnergy + amount);
        return true;
    }

    /// <summary>
    /// Refills 1 energy and triggers metallic guard feedback (sound, sparks, camera shake).
    /// </summary>
    public void RefillEnergyByGuard(Vector3 impactPoint, Vector3 impactNormal)
    {
        if (equipment == null) equipment = GetComponent<PlayerGauntletEquipment>();
        if (equipment != null && !equipment.HasGauntlet) return;

        storedEnergy = Mathf.Min(3, storedEnergy + 1);
        if (presentation != null)
            presentation.GuardFeedback(true, impactPoint, impactNormal);
        GuardSucceeded?.Invoke();
    }

    public void NotifyPlasticCollision()
    {
        if (presentation != null)
            presentation.GuardFeedback(false);
    }

    // Call only after the combat system confirms a successful block.
    public void NotifySuccessfulBlock() => AddEnergy(3);

    private bool charging;
    private bool releaseQueued;
    private bool cursorWasLocked;
    private float elapsed;
    private float chargeStartedAt;
    private float nextReady;
    public int CurrentTier => Mathf.Min(storedEnergy, elapsed >= tierThreeTime ? 3 : elapsed >= tierTwoTime ? 2 : elapsed >= tierOneTime ? 1 : 0);

    private void Awake()
    {
        storedEnergy = initialEnergy;
        presentation = GetComponent<PunchPresentation>();
        if (presentation == null) presentation = gameObject.AddComponent<PunchPresentation>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombatController>();
        equipment = GetComponent<PlayerGauntletEquipment>();
        interactor = GetComponent<PlayerInteractor>();
        controller = GetComponent<CharacterController>();
        aimCamera = GetComponentInChildren<Camera>();
        aim = aimCamera != null ? aimCamera.transform : transform.Find("CameraPivot");
        if (aim == null) aim = transform;
    }

    private void OnValidate()
    {
        minimumWindup = Mathf.Max(0.01f, minimumWindup);
        tierOneTime = Mathf.Max(minimumWindup + 0.01f, tierOneTime);
        tierTwoTime = Mathf.Max(tierOneTime + 0.01f, tierTwoTime);
        tierThreeTime = Mathf.Max(tierTwoTime + 0.01f, tierThreeTime);
        tierOneLungeDistance = Mathf.Max(0.05f, tierOneLungeDistance);
        tierTwoLungeDistance = Mathf.Max(tierOneLungeDistance + 0.05f, tierTwoLungeDistance);
        tierThreeLungeDistance = Mathf.Max(tierTwoLungeDistance + 0.05f, tierThreeLungeDistance);
    }

    private void Update()
    {
        if (presentation != null && presentation.IsHitStopped) return;
        if (equipment == null) equipment = GetComponent<PlayerGauntletEquipment>();
        if (equipment != null && !equipment.HasGauntlet)
        {
            CancelCharge();
            attacking = false;
            if (movement != null) movement.StopPunchLunge();
            return;
        }
        if (attacking)
        {
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked
                || movement == null || !movement.enabled
                || (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)))
            {
                attacking = false;
                if (movement != null) movement.StopPunchLunge();
                return;
            }
            TryLungeHit();
            return;
        }
        Keyboard key = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool cancel = !Application.isFocused || Cursor.lockState != CursorLockMode.Locked
            || (movement != null && (!movement.enabled || movement.IsDashing))
            || (key != null && (key.escapeKey.wasPressedThisFrame || key.rKey.wasPressedThisFrame));
        if (cancel || mouse == null)
        {
            CancelCharge();
            return;
        }
        // Pressing Right Mouse Button to guard cancels any unlaunched charge
        if (charging && mouse.rightButton.wasPressedThisFrame)
        {
            CancelCharge();
            return;
        }
        if (!charging && cursorWasLocked && !mouse.rightButton.isPressed && Time.unscaledTime >= nextReady && mouse.leftButton.wasPressedThisFrame)
        {
            charging = true;
            releaseQueued = false;
            elapsed = 0f;
            chargeStartedAt = Time.unscaledTime;
            ChargeChanged?.Invoke(0f);
            ChargeStarted?.Invoke();
        }
        if (!charging) return;
        // Real time keeps the hold duration consistent inside bullet time.
        elapsed = Mathf.Min(ChargeCap, Time.unscaledTime - chargeStartedAt);
        ChargeChanged?.Invoke(elapsed);
        if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed) releaseQueued = true;
        if (releaseQueued && elapsed >= minimumWindup)
        {
            int tier = CurrentTier;
            ClearCharge();
            nextReady = Time.unscaledTime + recovery;
            storedEnergy -= tier;
            attackTier = tier;
            attacking = true;
            PunchReleased?.Invoke(tier);
            // 无蓄力快拳（0 档）不给任何冲刺位移：否则玩家能靠零消耗的普攻反复推自己上天（无限飞行）。
            float distance = tier == 3 ? tierThreeLungeDistance
                : tier == 2 ? tierTwoLungeDistance
                : tier == 1 ? tierOneLungeDistance
                : 0f;
            if (presentation != null) presentation.Swing(tier);
            if (TryHitAimedTarget()) return;
            if (movement != null && distance > 0.0001f) movement.BeginPunchLunge(distance);
            FinishMissIfLungeEnded();
        }
    }

    private void LateUpdate()
    {
        cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
        bool guarding = IsGuarding;
        if (guarding == guardWasHeld) return;
        guardWasHeld = guarding;
        if (guarding) GuardStarted?.Invoke(); else GuardEnded?.Invoke();
    }

    private bool guardWasHeld;
    private void OnDisable() { CancelCharge(); attacking = false; if (movement != null) movement.StopPunchLunge(); cursorWasLocked = false; }
    private void OnApplicationFocus(bool focused) { if (!focused) { CancelCharge(); cursorWasLocked = false; } }
    public void CancelCharge()
    {
        bool wasCharging = charging;
        ClearCharge();
        if (!wasCharging) return;
        ChargeChanged?.Invoke(0f);
        ChargeCanceled?.Invoke();
    }

    private void ClearCharge()
    {
        charging = false;
        releaseQueued = false;
        elapsed = 0f;
    }
    public void ResetPunchState() { CancelCharge(); attacking = false; storedEnergy = initialEnergy; nextReady = 0f; }

    private void TryLungeHit()
    {
        if (!TryHitAimedTarget()) FinishMissIfLungeEnded();
    }

    private void FinishMissIfLungeEnded()
    {
        if (attacking && (movement == null || !movement.IsPunchLunging))
        {
            attacking = false;
            ChargedPunchPulse.Spawn(aim.position + aim.forward * reach, -aim.forward, attackTier);
            nextReady = Time.unscaledTime + recovery;
        }
    }

    private bool TryHitAimedTarget()
    {
        Camera camera = interactor != null && interactor.ViewCamera != null ? interactor.ViewCamera : aimCamera;
        Ray ray = camera != null ? camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(aim.position, aim.forward);
        if (camera != null && !camera.orthographic) ray.origin = camera.transform.position;

        Bounds body = controller != null ? controller.bounds : new Bounds(transform.position, Vector3.zero);
        float searchDistance = reach + Vector3.Distance(ray.origin, body.center) + body.extents.magnitude;
        RaycastHit? nearest = null;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, searchDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (!nearest.HasValue) return false;

        RaycastHit target = nearest.Value;
        // Measure from the player capsule, so looking down at a box does not shorten the reach.
        float radius = Mathf.Min(body.extents.x, body.extents.y, body.extents.z);
        float halfSegment = body.extents.y - radius;
        Vector3 axisPoint = body.center;
        axisPoint.y = Mathf.Clamp(target.point.y, axisPoint.y - halfSegment, axisPoint.y + halfSegment);
        if ((target.point - axisPoint).sqrMagnitude > (reach + radius) * (reach + radius)) return false;

        Collider collider = target.collider;
        KineticBox box = collider.GetComponentInParent<KineticBox>();
        IPunchReceiver receiver = collider.GetComponentInParent<IPunchReceiver>();
        PunchTargetHealth health = collider.GetComponentInParent<PunchTargetHealth>();
        Rigidbody rigidbody = collider.attachedRigidbody;
        if (box == null && receiver == null && health == null && (rigidbody == null || rigidbody.isKinematic))
            return false;

        ResolvePunchHit(collider, target.point, target.normal, ray.direction, box, receiver, health);
        return true;
    }

    public void ResolveLungeContact(Collider collider, Vector3 point, Vector3 normal)
    {
        if (!attacking || collider.transform.IsChildOf(transform)) return;
        // Ignore ground support contact; walls/targets consume this attack exactly once.
        if (normal.y > 0.65f && aim.forward.y > -0.4f) return;
        ResolvePunchHit(collider, point, normal, aim.forward, collider.GetComponentInParent<KineticBox>(),
            collider.GetComponentInParent<IPunchReceiver>(), collider.GetComponentInParent<PunchTargetHealth>());
    }

    private void ResolvePunchHit(Collider collider, Vector3 point, Vector3 normal, Vector3 direction,
        KineticBox box, IPunchReceiver receiver, PunchTargetHealth health)
    {
        attacking = false;
        if (movement != null) movement.StopPunchLunge();
        int tier = attackTier;
        int damage = tier == 3 ? tierThreeDamage : tier == 2 ? tierTwoDamage : tier == 1 ? tierOneDamage : basicDamage;
        float impulse = tier == 3 ? tierThreeImpulse : tier == 2 ? tierTwoImpulse : tier == 1 ? tierOneImpulse : 0f;
        if (box != null && tier > 0)
        {
            // Aiming at the lid from beside a box should push it across the floor.
            Vector3 horizontalAim = Vector3.ProjectOnPlane(direction, Vector3.up);
            bool hitLidFromBeside = normal.y > 0.65f && controller != null
                && controller.bounds.min.y < point.y - 0.15f && horizontalAim.sqrMagnitude > 0.01f;
            Vector3 appliedDir = box.ApplyCardinalImpulse(hitLidFromBeside ? -horizontalAim.normalized : normal,
                direction, impulse);
            if (movement != null) movement.ApplyBoxHitRecoil(appliedDir);
        }
        if (health != null) health.TakeDamage(damage);

        if (receiver != null) receiver.OnPunched(point, normal, direction, tier, impulse);
        if (receiver != null && tier > 0 && liftOnEnemyHit && movement != null)
            movement.LiftAfterCombatPunch(tier == 3 ? combatLiftSpeeds.z : tier == 2 ? combatLiftSpeeds.y : combatLiftSpeeds.x);
        if (presentation != null) presentation.Impact(point, normal, tier, receiver != null && receiver.IsFlesh);
        Rigidbody body = collider.attachedRigidbody;
        if (box == null && tier > 0 && body != null && !body.isKinematic)
            body.AddForce(direction * impulse, ForceMode.VelocityChange);
        ChargedPunchPulse.Spawn(point + normal * 0.03f, normal, tier);
        nextReady = Time.unscaledTime + recovery;
    }

    // 储能与蓄力反馈由 PlayerGauntletChargeEffects 显示在拳套本体上。
}
