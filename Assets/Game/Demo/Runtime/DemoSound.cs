using UnityEngine;
namespace NullBoundary.Demo
{
    public sealed class DemoSound : MonoBehaviour
    {
        public enum Cue { Shock, Cable, Door, Crystal, Block, Release, Hit, Swing, Alarm }
        AudioClip[] clips;
        AudioSource loop;
        void Awake()
        {
            clips=new AudioClip[9];
            for(int i=0;i<clips.Length;i++)clips[i]=Make(i);
            loop=gameObject.AddComponent<AudioSource>();loop.loop=true;loop.volume=.12f;loop.clip=clips[8];
        }
        AudioClip Make(int kind)
        {
            float duration=kind==8?1.25f:kind==0?1.1f:kind==2?.75f:.38f;
            int n=Mathf.CeilToInt(22050*duration);float[] samples=new float[n];
            var random=new System.Random(170+kind);
            float[] frequency={48,210,83,510,150,68,95,240,440};
            for(int i=0;i<n;i++)
            {
                float t=i/22050f,env=kind==8?.6f:Mathf.Pow(1-t/duration,2);
                float f=frequency[kind]*(kind==8?1+.2f*Mathf.Sin(t*8):1);
                float tone=Mathf.Sin(t*f*Mathf.PI*2);
                float noise=(float)random.NextDouble()*2-1;
                samples[i]=(tone*.55f+noise*(kind==3?.03f:.35f))*env*.4f;
            }
            var clip=AudioClip.Create("NB_"+(Cue)kind,n,1,22050,false);clip.SetData(samples,0);return clip;
        }
        public void Play(Cue cue,Vector3 at)
        {
            if(clips==null)return;
            AudioSource.PlayClipAtPoint(clips[(int)cue],at,cue==Cue.Shock?.7f:.45f);
        }
        public void Step(Vector3 at)
        {
            if(clips!=null)AudioSource.PlayClipAtPoint(clips[6],at,.06f);
        }
        public void Alarm(bool value)
        {
            if(value)loop.Play();else loop.Stop();
        }
    }
}

