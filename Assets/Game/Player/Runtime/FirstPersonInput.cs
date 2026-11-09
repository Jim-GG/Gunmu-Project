using UnityEngine;
using UnityEngine.InputSystem;
namespace FirstPerson
{
    [DefaultExecutionOrder(-100)]
    public sealed class FirstPersonInput : MonoBehaviour
    {
        public GauntletCombat combat;
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Sprint { get; private set; }
        public bool JumpPressed { get; private set; }
        private void OnEnable() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        private void Update()
        {
            var k = Keyboard.current; var m = Mouse.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) Unlock();
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Move = Look = Vector2.zero; Sprint = JumpPressed = false;
                if (m != null && m.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                return;
            }
            Move = k == null ? Vector2.zero : Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)),1);
            Look = m == null ? Vector2.zero : m.delta.ReadValue();
            Sprint = k != null && k.leftShiftKey.isPressed;
            JumpPressed = k != null && k.spaceKey.wasPressedThisFrame;
            if (combat && m != null)
            {
                if (m.rightButton.wasPressedThisFrame) combat.BeginBlock();
                if (m.rightButton.wasReleasedThisFrame) combat.EndBlock();
                if (m.leftButton.wasPressedThisFrame) combat.Press();
                if (m.leftButton.wasReleasedThisFrame) combat.Release();
            }
        }
        private void Unlock() { Move = Look = Vector2.zero; Sprint = JumpPressed = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; if(combat)combat.Cancel(); }
        private void OnApplicationFocus(bool focus) { if(!focus)Unlock(); }
        private void OnDisable() => Unlock();
    }
}
