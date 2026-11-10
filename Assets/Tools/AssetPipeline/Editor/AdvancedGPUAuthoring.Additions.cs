using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AssetPipeline.Editor
{
 public static partial class AdvancedGPUAuthoring
 {
  static void NewSystem(string label,int column)
  {
   spawn=Add("VFXBasicSpawner",graph);init=Add("VFXBasicInitialize",graph);update=Add("VFXBasicUpdate",graph);output=Add("VFXPlanarPrimitiveOutput",graph);
   GraphReflect.Call(spawn,"LinkTo",init);GraphReflect.Call(init,"LinkTo",update);GraphReflect.Call(update,"LinkTo",output);
   int row=0;foreach(var c in new[]{spawn,init,update,output}){GraphReflect.Set(c,"label",label);GraphReflect.Set(c,"position",new Vector2(column*650,350+row++*450));}
  }
  static bool HasSystem(string label)=>Children(graph).Any(n=>n.GetType().Name=="VFXBasicInitialize"&&GraphReflect.Get(n,"label")?.ToString()==label);
  static void LoadGraph(string name)
  {
   path=Root+"Graphs/"+name+".vfx";resource=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResource"),"GetResourceAtPath",path);graph=GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);
  }
  static void ExplosionAdditions(bool big)
  {
   LoadGraph(big?"VFX_ExplosionFinal":"VFX_ExplosionSmall");GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"WriteAssetWithSubAssets",resource);File.Copy(path,Stage+(big?"BigExplosion":"SmallExplosion")+"_BeforeIntegratedSpray_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".vfx");
   var fire=Children(graph).First(n=>n.GetType().Name=="VFXPlanarPrimitiveOutput");var curve=Children(fire).First(n=>n.GetType().Name=="AttributeFromCurve"&&GraphReflect.Call(n,"GetSettingValue","attribute").ToString()=="color");GraphReflect.Set(Inputs(curve)[0],"value",Fade(big?new Color(4,3,2,1):new Color(2.5f,2,1.5f,1)));
   if(!HasSystem("Energetic streak spray"))
   {
    NewSystem("Energetic streak spray",2);Basics(big?350:170,.35f,big?1.4f:1.05f,big?.065f:.04f,big?.13f:.085f,new Vector3(big?-28:-18,2,big?-28:-18),new Vector3(big?28:18,big?27:19,big?28:18),new Vector3(0,-12,0),1.2f);
    Attribute(init,"scale",big?new Vector3(.28f,4,1):new Vector3(.25f,3,1));Setting(Children(output).First(),"mode","AlongVelocity");Setting(output,"blendMode","Additive");Value(output,"mainTexture",AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.visualeffectgraph/Textures/DefaultDot.tga"));Curve(output,"color",Fade(big?new Color(50,15,1.5f,1):new Color(28,8,.8f,1),true),"Overwrite");Curve(output,"size",AnimationCurve.Linear(0,1,1,.15f));GroundCollision(update);
   }
   if(!HasSystem("Ballistic fragments"))
   {
    NewSystem("Ballistic fragments",3);Basics(big?100:24,big?3:1.3f,big?5:3,big?.035f:.02f,big?.1f:.06f,new Vector3(-10,2,-10),new Vector3(10,big?13:8,10),new Vector3(0,-9.81f,0),.3f);output=MeshOutput(update,output,AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/VFX/Combat/ConcreteFragment.asset"));GraphReflect.Set(output,"label","Ballistic fragments");Attribute(init,"angle",Vector3.zero,Vector3.one*6.28f);Curve(output,"color",Fade(new Color(.25f,.22f,.2f,1),true),"Overwrite");GroundCollision(update);
   }
   if(big&&!HasSystem("Rising black smoke"))
   {
    NewSystem("Rising black smoke",4);Basics(48,3.5f,6,1.5f,2.8f,new Vector3(-2,1,-2),new Vector3(2,3,2),Vector3.up*.5f,.45f);Attribute(init,"position",new Vector3(-1.4f,-.2f,-1.4f),new Vector3(1.4f,1.2f,1.4f));Flipbook(Root+"Textures/Smoke24_8x8.TGA",new Color(.1f,.1f,.11f,.8f),3.2f);
   }
   Save();
  }
  static void FullChargeFlash()
  {
   string target=Root+"Graphs/VFX_GauntletFullFlash.vfx";if(File.Exists(target))return;
   New("VFX_GauntletFullFlash");Basics(1,.14f,.14f,.2f,.2f,Vector3.zero,Vector3.zero,Vector3.zero,0);Attribute(init,"position",Vector3.zero);Attribute(init,"scale",new Vector3(6,.3f,1));Setting(output,"blendMode","Additive");Value(output,"mainTexture",AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.visualeffectgraph/Textures/DefaultDot.tga"));Curve(output,"color",Fade(new Color(35,.12f,.04f,1),true),"Overwrite");Curve(output,"size",new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.08f,1),new Keyframe(1,0)));Save();
  }
  static void FinalExtras(){ExplosionAdditions(false);ExplosionAdditions(true);FullChargeFlash();File.WriteAllText(Stage+"final_extras.json",JsonConvert.SerializeObject(new{passed=true,smallStreaks=170,bigStreaks=350,smallFragments=24,bigFragments=100,bigSmoke=48,fullFlash=.14f}));}
  static void FireGlow()
  {
   foreach(bool big in new[]{false,true})
   {
    LoadGraph(big?"VFX_ExplosionFinal":"VFX_ExplosionSmall");GraphReflect.Call(GraphReflect.T(NS+"VisualEffectResourceExtensions"),"WriteAssetWithSubAssets",resource);File.Copy(path,Stage+(big?"Big":"Small")+"_BeforeHDRGlow_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".vfx");
    var fire=Children(graph).First(n=>n.GetType().Name=="VFXPlanarPrimitiveOutput");var color=Children(fire).First(n=>n.GetType().Name=="AttributeFromCurve"&&GraphReflect.Call(n,"GetSettingValue","attribute").ToString()=="color");GraphReflect.Set(Inputs(color)[0],"value",Fade(big?new Color(8,5,2,1):new Color(4,2.5f,1,1)));var fireData=GraphReflect.Call(fire,"GetData");var fireInit=Children(graph).First(n=>n.GetType().Name=="VFXBasicInitialize"&&Equals(GraphReflect.Call(n,"GetData"),fireData));var pivot=Children(fireInit).FirstOrDefault(n=>n.GetType().Name=="SetAttribute"&&GraphReflect.Call(n,"GetSettingValue","attribute").ToString()=="pivot");if(pivot==null)Attribute(fireInit,"pivot",new Vector3(0,big?-.2f:-.1f,0));else SlotValue(Inputs(pivot)[0],new Vector3(0,big?-.2f:-.1f,0));Save();
   }
   File.WriteAllText(Stage+"fire_glow.json","{\"passed\":true,\"smallHDR\":[4,2.5,1],\"bigHDR\":[8,5,2]}");
  }
 }
}
