using System;
using System.Linq;
using UnityEngine;
namespace UDK7M
{
    public enum UDK7MSound { SlamPrepare,ChargePrepare,ChargeStart,SlamLight,SlamHeavy,ChargeImpact,TracksLoop,DrillLoop,ChargeLoop,DrillCapture,ResonanceBurst,OverloadCharge,PillarShatter,PillarRegrow,RockWarning,RockLanding,CrystalLanding,TetherStart,TetherAttach,CrystalRebound,PureCrystalCharge,GroundPoundStart,GroundPoundImpact,Paralyzed,CycleRestart,Crush,Defeat,TripleFinalImpact,GauntletSwing,ParryImpact,ChargeTier }
    [Serializable]public sealed class UDK7MSoundEntry
    {
        public UDK7MSound cue;public AudioClip[] clips=Array.Empty<AudioClip>();
        [Range(0,1)]public float volume=.8f;public Vector2 pitch=new Vector2(.95f,1.05f);
        [Min(0)]public float cooldown=.08f;public bool followSource;
        [Min(.1f)]public float minDistance=2,maxDistance=65;
    }
    [CreateAssetMenu(menuName="UDK7M/Audio Profile")]
    public sealed class UDK7MAudioProfile:ScriptableObject
    {
        [Range(0,1)]public float masterVolume=1;
        [Range(6,32)]public int maximumVoices=16;
        public UDK7MSoundEntry[] sounds=Defaults();
        public UDK7MSoundEntry Find(UDK7MSound cue)=>sounds?.FirstOrDefault(s=>s!=null&&s.cue==cue);
        static UDK7MSoundEntry[] Defaults()=>((UDK7MSound[])Enum.GetValues(typeof(UDK7MSound))).Select(c=>new UDK7MSoundEntry{cue=c,followSource=c==UDK7MSound.TracksLoop||c==UDK7MSound.DrillLoop||c==UDK7MSound.ChargeLoop||c==UDK7MSound.TetherStart||c==UDK7MSound.TetherAttach||c==UDK7MSound.PureCrystalCharge||c==UDK7MSound.OverloadCharge||c==UDK7MSound.DrillCapture}).ToArray();
    }
}

