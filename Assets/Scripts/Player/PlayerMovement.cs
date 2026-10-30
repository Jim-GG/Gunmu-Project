using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the same Player object as CharacterController and PlayerLook.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField, Min(0.1f)] private float gravity = 20f;

    private CharacterController controller;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        verticalSpeed = 0f;
    }

    private void Update()
    {
        if (!controller.enabled)
            return;

        bool grounded = controller.isGrounded;
        if (grounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        // Releasing the cursor pauses keyboard movement; gravity keeps working.
        if (keyboard != null && Cursor.lockState == CursorLockMode.Locked)
        {
            input.x = (keyboard.dKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed ? 1f : 0f);

            // Limit diagonal input so W+D is not faster than W alone.
            input = Vector2.ClampMagnitude(input, 1f);

            if (grounded && keyboard.spaceKey.wasPressedThisFrame)
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        Vector3 horizontalVelocity =
            (transform.right * input.x + transform.forward * input.y) * moveSpeed;

        verticalSpeed -= gravity * Time.deltaTime;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalSpeed;

        // Move takes displacement, so convert speed into this frame's distance.
        CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);

        // Stop rising after hitting a ceiling, and stop falling after landing.
        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            verticalSpeed = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            verticalSpeed = -2f;
    }
}
