using UnityEngine;

namespace GameActions
{
    public enum PuzzleFeedback { Recharge, Lock, Release, Mechanism }
    /// <summary>机关自身持有音源；反馈只在已成功发生的状态变更后触发。</summary>
    [DisallowMultipleComponent]
    public sealed class PuzzlePresentation : MonoBehaviour
    {
        AudioSource source;
        LevelFeedbackProfile profile;
        SurfaceContactAudio contacts;
        float nextMechanism;
        public int SignalsPlayed { get; private set; }
        public static PuzzlePresentation Ensure(GameObject owner) => owner.GetComponent<PuzzlePresentation>() ?? owner.AddComponent<PuzzlePresentation>();
        void Awake()
        {
            profile = LevelFeedbackProfile.Load();
            var go = new GameObject("Puzzle Feedback Audio"); go.transform.SetParent(transform, false);
            source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = .8f; source.minDistance = 2; source.maxDistance = 30;
        }
        public static void Play(Component owner, PuzzleFeedback cue)
        {
            if (owner && !GameplayGate.IsBlocked) Ensure(owner.gameObject).Signal(cue);
        }
        public void Signal(PuzzleFeedback cue)
        {
            if (!profile || GameplayGate.IsBlocked) return;
            if (cue == PuzzleFeedback.Mechanism && Time.unscaledTime < nextMechanism) return;
            if (cue == PuzzleFeedback.Mechanism) nextMechanism = Time.unscaledTime + .15f;
            SignalsPlayed++;
            AudioClip clip = cue == PuzzleFeedback.Recharge ? profile.recharge : cue == PuzzleFeedback.Lock ? profile.momentumLock : cue == PuzzleFeedback.Release ? profile.momentumRelease : profile.mechanism;
            if (clip) source.PlayOneShot(clip, profile.puzzleVolume);
            // Keep locked-box vectors and button labels readable; particles are brief and small.
            if (cue == PuzzleFeedback.Recharge || cue == PuzzleFeedback.Release)
            {
                CombatEffects.Emit(CombatEffect.Resonance, transform.position, Vector3.up, profile.puzzleEffectScale);
                if (profile.advancedScreenEffects)
                    GauntletAdvancedPresentation.Wave(transform.position, cue == PuzzleFeedback.Recharge ? .55f : .8f, .35f, profile.puzzleDistortion, profile.puzzleChromatic);
            }
        }
        void OnCollisionEnter(Collision hit)
        {
            if (GameplayGate.IsBlocked || hit.relativeVelocity.sqrMagnitude < 4 || hit.contactCount == 0) return;
            if (!contacts) contacts = GetComponent<SurfaceContactAudio>() ?? gameObject.AddComponent<SurfaceContactAudio>();
            contacts.Contact(hit.collider, hit.GetContact(0).point, false);
        }
        void OnDisable() { if (source) source.Stop(); }
    }
}
