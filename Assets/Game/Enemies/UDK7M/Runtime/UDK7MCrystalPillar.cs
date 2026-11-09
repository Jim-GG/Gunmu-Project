using UnityEngine;
using GameActions;
using System.Collections;
namespace UDK7M
{
    public sealed class UDK7MCrystalPillar:MonoBehaviour
    {
        public GameObject crystals;public Collider crystalBounds;public bool Consumed {get;private set;}
        public Vector3 ContactPoint(Vector3 from)=>crystalBounds?crystalBounds.ClosestPoint(from):transform.position+Vector3.up*3;
        public bool Consume(UDK7MBoss boss)
        {
            if(Consumed||!boss||!boss.Engaged||boss.Phase!=MachinePhase.Stable)return false;
            boss.arena.PillarResonated();return true;
        }
        public void Shatter(Vector3 source)
        {
            Consumed=true;
            CombatEffects.Emit(CombatEffect.Resonance,ContactPoint(source),Vector3.up,2);CombatEffects.Emit(CombatEffect.CrystalShards,ContactPoint(source),Vector3.up,2);
            if(crystals)crystals.SetActive(false);
        }
        public void Regrow(){StopAllCoroutines();Consumed=false;if(crystals){crystals.SetActive(true);StartCoroutine(Grow());}}
        IEnumerator Grow(){float time=0;while(time<1.2f){time+=Time.deltaTime;crystals.transform.localScale=Vector3.one*Mathf.SmoothStep(.02f,1,time/1.2f);yield return null;}crystals.transform.localScale=Vector3.one;}
        public void ResetPillar(){StopAllCoroutines();Consumed=false;if(crystals){crystals.transform.localScale=Vector3.one;crystals.SetActive(true);}}
    }
}
