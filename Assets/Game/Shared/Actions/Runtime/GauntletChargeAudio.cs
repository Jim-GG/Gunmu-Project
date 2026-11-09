using UnityEngine;
using FirstPerson;
namespace GameActions
{
    // The provided three-stage recording is one continuous charge timeline.
    [DefaultExecutionOrder(100)]public sealed class GauntletChargeAudio:MonoBehaviour
    {
        public AudioSource stages,fullLoop;
        public float seekTolerance=.09f;
        GauntletCombat combat;
        public void EnsureSources()
        {
            if(!stages)stages=Create("Gauntlet Three Stage Charge",false);
            if(!fullLoop)fullLoop=Create("Gauntlet Full Charge Loop",true);
        }
        AudioSource Create(string name,bool loop)
        {
            var t=transform.Find(name);var go=t?t.gameObject:new GameObject(name);go.transform.SetParent(transform,false);
            var s=go.GetComponent<AudioSource>();if(!s)s=go.AddComponent<AudioSource>();s.loop=loop;s.playOnAwake=false;s.spatialBlend=0;s.volume=.65f;s.priority=40;return s;
        }
        void LateUpdate()=>Sync();
        public void Sync()
        {
            if(!combat)combat=GetComponent<GauntletCombat>();EnsureSources();
            if(!combat||!combat.enabled){Stop();return;}
            bool charging=combat.State==GauntletCombat.Phase.Charging||combat.State==GauntletCombat.Phase.ReleasePending;
            if(combat.State==GauntletCombat.Phase.Full)
            {
                if(stages.isPlaying)stages.Stop();if(fullLoop.clip&&!fullLoop.isPlaying)fullLoop.Play();return;
            }
            if(fullLoop.isPlaying)fullLoop.Stop();
            if(!charging||!stages.clip){if(stages.isPlaying)stages.Stop();return;}
            float duration=Mathf.Max(.01f,combat.policy.full/combat.policy.framesPerSecond);
            float time=Mathf.Clamp01(combat.ChargeSeconds/duration)*stages.clip.length;
            time=Mathf.Clamp(time,0,Mathf.Max(0,stages.clip.length-.005f));
            float speed=combat.actions&&combat.actions.Current!=null?combat.actions.Current.Motion.speed:1;
            stages.pitch=Mathf.Clamp(stages.clip.length/duration*speed,.1f,3);
            if(!stages.isPlaying){stages.Play();stages.time=time;}
            else if(Mathf.Abs(stages.time-time)>seekTolerance)stages.time=time;
        }
        public void Stop(){if(stages)stages.Stop();if(fullLoop)fullLoop.Stop();}
        void OnDisable()=>Stop();
    }
}
