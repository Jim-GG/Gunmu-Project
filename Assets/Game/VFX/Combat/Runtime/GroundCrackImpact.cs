using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace GameActions
{
 public sealed class GroundCrackImpact:MonoBehaviour
 {
  public Renderer parallaxMesh;public DecalProjector distantDecal;
  [Range(0,.45f)]public float depth=.2f;
  public float nearDistance=8,farDistance=18,lifetime=25,fadeSeconds=3;
  [Range(1,32)]public int nearSteps=24,middleSteps=8;
  public int maximumNearCracks=3;
  static readonly List<GroundCrackImpact> active=new List<GroundCrackImpact>();
  MaterialPropertyBlock block;float born;Camera camera;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetRegistry()=>active.Clear();
  void OnEnable(){born=Time.time;if(!active.Contains(this))active.Add(this);block=new MaterialPropertyBlock();}
  void OnDisable(){active.Remove(this);}
  public void SpawnCrack(Vector3 position,Vector3 normal,float diameter,float depth)
  {
   normal=normal.sqrMagnitude>.001f?normal.normalized:Vector3.up;
   transform.SetPositionAndRotation(position+normal*.008f,Quaternion.FromToRotation(Vector3.back,normal)*Quaternion.Euler(0,0,Random.Range(0,360)));
   transform.localScale=Vector3.one*Mathf.Max(.25f,diameter);this.depth=Mathf.Clamp(depth,0,.45f);born=Time.time;
  }
  void LateUpdate()
  {
   if(!camera)camera=Camera.main;
   float distance=camera?Vector3.Distance(camera.transform.position,transform.position):0;
   int closer=0;if(camera)foreach(var c in active)if(c&&c!=this&&Vector3.SqrMagnitude(c.transform.position-camera.transform.position)<distance*distance)closer++;
   bool near=distance<farDistance&&closer<maximumNearCracks;
   float fade=Mathf.Clamp01((lifetime-(Time.time-born))/Mathf.Max(.01f,fadeSeconds));
   if(parallaxMesh){parallaxMesh.enabled=near;parallaxMesh.GetPropertyBlock(block);block.SetFloat("_HeightAmplitudeCm",depth*100*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(nearDistance,farDistance,distance))));block.SetFloat("_POMSteps",distance<nearDistance?nearSteps:middleSteps);block.SetVector("_PrimitiveSize",new Vector4(1,1,0,0));block.SetFloat("_CrackOpacity",fade);parallaxMesh.SetPropertyBlock(block);}
   if(distantDecal){distantDecal.enabled=!near;distantDecal.fadeFactor=fade;}
  }
 }
}
