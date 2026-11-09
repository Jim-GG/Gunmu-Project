using UnityEngine;
using UnityEngine.InputSystem;

// Reloading the current room resets all scene objects and the authored player spawn.
[DefaultExecutionOrder(-9000)]
public class RoomRestartController : MonoBehaviour
{
    [SerializeField, InspectorName("启用 R 重置")] private bool enableRestartKey = true;
    [SerializeField, InspectorName("跌落视为死亡")] private bool detectFall = true;
    [SerializeField, Min(2), InspectorName("低于初始位置多少米判定跌落")] private float fallDistance = 15;
    private float initialY;
    private bool restartKeyArmed;
    private void Awake() { initialY = transform.position.y; }
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        // 死亡时时间被冻结、玩法输入被拦截，但重开必须仍然可用。
        bool dead = GameStateManager.State == GameState.Dead;
        if (GameplayGate.IsBlocked && !dead) return;
        if (!dead && detectFall && transform.position.y < initialY - fallDistance)
        {
            GameStateManager.NotifyPlayerDied("你坠出了实验区域。");
            RestartRoom();
            return;
        }
        // A held R must not immediately restart the newly loaded room again.
        if (keyboard != null && !keyboard.rKey.isPressed) restartKeyArmed = true;
        if (enableRestartKey && restartKeyArmed && keyboard != null && keyboard.rKey.wasPressedThisFrame)
        { restartKeyArmed = false; RestartRoom(); }
    }

    private void OnGUI()
    {
        if (GameStateManager.State != GameState.Dead) return;
        ChineseGameUI.EnsureFont();
        if (GUI.Button(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.5f + 10f, 200f, 44f), "重新开始（R）"))
            RestartRoom();
    }
    public void RestartRoom()
    {
        GameStateManager.RestartCurrentRoom();
    }
}
