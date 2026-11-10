using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;using System.Collections.Generic;using Newtonsoft.Json;using UnityEditor;using UnityEngine;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class CombatGraphProbe
 {
  const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;const string Stage="AssetStaging/VFXUpgrade/";
  static CombatGraphProbe(){EditorApplication.update+=Poll;}
  static object P(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var p=t.GetProperty(n,F|BindingFlags.DeclaredOnly);if(p!=null)return p.GetValue(o);var f=t.GetField(n,F|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(o);}return null;}
  static Type T(string n)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).FirstOrDefault(t=>t!=null);
  static object Call(object o,string n,params object[] args){var type=o as Type??o.GetType();var m=type.GetMethods(F).First(x=>x.Name==n&&x.GetParameters().Length>=args.Length&&x.GetParameters().Skip(args.Length).All(p=>p.IsOptional));var ps=m.GetParameters();var full=new object[ps.Length];for(int i=0;i<ps.Length;i++)full[i]=i<args.Length?args[i]:ps[i].DefaultValue;return m.Invoke(o is Type?null:o,full);}
  static object Slot(object o,int depth){var ch=P(o,"children") as IEnumerable;return new{type=o.GetType().Name,name=P(o,"name")?.ToString(),value=P(o,"value")?.ToString(),children=depth<3&&ch!=null?ch.Cast<object>().Select(c=>Slot(c,depth+1)).ToArray():null};}
  static object Model(object o)=>new{type=o.GetType().FullName,name=P(o,"name")?.ToString(),inputs=(P(o,"inputSlots") as IEnumerable)?.Cast<object>().Select(c=>Slot(c,0)).ToArray(),children=(P(o,"children") as IEnumerable)?.Cast<object>().Select(Model).ToArray()};
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"GraphProbe.request"))return;File.Delete(Stage+"GraphProbe.request");try{
   const string path="Assets/Game/VFX/Combat/Graphs/SparkBurst.vfx";if(!File.Exists(path)){File.Copy("Library/PackageCache/com.unity.visualeffectgraph@a963c7d0e87b/Editor/Templates/03_Simple_Burst.vfx",path);AssetDatabase.ImportAsset(path);}
   var resource=Call(T("UnityEditor.VFX.VisualEffectResource"),"GetResourceAtPath",path);var graph=Call(T("UnityEditor.VFX.VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);
   File.WriteAllText(Stage+"graph_probe.json",JsonConvert.SerializeObject(new{vfx=Model(graph),shaderTypes=new[]{"UnityEditor.ShaderGraph.GraphData","UnityEditor.ShaderGraph.Vector4MaterialSlot","UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"}.Select(n=>new{name=n,found=T(n)!=null,assembly=T(n)?.Assembly.GetName().Name})},Formatting.Indented));
  }catch(Exception e){File.WriteAllText(Stage+"graph_probe.json",e.ToString());Debug.LogException(e);}}
 }
}
