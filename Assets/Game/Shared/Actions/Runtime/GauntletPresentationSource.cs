using UnityEngine;
using Gauntlet;
using FirstPerson;
namespace GameActions
{
    [DisallowMultipleComponent]
    public sealed class GauntletPresentationSource:MonoBehaviour
    {
        GauntletCombat combat;PlayerChargedPunch puzzle;
        void Resolve(){if(!combat)combat=GetComponent<GauntletCombat>();if(!puzzle)puzzle=GetComponent<PlayerChargedPunch>();}
        public static GauntletPresentationSource Ensure(GameObject actor)=>actor.GetComponent<GauntletPresentationSource>()??actor.AddComponent<GauntletPresentationSource>();
        public bool UsesPuzzleRules {get{Resolve();return puzzle;}}
        public bool IsUsable {get{Resolve();return combat&&combat.isActiveAndEnabled&&(!puzzle||puzzle.CanUseSharedCombat);}}
        public bool IsCharging=>IsUsable&&(combat.State==GauntletCombat.Phase.Charging||combat.State==GauntletCombat.Phase.ReleasePending||combat.State==GauntletCombat.Phase.Full);
        public bool IsFull=>IsCharging&&combat.State==GauntletCombat.Phase.Full;
        public bool AtPartialCap=>false;
        public float Seconds {get{Resolve();return combat?combat.ChargeSeconds:0;}}
        public float FullDuration {get{Resolve();return combat?combat.policy.full/combat.policy.framesPerSecond:92/30f;}}
        public float PlaybackSpeed=>combat&&combat.actions&&combat.actions.Current!=null?combat.actions.Current.Motion.speed:1;
        public GauntletAnimationPlayer Animation {get{Resolve();return combat?combat.animationPlayer:null;}}
        public Transform WeaponAnchor
        {
            get
            {
                var animation=Animation;if(!animation)return null;
                foreach(var t in animation.GetComponentsInChildren<Transform>(true))if(t.name=="FX_FullChargeCore")return t;
                if(animation.meshRenderer)foreach(var bone in animation.meshRenderer.bones)if(bone&&bone.name.ToLowerInvariant().Contains("mechanism"))return bone;
                return animation.transform;
            }
        }
    }
}
