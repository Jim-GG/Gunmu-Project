using UnityEngine;

/// <summary>Presentation only. DashCrystal owns collection, energy and cooldown.</summary>
[DisallowMultipleComponent]
public sealed class FloatingChargeCrystal : MonoBehaviour
{
    [Header("悬浮外观")]
    [SerializeField, Min(0f)] private float floatHeight = .10f;
    [SerializeField, Min(0f)] private float floatSpeed = 1.3f;
    [SerializeField] private float rotateSpeed = 24f;
    [SerializeField, Range(0f, .2f)] private float glimmerStrength = .055f;

    private static readonly int PulseId = Shader.PropertyToID("_PickupPulse");
    private Renderer[] surfaces;
    private MaterialPropertyBlock properties;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool initialized;
    private bool available = true;
    private float clock;

    public bool IsAvailable => available;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        surfaces = GetComponentsInChildren<Renderer>(true);
        properties = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!available) return;
        clock += Time.deltaTime;
        // Move the mesh only: the gameplay trigger remains at its authored position.
        transform.localPosition = restPosition + Vector3.up * (Mathf.Sin(clock * floatSpeed) * floatHeight);
        transform.localRotation = restRotation * Quaternion.Euler(0f, clock * rotateSpeed, 0f);
        float glimmer = glimmerStrength * (.5f + .5f * Mathf.Sin(clock * 2.2f));
        foreach (var surface in surfaces)
        {
            if (surface == null) continue;
            surface.GetPropertyBlock(properties);
            properties.SetFloat(PulseId, glimmer);
            surface.SetPropertyBlock(properties);
        }
    }

    public void SetAvailable(bool value)
    {
        Initialize();
        available = value;
        foreach (var surface in surfaces)
            if (surface != null) surface.enabled = value;
    }

    public void ResetPose()
    {
        Initialize();
        clock = 0f;
        transform.SetLocalPositionAndRotation(restPosition, restRotation);
    }
}
