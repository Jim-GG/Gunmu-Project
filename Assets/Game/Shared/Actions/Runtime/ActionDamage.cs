using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    public enum HitRepeat { Once, EffectsOnly, Cooldown, EverySample }
    public enum GuardKind { None, Imperfect, Perfect }
    public enum DamageCause { Ordinary, Resonance }
    public readonly struct ActionDamage
    {
        public readonly float damage,knockback,duration,blockedKnockback;
        public readonly Vector3 source,direction;
        public readonly int chargeLevel;
        public readonly bool blockable;
        public readonly DamageCause cause;
        public ActionDamage(float damage,Vector3 source,float knockback=0,float duration=.2f,bool blockable=true,float blockedKnockback=0,int chargeLevel=0,DamageCause cause=DamageCause.Ordinary,Vector3 direction=default)
        {this.damage=damage;this.source=source;this.direction=direction;this.knockback=knockback;this.duration=duration;this.blockable=blockable;this.blockedKnockback=blockedKnockback;this.chargeLevel=chargeLevel;this.cause=cause;}
        public static ActionDamage From(HitWindow h,Vector3 source,int charge=0,Vector3 direction=default)=>new ActionDamage(h.damage,source,h.knockbackDistance<0?h.damage*h.knockbackPerDamage:h.knockbackDistance,h.knockbackDuration,h.blockable,h.blockedKnockbackMultiplier,charge,direction:direction);
    }
    public interface IActionDamageReceiver { void ReceiveActionDamage(ActionDamage hit); }
    public static class ActionDamageDelivery
    {
        public static void Deliver(IActionTarget target,ActionDamage hit,float legacyImpulse=0)
        {if(target is IActionDamageReceiver receiver)receiver.ReceiveActionDamage(hit);else target.ReceiveActionHit(hit.damage,hit.source,legacyImpulse);}
    }
}
