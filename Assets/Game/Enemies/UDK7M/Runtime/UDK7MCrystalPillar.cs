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
            boss.arena.PillarResonated(this);return true;
        }
        public void Shatter(Vector3 source,bool playEffects=true)
        {
            if(Consumed)return;
            Consumed=true;
            if(playEffects){var boss=GetComponentInParent<UDK7MArena>()?.boss;if(!boss)boss=UnityEngine.Object.FindFirstObjectByType<UDK7MBoss>();if(boss)UDK7MPresentation.Ensure(boss).PlayPillarShatter(ContactPoint(source));}
            if(crystals)crystals.SetActive(false);
        }
        public void Regrow(){StopAllCoroutines();Consumed=false;if(crystals){crystals.SetActive(true);StartCoroutine(Grow());}}
        IEnumerator Grow(){float time=0;while(time<1.2f){time+=Time.deltaTime;crystals.transform.localScale=Vector3.one*Mathf.SmoothStep(.02f,1,time/1.2f);yield return null;}crystals.transform.localScale=Vector3.one;}
        public void ResetPillar(){StopAllCoroutines();Consumed=false;if(crystals){crystals.transform.localScale=Vector3.one;crystals.SetActive(true);}}
    }
}
