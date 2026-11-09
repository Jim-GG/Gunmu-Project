using Resonator.AI;
using UnityEngine;

[RequireComponent(typeof(KineticBox))]
public sealed class ModernCargoMotion : MonoBehaviour
{
    public bool damageEnemies;
    public Transform feedDirection;
    [Min(0f)] public float feedSpeed = 3.2f;
    [Min(.1f)] public float restartDelay = 2f;
    public ModernImpactMechanism stopFeedingAfter;
    private KineticBox box;
    private float feedAt;
    private float lastDamageAt = -10f;

    private void Start()
    {
        box = GetComponent<KineticBox>();
        feedAt = Time.time + 1f;
    }

    private void FixedUpdate()
    {
        if (!feedDirection || GameplayGate.IsBlocked || box.IsMomentumLocked || (stopFeedingAfter && stopFeedingAfter.IsSatisfied)) return;
        if (Time.time < feedAt) return;
        if (box.Body.linearVelocity.sqrMagnitude > .25f)
        {
            feedAt = Time.time + restartDelay;
            return;
        }
        box.ResetBox();
        box.ApplyFreeImpulse(feedDirection.forward, feedSpeed);
        feedAt = Time.time + restartDelay;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!damageEnemies || !box || box.IsMomentumLocked || Time.time < lastDamageAt + .25f) return;
        var enemy = collision.collider.GetComponentInParent<ResonatorEnemy>();
        float speed = Mathf.Max(collision.relativeVelocity.magnitude, box.PreImpactVelocity.magnitude);
        if (!enemy || speed < 5f) return;
        lastDamageAt = Time.time;
        enemy.TakeDamage(Mathf.Clamp(speed * 5f, 25f, 75f), transform.position, speed);
    }
}
