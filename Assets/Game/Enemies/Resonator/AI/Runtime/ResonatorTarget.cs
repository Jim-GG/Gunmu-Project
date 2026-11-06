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
    public sealed class ResonatorTarget : MonoBehaviour
    {
        public Transform eyes;
        public FirstPerson.FirstPersonVitals vitals;
        public FirstPerson.GauntletCombat gauntlet;
        public NullBoundary.Demo.DemoPlayer demoPlayer;
        public bool invulnerableForTesting;
        public bool Alive => invulnerableForTesting || (vitals ? vitals.Health>0 : demoPlayer ? demoPlayer.IsAlive : true);
        public Vector3 EyePosition => eyes?eyes.position:transform.position+Vector3.up*1.6f;
        public Vector3 ChestPosition => transform.position+Vector3.up*1.05f;
        public StrikeResult ReceiveStrike(float damage,Vector3 source)
        {
            if(!Alive)return StrikeResult.Miss;
            if(gauntlet&&gauntlet.TryBlock(source))return StrikeResult.Blocked;
            if(invulnerableForTesting)return StrikeResult.Damaged;
            if(vitals)vitals.ReceiveDamage(damage);
            else if(demoPlayer)return demoPlayer.TakeDamage(damage,source)?StrikeResult.Blocked:StrikeResult.Damaged;
            return StrikeResult.Damaged;
        }
    }
}
