using UnityEngine;
using Unity.Cinemachine;
using GameActions;
namespace UDK7M
{
    public sealed class UDK7MFeedback:MonoBehaviour
    {
        public UDK7MTuning tuning;public CinemachineImpulseSource source;
        public UDK7MAudio audio;
        public GameObject shockwavePrefab,energyGatherPrefab;
        readonly System.Collections.Generic.List<GameObject> waves=new System.Collections.Generic.List<GameObject>();
        public void Sound(UDK7MSound cue,Vector3 point,Transform follow=null)
        {
            if(cue==UDK7MSound.ResonanceBurst)ScreenEffectSignals.Emit(ScreenCue.Resonance,point,1,28);
            else if(cue==UDK7MSound.Defeat)ScreenEffectSignals.Emit(ScreenCue.Explosion,point,1,55);
            var target=follow?follow:transform;
            for(var current=target;current;current=current.parent)
            {
                var local=current.GetComponent<ObjectAudio>();
                if(local&&System.Array.Exists(local.sounds,s=>s.cue==cue)){local.Play(cue,point);return;}
            }
        }
        public int SignalsSent {get;private set;}
        public void Shake(Vector3 position,float force,float duration=.28f)
        {
            if(force>0)ScreenEffectSignals.Emit(ScreenCue.Impact,position,Mathf.Clamp01(force*2),24);
            if(force<=0)return;if(!source)source=GetComponent<CinemachineImpulseSource>();if(!source)source=gameObject.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition.ImpulseChannel=tuning?tuning.impulseChannel:4;source.ImpulseDefinition.ImpulseShape=CinemachineImpulseDefinition.ImpulseShapes.Rumble;
            source.ImpulseDefinition.ImpulseType=CinemachineImpulseDefinition.ImpulseTypes.Dissipating;source.ImpulseDefinition.DissipationDistance=90;source.ImpulseDefinition.ImpulseDuration=duration;
            source.GenerateImpulseAtPositionWithVelocity(position,new Vector3(.28f,1,.14f)*force);SignalsSent++;
        }
        public void Wave(Vector3 position,float radius,float duration=.85f)
        {
            CombatEffects.Emit(CombatEffect.Dust,position,Vector3.up,3);
            SpawnWave(shockwavePrefab,"Expanding resonance shockwave",position+Vector3.up*.08f,radius,duration,false);
        }
        public void Accumulate(Vector3 position,float duration)
        {
            SpawnWave(energyGatherPrefab?energyGatherPrefab:shockwavePrefab,"Energy gathering inward",position,7,duration,true);
        }
        void SpawnWave(GameObject prefab,string name,Vector3 position,float radius,float duration,bool collapse)
        {
            var go=prefab?Instantiate(prefab,transform):new GameObject(name);go.name=name;go.transform.SetParent(transform);go.transform.position=position;waves.RemoveAll(g=>!g);waves.Add(go);
            var wave=go.GetComponent<ICombatWave>();if(!prefab&&wave==null)wave=go.AddComponent<UDK7MShockwave>();
            if(wave!=null)wave.Begin(radius,duration,collapse);else{float life=duration;foreach(var p in go.GetComponentsInChildren<ParticleSystem>())life=Mathf.Max(life,p.main.duration+p.main.startLifetime.constantMax);Destroy(go,life);}
        }
        public void ClearWaves(){foreach(var wave in waves)if(wave)Destroy(wave);waves.Clear();}
    }
}
