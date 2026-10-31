using UnityEngine;

/// <summary>
/// East-West swinging giant pendulum above the trench.
/// Calculates the impulse direction dynamically from the pendulum's instantaneous swing angle (tangent vector) at the moment of impact!
/// </summary>
public class PendulumMechanism : MonoBehaviour
{
    [SerializeField] private Transform pivotTransform;
    [SerializeField] private Collider hammerCollider;
    [SerializeField] private float startAngleDeg = -65f;
    [SerializeField] private float maxEastAngleDeg = 65f;
    [SerializeField] private float swingSpeedDegPerSec = 140f;
    [SerializeField] private float impulseSpeedToBox = 18.5f;

    private enum State
    {
        ReadyAtWest,
        SwingingEast,
        ReboundingWest,
        Stopped
    }

    private State currentState = State.ReadyAtWest;
    private float currentAngleDeg;
    private bool hasDeliveredHit;

    public bool IsReady => currentState == State.ReadyAtWest;
    public float CurrentAngleDeg => currentAngleDeg;

    private void Awake()
    {
        if (pivotTransform == null)
            pivotTransform = transform;

        if (hammerCollider == null)
        {
            Transform hammerChild = transform.Find("PendulumHammer");
            if (hammerChild != null)
                hammerCollider = hammerChild.GetComponent<Collider>();
        }

        ResetPendulum();
    }

    private void Update()
    {
        if (currentState == State.SwingingEast)
        {
            currentAngleDeg += swingSpeedDegPerSec * Time.deltaTime;
            ApplyPivotAngle(currentAngleDeg);

            CheckHammerBoxHit();

            if (currentAngleDeg >= maxEastAngleDeg)
            {
                currentState = State.ReboundingWest;
            }
        }
        else if (currentState == State.ReboundingWest)
        {
            currentAngleDeg = Mathf.MoveTowards(currentAngleDeg, startAngleDeg, swingSpeedDegPerSec * 0.85f * Time.deltaTime);
            ApplyPivotAngle(currentAngleDeg);

            if (Mathf.Approximately(currentAngleDeg, startAngleDeg))
            {
                currentState = State.ReadyAtWest;
                hasDeliveredHit = false;
            }
        }
    }

    private void CheckHammerBoxHit()
    {
        if (hasDeliveredHit || hammerCollider == null)
            return;

        Bounds b = hammerCollider.bounds;
        Collider[] hits = Physics.OverlapBox(b.center, b.extents * 1.1f, hammerCollider.transform.rotation);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].TryGetComponent(out KineticBox box))
            {
                hasDeliveredHit = true;

                // Compute the instantaneous tangent direction from the pendulum's current swing angle!
                // At currentAngleDeg == 0 (lowest point), tangent is (1, 0, 0) (horizontal East).
                // At currentAngleDeg > 0 (upswing on the East side of the pivot), tangent is (cos(θ), sin(θ), 0) (diagonally Upward-East!).
                float rad = currentAngleDeg * Mathf.Deg2Rad;
                Vector3 tangentDirection = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;

                box.ApplyFreeImpulse(tangentDirection, impulseSpeedToBox);

                // Rebound back West so the hammer clears the Eastward launch trajectory!
                currentState = State.ReboundingWest;
                Debug.Log($"[PendulumMechanism] Struck KineticBox at swing angle {currentAngleDeg:F1}° -> Tangent impulse direction: {tangentDirection}", this);
                return;
            }
        }
    }

    public void TriggerRelease()
    {
        if (currentState != State.ReadyAtWest)
            return;

        hasDeliveredHit = false;
        currentState = State.SwingingEast;
        Debug.Log("[PendulumMechanism] Pendulum Released! Swinging from West (-X) to East (+X)!", this);
    }

    public void ResetPendulum()
    {
        currentState = State.ReadyAtWest;
        currentAngleDeg = startAngleDeg;
        hasDeliveredHit = false;
        ApplyPivotAngle(currentAngleDeg);
    }

    private void ApplyPivotAngle(float angleDeg)
    {
        if (pivotTransform != null)
        {
            pivotTransform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }
    }
}
