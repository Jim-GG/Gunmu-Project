using UnityEngine;

// CharacterController sweeps against walls while impact speed decays.
[RequireComponent(typeof(CharacterController))]
public class DemoEnemyKnockback : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float deceleration = 14f;
    [SerializeField, Min(0f)] private float chaseSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float stopDistance = 1.7f;
    [SerializeField, Min(0.1f)] private float detectionRange = 12f;
    private DemoEnemyAttack attack;
    private CharacterController controller;
    private DemoEnemyHealth health;
    private Vector3 impactVelocity;
    private float verticalSpeed;

    public bool IsBeingPushed => impactVelocity.sqrMagnitude > 0.01f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<DemoEnemyHealth>();
        attack = GetComponent<DemoEnemyAttack>();
    }

    public void Push(Vector3 direction, float speed)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
            return;
        impactVelocity = horizontalDirection.normalized * Mathf.Max(0f, speed);
        if (attack != null)
            attack.InterruptAttack();
    }

    private void Update()
    {
        if (!controller.enabled || (health != null && health.IsDefeated))
            return;
        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        verticalSpeed -= 20f * Time.deltaTime;
        Vector3 chaseVelocity = Vector3.zero;
        // This first-room prototype walks directly toward the target; walls still block it.
        // Stand still during windup/recovery so players can read and dodge attacks.
        if (attack != null && attack.enabled && !attack.IsBusy && !IsBeingPushed
            && attack.PlayerTarget != null && !attack.PlayerTarget.IsDefeated)
        {
            Vector3 toPlayer = Vector3.ProjectOnPlane(
                attack.PlayerTarget.transform.position - transform.position, Vector3.up);
            float distance = toPlayer.magnitude;
            if (distance > stopDistance && distance <= detectionRange)
            {
                chaseVelocity = toPlayer.normalized * chaseSpeed;
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            }
        }

        CollisionFlags flags = controller.Move(
            (chaseVelocity + impactVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            verticalSpeed = -2f;
        if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            verticalSpeed = 0f;
        impactVelocity = Vector3.MoveTowards(impactVelocity, Vector3.zero,
            deceleration * Time.deltaTime);
    }
}
