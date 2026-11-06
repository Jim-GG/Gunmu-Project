using UnityEngine;
namespace Resonator.AI {
    [CreateAssetMenu(menuName="Resonator/Motion Track")]
    public sealed class ResonatorMotionTrack : ScriptableObject
    {
        public AnimationClip clip;
        public float framesPerSecond=30, sampleRate=30;
        public Vector3 referenceHip;
        public Vector3[] trajectory;
        public float averageSpeed;
        public float Duration => clip ? clip.length : 0;
        public Vector3 Sample(float seconds)
        {
            if(trajectory==null||trajectory.Length==0)return Vector3.zero;
            float f=Mathf.Clamp(seconds*sampleRate,0,trajectory.Length-1);
            int i=Mathf.FloorToInt(f);return Vector3.Lerp(trajectory[i],trajectory[Mathf.Min(i+1,trajectory.Length-1)],f-i);
        }
    }

}
