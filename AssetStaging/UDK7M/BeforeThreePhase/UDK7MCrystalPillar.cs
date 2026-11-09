using UnityEngine;
using GameActions;
namespace UDK7M
{
    public sealed class UDK7MCrystalPillar:MonoBehaviour
    {
        public GameObject crystals;public Collider crystalBounds;public bool Consumed {get;private set;}
        public Vector3 ContactPoint(Vector3 from)=>crystalBounds?crystalBounds.ClosestPoint(from):transform.position+Vector3.up*3;
        public bool Consume(UDK7MBoss boss)
        {
            if(Consumed||!boss||!boss.Engaged)return false;Consumed=true;
            CombatEffects.Emit(CombatEffect.Resonance,ContactPoint(boss.DrillTip),Vector3.up,2);CombatEffects.Emit(CombatEffect.CrystalShards,ContactPoint(boss.DrillTip),Vector3.up,2);
            if(crystals)crystals.SetActive(false);boss.arena.PillarResonated();return true;
        }
        public void ResetPillar(){Consumed=false;if(crystals)crystals.SetActive(true);}
    }
}
