using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UDK7M;
namespace GameActions
{
    [Serializable] public class ObjectSound
    {
        public UDK7MSound cue;
        public AudioClip[] clips=Array.Empty<AudioClip>();
        [Range(0,1)] public float volume=.65f;
        [Min(0)] public float cooldown=.12f;
        [Range(0,1)] public float spatialBlend=.45f;
        [Min(.1f)] public float minDistance=8,maxDistance=100;
        [Range(0,256)]public int priority=80;
    }
    // The owner retains its source. No detached global one-shot objects are created.
    [RequireComponent(typeof(AudioSource))] public sealed class ObjectAudio:MonoBehaviour
    {
        public ObjectSound[] sounds=Array.Empty<ObjectSound>();
        public AudioSource source;
        public AudioClip LastClip {get;private set;}
        readonly Dictionary<UDK7MSound,float> next=new Dictionary<UDK7MSound,float>();
        public bool Play(UDK7MSound cue,Vector3 point)
        {
            var sound=Array.Find(sounds,s=>s!=null&&s.cue==cue);
            if(sound==null||next.TryGetValue(cue,out var deadline)&&Time.time<deadline)return false;
            var clips=sound.clips.Where(c=>c).ToArray();if(clips.Length==0)return false;
            if(!source||source.transform==transform)
            {
                var go=new GameObject("Event AudioSource");go.transform.SetParent(transform,false);
                source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
                source.minDistance=2;source.maxDistance=65;
            }
            source.transform.position=point;source.pitch=UnityEngine.Random.Range(.96f,1.04f);
            source.spatialBlend=sound.spatialBlend;source.rolloffMode=AudioRolloffMode.Linear;
            source.minDistance=sound.minDistance;source.maxDistance=Mathf.Max(sound.minDistance,sound.maxDistance);source.priority=sound.priority;
            LastClip=clips[UnityEngine.Random.Range(0,clips.Length)];source.PlayOneShot(LastClip,sound.volume);
            next[cue]=Time.time+sound.cooldown;return true;
        }
        void OnEnable(){next.Clear();}
        void OnDisable(){if(source)source.Stop();next.Clear();}
    }
}
