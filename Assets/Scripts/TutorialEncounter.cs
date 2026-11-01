using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TutorialEncounter : MonoBehaviour
{
    [SerializeField] private PlayerCombatController player;
    [SerializeField] private PlayerGauntletEquipment equipment;
    [SerializeField] private TutorialEnemy enemyTemplate;
    [SerializeField] private Transform[] spawnPoints = new Transform[3];
    [SerializeField] private GameObject exitDoor;
    [SerializeField] private UnityEvent onCleared = new UnityEvent();
    private TutorialEnemy current;
    private int spawned;
    private float nextSpawn;
    private bool ready;
    private bool failed;
    public bool IsCleared { get; private set; }

    private void Start()
    {
        var missing = new System.Collections.Generic.List<string>();
        if (player == null) missing.Add("Player（玩家战斗组件）");
        if (equipment == null) missing.Add("Equipment（玩家拳套装备组件）");
        if (enemyTemplate == null) missing.Add("Enemy Template（敌人模板的 TutorialEnemy 组件）");
        if (spawnPoints == null || spawnPoints.Length != 3) missing.Add("Spawn Points 数量必须为 3");
        else for (int i = 0; i < spawnPoints.Length; i++)
            if (spawnPoints[i] == null) missing.Add($"Spawn Points 的 Element {i}（出生点）");
        if (missing.Count > 0)
        {
            Debug.LogError("教学战斗管理器缺少配置：" + string.Join("、", missing)
                + "。请停止运行后拖入引用，并保存场景。", this);
            return;
        }
        if (enemyTemplate.gameObject.scene.IsValid()) enemyTemplate.gameObject.SetActive(false);
        if (exitDoor != null) exitDoor.SetActive(true);
        ready = true;
    }

    private void Update()
    {
        if (DemoSession.IsGameplayBlocked) return;
        if (!ready || IsCleared) return;
        if (player.IsDefeated)
        {
            if (DemoSession.Instance != null && DemoSession.Instance.IsDemoScene)
            { DemoSession.Instance.ShowDeath(); return; }
            if (!failed)
            {
                failed = true;
                player.GetComponent<PlayerMovement>().enabled = false;
                player.GetComponent<PlayerLook>().enabled = false;
                player.GetComponent<PlayerChargedPunch>().enabled = false;
                player.GetComponent<PlayerInteractor>().enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
            return;
        }
        if (!equipment.HasGauntlet) return;
        if (current != null && current.IsDefeated)
        {
            current = null;
            nextSpawn = Time.time + 1.5f;
            if (spawned == 3)
            {
                IsCleared = true;
                if (exitDoor != null) exitDoor.SetActive(false);
                onCleared.Invoke();
                return;
            }
        }
        if (current == null && spawned < 3 && Time.time >= nextSpawn)
        {
            Transform point = spawnPoints[spawned++];
            current = Instantiate(enemyTemplate, point.position + Vector3.up, point.rotation);
            current.name = "TutorialEnemy_" + spawned;
            current.Initialize(player);
            current.gameObject.SetActive(true);
        }
    }

    private void Restart()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(gameObject.scene.buildIndex);
#endif
    }

    private void OnGUI()
    {
        if (DemoSession.IsGameplayBlocked) return;
        ChineseGameUI.EnsureFont();
        if (!ready) return;
        GUI.Label(new Rect(15f, 18f, Screen.width - 30f, 50f), failed ? "战斗失败，按 R 重新开始"
            : IsCleared ? "教学战斗完成！出口已解锁。"
            : !equipment.HasGauntlet ? "先完成终端校准，再按 E 拾取拳套。"
            : $"教学敌人：{spawned}/3｜右键防御攒充能，左键蓄力反击。");
        if (failed && GUI.Button(new Rect(Screen.width / 2f - 100f, Screen.height / 2f, 200f, 50f), "重新开始（R）")) Restart();
    }
}
