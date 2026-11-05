using UnityEngine;
using UnityEngine.InputSystem;
namespace Resonator.AI
{
    /// <summary>共鸣者测试场景用的调试 HUD（正常关卡不需要）。</summary>
    public sealed class ResonatorArenaHUD : MonoBehaviour
    {
        public ResonatorPlayerBridge player;
        public ResonatorEnemy[] enemies = new ResonatorEnemy[0];
        void Update() { if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame && player) player.EmitTestNoise(); }
        void OnGUI()
        {
            ChineseGameUI.EnsureFont();
            int rows = enemies != null ? enemies.Length : 0;
            GUI.Box(new Rect(12, 12, 420, 70 + rows * 25), "共鸣者调试｜N：发出噪音｜Esc：释放鼠标");
            GUI.Label(new Rect(24, 38, 395, 25), "左键：冲拳 / 蓄力　右键：格挡　R：恢复生命");
            for (int i = 0; i < rows; i++)
                if (enemies[i])
                    GUI.Label(new Rect(24, 66 + i * 25, 395, 24),
                        $"{enemies[i].initialActivity}：{enemies[i].State}  帧 {enemies[i].CurrentFrame:0}  生命 {enemies[i].Health:0}");
            PlayerCombatController combat = PlayerCombat();
            if (combat != null) GUI.Label(new Rect(24, 63 + rows * 25, 390, 25), $"玩家生命 {combat.Health}");
        }
        void LateUpdate()
        {
            if (Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;
            PlayerCombatController combat = PlayerCombat();
            if (combat != null) combat.RestoreHealth();
        }
        private PlayerCombatController PlayerCombat() => player && player.actor ? player.actor.combat : null;
    }
}
