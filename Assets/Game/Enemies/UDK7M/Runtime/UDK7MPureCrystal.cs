using UnityEngine;
using CrystalParkour;
namespace UDK7M
{
    [RequireComponent(typeof(CrystalSurface))]public sealed class UDK7MPureCrystal:MonoBehaviour
    {
        public UDK7MArena arena;public bool Used {get;private set;}CrystalSurface surface;Renderer[] visuals;
        void Awake(){surface=GetComponent<CrystalSurface>();visuals=GetComponentsInChildren<Renderer>();}
        void OnEnable(){if(!surface)surface=GetComponent<CrystalSurface>();surface.Rebounded+=Strike;}
        void OnDisable(){if(surface)surface.Rebounded-=Strike;}
        void Strike(CrystalTraversal player){if(Used||!arena||arena.boss.Phase!=MachinePhase.Paralyzed)return;Used=true;player.combat.GrantResonance(arena.tuning.chargedModeSeconds);surface.canTether=false;foreach(var r in visuals)r.enabled=false;arena.feedback.Shake(transform.position,arena.tuning.lightSlamShake);arena.feedback.Sound(UDK7MSound.PureCrystalCharge,player.transform.position,player.transform);}
        public void ResetCrystal(){Used=false;if(!surface)surface=GetComponent<CrystalSurface>();surface.canTether=true;if(visuals==null)visuals=GetComponentsInChildren<Renderer>();foreach(var r in visuals)r.enabled=true;}
    }
}
