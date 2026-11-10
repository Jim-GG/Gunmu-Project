using UnityEngine;
namespace GameActions
{
 public sealed class HeatHazeBillboard:MonoBehaviour
 {
  public float opacity=.3f;MaterialPropertyBlock block;
  void LateUpdate(){var camera=Camera.main;if(camera)transform.rotation=Quaternion.LookRotation(camera.transform.forward,Vector3.up);if(block==null)block=new MaterialPropertyBlock();var r=GetComponent<Renderer>();if(r){r.GetPropertyBlock(block);block.SetFloat("_Opacity",opacity);block.SetFloat("_Progress",0);r.SetPropertyBlock(block);}}
 }
}
