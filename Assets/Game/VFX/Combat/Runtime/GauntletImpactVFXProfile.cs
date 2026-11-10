using UnityEngine;
namespace GameActions
{
    [CreateAssetMenu(menuName="Game Actions/Gauntlet impact VFX")]
    public sealed class GauntletImpactVFXProfile:ScriptableObject
    {
        public GameObject metalSpark,concreteDebris,bloodMist,crystalFragments;
        public GameObject[] concreteCracks;
        [Min(.01f)]public float sparkScale=1,concreteDebrisScale=.3f,bloodScale=.3f,crystalScale=.35f;
        [Tooltip("Relative to the 3.4 metre SlamImpact crater diameter.")]
        [Range(.01f,1)]public float concreteDecalScale=.2f;
        [Min(.1f)]public float referenceCraterDiameter=3.4f;
        [Range(0,.45f)]public float concreteDepth=.04f;
        [Min(.1f)]public float decalLifetime=18;
    }
}
