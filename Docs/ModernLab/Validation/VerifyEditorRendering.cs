using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEditor;
public static class VerifyEditorRendering
{
 public static async Task<object> Run()
 {
  var errors=new List<string>();Application.LogCallback capture=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=capture;
  try{for(int i=0;i<6;i++){SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(500);}}
  finally{Application.logMessageReceived-=capture;}
  return new{passed=errors.Count==0,errors=errors.Distinct().ToArray(),count=errors.Count};
 }
}
