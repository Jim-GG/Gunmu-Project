using System.Collections.Generic;
using UnityEngine;
namespace CrystalParkour
{
    public enum WindKind { GravityRelief, HorizontalAirflow }
    public enum WindShape { Box, Cylinder }
    public struct WindSample
    {
        public float gravityScale,response,enterBlend,exitBlend,strength;
        public Vector3 drift;public int volumes;
    }
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WindVolume:MonoBehaviour
    {
        static readonly List<WindVolume> active=new List<WindVolume>();
        public WindKind kind;
        public WindShape shape;
        public bool windEnabled=true;
        [Header("Vertical wind: remaining gravity, never a levitation force")]
        [Range(.2f,1)]public float gravityScale=.42f;
        [Header("Horizontal wind: independent of player steering")]
        public Vector3 direction=Vector3.forward;
        public bool directionIsLocal;
        [Min(0)]public float driftSpeed=6,responseAcceleration=4;
        [Header("Timed valve")]
        public bool intermittent;
        [Min(.1f)]public float onSeconds=5,offSeconds=2.5f;
        public float phaseOffset;
        [Min(0)]public float rampSeconds=.35f;
        public float enterBlend=.2f,exitBlend=.3f;
        [HideInInspector]public float acceleration=19,maximumRiseSpeed=7;
        BoxCollider box;ParticleSystem[] streams;float[] baseEmission;
        public Vector3 WorldDirection=>kind==WindKind.GravityRelief?Vector3.up:Vector3.ProjectOnPlane(directionIsLocal?transform.TransformDirection(direction):direction,Vector3.up).normalized;
        public float Strength=>StrengthAt(Time.timeSinceLevelLoad);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetRegistry()=>active.Clear();
        void Awake(){box=GetComponent<BoxCollider>();box.isTrigger=true;streams=GetComponentsInChildren<ParticleSystem>(true);baseEmission=new float[streams.Length];for(int i=0;i<streams.Length;i++)baseEmission[i]=streams[i].emission.rateOverTime.constant;}
        void OnEnable(){if(!active.Contains(this))active.Add(this);}
        void OnDisable()=>active.Remove(this);
        void Update(){if(streams==null)return;for(int i=0;i<streams.Length;i++){var emission=streams[i].emission;emission.rateOverTime=baseEmission[i]*Strength;}}
        public float StrengthAt(double time)
        {
            if(!windEnabled)return 0;if(!intermittent)return 1;
            double period=Mathf.Max(.1f,onSeconds)+Mathf.Max(.1f,offSeconds);float phase=(float)(((time+phaseOffset)%period+period)%period);
            if(phase>=onSeconds)return 0;if(rampSeconds<=0)return 1;float ramp=Mathf.Min(rampSeconds,onSeconds*.5f);return Mathf.Clamp01(Mathf.Min(phase/ramp,(onSeconds-phase)/ramp));
        }
        public bool Contains(Vector3 point)
        {
            if(!box)box=GetComponent<BoxCollider>();var p=transform.InverseTransformPoint(point)-box.center;var h=box.size*.5f;
            if(Mathf.Abs(p.y)>h.y)return false;
            return shape==WindShape.Cylinder?p.x*p.x/Mathf.Max(.0001f,h.x*h.x)+p.z*p.z/Mathf.Max(.0001f,h.z*h.z)<=1:Mathf.Abs(p.x)<=h.x&&Mathf.Abs(p.z)<=h.z;
        }
        public static WindSample Evaluate(Vector3 point,double time)
        {
            var result=new WindSample{gravityScale=1,response=5,enterBlend=.2f,exitBlend=.3f};float strongest=0;
            foreach(var w in active)
            {
                if(!w||!w.Contains(point))continue;float strength=w.StrengthAt(time);if(strength<=0)continue;
                result.volumes++;result.strength=Mathf.Max(result.strength,strength);result.enterBlend=w.enterBlend;result.exitBlend=w.exitBlend;
                if(w.kind==WindKind.GravityRelief)result.gravityScale=Mathf.Min(result.gravityScale,Mathf.Lerp(1,Mathf.Clamp(w.gravityScale,.2f,1),strength));
                else if(w.driftSpeed*strength>=strongest){strongest=w.driftSpeed*strength;result.drift=w.WorldDirection*strongest;result.response=w.responseAcceleration;}
            }
            return result;
        }
        // Compatibility query for tools authored against the first prototype.
        public static Vector3 Sample(Vector3 point,out float limit,out float blend)=>Sample(point,out limit,out blend,out _);
        public static Vector3 Sample(Vector3 point,out float limit,out float blend,out float exit)
        {var s=Evaluate(point,Time.timeSinceLevelLoad);limit=float.PositiveInfinity;blend=s.enterBlend;exit=s.exitBlend;return s.drift;}
        void OnDrawGizmos(){var b=GetComponent<BoxCollider>();if(!b)return;Gizmos.color=kind==WindKind.GravityRelief?new Color(.15f,.8f,1,.45f):new Color(1,.6f,.15f,.5f);Gizmos.matrix=transform.localToWorldMatrix;Gizmos.DrawWireCube(b.center,b.size);Gizmos.matrix=Matrix4x4.identity;Gizmos.DrawRay(transform.TransformPoint(b.center),WorldDirection*3);}
    }
}

