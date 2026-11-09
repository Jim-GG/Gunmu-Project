using UnityEngine;
namespace UDK7M
{
    public sealed class UDK7MShockwave:MonoBehaviour
    {
        LineRenderer line;float radius,duration,time;Material material;bool collapse;
        public void Begin(float radius,float duration,bool collapse=false)
        {
            this.radius=radius;this.duration=duration;this.collapse=collapse;line=gameObject.AddComponent<LineRenderer>();line.loop=true;line.useWorldSpace=false;line.positionCount=96;line.widthMultiplier=.28f;
            material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));material.SetColor("_BaseColor",new Color(.25f,.9f,1,.8f));line.sharedMaterial=material;
        }
        void Update()
        {
            time+=Time.deltaTime;float t=Mathf.Clamp01(time/Mathf.Max(.05f,duration)),r=Mathf.Lerp(.2f,radius,1-Mathf.Pow(1-t,2));
            if(collapse)r=Mathf.Lerp(radius,.2f,t);
            for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;line.SetPosition(i,new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r));}
            line.widthMultiplier=Mathf.Lerp(.5f,.02f,t);line.startColor=line.endColor=new Color(.2f,.85f,1,1-t);if(t>=1)Destroy(gameObject);
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
