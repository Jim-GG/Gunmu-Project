using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
public class TutorialExitZone : MonoBehaviour
{
    [SerializeField] private TutorialEncounter encounter;
    [SerializeField] private PlayerCombatController player;
    [SerializeField] private string nextSceneName = "Level1_Billiards";
    private bool loading;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (encounter == null || player == null)
        {
            Debug.LogError("教学出口缺少引用：请把教学管理器拖入 Encounter，把玩家拖入 Player。", this);
            enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GameplayGate.IsBlocked || !enabled || loading || !encounter.IsCleared || player.IsDefeated) return;
        if (other.GetComponentInParent<PlayerCombatController>() != player) return;
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError("无法进入下一关：请在构建配置的场景列表中添加并勾选场景“"
                + nextSceneName + "”。", this);
            return;
        }
        loading = true;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}
