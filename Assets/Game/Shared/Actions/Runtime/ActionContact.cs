using System.Collections.Generic;
using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    public interface IActionTarget
    {
        bool ActionTargetAlive {get;}
        Transform ActionRoot {get;}
        Vector3 ActionAimPoint {get;}
        void ReceiveActionHit(float damage,Vector3 source,float impulse);
    }
    public static class ActionVisibility
    {
        public static bool Clear(Vector3 from,Vector3 to,Transform self,Transform target,LayerMask mask)
        {
            var delta=to-from;if(delta.sqrMagnitude<.00001f)return true;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,mask,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(self)||target&&hit.transform.IsChildOf(target))continue;
                var actor=hit.collider.GetComponentInParent<IActionTarget>();
                if(actor!=null&&!actor.ActionTargetAlive)continue;
                return false;
            }
            return true;
        }
    }
    public sealed class ActionContact
    {
        readonly Dictionary<string,HashSet<IActionTarget>> struck=new Dictionary<string,HashSet<IActionTarget>>();
        public int HitCount {get;private set;}
        public void Reset(){struck.Clear();HitCount=0;}
        public bool Resolve(ActionDefinition action,ActionMotionPlayer motion,ActionHitbox boxes,Transform owner,Vector3 origin,LayerMask mask,float impulse)
        {
            if(action==null||!action.isAttack||action.action.hits==null||boxes==null||!motion||!owner)return false;bool connected=false;
            for(int w=0;w<action.action.hits.Length;w++)
            {
                var h=action.action.hits[w];
                if(!Active(h,motion.SourceFrame)||motion.DestinationWeight<h.minimumBlendWeight)continue;
                string group=string.IsNullOrEmpty(h.hitGroup)?"window:"+w:"group:"+h.hitGroup;
                if(!struck.TryGetValue(group,out var set)){set=new HashSet<IActionTarget>();struck[group]=set;}
                int count=boxes.Query(h,out var hits);
                for(int i=0;i<count;i++)
                {
                    if(!hits[i]||hits[i].transform.IsChildOf(owner))continue;
                    var target=hits[i].GetComponentInParent<IActionTarget>();
                    if(target==null||!target.ActionTargetAlive||set.Contains(target))continue;
                    if(!ActionVisibility.Clear(origin,target.ActionAimPoint,owner,target.ActionRoot,mask))continue;
                    set.Add(target);HitCount++;connected=true;
                    target.ReceiveActionHit(h.damage,owner.position,impulse);
                }
            }
            return connected;
        }
        public static bool Active(HitWindow h,float frame)=>h!=null&&h.enabled&&h.startFrame<=h.endFrame&&frame+.0001f>=h.startFrame&&frame<=h.endFrame+.0001f;
        public static bool Active(ActionDefinition action,float frame)
        {if(action?.action.hits!=null)foreach(var h in action.action.hits)if(Active(h,frame))return true;return false;}
    }
}
