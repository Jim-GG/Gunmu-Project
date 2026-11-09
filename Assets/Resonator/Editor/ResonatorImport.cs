using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace Resonator.Editor {
 public static class ResonatorImport {
  const string R="Assets/Game/Enemies/Resonator/";
  static Material Material(string label,bool crystal){
   string name=(crystal?"Crystal_":"")+label+"_Unity";string path=R+"Materials/"+name+".mat";
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
   m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(R+"Textures/"+label+"_BaseColor.png"));
   m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(R+"Textures/"+label+"_Normal.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",1);
   m.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(R+"Textures/"+label+"_Surface.png"));m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",crystal?1:.95f);m.SetFloat("_Metallic",crystal?.2f:0);m.SetFloat("_SpecularHighlights",1);m.SetFloat("_EnvironmentReflections",1);
   EditorUtility.SetDirty(m);return m;
  }
  [MenuItem("Tools/Resonator/Import and Build Preview")]
  public static void Build(){
   if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first");
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   foreach(var path in Directory.GetFiles(R+"Textures","*.png")){
    var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;t.sRGBTexture=path.Contains("_BaseColor");t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.Uncompressed;t.mipmapEnabled=true;t.SaveAndReimport();
   }
   var model=(ModelImporter)AssetImporter.GetAtPath(R+"Models/Resonator.fbx");model.animationType=ModelImporterAnimationType.Generic;model.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;model.importAnimation=false;model.materialImportMode=ModelImporterMaterialImportMode.None;model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.CalculateMikk;model.preserveHierarchy=true;model.optimizeGameObjects=false;model.SaveAndReimport();
   var avatar=AssetDatabase.LoadAllAssetsAtPath(R+"Models/Resonator.fbx").OfType<Avatar>().First();
   var paths=Directory.GetFiles(R+"Animations","*.fbx").OrderBy(x=>x).ToArray();
   var animations=paths.Select(path=>{
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=avatar;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.preserveHierarchy=true;
    var clips=importer.defaultClipAnimations;string name=Path.GetFileNameWithoutExtension(path);foreach(var clip in clips){clip.name=name;if(name.StartsWith("Zombie_"))clip.firstFrame=Mathf.Max(2,clip.firstFrame);clip.loopTime=(name.Contains("Idle")||name.Contains("Walk")||name.Contains("Running"))&&!name.Contains("Attack");clip.loopPose=false;}importer.clipAnimations=clips;importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
   }).ToArray();
   string cp=R+"Resonator.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(cp);
   var machine=controller.layers[0].stateMachine;foreach(var clip in animations){var state=machine.states.FirstOrDefault(s=>s.state.name==clip.name).state;if(!state)state=machine.AddState(clip.name);state.motion=clip;state.writeDefaultValues=false;}
   machine.defaultState=machine.states.First(s=>s.state.name=="Resonator_Walking").state;
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
   UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(R+"Models/Resonator.fbx");var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);go.name="Resonator Scientist";
   foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()){
    string label=renderer.name.Contains("Clothing")?"Clothing":"SculptedHeadSkin";renderer.sharedMaterials=new[]{Material(label,false),Material(label,true)};renderer.updateWhenOffscreen=true;renderer.quality=SkinQuality.Bone4;
   }
   var animator=go.GetComponent<Animator>();if(!animator)animator=go.AddComponent<Animator>();animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   var preview=go.AddComponent<ResonatorAnimationPreview>();preview.animator=animator;preview.clips=animations.Select(a=>a.name).ToArray();
   PrefabUtility.SaveAsPrefabAsset(go,R+"Prefabs/ResonatorScientist.prefab");
   Bounds bounds=new Bounds();bool first=true;foreach(var r in go.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
   var cameraGo=new GameObject("Resonator Preview Camera",typeof(Camera),typeof(AudioListener));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);var cam=cameraGo.GetComponent<Camera>();cameraGo.transform.position=bounds.center+new Vector3(2.8f,1.2f,-4);cameraGo.transform.LookAt(bounds.center);cam.nearClipPlane=.02f;cam.farClipPlane=100;cam.backgroundColor=new Color(.10f,.11f,.14f);cam.clearFlags=CameraClearFlags.SolidColor;
   foreach(var data in new[]{new Vector3(-3,4,-4),new Vector3(3,2,-1),new Vector3(0,3,3)}){var lightGo=new GameObject("Studio Light",typeof(Light));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo,scene);lightGo.transform.position=data;lightGo.transform.LookAt(bounds.center);var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=data.z>0?1.8f:1.2f;light.color=data.x>0?new Color(.72f,.8f,1):new Color(1,.88f,.76f);}
   var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Studio Ground";UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new Vector3(0,bounds.min.y-.015f,0);floor.transform.localScale=Vector3.one*3;
   var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));ground.SetColor("_BaseColor",new Color(.17f,.16f,.15f));string gp=R+"Materials/Ground.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(gp);if(existing)UnityEngine.Object.DestroyImmediate(ground);else {AssetDatabase.CreateAsset(ground,gp);existing=ground;}floor.GetComponent<Renderer>().sharedMaterial=existing;
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.45f,.48f,.58f);RenderSettings.ambientEquatorColor=new Color(.25f,.24f,.26f);RenderSettings.ambientGroundColor=new Color(.12f,.11f,.10f);
   var probeGo=new GameObject("Crystal Reflection Probe",typeof(ReflectionProbe));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probeGo,scene);probeGo.transform.position=bounds.center;var probe=probeGo.GetComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.size=Vector3.one*15;probe.resolution=128;probe.RenderProbe();
   EditorSceneManager.SaveScene(scene,R+"Scenes/ResonatorPreview.unity");EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();Selection.activeGameObject=go;
  }
 }
}
