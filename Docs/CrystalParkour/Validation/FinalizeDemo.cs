using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using CrystalParkour;
public static class FinalizeCrystalDemo
{
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Stop Play Mode first");const string path="Assets/Game/Scene/CrystalImpactParkour.unity";var scene=SceneManager.GetSceneByPath(path);if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);var roots=scene.GetRootGameObjects();var player=roots.Single(g=>g.GetComponent<CrystalTraversal>());
  foreach(var c in player.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true)){var lens=c.Lens;lens.FarClipPlane=180;c.Lens=lens;}
  foreach(var c in player.GetComponentsInChildren<Camera>(true))c.farClipPlane=180;
  string prefabPath="Assets/Game/CrystalParkour/Prefabs/CrystalParkourPlayer.prefab";PrefabUtility.SaveAsPrefabAsset(player,prefabPath);
  void SaveCrystal(GameObject source,string name)
  {string p="Assets/Game/Environment/crystal/Prefabs/"+name+".prefab";if(File.Exists(p))return;var copy=UnityEngine.Object.Instantiate(source);copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);try{PrefabUtility.SaveAsPrefabAsset(copy,p);}finally{UnityEngine.Object.DestroyImmediate(copy);}}
  SaveCrystal(roots.First(g=>g.name=="03 Left wall anchor"),"CrystalAnchor_Wall");SaveCrystal(roots.SelectMany(g=>g.GetComponentsInChildren<CrystalGrowthSite>(true)).First(g=>!g.wallGrowth).growthRoot,"CrystalPlatform_Standable");
  // The platform is hidden in an untriggered growth site; its reusable prefab should be active.
  string platformPath="Assets/Game/Environment/crystal/Prefabs/CrystalPlatform_Standable.prefab";var platform=PrefabUtility.LoadPrefabContents(platformPath);try{platform.SetActive(true);PrefabUtility.SaveAsPrefabAsset(platform,platformPath);}finally{PrefabUtility.UnloadPrefabContents(platform);}
  EditorSceneManager.SaveScene(scene,path);
  int missing=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing!=0)throw new Exception("Missing scripts in demo");
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Environment/crystal/Shaders/CrystalFacet.shader");var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>m.message).ToArray();if(errors.Length!=0)throw new Exception(string.Join(";",errors));
  var result=new{passed=true,path,models=Directory.GetFiles("Assets/Game/Environment/crystal/Models","*.fbx").Length,prefabs=Directory.GetFiles("Assets/Game/Environment/crystal/Prefabs","*.prefab").Length,crystalMaterials=Directory.GetFiles("Assets/Game/Environment/crystal/Materials","*.mat").Length,missingScripts=missing,shaderErrors=errors,sceneObjects=roots.Sum(g=>g.GetComponentsInChildren<Transform>(true).Length),inputProfile=AssetDatabase.GetAssetPath(player.GetComponent<FirstPerson.GauntletCombat>().profile)};File.WriteAllText("Docs/CrystalParkour/Validation/AssetReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
}
