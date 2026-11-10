using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class GauntletCoreSocketSetup
 {
  const string Stage="AssetStaging/VFXPhase2/",Prefab="Assets/Game/Player/Gauntlet/Prefabs/GauntletViewModel.prefab";
  static GauntletCoreSocketSetup(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;if(File.Exists(Stage+"CoreFollowVerify.request")){File.Delete(Stage+"CoreFollowVerify.request");try{VerifyFollow();}catch(Exception e){File.WriteAllText(Stage+"core_follow_verify.json",e.ToString());Debug.LogException(e);}return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;foreach(var task in new[]{"CoreSocketProbe","CoreSocketApply"})if(File.Exists(Stage+task+".request")){File.Delete(Stage+task+".request");try{Run(task=="CoreSocketApply");}catch(Exception e){File.WriteAllText(Stage+"core_socket_error.txt",e.ToString());Debug.LogException(e);}return;}};}
  static void VerifyFollow()
  {
   var go=PrefabUtility.LoadPrefabContents(Prefab);go.SetActive(false);
   try
   {
    var socket=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FX_FullChargeCore");var combat=go.AddComponent<FirstPerson.GauntletCombat>();combat.enabled=false;combat.animationPlayer=go.GetComponent<Gauntlet.GauntletAnimationPlayer>();var show=go.AddComponent<GameActions.WeaponEnergyVFXController>();show.enabled=false;show.combat=combat;show.library=GameActions.AdvancedCombatVFXLibrary.Load();show.flashAnchor=socket;
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(GameActions.WeaponEnergyVFXController).GetMethod("PlayFullFlash",flags).Invoke(show,null);var flash=(GameObject)typeof(GameActions.WeaponEnergyVFXController).GetField("fullFlash",flags).GetValue(show);if(!flash||flash.transform.parent!=socket)throw new Exception("Flash is not directly parented to the core socket.");
    var before=flash.transform.position;socket.parent.localPosition+=new Vector3(.12f,.07f,.03f);socket.parent.localRotation*=Quaternion.Euler(27,-14,18);var expected=socket.position+socket.rotation*show.library.fullChargeFlashOffset;float error=Vector3.Distance(flash.transform.position,expected);if(error>.0001f||Vector3.Distance(before,flash.transform.position)<.05f)throw new Exception("Emitted flash did not follow fist translation and rotation without another update.");
    var resource=GraphReflect.Call(GraphReflect.T("UnityEditor.VFX.VisualEffectResource"),"GetResourceAtPath","Assets/Game/VFX/Combat/Advanced/Graphs/VFX_GauntletFullFlash.vfx");var graph=GraphReflect.Call(GraphReflect.T("UnityEditor.VFX.VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);var init=GraphReflect.List(GraphReflect.Get(graph,"children")).First(n=>n.GetType().Name=="VFXBasicInitialize");string space=GraphReflect.Get(GraphReflect.Call(init,"GetData"),"space").ToString();if(space!="Local")throw new Exception("Flash particles are not in local space.");
    File.WriteAllText(Stage+"core_follow_verify.json",JsonConvert.SerializeObject(new{passed=true,anchor=socket.name,directParent=true,simulationSpace=space,followErrorMetres=error,movedMetres=Vector3.Distance(before,flash.transform.position),rotationFollows=Quaternion.Angle(flash.transform.rotation,socket.rotation)<.01f},Formatting.Indented));
   }
   finally{PrefabUtility.UnloadPrefabContents(go);}
  }
  static void Run(bool apply)
  {
   var go=PrefabUtility.LoadPrefabContents(Prefab);var renderer=go.GetComponentInChildren<SkinnedMeshRenderer>(true);var mesh=renderer.sharedMesh;var bake=new Mesh();renderer.BakeMesh(bake);
   try
   {
    var triangles=mesh.GetTriangles(1);var vertices=bake.vertices;var parent=Enumerable.Range(0,mesh.vertexCount).ToArray();int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}void Union(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
    for(int i=0;i<triangles.Length;i+=3){Union(triangles[i],triangles[i+1]);Union(triangles[i],triangles[i+2]);}
    var weld=new Dictionary<string,int>();foreach(int i in triangles.Distinct()){var p=vertices[i];string key=Mathf.RoundToInt(p.x*10000)+","+Mathf.RoundToInt(p.y*10000)+","+Mathf.RoundToInt(p.z*10000);if(weld.TryGetValue(key,out int prior))Union(i,prior);else weld[key]=i;}
    var groups=triangles.Distinct().GroupBy(Find).OrderByDescending(g=>g.Count()).ToArray();var report=new List<object>();var centres=new List<Vector3>();var bones=new List<Transform>();
    foreach(var group in groups){var ids=group.ToArray();var bounds=new Bounds(renderer.transform.TransformPoint(vertices[ids[0]]),Vector3.zero);foreach(var i in ids)bounds.Encapsulate(renderer.transform.TransformPoint(vertices[i]));var weights=new Dictionary<int,float>();foreach(var i in ids){var w=mesh.boneWeights[i];void Weight(int bone,float value){if(!weights.ContainsKey(bone))weights[bone]=0;weights[bone]+=value;}Weight(w.boneIndex0,w.weight0);Weight(w.boneIndex1,w.weight1);Weight(w.boneIndex2,w.weight2);Weight(w.boneIndex3,w.weight3);}var bone=renderer.bones[weights.OrderByDescending(p=>p.Value).First().Key];var local=bone.InverseTransformPoint(bounds.center);float surfaceY=ids.Max(i=>bone.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i])).y);var surface=new Vector3(local.x,surfaceY+.001f,local.z);centres.Add(surface);bones.Add(bone);report.Add(new{index=centres.Count-1,vertices=ids.Length,bone=bone.name,boneLocal=new[]{local.x,local.y,local.z},surfaceSocket=new[]{surface.x,surface.y,surface.z},worldCentre=new[]{bounds.center.x,bounds.center.y,bounds.center.z},worldSize=new[]{bounds.size.x,bounds.size.y,bounds.size.z}});}
    File.WriteAllText(Stage+"core_socket_components.json",JsonConvert.SerializeObject(report,Formatting.Indented));
    if(apply){int index=File.Exists(Stage+"CoreSocketIndex.txt")?int.Parse(File.ReadAllText(Stage+"CoreSocketIndex.txt")):0;if(index<0||index>=centres.Count)throw new Exception("Invalid glass component selection");var socket=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="FX_FullChargeCore");if(!socket)socket=new GameObject("FX_FullChargeCore").transform;socket.SetParent(bones[index],false);socket.localPosition=centres[index];socket.localRotation=Quaternion.identity;socket.localScale=Vector3.one;PrefabUtility.SaveAsPrefabAsset(go,Prefab);File.WriteAllText(Stage+"core_socket_applied.json",JsonConvert.SerializeObject(new{passed=true,index,bone=bones[index].name,position=new[]{centres[index].x,centres[index].y,centres[index].z}}));}
   }
   finally{UnityEngine.Object.DestroyImmediate(bake);PrefabUtility.UnloadPrefabContents(go);}
  }
 }
}
