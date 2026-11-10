using UnityEngine;
namespace GameActions
{
 public sealed class GroundImpactPreview:MonoBehaviour
 {
  public GroundImpactDecals stamps;public bool heavy=true,alternate=true;public float interval=4.5f;float next;
  void OnEnable(){next=Time.time+.5f;}
  void Update(){if(!stamps||Time.time<next)return;next=Time.time+interval;stamps.Clear();stamps.Stamp(transform.position,heavy?2.2f:1.1f);if(alternate)heavy=!heavy;}
 }
}
