using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEditor;using UnityEngine;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static partial class CombatShaderAuthoring
 {
  const string Stage="AssetStaging/VFXUpgrade/",Root="Assets/Game/VFX/Combat/Graphs/",SG="UnityEditor.ShaderGraph.";static object graph;
  static CombatShaderAuthoring(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"ShaderBuild.request"))return;File.Delete(Stage+"ShaderBuild.request");try{Refraction();File.WriteAllText(Stage+"shader_build.json","{\"passed\":true}");}catch(Exception e){File.WriteAllText(Stage+"shader_build.json",e.ToString());Debug.LogException(e);}}
  static object Node(string type,float x,float y){var n=GraphReflect.New(SG+type);GraphReflect.Call(graph,"AddNode",n);GraphReflect.Place(n,x,y);return n;}
  static void Link(object a,int ap,object b,int bp)=>GraphReflect.Call(graph,"Connect",GraphReflect.Call(a,"GetSlotReference",ap),GraphReflect.Call(b,"GetSlotReference",bp));
  static object Prop(string type,string name,string reference,object value,float x,float y)
  {
   var p=GraphReflect.New(SG+"Internal."+type);GraphReflect.Set(p,"displayName",name);GraphReflect.Set(p,"overrideReferenceName",reference);
   if(value is Texture2D texture){var wrapped=GraphReflect.Get(p,"value");GraphReflect.Set(wrapped,"texture",texture);GraphReflect.Set(p,"value",wrapped);}else GraphReflect.Set(p,"value",value);
   GraphReflect.Call(graph,"AddGraphInput",p);var node=Node("PropertyNode",x,y);GraphReflect.Set(node,"property",p);return node;
  }
  static object Function(string name,string body,(string name,object value)[] inputs,(string name,object value)[] outputs,float x,float y)
  {
   var node=Node("CustomFunctionNode",x,y);GraphReflect.Set(node,"sourceType","String");GraphReflect.Set(node,"functionName",name);GraphReflect.Set(node,"functionBody",body);int id=0;
   foreach(var group in new[]{inputs,outputs})foreach(var slot in group){string type=slot.value is Vector4?"Vector4MaterialSlot":slot.value is Vector3?"Vector3MaterialSlot":slot.value is Vector2?"Vector2MaterialSlot":"Vector1MaterialSlot";GraphReflect.Call(node,"AddSlot",GraphReflect.New(SG+type,id++,slot.name,slot.name,ReferenceEquals(group,inputs)?"Input":"Output",slot.value));}
   return node;
  }
  static void Refraction()
  {
   graph=GraphReflect.New(SG+"GraphData");GraphReflect.Call(graph,"AddContexts");GraphReflect.Set(graph,"path","Game/VFX");
   var target=GraphReflect.New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");GraphReflect.Call(target,"TrySetActiveSubTarget",GraphReflect.T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"));GraphReflect.Set(target,"surfaceType","Transparent");GraphReflect.Set(target,"alphaMode","Alpha");GraphReflect.Set(target,"renderFace","Both");
   var targets=Array.CreateInstance(GraphReflect.T(SG+"Target"),1);targets.SetValue(target,0);string[] blocks={"BaseColor","Alpha"};var descriptors=Array.CreateInstance(GraphReflect.T(SG+"BlockFieldDescriptor"),2);for(int i=0;i<2;i++)descriptors.SetValue(GraphReflect.Field(SG+"BlockFields+SurfaceDescription",blocks[i]),i);GraphReflect.Call(graph,"InitializeOutputs",targets,descriptors);
   var screen=Node("ScreenPositionNode",-1000,-350);var uv=Node("UVNode",-1000,-100);var vertex=Node("VertexColorNode",-1000,150);
   var distortion=Prop("Vector1ShaderProperty","Distortion","_Distortion",.035f,-1000,400);var chromatic=Prop("Vector1ShaderProperty","Chromatic Split","_Chromatic",.006f,-1000,600);var opacity=Prop("Vector1ShaderProperty","Opacity","_Opacity",.8f,-1000,800);var mode=Prop("Vector1ShaderProperty","Mode: Ring 0 / Sphere 1 / Steam 2","_Mode",0f,-1000,1000);var progress=Prop("Vector1ShaderProperty","Progress","_Progress",0f,-1000,1200);
   var flow=Function("FlowUV",@"float2 p=UV*2-1; float r=length(p); float n=(NoiseValue-.5)*1.2+sin(p.x*14+_Time.y*3+sin(p.y*9-_Time.y*2))*sin(p.y*11-_Time.y*2)*.45; float2 dir=normalize(p+0.001);float2 turbulence=float2(sin(p.y*17+_Time.y*2),cos(p.x*13-_Time.y*3));float ring=smoothstep(.28,.55,r)*(1-smoothstep(.82,1,r));float sphere=pow(1-saturate(abs(dot(normalize(Normal),normalize(View)))),1.5);float steam=(1-smoothstep(.3,1,r))*.25;float mask=Mode<.5?ring:Mode<1.5?sphere:steam;mask*=saturate(1-Progress)*Color.a;float2 offset=(dir*(.6+n*.4)+turbulence*.3)*Distortion*mask;R=float4(Screen.xy+offset+dir*Chromatic*mask,0,1);G=float4(Screen.xy+offset,0,1);B=float4(Screen.xy+offset-dir*Chromatic*mask,0,1);Alpha=saturate(mask*Opacity);",new[]{("Screen",(object)Vector4.zero),("UV",(object)Vector2.zero),("Color",(object)Vector4.one),("Distortion",(object).035f),("Chromatic",(object).006f),("Opacity",(object).8f),("Mode",(object)0f),("Progress",(object)0f),("NoiseValue",(object).5f),("Normal",(object)Vector3.up),("View",(object)Vector3.forward)},new[]{("R",(object)Vector4.zero),("G",(object)Vector4.zero),("B",(object)Vector4.zero),("Alpha",(object)0f)},-550,0);
   Link(screen,0,flow,0);Link(uv,0,flow,1);Link(vertex,0,flow,2);Link(distortion,0,flow,3);Link(chromatic,0,flow,4);Link(opacity,0,flow,5);Link(mode,0,flow,6);Link(progress,0,flow,7);var noiseProp=Prop("Texture2DShaderProperty","Flow Noise","_NoiseTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/VFX/Combat/Textures/Noise_001.png"),-1400,1500);var noiseSample=Node("SampleTexture2DNode",-1050,1550);Link(noiseProp,0,noiseSample,1);Link(uv,0,noiseSample,2);Link(noiseSample,4,flow,8);var normal=Node("NormalVectorNode",-800,1750);var view=Node("ViewDirectionNode",-500,1750);GraphReflect.Set(normal,"m_Space","World");GraphReflect.Set(view,"m_Space","World");Link(normal,0,flow,9);Link(view,0,flow,10);
   var red=Node("SceneColorNode",-150,-200);var green=Node("SceneColorNode",-150,100);var blue=Node("SceneColorNode",-150,400);Link(flow,11,red,0);Link(flow,12,green,0);Link(flow,13,blue,0);
   var combine=Function("ChromaticCombine","Out=float3(R.r,G.g,B.b);",new[]{("R",(object)Vector3.zero),("G",(object)Vector3.zero),("B",(object)Vector3.zero)},new[]{("Out",(object)Vector3.zero)},200,50);Link(red,1,combine,0);Link(green,1,combine,1);Link(blue,1,combine,2);
   var context=GraphReflect.Get(graph,"fragmentContext");foreach(var reference in GraphReflect.List(GraphReflect.Get(context,"blocks"))){var block=GraphReflect.Get(reference,"value");string name=GraphReflect.Get(GraphReflect.Get(block,"descriptor"),"name").ToString();if(name.Contains("BaseColor"))Link(combine,3,block,0);else if(name.Contains("Alpha"))Link(flow,14,block,0);}
   var json=GraphReflect.Call(GraphReflect.T(SG+"Serialization.MultiJson"),"Serialize",graph).ToString();File.WriteAllText(Root+"ResonanceRefraction.shadergraph",json);AssetDatabase.ImportAsset(Root+"ResonanceRefraction.shadergraph",ImportAssetOptions.ForceUpdate);
  }
 }
}




