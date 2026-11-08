using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using CrystalParkour;

public static class ImportDrone
{
 const string Root="Assets/Game/Environment/Drone/";
 const string PrefabPath=Root+"Prefabs/Drone_MCU7A.prefab";
 const string ReviewScene="Assets/Game/Scene/MCU7A_Drone_Review.unity";
 const string Backup="ArtSource/Drone/UserModel/Baked_v12/UnityBackups/";
 static T Load<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
 static Material SaveMaterial(string name,Shader shader)
 {
  string p=Root+"Materials/"+name+".mat";var m=Load<Material>(p);
  if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,p);}else m.shader=shader;
  return m;
 }
 static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
 static float DeckTop(TraversalDrone d)
 {
  if(!d.deck)return .31f;
  return Enumerable.Range(0,8).Select(i=>d.transform.InverseTransformPoint(d.deck.transform.TransformPoint(d.deck.center+Vector3.Scale(d.deck.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))).y).Max();
 }
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");
  AssetDatabase.Refresh();
  foreach(var channel in new[]{"BaseColor","Normal","Mask","Emission"})
  {
   string path=Root+"Textures/Drone_"+channel+".png";var ti=(TextureImporter)AssetImporter.GetAtPath(path);
   if(!ti)throw new Exception("Missing texture "+path);
   ti.textureType=channel=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
   ti.sRGBTexture=channel=="BaseColor"||channel=="Emission";ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;
   ti.flipGreenChannel=false;ti.mipmapEnabled=true;ti.streamingMipmaps=true;ti.maxTextureSize=4096;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=4;
   ti.textureCompression=TextureImporterCompression.CompressedHQ;
   var platform=ti.GetPlatformTextureSettings("Standalone");platform.overridden=true;platform.maxTextureSize=4096;platform.format=channel=="Normal"?TextureImporterFormat.BC5:TextureImporterFormat.BC7;ti.SetPlatformTextureSettings(platform);ti.SaveAndReimport();
  }
  var shader=Shader.Find("Null Boundary/Drone/Baked Enamel");if(!shader)throw new Exception("Drone shader missing");
  var atlas=SaveMaterial("Drone_Atlas_Enamel",shader);
  atlas.SetTexture("_BaseMap",Load<Texture2D>(Root+"Textures/Drone_BaseColor.png"));atlas.SetTexture("_BumpMap",Load<Texture2D>(Root+"Textures/Drone_Normal.png"));atlas.SetTexture("_MetallicGlossMap",Load<Texture2D>(Root+"Textures/Drone_Mask.png"));atlas.SetTexture("_EmissionMap",Load<Texture2D>(Root+"Textures/Drone_Emission.png"));
  atlas.SetColor("_BaseColor",Color.white);atlas.SetColor("_EmissionColor",new Color(8,8,8,1));atlas.SetFloat("_BumpScale",1);atlas.SetFloat("_Metallic",1);atlas.SetFloat("_Smoothness",1);atlas.SetFloat("_OcclusionStrength",1);atlas.SetFloat("_ClearCoatMask",1);atlas.SetFloat("_ClearCoatSmoothness",.9f);atlas.SetFloat("_DetailNormalMapScale",.045f);atlas.SetFloat("_DetailAlbedoMapScale",28);
  atlas.SetTexture("_DetailNormalMap",Load<Texture2D>("Assets/Game/Environment/ModernLab/Textures/Lab_Detail_Coating_Normal.png"));atlas.EnableKeyword("_NORMALMAP");atlas.EnableKeyword("_EMISSION");atlas.enableInstancing=true;EditorUtility.SetDirty(atlas);
  var glass=SaveMaterial("Drone_Optical_Glass",Shader.Find("Universal Render Pipeline/Complex Lit"));
  glass.SetColor("_BaseColor",new Color(.025f,.13f,.16f,1));glass.SetFloat("_Metallic",.35f);glass.SetFloat("_Smoothness",.92f);glass.SetFloat("_ClearCoatMask",1);glass.SetFloat("_ClearCoatSmoothness",.97f);glass.EnableKeyword("_CLEARCOAT");glass.SetColor("_EmissionColor",new Color(.003f,.025f,.032f,1));glass.EnableKeyword("_EMISSION");glass.SetTexture("_EmissionMap",Texture2D.whiteTexture);glass.enableInstancing=true;EditorUtility.SetDirty(glass);
  string modelPath=Root+"Models/MCU7A_Drone.fbx";var mi=(ModelImporter)AssetImporter.GetAtPath(modelPath);
  mi.importCameras=false;mi.importLights=false;mi.importAnimation=false;mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;mi.meshCompression=ModelImporterMeshCompression.Off;mi.globalScale=1;mi.bakeAxisConversion=true;mi.addCollider=false;mi.isReadable=false;mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;mi.SaveAndReimport();
  var model=Load<GameObject>(modelPath);if(!model)throw new Exception("Model import failed");
  foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))foreach(var m in r.sharedMaterials.Where(m=>m).Distinct())mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),m.name),m.name.StartsWith("08")?glass:atlas);
  mi.SaveAndReimport();model=Load<GameObject>(modelPath);
  var root=new GameObject("MCU7A · maintenance / suppression drone");var d=root.AddComponent<TraversalDrone>();var deckGo=new GameObject("Load-bearing collision deck",typeof(BoxCollider));deckGo.transform.SetParent(root.transform,false);d.deck=deckGo.GetComponent<BoxCollider>();d.deck.size=new Vector3(.82f,.10f,1.12f);d.deck.center=new Vector3(0,.26f,0);
  AttachVisual(d,.31f);
  d.startPoint=Vector3.zero;d.endPoint=new Vector3(0,-6,0);d.speed=1.4f;d.route=DroneRoute.DescendingStream;d.respawnSeconds=2;
  PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
  var stats=model.GetComponentsInChildren<MeshFilter>(true).Select(m=>new{name=m.name,vertices=m.sharedMesh.vertexCount,triangles=(int)(Enumerable.Range(0,m.sharedMesh.subMeshCount).Sum(i=>(long)m.sharedMesh.GetIndexCount(i))/3),submeshes=m.sharedMesh.subMeshCount,centre=new[]{m.sharedMesh.bounds.center.x,m.sharedMesh.bounds.center.y,m.sharedMesh.bounds.center.z},size=new[]{m.sharedMesh.bounds.size.x,m.sharedMesh.bounds.size.y,m.sharedMesh.bounds.size.z}}).ToArray();
  var result=new {prefab=PrefabPath,meshParts=stats,shaderErrors=ShaderUtil.GetShaderMessages(shader).Select(m=>m.message).ToArray()};Directory.CreateDirectory("Docs/Drone");File.WriteAllText("Docs/Drone/import_report.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
 static void AttachVisual(TraversalDrone d,float deckTop)
 {
  var visual=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root+"Models/MCU7A_Drone.fbx"),d.gameObject.scene);visual.name="MCU7A game visual";visual.transform.SetParent(d.transform,false);
  // Axis conversion is baked by the importer. Keep the model's authored transform intact.
  visual.transform.localPosition+=Vector3.up*(deckTop-(.4625182f+.75f)/3);
  visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
  var atlas=Load<Material>(Root+"Materials/Drone_Atlas_Enamel.mat");var glass=Load<Material>(Root+"Materials/Drone_Optical_Glass.mat");
  foreach(var r in visual.GetComponentsInChildren<MeshRenderer>(true))
  {var mesh=r.GetComponent<MeshFilter>().sharedMesh;r.sharedMaterials=Enumerable.Range(0,mesh.subMeshCount).Select(i=>i==1?glass:atlas).ToArray();r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;}
  Transform Rotor(string name)
  {
   var blade=Find(visual.transform,name);if(!blade)throw new Exception("Missing rotor "+name);
   // The upright pivot is the runtime rotation target; the imported blade retains its axis conversion.
   var pivot=new GameObject(name+" · upright pivot").transform;pivot.SetParent(d.transform,false);pivot.position=blade.position;pivot.rotation=d.transform.rotation;blade.SetParent(pivot,true);return pivot;
  }
  d.rotorLeft=Rotor("Rotor_Left");d.rotorRight=Rotor("Rotor_Right");
  var lamp=new GameObject("Warm yellow fill lamp",typeof(Light));lamp.transform.SetParent(visual.transform,false);lamp.transform.localPosition=new Vector3(-.47372f/3,(-.92802f+.75f)/3,-2.48f/3);lamp.transform.localRotation=Quaternion.Euler(0,180,0);var l=lamp.GetComponent<Light>();l.type=LightType.Spot;l.color=new Color(1,.67f,.27f);l.intensity=1.8f;l.range=7;l.spotAngle=55;l.innerSpotAngle=30;l.shadows=LightShadows.None;
 }
 public static object Integrate()
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");Directory.CreateDirectory(Backup);
  int changed=0;string path="Assets/Game/CrystalParkour/Prefabs/TraversalDronePlaceholder.prefab";string saved=Backup+"TraversalDronePlaceholder_before.prefab";if(!File.Exists(saved))File.Copy(path,saved);
  var contents=PrefabUtility.LoadPrefabContents(path);
  try {var d=contents.GetComponent<TraversalDrone>();Upgrade(d);PrefabUtility.SaveAsPrefabAsset(contents,path);changed++;}finally{PrefabUtility.UnloadPrefabContents(contents);}
  var oldActive=SceneManager.GetActiveScene();
  foreach(string scenePath in new[]{"Assets/Game/Scene/CrystalImpactParkour.unity","Assets/Game/Scene/CrystalTraversalLab_V2.unity"})
  {
   var scene=SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
   try
   {
    string backup=Backup+Path.GetFileName(scenePath);if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
    foreach(var d in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TraversalDrone>(true)))
    {
     if(Find(d.transform,"MCU7A game visual"))continue;
     if(PrefabUtility.IsPartOfPrefabInstance(d.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(d.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
     Upgrade(d);changed++;
    }
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
  if(oldActive.IsValid()&&oldActive.isLoaded)SceneManager.SetActiveScene(oldActive);AssetDatabase.SaveAssets();return new {updated=changed,backup=Backup};
 }
 static void Upgrade(TraversalDrone d)
 {
  if(Find(d.transform,"MCU7A game visual"))return;
  float top=DeckTop(d);var deck=d.deck;var deckRoot=deck?deck.transform:null;
  if(deckRoot&&deckRoot!=d.transform){deckRoot.SetParent(d.transform,true);deckRoot.localPosition=Vector3.zero;deckRoot.localRotation=Quaternion.identity;deckRoot.localScale=Vector3.one;}
  foreach(var child in d.transform.Cast<Transform>().ToArray())if(child!=deckRoot&&child!=d.startAnchor&&child!=d.endAnchor)UnityEngine.Object.DestroyImmediate(child.gameObject);
  if(!deck){var go=new GameObject("Load-bearing collision deck",typeof(BoxCollider));go.transform.SetParent(d.transform,false);deck=go.GetComponent<BoxCollider>();}
  var oldRenderer=deck.GetComponent<MeshRenderer>();if(oldRenderer)UnityEngine.Object.DestroyImmediate(oldRenderer);var oldFilter=deck.GetComponent<MeshFilter>();if(oldFilter)UnityEngine.Object.DestroyImmediate(oldFilter);
  deck.name="Load-bearing collision deck";deck.size=new Vector3(.82f,.10f,1.12f);deck.center=new Vector3(0,top-.05f,0);d.deck=deck;AttachVisual(d,top);
 }
}
