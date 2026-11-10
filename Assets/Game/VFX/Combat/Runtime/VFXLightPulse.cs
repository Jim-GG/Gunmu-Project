using UnityEngine;
namespace GameActions
{
 public sealed class VFXLightPulse:MonoBehaviour
 {
  public Light point;public float duration=.16f,peakIntensity=4;[Min(0)]public float attackSeconds;float age;static int frame,active;
  public float IntensityAt(float seconds){float attack=Mathf.Clamp(attackSeconds,0,Mathf.Max(0,duration-.001f));return seconds<attack?peakIntensity*Mathf.Clamp01(seconds/attack):peakIntensity*Mathf.Pow(Mathf.Clamp01(1-(seconds-attack)/Mathf.Max(.001f,duration-attack)),2);}
  void OnEnable(){age=0;if(!point)point=GetComponent<Light>();if(point){point.shadows=LightShadows.None;point.enabled=true;point.intensity=IntensityAt(0);}}
  void Update(){age+=Time.deltaTime;if(!point)return;if(frame!=Time.frameCount){frame=Time.frameCount;active=0;}bool show=age<duration&&active++<4;point.enabled=show;if(show)point.intensity=IntensityAt(age);}
  void OnDisable(){if(point)point.enabled=false;}
 }
}
