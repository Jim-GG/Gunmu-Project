using UnityEngine;

namespace GameActions
{
    [CreateAssetMenu(menuName = "Game Actions/Level feedback profile")]
    public sealed class LevelFeedbackProfile : ScriptableObject
    {
        public ObjectSound[] actorSounds = System.Array.Empty<ObjectSound>();
        public AudioClip chargeStages, fullChargeLoop, wounded, recharge, momentumLock, momentumRelease, mechanism;
        [Range(0, 1)] public float puzzleVolume = .35f;
        [Range(0, 1)] public float puzzleEffectScale = .22f;
        public static LevelFeedbackProfile Load() => Resources.Load<LevelFeedbackProfile>("LevelFeedback");
    }
}
