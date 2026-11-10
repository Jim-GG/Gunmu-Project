using UnityEngine;using UnityEngine.VFX;
namespace GameActions
{
 public sealed class DashWindConeController:MonoBehaviour
 {
  public VisualEffect gpu;public Renderer[] shells;public bool useGPU;public float duration=.35f;
  public WindConeFX.WindConeBurst prefabPlayback;
  public Vector3 tipLocal;
  public Vector3 TipPosition=>transform.TransformPoint(tipLocal);
  public void AlignTip(Vector3 point,Vector3 forward,float scale=1){var rotation=Quaternion.LookRotation(forward);transform.SetPositionAndRotation(point-rotation*(tipLocal*Mathf.Clamp(scale,.5f,2)),rotation);transform.localScale=Vector3.one*Mathf.Clamp(scale,.5f,2);}
  Vector3 prefabScale;bool cachedScale;float age=10;MaterialPropertyBlock block;
  public void PlayDash(Vector3 point,Vector3 forward,float intensity=1)
  {
   if(!gameObject.activeSelf)gameObject.SetActive(true);age=0;transform.SetPositionAndRotation(point,Quaternion.LookRotation(forward));transform.localScale=Vector3.one*Mathf.Clamp(intensity,.5f,2);
   if(prefabPlayback){if(!cachedScale){prefabScale=prefabPlayback.transform.localScale;cachedScale=true;}prefabPlayback.PlayDash(point,forward);prefabPlayback.transform.localScale=prefabScale;duration=prefabPlayback.duration;return;}
   block=new MaterialPropertyBlock();if(gpu){gpu.gameObject.SetActive(useGPU);if(useGPU)gpu.Reinit();}for(int i=0;i<shells.Length;i++)if(shells[i])shells[i].gameObject.SetActive(!useGPU||i>0);
  }
  void LateUpdate(){age+=Time.deltaTime;if(!prefabPlayback){if(block==null)block=new MaterialPropertyBlock();float t=age/Mathf.Max(.05f,duration),alpha=Mathf.SmoothStep(0,1,t/.2f)*(1-Mathf.SmoothStep(.35f,1,t));foreach(var r in shells)if(r){r.GetPropertyBlock(block);block.SetFloat("_LifeAlpha",alpha);r.SetPropertyBlock(block);}}if(age>=duration)gameObject.SetActive(false);}
 }
}
