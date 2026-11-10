using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEditor;using UnityEngine;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static partial class AdvancedGPUAuthoring
 {
  const string NS="UnityEditor.VFX.",Root="Assets/Game/VFX/Combat/Advanced/",Stage="AssetStaging/VFXPhase2/",Templates="Library/PackageCache/com.unity.visualeffectgraph@a963c7d0e87b/Editor/Templates/";
  static object graph,resource,spawn,init,update,output;static string path;
  static AdvancedGPUAuthoring(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;if(File.Exists(Stage+"FireGlow.request")){File.Delete(Stage+"FireGlow.request");try{FireGlow();}catch(Exception e){File.WriteAllText(Stage+"fire_glow.json",e.ToString());Debug.LogException(e);}return;}if(File.Exists(Stage+"FinalExtras.request")){File.Delete(Stage+"FinalExtras.request");try{FinalExtras();}catch(Exception e){File.WriteAllText(Stage+"final_extras.json",e.ToString());Debug.LogException(e);}return;}if(File.Exists(Stage+"SparkFlash.request")){File.Delete(Stage+"SparkFlash.request");try{AppendSparkFlash();}catch(Exception e){File.WriteAllText(Stage+"spark_flash.json",e.ToString());Debug.LogException(e);}return;}if(!File.Exists(Stage+"GPU.request"))return;File.Delete(Stage+"GPU.request");try{Build();File.WriteAllText(Stage+"gpu_build.json","{\"passed\":true}");}catch(Exception e){File.WriteAllText(Stage+"gpu_build.json",path+"\n"+e);Debug.LogException(e);}};}
  static object[] Children(object o)=>GraphReflect.List(GraphReflect.Get(o,"children"));static object[] Inputs(object o)=>GraphReflect.List(GraphReflect.Get(o,"inputSlots"));
  static object Slot(object o,string path){object s=o;bool first=true;foreach(var part in path.Split('/')){var list=first?Inputs(s):Children(s);s=list.FirstOrDefault(x=>string.Equals(GraphReflect.Get(x,"name").ToString(),part,StringComparison.OrdinalIgnoreCase))??throw new Exception("Slot "+path+" missing: "+string.Join(",",list.Select(x=>GraphReflect.Get(x,"name"))));first=false;}return s;}
  static void SlotValue(object slot,object value)
  {
   var type=(Type)GraphReflect.Get(GraphReflect.Get(slot,"property"),"type");
   if(value!=null&&!type.IsInstanceOfType(value))
   {
    var conversion=type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).FirstOrDefault(m=>m.Name=="op_Implicit"&&m.ReturnType==type&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.IsInstanceOfType(value));
    if(conversion==null)throw new Exception("Cannot assign "+value.GetType()+" to VFX slot "+type);
    value=conversion.Invoke(null,new[]{value});
   }
   GraphReflect.Set(slot,"value",value);
  }
  static void Value(object o,string slot,object v)=>SlotValue(Slot(o,slot),v);
  static void Setting(object o,string name,object value){var current=GraphReflect.Call(o,"GetSettingValue",name);if(current!=null&&current.GetType().IsEnum&&value is string text)value=Enum.Parse(current.GetType(),text);GraphReflect.Call(o,"SetSettingValue",name,value);}
  static object Add(string type,object parent){var b=ScriptableObject.CreateInstance(GraphReflect.T(NS+type));GraphReflect.Call(parent,"AddChild",b);return b;}
  static void Remove(object parent,object child)=>GraphReflect.Call(parent,"RemoveChild",child);
  static object Attribute(object parent,string name,object a,object b=null,string composition="Overwrite")
  {
   var node=Add("Block.SetAttribute",parent);Setting(node,"attribute",name);Setting(node,"Composition",composition);if(b!=null){Setting(node,"Random",a is Vector3?"PerComponent":"Uniform");var slots=Inputs(node);SlotValue(slots[0],a);SlotValue(slots[1],b);}else SlotValue(Inputs(node)[0],a);return node;
  }
  static object Parameter(string name,object value,object input)
  {
   var p=Children(graph).FirstOrDefault(n=>n.GetType().Name=="VFXParameter"&&GraphReflect.Call(n,"GetSettingValue","m_ExposedName").ToString()==name);if(p==null){p=Add("VFXParameter",graph);GraphReflect.Call(p,"Init",value.GetType());Setting(p,"m_ExposedName",name);Setting(p,"m_Exposed",true);GraphReflect.Set(p,"value",value);}GraphReflect.Call(input,"Link",GraphReflect.List(GraphReflect.Get(p,"outputSlots"))[0]);return p;
  }
  static object Curve(object parent,string attribute,object curve,string composition="Multiply")
  {
   var n=Add("Block.AttributeFromCurve",parent);Setting(n,"attribute",attribute);Setting(n,"Composition",composition);if(curve is Gradient){Setting(n,"ColorMode","ColorAndAlpha");Setting(n,"AlphaComposition","Multiply");}GraphReflect.Set(Inputs(n)[0],"value",curve);return n;
  }
  static Gradient Fade(Color c,bool flash=false)
  {
   var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(new Color(c.r,c.g,c.b),0),new GradientColorKey(new Color(c.r*.8f,c.g*.8f,c.b*.8f),1)},flash?new[]{new GradientAlphaKey(c.a,0),new GradientAlphaKey(0,1)}:new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(c.a,.07f),new GradientAlphaKey(c.a*.8f,.5f),new GradientAlphaKey(0,1)});return g;
  }
  static void New(string name,string template="03_Simple_Burst")
  {
   path=Root+"Graphs/"+name+".vfx";File.Copy(Templates+template+".vfx",path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);resource=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResource"),"GetResourceAtPath",path);graph=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);var contexts=Children(graph);spawn=contexts.First(x=>x.GetType().Name=="VFXBasicSpawner");init=contexts.First(x=>x.GetType().Name=="VFXBasicInitialize");update=contexts.First(x=>x.GetType().Name=="VFXBasicUpdate");output=contexts.First(x=>x.GetType().Name=="VFXPlanarPrimitiveOutput");
  }
  static void Clear(object parent){foreach(var b in Children(parent))Remove(parent,b);}
  static void Bounds(object initialization,uint capacity=1024){Setting(initialization,"capacity",capacity);Value(initialization,"bounds/center",Vector3.up*2);Value(initialization,"bounds/size",Vector3.one*50);GraphReflect.Set(GraphReflect.Call(initialization,"GetData"),"space","Local");}
  static void Burst(float count){Clear(spawn);var b=Add("VFXSpawnerBurst",spawn);Value(b,"Count",count);}
  static void Basics(float count,float minLife,float maxLife,float minSize,float maxSize,Vector3 minVelocity,Vector3 maxVelocity,Vector3 gravity,float drag)
  {
   Burst(count);Clear(init);Bounds(init);Attribute(init,"lifetime",minLife,maxLife);Attribute(init,"position",new Vector3(-.12f,0,-.12f),new Vector3(.12f,.15f,.12f));Attribute(init,"size",minSize,maxSize);Attribute(init,"velocity",minVelocity,maxVelocity);Clear(update);var g=Add("Block.Gravity",update);Value(g,"Force",gravity);GraphReflect.Set(Slot(g,"Force"),"space","World");var d=Add("Block.Drag",update);Value(d,"dragCoefficient",drag);Clear(output);var orient=Add("Block.Orient",output);Setting(orient,"mode","FaceCameraPosition");
  }
  static void Flipbook(string texture,Color tint,float grow=2)
  {
   Setting(output,"uvMode","Flipbook");Setting(output,"flipbookBlendFrames",true);Value(output,"mainTexture",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));Value(output,"flipBookSize",new Vector2(8,8));Curve(output,"texIndex",AnimationCurve.Linear(0,0,1,63),"Overwrite");Curve(output,"color",Fade(tint),"Overwrite");Curve(output,"size",AnimationCurve.Linear(0,.6f,1,grow));
  }
  static object MeshOutput(object upd,object old,Mesh mesh)
  {
   GraphReflect.Call(upd,"UnlinkTo",old);Remove(graph,old);var n=Add("VFXMeshOutput",graph);GraphReflect.Call(upd,"LinkTo",n);Setting(n,"useBaseColorMap","None");Value(n,"mesh",mesh);return n;
  }
  static void GroundCollision(object upd)
  {
   var c=Add("Block.CollisionShape",upd);Value(c,"Bounce",.25f);Value(c,"Friction",1.5f);Parameter("GroundPoint",Vector3.zero,Slot(c,"Plane/position"));Parameter("GroundNormal",Vector3.up,Slot(c,"Plane/normal"));
  }
  static void Save(){GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"WriteAssetWithSubAssets",resource);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
  static void SimpleSystems()
  {
   foreach(bool small in new[]{false,true}){New(small?"VFX_ExplosionSmall":"VFX_ExplosionFinal");Basics(1,small?1.4f:2.1f,small?1.4f:2.1f,small?4.5f:12,small?4.5f:12,Vector3.zero,Vector3.zero,Vector3.zero,0);Flipbook(Root+"Textures/"+(small?"Explosion_14_8x8.tga":"Explosion_30_8x8.tga"),Color.white,1.25f);Save();}
   New("VFX_BlackSmokeBurst");Basics(36,3.5f,6,1,2.2f,new Vector3(-2,1,-2),new Vector3(2,3,2),Vector3.up*.4f,.55f);Flipbook(Root+"Textures/Smoke24_8x8.TGA",new Color(.07f,.07f,.08f,.75f),2.8f);Save();
   New("VFX_GroundDustBurst");Basics(120,2.3f,3.7f,.5f,1.2f,new Vector3(-8,.1f,-8),new Vector3(8,.5f,8),Vector3.zero,.9f);Flipbook("Assets/Game/VFX/Combat/Textures/Smoke_07_loop_8x8.tga",new Color(.42f,.38f,.33f,.38f),3);Save();
   New("VFX_ConcreteRubbleBurst");Basics(100,3,5,.025f,.085f,new Vector3(-9,1,-9),new Vector3(9,10,9),new Vector3(0,-9.81f,0),.25f);output=MeshOutput(update,output,AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/VFX/Combat/ConcreteFragment.asset"));Attribute(init,"angle",Vector3.zero,Vector3.one*6.28f);Curve(output,"color",Fade(new Color(.3f,.28f,.25f,1),true),"Overwrite");GroundCollision(update);Save();
   if(!File.Exists(Root+"Graphs/VFX_SparksFine.vfx")){New("VFX_SparksFine");Basics(88,.28f,.8f,.016f,.028f,new Vector3(-9,-2,-9),new Vector3(9,10,9),new Vector3(0,-9.81f,0),1);Attribute(init,"scale",new Vector3(.18f,1.6f,1));Setting(Children(output).First(),"mode","AlongVelocity");Setting(output,"blendMode","Additive");Curve(output,"color",Fade(new Color(12,3,.25f,1),true),"Overwrite");Curve(output,"size",AnimationCurve.Linear(0,1,1,.1f));GroundCollision(update);Save();}
  }
  static void Track()
  {
   New("VFX_TrackDust");Basics(0,.7f,1.7f,.25f,.65f,new Vector3(-.5f,.1f,-.5f),new Vector3(.5f,.5f,.5f),Vector3.up*.1f,.7f);Clear(spawn);var rate=Add("VFXSpawnerConstantRate",spawn);Parameter("SpawnRate",0f,Slot(rate,"Rate"));GraphReflect.Set(GraphReflect.Call(init,"GetData"),"space","World");var oldPosition=Children(init).First(x=>x.GetType().Name=="SetAttribute"&&GraphReflect.Call(x,"GetSettingValue","attribute").ToString()=="position");Remove(init,oldPosition);var position=Attribute(init,"position",Vector3.zero);Parameter("SpawnPosition",Vector3.zero,Inputs(position)[0]);var velocity=Attribute(init,"velocity",Vector3.zero,null,"Add");Parameter("EmissionVelocity",Vector3.zero,Inputs(velocity)[0]);Flipbook("Assets/Game/VFX/Combat/Textures/Smoke_07_loop_8x8.tga",new Color(.4f,.37f,.33f,.32f),3);Value(init,"bounds/center",Vector3.zero);Value(init,"bounds/size",Vector3.one*160);Save();
  }
  static void Crystal()
  {
   New("VFX_CrystalShatterGPU","06_Firework");var contexts=Children(graph);var inits=contexts.Where(x=>x.GetType().Name=="VFXBasicInitialize").ToArray();var updates=contexts.Where(x=>x.GetType().Name=="VFXBasicUpdate").ToArray();var outputs=contexts.Where(x=>x.GetType().Name=="VFXPlanarPrimitiveOutput").ToArray();init=inits[0];update=updates[0];output=outputs[0];Burst(48);Clear(init);Bounds(init,128);Attribute(init,"lifetime",1.1f,1.7f);Attribute(init,"position",new Vector3(-.1f,-.1f,-.1f),new Vector3(.1f,.1f,.1f));Attribute(init,"size",.06f,.14f);Attribute(init,"velocity",new Vector3(-10,-5,-10),new Vector3(10,11,10));Attribute(init,"color",new Vector3(.6f,.18f,1));Attribute(init,"angle",Vector3.zero,Vector3.one*6.28f);Clear(update);var gravity=Add("Block.Gravity",update);Value(gravity,"Force",new Vector3(0,-9.81f,0));var drag=Add("Block.Drag",update);Value(drag,"dragCoefficient",.4f);GroundCollision(update);
   var evt=Add("Block.TriggerEvent",update);Setting(evt,"mode","OverTime");Parameter("TrailRate",30f,Slot(evt,"Rate"));var gpu=contexts.Single(x=>x.GetType().Name=="VFXBasicGPUEvent");GraphReflect.Call(Inputs(gpu)[0],"UnlinkAll");GraphReflect.Call(Inputs(gpu)[0],"Link",GraphReflect.List(GraphReflect.Get(evt,"outputSlots"))[0]);output=MeshOutput(update,output,AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/VFX/Combat/VioletMicroCrystal.asset"));Curve(output,"color",Fade(new Color(.75f,.35f,1,1),true),"Overwrite");
   init=inits[1];update=updates[1];output=outputs[1];Clear(init);Bounds(init,4096);Attribute(init,"lifetime",.3f,.65f);var inherit=Add("Block.SetAttribute",init);Setting(inherit,"attribute","position");Setting(inherit,"Source","Source");Attribute(init,"size",.16f,.38f);Attribute(init,"velocity",new Vector3(-.2f,.15f,-.2f),new Vector3(.2f,.55f,.2f));Clear(update);var up=Add("Block.Gravity",update);Value(up,"Force",Vector3.up*.3f);Clear(output);var orient=Add("Block.Orient",output);Setting(orient,"mode","FaceCameraPosition");Flipbook("Assets/Game/VFX/Combat/Textures/Smoke_01_loop_8x8.tga",new Color(.36f,.12f,.65f,.55f),2);Save();
  }
  static void ShaderOutput(string name,string shaderName,bool mesh,Mesh geometry,float life,float size)
  {
   New(name);Basics(1,life,life,size,size,Vector3.zero,Vector3.zero,Vector3.zero,0);if(name=="VFX_HeatBurst"){Attribute(init,"position",Vector3.up*1.5f);Attribute(init,"velocity",Vector3.up*.6f);}if(mesh)output=MeshOutput(update,output,geometry);var asset=AssetDatabase.LoadAllAssetsAtPath(Root+"Graphs/"+shaderName+".shadergraph").FirstOrDefault(x=>x.GetType().Name=="ShaderGraphVfxAsset");if(!asset)throw new Exception("No VFX-compatible subasset "+shaderName);Setting(output,"shaderGraph",asset);Curve(output,"color",Fade(Color.white,true),"Overwrite");Curve(output,"size",AnimationCurve.Linear(0,.15f,1,1));
   foreach(var input in Inputs(output)){var n=GraphReflect.Get(input,"name").ToString();if(n=="_NoiseA")GraphReflect.Set(input,"value",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/VFX/Combat/Textures/Noise_001.png"));if(n=="_NoiseB")GraphReflect.Set(input,"value",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/VFX/Combat/Textures/Noise_002.png"));if(n.Contains("CenterSS"))Parameter("CenterSS",new Vector2(.5f,.5f),input);if(n.Contains("DistortionPixels"))Parameter("DistortionPixels",6f,input);if(n.Contains("ChromaticPixels"))Parameter("ChromaticPixels",2f,input);if(n.Contains("InsideSphere"))Parameter("InsideSphere",0f,input);}
   File.WriteAllText(Stage+name+"_shader_slots.json",JsonConvert.SerializeObject(Inputs(output).Select(s=>new{name=GraphReflect.Get(s,"name"),value=GraphReflect.Get(s,"value")?.ToString()}),Formatting.Indented));Save();
  }
  static void Build()
  {
   SimpleSystems();Track();Crystal();var sphere=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");ShaderOutput("VFX_ChromaticSphere","SG_ChromaticDistortion",true,sphere,.65f,1);ShaderOutput("VFX_HeatBurst","SG_HeatHaze_URP",false,null,2,3);var wind=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"DashWind_UserMesh.asset");if(wind)ShaderOutput("VFX_DashWindCone","SG_DashWind_URP",true,wind,.35f,1);
  }
  static void AppendSparkFlash()
  {
   path=Root+"Graphs/VFX_SparksFine.vfx";resource=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResource"),"GetResourceAtPath",path);graph=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);
   bool liveWindow=false;foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().Name=="VFXViewWindow")){if(!Equals(GraphReflect.Get(window,"displayedResource"),resource))continue;var controller=GraphReflect.Get(GraphReflect.Get(window,"graphView"),"controller");if(controller!=null){graph=GraphReflect.Get(controller,"graph");liveWindow=true;}}
   if(Children(graph).Any(n=>n.GetType().Name=="VFXBasicInitialize"&&GraphReflect.Get(n,"label")?.ToString()=="Contact core flash"))throw new Exception("Contact core flash already exists; edit its exposed parameters instead of appending twice.");
   // Save the live editor model first so the backup includes the user's latest node edits.
   GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"WriteAssetWithSubAssets",resource);string backup=Stage+"SparksFine_UserBeforeCore_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".vfx";File.Copy(path,backup);
   var original=Children(graph);var before=original.ToDictionary(n=>(UnityEngine.Object)n,n=>EditorJsonUtility.ToJson((UnityEngine.Object)n));
   spawn=Add("VFXBasicSpawner",graph);init=Add("VFXBasicInitialize",graph);update=Add("VFXBasicUpdate",graph);output=Add("VFXPlanarPrimitiveOutput",graph);GraphReflect.Call(spawn,"LinkTo",init);GraphReflect.Call(init,"LinkTo",update);GraphReflect.Call(update,"LinkTo",output);
   GraphReflect.Set(spawn,"label","One contact flash");GraphReflect.Set(init,"label","Contact core flash");GraphReflect.Set(update,"label","Stationary, short lifetime");GraphReflect.Set(output,"label","Additive white-hot core");int index=0;foreach(var context in new[]{spawn,init,update,output})GraphReflect.Set(context,"position",new Vector2(1150,350+index++*390));
   Bounds(init,4);Burst(1);var life=Attribute(init,"lifetime",.1f);Parameter("CoreFlashDuration",.1f,Inputs(life)[0]);Attribute(init,"position",Vector3.zero);Attribute(init,"velocity",Vector3.zero);var size=Attribute(init,"size",.24f);Parameter("CoreFlashSize",.24f,Inputs(size)[0]);var color=Attribute(init,"color",new Vector3(30,10,1.5f));Parameter("CoreFlashHDR",new Vector3(30,10,1.5f),Inputs(color)[0]);Attribute(init,"alpha",1f);
   Setting(output,"blendMode","Additive");Value(output,"mainTexture",AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.visualeffectgraph/Textures/DefaultDot.tga"));var orient=Add("Block.Orient",output);Setting(orient,"mode","FaceCameraPosition");Curve(output,"size",new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.1f,1),new Keyframe(1,0)));Curve(output,"alpha",new AnimationCurve(new Keyframe(0,1),new Keyframe(.22f,1),new Keyframe(1,0)),"Multiply");
   bool preserved=before.All(pair=>EditorJsonUtility.ToJson(pair.Key)==pair.Value);if(!preserved)throw new Exception("Existing spark nodes changed unexpectedly; inspect backup before saving.");Save();File.WriteAllText(Stage+"spark_flash.json",JsonConvert.SerializeObject(new{passed=true,backup,liveWindow,originalNodes=original.Length,existingNodesPreserved=preserved,duration=.1f,size=.24f},Formatting.Indented));
  }
 }
}



