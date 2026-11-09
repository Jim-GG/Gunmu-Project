using UnityEngine;
using GameActions;
using Resonator.AI;
namespace UDK7M
{
    [RequireComponent(typeof(Rigidbody))]public sealed class UDK7MDebris:MonoBehaviour
    {
        UDK7MArena arena;Rigidbody body;bool crystal,landed,hitPlayer;float expires;static Mesh mesh;
        public float SpawnTime {get;private set;}
        public bool IsCrystal=>crystal;public bool Landed=>landed;
        public void Begin(UDK7MArena arena,Vector3 position,bool crystal)
        {
            this.arena=arena;this.crystal=crystal;landed=hitPlayer=false;SpawnTime=Time.time;expires=Time.time+30;
            transform.SetPositionAndRotation(position,Random.rotation);transform.localScale=Vector3.one*(crystal?.65f:.8f);gameObject.SetActive(true);
            if(!mesh){mesh=new Mesh{name="Falling fracture"};mesh.vertices=new[]{Vector3.up,Vector3.down*.6f,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};mesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};mesh.RecalculateNormals();}
            GetComponent<MeshFilter>().sharedMesh=mesh;GetComponent<Renderer>().sharedMaterial=crystal?arena.crystalMaterial:arena.rockMaterial;body=GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=true;body.mass=8;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearVelocity=Vector3.down*12;body.angularVelocity=Random.insideUnitSphere*3;
            foreach(var collider in arena.boss.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(GetComponent<Collider>(),collider,true);
        }
        void OnCollisionEnter(Collision collision)
        {
            if(!arena)return;var target=collision.collider.GetComponentInParent<ResonatorTarget>();
            if(target&&!hitPlayer&&!landed){hitPlayer=true;target.ReceiveStrike(new ActionDamage(arena.tuning.rockDamage,transform.position,1,.18f,true,.5f));}
            if(collision.contacts.Length>0&&collision.contacts[0].normal.y>.35f)
            {landed=true;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;expires=Time.time+(crystal?30:8);CombatEffects.Emit(CombatEffect.Dust,transform.position,Vector3.up);}
        }
        void Update()
        {
            if(Time.time>=expires||transform.position.y< -8){gameObject.SetActive(false);return;}
            if(crystal&&landed&&arena&&Vector3.Distance(transform.position,arena.player.transform.position+Vector3.up*.6f)<1.65f){arena.player.gauntlet.AddEnergy(1);CombatEffects.Emit(CombatEffect.Resonance,transform.position,Vector3.up,.5f);gameObject.SetActive(false);}
        }
    }
}
