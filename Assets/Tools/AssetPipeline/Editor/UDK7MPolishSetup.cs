using System;using System.IO;using System.Linq;using System.Collections.Generic;using Newtonsoft.Json;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using GameActions;using UDK7M;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class UDK7MPolishSetup
 {
  const string Stage="AssetStaging/UDK7M/Boss/",Root="Assets/Game/VFX/Combat/";static bool busy;
  static UDK7MPolishSetup(){EditorApplication.update+=Poll;}
  static void Poll(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"BossFlowVFX.request"))return;File.Delete(Stage+"BossFlowVFX.request");Run();}
  static Material Material(string name)
  {
   var path=Root+"Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;
   mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;AssetDatabase.CreateAsset(mat,path);return mat;
  }
  static GameObject Particle(string name,bool sparks)
  {
   var path=Root+name+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab)return prefab;
   var go=new GameObject(name);try
   {
    var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=sparks?.3f:1.1f;main.startLifetime=sparks?.25f:1.1f;main.startSpeed=sparks?3:9;main.startSize=sparks?.035f:.18f;main.startColor=sparks?new Color(1,.65f,.12f):new Color(1,.35f,.03f);main.gravityModifier=.7f;main.maxParticles=150;main.simulationSpace=ParticleSystemSimulationSpace.World;
    var emission=ps.emission;emission.enabled=true;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(sparks?9:80))});var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;go.GetComponent<ParticleSystemRenderer>().sharedMaterial=Material(sparks?"Sparks":"Explosion");return PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
  static GameObject Wave()
  {
   var path=Root+"ResonanceShockwave.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab)return prefab;
   var go=new GameObject("ResonanceShockwave");try{var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Material("Shockwave");line.loop=true;line.useWorldSpace=false;line.positionCount=96;line.widthMultiplier=.5f;line.startColor=line.endColor=new Color(.2f,.85f,1,1);for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;line.SetPosition(i,new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)));}go.AddComponent<UDK7MShockwave>();return PrefabUtility.SaveAsPrefabAsset(go,path);}finally{UnityEngine.Object.DestroyImmediate(go);}
  }
  static void Loops(GameObject root){foreach(var loop in root.GetComponentsInChildren<MachineLoopAudio>(true)){loop.source.spatialBlend=loop.drill?.85f:.95f;loop.source.rolloffMode=AudioRolloffMode.Logarithmic;loop.source.minDistance=3;loop.source.maxDistance=55;}}
  static void Run()
  {
   busy=true;var prior=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(UDK7MThreePhaseBuilder.ScenePath);bool opened=false;
   try
   {
    Directory.CreateDirectory(Root+"Materials");Directory.CreateDirectory(Stage+"BeforePolish");AssetDatabase.Refresh();var sparks=Particle("Sparks",true);var explosion=Particle("Explosion",false);var wave=Wave();
    const string libraryPath="Assets/Game/Shared/Actions/Resources/CombatVFX.asset";var library=AssetDatabase.LoadAssetAtPath<CombatEffectLibrary>(libraryPath);if(!library){library=ScriptableObject.CreateInstance<CombatEffectLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}var entries=new List<CombatEffectPrefab>(library.effects);if(!entries.Any(e=>e.kind==CombatEffect.Sparks))entries.Add(new CombatEffectPrefab{kind=CombatEffect.Sparks,prefab=sparks,lifetime=.6f});if(!entries.Any(e=>e.kind==CombatEffect.Explosion))entries.Add(new CombatEffectPrefab{kind=CombatEffect.Explosion,prefab=explosion,lifetime=2});library.effects=entries.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
    foreach(var path in new[]{"Assets/Game/Enemies/UDK7M/Prefabs/UDK7M.prefab","Assets/Game/Enemies/UDK7M/AI/Prefabs/UDK7MBoss.prefab"}){var root=PrefabUtility.LoadPrefabContents(path);try{Loops(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
    if(!scene.IsValid()||!scene.isLoaded){scene=EditorSceneManager.OpenScene(UDK7MThreePhaseBuilder.ScenePath,OpenSceneMode.Additive);opened=true;}SceneManager.SetActiveScene(scene);if(!File.Exists(Stage+"BeforePolish/Arena.unity"))EditorSceneManager.SaveScene(scene,Stage+"BeforePolish/Arena.unity",true);
    var arena=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArena>()).Single();Loops(arena.boss.gameObject);arena.warningSounds=Array.Empty<AudioClip>();arena.tuning.nearPillarDistance=10;arena.tuning.nearPillarTripleChance=.1f;EditorUtility.SetDirty(arena.tuning);AssetDatabase.SaveAssetIfDirty(arena.tuning);if(!arena.feedback.shockwavePrefab)arena.feedback.shockwavePrefab=wave;if(!arena.feedback.energyGatherPrefab)arena.feedback.energyGatherPrefab=wave;EditorSceneManager.SaveScene(scene);
    File.WriteAllText(Stage+"polish.json",JsonConvert.SerializeObject(new{passed=true,prefabs=new[]{AssetDatabase.GetAssetPath(sparks),AssetDatabase.GetAssetPath(explosion),AssetDatabase.GetAssetPath(wave)},nearPillarDistance=arena.tuning.nearPillarDistance,tripleChance=arena.tuning.nearPillarTripleChance,fallWarningAudio=arena.warningSounds.Length,loops=arena.boss.GetComponentsInChildren<MachineLoopAudio>().Select(l=>new{l.drill,spatialBlend=l.source.spatialBlend})},Formatting.Indented));
   }catch(Exception e){File.WriteAllText(Stage+"polish.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
   finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);if(opened&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);busy=false;}
  }
 }
}

