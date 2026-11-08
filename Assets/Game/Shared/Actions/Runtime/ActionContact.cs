using System;
using System.Collections.Generic;
using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    public interface IActionTarget
    {
        bool ActionTargetAlive {get;} Transform ActionRoot {get;} Vector3 ActionAimPoint {get;}
        void ReceiveActionHit(float damage,Vector3 source,float impulse);
    }
    public static class ActionVisibility
    {
        public static bool Clear(Vector3 from,Vector3 to,Transform self,Transform target,LayerMask mask)
        {
            var delta=to-from;if(delta.sqrMagnitude<.00001f)return true;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,mask,QueryTriggerInteraction.Ignore))
            {
                if(self&&hit.transform.IsChildOf(self)||target&&hit.transform.IsChildOf(target))continue;
                var actor=hit.collider.GetComponentInParent<IActionTarget>();if(actor!=null&&!actor.ActionTargetAlive)continue;
                return false;
            }
            return true;
        }
    }
    public sealed class ActionContact
    {
        readonly Dictionary<string,HashSet<IActionTarget>> struck=new Dictionary<string,HashSet<IActionTarget>>();
        readonly Dictionary<string,double> nextDamage=new Dictionary<string,double>();
        readonly Dictionary<int,double> nextEffect=new Dictionary<int,double>();
        readonly Dictionary<HitWindow,float> lastSample=new Dictionary<HitWindow,float>();
        readonly Dictionary<HitWindow,Quaternion> rotations=new Dictionary<HitWindow,Quaternion>();
        public Func<IActionTarget,bool> TargetFilter;
        public event Action<int,IActionTarget> HitDelivered;
        double clock;
        public int HitCount {get;private set;}
        public void Reset(bool keepCooldowns=false){struck.Clear();lastSample.Clear();rotations.Clear();HitCount=0;if(!keepCooldowns){nextDamage.Clear();nextEffect.Clear();clock=0;}}
        public bool Resolve(ActionDefinition action,ActionMotionPlayer motion,ActionHitbox boxes,Transform owner,Vector3 origin,LayerMask mask,float impulse,float stepSeconds=0,int chargeOverride=-1,Vector3 direction=default)
        {
            clock+=stepSeconds>0?stepSeconds:Time.deltaTime;
            if(action==null||!action.isAttack||action.action.hits==null||boxes==null||!motion||!owner)return false;bool connected=false;
            for(int w=0;w<action.action.hits.Length;w++)
            {
                var h=action.action.hits[w];float frame=motion.SourceFrame;
                if(!Active(h,frame)||motion.DestinationWeight<h.minimumBlendWeight||!boxes.HasAnchor(h.anchor,h.customBone))continue;
                if(lastSample.TryGetValue(h,out var previous)&&frame>=previous-.0001f&&frame<previous+Mathf.Max(1,h.sampleEveryFrames)-.0001f)continue;
                lastSample[h]=frame;
                var anchor=boxes.Anchor(h.anchor,h.customBone);
                if(h.onlyWhileRotating)
                {
                    var rotation=anchor.localRotation;bool rotating=false;
                    if(rotations.TryGetValue(h,out var prior)){var delta=Quaternion.Inverse(prior)*rotation;delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;rotating=Mathf.Abs(angle*Vector3.Dot(axis,h.rotationAxis.normalized))>h.rotationThreshold/Mathf.Max(1,motion.Beat.track.framesPerSecond)*Mathf.Max(1,h.sampleEveryFrames);}
                    rotations[h]=rotation;if(!rotating)continue;
                }
                string group=string.IsNullOrEmpty(h.hitGroup)?"window:"+w:"group:"+h.hitGroup;
                if(!struck.TryGetValue(group,out var set)){set=new HashSet<IActionTarget>();struck[group]=set;}
                int count=boxes.Query(h,out var hits);var pose=boxes.Pose(h);var sampled=new HashSet<IActionTarget>();
                for(int i=0;i<count;i++)
                {
                    var collider=hits[i];if(!collider||collider.transform.IsChildOf(owner))continue;
                    if(h.contactEffects&&(!nextEffect.TryGetValue(collider.GetInstanceID(),out var at)||clock>=at))
                    {nextEffect[collider.GetInstanceID()]=clock+Mathf.Max(.02f,h.effectCooldown);var point=collider.ClosestPoint(pose.centre);if(h.contactEffect||h.contactSound)CombatEffects.Contact(h.contactEffect,h.contactSound,point,h.contactEffectLifetime);else CombatEffects.Emit(collider.GetComponentInParent<CrystalParkour.CrystalSurface>()?CombatEffect.CrystalShards:CombatEffect.Sparks,point,Vector3.up);}
                    if(h.repeat==HitRepeat.EffectsOnly)continue;
                    var target=collider.GetComponentInParent<IActionTarget>();if(target==null||!target.ActionTargetAlive||!sampled.Add(target)||TargetFilter!=null&&!TargetFilter(target))continue;
                    if(h.repeat==HitRepeat.Once&&set.Contains(target))continue;
                    string key=group+":"+target.ActionRoot.GetInstanceID();
                    if(h.repeat==HitRepeat.Cooldown&&nextDamage.TryGetValue(key,out var deadline)&&clock<deadline)continue;
                    if(!ActionVisibility.Clear(origin,target.ActionAimPoint,owner,target.ActionRoot,mask))continue;
                    set.Add(target);nextDamage[key]=clock+Mathf.Max(.01f,h.damageCooldown);HitCount++;connected=true;
                    ActionDamageDelivery.Deliver(target,ActionDamage.From(h,owner.position,chargeOverride>=0?chargeOverride:action.chargeLevel,direction),impulse);
                    HitDelivered?.Invoke(w,target);
                }
            }
            return connected;
        }
        public static bool Active(HitWindow h,float frame)=>h!=null&&h.enabled&&h.startFrame<=h.endFrame&&frame+.0001f>=h.startFrame&&frame<=h.endFrame+.0001f;
        public static bool Active(ActionDefinition action,float frame){if(action?.action.hits!=null)foreach(var h in action.action.hits)if(Active(h,frame))return true;return false;}
    }
}
