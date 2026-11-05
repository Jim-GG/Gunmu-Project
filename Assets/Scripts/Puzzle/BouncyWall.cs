using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 弹性墙（弹性反射）：
/// - 反弹规则（倍率 + 阈值）统一由这面墙决定，方块与格挡玩家的反弹结果完全一致。
/// - 移动中的 KineticBox 撞上会保留（或放大）动量并沿碰撞法线反射。
/// - 玩家按住右键面向墙面撞上会继承反射速度并回满蓄力；未格挡则是塑性碰撞（由玩家侧判定）。
/// </summary>
[RequireComponent(typeof(Collider))]
public class BouncyWall : MonoBehaviour
{
    [Header("反弹参数")]
    [SerializeField, Range(0.5f, 2.0f), InspectorName("反射速度倍率")] private float speedRetentionMultiplier = 1.0f;
    [SerializeField, Min(0f), InspectorName("最低入射速度（m/s）")] private float minImpactSpeed = 3.5f;
    [SerializeField, Min(0f), InspectorName("最低垂直入射分量（m/s）")] private float minIntoWallSpeed = 1f;

    [Header("外观颜色")]
    [SerializeField, InspectorName("表面颜色")] private Color surfaceColor = new Color(0.15f, 0.95f, 0.85f, 1f);
    [SerializeField, InspectorName("发光颜色")] private Color emissionColor = new Color(0.08f, 0.65f, 0.6f, 1f);
    [SerializeField, InspectorName("发光强度"), Min(0f)] private float emissionIntensity = 2f;

    [Header("事件")]
    [SerializeField, InspectorName("被弹射时")] private UnityEvent onReflected = new UnityEvent();

    private Renderer wallRenderer;
    private MaterialPropertyBlock propBlock;
    private Collider wallCollider;
    private Material runtimeEmissiveMaterial;

    public Collider WallCollider => wallCollider;

    /// <summary>最近一次反弹的撞击点。</summary>
    public Vector3 LastReflectedPoint { get; private set; }
    /// <summary>最近一次反弹的碰撞法线（世界空间）。</summary>
    public Vector3 LastReflectedNormal { get; private set; }
    /// <summary>最近一次反弹后施加给物体的速度。</summary>
    public Vector3 LastReflectedVelocity { get; private set; }
    /// <summary>最近一次反弹速度大小。</summary>
    public float LastReflectedSpeed => LastReflectedVelocity.magnitude;

    private void Awake()
    {
        wallCollider = GetComponent<Collider>();
        wallRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        EnsureEmissiveMaterial();
        ApplyVisuals();
    }

    /// <summary>
    /// URP Lit 需要材质上开启 _EMISSION 关键字才会真的发光，而 MaterialPropertyBlock 改不了关键字。
    /// 若场景里的材质没有开启，就在运行时复制一份并打开发光（随本组件一起销毁）。
    /// </summary>
    private void EnsureEmissiveMaterial()
    {
        if (wallRenderer == null)
            return;

        Material source = wallRenderer.sharedMaterial;
        if (source == null || !source.HasProperty("_EmissionColor"))
            return;

        if (source.IsKeywordEnabled("_EMISSION"))
            return;

        runtimeEmissiveMaterial = new Material(source) { name = source.name + "（弹性墙发光）" };
        runtimeEmissiveMaterial.EnableKeyword("_EMISSION");
        runtimeEmissiveMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        wallRenderer.sharedMaterial = runtimeEmissiveMaterial;
    }

    private void OnDestroy()
    {
        if (runtimeEmissiveMaterial != null)
            Destroy(runtimeEmissiveMaterial);
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
        propBlock.SetColor("_EmissionColor", new Color(emissionColor.r, emissionColor.g, emissionColor.b) * emissionIntensity);
        wallRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>
    /// 纯查询：这次碰撞是否算“弹性反弹”。算的话输出反射速度，倍率与阈值由本墙统一决定。
    /// 不产生任何副作用（特效/事件由真正应用反弹的一方调用 <see cref="NotifyReflected"/> 触发）。
    /// </summary>
    public bool TryReflect(Vector3 incomingVelocity, Vector3 normal, out Vector3 reflectedVelocity)
    {
        reflectedVelocity = Vector3.zero;

        Vector3 surfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : transform.forward;

        if (incomingVelocity.magnitude < minImpactSpeed)
            return false;

        if (-Vector3.Dot(incomingVelocity, surfaceNormal) < minIntoWallSpeed)
            return false;

        reflectedVelocity = ComputeReflectedVelocity(incomingVelocity, surfaceNormal);
        return true;
    }

    /// <summary>
    /// 记录一次真正的反弹并抛出 onReflected。只有真的被弹开的物体才应调用它。
    /// </summary>
    public void NotifyReflected(Vector3 point, Vector3 normal, Vector3 reflectedVelocity)
    {
        LastReflectedPoint = point;
        LastReflectedNormal = normal;
        LastReflectedVelocity = reflectedVelocity;
        onReflected.Invoke();
    }

    /// <summary>
    /// 纯数学反射：沿碰撞法线反射并按倍率保留速度。
    /// </summary>
    public Vector3 ComputeReflectedVelocity(Vector3 incomingVelocity, Vector3 hitNormal)
    {
        Vector3 normal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : transform.forward;
        return Vector3.Reflect(incomingVelocity, normal) * speedRetentionMultiplier;
    }
}
