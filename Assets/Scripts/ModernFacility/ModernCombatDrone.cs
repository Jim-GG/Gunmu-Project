using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A hostile patrol drone that can be knocked down and briefly piloted.
/// While piloting, the player body follows the drone, so leaving control keeps
/// the player at the drone's current location.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class ModernCombatDrone : MonoBehaviour, IPunchReceiver
{
    public enum DroneState { Patrol, Attacking, Launched, Wrecked, Piloted }

    [Header("AI")]
    public Transform patrolA;
    public Transform patrolB;
    public PlayerCombatController targetPlayer;
    [Min(1f)] public float detectionRange = 18f;
    [Min(0.1f)] public float patrolSpeed = 2.2f;
    [Min(0.1f)] public float attackSpeed = 3f;
    [Min(0.1f)] public float attackInterval = 2.8f;
    [Min(0f)] public float attackDamage = 1f;
    [Min(0f)] public float attackWindup = 0.8f;
    [Min(1f)] public float attackAltitude = 4.5f;
    [Min(1f)] public float diveSpeed = 8f;

    [Header("击毁与接管")]
    [Min(1f)] public float maxHealth = 3f;
    [Min(0.1f)] public float pilotSpeed = 7f;
    [Min(0.1f)] public float pilotDuration = 20f;
    [Min(0.5f)] public float boardingDistance = 2.2f;
    public Transform playerCamera;
    public Transform seat;
    public PlayerMovement playerMovement;
    public PlayerLook playerLook;
    public PlayerChargedPunch playerPunch;
    public Collider pilotGate;

    public DroneState State { get; private set; } = DroneState.Patrol;
    public float Health { get; private set; }
    public bool IsFlesh => false;
    public bool IsWrecked => State == DroneState.Wrecked;
    public bool IsPiloted => State == DroneState.Piloted;

    private Rigidbody body;
    private Collider hitCollider;
    private PlayerChargedPunch chargedPunch;
    private CharacterController playerBody;
    private Vector3 wreckVelocity;
    private float nextAttack;
    private float attackReady;
    private float remainingPilotTime;
    private bool patrolTowardB = true;
    private bool movementWasEnabled;
    private bool punchWasEnabled;
    private bool lookWasEnabled;
    private Transform cameraPivot;
    private float pilotPitch;
    private float pilotYaw;
    private Light warningLight;
    private Renderer warningRenderer;
    private Material warningMaterial;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        hitCollider = GetComponent<Collider>();
        body.useGravity = false;
        body.isKinematic = true;
        Health = maxHealth;
        EnsureAttackWarning();
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked) return;

        UpdateAttackWarning();

        if (State == DroneState.Piloted)
        {
            TickPilot();
            return;
        }

        if (State == DroneState.Wrecked)
        {
            TryBoardFromPlayerInput();
            return;
        }

        if (State == DroneState.Wrecked || State == DroneState.Launched) return;

        if (State == DroneState.Attacking && Time.time >= attackReady)
        {
            ResolveAttack();
            State = DroneState.Patrol;
            nextAttack = Time.time + attackInterval;
        }
    }

    private void FixedUpdate()
    {
        if (GameplayGate.IsBlocked) return;

        if (State == DroneState.Launched)
        {
            body.MovePosition(body.position + wreckVelocity * Time.fixedDeltaTime);
            return;
        }

        if (State == DroneState.Piloted || State == DroneState.Wrecked) return;
        if (!targetPlayer || targetPlayer.IsDefeated)
        {
            Patrol();
            return;
        }

        Vector3 playerPoint = targetPlayer.transform.position + Vector3.up;
        Vector3 toPlayer = playerPoint - transform.position;
        Vector3 rayOrigin = transform.position + toPlayer.normalized * 0.9f;
        bool visible = toPlayer.magnitude <= detectionRange
            && Physics.Raycast(rayOrigin, toPlayer.normalized, out RaycastHit hit,
                Mathf.Max(0f, toPlayer.magnitude - 0.9f), ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerCombatController>() == targetPlayer;

        if (!visible)
        {
            if (State == DroneState.Attacking) State = DroneState.Patrol;
            Patrol();
            return;
        }

        Face(playerPoint);
        if (State == DroneState.Attacking)
        {
            MoveToward(playerPoint + Vector3.up * 0.4f, diveSpeed);
            return;
        }

        State = DroneState.Patrol;
        Vector3 horizontalAway = Vector3.ProjectOnPlane(-toPlayer, Vector3.up).normalized;
        Vector3 highApproach = playerPoint + Vector3.up * attackAltitude + horizontalAway * 1.4f;
        MoveToward(highApproach, attackSpeed);
        if (Time.time >= nextAttack && Vector3.Distance(transform.position, highApproach) < 1.25f)
        {
            State = DroneState.Attacking;
            attackReady = Time.time + attackWindup;
        }
    }

    private void Patrol()
    {
        if (!patrolA || !patrolB) return;
        Transform destination = patrolTowardB ? patrolB : patrolA;
        MoveToward(destination.position, patrolSpeed);
        if (Vector3.Distance(transform.position, destination.position) < 0.3f)
            patrolTowardB = !patrolTowardB;
    }

    private void MoveToward(Vector3 destination, float speed)
    {
        Vector3 next = Vector3.MoveTowards(body.position, destination, speed * Time.fixedDeltaTime);
        Vector3 direction = next - body.position;
        if (direction.sqrMagnitude > 0.001f) Face(next + direction.normalized);
        body.MovePosition(next);
    }

    private void Face(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
        body.MoveRotation(Quaternion.RotateTowards(body.rotation, desired, 240f * Time.fixedDeltaTime));
    }

    private void ResolveAttack()
    {
        if (!targetPlayer || targetPlayer.IsDefeated) return;
        Vector3 target = targetPlayer.transform.position + Vector3.up;
        Vector3 direction = target - transform.position;
        Vector3 origin = transform.position + direction.normalized * 0.9f;
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                Mathf.Min(detectionRange, direction.magnitude), ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerCombatController>() == targetPlayer)
            targetPlayer.ReceiveAttack(origin, attackDamage, true);
    }

    public void OnPunched(Vector3 point, Vector3 normal, Vector3 direction, int tier, float strength)
    {
        if (State == DroneState.Wrecked || State == DroneState.Piloted) return;

        Health -= Mathf.Max(1, tier + 1);
        if (Health <= 0)
        {
            State = DroneState.Wrecked;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = direction.normalized * Mathf.Max(5f, strength);
            body.angularVelocity = Random.insideUnitSphere * 5f;
            return;
        }

        if (tier > 0)
        {
            State = DroneState.Launched;
            wreckVelocity = direction.normalized * (5f + tier * 3f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (State != DroneState.Launched || collision.collider.isTrigger) return;
        State = DroneState.Wrecked;
        wreckVelocity = Vector3.zero;
        if (body) body.isKinematic = false;
    }

    private void TickPilot()
    {
        remainingPilotTime -= Time.deltaTime;
        if (Keyboard.current?.eKey.wasPressedThisFrame == true || remainingPilotTime <= 0f)
        {
            EndPilot();
            return;
        }

        if (playerCamera && Mouse.current?.rightButton.isPressed == true)
        {
            Vector3 direction = playerCamera.forward;
            // The hit collider is disabled while piloted. Move both the transform
            // and rigidbody pose directly so a kinematic physics step cannot
            // overwrite mouse-flight movement between Update and FixedUpdate.
            Vector3 nextPosition = transform.position + direction * pilotSpeed * Time.deltaTime;
            transform.position = nextPosition;
            body.position = nextPosition;
        }

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            pilotYaw = Mathf.Repeat(pilotYaw + mouseDelta.x * 0.1f, 360f);
            Quaternion nextRotation = Quaternion.Euler(0f, pilotYaw, 0f);
            transform.rotation = nextRotation;
            body.rotation = nextRotation;
            pilotPitch = Mathf.Clamp(pilotPitch - mouseDelta.y * 0.1f, -75f, 75f);
            if (playerLook) playerLook.SynchronizePitch(pilotPitch);
            else if (cameraPivot) cameraPivot.localRotation = Quaternion.Euler(pilotPitch, 0f, 0f);
        }
    }

    private void TryBoardFromPlayerInput()
    {
        if (!playerMovement || !playerLook || !playerPunch || Keyboard.current?.eKey.wasPressedThisFrame != true)
            return;

        TryBeginPilot(playerMovement, playerLook, playerPunch);
    }

    public bool TryBeginPilot(PlayerMovement movement, PlayerLook look, PlayerChargedPunch punch)
    {
        if (!IsWrecked || !movement || Vector3.Distance(movement.transform.position, transform.position) > boardingDistance)
            return false;

        playerMovement = movement;
        playerLook = look;
        chargedPunch = punch;
        playerBody = movement.GetComponent<CharacterController>();
        targetPlayer = movement.GetComponent<PlayerCombatController>();
        playerCamera = playerCamera ? playerCamera : movement.GetComponentInChildren<Camera>()?.transform;
        cameraPivot = movement.transform.Find("CameraPivot");
        pilotPitch = 0f;
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        pilotYaw = flatForward.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(flatForward.normalized, Vector3.up).eulerAngles.y
            : movement.transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, pilotYaw, 0f);
        body.rotation = transform.rotation;
        if (look) look.SynchronizePitch(0f);

        movementWasEnabled = movement.enabled;
        punchWasEnabled = punch && punch.enabled;
        lookWasEnabled = look && look.enabled;
        if (movement) movement.enabled = false;
        if (punch) punch.enabled = false;
        if (look) look.enabled = false;
        if (playerBody) playerBody.enabled = false;

        body.isKinematic = true;
        hitCollider.enabled = false;
        if (pilotGate) pilotGate.enabled = false;
        State = DroneState.Piloted;
        remainingPilotTime = pilotDuration;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlacePlayerAtSeat();
        return true;
    }

    private void PlacePlayerAtSeat()
    {
        if (!playerBody) return;
        Vector3 destination = seat ? seat.position : transform.position + Vector3.up * 1.1f;
        // The drone body can pitch and roll during a dive or wreck animation.
        // Never copy that tilt to the player's root: only transfer the yaw.
        // The player's vertical look remains on CameraPivot via PlayerLook.
        Quaternion uprightYaw = Quaternion.Euler(0f, pilotYaw, 0f);
        playerMovement.transform.SetPositionAndRotation(destination, uprightYaw);
    }

    private void LateUpdate()
    {
        if (State == DroneState.Piloted) PlacePlayerAtSeat();
    }

    public void EndPilot()
    {
        if (State != DroneState.Piloted) return;
        PlacePlayerAtSeat();
        State = DroneState.Wrecked;
        // Keep the wreck non-colliding until destruction at the end of the frame.
        hitCollider.enabled = false;
        if (playerBody) playerBody.enabled = false;
        if (playerMovement) playerMovement.enabled = movementWasEnabled;
        if (chargedPunch) chargedPunch.enabled = punchWasEnabled;
        if (playerLook) playerLook.enabled = lookWasEnabled;
        if (playerLook) playerLook.SynchronizePitch(pilotPitch);
        if (playerBody) playerBody.enabled = true;
        if (pilotGate) pilotGate.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        if (IsPiloted)
            GUI.Label(new Rect(20, 20, 400, 30), $"无人机遥控剩余 {remainingPilotTime:0} 秒　右键飞行　E 退出");
        else if (IsWrecked && playerMovement
            && Vector3.Distance(playerMovement.transform.position, transform.position) < boardingDistance)
            GUI.Label(new Rect(Screen.width * 0.5f - 120, Screen.height * 0.7f, 300, 32), "按 E 接管无人机");
    }

    private void EnsureAttackWarning()
    {
        Transform existing = transform.Find("攻击预警灯");
        if (existing)
        {
            warningLight = existing.GetComponent<Light>();
            warningRenderer = existing.GetComponent<Renderer>();
        }
        if (!existing)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "攻击预警灯";
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * 0.42f;
            lamp.transform.localScale = Vector3.one * 0.18f;
            Collider lampCollider = lamp.GetComponent<Collider>();
            if (lampCollider) Destroy(lampCollider);
            warningRenderer = lamp.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            warningMaterial = new Material(shader);
            warningMaterial.color = new Color(1f, .04f, .02f);
            warningMaterial.EnableKeyword("_EMISSION");
            if (warningMaterial.HasProperty("_EmissionColor")) warningMaterial.SetColor("_EmissionColor", new Color(1f, .015f, .005f));
            warningRenderer.material = warningMaterial;
            warningLight = lamp.AddComponent<Light>();
            warningLight.type = LightType.Point;
            warningLight.color = new Color(1f, .025f, .01f);
            warningLight.range = 5f;
            warningLight.shadows = LightShadows.None;
        }
        if (warningLight) warningLight.intensity = 0f;
        if (warningRenderer) warningRenderer.enabled = false;
    }

    private void UpdateAttackWarning()
    {
        bool warning = State == DroneState.Attacking;
        float pulse = warning ? .35f + Mathf.PingPong(Time.time * 7f, .65f) : 0f;
        if (warningLight) warningLight.intensity = pulse * 4f;
        if (warningRenderer)
        {
            warningRenderer.enabled = warning;
            if (warningMaterial && warningMaterial.HasProperty("_EmissionColor"))
                warningMaterial.SetColor("_EmissionColor", new Color(1f, .015f, .005f) * pulse * 2f);
        }
    }
}
