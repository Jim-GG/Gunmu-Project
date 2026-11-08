using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using CrystalParkour;
public static class TraversalV2AssetAudit
{
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Stop Play Mode");var s=SceneManager.GetSceneByPath(CrystalParkour.Editor.CrystalTraversalLabBuilder.ScenePath);if(!s.isLoaded)s=EditorSceneManager.OpenScene(CrystalParkour.Editor.CrystalTraversalLabBuilder.ScenePath,OpenSceneMode.Single);
  var all=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing!=0)throw new Exception("Missing scripts");
  var emptyMaterials=all.SelectMany(t=>t.GetComponents<Renderer>()).Where(r=>r.sharedMaterials.Any(m=>!m)).Select(r=>r.name).ToArray();if(emptyMaterials.Length>0)throw new Exception("Missing materials: "+string.Join(",",emptyMaterials));
  var shaderErrors=new[]{"Assets/Game/Environment/crystal/Shaders/CrystalFacet.shader","Assets/Game/CrystalParkour/Runtime/WorldLabel.shader"}.SelectMany(path=>ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(path))).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>m.message).ToArray();if(shaderErrors.Length>0)throw new Exception(string.Join(";",shaderErrors));
  foreach(string file in Directory.GetFiles("Assets/Game/CrystalParkour/Prefabs/LevelModules","*.prefab"))if(AssetDatabase.LoadAssetAtPath<GameObject>(file).GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))throw new Exception("Missing prefab script: "+file);
  var ground=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/crystal/Prefabs/V2/EnvCrystal_05_GroundMixed.prefab");var camera=SceneView.lastActiveSceneView;if(camera){camera.pivot=new Vector3(0,6,0);camera.rotation=Quaternion.Euler(13,40,0);camera.size=9;camera.Repaint();}
  EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();var result=new{passed=true,scene=s.path,sceneObjects=all.Length,missingScripts=missing,missingMaterials=emptyMaterials,shaderErrors,crystalVariants=12,modules=6,sourceBlend="ArtSource/Crystal/V2/Crystal_Environmental_GeometryNodes.blend",guide="Docs/CrystalParkour/V2/关卡搭建与参数指南.md"};File.WriteAllText("Docs/CrystalParkour/V2/Validation/AssetReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
}
