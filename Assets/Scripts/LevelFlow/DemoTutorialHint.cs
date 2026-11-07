using UnityEngine;

public sealed class DemoTutorialHint : MonoBehaviour
{
    [SerializeField, InspectorName("关卡标题")] private string title;
    [SerializeField, TextArea, InspectorName("目标")] private string goal;
    [SerializeField, TextArea, InspectorName("操作提示")] private string instructions;
    [SerializeField, InspectorName("进度")] private DemoTutorialGate progress;

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        ChineseGameUI.EnsureFont();
        float width = Mathf.Min(Screen.width - 32f, 690f);
        Rect panel = new Rect(16f, 14f, width, progress == null ? 122f : 146f);
        GUI.Box(panel, GUIContent.none);
        var heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 19 };
        var text = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
        GUI.Label(new Rect(28f, 20f, width - 24f, 28f), title, heading);
        GUI.Label(new Rect(28f, 50f, width - 24f, 31f), "目标：" + goal, text);
        GUI.Label(new Rect(28f, 79f, width - 24f, 54f), instructions, text);
        if (progress != null)
            GUI.Label(new Rect(28f, 132f, width - 24f, 24f),
                $"进度：{progress.CompletedCount}/{progress.TotalCount}", text);
    }
}
