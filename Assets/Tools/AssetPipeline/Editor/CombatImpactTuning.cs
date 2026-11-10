using System;using System.IO;using UnityEditor;using UnityEngine;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class CombatImpactTuning
 {
  const string Root="Assets/Game/VFX/Combat/",Stage="AssetStaging/VFXUpgrade/";
  static CombatImpactTuning(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"ImpactTune.request"))return;File.Delete(Stage+"ImpactTune.request");try{Tune();File.WriteAllText(Stage+"impact_tune.json","{\"passed\":true}");}catch(Exception e){File.WriteAllText(Stage+"impact_tune.json",e.ToString());Debug.LogException(e);}};}
  [MenuItem("Tools/Combat VFX/Select Heavy Ground Impact")]static void SelectHeavy(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"ConcreteImpactHeavy.prefab");EditorGUIUtility.PingObject(Selection.activeObject);}
  [MenuItem("Tools/Combat VFX/Select Light Ground Impact")]static void SelectLight(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"ConcreteImpactLight.prefab");EditorGUIUtility.PingObject(Selection.activeObject);}
  internal static void Configure(GameObject root,bool heavy){foreach(var ps in root.GetComponentsInChildren<ParticleSystem>()){var m=ps.main;if(ps.name=="Colliding concrete chunks")m.startSize=new ParticleSystem.MinMaxCurve(heavy?.04f:.025f,heavy?.085f:.055f);if(ps.name=="Expanding ground dust"){m.startSpeed=new ParticleSystem.MinMaxCurve(heavy?6:4,heavy?8:5.5f);m.startSize=new ParticleSystem.MinMaxCurve(heavy?.6f:.4f,heavy?1.1f:.8f);ps.transform.localPosition=Vector3.up*.15f;var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.radial=0;velocity.y=.15f;velocity.speedModifier=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.5f,.55f),new Keyframe(1,.12f)));var noise=ps.noise;noise.separateAxes=true;noise.strengthX=.3f;noise.strengthY=.04f;noise.strengthZ=.3f;var emission=ps.emission;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(heavy?40:26))});}}}
  static void Tune()
  {
   foreach(bool heavy in new[]{false,true}){string path=Root+(heavy?"ConcreteImpactHeavy":"ConcreteImpactLight")+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{Configure(root,heavy);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
   Selection.activeObject=null;ActiveEditorTracker.sharedTracker.isLocked=false;ActiveEditorTracker.sharedTracker.ForceRebuild();SelectHeavy();
  }
 }
}
