using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModernLabTrials.Editor
{
    // Renders one editor preview per generated level without saving any scene.
    public static class ModernLabTrialPreview
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/ModernLabTrials/Scenes/MLT_01_CrystalIntake.unity",
            "Assets/Game/ModernLabTrials/Scenes/MLT_02_CrystalAscent.unity",
            "Assets/Game/ModernLabTrials/Scenes/MLT_03_ResonanceArena.unity",
            "Assets/Game/ModernLabTrials/Scenes/MLT_04_CoreBreach.unity",
        };

        [MenuItem("Tools/Modern Lab Trials/Capture Previews")]
        public static void CaptureMenu()
        {
            Capture();
        }

        public static void CaptureBatch()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Capture()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "ds unity", "screenshots", "modernlab");
            Directory.CreateDirectory(folder);
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                string name = Path.GetFileNameWithoutExtension(path);
                TrialLevel level = Find<TrialLevel>(scene);
                Vector3 spawn = level != null && level.spawn != null ? level.spawn.position : Vector3.zero;

                GameObject cameraObject = new GameObject("PreviewCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 65f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 200f;
                camera.transform.position = spawn + new Vector3(0f, 6.5f, -9f);
                camera.transform.LookAt(spawn + new Vector3(0f, 1.2f, 2f));
                Render(camera, Path.Combine(folder, name + "_room.png"));

                Camera playerCamera = level != null ? level.view : null;
                if (playerCamera != null)
                {
                    playerCamera.fieldOfView = 72f;
                    Render(playerCamera, Path.Combine(folder, name + "_view.png"));
                }

                GameObject topObject = new GameObject("PreviewTop");
                Camera top = topObject.AddComponent<Camera>();
                top.fieldOfView = 60f;
                top.transform.position = spawn + new Vector3(0f, 7f, 0f);
                top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Render(top, Path.Combine(folder, name + "_top.png"));
                UnityEngine.Object.DestroyImmediate(topObject);

                int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
                int floorRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Count(r => r.name.StartsWith("Floor"));
                Debug.Log($"MODERNLAB_PREVIEW_STATS: {name} lights={lights} floorRenderers={floorRenderers} ambient={RenderSettings.ambientLight}");

                UnityEngine.Object.DestroyImmediate(cameraObject);
                Debug.Log("MODERNLAB_PREVIEW: " + name + " -> " + folder);
            }
        }

        private static void Render(Camera camera, string path)
        {
            RenderTexture texture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
