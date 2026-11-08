using System;using System.IO;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;
public static class PrepareTraversalV2Live
{
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Already playing");
  for(int i=0;i<SceneManager.sceneCount;i++)
  {
   var s=SceneManager.GetSceneAt(i);if(!s.isDirty)continue;if(string.IsNullOrEmpty(s.path))throw new Exception("Unsaved untitled scene: preserve it before switching");
   Directory.CreateDirectory("ArtSource/Crystal/V2/Backups");EditorSceneManager.SaveScene(s,"ArtSource/Crystal/V2/Backups/"+Path.GetFileNameWithoutExtension(s.path)+"_BeforeLive.unity",true);EditorSceneManager.SaveScene(s);
  }
  EditorSceneManager.OpenScene(CrystalParkour.Editor.CrystalTraversalLabBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;return true;
 }
}
