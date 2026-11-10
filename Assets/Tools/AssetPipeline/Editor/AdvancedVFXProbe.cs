using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEditor;using UnityEngine;using UDK7M;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class AdvancedVFXProbe
 {
  const string Stage="AssetStaging/VFXPhase2/";static AdvancedVFXProbe(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"Probe.request"))return;File.Delete(Stage+"Probe.request");try{Run();}catch(Exception e){File.WriteAllText(Stage+"probe_error.txt",e.ToString());Debug.LogException(e);}};}
  static object Get(object o,string name){try{return GraphReflect.Get(o,name);}catch{return null;}}
  static object Model(object o)=>new{type=o.GetType().FullName,label=Get(o,"label"),name=Get(o,"name"),inputs=GraphReflect.List(Get(o,"inputSlots")).Select(s=>new{name=Get(s,"name"),value=Get(s,"value")?.ToString()}),settings=GraphReflect.List(GraphReflect.Call(o,"GetSettings",true)).Select(s=>new{name=Get(s,"name"),value=Get(s,"value")?.ToString()}),children=GraphReflect.List(Get(o,"children")).Select(Model)};
  static void Run()
  {
   foreach(var template in new[]{"03_Simple_Burst","06_Firework"}){string path="Assets/Game/VFX/Combat/Advanced/Graphs/"+(template=="06_Firework"?"CrystalShatterGPU":"ExplosionGPU")+".vfx";if(!File.Exists(path)){File.Copy("Library/PackageCache/com.unity.visualeffectgraph@a963c7d0e87b/Editor/Templates/"+template+".vfx",path);AssetDatabase.ImportAsset(path);}var resource=GraphReflect.Call(GraphReflect.T("UnityEditor.VFX.VisualEffectResource"),"GetResourceAtPath",path);var graph=GraphReflect.Call(GraphReflect.T("UnityEditor.VFX.VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);File.WriteAllText(Stage+template+"_probe.json",JsonConvert.SerializeObject(Model(graph),Formatting.Indented));}
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Gauntlet/Prefabs/GauntletViewModel.prefab");File.WriteAllText(Stage+"gauntlet_probe.json",JsonConvert.SerializeObject(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>new{name=r.name,mesh=r.sharedMesh.name,verts=r.sharedMesh.vertexCount,materials=r.sharedMaterials.Select(m=>m?m.name:"null"),bones=r.bones.Select(b=>b.name),bounds=r.localBounds.ToString()}),Formatting.Indented));
   var wind=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/VFX/Combat/击中后溢气流.fbx");File.WriteAllText(Stage+"wind_unity_probe.json",JsonConvert.SerializeObject(wind.GetComponentsInChildren<MeshFilter>().Select(f=>new{name=f.name,vertices=f.sharedMesh.vertexCount,bounds=f.sharedMesh.bounds.ToString(),scale=f.transform.lossyScale.ToString()}),Formatting.Indented));
   var boss=UnityEngine.Object.FindFirstObjectByType<UDK7MBoss>();if(boss)File.WriteAllText(Stage+"boss_anchors.json",JsonConvert.SerializeObject(boss.GetComponentsInChildren<Transform>(true).Where(t=>t.name.ToLower().Contains("gas")||t.name.ToLower().Contains("tank")||t.name.ToLower().Contains("can")||t.name.ToLower().Contains("body")).Select(t=>new{name=t.name,position=boss.transform.InverseTransformPoint(t.position).ToString()}),Formatting.Indented));
  }
 }
}
