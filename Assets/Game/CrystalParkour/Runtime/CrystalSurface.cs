using UnityEngine;
using GameActions;
namespace CrystalParkour
{
    [DisallowMultipleComponent]
    public sealed class CrystalSurface : MonoBehaviour, IActionTarget
    {
        public Collider contact;
        public string displayName="共鸣晶体";
        public bool canTether=true;
        public bool useLaunchNormal;
        public Vector3 launchNormal=Vector3.forward;
        public Vector3 WorldLaunchNormal=>transform.TransformDirection(launchNormal).normalized;
        [Min(0)] public float normalLaunchSpeed=8, upwardLaunchSpeed=5, tangentialRetention=.7f;
        [Min(0)]public float horizontalAimLaunchSpeed;
        public event System.Action<CrystalTraversal> Rebounded;
        public bool ActionTargetAlive=>isActiveAndEnabled&&contact&&contact.enabled;
        public Transform ActionRoot=>transform;
        public Vector3 ActionAimPoint=>contact?contact.bounds.center:transform.position;
        Renderer[] visuals;MaterialPropertyBlock properties;float pulse;
        void Awake(){if(!contact)contact=GetComponent<Collider>();visuals=GetComponentsInChildren<Renderer>(true);properties=new MaterialPropertyBlock();}
        public void ReceiveActionHit(float damage,Vector3 source,float impulse)
        {pulse=1;var player=CrystalTraversal.Find(source);if(player&&player.Strike(this))Rebounded?.Invoke(player);}
        void Update(){if(pulse<=0)return;pulse=Mathf.MoveTowards(pulse,0,Time.deltaTime*3);foreach(var r in visuals){r.GetPropertyBlock(properties);properties.SetFloat("_ImpactPulse",pulse);r.SetPropertyBlock(properties);}}
        void OnDrawGizmosSelected(){if(!contact)contact=GetComponent<Collider>();if(contact){Gizmos.color=new Color(.65f,.4f,1);Gizmos.DrawWireCube(contact.bounds.center,contact.bounds.size);}}
    }
}
