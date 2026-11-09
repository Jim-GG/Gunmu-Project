using UnityEngine;
namespace UDK7M
{
    // Shared by both phases and both preparation animations, with no extra charge layer.
    [RequireComponent(typeof(AudioSource))]public sealed class MachinePrepareAudio:MonoBehaviour
    {
        public AudioSource source;UDK7MBoss boss;
        void Start(){if(!source)source=GetComponent<AudioSource>();boss=GetComponentInParent<UDK7MBoss>();source.playOnAwake=false;source.loop=true;}
        void Update()=>Sync();
        public void Sync()
        {
            if(!source)source=GetComponent<AudioSource>();if(!boss)boss=GetComponentInParent<UDK7MBoss>();
            bool active=boss&&boss.ActionTargetAlive&&(boss.State==BossState.SlamPrepare||boss.State==BossState.ChargePrepare);
            if(active&&source.clip&&!source.isPlaying)source.Play();else if(!active&&source.isPlaying)source.Stop();
        }
        void OnDisable(){if(source)source.Stop();}
    }
}
