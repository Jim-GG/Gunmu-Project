using UnityEngine;
namespace UDK7M
{
    public sealed class UDK7MShockwave:MonoBehaviour,GameActions.ICombatWave
    {
        [Min(12)]public int segments=96;
        public float previewRadius=8,previewDuration=.85f;
        public AnimationCurve radiusOverLifetime=new AnimationCurve(new Keyframe(0,0,2,2),new Keyframe(1,1,0,0));
        public AnimationCurve widthOverLifetime=AnimationCurve.Linear(0,.5f,1,.02f);
        public Gradient colorOverLifetime=DefaultColor();
        LineRenderer line;float radius,duration,time;Material ownedMaterial;bool collapse,initialized;
        static Gradient DefaultColor(){var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(new Color(.2f,.85f,1),0),new GradientColorKey(new Color(.2f,.85f,1),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});return g;}
        void Start(){if(!initialized)Begin(previewRadius,previewDuration);}
        public void Begin(float radius,float duration,bool collapse=false)
        {
            initialized=true;time=0;this.radius=radius;this.duration=duration;this.collapse=collapse;
            line=GetComponent<LineRenderer>();if(!line)line=gameObject.AddComponent<LineRenderer>();
            line.loop=true;line.useWorldSpace=false;line.positionCount=Mathf.Max(12,segments);
            if(!line.sharedMaterial){ownedMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));line.sharedMaterial=ownedMaterial;}
            Draw(0);
        }
        void Update(){if(!initialized)return;time+=Time.deltaTime;float t=Mathf.Clamp01(time/Mathf.Max(.05f,duration));Draw(t);if(t>=1)Destroy(gameObject);}
        void Draw(float t)
        {
            float r=Mathf.Lerp(.2f,radius,collapse?1-t:radiusOverLifetime.Evaluate(t));int count=line.positionCount;
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;line.SetPosition(i,new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r));}
            line.widthMultiplier=widthOverLifetime.Evaluate(t);line.startColor=line.endColor=colorOverLifetime.Evaluate(t);
        }
        void OnDestroy(){if(ownedMaterial)Destroy(ownedMaterial);}
    }
}
