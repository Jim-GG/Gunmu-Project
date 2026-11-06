using System;using System.Linq;using System.Reflection;using UnityEngine;using UnityEditor;using Resonator.AI;using Resonator.AI.Editor;
public static class WorkbenchPreviewScenarios
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Exit play mode first");
  var tuning=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ResonatorTuning>("Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset"));
  var window=ScriptableObject.CreateInstance<ResonatorAnimationWorkbench>();var data=new SerializedObject(window);data.FindProperty("tuning").objectReferenceValue=tuning;data.ApplyModifiedPropertiesWithoutUndo();
  var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"));var e=go.GetComponent<ResonatorEnemy>();e.enabled=false;
  try
  {
   window.ShowAction(1,24);window.SaveModelPreview("Docs/ProjectStructure/Validation/Preview/Workbench_Kick24.png");
   var root=(GameObject)Field(window,"previewRoot");var model=(GameObject)Field(window,"model");var preview=(ResonatorHitbox)Field(window,"geometry");
   Check(model.transform.parent==root.transform,"Preview must have a separate actor root");
   var runtime=new ResonatorHitbox(go.transform);e.motion.Play(tuning.kick.animation,24,true);
   var boneHit=tuning.kick.hits[0];float boneError=Vector3.Distance(preview.Pose(boneHit).centre,runtime.Pose(boneHit).centre);Check(boneError<.001f,"Bone hitbox differs between preview and runtime: "+boneError);
   var rootHit=new HitWindow{anchor=HitboxAnchor.Root,position=new Vector3(0,1,.9f)};float rootError=Vector3.Distance(preview.Pose(rootHit).centre,runtime.Pose(rootHit).centre);Check(rootError<.001f,"Root hitbox uses compensated Visual instead of actor root");
   var scale=e.motion.transform.localScale;e.motion.transform.localScale=new Vector3(1.17f,1.3f,.9f);e.motion.Play(tuning.walk,15,true);
   var hip=go.transform.InverseTransformPoint(e.motion.hips.position);var reference=e.motion.ReferenceHipInParent;
   float scaledDrift=new Vector2(hip.x-reference.x,hip.z-reference.z).magnitude;Check(scaledDrift<.0001f,"Scaled model double-applies root travel: "+scaledDrift);e.motion.transform.localScale=scale;
   int index=Array.FindIndex(tuning.transitions,r=>r.from==EnemyState.Headbutt&&r.to==EnemyState.Kicking);var edge=tuning.transitions[index];float time=edge.blend*.5f;
   window.ShowTransition(index,time);window.SaveModelPreview("Docs/ProjectStructure/Validation/Preview/Workbench_ComboBlend.png");
   var animator=model.GetComponent<Animator>();e.motion.Play(tuning.headbutt.animation,edge.cancelFrameOnHit,true);e.motion.Play(tuning.kick.animation,edge.entryFrame,false,edge.blend);e.motion.Advance(time);
   var previewBones=model.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("ValveBiped.")).ToDictionary(t=>t.name,t=>t);float angle=e.ragdoll.definitions.Max(d=>Quaternion.Angle(d.bone.localRotation,previewBones[d.bone.name].localRotation));Check(angle<.1f,"Workbench transition differs from runtime: "+angle);
   data.Update();data.FindProperty("previewRootMotion").boolValue=true;data.FindProperty("previewTargetDistance").floatValue=3;data.ApplyModifiedPropertiesWithoutUndo();window.ShowAction(2,45);window.SaveModelPreview("Docs/ProjectStructure/Validation/Preview/Workbench_AttackDisplacement.png");
   float expected=AttackMoveBudget.Preview(tuning.attack,tuning.attack.animation.startFrame,45,3,tuning.minimumSeparation);Check(Mathf.Abs(root.transform.position.z-expected)<.001f,"Workbench did not apply its displacement budget to the preview actor");
   return new{passed=true,bonePositionError=boneError,rootPositionError=rootError,blendAngleError=angle,scaledRootDrift=scaledDrift,previewAttackTravel=expected};
  }
  finally{UnityEngine.Object.DestroyImmediate(window);if(e&&e.motion)e.motion.StopForRagdoll();UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(tuning);}
 }
}
