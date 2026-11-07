using UnityEngine;

public sealed class DemoTutorialHint : MonoBehaviour
{
    [SerializeField, InspectorName("关卡标题")] private string title;
    [SerializeField, TextArea, InspectorName("目标")] private string goal;
    [SerializeField, TextArea, InspectorName("操作提示")] private string instructions;
    [SerializeField, InspectorName("进度")] private DemoTutorialGate progress;
    [SerializeField, InspectorName("面板边距（左上角）")] private Vector2 margin = new Vector2(32f, 26f);

    /// <summary>面板高度（其它提示用它把自己排到下方）。</summary>
    public float PanelHeight => progress == null ? 122f : 146f;
    /// <summary>面板边距。</summary>
    public Vector2 Margin => margin;

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        ChineseGameUI.EnsureFont();
        float width = Mathf.Min(Screen.width - margin.x - 16f, 690f);
        Rect panel = new Rect(margin.x, margin.y, width, PanelHeight);
        GUI.Box(panel, GUIContent.none);
        var heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 19 };
        var text = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
        GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, width - 24f, 28f), title, heading);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 36f, width - 24f, 31f), "目标：" + goal, text);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 65f, width - 24f, 54f), instructions, text);
        if (progress != null)
            GUI.Label(new Rect(panel.x + 12f, panel.y + 118f, width - 24f, 24f),
                $"进度：{progress.CompletedCount}/{progress.TotalCount}", text);
    }
}
