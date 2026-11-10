using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEditor;using UnityEngine;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class SparkGraphAuthoring
 {
  const string Stage="AssetStaging/VFXUpgrade/",NS="UnityEditor.VFX.";
  static SparkGraphAuthoring(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"SparkGraphBuild.request"))return;File.Delete(Stage+"SparkGraphBuild.request");try{Build();File.WriteAllText(Stage+"spark_graph_build.json","{\"passed\":true}");}catch(Exception e){File.WriteAllText(Stage+"spark_graph_build.json",e.ToString());Debug.LogException(e);}}
  static object[] Children(object o)=>GraphReflect.List(GraphReflect.Get(o,"children"));
  static object[] Inputs(object o)=>GraphReflect.List(GraphReflect.Get(o,"inputSlots"));
  static object Slot(object node,string path)
  {
   object found=node;foreach(var part in path.Split('/')){var candidates=ReferenceEquals(found,node)?Inputs(found):Children(found);found=candidates.FirstOrDefault(s=>string.Equals(GraphReflect.Get(s,"name").ToString(),part,StringComparison.OrdinalIgnoreCase))??throw new Exception("Slot "+path+" missing; available: "+string.Join(",",candidates.Select(s=>GraphReflect.Get(s,"name").ToString())));}return found;
  }
  static void Value(object node,string path,object value)=>GraphReflect.Set(Slot(node,path),"value",value);
  static object Block(string type,object parent){var b=ScriptableObject.CreateInstance(GraphReflect.T(NS+"Block."+type));GraphReflect.Call(parent,"AddChild",b);return b;}
  static object Parameter(object graph,string name,Vector3 value,object input)
  {
   var p=ScriptableObject.CreateInstance(GraphReflect.T(NS+"VFXParameter"));GraphReflect.Call(p,"Init",typeof(Vector3));GraphReflect.Call(p,"SetSettingValue","m_ExposedName",name);GraphReflect.Call(p,"SetSettingValue","m_Exposed",true);GraphReflect.Call(graph,"AddChild",p);GraphReflect.Set(p,"value",value);GraphReflect.Call(input,"Link",GraphReflect.List(GraphReflect.Get(p,"outputSlots"))[0]);return p;
  }
  static void Build()
  {
   const string path="Assets/Game/VFX/Combat/Graphs/SparkBurst.vfx";File.Copy("Library/PackageCache/com.unity.visualeffectgraph@a963c7d0e87b/Editor/Templates/03_Simple_Burst.vfx",path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
   var resource=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResource"),"GetResourceAtPath",path);var graph=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);var contexts=Children(graph);var spawn=contexts.Single(c=>c.GetType().Name=="VFXBasicSpawner");var init=contexts.Single(c=>c.GetType().Name=="VFXBasicInitialize");var update=contexts.Single(c=>c.GetType().Name=="VFXBasicUpdate");var output=contexts.Single(c=>c.GetType().Name=="VFXPlanarPrimitiveOutput");
   Value(Children(spawn).Single(),"Count",56f);var data=GraphReflect.Call(init,"GetData");GraphReflect.Set(data,"space","Local");
   foreach(var b in Children(init))
   {
    string name=GraphReflect.Get(b,"name").ToString();if(name.Contains("Lifetime")){Value(b,"A",.45f);Value(b,"B",1.1f);}if(name.Contains("Velocity")){Value(b,"MinSpeed",3f);Value(b,"MaxSpeed",10f);Value(b,"DirectionBlend",.8f);}
   }
   Value(init,"bounds/center",Vector3.up);Value(init,"bounds/size",Vector3.one*25);
   var gravity=Children(update).First(b=>b.GetType().Name=="Gravity");GraphReflect.Set(Slot(gravity,"Force"),"space","World");var size=Block("SetAttribute",init);GraphReflect.Call(size,"SetSettingValue","attribute","size");GraphReflect.Set(Inputs(size)[0],"value",.025f);
   var collision=Block("CollisionShape",update);Value(collision,"Bounce",.35f);Value(collision,"Friction",2f);Value(collision,"LifetimeLoss",.12f);Parameter(graph,"GroundPoint",Vector3.zero,Slot(collision,"Plane/position"));Parameter(graph,"GroundNormal",Vector3.up,Slot(collision,"Plane/normal"));
   var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(8,5,1),0),new GradientColorKey(new Color(3,.8f,.08f),.3f),new GradientColorKey(new Color(.7f,.1f,0),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
   foreach(var b in Children(output)){string name=GraphReflect.Get(b,"name").ToString();if(name.Contains("Color"))Value(b,"Color",gradient);if(name.Contains("Size"))Value(b,"Size",AnimationCurve.Linear(0,1,1,0));}
   GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"WriteAssetWithSubAssets",resource);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  }
 }
}




