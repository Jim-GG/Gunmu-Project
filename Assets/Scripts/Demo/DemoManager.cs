using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum DemoGameState
{
    Playing,
    Won,
    Lost
}

/// <summary>Owns the demo session flow and the win condition.</summary>
[DefaultExecutionOrder(-50)]
public class DemoManager : MonoBehaviour
{
    public static DemoManager Instance { get; private set; }

    public DemoGameState State { get; private set; } = DemoGameState.Playing;
    public bool IsPlaying => State == DemoGameState.Playing;
    public PlayerCombat Player { get; private set; }
    public CrystalEnemy Enemy { get; private set; }
    public DemoHUD Hud { get; private set; }
    public DemoAudio Audio { get; private set; }

    private PlayerMovement playerMovement;
    private PlayerLook playerLook;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Audio = gameObject.GetComponent<DemoAudio>() ?? gameObject.AddComponent<DemoAudio>();
        Hud = gameObject.GetComponent<DemoHUD>() ?? gameObject.AddComponent<DemoHUD>();
    }

    private void Start()
    {
        Player = FindFirstObjectByType<PlayerCombat>();
        Enemy = FindFirstObjectByType<CrystalEnemy>();

        if (Player != null)
        {
            playerMovement = Player.GetComponent<PlayerMovement>();
            playerLook = Player.GetComponent<PlayerLook>();
        }

        Hud.Initialize(Player, Enemy);
        Audio.PlayAmbient();
    }

    private void Update()
    {
        if (State != DemoGameState.Playing)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                Restart();
            }
            return;
        }
    }

    public void NotifyEnemyDefeated(CrystalEnemy defeated)
    {
        if (!IsPlaying)
        {
            return;
        }

        StartCoroutine(EndAfterDelay(true, 1.15f));
    }

    public void NotifyPlayerDied()
    {
        if (!IsPlaying)
        {
            return;
        }

        StartCoroutine(EndAfterDelay(false, 0.65f));
    }

    private IEnumerator EndAfterDelay(bool won, float delay)
    {
        State = won ? DemoGameState.Won : DemoGameState.Lost;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }
        if (playerLook != null)
        {
            playerLook.enabled = false;
        }

        if (Enemy != null)
        {
            Enemy.SetCombatActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return new WaitForSeconds(delay);

        if (won)
        {
            Hud.ShowWin(Player != null ? Player.BlockCount : 0,
                        Player != null ? Player.ShockCount : 0,
                        Player != null ? Mathf.CeilToInt(Player.Health) : 0);
            Audio.PlayWin();
        }
        else
        {
            Hud.ShowDeath();
        }
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Scene active = SceneManager.GetActiveScene();
        if (active.buildIndex >= 0)
        {
            SceneManager.LoadScene(active.buildIndex);
        }
        else
        {
            SceneManager.LoadScene(active.name);
        }
    }
}
