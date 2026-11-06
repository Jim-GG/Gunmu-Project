using UnityEngine;
using UnityEngine.Playables;
namespace NullBoundary.Demo
{
    public sealed class DemoIntro : MonoBehaviour
    {
        public PlayableDirector director;
        public Transform cabin;
        public GameObject intactCable,brokenCable;
        public Light ceilingLight;
        public TextMesh floorDisplay;
        public ParticleSystem[] sparks;
        public float Black {get;private set;}
        public bool Running {get;private set;}
        public float SequenceTime {get;private set;}
        float last=-1;
        public void Begin()
        {
            Running=true;Black=0;last=-1;SequenceTime=0;
            cabin.gameObject.SetActive(true);intactCable.SetActive(true);brokenCable.SetActive(false);
            foreach(var p in sparks)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            director.time=0;director.Play();Evaluate(0);
        }
        void Update()
        {
            if(Running&&director.time>=25.98)Complete();
        }
        public void Complete()
        {
            if(!Running)return;
            Running=false;director.Stop();Black=0;cabin.gameObject.SetActive(false);
            intactCable.SetActive(false);brokenCable.SetActive(false);
            foreach(var p in sparks)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            DemoFlow.Instance.IntroFinished();
        }
        public void Evaluate(float t)
        {
            if(!Running||!DemoFlow.Instance)return;
            SequenceTime=t;
            var flow=DemoFlow.Instance;var camera=flow.player.view.transform;
            float fall=Mathf.Clamp01((t-10.7f)/4.6f);
            cabin.position=new Vector3(80,-24*fall*fall,0);
            cabin.rotation=Quaternion.Euler(0,0,t>=13.5f?Mathf.Sin((t-13.5f)*2)*7:0);
            int floor=-Mathf.Clamp(2+Mathf.FloorToInt(Mathf.Max(0,t-1.5f)/1.05f),2,8);
            floorDisplay.text=t<9.75f?"LEVEL "+floor+"\nDEST  -17":"LEVEL -8\nSIGNAL LOST";
            ceilingLight.intensity=t>=8.6f?0:t>=8&&((t<8.12f)||(t>=8.28f&&t<8.42f))?.1f:2.8f;
            intactCable.SetActive(t<10.7f);brokenCable.SetActive(t>=10.7f&&t<19);
            if(last<9&&t>=9)flow.sound.Play(DemoSound.Cue.Shock,camera.position);
            if(last<9.75f&&t>=9.75f)flow.sound.Play(DemoSound.Cue.Shock,camera.position);
            if(last<10.7f&&t>=10.7f)
            {
                flow.sound.Play(DemoSound.Cue.Cable,cabin.position);
                foreach(var p in sparks)p.Play();
            }
            if(last<16.4f&&t>=16.4f)flow.sound.Play(DemoSound.Cue.Shock,camera.position);
            if(t<10)
            {
                camera.position=cabin.TransformPoint(0,1.65f,-.9f);camera.rotation=Quaternion.identity;
            }
            else if(t<13.5f)
            {
                camera.position=new Vector3(80,8,10);
                camera.rotation=Quaternion.LookRotation(cabin.position+Vector3.up-camera.position);
            }
            else if(t<19)
            {
                float up=Mathf.SmoothStep(0,1,Mathf.InverseLerp(13.5f,15.25f,t));
                float down=Mathf.SmoothStep(0,1,Mathf.InverseLerp(15.4f,16.8f,t));
                Vector3 local=new Vector3(.15f*Mathf.Sin(t*2),Mathf.Lerp(1.65f,2.7f,up)-down*2.25f,-.8f);
                camera.position=cabin.TransformPoint(local);
                camera.rotation=cabin.rotation*Quaternion.Euler(Mathf.Lerp(-48,54,down),0,4*Mathf.Sin(t));
            }
            else
            {
                cabin.gameObject.SetActive(false);
                float k=Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,25.8f,t));
                camera.position=new Vector3(0,Mathf.Lerp(.38f,1.65f,k),3);
                camera.rotation=Quaternion.Euler(Mathf.Lerp(18,0,k),0,Mathf.Lerp(9,0,k));
            }
            Black=t<16.7f?0:t<17?Mathf.InverseLerp(16.7f,17,t):t<19?1:1-Mathf.InverseLerp(19,20.3f,t);
            if(!flow.reduceMotion&&t<17)
            {
                float shake=Shake(t-9,.065f)+Shake(t-9.75f,.16f)+Shake(t-16.4f,.18f);
                camera.position+=camera.right*Mathf.Sin(t*95)*shake+camera.up*Mathf.Cos(t*83)*shake;
            }
            last=t;
        }
        static float Shake(float elapsed,float amplitude) => elapsed<0||elapsed>.55f?0:amplitude*(1-elapsed/.55f);
    }
}

