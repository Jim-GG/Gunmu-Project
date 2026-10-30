using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The lab slice extends the tested combat room without changing its original scene.
public class LabDemoFlow : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private CalibrationTerminal terminal;
    [SerializeField] private GauntletPickup pickup;
    [SerializeField] private DemoEnemyHealth enemy;
    [SerializeField] private GameObject exitDoor;
    private enum Stage { Calibration, Pickup, Combat, Exit, Won, Lost }
    private Stage stage;
    private bool ready;
    private bool restarting;

    public void Configure(PlayerHealth p, CalibrationTerminal t, GauntletPickup g,
        DemoEnemyHealth e, GameObject door)
    {
        player = p; terminal = t; pickup = g; enemy = e; exitDoor = door;
    }

    private void Awake()
    {
        if (player == null || terminal == null || pickup == null || enemy == null || exitDoor == null)
        {
            Debug.LogError("LabDemoFlow: run Tools > Null Boundary > Configure Lab Demo.", this);
            enabled = false;
            return;
        }
        PlayerGauntlet weapon = player.GetComponent<PlayerGauntlet>();
        if (weapon != null) weapon.enabled = false;
        enemy.gameObject.SetActive(false);
        pickup.gameObject.SetActive(true);
        exitDoor.SetActive(true);
        stage = Stage.Calibration;
        ready = true;
    }

    private void Update()
    {
        if (!ready || restarting) return;
        if (stage == Stage.Won || stage == Stage.Lost)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
            return;
        }
        if (player.IsDefeated || player.transform.position.y < -15f)
        {
            End(false);
            return;
        }
        if (stage == Stage.Calibration && terminal.IsSolved) stage = Stage.Pickup;
        if (stage == Stage.Combat && enemy.IsDefeated)
        {
            stage = Stage.Exit;
            exitDoor.SetActive(false);
            player.GetComponent<PlayerInteractor>().ShowFeedback("Exit unlocked - walk through the opening!", 4f);
        }
    }

    public bool EquipGauntlet()
    {
        // Check the actual solved flag so an interaction cannot race the flow Update.
        if (!ready || !terminal.IsSolved || (stage != Stage.Calibration && stage != Stage.Pickup))
            return false;
        PlayerGauntlet weapon = player.GetComponent<PlayerGauntlet>();
        if (weapon == null) return false;
        weapon.enabled = true;
        stage = Stage.Combat;
        enemy.gameObject.SetActive(true);
        player.GetComponent<PlayerInteractor>().ShowFeedback("Gauntlet equipped! Hold RMB to guard.", 4f);
        return true;
    }

    public void TryFinish(PlayerHealth visitor)
    {
        if (ready && stage == Stage.Exit && visitor == player) End(true);
    }

    private void End(bool won)
    {
        stage = won ? Stage.Won : Stage.Lost;
        PlayerMovement move = player.GetComponent<PlayerMovement>();
        PlayerLook look = player.GetComponent<PlayerLook>();
        PlayerGauntlet weapon = player.GetComponent<PlayerGauntlet>();
        PlayerInteractor interact = player.GetComponent<PlayerInteractor>();
        if (move != null) move.enabled = false;
        if (look != null) look.enabled = false;
        if (weapon != null) weapon.enabled = false;
        if (interact != null) interact.enabled = false;
        DemoEnemyAttack attack = enemy.GetComponent<DemoEnemyAttack>();
        DemoEnemyKnockback motion = enemy.GetComponent<DemoEnemyKnockback>();
        if (attack != null) attack.enabled = false;
        if (motion != null) motion.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Restart()
    {
        if (restarting) return;
        restarting = true;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            gameObject.scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(gameObject.scene.buildIndex, LoadSceneMode.Single);
#endif
    }

    private void OnGUI()
    {
        if (!ready) return;
        GUI.depth = -90;
        string objective = stage == Stage.Calibration ? "1. Find the terminal and calibrate it [E]"
            : stage == Stage.Pickup ? "2. Pick up the gauntlet from the rack [E]"
            : stage == Stage.Combat ? "3. Guard, store impact and defeat the enemy"
            : stage == Stage.Exit ? "4. Exit unlocked - walk through the far opening" : "";
        GUI.Label(new Rect(15f, 90f, Screen.width - 30f, 35f), objective);
        if (stage != Stage.Won && stage != Stage.Lost) return;
        float width = Mathf.Min(540f, Screen.width - 20f);
        Rect box = new Rect((Screen.width - width) / 2f, (Screen.height - 220f) / 2f, width, 220f);
        GUI.Box(box, "");
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(box.x, box.y + 30f, width, 60f),
            stage == Stage.Won ? "LAB DEMO COMPLETE" : "DEFEATED", title);
        if (GUI.Button(new Rect(box.center.x - 120f, box.y + 120f, 240f, 50f), "Restart demo (R)")) Restart();
    }
}
