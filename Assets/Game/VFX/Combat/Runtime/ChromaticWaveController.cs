using UnityEngine;using UnityEngine.VFX;
namespace GameActions
{
 public sealed class ChromaticWaveController:MonoBehaviour
 {
  public VisualEffect gpu;public Renderer meshCore;public bool useGPU;public float radius=2,duration=.65f,distortionPixels=8,chromaticPixels=3;
  float age;MaterialPropertyBlock block;Camera camera;
  public void SetCamera(Camera view){camera=view;}
  public void SetNormalizedTime(float value){age=Mathf.Clamp01(value)*duration;Apply();}
  void OnEnable(){age=0;block=new MaterialPropertyBlock();if(gpu){gpu.gameObject.SetActive(useGPU);gpu.playRate=.65f/Mathf.Max(.1f,duration);gpu.Reinit();}if(meshCore)meshCore.gameObject.SetActive(!useGPU);}
  public void PlayAt(Vector3 point,float radius,float duration,float distortion,float chromatic){transform.position=point;this.radius=radius;this.duration=duration;distortionPixels=distortion;chromaticPixels=chromatic;age=0;if(gpu){gpu.playRate=.65f/Mathf.Max(.1f,duration);gpu.Reinit();}Apply();}
  void Update(){age+=Time.deltaTime;Apply();if(age>=duration)gameObject.SetActive(false);}
  void Apply()
  {
   if(!camera)camera=Camera.main;float t=Mathf.Clamp01(age/Mathf.Max(.05f,duration));float diameter=radius*2;
   Vector3 centre=camera?camera.WorldToViewportPoint(transform.position):new Vector3(.5f,.5f,1);float inside=camera&&Vector3.Distance(camera.transform.position,transform.position)<radius?1:0;
   if(useGPU&&gpu){gpu.transform.localScale=Vector3.one*diameter;if(gpu.HasVector2("CenterSS"))gpu.SetVector2("CenterSS",new Vector2(centre.x,centre.y));if(gpu.HasFloat("InsideSphere"))gpu.SetFloat("InsideSphere",inside);if(gpu.HasFloat("DistortionPixels"))gpu.SetFloat("DistortionPixels",distortionPixels);if(gpu.HasFloat("ChromaticPixels"))gpu.SetFloat("ChromaticPixels",chromaticPixels);}
   if(!useGPU&&meshCore){meshCore.transform.localScale=Vector3.one*Mathf.Lerp(.15f,diameter,1-Mathf.Pow(1-t,2));meshCore.GetPropertyBlock(block);block.SetVector("_CenterSS",centre);block.SetFloat("_InsideSphere",inside);block.SetFloat("_Lifecycle",t);block.SetFloat("_DistortionPixels",distortionPixels);block.SetFloat("_ChromaticPixels",chromaticPixels);block.SetFloat("_Mode",1);meshCore.SetPropertyBlock(block);}
  }
 }
}
