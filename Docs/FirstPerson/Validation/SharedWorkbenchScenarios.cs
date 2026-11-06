using System;using System.Linq;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using GameActions;using Resonator.AI;using Resonator.AI.Editor;
public static class SharedWorkbenchScenarios
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 public static async Task<object> Run()
 {
  await Task.CompletedTask;Check(!Application.isPlaying,"Exit Play Mode first");var cases=new System.Collections.Generic.List<string>();
  var profile=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ActionSet>("Assets/Game/Shared/Actions/Resources/GauntletActions.asset"));
  var w=ScriptableObject.CreateInstance<ResonatorAnimationWorkbench>();GameObject reference=null;
  try
  {
   typeof(ResonatorAnimationWorkbench).GetMethod("SetSharedProfile",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,new object[]{profile});
   reference=UnityEngine.Object.Instantiate(profile.previewModel);foreach(var b in reference.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
   var animator=reference.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;
   float maxError=0;int signals=0;
   System.IO.Directory.CreateDirectory("Docs/FirstPerson/Validation/Preview");
   for(int i=1;i<=4;i++)
   {
    int index=Array.FindIndex(profile.actions,a=>a.id=="punch"+i);float frame=i==1?16:i==2?4:i==3?6:8;
    var a=profile.actions[index];w.ShowAction(index,frame);w.SaveModelPreview("Docs/FirstPerson/Validation/Preview/Punch"+i+".png");
    var model=(GameObject)Field(w,"model");var player=(ActionMotionPlayer)Field(w,"player");player.FrameEventRaised+=e=>signals++;
    a.Motion.track.clip.SampleAnimation(animator.gameObject,frame/a.Motion.track.framesPerSecond);
    var expected=reference.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");var actual=model.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");
    float error=Quaternion.Angle(expected.localRotation,actual.localRotation);maxError=Mathf.Max(maxError,error);Check(error<.1f,"Workbench pose differs from original FBX punch"+i+": "+error);
    var bindings=AnimationUtility.GetCurveBindings(a.Motion.track.clip);Check(bindings.All(b=>b.path==""||model.GetComponentInChildren<Animator>().transform.Find(b.path)),"Unbound imported curve punch"+i);
   }
   cases.Add("All four punch previews match original FBX poses and bind every animation curve");
   var geometry=(ResonatorHitbox)Field(w,"geometry");var last=profile.Find("punch4").action.hits[0];var pose=geometry.Pose(last);Check(Mathf.Abs(pose.centre.y+.25f)<.002f&&Mathf.Abs(pose.centre.z-1.05f)<.002f,"First person Root hitbox is not relative to its view origin");cases.Add("First-person hitbox binds to the view origin, in metre units");
   int edge=Array.FindIndex(profile.transitions,t=>!t.anySource&&t.from=="punch1"&&t.to=="charge");w.ShowTransition(edge,profile.transitions[edge].blend*.5f);w.SaveModelPreview("Docs/FirstPerson/Validation/Preview/RecoveryToChargeBlend.png");
   var playerBlend=(ActionMotionPlayer)Field(w,"player");Check(Mathf.Abs(playerBlend.DestinationWeight-.5f)<.001f,"Transition preview does not use runtime blend weights");Check(signals==0,"Workbench emitted gameplay frame events");cases.Add("Same runtime mixer previews interrupted action transitions; preview emits no gameplay events");
   Check(profile.Find("charge full").Motion.End<=50.001f,"Charge full import range not preserved");cases.Add("Charge-full preview uses only source frames 0-50");
   return new{passed=true,cases=cases.ToArray(),maximumPoseAngleError=maxError};
  }
  finally{UnityEngine.Object.DestroyImmediate(w);if(reference)UnityEngine.Object.DestroyImmediate(reference);UnityEngine.Object.DestroyImmediate(profile);}
 }
}
