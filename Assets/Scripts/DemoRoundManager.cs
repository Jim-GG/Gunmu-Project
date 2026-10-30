using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Single-enemy round for the core-mechanic prototype, not the later 3-enemy encounter.
public class DemoRoundManager : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private DemoEnemyHealth enemy;

    private enum RoundState { Playing, Won, Lost }
    private RoundState state;
    private bool ready;
    private bool restarting;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;

    private void Start()
    {
        if (player == null || enemy == null)
        {
            Debug.LogError("DemoRoundManager: assign Player and Enemy in the Inspector.", this);
            enabled = false;
            return;
        }
        ready = true;
    }

    private void Update()
    {
        if (!ready || restarting)
            return;
        if (state == RoundState.Playing)
        {
            // Resolve simultaneous defeat consistently as a loss.
            if (player.IsDefeated)
                FinishRound(RoundState.Lost);
            else if (enemy.IsDefeated)
                FinishRound(RoundState.Won);
        }
        else if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartRound();
        }
    }

    private void FinishRound(RoundState result)
    {
        state = result;
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        PlayerLook look = player.GetComponent<PlayerLook>();
        PlayerGauntlet gauntlet = player.GetComponent<PlayerGauntlet>();
        if (movement != null) movement.enabled = false;
        if (look != null) look.enabled = false;
        if (gauntlet != null) gauntlet.enabled = false;

        DemoEnemyAttack attack = enemy.GetComponent<DemoEnemyAttack>();
        DemoEnemyKnockback motion = enemy.GetComponent<DemoEnemyKnockback>();
        if (attack != null) attack.enabled = false;
        if (motion != null) motion.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartRound()
    {
        if (!ready || state == RoundState.Playing || restarting)
            return;
        restarting = true;
        Time.timeScale = 1f;
        Scene scene = gameObject.scene;
#if UNITY_EDITOR
        // The saved scene can be tested before configuring the final build scene list.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
#endif
    }

    private void OnGUI()
    {
        if (!ready || state == RoundState.Playing)
            return;
        GUI.depth = -100;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }
        float width = Mathf.Min(560f, Screen.width - 20f);
        Rect panel = new Rect((Screen.width - width) / 2f,
            (Screen.height - 250f) / 2f, width, 250f);
        GUI.Box(panel, "");
        GUI.Label(new Rect(panel.x, panel.y + 20f, width, 50f),
            state == RoundState.Won ? "VICTORY" : "DEFEATED", titleStyle);
        GUI.Label(new Rect(panel.x + 15f, panel.y + 80f, width - 30f, 70f),
            state == RoundState.Won ? "Enemy defeated!\nTry guarding and releasing a stored impact again."
                : "Watch the attack warning.\nFace the enemy and hold RMB to guard.", bodyStyle);
        if (GUI.Button(new Rect(panel.center.x - 120f, panel.y + 175f, 240f, 45f),
            restarting ? "Restarting..." : "Retry (R)"))
            RestartRound();
    }
}
