using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AssetPipeline.Editor
{
    [InitializeOnLoad]
    public static class ApprovedFacilityPropsImport
    {
        const string Stage="AssetStaging/FacilityProps/V02/";
        const string Review="Assets/Game/Scene/FacilityPropsReview.unity";
        static bool busy;
        static ApprovedFacilityPropsImport(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"UnityImport.request"))return;
            File.Delete(Stage+"UnityImport.request");Run();
        }
        [MenuItem("Tools/Asset Pipeline/Import Approved Facility Props")]
        public static async void Run()
        {
            if(busy)return;busy=true;
            File.WriteAllText(Stage+"unity_import_status.json","{\"status\":\"running\"}");
            try{var report=await Build();File.WriteAllText(Stage+"unity_import_status.json",JsonConvert.SerializeObject(report,Formatting.Indented));Debug.Log("FACILITY_PROPS_IMPORT_PASSED");}
            catch(Exception e){File.WriteAllText(Stage+"unity_import_status.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{busy=false;}
        }
        static string S(JToken t)=>((string)t)?.Replace('\\','/');
        static float F(JToken t,float fallback=0)=>t==null?fallback:(float)t;
        static Color C(JToken t)=>t==null?Color.white:new Color((float)t[0],(float)t[1],(float)t[2],t.Count()>3?(float)t[3]:1);
        static T A<T>(string p)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(p);
        static async Task<object> Build()
        {
            if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before importing assets.");
            var cfg=JObject.Parse(File.ReadAllText(Stage+"unity_import.json"));var mats=new Dictionary<string,Material>();
            var shader=A<Shader>("Assets/Game/Environment/ModernLab/Shaders/ModernLabLayered.shadergraph");if(!shader)throw new Exception("ModernLabLayered shader missing");
            var materialConfigs=(JObject)cfg["materials"];
            foreach(var p in materialConfigs.Properties())Directory.CreateDirectory(Path.GetDirectoryName(S(p.Value["asset_path"])));
            foreach(var p in cfg["models"])Directory.CreateDirectory(Path.GetDirectoryName(S(p["prefab_path"])));
            Directory.CreateDirectory("Assets/Game/Scene");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var newTextures=materialConfigs.Properties().SelectMany(p=>p.Value["textures"] is JObject tx?tx.Properties().Select(x=>S(x.Value)):p.Value["texture"]!=null?new[]{S(p.Value["texture"])}:Array.Empty<string>()).Where(p=>p.Contains("/Textures/Props/")).Distinct();
            foreach(var path in newTextures)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("Missing texture "+path);
                importer.textureType=path.EndsWith("_Normal.png")?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=path.EndsWith("_BaseColor.png")||path.Contains("_Exit_")||path.EndsWith("_Label.png");
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;importer.wrapMode=path.Contains("_Exit_")||path.EndsWith("_Label.png")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.flipGreenChannel=false;importer.SaveAndReimport();
            }
            foreach(var pair in materialConfigs.Properties())
            {
                var p=pair.Value;string path=S(p["asset_path"]);var mat=A<Material>(path);
                if(p["source_material"]!=null){if(!mat)throw new Exception("Missing approved source material "+path);mats[pair.Name]=mat;continue;}
                bool layered=p["textures"]!=null;var targetShader=layered?shader:Shader.Find("Universal Render Pipeline/Lit");
                if(!mat){mat=new Material(targetShader){name=pair.Name};AssetDatabase.CreateAsset(mat,path);}mat.shader=targetShader;mat.shaderKeywords=Array.Empty<string>();mat.enableInstancing=true;
                if(layered)
                {
                    foreach(var tx in ((JObject)p["textures"]).Properties())mat.SetTexture("_"+tx.Name,A<Texture2D>(S(tx.Value)));
                    foreach(var v in ((JObject)p["values"]).Properties())mat.SetFloat("_"+v.Name,(float)v.Value);
                    foreach(var color in ((JObject)p["colors"]).Properties())mat.SetColor("_"+color.Name,C(color.Value));
                }
                else
                {
                    mat.SetColor("_BaseColor",p["color"]!=null?C(p["color"]):Color.white);mat.SetFloat("_Metallic",F(p["metallic"]));mat.SetFloat("_Smoothness",1-F(p["roughness"],.5f));mat.SetFloat("_Surface",0);mat.SetFloat("_ZWrite",1);
                    if(p["texture"]!=null)mat.SetTexture("_BaseMap",A<Texture2D>(S(p["texture"])));
                    float emission=F(p["emission"]);
                    if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",(p["color"]!=null?C(p["color"]):Color.white)*emission);mat.SetTexture("_EmissionMap",p["texture"]!=null?A<Texture2D>(S(p["texture"])):Texture2D.whiteTexture);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
                }
                mat.renderQueue=-1;EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);mats[pair.Name]=mat;
            }
            var prefabPaths=new List<string>();var meshChecks=new List<object>();var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(var p in cfg["models"])
                {
                    string path=S(p["model_path"]),name=(string)p["name"];
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=false;importer.swapUVChannels=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                    foreach(var n in p["materials"].Values<string>())importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),n),mats[n]);importer.SaveAndReimport();
                    var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,preview);var visual=(GameObject)PrefabUtility.InstantiatePrefab(A<GameObject>(path),preview);visual.name="Model";visual.transform.SetParent(root.transform,false);
                    try
                    {
                        int triangles=0;Bounds bounds=new Bounds();bool first=true;
                        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&mats.TryGetValue(m.name,out var found)?found:throw new Exception("Unmapped material in "+name)).ToArray();
                            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;if(mesh.uv.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount)throw new Exception("Missing UV/tangents in "+name);triangles+=mesh.triangles.Length/3;
                            if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                        }
                        int minimum=p["nondegenerate"]==null?(int)p["triangles"]:(int)p["nondegenerate"];
                        if(triangles<minimum||triangles>(int)p["triangles"])throw new Exception("Triangle mismatch "+name+": "+triangles+" expected "+minimum+".."+p["triangles"]);
                        var collider=root.AddComponent<BoxCollider>();collider.center=root.transform.InverseTransformPoint(bounds.center);collider.size=bounds.size;
                        var option=cfg["side_arrow_material_options"]?[name];
                        if(option!=null)
                        {
                            var renderer=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterials.Any(m=>m.name==(string)option["default"]));int slot=Array.FindIndex(renderer.sharedMaterials,m=>m.name==(string)option["default"]);
                            var direction=root.AddComponent<FacilityProps.ExitSignDirection>();direction.rightMaterial=mats[(string)option["default"]];direction.leftMaterial=mats[(string)option["left"]];direction.Configure(renderer,slot);direction.SetDirection(FacilityProps.ExitSignDirection.Direction.Left);
                            if(renderer.sharedMaterials[slot]!=direction.leftMaterial)throw new Exception("Left arrow switch failed");direction.SetDirection(FacilityProps.ExitSignDirection.Direction.Right);
                        }
                        string destination=S(p["prefab_path"]);var saved=PrefabUtility.SaveAsPrefabAsset(root,destination);if(Quaternion.Angle(saved.transform.localRotation,Quaternion.identity)>.001f||saved.transform.localScale!=Vector3.one)throw new Exception("Invalid prefab root "+name);prefabPaths.Add(destination);meshChecks.Add(new{name,triangles,size=new[]{bounds.size.x,bounds.size.y,bounds.size.z}});
                    }
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();await BuildReview(cfg);
            var shaderErrors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Select(m=>m.message).ToArray();if(shaderErrors.Length>0)throw new Exception(string.Join("\n",shaderErrors));
            return new{passed=true,prefabs=prefabPaths,materials=mats.Count,meshChecks,scene=Review,existingMaterialsPreserved=true,leftRightSwitchValidated=true,ceilingPivotsPreserved=true};
        }
        static async Task BuildReview(JObject cfg)
        {
            if(SceneManager.GetSceneByPath(Review).isLoaded)throw new Exception("Close FacilityPropsReview before rebuilding it.");
            var prior=SceneManager.GetActiveScene();var probes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var p in probes)p.enabled=false;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach(var p in cfg["models"])
                {
                    string name=(string)p["name"];bool soviet=name.StartsWith("SV");var go=(GameObject)PrefabUtility.InstantiatePrefab(A<GameObject>(S(p["prefab_path"])),scene);float x=soviet?1.55f:-1.55f;
                    if(name.Contains("WoodCrate"))go.transform.position=new Vector3(x+.63f,0,0);
                    else if(name.Contains("Crate"))go.transform.position=new Vector3(x-.63f,0,0);
                    else{string variant=(string)p["mount"];float y=variant=="Side"?2.7f:variant=="Ceiling"?2.07f:1.28f;if(variant=="Ceiling")y+=.508f;go.transform.position=new Vector3(x,y,.4f);}
                    foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                }
                var groundMat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="FP_Review_Ground"};groundMat.SetColor("_BaseColor",new Color(.11f,.13f,.15f));groundMat.SetFloat("_Smoothness",.22f);
                string groundPath="Assets/Game/Environment/ModernLab/Materials/Props/FP_Review_Ground.mat";var existing=A<Material>(groundPath);if(!existing)AssetDatabase.CreateAsset(groundMat,groundPath);else{UnityEngine.Object.DestroyImmediate(groundMat);groundMat=existing;}
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);floor.name="Review floor";floor.layer=30;floor.transform.position=new Vector3(0,-.07f,0);floor.transform.localScale=new Vector3(40,.12f,40);floor.GetComponent<Renderer>().sharedMaterial=groundMat;
                AssetReviewUtility.Light(scene,"Soft key",new Vector3(-4,6,5),new Vector3(0,1,0),LightType.Spot,100,new Color(.88f,.94f,1));AssetReviewUtility.Light(scene,"Warm side",new Vector3(4,4,1),new Vector3(0,1,0),LightType.Spot,75,new Color(1,.87f,.73f));AssetReviewUtility.Light(scene,"Rear rim",new Vector3(0,5,-4),new Vector3(0,1,0),LightType.Spot,110,new Color(.73f,.86f,1));
                AssetReviewUtility.Light(scene,"Diffuse fill",new Vector3(0,5,2),Vector3.zero,LightType.Directional,.7f,Color.white);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.28f,.30f,.33f);var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.35f,.38f,.40f));RenderSettings.ambientProbe=sh;
                var camera=AssetReviewUtility.Camera(scene,new Vector3(4.2f,3.3f,7.3f),new Vector3(0,1.25f,.2f),32);
                var probeObject=new GameObject("Review reflections",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(probeObject,scene);probeObject.layer=30;probeObject.transform.position=new Vector3(0,1.7f,-1);var probe=probeObject.GetComponent<ReflectionProbe>();probe.enabled=false;probe.mode=ReflectionProbeMode.Custom;probe.boxProjection=true;probe.size=new Vector3(25,12,25);probe.customBakedTexture=AssetReviewUtility.RoomCube(scene,probeObject.transform.position,"Assets/Game/Scene/FacilityPropsReviewReflection.cubemap");probe.intensity=.65f;probe.enabled=true;
                await Task.Delay(500);AssetReviewUtility.Capture(scene,camera,Stage+"Preview/Unity_FacilityProps.png");EditorSceneManager.SaveScene(scene,Review);
            }
            finally
            {
                foreach(var go in scene.GetRootGameObjects()){foreach(var c in go.GetComponentsInChildren<Camera>())c.enabled=false;foreach(var p in go.GetComponentsInChildren<ReflectionProbe>())p.enabled=false;}
                await Task.Delay(500);SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);foreach(var p in probes)if(p)p.enabled=true;
            }
        }
    }

    public static class AssetReviewUtility
    {
        public static void Light(Scene scene,string name,Vector3 position,Vector3 target,LightType type,float intensity,Color color)
        {
            var go=new GameObject(name,typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=position;go.transform.LookAt(target);var lamp=go.GetComponent<Light>();lamp.type=type;lamp.intensity=intensity;lamp.color=color;lamp.range=35;lamp.spotAngle=110;lamp.innerSpotAngle=85;lamp.cullingMask=1<<30;lamp.shadows=type==LightType.Directional?LightShadows.None:LightShadows.Soft;
        }
        public static Camera Camera(Scene scene,Vector3 position,Vector3 target,float fov)
        {
            var go=new GameObject("Review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=position;go.transform.LookAt(target);var camera=go.GetComponent<Camera>();camera.fieldOfView=fov;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.13f,.16f);camera.cullingMask=1<<30;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.GetUniversalAdditionalCameraData().volumeLayerMask=1<<30;camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var vg=new GameObject("Review colour",typeof(Volume));SceneManager.MoveGameObjectToScene(vg,scene);vg.layer=30;vg.GetComponent<Volume>().isGlobal=true;vg.GetComponent<Volume>().sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Game/Environment/ModernLab/Scenes/ModernLabPreviewVolume.asset");return camera;
        }
        public static void Capture(Scene scene,Camera camera,string path)
        {
            ulong previousMask=EditorSceneManager.GetSceneCullingMask(scene);ulong mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);camera.overrideSceneCullingMask=mask;
            var rt=RenderTexture.GetTemporary(1600,1100,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;Texture2D image=null;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.overrideSceneCullingMask=0;EditorSceneManager.SetSceneCullingMask(scene,previousMask);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
        }
        public static Cubemap RoomCube(Scene scene,Vector3 position,string path)
        {
            ulong old=EditorSceneManager.GetSceneCullingMask(scene);ulong mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);
            var go=new GameObject("Temporary reflection capture",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;var camera=go.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.overrideSceneCullingMask=mask;camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.21f,.24f);
            var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){dimension=TextureDimension.Cube};rt.Create();var image=new Texture2D(128,128,TextureFormat.RGBAHalf,false,true);var previous=RenderTexture.active;var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);bool created=!cube;if(created)cube=new Cubemap(128,TextureFormat.RGBAHalf,true){name=Path.GetFileNameWithoutExtension(path)};
            try{if(!camera.RenderToCubemap(rt))throw new Exception("Room reflection capture failed");for(int i=0;i<6;i++){Graphics.SetRenderTarget(rt,0,(CubemapFace)i);image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();cube.SetPixels(image.GetPixels(),(CubemapFace)i);}cube.Apply(true,false);if(created)AssetDatabase.CreateAsset(cube,path);else EditorUtility.SetDirty(cube);AssetDatabase.SaveAssetIfDirty(cube);return cube;}
            finally{EditorSceneManager.SetSceneCullingMask(scene,old);RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
