using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrystalParkour;
namespace UDK7M
{
    [DefaultExecutionOrder(130)]public sealed class UDK7MAudio:MonoBehaviour
    {
        public UDK7MAudioProfile profile;public UDK7MArena arena;
        sealed class Voice {public AudioSource source;public Transform follow;public Vector3 offset;public float end;public bool looping;public UDK7MSound cue;}
        readonly List<Voice> voices=new List<Voice>();readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        readonly Dictionary<CrystalSurface,Action<CrystalTraversal>> surfaces=new Dictionary<CrystalSurface,Action<CrystalTraversal>>();CrystalTraversal traversal;
        public int AllocatedVoices=>voices.Count;public int PlayingVoices=>voices.Count(v=>v.source&&v.source.isPlaying);
        void Start()
        {
            if(!arena)arena=GetComponent<UDK7MArena>();if(!arena)return;traversal=arena.player.GetComponent<CrystalTraversal>();
            if(traversal){traversal.TetherStarted+=TetherStart;traversal.AttachedToSurface+=TetherAttach;}
            foreach(var surface in gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CrystalSurface>(true)))Observe(surface);
        }
        public void Observe(CrystalSurface surface)
        {
            if(!surface||surfaces.ContainsKey(surface))return;Action<CrystalTraversal> callback=p=>PlayCue(UDK7MSound.CrystalRebound,surface.contact?surface.contact.bounds.center:surface.transform.position,p.transform);surfaces[surface]=callback;surface.Rebounded+=callback;
        }
        void TetherStart(CrystalSurface s)=>PlayCue(UDK7MSound.TetherStart,arena.player.EyePosition,arena.player.transform);
        void TetherAttach(CrystalSurface s)=>PlayCue(UDK7MSound.TetherAttach,arena.player.EyePosition,arena.player.transform);
        public bool PlayCue(UDK7MSound cue,Vector3 position,Transform follow=null)
        {
            var entry=profile?profile.Find(cue):null;if(entry==null||entry.clips==null)return false;var clips=entry.clips.Where(c=>c).ToArray();if(clips.Length==0)return false;
            string key=((int)cue)+":"+(follow?follow.GetInstanceID():0);if(cooldowns.TryGetValue(key,out float until)&&Time.time<until)return false;cooldowns[key]=Time.time+entry.cooldown;
            var voice=Allocate();Begin(voice,entry,clips[UnityEngine.Random.Range(0,clips.Length)],position,entry.followSource?follow:null,false);return true;
        }
        Voice Allocate()
        {
            var voice=voices.FirstOrDefault(v=>!v.source.isPlaying&&!v.looping);
            if(voice==null&&voices.Count<Mathf.Clamp(profile.maximumVoices,6,32)){var go=new GameObject("Pooled UDK7M audio");go.transform.SetParent(transform);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;voice=new Voice{source=source};voices.Add(voice);}
            if(voice==null)voice=voices.Where(v=>!v.looping).OrderBy(v=>v.end).FirstOrDefault()??voices[0];voice.source.Stop();return voice;
        }
        void Begin(Voice voice,UDK7MSoundEntry entry,AudioClip clip,Vector3 point,Transform follow,bool loop)
        {
            voice.cue=entry.cue;voice.follow=follow;voice.offset=follow?follow.InverseTransformPoint(point):Vector3.zero;voice.looping=loop;
            var source=voice.source;source.transform.position=point;source.clip=clip;source.loop=loop;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=entry.minDistance;source.maxDistance=Mathf.Max(entry.minDistance,entry.maxDistance);source.volume=entry.volume*profile.masterVolume;source.pitch=UnityEngine.Random.Range(Mathf.Max(.1f,entry.pitch.x),Mathf.Max(.1f,entry.pitch.y));voice.end=Time.time+clip.length/source.pitch;source.Play();
        }
        void Loop(UDK7MSound cue,bool active,Transform anchor)
        {
            var existing=voices.FirstOrDefault(v=>v.looping&&v.cue==cue);var entry=profile?profile.Find(cue):null;var clip=entry?.clips?.FirstOrDefault(c=>c);
            if(!active||!clip||!anchor){if(existing!=null){existing.source.Stop();existing.looping=false;existing.follow=null;}return;}
            if(existing!=null)return;Begin(Allocate(),entry,clip,anchor.position,entry.followSource?anchor:null,true);
        }
        void LateUpdate()
        {
            if(arena&&arena.boss)
            {
                var boss=arena.boss;bool enabled=boss.ActionTargetAlive&&boss.State!=BossState.DrillCapture&&boss.State!=BossState.Paralyzed&&boss.State!=BossState.OverloadCharge;
                Loop(UDK7MSound.TracksLoop,enabled&&(boss.motor.ActualVelocity.sqrMagnitude>.04f||boss.State==BossState.Turning),boss.transform);
                Loop(UDK7MSound.DrillLoop,enabled&&boss.DrillSpinning,boss.drill);Loop(UDK7MSound.ChargeLoop,enabled&&(boss.State==BossState.SlamHold||boss.State==BossState.ChargeHold),boss.transform);
            }
            foreach(var voice in voices)if(voice.source.isPlaying)
            {
                var entry=profile?profile.Find(voice.cue):null;if(entry!=null)voice.source.volume=entry.volume*profile.masterVolume;
                if(voice.follow){voice.source.transform.position=voice.follow.TransformPoint(voice.offset);if(!voice.follow.gameObject.activeInHierarchy){voice.source.Stop();voice.looping=false;voice.follow=null;}}
            }
        }
        public void StopLoops(){foreach(var v in voices)if(v.looping){v.source.Stop();v.looping=false;v.follow=null;}}
        public void StopAll(){foreach(var v in voices){if(v.source)v.source.Stop();v.looping=false;v.follow=null;}cooldowns.Clear();}
        void OnDisable()=>StopAll();
        void OnDestroy(){if(traversal){traversal.TetherStarted-=TetherStart;traversal.AttachedToSurface-=TetherAttach;}foreach(var pair in surfaces)if(pair.Key)pair.Key.Rebounded-=pair.Value;}
    }
}
