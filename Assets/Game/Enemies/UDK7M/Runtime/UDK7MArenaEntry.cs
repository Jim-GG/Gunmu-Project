using UnityEngine;
using Resonator.AI;
namespace UDK7M
{
    public sealed class UDK7MArenaEntry:MonoBehaviour
    {
        public UDK7MArena arena;
        void OnTriggerEnter(Collider other){if(other.GetComponentInParent<ResonatorTarget>())arena.BeginFight();}
    }
}
