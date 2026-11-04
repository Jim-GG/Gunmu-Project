using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent level-to-level flow. Owns the black fade, the ordered level list and
// the final "demo complete" hand-off. No coordinates live here.
[DefaultExecutionOrder(-20000)]
public class LevelFlow : MonoBehaviour
{
    public static LevelFlow Instance { get; private set; }
    public static bool IsTransitioning { get; private set; }

    [SerializeField, Min(.05f), InspectorName("黑场淡出用时")] private float fadeOutDuration = .45f;
    [SerializeField, Min(.05f), InspectorName("黑场淡入用时")] private float fadeInDuration = .55f;
    [SerializeField, InspectorName("转场时冻结世界")] private bool freezeWorld = true;

    private static readonly string[] LevelScenes =
    {
        "Level1_Billiards",
        "Level2_Pendulum",
        "Level3_Seesaw",
        "Level4_MomentumRedirect",
        "Level5_ChainReaction",
        "Level6_BouncyWall",
    };

    private float fadeAlpha;
    private bool transitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null || !Application.isPlaying) return;
        GameObject flow = new GameObject("等级流程 LevelFlow");
        flow.AddComponent<LevelFlow>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        IsTransitioning = false;
    }

    public static bool IsLevelScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && System.Array.IndexOf(LevelScenes, sceneName) >= 0;
    }

    public static int LevelNumber(string sceneName)
    {
        int index = System.Array.IndexOf(LevelScenes, sceneName);
        return index >= 0 ? index + 1 : 0;
    }

    public static string NextScene(string sceneName)
    {
        int index = System.Array.IndexOf(LevelScenes, sceneName);
        return index >= 0 && index + 1 < LevelScenes.Length ? LevelScenes[index + 1] : "";
    }

    public static bool IsLastLevel(string sceneName)
    {
        return sceneName == LevelScenes[LevelScenes.Length - 1];
    }

    // Call this from a walk-in trigger. finalLevel shows the demo-complete screen
    // instead of loading another scene.
    public static void Go(string nextSceneName, bool finalLevel)
    {
        if (Instance == null)
        {
            if (finalLevel)
            {
                if (DemoSession.Instance != null) DemoSession.Instance.CompleteDemo();
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = .02f;
                SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
            }
            return;
        }
        Instance.BeginTransition(nextSceneName, finalLevel);
    }

    private void BeginTransition(string nextSceneName, bool finalLevel)
    {
        if (transitioning) return;
        StartCoroutine(TransitionRoutine(nextSceneName, finalLevel));
    }

    private IEnumerator TransitionRoutine(string nextSceneName, bool finalLevel)
    {
        transitioning = true;
        IsTransitioning = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (freezeWorld) Time.timeScale = 0f;

        fadeAlpha = 0f;
        float time = 0f;
        while (time < fadeOutDuration)
        {
            time += Time.unscaledDeltaTime;
            fadeAlpha = Mathf.Clamp01(time / fadeOutDuration);
            yield return null;
        }
        fadeAlpha = 1f;
        yield return new WaitForSecondsRealtime(.05f);

        if (finalLevel)
        {
            if (DemoSession.Instance != null) DemoSession.Instance.CompleteDemo();
            yield return new WaitForSecondsRealtime(.12f);
            yield return FadeInRoutine();
            yield break;
        }

        if (string.IsNullOrEmpty(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError("无法进入下一关，请在构建场景列表中勾选：" + nextSceneName, this);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = .02f;
            fadeAlpha = 0f;
            transitioning = false;
            IsTransitioning = false;
            yield break;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = .02f;
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        // LevelFlow is DontDestroyOnLoad, so this coroutine survives the scene swap.
        yield return null;
        yield return FadeInRoutine();
    }

    private IEnumerator FadeInRoutine()
    {
        float time = 0f;
        while (time < fadeInDuration)
        {
            time += Time.unscaledDeltaTime;
            fadeAlpha = 1f - Mathf.Clamp01(time / fadeInDuration);
            yield return null;
        }
        fadeAlpha = 0f;
        transitioning = false;
        IsTransitioning = false;
    }

    private void OnGUI()
    {
        if (fadeAlpha <= .001f) return;
        int oldDepth = GUI.depth;
        Color oldColor = GUI.color;
        GUI.depth = -20000;
        GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }
}
