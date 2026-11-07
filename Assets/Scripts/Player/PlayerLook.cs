using UnityEngine;
using UnityEngine.InputSystem;

// Attach to Player. Assign its CameraPivot child in the Inspector.
public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;

    /// <summary>鼠标灵敏度倍率（<see cref="PlayerZoom"/> 等放大视野时临时下调，默认 1 = 不改变）。</summary>
    public float SensitivityScale { get; set; } = 1f;

    private float pitch;

    private void Start()
    {
        if (cameraPivot == null)
        {
            Debug.LogError("PlayerLook: assign Camera Pivot in the Inspector.", this);
            enabled = false;
            return;
        }

        SetCursorLocked(true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(false);
            return;
        }

        if (Mouse.current == null)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            // Click inside the Game view to resume looking.
            if (Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
            return;
        }

        // Mouse delta is displacement this frame, so do not multiply by deltaTime.
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * (mouseSensitivity * SensitivityScale);

        // Turn the whole player left/right, but only the camera pivot up/down.
        transform.Rotate(Vector3.up, mouseDelta.x, Space.World);
        pitch = Mathf.Clamp(pitch - mouseDelta.y, -pitchLimit, pitchLimit);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetCursorLocked(false);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
