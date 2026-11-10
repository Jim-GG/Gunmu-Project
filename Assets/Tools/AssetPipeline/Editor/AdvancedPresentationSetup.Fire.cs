using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using GameActions;
namespace AssetPipeline.Editor
{
 public static partial class AdvancedPresentationSetup
 {
  static void FireLighting()
  {
   var data=Library();var samples=JArray.Parse(File.ReadAllText(Stage+"surface_explosion_mounts.json"));data.blastSurfacePoints=new Vector3[samples.Count];data.blastSurfaceNormals=new Vector3[samples.Count];for(int i=0;i<samples.Count;i++){var p=samples[i]["surface"];var n=samples[i]["normal"];data.blastSurfacePoints[i]=new Vector3((float)p[0],(float)p[1],(float)p[2]);data.blastSurfaceNormals[i]=new Vector3((float)n[0],(float)n[1],(float)n[2]);}data.finalExplosionOnSurface=true;data.finalSurfaceOutset=.55f;
   foreach(bool big in new[]{false,true})
   {
    string path=AssetDatabase.GetAssetPath(big?data.finalExplosion:data.smallExplosion);var go=PrefabUtility.LoadPrefabContents(path);
    try{var child=go.transform.Find("Orange fire illumination");if(!child){child=new GameObject("Orange fire illumination").transform;child.SetParent(go.transform,false);}var light=child.GetComponent<Light>();if(!light)light=child.gameObject.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.color=new Color(1,.27f,.045f);light.range=big?12:6;light.intensity=0;var pulse=child.GetComponent<VFXLightPulse>();if(!pulse)pulse=child.gameObject.AddComponent<VFXLightPulse>();pulse.point=light;pulse.duration=big?1.5f:.75f;pulse.peakIntensity=big?80:24;PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}
   }
   EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
  }
 }
}
