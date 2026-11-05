using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gauntlet.Editor
{
    public static class GauntletCinemachineSetup
    {
        [MenuItem("Tools/Gauntlet/Connect Preview Camera with Cinemachine")]
        public static void ConfigureCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(c => c.name == "Gauntlet Preview Camera");
            var player = camera.GetComponentInChildren<GauntletAnimationPlayer>(true);
            if (!player) player = UnityEngine.Object.FindObjectsByType<GauntletAnimationPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(p => p.gameObject.scene == camera.gameObject.scene);
            Directory.CreateDirectory("Temp/GauntletCameraFix");
            const string temporaryBackup = "Assets/Gauntlet/Scenes/CameraConstraint_Before.unity";
            EditorSceneManager.SaveScene(camera.gameObject.scene, temporaryBackup, true);
            File.Copy(temporaryBackup, "Temp/GauntletCameraFix/BeforeCameraConstraint.unity", true);
            AssetDatabase.DeleteAsset(temporaryBackup);
            Connect(camera, player.gameObject);
            EditorSceneManager.SaveScene(camera.gameObject.scene);
            Selection.activeGameObject = camera.gameObject;
        }

        public static void Connect(Camera camera, GameObject viewModel)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect gauntlet camera constraint");
            var player = viewModel.GetComponent<GauntletAnimationPlayer>();
            var animator = player.animator;
            var target = animator.GetComponentsInChildren<Transform>(true).Single(t => t.name == "camera_target");

            foreach (var old in viewModel.GetComponentsInChildren<GauntletCameraMotion>(true))
            {
                Undo.RecordObject(old, "Disable previous camera driver");
                old.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(old);
            }
            // The animation source must be independent of the camera it drives.
            if (viewModel.transform.IsChildOf(camera.transform))
                Undo.SetTransformParent(viewModel.transform, camera.transform.parent, "Detach animation rig from output camera");
            PrefabUtility.RecordPrefabInstancePropertyModifications(viewModel.transform);

            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            var proxy = target.Find("Camera Rotation Offset");
            if (!proxy)
            {
                var transforms = animator.GetComponentsInChildren<Transform>(true);
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var scales = transforms.Select(t => t.localScale).ToArray();
                var idle = animator.runtimeAnimatorController.animationClips.First(c => c.name == "Idle");
                idle.SampleAnimation(animator.gameObject, 0f);
                var offset = new GameObject("Camera Rotation Offset");
                Undo.RegisterCreatedObjectUndo(offset, "Create camera rotation offset");
                proxy = offset.transform;
                proxy.SetParent(target, false);
                proxy.rotation = cameraRotation;
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
                    transforms[i].localScale = scales[i];
                }
            }

            var brain = camera.GetComponent<CinemachineBrain>();
            if (!brain) brain = Undo.AddComponent<CinemachineBrain>(camera.gameObject);
            Undo.RecordObject(brain, "Configure output camera brain");
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            var existing = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Gauntlet Animation Camera" && c.gameObject.scene == camera.gameObject.scene);
            if (!existing)
            {
                var go = new GameObject("Gauntlet Animation Camera");
                Undo.RegisterCreatedObjectUndo(go, "Create Cinemachine animation camera");
                go.transform.SetParent(camera.transform.parent, true);
                go.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                existing = Undo.AddComponent<CinemachineCamera>(go);
            }
            Undo.RecordObject(existing, "Set animated follow target");
            existing.Priority = 100;
            existing.Lens = LensSettings.FromCamera(camera);
            existing.Follow = proxy;
            var rotation = existing.GetComponent<CinemachineRotateWithFollowTarget>();
            if (!rotation) rotation = Undo.AddComponent<CinemachineRotateWithFollowTarget>(existing.gameObject);
            Undo.RecordObject(rotation, "Match animated target rotation");
            rotation.Damping = 0f;
            EditorUtility.SetDirty(brain);
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(rotation);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
