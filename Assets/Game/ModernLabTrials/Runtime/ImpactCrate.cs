using GameActions;
using UnityEngine;

namespace ModernLabTrials
{
    // A punchable / pushable lab crate. Implements IActionTarget so the existing
    // gauntlet action hit windows can launch it without a special-case input path.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class ImpactCrate : MonoBehaviour, IActionTarget
    {
        [SerializeField] private float punchMultiplier = 3.2f;
        [SerializeField] private float minPunchSpeed = 5f;
        [SerializeField] private float maxSpeed = 20f;
        [SerializeField] private float resetBelowY = -8f;

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;

        public bool ActionTargetAlive => isActiveAndEnabled;
        public Transform ActionRoot => transform;
        public Vector3 ActionAimPoint => transform.position + Vector3.up * .5f;
        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = 7f;
            body.linearDamping = .35f;
            body.angularDamping = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        public void ReceiveActionHit(float damage, Vector3 source, float impulse)
        {
            if (!body) return;
            Vector3 direction = transform.position - source;
            direction.y = .12f;
            if (direction.sqrMagnitude < .001f) direction = transform.forward;
            float speed = Mathf.Clamp(impulse * punchMultiplier, minPunchSpeed, maxSpeed);
            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity + direction.normalized * speed, maxSpeed);
        }

        private void FixedUpdate()
        {
            if (transform.position.y < resetBelowY || transform.position.sqrMagnitude > 90000f)
                ResetCrate();
        }

        public void ResetCrate()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = startPosition;
            body.rotation = startRotation;
        }

        public void SetStart(Vector3 position, Quaternion rotation)
        {
            startPosition = position;
            startRotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
