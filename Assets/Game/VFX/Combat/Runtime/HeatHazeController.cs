using UnityEngine;using UnityEngine.VFX;
namespace GameActions
{
 public sealed class HeatHazeController:MonoBehaviour
 {
  public VisualEffect gpu;public bool useGPU;public Renderer surface;public bool loop=true;public float duration=2,distortionPixels=5,opacity=.75f;float age;MaterialPropertyBlock block;Camera camera;
  public void SetCamera(Camera view){camera=view;}
  void OnEnable(){age=0;block=new MaterialPropertyBlock();if(gpu){gpu.gameObject.SetActive(useGPU);if(useGPU)gpu.Reinit();}if(surface)surface.gameObject.SetActive(!useGPU);}
  void LateUpdate(){age+=Time.deltaTime;if(!camera)camera=Camera.main;if(camera){var to=camera.transform.position-transform.position;to.y=0;if(to.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(to,Vector3.up);}if(surface&&!useGPU){surface.GetPropertyBlock(block);block.SetFloat("_Mode",2);block.SetFloat("_DistortionPixels",distortionPixels);block.SetFloat("_ChromaticPixels",0);block.SetFloat("_Opacity",opacity);block.SetFloat("_Lifecycle",loop?0:Mathf.Clamp01(age/Mathf.Max(.1f,duration)));surface.SetPropertyBlock(block);}if(!loop&&age>=duration)gameObject.SetActive(false);}
 }
}
