using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace GameActions
{
    public enum CombatEffect { Sparks, Resonance, PurpleBlood, CrystalShards, Explosion, Dust, MetalDebris }
    public static class CombatEffects
    {
        static ParticleSystem[] pool;static int cursor;static Material material;static Mesh shard;
        static Transform root;static readonly Dictionary<int,List<CombatEffectLease>> custom=new Dictionary<int,List<CombatEffectLease>>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){pool=null;material=null;shard=null;cursor=0;root=null;custom.Clear();}
        public static void Contact(GameObject prefab,AudioClip sound,Vector3 point,float lifetime)
        {
            if(!prefab&&!sound)return;
            if(!root)Create();int key=prefab?prefab.GetInstanceID():sound.GetInstanceID();if(!custom.TryGetValue(key,out var leases))custom[key]=leases=new List<CombatEffectLease>();
            leases.RemoveAll(l=>!l);var lease=leases.FirstOrDefault(l=>!l.gameObject.activeSelf||l.Until<=Time.time);
            if(!lease&&leases.Count>=8)lease=leases.OrderBy(l=>l.Until).First();
            if(!lease){var go=prefab?Object.Instantiate(prefab,root):new GameObject("Pooled contact audio");go.transform.SetParent(root);lease=go.AddComponent<CombatEffectLease>();leases.Add(lease);}
            lease.gameObject.SetActive(false);lease.transform.position=point;lease.Until=Time.time+Mathf.Max(.05f,lifetime);lease.gameObject.SetActive(true);
            foreach(var p in lease.GetComponentsInChildren<ParticleSystem>()){p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);p.Play();}
            if(sound){var source=lease.GetComponent<AudioSource>();if(!source)source=lease.gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=1;source.maxDistance=25;source.playOnAwake=false;source.PlayOneShot(sound);lease.Until=Mathf.Max(lease.Until,Time.time+sound.length);}
        }
        public static void Emit(CombatEffect kind,Vector3 position,Vector3 normal,float size=1)
        {
            if(pool==null||pool.Length==0||!pool[0])Create();
            var p=pool[cursor++%pool.Length];p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);p.transform.position=position;
            var main=p.main;main.startLifetime=kind==CombatEffect.Sparks?.25f:kind==CombatEffect.PurpleBlood?.6f:1.1f;main.startSpeed=(kind==CombatEffect.Sparks?3:kind==CombatEffect.Explosion?9:4)*size;main.gravityModifier=kind==CombatEffect.Resonance?0:.7f;
            main.startSize=(kind==CombatEffect.Sparks?.035f:kind==CombatEffect.CrystalShards?.09f:kind==CombatEffect.Resonance?.45f:.18f)*size;
            main.startColor=kind==CombatEffect.PurpleBlood?new Color(.55f,.04f,.65f):kind==CombatEffect.Resonance?new Color(.2f,1,1):kind==CombatEffect.CrystalShards?new Color(.55f,.25f,1):kind==CombatEffect.Explosion?new Color(1,.35f,.03f):kind==CombatEffect.Dust?new Color(.32f,.28f,.24f):new Color(1,.65f,.12f);
            if(kind==CombatEffect.MetalDebris){main.startColor=new Color(.16f,.13f,.1f);main.startLifetime=2;main.startSpeed=5*size;main.startSize=.13f*size;}
            var renderer=p.GetComponent<ParticleSystemRenderer>();bool mesh=kind==CombatEffect.CrystalShards||kind==CombatEffect.MetalDebris;renderer.renderMode=mesh?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;if(mesh)renderer.mesh=shard;
            p.Play();p.Emit(kind==CombatEffect.Sparks?9:kind==CombatEffect.Explosion?80:kind==CombatEffect.Resonance?90:30);
        }
        static void Create()
        {
            var owner=new GameObject("Combat VFX Pool");root=owner.transform;pool=new ParticleSystem[24];
            material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.renderQueue=3000;material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shard=new Mesh{name="Cosmetic crystal shard"};shard.vertices=new[]{new Vector3(0,2,0),new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,0,.5f)};shard.triangles=new[]{0,2,1,0,3,2,0,1,3,1,2,3};shard.RecalculateNormals();
            for(int i=0;i<pool.Length;i++)
            {
                var go=new GameObject("Pooled contact effect");go.transform.SetParent(root);var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;main.playOnAwake=false;main.loop=false;main.maxParticles=150;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=p.emission;emission.enabled=false;var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;go.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;pool[i]=p;
            }
        }
    }
}
