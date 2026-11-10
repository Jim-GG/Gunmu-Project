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
        [Header("玩法中的色散与热浪")]
        public bool advancedScreenEffects = true;
        [Range(0, 1)] public float chargingHeatStart = .65f;
        [Min(.01f)] public float chargingHeatScale = .22f;
        [Min(0)] public float chargingHeatDistortion = 8;
        [Range(0, 1)] public float chargingHeatOpacity = .75f;
        [Min(.01f)] public float heavyWaveRadius = 1.2f;
        [Min(0)] public float heavyDistortion = 6, heavyChromatic = 2;
        [Min(.01f)] public float impactHeatScale = .5f;
        [Min(0)] public float impactHeatDistortion = 6;
        [Range(0, 1)] public float impactHeatOpacity = .7f;
        [Min(.01f)] public float perfectGuardRadius = .55f;
        [Min(0)] public float perfectGuardDistortion = 6, perfectGuardChromatic = 3;
        [Min(0)] public float puzzleDistortion = 2, puzzleChromatic = .8f;
        public static LevelFeedbackProfile Load() => Resources.Load<LevelFeedbackProfile>("LevelFeedback");
    }
}
