using System;
using System.Linq;
using UnityEngine;
namespace GameActions
{
    public enum SoundSurface { Concrete,Metal,Wood,Crystal,Flesh,Plastic }
    [Serializable] public class SurfaceSounds
    {
        public SoundSurface surface;
        public AudioClip[] contacts=Array.Empty<AudioClip>(),steps=Array.Empty<AudioClip>(),landings=Array.Empty<AudioClip>();
        public AudioClip fist;
    }
    // Optional material override only. Reading a surface never creates an AudioSource.
    public sealed class SurfaceAudio:MonoBehaviour
    {
        public SoundSurface surface;
        public SurfaceSoundLibrary library;
        public AudioClip[] overrides=Array.Empty<AudioClip>();
        static SurfaceSoundLibrary defaults;
        public static SurfaceAudio Tag(Collider c)=>c?c.GetComponentInParent<SurfaceAudio>():null;
        public static SoundSurface Kind(Collider c){var tag=Tag(c);return tag?tag.surface:Classify(c);}
        public static int Identity(Collider c)
        {
            var actor=c.GetComponentInParent<IActionTarget>();if(actor!=null)return actor.ActionRoot.GetInstanceID();
            var tag=Tag(c);return tag?tag.GetInstanceID():c.GetInstanceID();
        }
        public static SurfaceSounds Preset(Collider c)
        {
            var tag=Tag(c);if(!defaults)defaults=Resources.Load<SurfaceSoundLibrary>("SurfaceSounds");
            var data=tag&&tag.library?tag.library:defaults;
            return data?Array.Find(data.surfaces,s=>s.surface==Kind(c)):null;
        }
        public static AudioClip ContactClip(Collider c,bool fist)
        {
            var tag=Tag(c);var preset=Preset(c);
            if(tag&&tag.overrides!=null&&tag.overrides.Length>0)return Pick(tag.overrides);
            return fist&&preset?.fist?preset.fist:Pick(preset?.contacts);
        }
        public static AudioClip Pick(AudioClip[] clips)
        {
            if(clips==null||clips.Length==0)return null;
            int first=UnityEngine.Random.Range(0,clips.Length);
            for(int i=0;i<clips.Length;i++)if(clips[(first+i)%clips.Length])return clips[(first+i)%clips.Length];return null;
        }
        public static SoundSurface Classify(Collider c)
        {
            if(!c)return SoundSurface.Concrete;
            if(c.GetComponentInParent<CrystalParkour.CrystalSurface>())return SoundSurface.Crystal;
            if(c.GetComponentInParent<Resonator.AI.ResonatorEnemy>())return SoundSurface.Flesh;
            if(c.GetComponentInParent<UDK7M.UDK7MBoss>())return SoundSurface.Metal;
            var r=c.GetComponent<Renderer>()??c.GetComponentInChildren<Renderer>()??c.GetComponentInParent<Renderer>();string name=(c.name+" "+(r&&r.sharedMaterial?r.sharedMaterial.name:"")).ToLowerInvariant();
            if(new[]{"wood","木"}.Any(name.Contains))return SoundSurface.Wood;
            if(new[]{"crystal","glass","水晶"}.Any(name.Contains))return SoundSurface.Crystal;
            if(new[]{"metal","steel","iron","titanium","machined","brass","aluminium","aluminum","rail","grate","bulkhead","hatch","vent","pipe","护栏","金属","diamond"}.Any(name.Contains))return SoundSurface.Metal;
            if(name.Contains("plastic"))return SoundSurface.Plastic;return SoundSurface.Concrete;
        }
    }
}
