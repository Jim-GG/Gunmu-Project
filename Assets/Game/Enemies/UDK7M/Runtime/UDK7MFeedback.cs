using UnityEngine;
using Unity.Cinemachine;
using GameActions;
namespace UDK7M
{
    public sealed class UDK7MFeedback:MonoBehaviour
    {
        public UDK7MTuning tuning;public CinemachineImpulseSource source;
        public UDK7MAudio audio;
        public void Sound(UDK7MSound cue,Vector3 point,Transform follow=null)
        {
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
            if(force<=0)return;if(!source)source=GetComponent<CinemachineImpulseSource>();if(!source)source=gameObject.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition.ImpulseChannel=tuning?tuning.impulseChannel:4;source.ImpulseDefinition.ImpulseShape=CinemachineImpulseDefinition.ImpulseShapes.Rumble;
            source.ImpulseDefinition.ImpulseType=CinemachineImpulseDefinition.ImpulseTypes.Dissipating;source.ImpulseDefinition.DissipationDistance=90;source.ImpulseDefinition.ImpulseDuration=duration;
            source.GenerateImpulseAtPositionWithVelocity(position,new Vector3(.28f,1,.14f)*force);SignalsSent++;
        }
        public void Wave(Vector3 position,float radius,float duration=.85f)
        {
            CombatEffects.Emit(CombatEffect.Dust,position,Vector3.up,3);
            var go=new GameObject("Expanding resonance shockwave");go.transform.SetParent(transform);go.transform.position=position+Vector3.up*.08f;var wave=go.AddComponent<UDK7MShockwave>();wave.Begin(radius,duration);
        }
        public void Accumulate(Vector3 position,float duration)
        {
            var go=new GameObject("Energy gathering inward");go.transform.SetParent(transform);go.transform.position=position;go.AddComponent<UDK7MShockwave>().Begin(7,duration,true);
        }
        public void ClearWaves(){foreach(var wave in GetComponentsInChildren<UDK7MShockwave>())Destroy(wave.gameObject);}
    }
}
