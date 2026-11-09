using UnityEngine;
namespace UDK7M
{
    [RequireComponent(typeof(AudioSource))]public sealed class MachineLoopAudio:MonoBehaviour
    {
        public bool drill;public AudioSource source;UDK7MBoss boss;
        void Start(){source=GetComponent<AudioSource>();boss=GetComponentInParent<UDK7MBoss>();source.loop=true;source.playOnAwake=false;}
        void Update(){bool play=!boss||boss.ActionTargetAlive&&(!drill||boss.DrillSpinning&&boss.State!=BossState.DrillCapture);if(play&&!source.isPlaying&&source.clip)source.Play();if(!play&&source.isPlaying)source.Stop();}
        void OnDisable(){if(source)source.Stop();}
    }
}
