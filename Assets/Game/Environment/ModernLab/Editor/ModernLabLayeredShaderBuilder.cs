using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEditor;
namespace ModernLab.Editor
{
 public static class ModernLabLayeredShaderBuilder
 {
  const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  static Type Type(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null)??throw new Exception("Shader Graph type missing: "+name);
  static object Create(Type type,params object[] args)
  {
   foreach(var ctor in type.GetConstructors(Flags))if(Bind(ctor.GetParameters(),args,out var values))return ctor.Invoke(values);
   throw new Exception("Constructor not found: "+type.FullName+" arguments "+args.Length);
  }
  static bool Bind(ParameterInfo[] p,object[] args,out object[] values)
  {
   values=new object[p.Length];if(args.Length>p.Length)return false;
   for(int i=0;i<p.Length;i++){if(i>=args.Length){if(!p[i].IsOptional)return false;values[i]=p[i].DefaultValue;}else{var v=args[i];if(v!=null&&!p[i].ParameterType.IsInstanceOfType(v))return false;values[i]=v;}}return true;
  }
  static object Call(object target,string name,params object[] args)
  {
   var type=target as Type??target.GetType();foreach(var m in type.GetMethods(Flags).Where(m=>m.Name==name&&!m.IsGenericMethodDefinition))if(Bind(m.GetParameters(),args,out var values))return m.Invoke(target is Type?null:target,values);
   throw new Exception("Method not found: "+type.FullName+"."+name);
  }
  static object Get(object target,string name)
  {for(var t=target as Type??target.GetType();t!=null;t=t.BaseType){var p=t.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(p!=null)return p.GetValue(target is Type?null:target);var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(target is Type?null:target);}throw new Exception("Member missing: "+name);}
  static void Set(object target,string name,object value)
  {for(var t=target.GetType();t!=null;t=t.BaseType){var p=t.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(p!=null&&p.CanWrite){p.SetValue(target,value);return;}var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(target,value);return;}}throw new Exception("Set member missing: "+name);}
  static object New(string type,params object[] args)=>Create(Type("UnityEditor.ShaderGraph."+type),args);
  static void Position(object node,float x,float y){var state=Get(node,"drawState");Set(state,"position",new Rect(x,y,220,140));Set(node,"drawState",state);}
  public static object Build()
  {
   const string path="Assets/Game/Environment/ModernLab/Shaders/ModernLabLayered.shadergraph";const string include="Assets/Game/Environment/ModernLab/Shaders/LabSurface.hlsl";
   Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.ImportAsset(include,ImportAssetOptions.ForceSynchronousImport);
   var graph=New("GraphData");Call(graph,"AddContexts");Call(graph,"AddCategory",Call(Type("UnityEditor.ShaderGraph.CategoryData"),"DefaultCategory"));Set(graph,"path","ModernLab");Set(Get(graph,"vertexContext"),"position",new Vector2(600,-200));Set(Get(graph,"fragmentContext"),"position",new Vector2(600,100));
   var target=Create(Type("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"));Call(target,"TrySetActiveSubTarget",Type("UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget"));
   var targets=Array.CreateInstance(Type("UnityEditor.ShaderGraph.Target"),1);targets.SetValue(target,0);
   var descriptors=new List<object>();var fields=Type("UnityEditor.ShaderGraph.BlockFields");
   foreach(var name in new[]{"Position","Normal","Tangent"})descriptors.Add(Get(fields.GetNestedType("VertexDescription",Flags),name));
   foreach(var name in new[]{"BaseColor","NormalTS","Metallic","Smoothness","Emission","Occlusion"})descriptors.Add(Get(fields.GetNestedType("SurfaceDescription",Flags),name));
   var array=Array.CreateInstance(Type("UnityEditor.ShaderGraph.BlockFieldDescriptor"),descriptors.Count);for(int i=0;i<descriptors.Count;i++)array.SetValue(descriptors[i],i);Call(graph,"InitializeOutputs",targets,array);
   var fn=New("CustomFunctionNode");Set(fn,"functionName","LabSurface");Set(fn,"functionSource",AssetDatabase.AssetPathToGUID(include));Set(fn,"precision",Enum.Parse(Type("UnityEditor.ShaderGraph.Internal.Precision"),"Single"));Call(graph,"AddNode",fn);Position(fn,-180,100);
   var slotType=Type("UnityEditor.Graphing.SlotType");object input=Enum.Parse(slotType,"Input"),output=Enum.Parse(slotType,"Output");
   void Slot(int id,string name,string kind,bool isOutput=false)
   {
    object slot;
    if(kind=="Texture2DInput")slot=New(kind+"MaterialSlot",id,name,name);
    else{var type=Type("UnityEditor.ShaderGraph."+kind+"MaterialSlot");var ctor=type.GetConstructors(Flags).First(c=>c.GetParameters().Length>=5);var valueType=ctor.GetParameters()[4].ParameterType;slot=Create(type,id,name,name,isOutput?output:input,Activator.CreateInstance(valueType));}
    Call(fn,"AddSlot",slot);
   }
   Slot(0,"UV","Vector2");foreach(var pair in new[]{(1,"BaseMap"),(2,"NormalMap"),(3,"MaskMap"),(4,"MicroNormal")})Slot(pair.Item1,pair.Item2,"Texture2DInput");Slot(5,"Tint","Vector4");Slot(6,"DustColour","Vector4");
   string[] floats={"DustAmount","DustColourStrength","DustRoughness","SpotAmount","NormalStrength","MicroStrength","MicroTiling","RoughnessBias","SurfaceTiling","DustTiling"};for(int i=0;i<floats.Length;i++)Slot(i+7,floats[i],"Vector1");
   foreach(var v in new[]{(20,"Albedo","Vector3"),(21,"Normal","Vector3"),(22,"Metallic","Vector1"),(23,"Smoothness","Vector1"),(24,"Occlusion","Vector1")})Slot(v.Item1,v.Item2,v.Item3,true);
   void Connect(object from,int source,object to,int dest)=>Call(graph,"Connect",Call(from,"GetSlotReference",source),Call(to,"GetSlotReference",dest));
   var uv=New("UVNode");Call(graph,"AddNode",uv);Position(uv,-850,0);Connect(uv,0,fn,0);
   int index=0;
   void Property(string kind,string reference,string display,object value,int dest)
   {
    var property=Create(Type("UnityEditor.ShaderGraph.Internal."+kind+"ShaderProperty"));Set(property,"displayName",display);Set(property,"overrideReferenceName",reference);Set(property,"generatePropertyBlock",true);if(value!=null)Set(property,"value",value);
    Call(graph,"AddGraphInput",property);var node=New("PropertyNode");Call(graph,"AddNode",node);Set(node,"property",property);Position(node,-800,180+(index++)*150);Connect(node,0,fn,dest);
   }
   Property("Texture2D","_BaseMap","Base Color RGB / Spots A",null,1);Property("Texture2D","_BumpMap","Main Normal",null,2);Property("Texture2D","_SurfaceMask","Metal R / AO G / Dust B / Smooth A",null,3);Property("Texture2D","_MicroNormal","Shared Micro Normal",null,4);
   Property("Color","_BaseColor","Paint Tint",Color.white,5);Property("Color","_DustColour","Neutral Dust Colour",new Color(.56f,.57f,.56f),6);
   float[] defaults={.1f,.01f,.45f,1,1,.4f,8,0,1,2};for(int i=0;i<floats.Length;i++)Property("Vector1","_"+floats[i],floats[i],defaults[i],i+7);
   var method=graph.GetType().GetMethods(Flags).Single(m=>m.Name=="GetNodes"&&m.IsGenericMethodDefinition);var blocks=(IEnumerable)method.MakeGenericMethod(Type("UnityEditor.ShaderGraph.BlockNode")).Invoke(graph,null);
   var outputSlots=new Dictionary<string,int>{{"BaseColor",20},{"NormalTS",21},{"Metallic",22},{"Smoothness",23},{"Occlusion",24}};
   foreach(var block in blocks){var name=(string)Get(Get(block,"descriptor"),"name");if(outputSlots.TryGetValue(name,out int s))Connect(fn,s,block,0);}
   Call(graph,"ValidateGraph");var json=(string)Call(Type("UnityEditor.ShaderGraph.Serialization.MultiJson"),"Serialize",graph);File.WriteAllText(path,json);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Layered Shader Graph failed import");
   return new{path,shader=shader.name};
  }
 }
}
