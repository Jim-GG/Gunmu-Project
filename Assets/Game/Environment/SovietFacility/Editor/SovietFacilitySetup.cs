using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SovietFacility.Editor
{
    /// <summary>Rebuilds only this kit's generated assets; never saves or replaces the active user scene.</summary>
    [InitializeOnLoad]
    public static class SovietFacilitySetup
    {
        const string Root="Assets/Game/Environment/SovietFacility/", Source="ArtSource/SovietFacility/";
        [Serializable] class Surface
        {
            public string name,kind,@base,normal,mask,detail;
            public float DustAmount,DustColourStrength,DustRoughness,DustTiling,SpotAmount,NormalStrength,MicroStrength,MicroTiling,RoughnessBias,SurfaceTiling;
        }
        [Serializable] class Module {public string name,category; public int triangles,nondegenerate; public string[] materials;}
        [Serializable] class Instance {public string module; public float[] position;public float yaw;}
        [Serializable] class Extra {public string name;public float[] position,size;}
        [Serializable] class Lamp {public string name;public float[] position,target,color;public float energy,size;}
        [Serializable] class Shot {public float[] position,target;public float fov;}
        [Serializable] class Layout {public string name;public Instance[] instances;public Extra[] extras;public Lamp[] lights;public Shot camera;}
        [Serializable] class Config {public Surface[] materials;public Module[] modules;public Layout[] scenes;}
        [Serializable] class Report {public bool passed;public int prefabs,materials,triangles;public string error;public string[] scenes,checks;}
        static bool busy;
        static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
        static SovietFacilitySetup(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Source+"UnityImport.request")||!File.Exists(Source+"unity_import.json"))return;
            File.WriteAllText(Source+"UnityImport.started",File.ReadAllText(Source+"UnityImport.request"));File.Delete(Source+"UnityImport.request");Run();
        }
        [MenuItem("Tools/Soviet Facility/Rebuild Kit And Review Scenes")]
        static async void Run()
        {
            if(busy)return;busy=true;
            var report=new Report();var errors=new List<string>();
            Application.LogCallback record=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception)errors.Add(message);};
            Application.logMessageReceived+=record;
            try
            {
                report=await Build();
                if(errors.Count>0)throw new Exception(string.Join("\n",errors.Distinct()));
                report.passed=true;Debug.Log("SOVIET_FACILITY_IMPORT_PASSED "+report.prefabs+" prefabs");
            }
            catch(Exception ex){report.passed=false;report.error=ex.ToString();Debug.LogException(ex);}
            finally{Application.logMessageReceived-=record;File.WriteAllText(Source+"unity_validation.json",JsonUtility.ToJson(report,true));busy=false;}
        }
        static T Asset<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static Vector3 P(float[] v)=>new Vector3(-v[0],v[2],-v[1]);
        static async Task<Report> Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before rebuilding the kit.");
            var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(Source+"unity_import.json"));
            foreach(var sub in new[]{"Materials","Prefabs","Scenes"})Directory.CreateDirectory(Root+sub);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader=Asset<Shader>("Assets/Game/Environment/ModernLab/Shaders/ModernLabLayered.shadergraph");if(!shader)throw new Exception("ModernLabLayered shader missing");
            var textureNames=cfg.materials.SelectMany(s=>new[]{s.@base,s.normal,s.mask}).Distinct();
            foreach(var name in textureNames)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"Textures/"+name+".png");if(!importer)throw new Exception("Texture missing: "+name);
                string before=EditorJsonUtility.ToJson(importer);
                importer.textureType=name.EndsWith("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=name.EndsWith("_BaseColor");importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
                importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            }
            Mats.Clear();
            foreach(var s in cfg.materials)
            {
                string path=Root+"Materials/"+s.name+".mat";var mat=Asset<Material>(path);if(!mat){mat=new Material(shader){name=s.name};AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.shaderKeywords=Array.Empty<string>();
                mat.SetTexture("_BaseMap",Asset<Texture2D>(Root+"Textures/"+s.@base+".png"));mat.SetTexture("_BumpMap",Asset<Texture2D>(Root+"Textures/"+s.normal+".png"));mat.SetTexture("_SurfaceMask",Asset<Texture2D>(Root+"Textures/"+s.mask+".png"));
                mat.SetTexture("_MicroNormal",Asset<Texture2D>("Assets/Game/Environment/ModernLab/Textures/"+(s.kind=="mineral"?"Lab_Detail_Mineral_Normal":"Lab_Detail_Coating_Normal")+".png"));
                mat.SetColor("_BaseColor",Color.white);mat.SetColor("_DustColour",new Color(.20f,.205f,.19f));
                foreach(var pair in new[]{("_DustAmount",s.DustAmount),("_DustColourStrength",s.DustColourStrength),("_DustRoughness",s.DustRoughness),("_DustTiling",s.DustTiling),("_SpotAmount",s.SpotAmount),("_NormalStrength",s.NormalStrength),("_MicroStrength",s.MicroStrength),("_MicroTiling",s.MicroTiling),("_RoughnessBias",s.RoughnessBias),("_SurfaceTiling",s.SurfaceTiling)})mat.SetFloat(pair.Item1,pair.Item2);
                mat.enableInstancing=true;mat.renderQueue=-1;EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);Mats[s.name]=mat;
            }
            // The review window is dark, dirty glazing; keep it separate from the opaque surface contract.
            var glass=Utility("SV_Window_Grimy",new Color(.09f,.13f,.11f,.55f),0);glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Smoothness",.72f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=3000;
            glass.SetTexture("_BumpMap",Mats["SV_Concrete_Damp"].GetTexture("_BumpMap"));glass.SetFloat("_BumpScale",.25f);glass.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(glass);Mats[glass.name]=glass;
            var staging=EditorSceneManager.NewPreviewScene();int triangles=0;
            try
            {
                foreach(var module in cfg.modules)
                {
                    string path=Root+"Models/"+module.name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("Model missing: "+path);
                    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.globalScale=1;importer.useFileScale=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                    foreach(var key in module.materials)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),key),Mats[key]);importer.SaveAndReimport();
                    var wrapper=new GameObject(module.name);SceneManager.MoveGameObjectToScene(wrapper,staging);
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(path),staging);go.transform.SetParent(wrapper.transform,false);
                    try
                    {
                        go.name="Model";int tris=0;
                        foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&Mats.TryGetValue(m.name,out var found)?found:throw new Exception("Unmapped material: "+module.name)).ToArray();
                            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;tris+=mesh.triangles.Length/3;
                            if(mesh.uv.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount)throw new Exception("Missing UV/tangents: "+module.name);
                            var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
                        }
                        // Unity removes zero-area triangles during FBX import. Only tolerate that measured source count.
                        if(tris>module.triangles||tris<module.nondegenerate||tris==0)throw new Exception("Triangle count mismatch: "+module.name+" "+tris+" vs "+module.nondegenerate+".."+module.triangles);triangles+=tris;
                        if(module.name=="T_Floor_2x2_Dark")
                        {
                            var mf=go.GetComponentInChildren<MeshFilter>();var b=mf.sharedMesh.bounds;var size=mf.transform.TransformVector(b.size);
                            if(Mathf.Abs(Mathf.Abs(size.x)-2)>.01f||Mathf.Abs(Mathf.Abs(size.z)-2)>.01f)throw new Exception("FBX unit conversion failed");
                        }
                        var saved=PrefabUtility.SaveAsPrefabAsset(wrapper,Root+"Prefabs/"+module.name+".prefab");
                        if(Quaternion.Angle(saved.transform.localRotation,Quaternion.identity)>.001f||saved.transform.localScale!=Vector3.one)throw new Exception("Non-neutral prefab root: "+module.name);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(wrapper);}
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(staging);}
            AssetDatabase.SaveAssets();
            foreach(var layout in cfg.scenes)await BuildScene(layout);
            return new Report{prefabs=cfg.modules.Length,materials=Mats.Count,triangles=triangles,scenes=cfg.scenes.Select(s=>Root+"Scenes/"+s.name+".unity").ToArray(),checks=new[]{"53 prefab meshes: triangle counts, UV0, Mikk tangents, material references and colliders","2m metric floor bounds verified","Existing ModernLabLayered shader and shared micro normals reused","2K linear masks and normal maps; sRGB albedo; packed alpha kept opaque","Review scenes rendered in URP with captured room reflections","Active scene left unsaved and unchanged"}};
        }
        static Material Utility(string name,Color color,float emission)
        {
            string path=Root+"Materials/"+name+".mat";var mat=Asset<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.3f);
            if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*emission);mat.SetTexture("_EmissionMap",Texture2D.whiteTexture);}EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);return mat;
        }
        static async Task BuildScene(Layout layout)
        {
            string path=Root+"Scenes/"+layout.name+".unity";
            if(SceneManager.GetSceneByPath(path).isLoaded)throw new Exception("Close the generated review scene before rebuilding it: "+path);
            var prior=SceneManager.GetActiveScene();var probes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var p in probes)p.enabled=false;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach(var item in layout.instances)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(Root+"Prefabs/"+item.module+".prefab"),scene);go.transform.SetPositionAndRotation(P(item.position),Quaternion.Euler(0,item.yaw,0));
                    foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                }
                var emission=Utility("SV_Review_Lamp",new Color(.65f,.68f,.55f),3);
                foreach(var item in layout.extras)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(go,scene);go.name=item.name;go.layer=30;go.transform.position=P(item.position);go.transform.localScale=new Vector3(item.size[0],item.size[2],item.size[1]);go.GetComponent<Renderer>().sharedMaterial=emission;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                }
                foreach(var item in layout.lights)
                {
                    var go=new GameObject(item.name,typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=P(item.position);go.transform.LookAt(P(item.target));var light=go.GetComponent<Light>();light.type=LightType.Spot;light.color=new Color(item.color[0],item.color[1],item.color[2]);light.intensity=item.energy*.055f;light.range=22;light.spotAngle=115;light.innerSpotAngle=85;light.shadows=LightShadows.Soft;light.cullingMask=1<<30;
                }
                var fill=new GameObject("Soft ambient fill",typeof(Light));SceneManager.MoveGameObjectToScene(fill,scene);fill.layer=30;fill.transform.rotation=Quaternion.Euler(45,-25,0);fill.GetComponent<Light>().type=LightType.Directional;fill.GetComponent<Light>().intensity=.8f;fill.GetComponent<Light>().cullingMask=1<<30;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.20f,.23f,.25f);RenderSettings.reflectionIntensity=.65f;
                var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.28f,.30f,.32f));RenderSettings.ambientProbe=ambient;
                var cg=new GameObject("Review camera",typeof(Camera),typeof(AudioListener));SceneManager.MoveGameObjectToScene(cg,scene);cg.layer=30;cg.transform.position=P(layout.camera.position);cg.transform.LookAt(P(layout.camera.target));cg.tag="MainCamera";var camera=cg.GetComponent<Camera>();camera.fieldOfView=Camera.HorizontalToVerticalFieldOfView(layout.camera.fov,1600f/1100);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.075f,.09f);camera.allowHDR=true;camera.cullingMask=1<<30;var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.volumeLayerMask=1<<30;
                var vg=new GameObject("Review colour",typeof(Volume));SceneManager.MoveGameObjectToScene(vg,scene);vg.layer=30;vg.GetComponent<Volume>().isGlobal=true;vg.GetComponent<Volume>().sharedProfile=Asset<VolumeProfile>("Assets/Game/Environment/ModernLab/Scenes/ModernLabPreviewVolume.asset");
                var pg=new GameObject("Room reflection",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(pg,scene);pg.layer=30;pg.transform.position=layout.name=="RailingsReview"?new Vector3(-2,1,-1):new Vector3(-2,1.5f,-3);var probe=pg.GetComponent<ReflectionProbe>();probe.enabled=false;probe.mode=ReflectionProbeMode.Custom;probe.size=new Vector3(22,12,24);probe.boxProjection=true;probe.customBakedTexture=CaptureRoom(scene,pg.transform.position,layout.name);probe.intensity=.65f;probe.enabled=true;
                await Task.Delay(500);
                ulong mask=EditorSceneManager.GetSceneCullingMask(scene);ulong capture=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,capture);camera.overrideSceneCullingMask=capture;
                try{Capture(camera,layout.name);}
                finally{camera.overrideSceneCullingMask=0;EditorSceneManager.SetSceneCullingMask(scene,mask);}
                EditorSceneManager.SaveScene(scene,path);
            }
            finally
            {
                foreach(var go in scene.GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Camera>())c.enabled=false;
                foreach(var go in scene.GetRootGameObjects())foreach(var p in go.GetComponentsInChildren<ReflectionProbe>())p.enabled=false;
                await Task.Delay(500);SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);foreach(var p in probes)if(p)p.enabled=true;
            }
        }
        static void Capture(Camera camera,string name)
        {
            var rt=RenderTexture.GetTemporary(1600,1100,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;Texture2D image=null;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Source+"Preview/Unity_"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
        }
        static Cubemap CaptureRoom(Scene scene,Vector3 position,string name)
        {
            ulong previousMask=EditorSceneManager.GetSceneCullingMask(scene);ulong mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);
            var go=new GameObject("Temporary room capture",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;var cam=go.GetComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<30;cam.overrideSceneCullingMask=mask;cam.nearClipPlane=.05f;cam.farClipPlane=80;cam.allowHDR=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.15f,.18f);cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){dimension=TextureDimension.Cube};rt.Create();var tex=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);var previous=RenderTexture.active;string path=Root+"Scenes/"+name+"_Reflection.cubemap";var cube=Asset<Cubemap>(path);bool created=!cube;if(created)cube=new Cubemap(256,TextureFormat.RGBAHalf,true){name=name+" room"};
            try
            {
                if(!cam.RenderToCubemap(rt))throw new Exception("Reflection capture failed");float min=float.MaxValue,max=float.MinValue;
                for(int i=0;i<6;i++){Graphics.SetRenderTarget(rt,0,(CubemapFace)i);tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();var pixels=tex.GetPixels();min=Mathf.Min(min,pixels.Min(c=>c.maxColorComponent));max=Mathf.Max(max,pixels.Max(c=>c.maxColorComponent));cube.SetPixels(pixels,(CubemapFace)i);}
                if(max-min<.05f)throw new Exception("Room reflection is a uniform background");cube.Apply(true,false);if(created)AssetDatabase.CreateAsset(cube,path);else EditorUtility.SetDirty(cube);AssetDatabase.SaveAssetIfDirty(cube);return cube;
            }
            finally{EditorSceneManager.SetSceneCullingMask(scene,previousMask);RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
