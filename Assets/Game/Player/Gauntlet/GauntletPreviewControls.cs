using UnityEngine;
using UnityEngine.InputSystem;

namespace Gauntlet
{
    public sealed class GauntletPreviewControls : MonoBehaviour
    {
        public GauntletAnimationPlayer player;
        public string[] clips;
        private Vector2 scroll;
        private int punch;

        private void Update()
        {
            if (!player) return;
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.digit1Key.wasPressedThisFrame) player.PlayClip("punch1");
            if (k.digit2Key.wasPressedThisFrame) player.PlayClip("punch2");
            if (k.digit3Key.wasPressedThisFrame) player.PlayClip("punch3");
            if (k.digit4Key.wasPressedThisFrame) player.PlayClip("punch4");
            if (k.cKey.wasPressedThisFrame) player.PlayClip("charge");
            if (k.iKey.wasPressedThisFrame) player.PlayClip("Idle");
            if (k.pKey.wasPressedThisFrame) player.PlayClip("Parry-Start");
            if (k.hKey.wasPressedThisFrame) player.PlayClip("Parry-Hit");
            if (k.kKey.wasPressedThisFrame) player.PlayClip("Parry-Keep");
            if (k.oKey.wasPressedThisFrame) player.PlayClip("Parry-Holster");
            if (k.spaceKey.wasPressedThisFrame) player.PlayClip("punch" + (punch++ % 4 + 1));
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 250, Screen.height - 40), GUI.skin.box);
            GUILayout.Label("GAUNTLET / ANIMATION PREVIEW");
            GUILayout.Label("1-4: Punch    C: Charge    I: Idle\nP / K / H / O: Parry sequence");
            scroll = GUILayout.BeginScrollView(scroll);
            if (clips != null)
                foreach (string clip in clips)
                    if (GUILayout.Button(clip, GUILayout.Height(28))) player.PlayClip(clip);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
