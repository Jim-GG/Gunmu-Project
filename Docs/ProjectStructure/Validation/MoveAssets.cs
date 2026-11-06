using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;using System.Text.RegularExpressions;using UnityEngine;using UnityEditor;
public static class ReorganizeGameAssets
{
 [Serializable]class Move{public string from,to;}
 [Serializable]class Plan{public string[] folders;public Move[] moves;public string emptyContainer;}
 public static object Run()
 {
  if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Editor must be stopped and ready");
  if(!File.Exists("Docs/ProjectStructure/Validation/BeforeNative.json")||!File.Exists("Docs/ProjectStructure/Validation/BeforeFiles.json"))throw new Exception("Baseline is required");
  var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(File.ReadAllText("Docs/ProjectStructure/MovePlan.json"));var done=new List<Move>();var changed=new List<string>();
  string Mapped(string path){foreach(var m in done.OrderByDescending(m=>m.from.Length))if(path==m.from||path.StartsWith(m.from+"/"))return m.to+path.Substring(m.from.Length);return path;}
  void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;if(Directory.Exists(path)){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);if(AssetDatabase.IsValidFolder(path))return;}string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);string guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(path));if(string.IsNullOrEmpty(guid)||AssetDatabase.GUIDToAssetPath(guid)!=path)throw new Exception("Cannot create exact folder "+path);}
  var replacements=new Dictionary<string,string>();foreach(var m in plan.moves){replacements[m.from]=m.to;replacements[m.from.Replace('/','\\')]=m.to.Replace('/','\\');replacements[m.from.Replace("/","\\\\")]=m.to.Replace("/","\\\\");}
  // Template layout path is relative to Application.dataPath, rather than Assets/.
  replacements["TutorialInfo/Layout.wlt"]="Game/Documentation/TutorialInfo/Layout.wlt";
  string pattern=string.Join("|",replacements.Keys.OrderByDescending(s=>s.Length).Select(Regex.Escape));var regex=new Regex(pattern);var utf8=new UTF8Encoding(false,true);
  EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
  try
  {
   foreach(string p in plan.folders)Folder(p);
   foreach(var m in plan.moves)
   {
    string from=Mapped(m.from);Folder(Path.GetDirectoryName(m.to).Replace('\\','/'));
    if(!Directory.Exists(from)&&!File.Exists(from)&&(Directory.Exists(m.to)||File.Exists(m.to)))
    {
     string original=m.from+".meta";var baseline=Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("Docs/ProjectStructure/Validation/BeforeFiles.json"));string expected=baseline.First(x=>(string)x["path"]==original).Value<string>("guid");if(AssetDatabase.AssetPathToGUID(m.to)!=expected)throw new Exception("Unexpected destination GUID "+m.to);done.Add(m);continue;
    }
    string error=AssetDatabase.ValidateMoveAsset(from,m.to);if(!string.IsNullOrEmpty(error))throw new Exception(from+" => "+m.to+": "+error);
    error=AssetDatabase.MoveAsset(from,m.to);if(!string.IsNullOrEmpty(error))throw new Exception(error);done.Add(m);File.WriteAllText("Docs/ProjectStructure/Validation/MoveProgress.json",Newtonsoft.Json.JsonConvert.SerializeObject(done,Newtonsoft.Json.Formatting.Indented));
   }
   if(Directory.Exists(plan.emptyContainer)){if(Directory.EnumerateFileSystemEntries(plan.emptyContainer).Any())throw new Exception("Legacy container unexpectedly nonempty");if(!AssetDatabase.DeleteAsset(plan.emptyContainer))throw new Exception("Cannot remove empty legacy container");}
   var extensions=new HashSet<string>{".cs",".py",".ps1",".md",".json",".asset",".prefab",".unity",".meta",".shadergraph",".shadersubgraph",".hlsl",".shader",".uxml",".uss",".wlt",".asmdef",".asmref",".txt",".xml",".yaml",".yml"};
   foreach(var root in new[]{"Assets","Docs","ArtSource","ProjectSettings"})foreach(string file in Directory.GetFiles(root,"*",SearchOption.AllDirectories))
   {
    string p=file.Replace('\\','/');if(p.StartsWith("Docs/ProjectStructure/")||p.Contains("/Backups/")||p.Contains("/Source/"))continue;
    string ext=Path.GetExtension(p);if(!extensions.Contains(ext))continue;if((p.StartsWith("Docs/")||p.Contains("/Validation/"))&&ext==".json")continue;
    byte[] data=File.ReadAllBytes(file);if(data.Contains((byte)0))continue;string text;try{text=utf8.GetString(data);}catch(DecoderFallbackException){continue;}
    string updated=regex.Replace(text,m=>replacements[m.Value]);if(updated==text)continue;File.WriteAllBytes(file,utf8.GetBytes(updated));changed.Add(p);
   }
   File.WriteAllText("Docs/ProjectStructure/Validation/MigrationResult.json",Newtonsoft.Json.JsonConvert.SerializeObject(new Result{moves=done.ToArray(),updatedFiles=changed.ToArray()},Newtonsoft.Json.Formatting.Indented));
  }
  finally{AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();AssetDatabase.Refresh();}
  return new{moved=done.Count,updatedPaths=changed.Count};
 }
 [Serializable]class Result{public Move[] moves;public string[] updatedFiles;}
}
