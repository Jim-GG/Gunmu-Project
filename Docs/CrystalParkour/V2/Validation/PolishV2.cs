using System;using System.Linq;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using CrystalParkour;
public static class PolishTraversalV2
{
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Edit Mode");
  typeof(CrystalParkour.Editor.CrystalTraversalLabBuilder).GetMethod("PrepareKit",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
  var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/CrystalParkour/Data/V2/DepthTestedLabel.mat");
  void Fix(GameObject root)
  {
   foreach(var surface in root.GetComponentsInChildren<CrystalSurface>(true).Where(c=>c.name.Contains("anchor")||c.name.StartsWith("Anchor ")))
   {var mesh=surface.GetComponentInChildren<MeshFilter>(true);if(!mesh)continue;var old=surface.GetComponent<BoxCollider>();if(old)UnityEngine.Object.DestroyImmediate(old);var collider=mesh.GetComponent<MeshCollider>();if(!collider)collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;surface.contact=collider;surface.useLaunchNormal=true;}
   foreach(var text in root.GetComponentsInChildren<TextMesh>(true)){text.GetComponent<Renderer>().sharedMaterial=mat;if(!text.GetComponent<WorldSpaceLabel>())text.gameObject.AddComponent<WorldSpaceLabel>();}
   foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Orange back panel").ToArray())
   {var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Orange circular back panel";SceneManager.MoveGameObjectToScene(go,root.scene);go.transform.SetParent(t.parent,false);go.transform.localPosition=t.localPosition;go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=new Vector3(4.2f,.04f,4.2f);go.GetComponent<Renderer>().sharedMaterial=t.GetComponent<Renderer>().sharedMaterial;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());UnityEngine.Object.DestroyImmediate(t.gameObject);}
  }
  string path=CrystalParkour.Editor.CrystalTraversalLabBuilder.ScenePath;var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
  try{foreach(var root in scene.GetRootGameObjects())Fix(root);EditorSceneManager.SaveScene(scene);}
  finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
  foreach(var file in System.IO.Directory.GetFiles("Assets/Game/CrystalParkour/Prefabs/LevelModules","*.prefab")){var contents=PrefabUtility.LoadPrefabContents(file);try{Fix(contents);PrefabUtility.SaveAsPrefabAsset(contents,file);}finally{PrefabUtility.UnloadPrefabContents(contents);}}
  AssetDatabase.SaveAssets();return new{fixedAxes=12,labelsDepthTested=true,circularFanBacks=true};
 }
}
