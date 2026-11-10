using FirstPerson;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameActions
{
    /// <summary>原版拳套的实际状态/接触驱动色散和热浪，不参与伤害或位移。</summary>
    [DisallowMultipleComponent]
    public sealed class GauntletAdvancedPresentation : MonoBehaviour
    {
        public LevelFeedbackProfile profile;
        GauntletCombat combat;
        GauntletPresentationSource state;
        Camera view;
        GameObject chargingHeat;
        HeatHazeController heat;
        Vector3 heatScale;
        bool subscribed, impactPlayed;
        public int WavesPlayed { get; private set; }
        public int ImpactHeatPlayed { get; private set; }
        public bool ChargingHeatActive => chargingHeat && chargingHeat.activeInHierarchy;
        public ChromaticWaveController LastWave { get; private set; }
        public HeatHazeController LastImpactHeat { get; private set; }

        public static GauntletAdvancedPresentation Ensure(GauntletCombat owner)
        {
            var c = owner.GetComponent<GauntletAdvancedPresentation>() ?? owner.gameObject.AddComponent<GauntletAdvancedPresentation>();
            c.Bind(owner); return c;
        }
        void Awake()
        {
            profile = LevelFeedbackProfile.Load(); state = GauntletPresentationSource.Ensure(gameObject);
            view = GetComponentInChildren<Camera>(true) ?? Camera.main;
            if (view) { var data = view.GetUniversalAdditionalCameraData(); data.requiresColorTexture = true; data.requiresDepthTexture = true; }
        }
        void Bind(GauntletCombat owner)
        {
            if (combat == owner && subscribed) return;
            Unsubscribe(); combat = owner;
            if (!combat || !isActiveAndEnabled) return;
            combat.AttackStarted += Attack; combat.ContactResolved += Contact; combat.AttackBlocked += Guard;
            subscribed = true;
        }
        bool Usable => profile && profile.advancedScreenEffects && state && state.IsUsable && !GameplayGate.IsBlocked;
        void Attack(int punch) { impactPlayed = false; StopHeat(); }
        void Contact(Collider target, Vector3 point, Vector3 normal)
        {
            if (!Usable || impactPlayed || !target || combat.ActivePunch < 3) return;
            impactPlayed = true;
            float strength = combat.ActivePunch == 4 ? 1 : .65f;
            LastWave = Wave(point, profile.heavyWaveRadius * strength, .45f, profile.heavyDistortion * strength, profile.heavyChromatic * strength, view);
            if (LastWave) WavesPlayed++;
            if (combat.ActivePunch != 4) return;
            var library = AdvancedCombatVFXLibrary.Load();
            if (!library || !library.heatBurst) return;
            var go = CombatEffects.Burst(library.heatBurst, point + normal * .03f, Vector3.up, profile.impactHeatScale, 1);
            LastImpactHeat = go ? go.GetComponent<HeatHazeController>() : null;
            if (LastImpactHeat)
            {
                LastImpactHeat.SetCamera(view); LastImpactHeat.loop = false; LastImpactHeat.duration = .8f;
                LastImpactHeat.distortionPixels = profile.impactHeatDistortion; LastImpactHeat.opacity = profile.impactHeatOpacity;
                ImpactHeatPlayed++;
            }
        }
        void Guard()
        {
            if (!Usable || combat.LastGuard != GuardKind.Perfect || !view) return;
            LastWave = Wave(view.transform.position + view.transform.forward * .9f, profile.perfectGuardRadius, .3f, profile.perfectGuardDistortion, profile.perfectGuardChromatic, view);
            if (LastWave) WavesPlayed++;
        }
        public static ChromaticWaveController Wave(Vector3 point, float radius, float duration, float distortion, float chromatic, Camera camera = null)
        {
            var library = AdvancedCombatVFXLibrary.Load(); if (!library || !library.chromaticSphere) return null;
            var go = CombatEffects.Burst(library.chromaticSphere, point, Vector3.up, 1, duration + .1f);
            var c = go ? go.GetComponent<ChromaticWaveController>() : null;
            if (c) { c.SetCamera(camera ? camera : Camera.main); c.PlayAt(point, radius, duration, distortion, chromatic); }
            return c;
        }
        void LateUpdate()
        {
            if (!Usable || !state.IsCharging || state.Seconds / Mathf.Max(.01f, state.FullDuration) < profile.chargingHeatStart)
            { StopHeat(); return; }
            if (!chargingHeat)
            {
                var library = AdvancedCombatVFXLibrary.Load(); if (!library || !library.heatLoop) return;
                chargingHeat = Instantiate(library.heatLoop, transform); heatScale = chargingHeat.transform.localScale;
                heat = chargingHeat.GetComponent<HeatHazeController>();
            }
            if (!heat) { StopHeat(); return; }
            chargingHeat.SetActive(true); heat.SetCamera(view); heat.loop = true;
            heat.distortionPixels = profile.chargingHeatDistortion; heat.opacity = profile.chargingHeatOpacity;
            chargingHeat.transform.localScale = heatScale * profile.chargingHeatScale;
            var anchor = state.WeaponAnchor;
            chargingHeat.transform.position = anchor ? anchor.position : view ? view.transform.position + view.transform.forward * .7f : transform.position;
        }
        void StopHeat() { if (chargingHeat) chargingHeat.SetActive(false); }
        void Unsubscribe()
        {
            if (combat && subscribed) { combat.AttackStarted -= Attack; combat.ContactResolved -= Contact; combat.AttackBlocked -= Guard; }
            subscribed = false;
        }
        void OnEnable() { if (combat) Bind(combat); }
        void OnDisable() { StopHeat(); Unsubscribe(); }
        void OnDestroy() { Unsubscribe(); }
    }
}
