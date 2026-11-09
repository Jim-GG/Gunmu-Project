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

namespace ModernLab.Editor
{
    public static class ModernLabMaterialSetup
    {
        const string Root="Assets/Game/Environment/ModernLab/";
        static readonly string[] Keys={"Lab_White_Matte","Lab_Dark_Panel","Lab_Floor_Resin","Lab_Rail_BrushedSteel","Lab_Floor_Checker","Lab_Ceiling_Acoustic","Lab_Wall_Ribbed","Lab_Paint_RoughWhite","Lab_Paint_GlossWhite","Lab_Paint_Lime","Lab_Paint_Red","Lab_Paint_Yellow","Lab_White_Coursed","Lab_Paint_WhiteSteel"};
        static string TextureKey(string key,string suffix)=>new[]{"Lab_Paint_Lime","Lab_Paint_Red","Lab_Paint_Yellow"}.Contains(key)?key=="Lab_Paint_Yellow"?"Lab_Paint_GlossWhite":"Lab_Paint_RoughWhite":key;
        static Color Tint(string key)=>key=="Lab_Paint_Lime"?new Color(.53f,.94f,.025f):key=="Lab_Paint_Red"?new Color(.97f,.065f,.035f):key=="Lab_Paint_Yellow"?new Color(.99f,.79f,.025f):Color.white;
        static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
        static T Asset<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        [MenuItem("Tools/Modern Lab/Prepare Materials And Module Prefabs")]
        static async void PrepareFromMenu()
        {try{await Setup();}catch(Exception exception){Debug.LogException(exception);}}
        public static async Task<object> Setup()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before importing material assets");
            foreach(var folder in new[]{"Materials","Prefabs","Scenes","PreviewMeshes"})Directory.CreateDirectory(Root+folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool revised=File.Exists("ArtSource/ModernLab/surface_v2_unity.json");
            if(revised)ModernLabSurfaceV2.Initialize();
            var shader=revised?Asset<Shader>(Root+"Shaders/ModernLabLayered.shadergraph"):Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("Modern Lab surface shader is missing");
            Mats.Clear();
            foreach(var key in Keys)
            {
                foreach(var suffix in new[]{"BaseColor","Normal","Mask"})
                {
                    string texturePath=Root+"Textures/"+TextureKey(key,suffix)+"_"+suffix+".png";
                    var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);if(!importer)throw new Exception("Missing texture: "+texturePath);
                    string before=EditorJsonUtility.ToJson(importer);
                    importer.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                    importer.sRGBTexture=suffix=="BaseColor";importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
                    importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.maxTextureSize=2048;
                    importer.textureCompression=TextureImporterCompression.CompressedHQ;
                    if(EditorJsonUtility.ToJson(importer)!=before)importer.SaveAndReimport();
                }
                string path=Root+"Materials/"+key+".mat";var material=Asset<Material>(path);
                if(!material){material=new Material(shader){name=key};AssetDatabase.CreateAsset(material,path);}
                material.shader=shader;
                if(revised)ModernLabSurfaceV2.Configure(material,key);
                else
                {
                material.SetTexture("_BaseMap",Asset<Texture2D>(Root+"Textures/"+TextureKey(key,"BaseColor")+"_BaseColor.png"));
                material.SetTexture("_BumpMap",Asset<Texture2D>(Root+"Textures/"+TextureKey(key,"Normal")+"_Normal.png"));
                var mask=Asset<Texture2D>(Root+"Textures/"+TextureKey(key,"Mask")+"_Mask.png");material.SetTexture("_MetallicGlossMap",mask);material.SetTexture("_OcclusionMap",mask);
                material.SetColor("_BaseColor",Tint(key));material.SetFloat("_Metallic",key=="Lab_Rail_BrushedSteel"?1:0);material.SetFloat("_Smoothness",1);material.SetFloat("_SmoothnessTextureChannel",0);
                material.SetFloat("_BumpScale",1);material.SetFloat("_OcclusionStrength",.35f);material.SetFloat("_Cull",2);
                material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");material.EnableKeyword("_OCCLUSIONMAP");
                }
                material.enableInstancing=true;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);Mats[key]=material;
            }
            var staging=EditorSceneManager.NewPreviewScene();var prefabs=new List<string>();
            try
            {
                foreach(var path in Directory.GetFiles(Root+"Models","*.fbx").OrderBy(p=>p))
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                    string before=EditorJsonUtility.ToJson(importer);
                    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.globalScale=1;importer.useFileScale=true;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=true;
                    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                    foreach(var pair in Mats)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
                    if(EditorJsonUtility.ToJson(importer)!=before)importer.SaveAndReimport();
                    var model=Asset<GameObject>(path);var go=(GameObject)PrefabUtility.InstantiatePrefab(model,staging);
                    try
                    {
                        go.name=Path.GetFileNameWithoutExtension(path);
                        foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>
                            {
                                if(!m)throw new Exception("Null source material: "+path);
                                var match=Keys.FirstOrDefault(k=>m.name.StartsWith(k,StringComparison.Ordinal));
                                if(match==null)throw new Exception("Unrecognized material "+m.name+" in "+path);
                                return Mats[match];
                            }).ToArray();
                        }
                        string prefab=Root+"Prefabs/"+go.name+".prefab";PrefabUtility.SaveAsPrefabAsset(go,prefab);prefabs.Add(prefab);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(go);}
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(staging);}
            await BuildPreview();
            await BuildModuleReview();
            await BuildModuleReview("addon_layout.json","ModernLabAdditionalSurfaces");
            return new{materials=Keys,prefabs=prefabs.Count,sharedTextures=revised?35:27,resolution=2048,mask="R metallic / G AO / B dust / A clean smoothness",scene=Root+"Scenes/ModernLabAdditionalSurfaces.unity"};
        }
        static Mesh BoxMesh(Vector3 size)
        {
            string path=Root+"PreviewMeshes/Box_"+size.x.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+".asset";
            var existing=Asset<Mesh>(path);if(existing)return existing;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 axis=Vector3.Cross(normal,Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up).normalized;
                Vector3 other=Vector3.Cross(normal,axis);int offset=vertices.Count;
                foreach(var p in new[]{-axis-other,axis-other,axis+other,-axis+other})
                {
                    var v=Vector3.Scale(normal+p,size)*.5f;vertices.Add(v);
                    float u,w;if(Mathf.Abs(normal.y)>.5f){u=v.x+size.x*.5f;w=v.z+size.z*.5f;}else if(Mathf.Abs(normal.z)>.5f){u=v.x+size.x*.5f;w=v.y+size.y*.5f;}else{u=v.z+size.z*.5f;w=v.y+size.y*.5f;}
                    uv.Add(new Vector2(u,w)*.5f);
                }
                triangles.AddRange(new[]{offset,offset+1,offset+2,offset,offset+2,offset+3});
            }
            var mesh=new Mesh{name="Material Preview Box"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject Box(Scene scene,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;
            go.GetComponent<MeshFilter>().sharedMesh=BoxMesh(size);go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static Material Utility(string name,Color colour,float emission)
        {
            string path=Root+"Materials/"+name+".mat";var material=Asset<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",colour);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.3f);
            if(emission>0){material.EnableKeyword("_EMISSION");material.SetTexture("_EmissionMap",Texture2D.whiteTexture);material.SetColor("_EmissionColor",colour*emission);material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;}
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        static async Task BuildPreview()
        {
            if(SceneManager.GetSceneByPath(Root+"Scenes/ModernLabMaterialPreview.unity").isLoaded)return;
            var otherProbes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();
            foreach(var existing in otherProbes)existing.enabled=false;
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var white=Mats[Keys[0]];var dark=Mats[Keys[1]];var floor=Mats[Keys[2]];
                Box(scene,"Reflective resin floor",new Vector3(0,-.1f,0),new Vector3(9,.2f,9),floor);
                Box(scene,"White back wall",new Vector3(0,2.6f,3.2f),new Vector3(8,3.2f,.15f),white);
                Box(scene,"Dark lower wall",new Vector3(0,.65f,3.08f),new Vector3(8,1.3f,.15f),dark);
                Box(scene,"White left wall",new Vector3(-4,2.1f,1),new Vector3(.15f,4.2f,4.4f),white);
                Box(scene,"Dark service column",new Vector3(3.5f,2,1.3f),new Vector3(.45f,4,3.8f),dark);
                Box(scene,"Service door",new Vector3(.4f,1.35f,3),new Vector3(1.6f,2.65f,.18f),dark);
                foreach(float x in new[]{-.48f,1.28f})Box(scene,"Door white trim",new Vector3(x,1.4f,2.87f),new Vector3(.075f,2.8f,.12f),white);
                Box(scene,"Door white header",new Vector3(.4f,2.81f,2.87f),new Vector3(1.85f,.075f,.12f),white);
                var emitter=Utility("Preview_Light",new Color(.8f,.92f,1),4);var yellow=Utility("Preview_Safety_Marking",new Color(.82f,.53f,.07f),0);
                Box(scene,"Observation window",new Vector3(-2.6f,2.3f,3.05f),new Vector3(1.8f,1.2f,.04f),emitter);
                foreach(float x in new[]{-2.5f,0,2.5f})
                {
                    Box(scene,"Ceiling light",new Vector3(x,3.75f,.6f),new Vector3(.11f,.04f,4),emitter);
                    foreach(float z in new[]{-.5f,1.5f})
                    {
                        var go=new GameObject("Ceiling illumination",typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(x,3.6f,z);go.transform.rotation=Quaternion.Euler(90,0,0);
                        var light=go.GetComponent<Light>();light.type=LightType.Spot;light.intensity=9;light.range=9;light.spotAngle=115;light.innerSpotAngle=80;light.color=new Color(.94f,.97f,1);light.shadows=LightShadows.Soft;
                    }
                }
                foreach(float x in new[]{-2f,2f})Box(scene,"Safety floor line",new Vector3(x,.006f,-.7f),new Vector3(.025f,.006f,5),yellow);
                var key=new GameObject("Broad fill",typeof(Light));SceneManager.MoveGameObjectToScene(key,scene);key.transform.rotation=Quaternion.Euler(40,-25,0);var sun=key.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.shadows=LightShadows.Soft;
                for(int i=0;i<3;i++)Box(scene,Keys[i]+" swatch",new Vector3(i*2.15f-2.15f,1,.5f),new Vector3(1.8f,1.8f,.18f),Mats[Keys[i]]);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.45f,.48f);RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;RenderSettings.reflectionIntensity=1;
                var probeGo=new GameObject("Room reflections",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(probeGo,scene);probeGo.transform.position=new Vector3(0,1.5f,0);
                var probe=probeGo.GetComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;probe.renderDynamicObjects=true;probe.size=new Vector3(9,4.8f,9);probe.center=new Vector3(0,.6f,0);probe.boxProjection=true;probe.resolution=256;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.18f,.21f,.25f);
                var cameraGo=new GameObject("Material Preview Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);cameraGo.transform.position=new Vector3(7,3.4f,-9);cameraGo.transform.LookAt(new Vector3(0,1.5f,1));
                var camera=cameraGo.GetComponent<Camera>();camera.fieldOfView=46;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.21f,.25f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                var vg=new GameObject("Preview colour",typeof(Volume));SceneManager.MoveGameObjectToScene(vg,scene);var volume=vg.GetComponent<Volume>();volume.isGlobal=true;
                var vp=ScriptableObject.CreateInstance<VolumeProfile>();var tonemap=vp.Add<Tonemapping>(true);tonemap.mode.Override(TonemappingMode.ACES);string volumePath=Root+"Scenes/ModernLabPreviewVolume.asset";
                var old=Asset<VolumeProfile>(volumePath);if(old){UnityEngine.Object.DestroyImmediate(vp);vp=old;}else{AssetDatabase.CreateAsset(vp,volumePath);foreach(var component in vp.components)AssetDatabase.AddObjectToAsset(component,vp);EditorUtility.SetDirty(vp);AssetDatabase.SaveAssetIfDirty(vp);}volume.sharedProfile=vp;
                // Isolate preview rendering when opened additively beside a game scene.
                foreach(var go in scene.GetRootGameObjects())go.layer=30;
                camera.cullingMask=1<<30;camera.GetUniversalAdditionalCameraData().volumeLayerMask=1<<30;
                foreach(var go in scene.GetRootGameObjects())if(go.TryGetComponent<Light>(out var light))light.cullingMask=1<<30;
                probe.cullingMask=1<<30;
                probe.enabled=false;
                var reflection=CaptureRoom(scene,probe.transform.position);
                probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=reflection;probe.enabled=true;
                EditorSceneManager.SaveScene(scene,Root+"Scenes/ModernLabMaterialPreview.unity");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                foreach(var root in scene.GetRootGameObjects()){if(root.TryGetComponent<ReflectionProbe>(out var p))p.enabled=false;if(root.TryGetComponent<Camera>(out var c))c.enabled=false;}
                // Let the editor finish culling this scene before destroying its probes.
                await Task.Delay(500);EditorSceneManager.CloseScene(scene,true);
                foreach(var existing in otherProbes)if(existing)existing.enabled=true;
            }
        }
        public static async Task RebuildPreview()
        {foreach(var key in Keys)Mats[key]=Asset<Material>(Root+"Materials/"+key+".mat");await BuildPreview();}
        public static Cubemap CaptureRoom(Scene scene,Vector3 position,string filename="ModernLabRoomReflection")
        {
            ulong previousMask=EditorSceneManager.GetSceneCullingMask(scene);ulong captureMask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,captureMask);
            string path=Root+"Scenes/"+filename+".cubemap";
            var go=new GameObject("Reflection capture",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;
            var camera=go.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.overrideSceneCullingMask=captureMask;camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.21f,.25f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){dimension=TextureDimension.Cube};target.Create();
            var readback=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);var prior=RenderTexture.active;
            var cube=Asset<Cubemap>(path);bool created=!cube;if(created)cube=new Cubemap(256,TextureFormat.RGBAHalf,true){name="ModernLabRoomReflection"};
            try
            {
                if(!camera.RenderToCubemap(target))throw new Exception("Room reflection capture failed");
                float min=float.MaxValue,max=float.MinValue;
                for(int i=0;i<6;i++)
                {
                    Graphics.SetRenderTarget(target,0,(CubemapFace)i);readback.ReadPixels(new Rect(0,0,256,256),0,0);readback.Apply();var pixels=readback.GetPixels();
                    min=Mathf.Min(min,pixels.Min(c=>c.maxColorComponent));max=Mathf.Max(max,pixels.Max(c=>c.maxColorComponent));cube.SetPixels(pixels,(CubemapFace)i);
                }
                if(max-min<.1f)throw new Exception("Reflection capture contains only a uniform background");
                cube.Apply(true,false);if(created)AssetDatabase.CreateAsset(cube,path);else EditorUtility.SetDirty(cube);AssetDatabase.SaveAssetIfDirty(cube);return cube;
            }
            finally{EditorSceneManager.SetSceneCullingMask(scene,previousMask);RenderTexture.active=prior;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(go);}
        }
        [Serializable]class ReviewObject{public string source;public float[] location;public string role;}
        [Serializable]class ReviewCamera{public float[] position;public float[] target;public float fov;}
        [Serializable]class ReviewLayout{public ReviewObject[] objects;public ReviewCamera camera;public float aspect;}
        [Serializable]class ExportItem{public string fbx;public string source;public string role;}
        [Serializable]class ExportReport{public ExportItem[] exports;}
        static Vector3 FromBlender(float[] value)=>new Vector3(-value[0],value[2],-value[1]);
        static async Task BuildModuleReview(string layoutFile="review_layout.json",string sceneName="ModernLabModuleReview")
        {
            if(SceneManager.GetSceneByPath(Root+"Scenes/"+sceneName+".unity").isLoaded)return;
            var layout=JsonUtility.FromJson<ReviewLayout>(File.ReadAllText("ArtSource/ModernLab/"+layoutFile));
            var exports=JsonUtility.FromJson<ExportReport>(File.ReadAllText("ArtSource/ModernLab/build_report.json")).exports.ToDictionary(e=>e.source,e=>e.fbx);
            var existingProbes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var p in existingProbes)p.enabled=false;
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach(var item in layout.objects)
                {
                    var path=Root+"Prefabs/"+Path.GetFileNameWithoutExtension(exports[item.source])+".prefab";
                    var prefab=Asset<GameObject>(path);if(!prefab)throw new Exception("Missing actual module "+path);
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.name=item.source;go.transform.position=FromBlender(item.location);
                    foreach(var transform in go.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=30;
                    if(layoutFile=="addon_layout.json")foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.renderingLayerMask=item.role=="Ceiling"?5u:item.role.StartsWith("Wall")||item.role=="AccentWall"?3u:1u;
                }
                var emitter=Asset<Material>(Root+"Materials/Preview_Light.mat");
                if(layoutFile=="addon_layout.json")
                {
                    foreach(var fill in new[]{("Ceiling bounce fill",new Vector3(-90,0,0),.5f)})
                    {
                        var go=new GameObject(fill.Item1,typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.rotation=Quaternion.Euler(fill.Item2);var light=go.GetComponent<Light>();light.type=LightType.Directional;light.intensity=fill.Item3;light.cullingMask=1<<30;light.shadows=LightShadows.None;light.GetUniversalAdditionalLightData().renderingLayers=4u;
                    }
                    {
                        var go=new GameObject("Frontal surface soft fill",typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=new Vector3(0,2.2f,2.5f);go.transform.LookAt(new Vector3(0,1.5f,-3));var light=go.GetComponent<Light>();light.type=LightType.Spot;light.intensity=60;light.range=20;light.spotAngle=110;light.innerSpotAngle=90;light.cullingMask=1<<30;light.shadows=LightShadows.None;light.GetUniversalAdditionalLightData().renderingLayers=2u;
                    }
                    foreach(float x in new[]{-3f,0,3f})
                    {
                        Box(scene,"Ceiling luminous strip",new Vector3(-x,3.23f,-.1f),new Vector3(.16f,.025f,2.9f),emitter).layer=30;
                        var go=new GameObject("Under ceiling illumination",typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=new Vector3(-x,3.1f,-.1f);go.transform.rotation=Quaternion.Euler(90,0,0);var light=go.GetComponent<Light>();light.type=LightType.Spot;light.intensity=18;light.range=10;light.spotAngle=120;light.innerSpotAngle=90;light.cullingMask=1<<30;light.shadows=LightShadows.Soft;
                    }
                }
                else foreach(var v in new[]{new Vector3(-4,5,1),new Vector3(-12,5,-2)})Box(scene,"Studio reflection strip",v,new Vector3(6,.025f,.4f),emitter).layer=30;
                foreach(var v in new[]{new Vector3(-2,7,3),new Vector3(-11,6,-3),new Vector3(0,7,-8)})
                {
                    var go=new GameObject("Module studio light",typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=v;go.transform.LookAt(new Vector3(-6,1,-2));
                    var light=go.GetComponent<Light>();light.type=LightType.Spot;light.intensity=35;light.range=30;light.spotAngle=125;light.innerSpotAngle=100;light.cullingMask=1<<30;light.shadows=LightShadows.Soft;
                }
                var key=new GameObject("Studio fill",typeof(Light));SceneManager.MoveGameObjectToScene(key,scene);key.layer=30;key.transform.rotation=Quaternion.Euler(45,-25,0);var sun=key.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.25f;sun.cullingMask=1<<30;sun.shadows=LightShadows.Soft;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.54f,.58f);RenderSettings.reflectionIntensity=1;
                var cameraGo=new GameObject("Actual module camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);cameraGo.layer=30;cameraGo.transform.position=FromBlender(layout.camera.position);cameraGo.transform.LookAt(FromBlender(layout.camera.target));
                var camera=cameraGo.GetComponent<Camera>();camera.fieldOfView=Camera.HorizontalToVerticalFieldOfView(layout.camera.fov,layout.aspect>0?layout.aspect:1600f/1100);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.21f,.25f);camera.allowHDR=true;camera.cullingMask=1<<30;
                var additional=camera.GetUniversalAdditionalCameraData();additional.renderPostProcessing=true;additional.volumeLayerMask=1<<30;additional.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var vg=new GameObject("Module colour",typeof(Volume));SceneManager.MoveGameObjectToScene(vg,scene);vg.layer=30;var volume=vg.GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=Asset<VolumeProfile>(Root+"Scenes/ModernLabPreviewVolume.asset");
                var probeGo=new GameObject("Module studio reflections",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(probeGo,scene);probeGo.layer=30;probeGo.transform.position=FromBlender(layout.camera.target)+Vector3.up*1.4f;var probe=probeGo.GetComponent<ReflectionProbe>();probe.enabled=false;probe.mode=ReflectionProbeMode.Custom;probe.boxProjection=true;probe.size=new Vector3(40,14,30);probe.cullingMask=1<<30;
                probe.customBakedTexture=CaptureRoom(scene,probeGo.transform.position,sceneName+"Reflection");probe.intensity=layoutFile=="addon_layout.json"?.25f:.65f;probe.enabled=true;
                EditorSceneManager.SaveScene(scene,Root+"Scenes/"+sceneName+".unity");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);foreach(var go in scene.GetRootGameObjects()){if(go.TryGetComponent<ReflectionProbe>(out var p))p.enabled=false;if(go.TryGetComponent<Camera>(out var c))c.enabled=false;}
                await Task.Delay(500);EditorSceneManager.CloseScene(scene,true);foreach(var p in existingProbes)if(p)p.enabled=true;
            }
        }
    }
}
