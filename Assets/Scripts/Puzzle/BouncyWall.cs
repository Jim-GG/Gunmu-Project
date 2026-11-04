using UnityEngine;

/// <summary>
/// Elastic wall (弹性墙):
/// - Guarding Player hitting this wall retains speed, reflects purely by 3D reflection (Vector3.Reflect across surface normal), and refills gauntlet energy to 3/3.
/// - Unguarded Player hitting this wall experiences a plastic collision (stops and loses speed, no energy refill).
/// - Moving/Flying KineticBox hitting this wall retains speed and reflects elastically in 3D space.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BouncyWall : MonoBehaviour
{
    [Header("弹性反射参数")]
    [SerializeField, Range(0.5f, 2.0f), InspectorName("反射速度倍率")] private float speedRetentionMultiplier = 1.0f;

    [Header("外观颜色")]
    [SerializeField] private Color surfaceColor = new Color(0.15f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color emissionColor = new Color(0.08f, 0.65f, 0.6f, 1f);

    private Renderer wallRenderer;
    private MaterialPropertyBlock propBlock;
    private Collider wallCollider;

    public Collider WallCollider => wallCollider;

    private void Awake()
    {
        wallCollider = GetComponent<Collider>();
        wallRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        ApplyVisuals();
    }

    private void OnValidate()
    {
        if (wallRenderer == null)
            wallRenderer = GetComponent<Renderer>();
        if (propBlock == null)
            propBlock = new MaterialPropertyBlock();
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (wallRenderer == null)
            return;

        wallRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", surfaceColor);
        propBlock.SetColor("_Color", surfaceColor);
        propBlock.SetColor("_EmissionColor", emissionColor);
        wallRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>
    /// Computes the reflected 3D velocity vector for a guarding Player or flying KineticBox hitting this BouncyWall.
    /// Uses pure 3D reflection across the wall's surface normal.
    /// </summary>
    public Vector3 ComputeReflectedVelocity(Vector3 incomingVelocity, Vector3 hitNormal)
    {
        Vector3 normal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : transform.forward;
        return Vector3.Reflect(incomingVelocity, normal) * speedRetentionMultiplier;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.15f, 1f, 0.85f, 0.9f);
        Vector3 center = transform.position;
        Vector3 normal = transform.forward;
        Gizmos.DrawRay(center, normal * 3.5f);
        Gizmos.DrawWireSphere(center + normal * 3.5f, 0.25f);
    }
}
