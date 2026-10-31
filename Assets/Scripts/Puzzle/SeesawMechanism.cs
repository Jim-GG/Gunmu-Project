using UnityEngine;

/// <summary>
/// Level 3 Seesaw & Overhead Crane mechanism.
/// - Initial state: West end (-X) is pressed down by LaunchBox; East end (+X) is tilted up underneath an overhead Crane holding CraneWeightBox.
/// - When the player presses the Crane Button, CraneWeightBox drops onto the East end, flipping the seesaw and launching LaunchBox straight up (+Y).
/// </summary>
public class SeesawMechanism : MonoBehaviour
{
    [SerializeField] private Transform seesawPlank;
    [SerializeField] private Transform craneWeightBox;
    [SerializeField] private KineticBox launchBox;

    [Header("Seesaw & Crane Settings")]
    [SerializeField] private float initialTiltDeg = 16f;   // +16 deg: West (-X) down, East (+X) up
    [SerializeField] private float flippedTiltDeg = -16f;  // -16 deg: East (+X) down, West (-X) up
    [SerializeField] private float flipSpeedDegPerSec = 180f;
    [SerializeField] private float craneDropGravity = 28f;
    [SerializeField] private float eastImpactHeight = 2.25f;
    [SerializeField] private float launchUpwardSpeed = 22.5f;

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

    public bool IsReady => currentState == State.Ready;

    private void Awake()
    {
        if (seesawPlank == null)
            seesawPlank = transform.Find("SeesawPlank");

        if (craneWeightBox == null && transform.parent != null)
            craneWeightBox = transform.parent.Find("CraneWeightBox");

        if (launchBox == null && transform.parent != null)
        {
            Transform t = transform.parent.Find("KineticBox_Target");
            if (t == null) t = transform.parent.Find("KineticBox_L4");
            if (t == null) t = transform.parent.Find("KineticBox_L3");
            if (t != null)
                launchBox = t.GetComponent<KineticBox>();
        }

        if (craneWeightBox != null)
            craneWeightInitialPos = craneWeightBox.position;

        ResetMechanism();
    }

    private void Update()
    {
        if (currentState == State.CraneDropping)
        {
            if (craneWeightBox == null)
                return;

            weightDropSpeed += craneDropGravity * Time.deltaTime;
            Vector3 pos = craneWeightBox.position;
            pos.y -= weightDropSpeed * Time.deltaTime;

            if (pos.y <= eastImpactHeight)
            {
                pos.y = eastImpactHeight - 1.1f;
                craneWeightBox.position = pos;
                TriggerSeesawFlipAndCatapult();
            }
            else
            {
                craneWeightBox.position = pos;
            }
        }
        else if (currentState == State.SeesawFlipping)
        {
            currentTiltDeg = Mathf.MoveTowards(currentTiltDeg, flippedTiltDeg, flipSpeedDegPerSec * Time.deltaTime);
            ApplyPlankTilt(currentTiltDeg);

            if (Mathf.Approximately(currentTiltDeg, flippedTiltDeg))
            {
                currentState = State.Completed;
            }
        }
    }

    public void TriggerCraneRelease()
    {
        if (currentState != State.Ready)
            return;

        weightDropSpeed = 0f;
        currentState = State.CraneDropping;
        Debug.Log("[SeesawMechanism] Crane released! Heavy block dropping onto East end of seesaw!", this);
    }

    private void TriggerSeesawFlipAndCatapult()
    {
        currentState = State.SeesawFlipping;

        if (launchBox != null)
        {
            // Catapult the West box strictly vertically upward (+Y)!
            launchBox.ApplyFreeImpulse(Vector3.up, launchUpwardSpeed);
            Debug.Log($"[SeesawMechanism] Seesaw flipped! Catapulted LaunchBox straight UP at {launchUpwardSpeed:F1} m/s!", this);
        }
    }

    public void ResetMechanism()
    {
        currentState = State.Ready;
        currentTiltDeg = initialTiltDeg;
        weightDropSpeed = 0f;
        ApplyPlankTilt(currentTiltDeg);

        if (craneWeightBox != null && craneWeightInitialPos != Vector3.zero)
            craneWeightBox.position = craneWeightInitialPos;
    }

    private void ApplyPlankTilt(float angleDeg)
    {
        if (seesawPlank != null)
            seesawPlank.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
    }
}
