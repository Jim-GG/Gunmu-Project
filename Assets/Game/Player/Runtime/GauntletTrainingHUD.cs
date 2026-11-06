using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstPerson
{
    // Preview instrumentation; gameplay communicates through combat/vitals events.
    public sealed class GauntletTrainingHUD : MonoBehaviour
    {
        public GauntletCombat combat;
        public FirstPersonVitals vitals;
        public ParryTrainingEnemy enemy;
        private GUIStyle label;
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && vitals) vitals.Restore();
        }
        private void OnGUI()
        {
            if (!combat) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Box(new Rect(16,16,440,190), GUIContent.none);
            string stacks = new string('|', combat.BlockEnergy).PadRight(3, '.');
            string text = "RMB: guard  |  LMB: charge / release\n" +
                "Guard energy: [" + stacks + "] " + combat.BlockEnergy + "/3\n" +
                "State: " + combat.State + "   Charge frame: " + combat.policy.Frame(combat.ChargeSeconds) + "\n" +
                "HP: " + (vitals ? vitals.Health.ToString("0") : "--") + "   R: restore training HP\n" +
                (enemy ? "Enemy: " + enemy.State + " / " + enemy.LastResult : "") + "\nWASD / Shift / Space   Esc: unlock cursor";
            GUI.Label(new Rect(28,24,416,174), text, label);
            GUI.Label(new Rect(Screen.width*.5f-6, Screen.height*.5f-12,20,24), "+", label);
        }
    }
}
