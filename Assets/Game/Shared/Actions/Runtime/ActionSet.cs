using System;
using UnityEngine;
using Resonator.AI;

namespace GameActions
{
    [Serializable] public sealed class ActionSocketBinding
    {
        public HitboxAnchor role;
        public string transformName;
    }
    [Serializable] public sealed class ActionDefinition
    {
        public string id="New Action";
        public bool isAttack;
        [Range(0,3)] public int chargeLevel;
        // Reuse the existing serialized beat/window/curve formats, including legacy profiles.
        public AttackBeat action=new AttackBeat{hits=Array.Empty<HitWindow>()};
        [Header("Target assistance / movement only, never rotates the camera")]
        [Min(0)] public float acquisitionRange=3;
        [Range(0,90)] public float acquisitionHalfAngle=32;
        [Min(0)] public float maxHeightDifference=1.25f;
        public bool groundedAssistanceOnly=true;
        public MotionBeat Motion=>action.animation;
    }

    [Serializable] public sealed class ActionTransition
    {
        public string label="New transition", from, to;
        public bool enabled=true, anySource;
        [Min(0)] public float blend=.07f;
        [Tooltip("-1 uses the destination start frame.")]
        public float entryFrame=-1;
        [Tooltip("First SOURCE frame allowing an input cancel. -1 waits for the end.")]
        public float cancelFrame=-1;
        [Tooltip("Last SOURCE frame for input cancel. -1 uses the source end.")]
        public float cancelEndFrame=-1;
        [Tooltip("First SOURCE frame accepting buffered input. -1 accepts from the start; expiry still applies.")]
        public float bufferStartFrame=-1;
        public bool CanCancel(float frame,MotionBeat source,bool finished)
        {
            if(!enabled||source==null)return false;
            float first=cancelFrame<0?source.End:cancelFrame;
            float last=cancelEndFrame<0?source.End:cancelEndFrame;
            return (frame+.0001f>=first||finished&&first<=source.End)&&frame<=last+.0001f;
        }
    }

    [CreateAssetMenu(menuName="Game Actions/Action Set")]
    public sealed class ActionSet : ScriptableObject
    {
        public GameObject previewModel;
        public bool firstPersonPreview;
        public string previewCameraBone="ViewCameraMotion";
        public float previewFieldOfView=65;
        [Tooltip("Optional third-person preview focus, in model metres. Used when Preview Distance is greater than zero.")]
        public Vector3 previewFocus;
        [Min(0), Tooltip("0 keeps the existing preview framing. Larger units can set their own starting camera distance; scrolling still zooms.")]
        public float previewDistance;
        public ActionSocketBinding[] sockets=Array.Empty<ActionSocketBinding>();
        [Min(.05f)] public float minimumSeparation=.85f;
        [Range(.03f,.5f)] public float inputBufferSeconds=.22f;
        public ActionDefinition[] actions=Array.Empty<ActionDefinition>();
        public ActionTransition[] transitions=Array.Empty<ActionTransition>();
        public ActionDefinition Find(string id)
        {if(actions!=null)foreach(var a in actions)if(a!=null&&a.id==id)return a;return null;}
        public ActionTransition Transition(string from,string to)
        {
            if(transitions==null)return null;
            foreach(var t in transitions)if(t!=null&&t.enabled&&!t.anySource&&t.from==from&&t.to==to)return t;
            foreach(var t in transitions)if(t!=null&&t.enabled&&t.anySource&&t.to==to)return t;
            return null;
        }
    }

    // One expiring command, so button mashing never schedules a series of future attacks.
    // Release is retained: a buffered click remains a click, rather than becoming a held charge.
    public sealed class ActionInputBuffer
    {
        public bool Pending {get;private set;}
        public bool Held {get;private set;}
        public double PressTime {get;private set;}
        public float HeldSeconds {get;private set;}
        double expires;
        public void Press(double now,float lifetime)
        {Pending=Held=true;PressTime=now;HeldSeconds=0;expires=now+Mathf.Max(0,lifetime);}
        public void Release(double now)
        {if(!Pending)return;Held=false;HeldSeconds=Mathf.Max(0,(float)(now-PressTime));}
        public bool Valid(double now)
        {if(Pending&&now>expires+.000001)Clear();return Pending;}
        public void Clear(){Pending=Held=false;HeldSeconds=0;}
    }
}
