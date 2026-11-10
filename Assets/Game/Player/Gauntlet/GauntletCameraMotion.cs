using UnityEngine;

namespace Gauntlet
{
    /// <summary>Follows the baked final Blender camera pose, including its source constraints.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GauntletCameraMotion : MonoBehaviour
    {
        public Transform model;
        public Transform cameraBone;
        public Quaternion referenceBoneRotation = Quaternion.identity;
        public Vector3 referenceBonePosition;
        public bool applyCameraMotion = true;
        private Camera viewCamera;
        private Quaternion cameraRestRotation, modelRestRotation;
        private Vector3 modelRestPosition, cameraRestPosition;
        private bool initialized;
        private bool rotationOnly;

        private void OnEnable()
        {
            viewCamera = GetComponentInParent<Camera>();
            if (!viewCamera || !model || !cameraBone) return;
            cameraRestRotation = viewCamera.transform.localRotation;
            cameraRestPosition = viewCamera.transform.localPosition;
            CaptureModelRestPose();
        }

        public void PrepareExternalDrive(Transform animatedTarget)
        {
            enabled = false;
            if (!model || !animatedTarget) return;
            cameraBone = animatedTarget;
            referenceBoneRotation = Quaternion.Inverse(model.rotation) * cameraBone.rotation;
            rotationOnly = true;
            CaptureModelRestPose();
        }

        private void CaptureModelRestPose()
        {
            modelRestRotation = model.localRotation;
            modelRestPosition = model.localPosition;
            initialized = true;
        }

        private void LateUpdate()
        {
            if (viewCamera) ApplyPose(viewCamera.transform, cameraRestPosition, cameraRestRotation, 1f);
        }

        public bool ApplyPose(Transform cameraTransform, Vector3 basePosition, Quaternion baseRotation, float strength)
        {
            if (!initialized || !gameObject.activeInHierarchy) return false;
            Quaternion boneRotation = Quaternion.Inverse(model.rotation) * cameraBone.rotation;
            Vector3 bonePosition = model.InverseTransformPoint(cameraBone.position);
            Quaternion delta = modelRestRotation * boneRotation * Quaternion.Inverse(referenceBoneRotation) * Quaternion.Inverse(modelRestRotation);
            Vector3 referenceEye = modelRestPosition + modelRestRotation * referenceBonePosition;
            Vector3 animatedEye = modelRestPosition + modelRestRotation * bonePosition;
            Vector3 displacement = animatedEye - delta * referenceEye;
            float weight = applyCameraMotion ? Mathf.Clamp01(strength) : 0f;
            delta = Quaternion.Slerp(Quaternion.identity, delta, weight);
            displacement = rotationOnly ? Vector3.zero : displacement * weight;
            cameraTransform.SetLocalPositionAndRotation(basePosition + baseRotation * displacement, baseRotation * delta);
            // The source arms are fixed in the view's unshaken frame. Counter-rotate
            // the viewmodel so camera motion does not get applied to the arms twice.
            model.localRotation = Quaternion.Inverse(delta) * modelRestRotation;
            model.localPosition = Quaternion.Inverse(delta) * (modelRestPosition - displacement);
            return true;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            if (viewCamera) viewCamera.transform.localRotation = cameraRestRotation;
            if (viewCamera) viewCamera.transform.localPosition = cameraRestPosition;
            if (model) model.SetLocalPositionAndRotation(modelRestPosition, modelRestRotation);
            initialized = false;
        }
    }
}
