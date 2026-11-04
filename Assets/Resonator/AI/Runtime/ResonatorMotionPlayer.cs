using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace Resonator.AI
{
    // The graph evaluates a single clock owned by the AI. No Animator transition can
    // silently extend an attack or bypass a frame gate. Crossfades remain configurable.
    public sealed class ResonatorMotionPlayer : MonoBehaviour
    {
        public Animator animator;
        public Transform hips;
        public MotionBeat Beat {get;private set;}
        public float SourceSeconds {get;private set;}
        public float SourceFrame => Beat==null||!Beat.track?0:SourceSeconds*Beat.track.framesPerSecond;
        public float PreviousFrame {get;private set;}
        public bool Finished {get;private set;}
        public Vector3 RootDelta {get;private set;}
        public event System.Action<MotionFrameEvent> FrameEventRaised;
        bool firstAdvance;
        MotionFrameEvent[] scheduledEvents=System.Array.Empty<MotionFrameEvent>();
        PlayableGraph graph; AnimationMixerPlayable mixer;
        sealed class Layer
        {
            public AnimationClipPlayable playable;
            public double time;
            public float speed, fadeWeight;
        }
        readonly List<Layer> layers=new List<Layer>();
        Layer current;
        float fadeClock,fadeLength;
        public float BlendSeconds=>fadeLength;
        public float DestinationWeight=>fadeLength<=0?1:Mathf.Clamp01(fadeClock/fadeLength);
        Vector3 restLocalPosition;
        public Vector3 ReferenceHipInParent=>Beat==null||!Beat.track?Vector3.zero:restLocalPosition+transform.localRotation*Vector3.Scale(Beat.track.referenceHip,transform.localScale);
        void Awake(){if(!animator)animator=GetComponent<Animator>();restLocalPosition=transform.localPosition;}
        void EnsureGraph()
        {
            if(graph.IsValid())return;
            graph=PlayableGraph.Create(name+" Resonator Motion");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,2);
            AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(mixer);
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;graph.Play();
        }
        public void Play(MotionBeat beat,float startFrame=-1,bool immediate=false,float blendOverride=-1)
        {
            if(beat==null||!beat.track||!beat.track.clip){Debug.LogError("Missing Resonator motion",this);return;}
            EnsureGraph();
            // Preserve the complete current mixture when an in-progress blend is
            // interrupted, instead of snapping to its not-yet-visible destination.
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];if(layer!=null)layer.fadeWeight=mixer.GetInputWeight(i);
            }
            bool hasSource=current!=null;
            current=new Layer{playable=AnimationClipPlayable.Create(graph,beat.track.clip),speed=beat.speed};
            current.playable.SetApplyFootIK(false);current.playable.SetApplyPlayableIK(false);current.playable.SetSpeed(0);
            int slot=layers.FindIndex(l=>l==null);if(slot<0){slot=layers.Count;layers.Add(null);}
            if(slot>=mixer.GetInputCount())mixer.SetInputCount(slot+1);
            layers[slot]=current;graph.Connect(current.playable,0,mixer,slot);
            Beat=beat;SourceSeconds=(startFrame>=0?startFrame:beat.startFrame)/beat.track.framesPerSecond;
            scheduledEvents=beat.events==null?System.Array.Empty<MotionFrameEvent>():beat.events.Where(e=>e!=null).OrderBy(e=>e.frame).ToArray();
            SourceSeconds=Mathf.Clamp(SourceSeconds,0,beat.EndSeconds);PreviousFrame=SourceFrame;Finished=false;RootDelta=Vector3.zero;firstAdvance=true;
            fadeClock=0;fadeLength=immediate||!hasSource?0:blendOverride>=0?blendOverride:beat.blend;Evaluate(0);
        }
        public void Advance(float dt,float playback=1,bool frozen=false)
        {
            if(Beat==null||!graph.IsValid())return;
            PreviousFrame=SourceFrame;float before=SourceSeconds;
            float step=frozen?0:Mathf.Max(0,dt)*Beat.speed*Mathf.Max(0,playback);
            float begin=Beat.StartSeconds,end=Beat.EndSeconds,length=Mathf.Max(.0001f,end-begin);
            float raw=before+step;RootDelta=Vector3.zero;
            if(Beat.loop&&raw>end)
            {
                RootDelta=Beat.track.Sample(end)-Beat.track.Sample(before);
                raw-=end;int whole=Mathf.FloorToInt(raw/length);
                RootDelta+=(Beat.track.Sample(end)-Beat.track.Sample(begin))*whole;
                SourceSeconds=begin+raw%length;RootDelta+=Beat.track.Sample(SourceSeconds)-Beat.track.Sample(begin);
            }
            else
            {
                SourceSeconds=Mathf.Min(end,raw);RootDelta=Beat.track.Sample(SourceSeconds)-Beat.track.Sample(before);
                Finished=!Beat.loop&&raw>=end-.00001f;
            }
            foreach(var layer in layers)if(layer!=null&&layer!=current)layer.time+=frozen?0:Mathf.Max(0,dt)*layer.speed*Mathf.Max(0,playback);
            Evaluate(dt);
            if(step>0)
            {
                var beat=Beat;
                if(beat.loop&&before+step>end)
                {
                    DispatchEvents(beat,before,end,firstAdvance);
                    int whole=Mathf.FloorToInt((before+step-end)/length);
                    for(int i=0;i<whole&&Beat==beat;i++)DispatchEvents(beat,begin,end,true);
                    if(Beat==beat)DispatchEvents(beat,begin,SourceSeconds,true);
                }
                else DispatchEvents(beat,before,SourceSeconds,firstAdvance);
                if(Beat==beat)firstAdvance=false;
            }
        }
        void DispatchEvents(MotionBeat beat,float from,float to,bool includeStart)
        {
            foreach(var marker in scheduledEvents)
            {
                if(marker==null)continue;float time=marker.frame/beat.track.framesPerSecond;
                if(time<=to+.000001f&&(time>from+.000001f||includeStart&&time>=from-.000001f))FrameEventRaised?.Invoke(marker);
                if(Beat!=beat)return;
            }
        }
        // Editor-only callers can scrub the exact runtime mixer without dispatching events.
        public void SampleBlend(MotionBeat from,float sourceFrame,MotionBeat to,float destinationFrame,float seconds,float elapsed)
        {
            Play(from,sourceFrame,true);Play(to,destinationFrame,false,seconds);Evaluate(elapsed);
        }
        void Evaluate(float dt)
        {
            fadeClock+=Mathf.Max(0,dt);float weight=fadeLength<=0?1:Mathf.Clamp01(fadeClock/fadeLength);
            current.time=SourceSeconds;
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];if(layer==null)continue;
                layer.playable.SetTime(layer.time);
                mixer.SetInputWeight(i,layer==current?weight:layer.fadeWeight*(1-weight));
            }
            graph.Evaluate(0);
            if(weight>=1)for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];if(layer==null||layer==current)continue;
                graph.Disconnect(mixer,i);graph.DestroyPlayable(layer.playable);layers[i]=null;
            }
            // Strip the actual mixed pose, not just the new clip, so crossfades cannot
            // double-apply authored hip travel. Keep vertical bob, crouch and death pose.
            if(hips)
            {
                Vector3 model=transform.InverseTransformPoint(hips.position);
                Vector3 offset=transform.localRotation*Vector3.Scale(model-Beat.track.referenceHip,transform.localScale);offset.y=0;
                transform.localPosition=restLocalPosition-offset;
            }
        }
        public void StopForRagdoll(){if(graph.IsValid())graph.Destroy();if(animator)animator.enabled=false;}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
