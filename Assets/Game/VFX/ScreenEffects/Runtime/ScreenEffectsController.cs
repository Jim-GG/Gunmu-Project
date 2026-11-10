using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FirstPerson;

namespace GameActions
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class ScreenEffectsController : MonoBehaviour
    {
        public ScreenEffectsProfile profile;
        public GameObject owner;
        public bool automaticInputs = true;
        [Tooltip("预览零强度透传；正常游戏关闭以跳过无效果时的颜色拷贝。")]
        public bool forcePassForPreview;
        public float RadialIntensity { get; private set; }
        public float ChromaticPixels { get; private set; }
        public float PurpleNoise { get; private set; }
        public float Vignette { get; private set; }
        public float RedEdge { get; private set; }
        public int SignalsReceived { get; private set; }
        public Material RuntimeMaterial => material;
        public Vector2 Centre { get; private set; } = new Vector2(.5f, .5f);
        public Vector3 LastCameraOffset { get; private set; }
        public Vector3 LastCameraAngles { get; private set; }
        struct Pulse { public ScreenEffectPreset preset; public Vector2 centre; public float age, scale, yaw; }
        readonly Pulse[] pulses = new Pulse[16]; int cursor;
        Camera view; Material material; GauntletCombat combat; FirstPersonMotor motor; PlayerMovement puzzleMotor;
        FirstPersonVitals vitals; PlayerCombatController puzzleVitals;
        float speedBlend, clock; Vector3 cameraOffset, cameraAngles, restPosition; Quaternion restRotation; bool applied;
        static readonly int EnabledId = Shader.PropertyToID("_Enabled"), IntensityId = Shader.PropertyToID("_Intensity"), StrengthId = Shader.PropertyToID("_Strength"), CenterId = Shader.PropertyToID("_Center"),
            InnerId = Shader.PropertyToID("_InnerRadius"), OuterId = Shader.PropertyToID("_OuterRadius"), ChromaticId = Shader.PropertyToID("_ChromaticPixels"), VignetteId = Shader.PropertyToID("_Vignette"),
            NoiseId = Shader.PropertyToID("_PurpleNoise"), RedId = Shader.PropertyToID("_RedEdge"), ClockId = Shader.PropertyToID("_EffectTime"), NoiseColorId = Shader.PropertyToID("_NoiseColor"),
            RedColorId = Shader.PropertyToID("_DamageColor"), FlowId = Shader.PropertyToID("_NoiseFlow"), NoiseScaleId = Shader.PropertyToID("_NoiseScale");

        public static ScreenEffectsController Ensure(GameObject actor)
        {
            var camera = actor.GetComponentInChildren<Camera>(true); if (!camera) return null;
            var c = camera.GetComponent<ScreenEffectsController>() ?? camera.gameObject.AddComponent<ScreenEffectsController>();
            c.Bind(actor); if (!camera.GetComponent<RadialBlurController>()) camera.gameObject.AddComponent<RadialBlurController>(); return c;
        }
        void Awake()
        {
            view = GetComponent<Camera>(); if (!profile) profile = ScreenEffectsProfile.Load();
            var actor=GetComponentInParent<GauntletCombat>();Bind(owner?owner:actor?actor.gameObject:transform.root.gameObject);
        }
        public void Bind(GameObject actor)
        {
            owner = actor; combat = actor.GetComponent<GauntletCombat>(); motor = actor.GetComponent<FirstPersonMotor>(); puzzleMotor = actor.GetComponent<PlayerMovement>();
            vitals = actor.GetComponent<FirstPersonVitals>(); puzzleVitals = actor.GetComponent<PlayerCombatController>();
            if (!material && profile && profile.material) { material = new Material(profile.material) { name = "Camera-owned combat screen effects", hideFlags = HideFlags.HideAndDontSave }; }
        }
        void OnEnable() { ScreenEffectSignals.Raised += Receive; RenderPipelineManager.beginCameraRendering += BeginRender; RenderPipelineManager.endCameraRendering += EndRender; }
        void Receive(ScreenEffectSignal signal)
        {
            if (!isActiveAndEnabled || !profile || !profile.effectsEnabled || GameplayGate.IsBlocked || signal.receiver && signal.receiver != owner) return;
            float attenuation = signal.receiver || signal.radius <= 0 ? 1 : Mathf.Clamp01(1 - Vector3.Distance(view.transform.position, signal.position) / signal.radius);
            if (attenuation <= .001f) return;
            var preset = profile.Find(signal.cue); if (preset == null) return;
            SignalsReceived++; Play(preset, Project(signal.position), signal.scale * attenuation, signal.position);
        }
        public Vector2 Project(Vector3 point)
        { var p = view.WorldToViewportPoint(point); return p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1 ? new Vector2(.5f, .5f) : new Vector2(p.x, p.y); }
        void Play(ScreenEffectPreset preset, Vector2 centre, float scale, Vector3 source)
        {
            var direction = view.transform.InverseTransformDirection(source - view.transform.position);
            float yaw = Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? -Mathf.Sign(direction.x) : 0;
            pulses[cursor++ % pulses.Length] = new Pulse { preset = preset, centre = centre, age = 0, scale = Mathf.Clamp(scale, 0, 1.5f), yaw = yaw };
        }
        public void PlayRadial(Vector2 centre, float strength = .1f)
        {
            // Debug requests reuse a cached preset; normal gameplay uses the asset's presets.
            if (debugPulse == null) debugPulse = new ScreenEffectPreset { cue = ScreenCue.Custom, radial = 1, attack = .04f, release = .22f, zoomMetres = 0 };
            debugPulse.strength = Mathf.Clamp(strength, 0, .25f);
            Play(debugPulse, new Vector2(Mathf.Clamp01(centre.x), Mathf.Clamp01(centre.y)), 1, view.transform.position);
        }
        ScreenEffectPreset debugPulse;
        public void Stop() { for (int i = 0; i < pulses.Length; i++) pulses[i] = default; speedBlend = 0; Tick(0, 0, 0, 1); }
        void LateUpdate()
        {
            if (!owner) return;
            float speed = automaticInputs ? motor ? motor.Velocity.magnitude : puzzleMotor ? puzzleMotor.CurrentVelocity.magnitude : 0 : 0;
            float charge = automaticInputs && combat && (combat.State == GauntletCombat.Phase.Charging || combat.State == GauntletCombat.Phase.Full || combat.State == GauntletCombat.Phase.ReleasePending) ? combat.ChargeTier / 3f : 0;
            float health = !automaticInputs ? 1 : puzzleVitals ? puzzleVitals.HealthFraction : vitals ? vitals.Health / Mathf.Max(1, vitals.maxHealth) : 1;
            Tick(Time.unscaledDeltaTime, speed, charge, health);
        }
        public void Tick(float dt, float speed, float charge, float health)
        {
            if (!material || !profile) return;
            bool blocked = GameplayGate.IsBlocked; dt = blocked ? 0 : Mathf.Max(0, dt); clock += dt;
            speedBlend = Mathf.MoveTowards(speedBlend, blocked ? 0 : Mathf.InverseLerp(profile.speedStart, Mathf.Max(profile.speedStart + .01f, profile.speedFull), speed), Mathf.Max(dt, .016f) * 5);
            float radial = speedBlend * profile.speedIntensity, strength = profile.speedStrength, chromatic = 0, noise = 0;
            Centre = new Vector2(.5f, .5f); cameraOffset = cameraAngles = Vector3.zero;
            for (int i = 0; i < pulses.Length; i++)
            {
                var p = pulses[i]; if (p.preset == null) continue; var e = p.preset;
                p.age += dt; pulses[i] = p; float length = e.attack + e.release;
                if (p.age >= length) { pulses[i] = default; continue; }
                float envelope = (p.age < e.attack ? p.age / Mathf.Max(.001f, e.attack) : 1 - (p.age - e.attack) / Mathf.Max(.001f, e.release)) * p.scale;
                envelope = Mathf.Clamp01(envelope);
                if (e.radial * envelope > radial) { radial = e.radial * envelope; strength = e.strength; Centre = p.centre; }
                chromatic = Mathf.Max(chromatic, e.chromaticPixels * envelope); noise = Mathf.Max(noise, e.purpleNoise * envelope);
                cameraOffset.z += Mathf.Sin(p.age * e.zoomFrequency * Mathf.PI * 2) * e.zoomMetres * envelope;
                if (e.cue == ScreenCue.Hurt) cameraAngles += new Vector3(-e.hurtDegrees, p.yaw * e.hurtDegrees * .8f, 0) * Mathf.Sin(Mathf.Clamp01(p.age / Mathf.Max(.01f, length)) * Mathf.PI) * p.scale;
            }
            RadialIntensity = blocked ? 0 : Mathf.Clamp01(radial); ChromaticPixels = blocked ? 0 : Mathf.Clamp(chromatic, 0, 10); PurpleNoise = blocked ? 0 : Mathf.Clamp01(noise);
            Vignette = Mathf.MoveTowards(Vignette, blocked ? 0 : Mathf.Clamp01(charge) * profile.chargeVignette, Mathf.Max(dt, .016f) * .5f);
            RedEdge = blocked ? 0 : (1 - Mathf.Clamp01(health / Mathf.Max(.01f, profile.lowHealthThreshold))) * profile.redEdgeOpacity;
            if (blocked) cameraOffset = cameraAngles = Vector3.zero;
            cameraOffset = Vector3.ClampMagnitude(cameraOffset, .035f); cameraAngles = Vector3.ClampMagnitude(cameraAngles, 2);
            material.SetFloat(EnabledId, profile.effectsEnabled ? 1 : 0); material.SetFloat(IntensityId, RadialIntensity); material.SetFloat(StrengthId, strength);
            material.SetVector(CenterId, new Vector4(Centre.x, Centre.y, 0, 0)); material.SetFloat(InnerId, profile.innerRadius); material.SetFloat(OuterId, Mathf.Max(profile.innerRadius + .01f, profile.outerRadius));
            material.SetFloat(ChromaticId, ChromaticPixels); material.SetFloat(VignetteId, Vignette); material.SetFloat(NoiseId, PurpleNoise); material.SetFloat(RedId, RedEdge); material.SetFloat(ClockId, clock);
            material.SetColor(NoiseColorId, profile.resonanceColor); material.SetColor(RedColorId, profile.damageColor); material.SetVector(FlowId, new Vector4(profile.noiseFlow.x, profile.noiseFlow.y, 0, 0)); material.SetFloat(NoiseScaleId, profile.noiseScale);
            CoreUtils.SetKeyword(material, "_RADIAL_8", profile.quality == ScreenEffectsProfile.SampleQuality.Eight); CoreUtils.SetKeyword(material, "_RADIAL_16", profile.quality == ScreenEffectsProfile.SampleQuality.Sixteen);
        }
        bool HasEffect => profile && profile.effectsEnabled && (RadialIntensity > .0001f || ChromaticPixels > .0001f || PurpleNoise > .0001f || Vignette > .0001f || RedEdge > .0001f);
        void BeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != view || !profile) return;
            foreach (var feature in profile.rendererFeatures) if (feature) feature.passMaterial = HasEffect || forcePassForPreview ? material : null;
            if (applied || !profile.effectsEnabled) return;
            restPosition = view.transform.localPosition; restRotation = view.transform.localRotation;
            LastCameraOffset = cameraOffset; LastCameraAngles = cameraAngles;
            view.transform.localPosition += view.transform.localRotation * cameraOffset; view.transform.localRotation *= Quaternion.Euler(cameraAngles); applied = true;
        }
        void EndRender(ScriptableRenderContext context, Camera camera) { if (camera == view) Restore(); }
        void Restore()
        {
            if (applied && view) view.transform.SetLocalPositionAndRotation(restPosition, restRotation); applied = false;
            if (profile) foreach (var feature in profile.rendererFeatures) if (feature && feature.passMaterial == material) feature.passMaterial = null;
        }
        void OnDisable() { ScreenEffectSignals.Raised -= Receive; RenderPipelineManager.beginCameraRendering -= BeginRender; RenderPipelineManager.endCameraRendering -= EndRender; Restore(); Stop(); }
        void OnDestroy() { Restore(); if (material) Destroy(material); }
        [ContextMenu("预览：中心径向冲击")] void Preview() => PlayRadial(new Vector2(.5f, .5f));
        [ContextMenu("预览：附近共鸣爆炸")] void PreviewResonance() => ScreenEffectSignals.Emit(ScreenCue.Resonance, view.transform.position + view.transform.forward * 3, receiver: owner);
        [ContextMenu("停止瞬时效果")] void PreviewStop() => Stop();
    }
}
