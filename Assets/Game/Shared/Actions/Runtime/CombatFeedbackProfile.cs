using UnityEngine;
namespace GameActions
{
    [CreateAssetMenu(menuName="Game Actions/Combat impact feedback")]
    public sealed class CombatFeedbackProfile : ScriptableObject
    {
        [Header("出拳、格挡和肉体接触")]
        public AudioClip tierThreeSwing, guardImpact, concreteImpact;
        public AudioClip[] fleshHits = System.Array.Empty<AudioClip>(), bodyFalls = System.Array.Empty<AudioClip>();
        public AudioClip resonatorRoar;
        public AudioClip[] playerHurts=System.Array.Empty<AudioClip>();
        [Range(0,1)] public float playerHurtVolume=.28f, roarVolume=.55f;
        [Min(0)] public float hurtSoundInterval=.25f, roarInterval=2;
        [Min(0)] public float roarScreenRadius=18;
        [Range(0,1)] public float swingVolume=.65f, guardVolume=.65f, bodyVolume=.55f;
        [Min(0)] public float corpseMinimumSpeed=1.5f, corpseSoundInterval=.2f;
        [Header("轻拳 / 一级 / 二级 / 三级卡肉，使用真实秒数")]
        public Vector4 hitStopSeconds = new Vector4(.033f,.033f,.05f,.078f);
        [Header("非致命拳的击退距离")]
        public Vector3 knockbackMetres = new Vector3(.78f,.91f,1.67f);
        [Min(.01f)] public float knockbackSeconds=.2f;
        [Header("落石与水晶外形，按最大边长归一化")]
        public GameObject fracturedCrystal, squatCrystal;
        public Mesh[] rockMeshes = System.Array.Empty<Mesh>();
        public Quaternion[] rockFaceDown=System.Array.Empty<Quaternion>();
        [Range(0,20)] public float rockTiltDegrees=6;
        [Min(.1f)] public float rockDiameter=2.1f, crystalDiameter=1.3f, centralCrystalDiameter=3.1f;
        public Material fallingWarning;
        [Min(0)] public float warningPadding=.45f;
        public float WarningDiameter(bool central)
        {
            // Bound every possible rotation, rather than using a fixed two metre disc.
            float size=central?centralCrystalDiameter:rockDiameter;
            float bound=size;
            if(central&&squatCrystal){var filter=squatCrystal.GetComponentInChildren<MeshFilter>();if(filter&&filter.sharedMesh)bound=NormalizedSphere(filter.sharedMesh,squatCrystal.transform.localRotation,size);}
            else for(int i=0;i<rockMeshes.Length;i++)if(rockMeshes[i])bound=Mathf.Max(bound,NormalizedSphere(rockMeshes[i],i<rockFaceDown.Length?rockFaceDown[i]:Quaternion.identity,size));
            return (bound+warningPadding*2)/.91f; // visible ring is at 0.91 of the quad radius
        }
        static float NormalizedSphere(Mesh mesh,Quaternion rotation,float size)
        {
            var raw=mesh.bounds;var rotated=new Bounds(rotation*raw.center,Vector3.zero);
            for(int i=0;i<8;i++)rotated.Encapsulate(rotation*(raw.center+Vector3.Scale(raw.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return size*raw.size.magnitude/Mathf.Max(.001f,Mathf.Max(rotated.size.x,Mathf.Max(rotated.size.y,rotated.size.z)));
        }
        [Header("撞晶柱的短促电流覆盖")]
        public Material electricity;
        [Min(.05f)] public float electricitySeconds=.6f;
        [Min(.1f)] public float overloadWaveRadius=6;
        public static CombatFeedbackProfile Load()=>Resources.Load<CombatFeedbackProfile>("CombatFeedback");
        public float HitStop(int tier) => hitStopSeconds[Mathf.Clamp(tier,0,3)];
    }
}
