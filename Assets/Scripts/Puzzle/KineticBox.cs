using UnityEngine;

/// <summary>
/// Puzzle cube that accepts:
/// - Strict 6-direction cardinal impulses from the Player's Kinetic Dash (ApplyCardinalImpulse).
/// - Free/diagonal 3D impulses from scripted level mechanisms such as the Giant Pendulum (ApplyFreeImpulse).
/// Supports remote Momentum Lock (动量锁定), scalar impulse accumulation, direction override, and Bullet Time highlighting.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class KineticBox : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField, Min(0f)] private float groundLinearDamping = 0.8f;
    [SerializeField, Min(0f)] private float launchedLinearDamping = 0.02f;
    [SerializeField, Min(0f)] private float stopSpeedThreshold = 0.15f;

    [Header("Visual Colors")]
    [SerializeField] private Color normalColor = new Color(0.95f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color bulletTimeHighlightColor = new Color(0.0f, 1.0f, 0.95f, 1f);
    [SerializeField] private Color bulletTimeHoverColor = new Color(0.35f, 1.0f, 0.2f, 1f);
    [SerializeField] private Color lockedBaseColor = new Color(0.85f, 0.15f, 1.0f, 1f);
    [SerializeField] private Color lockedChargedColor = new Color(1.0f, 0.15f, 0.05f, 1f);

    private Rigidbody rb;
    private BoxCollider boxCollider;
    private Renderer boxRenderer;
    private MaterialPropertyBlock propBlock;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 lastImpulseDirection;
    private float lastImpulseTime = -10f;

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
    public bool IsMomentumLocked => isMomentumLocked;
    public float StoredSpeed => storedSpeed;
    public Vector3 StoredDirection => storedDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        boxRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (gameObject.name.Contains("CueBall"))
            normalColor = new Color(0.95f, 0.96f, 1.0f, 1f);

        // Keep the cube strictly axis-aligned so its 6 faces remain consistent for player dashes.
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = groundLinearDamping;

        PhysicsMaterial slideMat = new PhysicsMaterial("KineticBoxSlide")
        {
            dynamicFriction = 0.05f,
            staticFriction = 0.05f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        boxCollider.material = slideMat;

        CreateDirectionArrowIndicator();
        UpdateVisuals();
    }

    private void CreateDirectionArrowIndicator()
    {
        GameObject rootObj = new GameObject("MomentumArrowRoot");
        arrowRoot = rootObj.transform;
        arrowRoot.SetParent(transform, false);
        arrowRoot.localPosition = Vector3.zero;

        // Shaft
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "ArrowShaft";
        Destroy(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(arrowRoot, false);
        shaft.transform.localPosition = new Vector3(0f, 0f, 0.75f);
        shaft.transform.localScale = new Vector3(0.18f, 0.18f, 1.1f);

        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "ArrowHead";
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(arrowRoot, false);
        head.transform.localPosition = new Vector3(0f, 0f, 1.35f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.38f, 0.22f, 0.38f);

        arrowRenderers = arrowRoot.GetComponentsInChildren<Renderer>();
        arrowRoot.gameObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (isMomentumLocked)
            return;

        if (highSpeedLaunchTimer > 0f)
        {
            highSpeedLaunchTimer -= Time.fixedDeltaTime;
            rb.linearDamping = launchedLinearDamping;
        }
        else
        {
            rb.linearDamping = groundLinearDamping;
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

        // Check Box-to-Box ("Cue Ball" -> Target Box) impulse transfer!
        CheckBoxToBoxImpulseTransfer();
    }

    private void CheckBoxToBoxImpulseTransfer()
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
        float speed = horizVel.magnitude;
        if (speed < 3.5f)
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
            float maxVertTolerance = other.IsMomentumLocked ? 2.0f : 1.0f;
            float hitRadius = (boxCollider.bounds.extents.x + other.boxCollider.bounds.extents.x) + speed * Time.fixedDeltaTime + 0.15f;

            if (forwardDist > 0f && forwardDist <= hitRadius && lateralDist <= 0.9f && verticalDist <= maxVertTolerance)
            {
                // Transfer impulse strictly along the 6 cardinal axes of the struck box, and stop the Cue Ball box!
                other.ApplyCardinalImpulse(-moveDir, moveDir, speed, allowVerticalAxis: false);
                rb.linearVelocity = Vector3.zero;
                isFreeTrajectory = false;
                Debug.Log($"[KineticBox] Cue Ball '{name}' struck '{other.name}' and transferred {speed:F1} m/s impulse along {moveDir}!", this);
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
            highSpeedLaunchTimer = 1.5f;
            rb.linearDamping = launchedLinearDamping;
            rb.linearVelocity = launchVel;

            // If the player is standing on top of (or right next to the launch face of) this box, catapult the player together with it!
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null && IsPlayerRidingOrTouching(player))
            {
                player.LaunchWithReleasedBox(launchVel, boxCollider);
            }

            Debug.Log($"[KineticBox] Momentum Released! Launching along {storedDirection} at {storedSpeed:F1} m/s", this);
        }

        storedSpeed = 0f;
        storedDirection = Vector3.zero;
        UpdateVisuals();
    }

    private bool IsPlayerRidingOrTouching(PlayerMovement player)
    {
        Vector3 pPos = player.transform.position;
        Bounds b = boxCollider.bounds;

        bool horizOverlap =
            Mathf.Abs(pPos.x - b.center.x) <= (b.extents.x + 0.65f) &&
            Mathf.Abs(pPos.z - b.center.z) <= (b.extents.z + 0.65f);

        bool onTopOrTouching =
            pPos.y >= (b.min.y - 0.2f) &&
            pPos.y <= (b.max.y + 0.85f);

        return horizOverlap && onTopOrTouching;
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
        if (Time.unscaledTime - lastImpulseTime < 0.1f)
            return lastImpulseDirection;

        lastImpulseTime = Time.unscaledTime;
        isFreeTrajectory = false;

        Vector3 rawPushDir = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : fallbackDirection;
        Vector3 cardinalDir = SnapToCardinalDirection(rawPushDir, allowVertical: allowVerticalAxis);
        lastImpulseDirection = cardinalDir;

        if (isMomentumLocked)
        {
            storedSpeed = Mathf.Min(storedSpeed + impulseSpeed, 40f);
            storedDirection = cardinalDir;
            UpdateVisuals();
            Debug.Log($"[KineticBox] Locked Box absorbed 6-Axis player impulse! Stored speed: {storedSpeed:F1} m/s, Dir: {storedDirection}", this);
        }
        else
        {
            rb.linearVelocity = cardinalDir * impulseSpeed;
            Debug.Log($"[KineticBox] Applied 6-Axis Impulse along {cardinalDir} with speed {impulseSpeed:F1} m/s", this);
        }

        return cardinalDir;
    }

    /// <summary>
    /// Used by scripted mechanisms (e.g., Giant Pendulum): applies an arbitrary/diagonal 3D impulse without 6-axis snapping.
    /// </summary>
    public Vector3 ApplyFreeImpulse(Vector3 worldDirection, float impulseSpeed)
    {
        if (Time.unscaledTime - lastImpulseTime < 0.1f)
            return lastImpulseDirection;

        lastImpulseTime = Time.unscaledTime;
        isFreeTrajectory = true;

        Vector3 dir = worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : transform.forward;
        lastImpulseDirection = dir;

        if (isMomentumLocked)
        {
            storedSpeed = Mathf.Min(storedSpeed + impulseSpeed, 40f);
            storedDirection = dir;
            UpdateVisuals();
            Debug.Log($"[KineticBox] Locked Box absorbed diagonal mechanism impulse! Stored speed: {storedSpeed:F1} m/s, Diagonal Dir: {storedDirection}", this);
        }
        else
        {
            highSpeedLaunchTimer = 1.5f;
            rb.linearDamping = launchedLinearDamping;
            rb.linearVelocity = dir * impulseSpeed;
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
        rb.isKinematic = false;
        rb.linearDamping = groundLinearDamping;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        lastImpulseTime = -10f;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (boxRenderer == null)
            return;

        Color targetColor = normalColor;
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

        boxRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", targetColor);
        propBlock.SetColor("_Color", targetColor);
        propBlock.SetColor("_EmissionColor", targetColor * (isMomentumLocked || inBulletTime ? 1.5f : 0f));
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
                    r.SetPropertyBlock(propBlock);
                }
            }
        }
    }
}
