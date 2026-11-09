using System;
using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    public sealed class ActionRunner : MonoBehaviour
    {
        public ActionSet profile;
        public ActionMotionPlayer motion;
        public string CurrentId {get;private set;}
        public ActionDefinition Current=>profile?profile.Find(CurrentId):null;
        public event Action<MotionFrameEvent> FrameEventRaised;
        void Awake(){if(!motion)motion=GetComponent<ActionMotionPlayer>();}
        void OnEnable(){if(!motion)motion=GetComponent<ActionMotionPlayer>();if(motion)motion.FrameEventRaised+=OnFrame;}
        void OnDisable(){if(motion)motion.FrameEventRaised-=OnFrame;}
        void OnFrame(MotionFrameEvent e)=>FrameEventRaised?.Invoke(e);
        public bool CanBuffer(string next)
        {
            var rule=profile?profile.Transition(CurrentId,next):null;
            return rule!=null&&motion&&motion.SourceFrame+.0001f>=rule.bufferStartFrame;
        }
        public bool CanCancel(string next)
        {
            var rule=profile?profile.Transition(CurrentId,next):null;
            return rule!=null&&motion&&rule.CanCancel(motion.SourceFrame,motion.Beat,motion.Finished);
        }
        public bool Play(string id,bool immediate=false)
        {
            var definition=profile?profile.Find(id):null;if(definition?.Motion.track==null||!definition.Motion.track.clip||!motion)return false;
            var rule=profile.Transition(CurrentId,id);
            motion.Play(definition.Motion,rule?.entryFrame??-1,immediate,rule?.blend??-1);
            CurrentId=id;return true;
        }
    }
}
