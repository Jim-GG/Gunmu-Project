using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        if (DemoSession.IsGameplayBlocked) return;
        if (detectFall && transform.position.y < initialY - fallDistance)
        {
            if (DemoSession.Instance != null) DemoSession.Instance.ShowDeath("你坠出了实验区域。");
            else RestartRoom();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        // A held R must not immediately restart the newly loaded room again.
        if (keyboard != null && !keyboard.rKey.isPressed) restartKeyArmed = true;
        if (enableRestartKey && restartKeyArmed && keyboard != null && keyboard.rKey.wasPressedThisFrame)
        { restartKeyArmed = false; RestartRoom(); }
    }
    public void RestartRoom()
    {
        if (DemoSession.Instance != null && DemoSession.Instance.IsDemoScene) DemoSession.Instance.RestartRoom();
        else
        {
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
