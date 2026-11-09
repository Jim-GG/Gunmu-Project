using UnityEngine;
using UnityEngine.Rendering;

/// <summary>拳套储能筒的纹理流光与贴近机械表面的细电弧。只读取玩法状态。</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(85)]
public sealed class PlayerGauntletChargeEffects : MonoBehaviour
{
    [Header("拳套本体蓄力反馈")]
    [SerializeField, ColorUsage(false, true), InspectorName("储能深紫色")]
    private Color storedColor = new Color(.32f, .09f, .7f);
    [SerializeField, ColorUsage(false, true), InspectorName("蓄力浅紫色")]
    private Color chargedColor = new Color(.83f, .46f, 1f);
    [SerializeField, Range(.1f, 3f), InspectorName("发光强度")]
    private float intensity = 1f;
    [SerializeField, InspectorName("细电弧材质（留空自动加载）")]
    private Material arcMaterial;

    [Header("电弧附着位置（相对 Gauntlet_Mechanism 骨骼）")]
    [SerializeField, InspectorName("起点")]
    private Vector3 arcStart = new Vector3(-.035f, .035f, .045f);
    [SerializeField, InspectorName("终点")]
    private Vector3 arcEnd = new Vector3(.035f, .19f, .045f);
    [SerializeField, Min(.001f), InspectorName("电弧离表面幅度")]
    private float arcSpread = .024f;
    [SerializeField, Range(.0005f, .015f), InspectorName("电弧线宽")]
    private float arcWidth = .003f;

    private static readonly int EnabledId = Shader.PropertyToID("_DiegeticCharge");
    private static readonly int StoredId = Shader.PropertyToID("_StoredEnergy");
    private static readonly int ProgressId = Shader.PropertyToID("_ChargeProgress");
    private static readonly int TierId = Shader.PropertyToID("_ChargeTier");
    private static readonly int ActiveId = Shader.PropertyToID("_ChargeActive");
    private static readonly int FlashId = Shader.PropertyToID("_ChargeFlash");
    private static readonly int StoredColorId = Shader.PropertyToID("_StoredColor");
    private static readonly int ChargedColorId = Shader.PropertyToID("_ChargedColor");
    private static readonly int IntensityId = Shader.PropertyToID("_ChargeFxIntensity");
    private static readonly int ClockId = Shader.PropertyToID("_ChargeFxTime");
    private readonly LineRenderer[] arcs = new LineRenderer[3];
    private readonly Vector3[] points = new Vector3[13];
    private MaterialPropertyBlock properties;
    private PlayerChargedPunch punch;
    private PlayerGauntletEquipment equipment;
    private SkinnedMeshRenderer mesh;
    private Transform model;
    private float clock;
    private float flash;
    private int previousEnergy;
    private int previousTier;
    private bool subscribed;
    [SerializeField, Range(0f, 1f), InspectorName("蓄力音量")]
    private float chargeVolume = .55f;
    private AudioSource chargeAudio;

    public void Initialize(PlayerChargedPunch source, SkinnedMeshRenderer renderer, Transform viewModel)
    {
        Unsubscribe();
        ClearArcs();
        punch = source;
        mesh = renderer;
        model = viewModel;
        equipment = GetComponent<PlayerGauntletEquipment>();
        properties = properties ?? new MaterialPropertyBlock();
        previousEnergy = punch != null ? punch.StoredEnergy : 0;
        previousTier = 0;
        Transform mechanism = null;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            if (t.name == "Gauntlet_Mechanism") { mechanism = t; break; }
        if (arcMaterial == null)
            arcMaterial = Resources.Load<Material>("DemoPresentation/GauntletChargeArc");
        if (mechanism != null && arcMaterial != null)
        {
            for (int i = 0; i < arcs.Length; i++)
            {
                var go = new GameObject("储能细电弧 " + (i + 1));
                go.layer = mesh != null ? mesh.gameObject.layer : model.gameObject.layer;
                go.transform.SetParent(mechanism, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = arcMaterial;
                line.useWorldSpace = false;
                line.positionCount = points.Length;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                line.widthCurve = new AnimationCurve(new Keyframe(0, .1f), new Keyframe(.18f, 1f),
                    new Keyframe(.75f, .75f), new Keyframe(1, .1f));
                line.enabled = false;
                arcs[i] = line;
            }
        }
        Subscribe();
        ApplyMaterial(0f, 0, false);
    }

    private void OnEnable() { Subscribe(); }
    private void Subscribe()
    {
        if (subscribed || punch == null) return;
        punch.ChargeStarted += PlayChargeAudio;
        punch.PunchReleased += OnPunch;
        punch.GuardSucceeded += OnGuard;
        punch.ChargeCanceled += OnCancel;
        subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!subscribed || punch == null) return;
        punch.ChargeStarted -= PlayChargeAudio;
        punch.PunchReleased -= OnPunch;
        punch.GuardSucceeded -= OnGuard;
        punch.ChargeCanceled -= OnCancel;
        subscribed = false;
    }
    private void OnPunch(int tier) { StopChargeAudio(); flash = .4f + tier * .2f; previousTier = 0; }
    private void OnGuard() { flash = .65f; }
    private void OnCancel() { StopChargeAudio(); flash = 0f; previousTier = 0; }

    private void PlayChargeAudio()
    {
        if (punch.StoredEnergy == 0 || GameplayGate.IsBlocked) return;
        if (!chargeAudio)
        {
            var clip = Resources.Load<AudioClip>("DemoPresentation/GauntletCharge");
            if (!clip) return;
            var emitter = new GameObject("拳套蓄力音效");
            emitter.transform.SetParent(transform, false);
            chargeAudio = emitter.AddComponent<AudioSource>();
            chargeAudio.playOnAwake = false;
            chargeAudio.spatialBlend = 0f;
            chargeAudio.clip = clip;
        }
        chargeAudio.Stop();
        chargeAudio.pitch = Mathf.Clamp(chargeAudio.clip.length / punch.TierThreeTime, .5f, 2f);
        chargeAudio.volume = chargeVolume;
        chargeAudio.Play();
    }

