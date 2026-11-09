using System.Collections.Generic;
using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    // Short, collision-constrained player movement. Does not rotate the view or move the enemy.
    public sealed class ActionTargetAssist
    {
        readonly AttackMoveBudget budget=new AttackMoveBudget();
        IActionTarget target;
        public float Travel=>budget.TotalTravel;
        public Transform Target=>(target as Object)&&target.ActionTargetAlive?target.ActionRoot:null;
        public void Reset(ActionDefinition action,float entry,Transform owner,Transform aim,LayerMask mask)
        {
            target=null;budget.Reset(action.action.displacement,entry);
            if(!action.action.displacement.enabled||!aim||action.acquisitionRange<=0)return;
            float best=float.PositiveInfinity;var seen=new HashSet<IActionTarget>();
            foreach(var collider in Physics.OverlapSphere(owner.position+Vector3.up,action.acquisitionRange+.5f,mask,QueryTriggerInteraction.Ignore))
            {
                var t=collider.GetComponentInParent<IActionTarget>();
                if(t==null||!t.ActionTargetAlive||t.ActionRoot.IsChildOf(owner)||!seen.Add(t))continue;
                var delta=t.ActionRoot.position-owner.position;
                if(Mathf.Abs(delta.y)>action.maxHeightDifference)continue;
                float distance=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;
                if(distance>action.acquisitionRange)continue;
                float angle=Vector3.Angle(aim.forward,t.ActionAimPoint-aim.position);
                if(angle>action.acquisitionHalfAngle||!ActionVisibility.Clear(aim.position,t.ActionAimPoint,owner,t.ActionRoot,mask))continue;
                float score=angle/Mathf.Max(1,action.acquisitionHalfAngle)+distance/Mathf.Max(.1f,action.acquisitionRange)*.35f;
                if(score<best){best=score;target=t;}
            }
        }
        public void Clear()=>target=null;
        public void Step(ActionDefinition action,float previous,float frame,float dt,Transform owner,Transform aim,CharacterController body,LayerMask mask,float separation,bool connected)
        {
            if(!Target||!body||!body.enabled||!body.gameObject.activeInHierarchy||!ActionContact.Active(action,frame))return;
            if(action.groundedAssistanceOnly&&!Grounded(body,owner,mask))return;
            if(Mathf.Abs(target.ActionRoot.position.y-owner.position.y)>action.maxHeightDifference)return;
            var delta=Vector3.ProjectOnPlane(target.ActionRoot.position-owner.position,Vector3.up);float gap=delta.magnitude;
            if(gap>action.acquisitionRange+.5f||Vector3.Angle(aim.forward,target.ActionAimPoint-aim.position)>action.acquisitionHalfAngle)return;
            if(!ActionVisibility.Clear(aim.position,target.ActionAimPoint,owner,target.ActionRoot,mask))return;
            var request=budget.Request(action.action.displacement,previous,frame,dt,gap,separation,connected,false);
            if(request.distance<=0||gap<.00001f)return;
            var before=owner.position;Vector3 direction=delta/gap;
            // CharacterController.Move also performs the final collision and slide resolution.
            float distance=request.distance;
            Vector3 centre=owner.TransformPoint(body.center);float radius=body.radius*Mathf.Max(Mathf.Abs(owner.lossyScale.x),Mathf.Abs(owner.lossyScale.z));
            float half=Mathf.Max(0,body.height*Mathf.Abs(owner.lossyScale.y)*.5f-radius);
            // Starting a full-size sweep while resting on the floor can report a
            // zero-distance hit with a synthetic -direction normal. Use an inset
            // sweep; the full CharacterController still resolves the final move.
            float sweepRadius=Mathf.Max(.01f,radius-Mathf.Max(.01f,body.skinWidth));
            foreach(var hit in Physics.CapsuleCastAll(centre+Vector3.up*half,centre-Vector3.up*half,sweepRadius,direction,distance+.025f,mask,QueryTriggerInteraction.Ignore))
            {if(hit.transform.IsChildOf(owner)||Vector3.Dot(hit.normal,direction)>=-.001f)continue;distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.025f));}
            if(distance>0)body.Move(direction*distance);
            budget.Consume(request,Mathf.Max(0,Vector3.Dot(owner.position-before,direction)));
        }
        static bool Grounded(CharacterController body,Transform owner,LayerMask mask)
        {
            if(body.isGrounded)return true;
            float scale=Mathf.Abs(owner.lossyScale.y),radius=body.radius*.9f;
            Vector3 foot=owner.TransformPoint(body.center)-Vector3.up*(body.height*scale*.5f-body.radius+.04f);
            foreach(var hit in Physics.SphereCastAll(foot+Vector3.up*.12f,radius,Vector3.down,.2f,mask,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(owner)&&hit.normal.y>.5f)return true;
            return false;
        }
    }
}
