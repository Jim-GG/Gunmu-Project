using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Gauntlet.Editor
{
    public static class GauntletAssetTools
    {
        public const string Root = "Assets/Game/Player/Gauntlet/";
        [Serializable] public class ClipData { public string name, fbx; public int frames, fps; public float seconds; public float[] camera_angles_deg, camera_translation_m; }
        [Serializable] public class Manifest { public ClipData[] clips; }
        public static Manifest ReadClips() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "Animations/Clips.json"));

        [MenuItem("Tools/Gauntlet/Rebuild Imported Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding assets.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in Directory.GetFiles(Root + "Textures", "*.png"))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("_Normal");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = path.Contains("BaseColor") || path.Contains("Emission");
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.isReadable = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 4;
                ti.maxTextureSize = path.Contains("BaseColor") || normal ? 4096 : 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                var platform = ti.GetPlatformTextureSettings("Standalone");
                platform.name = "Standalone"; platform.overridden = true;
                platform.maxTextureSize = ti.maxTextureSize;
                platform.format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
                ti.SetPlatformTextureSettings(platform);
                ti.SaveAndReimport();
            }

            string modelPath = Root + "Models/Gauntlet_Model.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            ConfigureModel(mi);
            mi.importAnimation = false;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isValid) throw new Exception("Base Generic avatar is missing or invalid.");
            var manifest = ReadClips();
            foreach (var data in manifest.clips)
            {
                var ai = (ModelImporter)AssetImporter.GetAtPath(Root + "Animations/" + data.fbx);
                ConfigureModel(ai);
                ai.importAnimation = true;
                ai.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                ai.sourceAvatar = avatar;
                ai.animationCompression = ModelImporterAnimationCompression.Off;
                ai.resampleCurves = true;
                var clips = ai.defaultClipAnimations;
                if (clips.Length != 1) throw new Exception("Expected exactly one take: " + data.fbx);
                clips[0].name = data.name;
                clips[0].loopTime = data.name == "Idle" || data.name == "Parry-Keep" || data.name == "charge full";
                clips[0].loopPose = false;
                clips[0].lockRootRotation = true;
                clips[0].lockRootHeightY = true;
                clips[0].lockRootPositionXZ = true;
                ai.clipAnimations = clips;
                ai.SaveAndReimport();
            }
            Material opaque = GetMaterial("Gauntlet_Atlas", Shader.Find("Gauntlet/Atlas URP"));
            opaque.SetTexture("_BaseMap", LoadTexture("BaseColor"));
            opaque.SetTexture("_BumpMap", LoadTexture("Normal"));
            opaque.SetTexture("_MetallicGlossMap", LoadTexture("MetallicSmoothness"));
            opaque.SetTexture("_EmissionMap", LoadTexture("EmissionRegions"));
            opaque.SetFloat("_EmissionStrength", 2f);
            opaque.SetFloat("_PulseAmount", .15f);
            opaque.SetFloat("_Cull", (float)CullMode.Back);
            Material glass = GetMaterial("Gauntlet_Glass", Shader.Find("Universal Render Pipeline/Lit"));
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetFloat("_Cull", (float)CullMode.Back);
            glass.SetColor("_BaseColor", new Color(.62f,.72f,.77f,.14f));
            glass.SetFloat("_Metallic", .05f);
            glass.SetFloat("_Smoothness", .88f);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.renderQueue = (int)RenderQueue.Transparent;

            string controllerPath = Root + "Animations/Gauntlet.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            foreach (var data in manifest.clips)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Root + "Animations/" + data.fbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                var state = machine.AddState(data.name);
                state.motion = clip; state.writeDefaultValues = false;
                if (data.name == "Idle") machine.defaultState = state;
            }

            var container = new GameObject("GauntletViewModel");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.name = "Gauntlet_Model";
                model.transform.SetParent(container.transform, false);
                var anchor = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "ViewCameraAnchor");
                Quaternion viewRotation = Quaternion.LookRotation(-anchor.forward, anchor.up);
                Quaternion correction = Quaternion.Inverse(viewRotation);
                Vector3 eye = anchor.position;
                model.transform.SetLocalPositionAndRotation(correction * -eye, correction);
                var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.sharedMaterials = new[] { opaque, glass };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.updateWhenOffscreen = true;
                renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
                var animator = model.GetComponent<Animator>();
                if (!animator) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var player = container.AddComponent<GauntletAnimationPlayer>();
                player.animator = animator; player.meshRenderer = renderer;
                var idle = AssetDatabase.LoadAllAssetsAtPath(Root + "Animations/Gauntlet_Idle.fbx").OfType<AnimationClip>().First(c => c.name == "Idle");
                idle.SampleAnimation(animator.gameObject, 0f);
                var motion = container.AddComponent<GauntletCameraMotion>();
                motion.model = model.transform;
                // This bone contains the final evaluated source Camera pose after
                // its Copy Rotation constraint, rather than guessing driver axes.
                motion.cameraBone = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "ViewCameraMotion");
                motion.referenceBoneRotation = Quaternion.Inverse(model.transform.rotation) * motion.cameraBone.rotation;
                motion.referenceBonePosition = model.transform.InverseTransformPoint(motion.cameraBone.position);
                PrefabUtility.SaveAsPrefabAsset(container, Root + "Prefabs/GauntletViewModel.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(container); }
            EditorUtility.SetDirty(opaque); EditorUtility.SetDirty(glass); EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            CreatePreview();
            Debug.Log("Gauntlet: 2 materials, 4 textures, 13 Generic animation clips and prefab ready.");
        }

        private static void ConfigureModel(ModelImporter m)
        {
            m.globalScale = 1f; m.useFileScale = true;
            m.animationType = ModelImporterAnimationType.Generic;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = false; m.importLights = false;
            m.preserveHierarchy = true; m.optimizeGameObjects = false;
            m.importBlendShapes = false; m.meshCompression = ModelImporterMeshCompression.Off;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.skinWeights = ModelImporterSkinWeights.Custom;
            m.maxBonesPerVertex = 8; m.minBoneWeight = .0001f;
            m.isReadable = false;
        }

        private static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Gauntlet_" + name + ".png");
        private static Material GetMaterial(string name, Shader shader)
        {
            if (!shader) throw new Exception("Shader not found for " + name);
            string path = Root + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            return m;
        }

        [MenuItem("Tools/Gauntlet/Create Preview Scene")]
        public static void CreatePreview()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = SceneManager.GetSceneByPath(Root + "Scenes/GauntletPreview.unity");
            bool created = !preview.IsValid() || !preview.isLoaded;
            if (created) preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            else foreach (var go in preview.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(go);
            SceneManager.SetActiveScene(preview);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.42f,.48f,.56f);
                RenderSettings.ambientEquatorColor = new Color(.20f,.22f,.25f);
                RenderSettings.ambientGroundColor = new Color(.12f,.10f,.08f);
                var cam = new GameObject("Gauntlet Preview Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";cam.nearClipPlane = .015f;cam.farClipPlane = 30f;cam.fieldOfView = 58.71551f;
                cam.clearFlags = CameraClearFlags.SolidColor;cam.backgroundColor = new Color(.055f,.07f,.085f);
                cam.gameObject.AddComponent<AudioListener>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/GauntletViewModel.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.transform.SetParent(cam.transform, false);
                var ui = new GameObject("Animation Preview Controls").AddComponent<GauntletPreviewControls>();
                ui.player = instance.GetComponent<GauntletAnimationPlayer>();
                ui.clips = ReadClips().clips.Select(c => c.name).ToArray();
                AddLight("Key", new Vector3(28f,-35f,0f), new Color(1f,.89f,.72f), 2.2f);
                AddLight("Fill", new Vector3(-15f,130f,0f), new Color(.62f,.78f,1f), 1.2f);
                GauntletCinemachineSetup.Connect(cam, instance);
                EditorSceneManager.SaveScene(preview, Root + "Scenes/GauntletPreview.unity");
            }
            finally
            {
                if (previous.IsValid() && previous.handle != preview.handle) SceneManager.SetActiveScene(previous);
                if (created) EditorSceneManager.CloseScene(preview, true);
            }
        }
        private static void AddLight(string name, Vector3 euler, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();light.type = LightType.Directional;
            light.transform.eulerAngles = euler;light.color = color;light.intensity = intensity;light.shadows = LightShadows.None;
        }

        [MenuItem("Tools/Gauntlet/Attach to Selected Camera")]
        public static void AttachToCamera()
        {
            Camera camera = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Camera>() : null;
            if (!camera) throw new InvalidOperationException("Select the player's Camera first.");
            if (camera.GetComponentInChildren<GauntletAnimationPlayer>(true)) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/GauntletViewModel.prefab"), camera.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Attach Gauntlet");
            go.transform.SetParent(camera.transform, false);
            GauntletCinemachineSetup.Connect(camera, go);
            Undo.RecordObject(camera, "Gauntlet Near Clip");camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, .02f);
            Selection.activeGameObject = go;
        }
    }
}

