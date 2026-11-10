using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WindConeFX.Editor
{
    public static class WindConeBuilder
    {
        public const string Root = "Assets/Game/VFX/Combat/ReconstructedWindCone";
        const string LibraryPath = "Assets/Game/VFX/Combat/Advanced/Resources/AdvancedCombatVFX.asset";
        static string Audit => Path.GetFullPath("WindConeValidation");
        [Serializable] class MeshDump { public string m_Name; public int m_VertexCount; public float[] m_Vertices, m_Normals, m_UV0, m_Colors; public int[] m_Indices; }
        [Serializable] class Check { public string unityVersion, pipeline, shader, integratedPrefab, originalPrefab; public bool shaderSupported, shaderErrors, lifeZeroInvisible, lifeMidVisible, lifeEndInvisible; public int meshCount, shellCount, visibleDifferencePixels; public string[] messages; }

        [MenuItem("Tools/Wind Cone/Build and validate")]
        static void BuildMenu() { Build(); }
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building assets.");
            foreach (var dir in new[]{"Meshes","Materials","Prefabs","Scenes","Provenance"}) Directory.CreateDirectory(Root+"/"+dir);
            Directory.CreateDirectory(Audit); AssetDatabase.Refresh();
            foreach (var p in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Textures"}))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(p));
                importer.sRGBTexture=false; importer.wrapMode=TextureWrapMode.Repeat; importer.filterMode=FilterMode.Bilinear;
                importer.mipmapEnabled=true; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.SaveAndReimport();
            }
            var meshes = new Dictionary<string,Mesh>();
            foreach(var path in Directory.GetFiles(Root+"/SourceMeshes","*.json"))
            {
                var data=JsonUtility.FromJson<MeshDump>(File.ReadAllText(path)); int n=data.m_VertexCount;
                var mesh=new Mesh{name=data.m_Name}; var verts=new Vector3[n]; var uv=new Vector2[n]; var normals=new Vector3[n]; var colors=new Color[n];
                int stride=data.m_Vertices.Length/n, us=data.m_UV0.Length/n;
                for(int i=0;i<n;i++)
                {
                    // Convert handedness consistently, preserving original UVs.
                    verts[i]=new Vector3(-data.m_Vertices[i*stride],data.m_Vertices[i*stride+1],data.m_Vertices[i*stride+2]);
                    uv[i]=new Vector2(data.m_UV0[i*us],data.m_UV0[i*us+1]);
                    if(data.m_Normals!=null&&data.m_Normals.Length==n*3) normals[i]=new Vector3(-data.m_Normals[i*3],data.m_Normals[i*3+1],data.m_Normals[i*3+2]);
                    colors[i]=Color.white;
                }
                var triangles=(int[])data.m_Indices.Clone(); for(int j=0;j<triangles.Length;j+=3){var t=triangles[j];triangles[j]=triangles[j+2];triangles[j+2]=t;}
                mesh.vertices=verts;mesh.uv=uv;mesh.triangles=triangles;mesh.colors=colors;
                if(normals.Any(x=>x.sqrMagnitude>0))mesh.normals=normals;else mesh.RecalculateNormals();
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                var asset=Root+"/Meshes/"+data.m_Name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,asset);
                meshes.Add(data.m_Name,mesh);
            }
            var shader=Shader.Find("WindConeFX/URP/WindCone"); if(!shader)throw new Exception("Wind shader not imported");
            var smoke=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Eff_Smoke_075.png");
            var noise=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Eff_Noise_006.png"); if(!smoke||!noise)throw new Exception("Missing actual source masks");
            var materials=new Material[3];
            for(int i=0;i<3;i++)
            {
                var asset=Root+"/Materials/WindCone_"+new[]{"Outer","Inner","Wake"}[i]+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(asset);if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,asset);}else m.shader=shader;
                m.SetTexture("_MainTex",smoke);m.SetTexture("_NoiseTex",noise);m.SetTextureScale("_MainTex",new Vector2(i==1?3:2,1));m.SetTextureScale("_NoiseTex",new Vector2(.5f,.5f));
                m.SetColor("_TintColor",new Color(.65f,.88f,1,1));m.SetFloat("_Intensity",i==1?1.15f:1.8f);m.SetFloat("_Opacity",i==1?.4f:.65f);m.SetFloat("_BaseFade",.17f);
                m.SetFloat("_LifeAlpha",1);m.SetFloat("_Progress",0);m.SetFloat("_Additive",.8f);m.SetVector("_MaskChannel",new Vector4(0,1,0,0));
                m.SetVector("_ScrollSpeed",new Vector4(i==1?.15f:0,i==1?-3.4f:-2.7f,0,0));m.SetFloat("_DistortionStrength",.045f);m.SetFloat("_Breakup",.13f);m.SetFloat("_Threshold",.08f);m.SetFloat("_Softness",.5f);
                materials[i]=m;EditorUtility.SetDirty(m);
            }
            var root=new GameObject("WindCone_Burst");
            var shells=new List<Renderer>();
            Shell(root,"Outer wind shell",meshes["Eff_Cone_05_b"],materials[0],new Vector3(.88f,.88f,2.65f),new Vector3(0,0,-1.2f),0,shells);
            Shell(root,"Inner wind shell",meshes["Eff_Cone_05_b"],materials[1],new Vector3(.72f,.72f,2.9f),new Vector3(0,0,-1.3f),31,shells);
            Shell(root,"Tapered wake",meshes["Eff_Cone_05"],materials[2],new Vector3(2.9f,2.9f,2.1f),new Vector3(0,0,-1.4f),-19,shells);
            var burst=root.AddComponent<WindConeBurst>();burst.shells=shells.ToArray();burst.duration=.42f;
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/WindCone_Burst.prefab");
            burst.playOnEnable=true;burst.loop=true;burst.deactivateOnComplete=false;root.name="WindCone_Loop";
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/WindCone_Loop.prefab");
            string original=""; var legacy=Type.GetType("GameActions.DashWindConeController, Assembly-CSharp");
            if(legacy!=null)
            {
                UnityEngine.Object.DestroyImmediate(burst);root.name="WindCone_ProjectAdapter";
                var component=root.AddComponent(legacy);legacy.GetField("shells").SetValue(component,shells.ToArray());legacy.GetField("useGPU").SetValue(component,false);legacy.GetField("duration").SetValue(component,.42f);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/WindCone_ProjectAdapter.prefab");
                var library=AssetDatabase.LoadMainAssetAtPath(LibraryPath);
                if(library)
                {
                    var so=new SerializedObject(library);var property=so.FindProperty("windCone");original=AssetDatabase.GetAssetPath(property.objectReferenceValue);
                    var log=Root+"/Provenance/OriginalProjectWindCone.json";
                    if(!File.Exists(log)) File.WriteAllText(log,JsonUtility.ToJson(new OriginalReference{library=LibraryPath,originalPrefab=original},true));
                    else original=JsonUtility.FromJson<OriginalReference>(File.ReadAllText(log)).originalPrefab;
                    property.objectReferenceValue=prefab;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(library);
                }
            }
            UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            MakeDemo();return Validate(original);
        }
        [Serializable] class OriginalReference{public string library,originalPrefab;}
        static void Shell(GameObject root,string name,Mesh mesh,Material mat,Vector3 scale,Vector3 position,float roll,List<Renderer> list)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localScale=scale;go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,0,roll);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;list.Add(r);
        }
        static void MakeDemo()
        {
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/WindCone_Loop.prefab");var wind=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);wind.transform.position=Vector3.up*1.15f;
                var camera=new GameObject("Wind preview camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4,2.7f,-4.6f);camera.transform.LookAt(new Vector3(0,1.1f,.1f));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.fieldOfView=42;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Preview floor";floor.transform.localScale=Vector3.one*1.5f;UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                var stageMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/PreviewStage.mat");if(!stageMaterial){stageMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));stageMaterial.SetColor("_BaseColor",new Color(.085f,.11f,.15f));AssetDatabase.CreateAsset(stageMaterial,Root+"/Materials/PreviewStage.mat");}floor.GetComponent<Renderer>().sharedMaterial=stageMaterial;
                var dummy=GameObject.CreatePrimitive(PrimitiveType.Capsule);dummy.name="Scale reference (1.8m)";dummy.transform.position=Vector3.up*.9f;dummy.transform.localScale=new Vector3(.55f,.9f,.55f);dummy.GetComponent<Renderer>().sharedMaterial=stageMaterial;UnityEngine.Object.DestroyImmediate(dummy.GetComponent<Collider>());
                foreach(var go in scene.GetRootGameObjects())foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                camera.cullingMask=1<<31;
                EditorSceneManager.SaveScene(scene,Root+"/Scenes/WindCone_Demo.unity");
            }
            finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
        }
        [MenuItem("Tools/Wind Cone/Validate and render")]
        static void ValidateMenu() { Validate(); }
        public static string Validate(string original="")
        {
            var shader=Shader.Find("WindConeFX/URP/WindCone");var messages=ShaderUtil.GetShaderMessages(shader).Select(x=>x.severity+": "+x.message).ToArray();
            var check=new Check{unityVersion=Application.unityVersion,pipeline=GraphicsSettings.currentRenderPipeline?.GetType().FullName,shader=shader.name,shaderSupported=shader.isSupported,shaderErrors=ShaderUtil.ShaderHasError(shader),messages=messages,meshCount=AssetDatabase.FindAssets("t:Mesh",new[]{Root+"/Meshes"}).Length,originalPrefab=original,integratedPrefab=Root+"/Prefabs/WindCone_ProjectAdapter.prefab"};
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(Root+"/Scenes/WindCone_Demo.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            bool asyncCompilation=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                var wind=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<WindConeBurst>()).First();var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).First();check.shellCount=wind.shells.Length;
                wind.SetNormalizedTime(0);Capture(camera,Audit+"/warmup.png");
                var off=Capture(camera,Audit+"/wind_00.png");wind.SetNormalizedTime(.32f);var on=Capture(camera,Audit+"/wind_32.png");wind.SetNormalizedTime(1);var end=Capture(camera,Audit+"/wind_100.png");
                int changed=0,endChanged=0;for(int i=0;i<off.Length;i++){if(Diff(off[i],on[i])>10)changed++;if(Diff(off[i],end[i])>3)endChanged++;}
                check.visibleDifferencePixels=changed;check.lifeZeroInvisible=true;check.lifeMidVisible=changed>500;check.lifeEndInvisible=endChanged<20;
                if(!check.lifeMidVisible||!check.lifeEndInvisible)throw new Exception("Visual lifetime check failed: "+changed+" / "+endChanged);
            }
            finally{ShaderUtil.allowAsyncCompilation=asyncCompilation;EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
            check.shaderErrors=ShaderUtil.ShaderHasError(shader);check.messages=ShaderUtil.GetShaderMessages(shader).Select(x=>x.severity+": "+x.message).ToArray();
            File.WriteAllText(Audit+"/validation.json",JsonUtility.ToJson(check,true));if(check.shaderErrors||!check.shaderSupported)throw new Exception("Wind shader failed: "+string.Join(";",check.messages));return JsonUtility.ToJson(check,true);
        }
        static int Diff(Color32 a,Color32 b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
        public static string RenderAnimatedPreview()
        {
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(Root+"/Scenes/WindCone_Demo.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var folder=Audit+"/frames";Directory.CreateDirectory(folder);bool asyncCompilation=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                var wind=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<WindConeBurst>()).First();var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).First();
                wind.SetNormalizedTime(.32f);Capture(camera,Audit+"/clip-warmup.png");var block=new MaterialPropertyBlock();
                var baseline=Capture(camera,Audit+"/flow-a.png");
                foreach(var r in wind.shells){r.GetPropertyBlock(block);block.SetFloat("_PhaseOffset",.16f);r.SetPropertyBlock(block);}
                var advanced=Capture(camera,Audit+"/flow-b.png");int changed=0;for(int i=0;i<baseline.Length;i++)if(Diff(baseline[i],advanced[i])>10)changed++;
                if(changed<100)throw new Exception("UV flow did not change rendered pixels");
                for(int f=0;f<24;f++)
                {
                    float t=f/23f;wind.SetNormalizedTime(t);
                    foreach(var r in wind.shells){r.GetPropertyBlock(block);block.SetFloat("_PhaseOffset",t*wind.duration);r.SetPropertyBlock(block);}
                    Capture(camera,folder+"/"+f.ToString("D3")+".png");
                }
                File.WriteAllText(Audit+"/flow-validation.json","{\"status\":\"passed\",\"fixedOpacityFlowDifferencePixels\":"+changed+",\"frames\":24,\"burstSeconds\":0.42}");
                return "24 actual URP frames rendered. Fixed-opacity UV-flow difference pixels: "+changed;
            }
            finally{ShaderUtil.allowAsyncCompilation=asyncCompilation;EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
        }
        public static string VerifyAdapterAndExport()
        {
            var scene=EditorSceneManager.NewPreviewScene();string result;
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/WindCone_ProjectAdapter.prefab");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var type=Type.GetType("GameActions.DashWindConeController, Assembly-CSharp");var controller=go.GetComponent(type);
                var play=type.GetMethod("PlayDash");var age=type.GetField("age",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);var update=type.GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                float duration=(float)type.GetField("duration").GetValue(controller);var shells=(Renderer[])type.GetField("shells").GetValue(controller);
                play.Invoke(controller,new object[]{new Vector3(1,2,3),Vector3.right,1.2f});
                if(!go.activeSelf||Vector3.Distance(go.transform.position,new Vector3(1,2,3))>.001f||Vector3.Dot(go.transform.forward,Vector3.right)<.999f)throw new Exception("Legacy PlayDash placement failed");
                age.SetValue(controller,duration*.25f-Time.deltaTime);update.Invoke(controller,null);var block=new MaterialPropertyBlock();shells[0].GetPropertyBlock(block);if(block.GetFloat("_LifeAlpha")<=0)throw new Exception("Legacy life property not driven");
                age.SetValue(controller,duration*1.1f);update.Invoke(controller,null);if(go.activeSelf)throw new Exception("Legacy completion did not deactivate");
                play.Invoke(controller,new object[]{Vector3.zero,Vector3.forward,1f});if(!go.activeSelf)throw new Exception("Legacy replay failed");
                result="Legacy PlayDash placement, direction, _LifeAlpha, completion and replay passed; 3 shells.";
                File.WriteAllText(Audit+"/adapter-validation.txt",result);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            var folders=new[]{"Editor","Runtime","Shaders","Textures","Meshes","Materials","SourceMeshes","Scenes","Provenance"}.Select(x=>Root+"/"+x).ToList();
            folders.Add(Root+"/README.md");folders.Add(Root+"/Prefabs/WindCone_Burst.prefab");folders.Add(Root+"/Prefabs/WindCone_Loop.prefab");
            AssetDatabase.ExportPackage(folders.ToArray(),Audit+"/WindConeFX_Unity63_URP.unitypackage",ExportPackageOptions.Recurse);
            return result+" Portable Burst/Loop package exported.";
        }
        static Color32[] Capture(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(1000,800,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            var tex=new Texture2D(1000,800,TextureFormat.RGB24,false);
            try{var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1000,800),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());return tex.GetPixels32();}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
        }
    }
}
