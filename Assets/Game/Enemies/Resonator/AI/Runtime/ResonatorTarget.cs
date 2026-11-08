using System;
using UnityEngine;
namespace Resonator.AI
{
    public readonly struct ResonatorNoiseEvent
    {
        public readonly Vector3 position; public readonly float radius; public readonly ResonatorTarget source;
        public ResonatorNoiseEvent(Vector3 position,float radius,ResonatorTarget source){this.position=position;this.radius=radius;this.source=source;}
    }
    public static class ResonatorNoise
    {
        public static event Action<ResonatorNoiseEvent> Emitted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear()=>Emitted=null;
        public static void Emit(Vector3 position,float radius,ResonatorTarget source){if(radius>0&&source)Emitted?.Invoke(new ResonatorNoiseEvent(position,radius,source));}
    }
    public enum StrikeResult { Miss, Blocked, Damaged }
    public sealed class ResonatorTarget : MonoBehaviour,GameActions.IActionTarget,GameActions.IActionDamageReceiver
    {
        public Transform eyes;
        public FirstPerson.FirstPersonVitals vitals;
        public FirstPerson.GauntletCombat gauntlet;
        public NullBoundary.Demo.DemoPlayer demoPlayer;
        public bool invulnerableForTesting;
        public bool Alive => invulnerableForTesting || (vitals ? vitals.Health>0 : demoPlayer ? demoPlayer.IsAlive : true);
        public Vector3 EyePosition => eyes?eyes.position:transform.position+Vector3.up*1.6f;
        public Vector3 ChestPosition => transform.position+Vector3.up*1.05f;
        public bool ActionTargetAlive=>Alive;
        public Transform ActionRoot=>transform;
        public Vector3 ActionAimPoint=>ChestPosition;
        public void ReceiveActionHit(float damage,Vector3 source,float impulse)=>ReceiveStrike(damage,source);
        public void ReceiveActionDamage(GameActions.ActionDamage hit)=>ReceiveStrike(hit);
        public StrikeResult ReceiveStrike(float damage,Vector3 source)
        {return ReceiveStrike(new GameActions.ActionDamage(damage,source));}
        public StrikeResult ReceiveStrike(GameActions.ActionDamage hit)
        {LastStrike=ResolveStrike(hit);return LastStrike;}
        public StrikeResult LastStrike {get;private set;}
        StrikeResult ResolveStrike(GameActions.ActionDamage hit)
        {
            if(!Alive)return StrikeResult.Miss;
            var guard=hit.blockable&&gauntlet?gauntlet.ResolveGuard(hit.source):GameActions.GuardKind.None;
            float damage=hit.damage*(guard==GameActions.GuardKind.Perfect?0:guard==GameActions.GuardKind.Imperfect?.34f:1);
            var motor=GetComponent<FirstPerson.FirstPersonMotor>();if(motor)motor.Knockback(hit.source,hit.knockback*(guard==GameActions.GuardKind.None?1:hit.blockedKnockback),hit.duration);
            if(damage<=0)return guard!=GameActions.GuardKind.None?StrikeResult.Blocked:StrikeResult.Miss;
            if(invulnerableForTesting)return StrikeResult.Damaged;
            if(vitals)vitals.ReceiveDamage(damage);
            else if(demoPlayer)return demoPlayer.TakeDamage(damage,hit.source)?StrikeResult.Blocked:StrikeResult.Damaged;
            return StrikeResult.Damaged;
        }
    }
}
