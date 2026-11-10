using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using Newtonsoft.Json.Linq;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad] public static class LevelFeedbackBatch
 {
  const string Key="LevelFeedbackBatch";
  static LevelFeedbackBatch(){if(Application.isBatchMode)Start();}
  public static void Run()=>Start();
  static void Start()
  {
   Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.dataPath));
   foreach(var dir in new[]{"Docs/Integration","AssetStaging/LevelFeedbackIntegration","AssetStaging/UDK7M/Boss","AssetStaging/VFXUpgrade","AssetStaging/VFXPhase2"})Directory.CreateDirectory(dir);
   if(!SessionState.GetBool(Key,false)){SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Phase",0);SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddMinutes(10).ToString("o"));}
   EditorApplication.update-=Poll;EditorApplication.update+=Poll;
  }
  static void Poll()
  {
   if(!SessionState.GetBool(Key,false))return;
   int phase=SessionState.GetInt(Key+"Phase",0);
   if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Key+"Deadline",DateTime.UtcNow.AddMinutes(10).ToString("o")),null,System.Globalization.DateTimeStyles.RoundtripKind)){Finish(false,"Batch timeout at "+phase);return;}
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   string path=phase==0?"Docs/Integration/level-feedback-validation.json":phase==1?"Docs/Integration/unity-play-smoke.json":phase==2?"AssetStaging/UDK7M/Boss/three_phase_validation.json":"Docs/Integration/unity-scene-audit.json";
   string request=phase==0?"AssetStaging/LevelFeedbackIntegration/Validate.request":phase==1?"Docs/Integration/play.request":phase==2?"AssetStaging/UDK7M/Boss/ThreePhaseCheck.request":"Docs/Integration/audit.request";
   if(!File.Exists(path)){if(!File.Exists(request))File.WriteAllText(request,"batch");return;}
   var report=JObject.Parse(File.ReadAllText(path));
   if(phase<3&&!(bool)report["passed"]){Finish(false,path+": "+report["error"]);return;}
   if(phase<3){SessionState.SetInt(Key+"Phase",phase+1);return;}
   bool ok=report["reports"].Count()==15&&report["reports"].All(r=>r["error"]==null&&!r["missingScripts"].Any()&&!r["brokenReferences"].Any());Finish(ok,ok?null:"Scene audit failed");
  }
  static void Finish(bool ok,string error){File.WriteAllText("Docs/Integration/batch-result.json",new JObject(new JProperty("passed",ok),new JProperty("phase",SessionState.GetInt(Key+"Phase",0)),new JProperty("error",error),new JProperty("time",DateTimeOffset.Now)).ToString());SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(ok?0:1);}
 }
}
