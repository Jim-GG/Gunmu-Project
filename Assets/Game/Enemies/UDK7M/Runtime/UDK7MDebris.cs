using UnityEngine;
using GameActions;
using Resonator.AI;
using CrystalParkour;
namespace UDK7M
{
    [RequireComponent(typeof(Rigidbody))]public sealed class UDK7MDebris:MonoBehaviour
    {
        UDK7MArena arena;Rigidbody body;bool crystal,landed,hitPlayer,energyGranted,large;float expires;static Mesh mesh;CrystalSurface surface;
        public float SpawnTime {get;private set;}
        public bool IsCrystal=>crystal;public bool Landed=>landed;
        public void Begin(UDK7MArena arena,Vector3 position,bool crystal,bool large=false)
        {
            this.arena=arena;this.crystal=crystal;this.large=large;landed=hitPlayer=energyGranted=false;SpawnTime=Time.time;expires=Time.time+(large?600:30);
            transform.SetPositionAndRotation(position,Random.rotation);transform.localScale=Vector3.one*(large?arena.tuning.centralCrystalScale:crystal?.9f:.8f);gameObject.SetActive(true);
            if(!mesh){mesh=new Mesh{name="Falling fracture"};mesh.vertices=new[]{Vector3.up,Vector3.down*.6f,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};mesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};mesh.RecalculateNormals();}
            GetComponent<MeshFilter>().sharedMesh=mesh;GetComponent<Renderer>().sharedMaterial=crystal?arena.crystalMaterial:arena.rockMaterial;body=GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=true;body.mass=8;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearVelocity=Vector3.down*12;body.angularVelocity=Random.insideUnitSphere*3;
            var contactAudio=GetComponent<SurfaceAudio>();if(contactAudio)contactAudio.surface=crystal?SoundSurface.Crystal:SoundSurface.Concrete;
            foreach(var collider in arena.boss.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(GetComponent<Collider>(),collider,true);
            surface=GetComponent<CrystalSurface>();if(crystal&&!surface)surface=gameObject.AddComponent<CrystalSurface>();
            if(surface)
            {
                surface.enabled=surface.canTether=crystal;surface.contact=GetComponent<Collider>();surface.displayName=large?"中央共振弹射晶体":"坠落共振晶体";surface.useLaunchNormal=true;surface.launchNormal=transform.InverseTransformDirection(Vector3.up);surface.normalLaunchSpeed=0;surface.upwardLaunchSpeed=large?arena.tuning.centralLaunchSpeed:15;surface.horizontalAimLaunchSpeed=large?10:0;
                surface.Rebounded-=Rebounded;surface.Rebounded+=Rebounded;
                if(arena.feedback.audio)arena.feedback.audio.Observe(surface);
                if(crystal)Physics.IgnoreCollision(GetComponent<Collider>(),arena.player.GetComponent<CharacterController>(),true);
            }
        }
        void Rebounded(CrystalTraversal player){if(energyGranted)return;energyGranted=true;player.combat.AddEnergy(1);}
        void OnCollisionEnter(Collision collision)
        {
            if(!arena)return;var target=collision.collider.GetComponentInParent<ResonatorTarget>();
            if(target&&!hitPlayer&&!landed){hitPlayer=true;target.ReceiveStrike(new ActionDamage(arena.tuning.rockDamage,transform.position,1,.18f,true,.5f));}
            if(collision.contacts.Length>0&&collision.contacts[0].normal.y>.35f)
            {landed=true;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;expires=Time.time+(large?600:crystal?60:8);if(surface)surface.launchNormal=transform.InverseTransformDirection(Vector3.up);CombatEffects.Emit(CombatEffect.Dust,transform.position,Vector3.up);arena.feedback.Shake(transform.position,large?arena.tuning.heavySlamShake:arena.tuning.lightSlamShake);arena.feedback.Sound(crystal?UDK7MSound.CrystalLanding:UDK7MSound.RockLanding,transform.position,transform);}
        }
        void Update()
        {
            if(Time.time>=expires||transform.position.y< -8){gameObject.SetActive(false);return;}
        }
    }
}
