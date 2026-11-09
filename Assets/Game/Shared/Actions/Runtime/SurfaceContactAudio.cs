using System;
using System.Collections.Generic;
using UnityEngine;
namespace GameActions
{
    // A bounded contact-voice pool owned by the attacker, never by floor tiles.
    public sealed class SurfaceContactAudio:MonoBehaviour
    {
        public Transform attachment;
        public AudioSource[] sources=Array.Empty<AudioSource>();
        [Range(0,1)]public float volume=.5f;
        [Min(0)]public float interval=.12f;
        public AudioClip LastClip{get;private set;}
        public int ContactsPlayed{get;private set;}
        readonly Dictionary<int,float> next=new Dictionary<int,float>();int cursor;
        public void EnsureSources()
        {
            if(sources!=null&&sources.Length>0&&Array.TrueForAll(sources,s=>s))return;
            sources=new AudioSource[4];var group=new GameObject("Hitbox Contact Audio");group.transform.SetParent(attachment?attachment:transform,false);
            for(int i=0;i<sources.Length;i++)
            {
                var go=new GameObject("Contact voice "+(i+1));go.transform.SetParent(group.transform,false);
                var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=.6f;s.minDistance=3;s.maxDistance=80;sources[i]=s;
            }
        }
        public bool Contact(Collider target,Vector3 point,bool fist)
        {
            if(!target||target.isTrigger)return false;int key=SurfaceAudio.Identity(target);
            if(next.TryGetValue(key,out var deadline)&&Time.time<deadline)return false;
            var clip=SurfaceAudio.ContactClip(target,fist);if(!clip)return false;
            EnsureSources();AudioSource source=null;foreach(var candidate in sources)if(!candidate.isPlaying){source=candidate;break;}
            if(!source){source=sources[cursor++%sources.Length];source.Stop();}
            source.transform.position=point;source.pitch=UnityEngine.Random.Range(.95f,1.05f);source.PlayOneShot(clip,volume);
            LastClip=clip;ContactsPlayed++;if(next.Count>128)next.Clear();next[key]=Time.time+interval;return true;
        }
        void OnEnable(){next.Clear();}
        void OnDisable(){if(sources!=null)foreach(var s in sources)if(s)s.Stop();next.Clear();}
    }
}
