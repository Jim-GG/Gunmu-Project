using UnityEngine;
namespace Resonator {
 public sealed class ResonatorAnimationPreview:MonoBehaviour {
  public Animator animator; public string[] clips; public int selected;
  public Camera previewCamera; public Transform followTarget;
  public Vector3 cameraOffset=new Vector3(2.4f,1.2f,3.5f);
  void LateUpdate(){if(previewCamera&&followTarget){var target=followTarget.position+Vector3.up*.1f;previewCamera.transform.position=target+cameraOffset;previewCamera.transform.LookAt(target);}}
  public void Play(int index){if(clips==null||clips.Length==0)return;selected=Mathf.Clamp(index,0,clips.Length-1);animator.CrossFadeInFixedTime(clips[selected],.12f,0,0);}
  void OnGUI(){GUI.Box(new Rect(12,12,310,Screen.height-24),"Resonator animation preview");for(int i=0;i<clips.Length;i++)if(GUI.Button(new Rect(22,42+i*27,290,25),(i==selected?"> ":"")+clips[i]))Play(i);}
 }
}
