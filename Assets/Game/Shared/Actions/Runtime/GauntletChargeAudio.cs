using UnityEngine;
using FirstPerson;
namespace GameActions
{
    // The provided three-stage recording is one continuous charge timeline.
    [DefaultExecutionOrder(100)]public sealed class GauntletChargeAudio:MonoBehaviour
    {
        public AudioSource stages,fullLoop;
        public float seekTolerance=.09f;
        GauntletPresentationSource state;
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
            if (!state) state = GauntletPresentationSource.Ensure(gameObject);
            EnsureSources();
            if (!state.IsUsable) { Stop(); return; }
            if (GameplayGate.IsBlocked) { stages.Pause(); fullLoop.Pause(); return; }
            if (state.IsFull)
            {
                if (stages.isPlaying) stages.Stop();
                fullLoop.UnPause();
                if (fullLoop.clip && !fullLoop.isPlaying) fullLoop.Play();
                return;
            }
            if (fullLoop.isPlaying) fullLoop.Stop();
            if (!state.IsCharging || !stages.clip) { Stop(); return; }
            float duration = Mathf.Max(.01f, state.FullDuration);
            float time = Mathf.Clamp(state.Seconds / duration * stages.clip.length, 0, Mathf.Max(0, stages.clip.length - .005f));
            stages.pitch = Mathf.Clamp(stages.clip.length / duration * state.PlaybackSpeed * Time.timeScale, .05f, 3);
            if (state.AtPartialCap) { stages.Pause(); stages.time = time; return; }
            stages.UnPause();
            if (!stages.isPlaying) { stages.Play(); stages.time = time; }
            else if (Mathf.Abs(stages.time - time) > seekTolerance) stages.time = time;
        }

        public void Stop(){if(stages)stages.Stop();if(fullLoop)fullLoop.Stop();}
        void OnDisable()=>Stop();
    }
}
