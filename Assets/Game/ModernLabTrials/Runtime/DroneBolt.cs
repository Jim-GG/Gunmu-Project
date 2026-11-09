using FirstPerson;
using UnityEngine;

namespace ModernLabTrials
{
    // Slow, telegraphed plasma bolt fired by the MCU-7A drone. A swept sphere
    // cast is used instead of a trigger so a 16 m/s bolt cannot tunnel through
    // the player or the lab walls at low frame rates.
    public sealed class DroneBolt : MonoBehaviour
    {
        [SerializeField] private float damage = 10f;
        [SerializeField] private float speed = 16f;
        [SerializeField] private float radius = .16f;
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private float gravity = 1.2f;

        private Vector3 velocity;
        private float age;
        private GameObject owner;
        private Transform glow;

        public float Damage => damage;
        public GameObject Owner => owner;

        public void Init(GameObject source, Vector3 direction, float boltSpeed, float boltDamage)
        {
            owner = source;
            if (boltSpeed > .01f) speed = boltSpeed;
            if (boltDamage > 0f) damage = boltDamage;
            Vector3 forward = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            velocity = forward * speed;
            transform.rotation = Quaternion.LookRotation(forward);
            BuildVisual();
        }

        private void BuildVisual()
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Bolt Core";
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * .16f;
            Collider collider = core.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            MeshRenderer renderer = core.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                Material material = new Material(shader) { name = "DroneBolt" };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, .42f, .22f, 1f));
                if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(1f, .42f, .22f, 1f));
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            GameObject lightObject = new GameObject("Bolt Glow");
            lightObject.transform.SetParent(transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .45f, .24f);
            light.range = 3.4f;
            light.intensity = 2.1f;
            light.shadows = LightShadows.None;
            glow = lightObject.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (age > lifetime)
            {
                Destroy(gameObject);
                return;
            }
            velocity += Vector3.down * gravity * dt;
            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance < .0001f) return;
            Vector3 direction = step / distance;

            if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, distance,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                bool ownDrone = owner != null && hit.collider.transform.IsChildOf(owner.transform);
                if (ownDrone)
                {
                    transform.position += step;
                }
                else
                {
                    FirstPersonVitals vitals = hit.collider.GetComponentInParent<FirstPersonVitals>();
                    if (vitals != null && vitals.Health > 0f) vitals.ReceiveDamage(damage);
                    transform.position = hit.point;
                    Destroy(gameObject);
                }
                return;
            }
            transform.position += step;
        }

        private void LateUpdate()
        {
            if (glow != null) glow.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 24f) * .12f);
        }
    }
}
