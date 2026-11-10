using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AssetPipeline.Editor
{
 public static class CombatVFXShortcuts
 {
  const string Root="Assets/Game/VFX/Combat/Advanced/";
  static void Select(string path){Selection.activeObject=AssetDatabase.LoadMainAssetAtPath(path);EditorGUIUtility.PingObject(Selection.activeObject);}
  [MenuItem("Tools/Combat VFX/Select settings")]static void Settings()=>Select(Root+"Resources/AdvancedCombatVFX.asset");
  [MenuItem("Tools/Combat VFX/Select Sparks Fine graph")]static void Sparks()=>Select(Root+"Graphs/VFX_SparksFine.vfx");
  [MenuItem("Tools/Combat VFX/Select small explosion graph")]static void Small()=>Select(Root+"Graphs/VFX_ExplosionSmall.vfx");
  [MenuItem("Tools/Combat VFX/Select big explosion graph")]static void Big()=>Select(Root+"Graphs/VFX_ExplosionFinal.vfx");
  [MenuItem("Tools/Combat VFX/Select wind source prefab")]static void Wind()=>Select("Assets/Game/VFX/Combat/ReconstructedWindCone/Prefabs/风锥.prefab");
  [MenuItem("Tools/Combat VFX/Open review scene")]static void Review(){if(EditorApplication.isPlaying){Debug.Log("Stop Play before opening the VFX review scene.");return;}if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(AdvancedVFXReviewSetup.ScenePath);}
 }
}
