using UnityEngine;using UnityEngine.VFX;
namespace GameActions
{
 public sealed class VFXGroundBinder:MonoBehaviour
 {
  void OnEnable()
  {
   Vector3 point=transform.position-Vector3.up*.15f,normal=Vector3.up;float distance=float.MaxValue;
   foreach(var hit in Physics.RaycastAll(transform.position+Vector3.up*.3f,Vector3.down,12,~0,QueryTriggerInteraction.Ignore))
    if(!hit.transform.IsChildOf(transform)&&hit.normal.y>.5f&&hit.collider.GetComponentInParent<IActionTarget>()==null&&hit.distance<distance){distance=hit.distance;point=hit.point;normal=hit.normal;}
   foreach(var vfx in GetComponentsInChildren<VisualEffect>()){if(vfx.HasVector3("GroundPoint"))vfx.SetVector3("GroundPoint",transform.InverseTransformPoint(point));if(vfx.HasVector3("GroundNormal"))vfx.SetVector3("GroundNormal",transform.InverseTransformDirection(normal));vfx.Reinit();}
  }
 }
}
