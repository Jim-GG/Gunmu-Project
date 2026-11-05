using System;
using UnityEngine;
namespace Resonator.AI
{
    [Serializable] public sealed class AttackMoveSegment
    {
        public string label="前进";
        public bool enabled=true;
        public float startFrame=20,endFrame=40;
        [Min(0)] public float maxDistance=.8f;
        [Min(.01f)] public float maxSpeed=3;
        [Min(.1f)] public float contactDistance=1;
        public AnimationCurve progress=AnimationCurve.EaseInOut(0,0,1,1);
        public float Value(float frame)
        {
            if(frame<=startFrame)return 0;if(frame>=endFrame)return 1;
            return Mathf.Clamp01(progress==null?Mathf.InverseLerp(startFrame,endFrame,frame):progress.Evaluate(Mathf.InverseLerp(startFrame,endFrame,frame)));
        }
    }
    [Serializable] public sealed class AttackDisplacement
    {
        public bool enabled;
        public bool includeAuthoredRootMotion;
        public bool stopOnHit=true;
        public bool trackTarget=true;
        public float trackingStartFrame,trackingEndFrame=32;
        [Min(0)] public float turnSpeed=140;
        [Range(0,180)] public float maxTrackingAngle=110;
        public AttackMoveSegment[] segments=Array.Empty<AttackMoveSegment>();
        public static AttackDisplacement Preset(EnemyState state,AttackBeat beat)
        {
            float first=state==EnemyState.Kicking?21:state==EnemyState.Attack?40:36,last=first+5;bool found=false;
            if(beat.hits!=null)foreach(var h in beat.hits)if(h!=null&&h.enabled&&h.startFrame<=h.endFrame)
            {first=found?Mathf.Min(first,h.startFrame):h.startFrame;last=found?Mathf.Max(last,h.endFrame):h.endFrame;found=true;}
            var d=new AttackDisplacement{enabled=true,trackingEndFrame=first,turnSpeed=140};
            if(state==EnemyState.Headbutt)
            {
                d.turnSpeed=100;d.trackingEndFrame=first+1;
                d.segments=new[]{Segment("伸手逼近",Mathf.Max(0,first-26),Mathf.Max(1,first-4),.65f,1.1f,.86f,false),Segment("头槌前压",Mathf.Max(0,first-3),last,.30f,1.9f,.82f,true)};
            }
            else if(state==EnemyState.Kicking)
            {d.turnSpeed=110;d.trackingEndFrame=first+1;d.segments=new[]{Segment("踢击前滑",Mathf.Max(0,first-6),last+4,.65f,2.4f,1.05f,false)};}
            else if(state==EnemyState.Attack)
            {d.turnSpeed=150;d.trackingEndFrame=Mathf.Max(0,first-4);d.segments=new[]{Segment("砸击爆发前冲",Mathf.Max(0,first-12),last,1.8f,6.5f,.95f,true)};}
            else
            {d.turnSpeed=150;d.segments=new[]{Segment("挥击踏步",Mathf.Max(0,first-12),last,.9f,3,1,true)};}
            return d;
        }
        static AttackMoveSegment Segment(string label,float start,float end,float distance,float speed,float contact,bool eased)=>new AttackMoveSegment
        {label=label,startFrame=start,endFrame=Mathf.Max(start+1,end),maxDistance=distance,maxSpeed=speed,contactDistance=contact,progress=eased?AnimationCurve.EaseInOut(0,0,1,1):AnimationCurve.Linear(0,0,1,1)};
    }
    public readonly struct AttackMoveRequest
    {
        public readonly int index;
        public readonly float distance,maxSpeed,contactDistance;
        public AttackMoveRequest(int index,float distance,float maxSpeed,float contact){this.index=index;this.distance=distance;this.maxSpeed=maxSpeed;contactDistance=contact;}
    }
    // Budget calculation is shared by runtime movement and the workbench simulation.
    // Only actually committed travel spends a budget. Skipped entry frames never
    // produce retroactive movement, and blocked travel cannot accumulate a teleport.
    public sealed class AttackMoveBudget
    {
        float[] spent=Array.Empty<float>(),baseline=Array.Empty<float>();
        public float TotalTravel {get;private set;}
        public void Reset(AttackDisplacement track,float entry)
        {
            int n=track?.segments?.Length??0;spent=new float[n];baseline=new float[n];TotalTravel=0;
            for(int i=0;i<n;i++)if(track.segments[i]!=null)baseline[i]=track.segments[i].Value(entry);
        }
        public AttackMoveRequest Request(AttackDisplacement track,float previous,float frame,float dt,float distance,float separation,bool hit,bool touching)
        {
            if(track==null||!track.enabled||track.segments==null||dt<=0)return default;
            if(spent.Length!=track.segments.Length)Reset(track,previous);
            if(track.stopOnHit&&hit||touching)return default;
            for(int i=0;i<track.segments.Length;i++)
            {
                var s=track.segments[i];if(s==null||!s.enabled||s.endFrame<=s.startFrame||frame<=s.startFrame||previous>=s.endFrame)continue;
                float contact=Mathf.Max(separation,s.contactDistance),gap=Mathf.Max(0,distance-contact);
                float available=Mathf.Max(0,Mathf.Max(0,s.maxDistance)*(s.Value(frame)-baseline[i])-spent[i]);
                return new AttackMoveRequest(i,Mathf.Min(gap,available,Mathf.Max(0,s.maxSpeed)*dt),Mathf.Max(.01f,s.maxSpeed),contact);
            }
            return default;
        }
        public void Consume(AttackMoveRequest request,float actual)
        {if(actual<=0||request.index<0||request.index>=spent.Length)return;float amount=Mathf.Min(request.distance,actual);spent[request.index]+=amount;TotalTravel+=amount;}
        public static float Preview(AttackBeat beat,float entry,float frame,float initialGap,float separation)
        {
            var budget=new AttackMoveBudget();budget.Reset(beat.displacement,entry);float moved=0,at=entry;
            float step=1f,secondsPerFrame=1/Mathf.Max(.001f,beat.animation.track.framesPerSecond*beat.animation.speed);
            while(at<frame-.00001f)
            {
                float next=Mathf.Min(frame,at+step);
                float root=beat.displacement.includeAuthoredRootMotion?(beat.animation.track.Sample(at/beat.animation.track.framesPerSecond)-beat.animation.track.Sample(entry/beat.animation.track.framesPerSecond)).z*beat.rootMotionScale:0;
                var r=budget.Request(beat.displacement,at,next,(next-at)*secondsPerFrame,initialGap-moved-root,separation,false,false);
                moved+=r.distance;budget.Consume(r,r.distance);at=next;
            }
            return moved;
        }
    }
}
