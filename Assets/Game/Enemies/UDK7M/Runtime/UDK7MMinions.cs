using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using GameActions;
using Resonator.AI;
namespace UDK7M
{
    public sealed class UDK7MMinions:MonoBehaviour
    {
        public UDK7MArena arena;public GameObject prefab;public ResonatorTuning profile;public Transform[] spawnPoints;
        public IReadOnlyList<ResonatorEnemy> Actors=>actors;
        public int Living=>actors.Count(e=>e&&e.gameObject.activeInHierarchy&&e.ActionTargetAlive);
        public bool Running {get;private set;}
        public float LastRespawnDelay {get;private set;}
        public DamageCause LastDeathCause {get;private set;}
        sealed class Slot {public ResonatorEnemy actor;public float ready;}
        readonly List<ResonatorEnemy> actors=new List<ResonatorEnemy>();readonly List<Slot> slots=new List<Slot>();
        readonly Dictionary<ResonatorEnemy,float> deaths=new Dictionary<ResonatorEnemy,float>(),reserved=new Dictionary<ResonatorEnemy,float>(),returned=new Dictionary<ResonatorEnemy,float>();
        public void Activate(){if(Running)return;Running=true;while(slots.Count<arena.tuning.livingMinions)slots.Add(new Slot());}
        public void Reserve(ResonatorEnemy e)=>reserved[e]=Time.time+.5f;
        void Update(){Tick();}
        public void Tick()
        {
            if(!Running)return;
            foreach(var slot in slots)if(!slot.actor&&Time.time>=slot.ready&&TrySpawnPoint(out var point))Spawn(slot,point);
            var corpses=deaths.Keys.Where(e=>e&&e.gameObject.activeInHierarchy).OrderBy(e=>deaths[e]).ToArray();int excess=corpses.Length-arena.tuning.maximumCorpses;
            foreach(var e in corpses){if(reserved.TryGetValue(e,out var until)&&until>Time.time)continue;if(Time.time-deaths[e]>arena.tuning.corpseLifetime||excess>0){Recycle(e);excess--;}}
        }
        bool TrySpawnPoint(out Vector3 point)
        {
            point=default;if(!arena.player||spawnPoints==null||spawnPoints.Length==0)return false;var camera=arena.player.eyes?arena.player.eyes.GetComponentInChildren<Camera>():Camera.main;
            int first=Random.Range(0,spawnPoints.Length);
            for(int n=0;n<spawnPoints.Length;n++)
            {
                var candidate=spawnPoints[(first+n)%spawnPoints.Length];if(!candidate||!NavMesh.SamplePosition(candidate.position,out var nav,1,NavMesh.AllAreas))continue;
                var p=nav.position;if(Vector3.Distance(p,arena.player.transform.position)<arena.tuning.spawnMinimumDistance)continue;
                if(camera){var v=camera.WorldToViewportPoint(p+Vector3.up);bool onScreen=v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1;if(onScreen&&ActionVisibility.Clear(camera.transform.position,p+Vector3.up,arena.player.transform,null,~0))continue;}
                if(Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.45f,.28f,~0,QueryTriggerInteraction.Ignore))continue;
                point=p;return true;
            }
            return false;
        }
        void Spawn(Slot slot,Vector3 point)
        {
            var enemy=actors.FirstOrDefault(e=>e&&!e.gameObject.activeSelf&&(!returned.TryGetValue(e,out var time)||Time.time-time>.1f));
            if(!enemy)
            {
                var go=Instantiate(prefab,transform);go.name="Pooled Resonator "+actors.Count;enemy=go.GetComponent<ResonatorEnemy>();go.SetActive(false);actors.Add(enemy);var captured=enemy;enemy.Killed+=cause=>Killed(captured,cause);
            }
            enemy.transform.SetPositionAndRotation(point,Quaternion.LookRotation(Vector3.ProjectOnPlane(arena.player.transform.position-point,Vector3.up)));
            enemy.tuning=profile;enemy.target=arena.player;enemy.gameObject.SetActive(true);enemy.Initialize(RestActivity.FeignDeath);enemy.BeginBossWake();slot.actor=enemy;
        }
        void Killed(ResonatorEnemy enemy,DamageCause cause)
        {
            if(deaths.ContainsKey(enemy))return;deaths[enemy]=Time.time;
            var slot=slots.FirstOrDefault(s=>s.actor==enemy);if(slot==null)return;slot.actor=null;var range=cause==DamageCause.Resonance?arena.tuning.resonanceRespawn:arena.tuning.ordinaryRespawn;LastRespawnDelay=Random.Range(range.x,range.y);LastDeathCause=cause;slot.ready=Time.time+LastRespawnDelay;
        }
        public void Recycle(ResonatorEnemy enemy)
        {
            if(!enemy)return;deaths.Remove(enemy);reserved.Remove(enemy);returned[enemy]=Time.time;enemy.ResetForPool();enemy.gameObject.SetActive(false);
        }
        public void StopAndClear(){Running=false;slots.Clear();foreach(var e in actors)if(e&&e.gameObject.activeSelf)Recycle(e);deaths.Clear();reserved.Clear();}
    }
}
