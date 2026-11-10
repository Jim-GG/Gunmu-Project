using UnityEngine;
namespace GameActions
{
 public sealed class RefractiveShockwave:MonoBehaviour,ICombatWave
 {
  public Transform ring,shell;public float previewRadius=7,previewDuration=1.1f;public float shellHeight=.65f;float radius,duration,time;bool begun,collapse;MaterialPropertyBlock block;
  void Start(){if(!begun)Begin(previewRadius,previewDuration);}
  public void Begin(float radius,float duration,bool collapse=false){this.radius=radius;this.duration=duration;this.collapse=collapse;begun=true;time=0;block=new MaterialPropertyBlock();Draw(0);}
  void Draw(float t){float e=collapse?1-t:1-Mathf.Pow(1-t,2);float r=Mathf.Lerp(.15f,radius,e);if(ring)ring.localScale=new Vector3(r*2,r*2,r*2);if(shell)shell.localScale=new Vector3(r*2,r*2*shellHeight,r*2);foreach(var renderer in GetComponentsInChildren<Renderer>()){renderer.GetPropertyBlock(block);block.SetFloat("_Progress",t);block.SetFloat("_Opacity",collapse?.35f:.85f);renderer.SetPropertyBlock(block);}}
  void Update(){if(!begun)return;time+=Time.deltaTime;float t=Mathf.Clamp01(time/Mathf.Max(.05f,duration));Draw(t);if(t>=1)Destroy(gameObject);}
 }
}
