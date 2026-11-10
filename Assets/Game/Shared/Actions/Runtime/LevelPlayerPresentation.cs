using UnityEngine;
using UDK7M;

namespace GameActions
{
    /// <summary>把现有关卡玩法接到共享的声音、材质命中与拳套特效；不发送伤害或物理命令。</summary>
    [DisallowMultipleComponent]
    public sealed class LevelPlayerPresentation : MonoBehaviour
    {
        public LevelFeedbackProfile profile;
        public GauntletSurfaceVFX surfaces;
        public SurfaceContactAudio contacts;
        public GauntletChargeAudio chargeAudio;
        public WeaponEnergyVFXController energy;
        ObjectAudio sounds;
        GauntletPresentationSource state;
        DashWindConeController wind;
        Camera view;
        AudioSource wounded;
        public int SwingsPlayed { get; private set; }
        public int GuardsPlayed { get; private set; }
        public static LevelPlayerPresentation Ensure(GameObject actor) => actor.GetComponent<LevelPlayerPresentation>() ?? actor.AddComponent<LevelPlayerPresentation>();
        void Awake()
        {
            if (!profile) profile = LevelFeedbackProfile.Load();
            state = GauntletPresentationSource.Ensure(gameObject);
            view = GetComponentInChildren<Camera>();
            surfaces = GetComponent<GauntletSurfaceVFX>() ?? gameObject.AddComponent<GauntletSurfaceVFX>();
            contacts = GetComponent<SurfaceContactAudio>() ?? gameObject.AddComponent<SurfaceContactAudio>();
            sounds = GetComponent<ObjectAudio>() ?? gameObject.AddComponent<ObjectAudio>();
            if (profile) sounds.sounds = profile.actorSounds;
            chargeAudio = GetComponent<GauntletChargeAudio>() ?? gameObject.AddComponent<GauntletChargeAudio>();
            chargeAudio.EnsureSources();
            if (profile) { chargeAudio.stages.clip = profile.chargeStages; chargeAudio.fullLoop.clip = profile.fullChargeLoop; }
            energy = WeaponEnergyVFXController.Ensure(GetComponent<PlayerChargedPunch>());
            if (!GetComponent<PlayerObjectAudio>()) gameObject.AddComponent<PlayerObjectAudio>();
            var source = new GameObject("Player Wound Audio"); source.transform.SetParent(transform, false);
            wounded = source.AddComponent<AudioSource>(); wounded.playOnAwake = false; wounded.spatialBlend = 0;
        }
        public void Swing(int tier)
        {
            if (GameplayGate.IsBlocked || !state.IsUsable) return;
            SwingsPlayed++; if(!GetComponent<FirstPerson.GauntletCombat>())sounds.Play(UDK7MSound.GauntletSwing, transform.position);
            var library = AdvancedCombatVFXLibrary.Load();
            if (tier <= 0 || !library || !library.windCone || !view) return;
            if (!wind)
            {
                var go = Instantiate(library.windCone, transform);
                wind = go.GetComponent<DashWindConeController>();
                if (!wind) { Destroy(go); return; }
            }
            wind.PlayDash(Anchor(), view.transform.forward, .65f + tier * .15f);
            wind.AlignTip(Anchor(), view.transform.forward, .65f + tier * .15f);
        }
        Vector3 Anchor() => state.WeaponAnchor ? state.WeaponAnchor.position : view.transform.position + view.transform.forward * .7f;
        void LateUpdate()
        {
            if (GameplayGate.IsBlocked || !state.IsUsable)
            {
                if (wind) wind.gameObject.SetActive(false);
                return;
            }
            if (wind && wind.gameObject.activeSelf && view) wind.AlignTip(Anchor(), view.transform.forward, wind.transform.localScale.x);
        }
        public void Contact(Collider collider, Vector3 point, Vector3 normal)
        {
            if (GameplayGate.IsBlocked || !collider) return;
            surfaces.Contact(collider, point, normal);
            contacts.Contact(collider, point, true);
        }
        public void Guard(bool success, Vector3 point, Vector3 normal)
        {
            if (GameplayGate.IsBlocked) return;
            if (success)
            {
                GuardsPlayed++; if(!GetComponent<FirstPerson.GauntletCombat>())sounds.Play(UDK7MSound.ParryImpact, point);
                CombatEffects.Emit(CombatEffect.Sparks, point, normal, .65f);
            }
            else if (profile && profile.wounded) wounded.PlayOneShot(profile.wounded, .45f);
        }
        void OnDisable()
        {
            if (chargeAudio) chargeAudio.Stop();
            if (energy) energy.SetEffectEnabled(false);
            if (wind) wind.gameObject.SetActive(false);
            if (wounded) wounded.Stop();
        }
        void OnEnable() { if (energy) energy.SetEffectEnabled(true); }
    }
}
