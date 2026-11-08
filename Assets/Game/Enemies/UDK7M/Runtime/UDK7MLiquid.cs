using UnityEngine;
using Resonator.AI;
namespace UDK7M
{
    public sealed class UDK7MLiquid:MonoBehaviour
    {
        public UDK7MArena arena;
        void OnTriggerStay(Collider other){var target=other.GetComponentInParent<ResonatorTarget>();if(target&&target.Alive&&!target.invulnerableForTesting&&target.vitals)target.vitals.ReceiveDamage(arena.tuning.liquidDps*Time.fixedDeltaTime);}
    }
}
