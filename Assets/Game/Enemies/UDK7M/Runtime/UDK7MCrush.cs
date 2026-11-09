using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameActions;
using Resonator.AI;

namespace UDK7M
{
    public sealed class UDK7MCrush
    {
        readonly Transform owner;
        readonly UDK7MTuning tuning;
        readonly Bounds[] trackBounds=new Bounds[2];
        readonly Dictionary<int,float> deadlines=new Dictionary<int,float>();
        readonly HashSet<IActionTarget> sampled=new HashSet<IActionTarget>();
        float clock;
        public int Hits {get;private set;}
        public UDK7MCrush(Transform owner,UDK7MTuning tuning)
        {
            this.owner=owner;this.tuning=tuning;
            for(int side=0;side<2;side++)
            {
                var bones=owner.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Tread_")&&(owner.InverseTransformPoint(t.position).x<0)==(side==0)).ToArray();
                if(bones.Length==0){trackBounds[side]=new Bounds(side==0?tuning.crushLeftCenter:tuning.crushRightCenter,tuning.crushBoxSize);continue;}
                var bounds=new Bounds(owner.InverseTransformPoint(bones[0].position),Vector3.zero);
                foreach(var b in bones)bounds.Encapsulate(owner.InverseTransformPoint(b.position));
                trackBounds[side]=bounds;
            }
            // Link pivots can sit on an edge of the shoe. Include the skinned
            // surface, so a thin line of pivots never becomes a thin crush box.
            foreach(var renderer in owner.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.bones.Any(b=>b&&b.name.StartsWith("Tread_")))continue;
                var mesh=new Mesh();renderer.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices){var point=owner.InverseTransformPoint(renderer.transform.TransformPoint(vertex));trackBounds[point.x<0?0:1].Encapsulate(point);}
                Object.Destroy(mesh);
            }
        }
        public void Reset(){clock=0;deadlines.Clear();Hits=0;}
        public Bounds LocalBounds(int side)
        {
            if(!tuning.crushUseTrackBounds)return new Bounds(side==0?tuning.crushLeftCenter:tuning.crushRightCenter,tuning.crushBoxSize);
            var bounds=trackBounds[side];bounds.Expand(Mathf.Max(0,tuning.crushBoundsPadding)*2);return bounds;
        }
        public void Tick(float dt,Vector3 before,Quaternion beforeRotation)
        {
            clock+=dt;if(!tuning.crushEnabled||dt<=0)return;
            float speed=Vector3.Distance(before,owner.position)/dt+Quaternion.Angle(beforeRotation,owner.rotation)*Mathf.Deg2Rad*tuning.bodyRadius/dt;
            if(speed<tuning.crushMinimumSpeed)return;
            sampled.Clear();
            for(int side=0;side<2;side++)
            {
                var bounds=LocalBounds(side);var centre=owner.TransformPoint(bounds.center);var size=Vector3.Scale(bounds.size,Abs(owner.lossyScale));
                foreach(var collider in Physics.OverlapBox(centre,size*.5f,owner.rotation,~0,QueryTriggerInteraction.Ignore))
                {
                    if(!collider||collider.transform.IsChildOf(owner))continue;
                    var target=collider.GetComponentInParent<IActionTarget>();
                    if(!(target is ResonatorTarget)&&!(target is ResonatorEnemy)||!target.ActionTargetAlive||!sampled.Add(target))continue;
                    int id=target.ActionRoot.GetInstanceID();if(deadlines.TryGetValue(id,out var until)&&clock<until)continue;
                    if(!ActionVisibility.Clear(centre,target.ActionAimPoint,owner,target.ActionRoot,~0))continue;
                    deadlines[id]=clock+Mathf.Max(.02f,tuning.crushCooldown);Hits++;
                    var push=Vector3.ProjectOnPlane(target.ActionAimPoint-centre,Vector3.up).normalized;
                    if(push.sqrMagnitude<.01f)push=owner.forward;
                    // Source just behind the victim gives both movement controllers
                    // the same outward direction, including while turning in place.
                    var enemy=target as ResonatorEnemy;float damage=enemy&&tuning.crushKillsMinions?Mathf.Max(enemy.Health,tuning.crushDamage):tuning.crushDamage;
                    ActionDamageDelivery.Deliver(target,new ActionDamage(damage,target.ActionRoot.position-push,tuning.crushKnockback,tuning.crushPushSeconds,true,tuning.crushBlockedKnockback,cause:enemy?DamageCause.Crush:DamageCause.Ordinary,direction:push));
                    CombatEffects.Emit(CombatEffect.Sparks,collider.ClosestPoint(centre),Vector3.up);
                    var boss=owner.GetComponent<UDK7MBoss>();if(boss&&boss.arena)boss.arena.feedback.Sound(UDK7MSound.Crush,collider.ClosestPoint(centre),owner);
                }
            }
        }
        static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        public void DrawGizmos()
        {
            var old=Gizmos.matrix;Gizmos.matrix=owner.localToWorldMatrix;Gizmos.color=new Color(1,.55f,.08f);
            for(int side=0;side<2;side++){var bounds=LocalBounds(side);Gizmos.DrawWireCube(bounds.center,bounds.size);}Gizmos.matrix=old;
        }
    }
}
