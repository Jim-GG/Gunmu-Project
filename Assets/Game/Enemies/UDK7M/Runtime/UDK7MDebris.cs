using UnityEngine;
using GameActions;
using Resonator.AI;
using CrystalParkour;
namespace UDK7M
{
    [RequireComponent(typeof(Rigidbody))]public sealed class UDK7MDebris:MonoBehaviour
    {
        UDK7MArena arena;Rigidbody body;bool crystal,landed,hitPlayer,energyGranted,large;float expires;static Mesh mesh;CrystalSurface surface;
        MeshRenderer art;MeshFilter artMesh;MeshCollider artCollider;
        public Renderer VisualRenderer=>art;
        public Mesh VisualMesh=>artMesh?artMesh.sharedMesh:null;
        public Collider PhysicalCollider=>artCollider&&artCollider.enabled?artCollider:GetComponent<Collider>();
        bool ConfigureArt()
        {
            var profile=CombatFeedbackProfile.Load();if(!profile)return false;
            GameObject prefab=crystal?(large?profile.squatCrystal:Random.value<.5f?profile.fracturedCrystal:profile.squatCrystal):null;
            var source=prefab?prefab.GetComponentInChildren<MeshFilter>():null;
            int rockIndex=!crystal&&profile.rockMeshes.Length>0?Random.Range(0,profile.rockMeshes.Length):-1;
            Mesh chosen=source?source.sharedMesh:rockIndex>=0?profile.rockMeshes[rockIndex]:null;
            if(!chosen)return false;
            if(!art){var go=new GameObject("Falling mesh art");go.transform.SetParent(transform,false);artMesh=go.AddComponent<MeshFilter>();art=go.AddComponent<MeshRenderer>();}
            GetComponent<Renderer>().enabled=false;artMesh.sharedMesh=chosen;art.enabled=true;
            Quaternion rotation=prefab?prefab.transform.localRotation:rockIndex>=0&&rockIndex<profile.rockFaceDown.Length?profile.rockFaceDown[rockIndex]:Quaternion.identity;
            var raw=chosen.bounds;var bounds=new Bounds(rotation*raw.center,Vector3.zero);
            for(int i=0;i<8;i++){var corner=raw.center+Vector3.Scale(raw.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));bounds.Encapsulate(rotation*corner);}
            float diameter=large?profile.centralCrystalDiameter:crystal?profile.crystalDiameter:profile.rockDiameter;
            float scale=diameter/Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
            art.transform.SetLocalPositionAndRotation(-bounds.center*scale,rotation);art.transform.localScale=Vector3.one*scale;
            // The collider shares the exact mesh and Transform with the art. A dynamic
            // rigidbody needs a convex hull; the old broad box must not remain active.
            if(!artCollider)artCollider=art.gameObject.AddComponent<MeshCollider>();
            artCollider.enabled=false;artCollider.sharedMesh=null;artCollider.convex=true;artCollider.sharedMesh=chosen;artCollider.enabled=true;
            foreach(var legacy in GetComponents<Collider>())legacy.enabled=false;
            if(crystal&&source.GetComponent<Renderer>())art.sharedMaterials=source.GetComponent<Renderer>().sharedMaterials;
            else{var materials=new Material[chosen.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=arena.rockMaterial;art.sharedMaterials=materials;}
            if(TryGetComponent<BoxCollider>(out var box)){box.center=Vector3.zero;box.size=Vector3.Max(bounds.size*scale,Vector3.one*.15f);}
            return true;
        }
        public float SpawnTime {get;private set;}
        public bool IsCrystal=>crystal;public bool Landed=>landed;
        public void Begin(UDK7MArena arena,Vector3 position,bool crystal,bool large=false)
        {
            this.arena=arena;this.crystal=crystal;this.large=large;landed=hitPlayer=energyGranted=false;SpawnTime=Time.time;expires=Time.time+(large?600:30);
            var feedback=CombatFeedbackProfile.Load();var tilt=Random.insideUnitCircle*(crystal?4:feedback?feedback.rockTiltDegrees:6);
            transform.SetPositionAndRotation(position,Quaternion.AngleAxis(Random.Range(0,360),Vector3.up)*Quaternion.Euler(tilt.x,0,tilt.y));transform.localScale=Vector3.one*(large?arena.tuning.centralCrystalScale:crystal?.9f:.8f);gameObject.SetActive(true);
            if(!mesh){mesh=new Mesh{name="Falling fracture"};mesh.vertices=new[]{Vector3.up,Vector3.down*.6f,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};mesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};mesh.RecalculateNormals();}
            if(ConfigureArt())transform.localScale=Vector3.one;
            else{if(art)art.enabled=false;if(artCollider)artCollider.enabled=false;foreach(var legacy in GetComponents<Collider>())legacy.enabled=true;GetComponent<MeshFilter>().sharedMesh=mesh;GetComponent<Renderer>().enabled=true;GetComponent<Renderer>().sharedMaterial=crystal?arena.crystalMaterial:arena.rockMaterial;}
            body=GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=true;body.mass=8;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearVelocity=Vector3.down*12;body.angularVelocity=Vector3.up*Random.Range(-.4f,.4f);
            var contactAudio=GetComponent<SurfaceAudio>();if(contactAudio)contactAudio.surface=crystal?SoundSurface.Crystal:SoundSurface.Concrete;
            foreach(var collider in arena.boss.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(PhysicalCollider,collider,true);
            Physics.IgnoreCollision(PhysicalCollider,arena.player.GetComponent<CharacterController>(),crystal);
            surface=GetComponent<CrystalSurface>();if(crystal&&!surface)surface=gameObject.AddComponent<CrystalSurface>();
            if(surface)
            {
                surface.enabled=surface.canTether=crystal;surface.contact=PhysicalCollider;surface.displayName=large?"中央共振弹射晶体":"坠落共振晶体";surface.useLaunchNormal=true;surface.launchNormal=transform.InverseTransformDirection(Vector3.up);surface.normalLaunchSpeed=0;surface.upwardLaunchSpeed=large?arena.tuning.centralLaunchSpeed:15;surface.horizontalAimLaunchSpeed=large?10:0;
                surface.Rebounded-=Rebounded;surface.Rebounded+=Rebounded;
                if(arena.feedback.audio)arena.feedback.audio.Observe(surface);
            }
        }
        void Rebounded(CrystalTraversal player){if(energyGranted)return;energyGranted=true;player.combat.AddEnergy(1);}
        void OnCollisionEnter(Collision collision)
        {
            if(!arena)return;var target=collision.collider.GetComponentInParent<ResonatorTarget>();
            if(target&&!hitPlayer&&!landed){hitPlayer=true;target.ReceiveStrike(new ActionDamage(arena.tuning.rockDamage,transform.position,1,.18f,true,.5f));}
            if(!landed&&collision.contacts.Length>0&&collision.contacts[0].normal.y>.35f&&collision.collider.GetComponentInParent<IActionTarget>()==null)
            {landed=true;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;expires=Time.time+(large?600:crystal?60:8);if(surface)surface.launchNormal=transform.InverseTransformDirection(Vector3.up);var contact=collision.contacts[0];var impact=Resources.Load<CombatEffectLibrary>("CombatVFX")?.Find(CombatEffect.ConcreteImpactHeavy);var prefab=arena.tuning.fallingImpactEffect?arena.tuning.fallingImpactEffect:impact?.prefab;float scale=large?arena.tuning.centralCrystalImpactScale:crystal?arena.tuning.fallingCrystalImpactScale:arena.tuning.fallingRockImpactScale;if(prefab)CombatEffects.Burst(prefab,contact.point+contact.normal*.025f,contact.normal,scale,impact?.lifetime??8);else CombatEffects.Emit(CombatEffect.Dust,contact.point,contact.normal);arena.feedback.Shake(transform.position,large?arena.tuning.heavySlamShake:arena.tuning.lightSlamShake);arena.feedback.Sound(crystal?UDK7MSound.CrystalLanding:UDK7MSound.RockLanding,transform.position,transform);}
        }
        void Update()
        {
            if(Time.time>=expires||transform.position.y< -8){gameObject.SetActive(false);return;}
        }
    }
}
