using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static class AssetLayoutSnapshot
{
 [Serializable]public class FileInfoRow{public string path,guid,type;public string[] dependencies;public int missingScripts;}
 [Serializable]public class SceneInfoRow{public string path,backup;public bool dirty;public int objects,missingScripts;}
 [Serializable]public class MaterialInfoRow{public string guid,shader;public string[] properties;}
 [Serializable]public class Snapshot{public FileInfoRow[] assets;public SceneInfoRow[] scenes;public MaterialInfoRow[] materials;public string[] buildScenes;}
 public static object Run()
 {
  if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Editor must be stopped and ready");
  const string root="Docs/ProjectStructure/Validation/";bool before=!File.Exists(root+"MigrationResult.json");var rows=new List<FileInfoRow>();var materials=new List<MaterialInfoRow>();
  foreach(var p in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&File.Exists(p)).OrderBy(p=>p))
  {
   var row=new FileInfoRow{path=p,guid=AssetDatabase.AssetPathToGUID(p),type="",dependencies=Array.Empty<string>()};
   if(new[]{".unity",".prefab",".asset",".mat",".controller",".overrideController",".playable",".shadergraph",".asmdef"}.Contains(Path.GetExtension(p)))row.dependencies=AssetDatabase.GetDependencies(p,true).Select(AssetDatabase.AssetPathToGUID).Where(g=>!string.IsNullOrEmpty(g)).Distinct().OrderBy(g=>g).ToArray();
   if(p.EndsWith(".cs")){var script=AssetDatabase.LoadAssetAtPath<MonoScript>(p);row.type=script?.GetClass()?.AssemblyQualifiedName??"";}
   if(p.EndsWith(".prefab")){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p);row.missingScripts=prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));}
   if(p.EndsWith(".mat"))
   {
    var m=AssetDatabase.LoadAssetAtPath<Material>(p);var properties=new List<string>();
    for(int i=0;i<m.shader.GetPropertyCount();i++)
    {
     string n=m.shader.GetPropertyName(i);var type=m.shader.GetPropertyType(i);string value;
     if(type==UnityEngine.Rendering.ShaderPropertyType.Texture){var texture=m.GetTexture(n);value=(texture?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture)):"")+"/"+m.GetTextureScale(n).ToString("R")+"/"+m.GetTextureOffset(n).ToString("R");}
     else if(type==UnityEngine.Rendering.ShaderPropertyType.Color)value=m.GetColor(n).ToString("R");else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)value=m.GetVector(n).ToString("R");else value=m.GetFloat(n).ToString("R",System.Globalization.CultureInfo.InvariantCulture);
     properties.Add(n+"="+value);
    }
    materials.Add(new MaterialInfoRow{guid=row.guid,shader=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m.shader)),properties=properties.ToArray()});
   }
   rows.Add(row);
  }
  var scenes=new List<SceneInfoRow>();
  for(int i=0;i<SceneManager.sceneCount;i++)
  {
   var scene=SceneManager.GetSceneAt(i);var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();string backup="";
   if(before&&scene.isDirty){Directory.CreateDirectory("Temp/AssetReorganization");backup="Temp/AssetReorganization/Unsaved_"+scene.name+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Failed to back up unsaved scene");}
   scenes.Add(new SceneInfoRow{path=scene.path,dirty=scene.isDirty,backup=backup,objects=transforms.Length,missingScripts=transforms.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))});
  }
  var snapshot=new Snapshot{assets=rows.ToArray(),scenes=scenes.ToArray(),materials=materials.ToArray(),buildScenes=EditorBuildSettings.scenes.Select(s=>s.path+"|"+s.enabled).ToArray()};File.WriteAllText(root+(before?"BeforeNative.json":"AfterNative.json"),Newtonsoft.Json.JsonConvert.SerializeObject(snapshot,Newtonsoft.Json.Formatting.Indented));
  return new{before,assets=rows.Count,prefabMissing=rows.Sum(r=>r.missingScripts),materials=materials.Count,scenes=snapshot.scenes};
 }
}
