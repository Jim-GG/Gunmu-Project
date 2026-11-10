using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameActions
{
    public enum ScreenCue { LightHit, ChargeOneHit, ChargeTwoHit, ChargeThreeHit, Impact, Explosion, Resonance, Hurt, PerfectGuard, PillarShock, Custom, ResonatorRoar }

    [Serializable]
    public sealed class ScreenEffectPreset
    {
        public ScreenCue cue;
        [Range(0, 1)] public float radial = .3f;
        [Range(0, .25f)] public float strength = .08f;
        [Min(0)] public float attack = .025f, release = .18f;
        [Range(0, 12)] public float chromaticPixels;
        [Range(0, 1)] public float purpleNoise;
        [Min(0)] public float zoomMetres = .009f;
        [Range(1, 45)] public float zoomFrequency = 24;
        [Range(0, 3)] public float hurtDegrees = .85f;
    }

    [CreateAssetMenu(menuName = "Game Actions/Screen effects profile")]
    public sealed class ScreenEffectsProfile : ScriptableObject
    {
        public Material material;
        public FullScreenPassRendererFeature[] rendererFeatures = Array.Empty<FullScreenPassRendererFeature>();
        public ScreenEffectPreset[] signals = Array.Empty<ScreenEffectPreset>();
        [Header("径向模糊")]
        public bool effectsEnabled = true;
        public enum SampleQuality { Eight = 8, Twelve = 12, Sixteen = 16 }
        public SampleQuality quality = SampleQuality.Twelve;
        [Range(0, .5f)] public float innerRadius = .06f;
        [Range(.1f, 1.5f)] public float outerRadius = .65f;
        public float speedStart = 10, speedFull = 28;
        [Range(0, 1)] public float speedIntensity = .75f;
        [Range(0, .25f)] public float speedStrength = .14f;
        [Header("蓄力暗角 / 危急生命 / 共鸣噪波")]
        [Range(0, .4f)] public float chargeVignette = .12f;
        [Range(0, 1)] public float lowHealthThreshold = .3f;
        [Range(0, .7f)] public float redEdgeOpacity = .36f;
        public Color resonanceColor = new Color(.4f, .04f, .58f, 1);
        public Color damageColor = new Color(.6f, .015f, .025f, 1);
        public Vector2 noiseFlow = new Vector2(.11f, .08f);
        public float noiseScale = 15;
        public ScreenEffectPreset Find(ScreenCue cue) => Array.Find(signals, p => p != null && p.cue == cue);
        public static ScreenEffectsProfile Load() => Resources.Load<ScreenEffectsProfile>("CombatScreenEffects");
    }
}