    private void StopChargeAudio()
    {
        if (chargeAudio && chargeAudio.isPlaying) chargeAudio.Stop();
    }

    private void LateUpdate()
    {
        if (punch == null || mesh == null || model == null) return;
        bool visible = model.gameObject.activeInHierarchy && (equipment == null || equipment.HasGauntlet);
        if (!visible || GameplayGate.IsBlocked || !punch.isActiveAndEnabled ||
            !punch.IsCharging || punch.IsChargeCapped)
            StopChargeAudio();
        if (!visible || GameplayGate.IsBlocked || !punch.isActiveAndEnabled)
        {
            flash = 0;
            previousTier = 0;
            previousEnergy = punch.StoredEnergy;
            ApplyMaterial(0f, 0, false);
            SetArcsVisible(false);
            return;
        }

        clock += Time.unscaledDeltaTime;
        int tier = punch.IsCharging ? punch.CurrentTier : 0;
        if (punch.StoredEnergy > previousEnergy) flash = Mathf.Max(flash, .55f);
        if (tier > previousTier) flash = .45f + tier * .13f;
        previousEnergy = punch.StoredEnergy;
        previousTier = tier;
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 3.5f);
        float progress = ChargeProgress();
        ApplyMaterial(progress, tier, punch.IsCharging);
        UpdateArcs(progress, tier);
    }

    private float ChargeProgress()
    {
        if (!punch.IsCharging) return 0f;
        float time = Mathf.Clamp(punch.ChargeTime, 0f, punch.ChargeCap);
        // 每档占相同视觉区间，实际所需蓄力时间仍然使用玩法自己的阈值。
        if (time <= punch.TierOneTime)
            return Mathf.Clamp01(time / Mathf.Max(.001f, punch.TierOneTime)) / 3f;
        if (time <= punch.TierTwoTime)
            return (1f + Mathf.InverseLerp(punch.TierOneTime, punch.TierTwoTime, time)) / 3f;
        return (2f + Mathf.InverseLerp(punch.TierTwoTime, punch.TierThreeTime, time)) / 3f;
    }

    private void ApplyMaterial(float progress, int tier, bool charging)
    {
        if (mesh == null || properties == null) return;
        mesh.GetPropertyBlock(properties, 0);
        properties.SetFloat(EnabledId, 1f);
        properties.SetFloat(StoredId, punch != null ? punch.StoredEnergy / 3f : 0f);
        properties.SetFloat(ProgressId, progress);
        properties.SetFloat(TierId, tier / 3f);
        properties.SetFloat(ActiveId, charging ? 1f : 0f);
        properties.SetFloat(FlashId, flash);
        properties.SetFloat(IntensityId, intensity);
        properties.SetFloat(ClockId, clock);
        properties.SetColor(StoredColorId, storedColor);
        properties.SetColor(ChargedColorId, chargedColor);
        mesh.SetPropertyBlock(properties, 0);
    }

    private void UpdateArcs(float progress, int tier)
    {
        float strength = Mathf.Max(progress, flash);
        bool active = strength > .025f && (punch.IsCharging || flash > .05f);
        Vector3 axis = (arcEnd - arcStart).normalized;
        Vector3 side = Vector3.Cross(axis, Vector3.forward).normalized;
        if (side.sqrMagnitude < .5f) side = Vector3.right;
        Vector3 surface = Vector3.Cross(axis, side);
        for (int i = 0; i < arcs.Length; i++)
        {
            var line = arcs[i];
            if (line == null) continue;
            // 高档位产生更密集的弧光，低档位只有一根短暂细弧，始终贴着器械。
            bool show = active && (i == 0 || tier >= i + 1 || flash > .7f);
            line.enabled = show;
            if (!show) continue;
            float flicker = .5f + .5f * Mathf.Sin(clock * (31f + i * 7f) + i * 1.7f);
            float spread = arcSpread * (.55f + progress * .45f);
            for (int n = 0; n < points.Length; n++)
            {
                float t = n / (float)(points.Length - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float noise = Mathf.PerlinNoise(n * 1.17f + i * 9.3f, clock * 16f) - .5f;
                points[n] = Vector3.Lerp(arcStart, arcEnd, t)
                    + side * (noise * spread + (i - 1) * spread * .38f) * envelope
                    + surface * (Mathf.Sin(t * 13f + clock * 9f + i * 2f) * spread * .25f * envelope);
            }
            line.SetPositions(points);
            line.widthMultiplier = arcWidth * (.65f + progress * .65f);
            Color color = Color.Lerp(storedColor, chargedColor, .3f + progress * .7f);
            color.a = (.12f + strength * .55f) * flicker;
            line.startColor = color;
            line.endColor = color;
        }
    }

    private void SetArcsVisible(bool visible)
    {
        foreach (var line in arcs) if (line != null) line.enabled = visible;
    }
    private void ClearArcs()
    {
        for (int i = 0; i < arcs.Length; i++)
        {
            if (arcs[i] != null) Destroy(arcs[i].gameObject);
            arcs[i] = null;
        }
    }
    private void OnDisable()
    {
        StopChargeAudio();
        Unsubscribe();
        flash = 0;
        SetArcsVisible(false);
        ApplyMaterial(0f, 0, false);
    }
    private void OnDestroy() { Unsubscribe(); ClearArcs(); }
}
