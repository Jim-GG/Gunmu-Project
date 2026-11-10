using UnityEngine;
namespace UDK7M
{
    // Shared by both phases and both preparation animations, with no extra charge layer.
    [RequireComponent(typeof(AudioSource))]public sealed class MachinePrepareAudio:MonoBehaviour
    {
        public AudioSource source;UDK7MBoss boss;float previousTime;bool wasActive;
        public int CompletedLoops{get;private set;}
        void Start(){if(!source)source=GetComponent<AudioSource>();boss=GetComponentInParent<UDK7MBoss>();source.playOnAwake=false;source.loop=true;}
        void Update()=>Sync();
        public void Sync()
        {
            if(!source)source=GetComponent<AudioSource>();if(!boss)boss=GetComponentInParent<UDK7MBoss>();
            source.loop=true;
            bool active=boss&&boss.ActionTargetAlive&&(boss.State==BossState.SlamPrepare||boss.State==BossState.SlamHold||boss.State==BossState.ChargePrepare||boss.State==BossState.ChargeHold);
            if(active&&!wasActive){CompletedLoops=0;previousTime=0;}
            if(active&&wasActive&&source.isPlaying&&source.time+.03f<previousTime)CompletedLoops++;
            if(active&&source.clip&&!source.isPlaying)source.Play();else if(!active&&source.isPlaying)source.Stop();
            previousTime=source.time;wasActive=active;
        }
        void OnDisable(){if(source)source.Stop();}
    }
}
