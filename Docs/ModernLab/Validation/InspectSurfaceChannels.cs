using System;using System.Linq;using UnityEngine;using UnityEditor;
public static class InspectSurfaceChannels
{
 public static object Run()
 {
  string[] paths={"Assets/Game/Environment/ModernLab/Textures/Lab_Floor_Resin_Mask.png","Assets/Game/Environment/ModernLab/Scenes/ModernLabRoomReflection.cubemap"};
  var data=new System.Collections.Generic.List<object>();
  foreach(string path in paths)
  {
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;bool readable=importer&&importer.isReadable;
   try
   {
    if(importer){importer.isReadable=true;importer.SaveAndReimport();}
    if(path.EndsWith("png")){var p=AssetDatabase.LoadAssetAtPath<Texture2D>(path).GetPixels();data.Add(new{path,alphaMin=p.Min(c=>c.a),alphaMean=p.Average(c=>c.a),alphaMax=p.Max(c=>c.a)});}
    else{var t=AssetDatabase.LoadAssetAtPath<Cubemap>(path);foreach(CubemapFace face in Enum.GetValues(typeof(CubemapFace)))if(face!=CubemapFace.Unknown){var p=t.GetPixels(face);data.Add(new{path,face=face.ToString(),mean=p.Average(c=>c.maxColorComponent),max=p.Max(c=>c.maxColorComponent)});}}
   }
   finally{if(importer){importer.isReadable=readable;importer.SaveAndReimport();}}
  }
  return data;
 }
}
