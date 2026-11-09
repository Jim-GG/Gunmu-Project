using UnityEngine;

[DefaultExecutionOrder(-30)]
public sealed class ModernServiceDrone : MonoBehaviour, IPunchReceiver
{
    public Transform lowerDock, upperDock, rotorLeft, rotorRight;
    public BoxCollider deck;
    public PlayerMovement rider;
    public MonoBehaviour powerGate;
    [Min(.1f)] public float speed = 2.2f;
    private CharacterController body;
    private Light indicator;
    private bool goingUp, returning;
    private float warningUntil, retaliateAt;
    public bool IsFlesh => false;

    private void Awake()
    {
        if (rider) body = rider.GetComponent<CharacterController>();
        indicator = GetComponentInChildren<Light>();
    }

    private bool HasRider()
    {
        if (!body || !body.enabled || !deck || rider.CurrentVelocity.y > .55f) return false;
        var bounds = deck.bounds;
        var feet = body.bounds;
        return Mathf.Abs(feet.min.y - bounds.max.y) < .18f
            && Mathf.Abs(feet.center.x - bounds.center.x) < bounds.extents.x + .12f
            && Mathf.Abs(feet.center.z - bounds.center.z) < bounds.extents.z + .12f;
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked || !lowerDock || !upperDock) return;
        float dt = Time.deltaTime;
        if (rotorLeft) rotorLeft.Rotate(Vector3.up, 750f * dt, Space.Self);
        if (rotorRight) rotorRight.Rotate(Vector3.up, -750f * dt, Space.Self);
        bool aboard = HasRider();
        bool powered = powerGate is not ILevelExitGate gate || gate.IsSatisfied;
        if (aboard && !returning && powered) goingUp = true;
        Vector3 target = goingUp ? upperDock.position : lowerDock.position;
        Vector3 delta = Vector3.MoveTowards(transform.position, target, speed * dt) - transform.position;
        if (aboard && delta.y >= 0f) body.Move(delta);
        transform.position += delta;
        Physics.SyncTransforms();
        if (aboard && delta.y < 0f) body.Move(delta);
        if (goingUp && !aboard && (transform.position - target).sqrMagnitude < .0001f)
        {
            goingUp = false;
            returning = true;
        }
        if (returning && (transform.position - lowerDock.position).sqrMagnitude < .0001f)
            returning = false;

        if (retaliateAt > 0f && Time.time >= retaliateAt && !aboard)
        {
            retaliateAt = 0f;
            if (rider && (rider.transform.position - transform.position).sqrMagnitude < 16f)
            {
                Vector3 origin = deck.bounds.center;
                if (Physics.Raycast(origin + Vector3.down * .1f,
                    (rider.transform.position + Vector3.up - origin).normalized, out var hit, 4f)
                    && hit.collider.GetComponentInParent<PlayerMovement>() == rider)
                    rider.GetComponent<PlayerCombatController>()?.ReceiveAttack(transform.position, 1);
            }
        }
        if (indicator) indicator.color = retaliateAt > 0f ? Color.red :
            Time.time < warningUntil ? new Color(1f,.7f,.16f) : new Color(.46f,.86f,1f);
    }

    public void OnPunched(Vector3 point, Vector3 normal, Vector3 direction, int tier, float strength)
    {
        if (Time.time < warningUntil) retaliateAt = Time.time + .55f;
        warningUntil = Time.time + 3f;
    }
}
