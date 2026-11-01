using UnityEngine;

// Shared IMGUI font for prototype Chinese text. Applies only to the current GUI skin.
public static class ChineseGameUI
{
    private static Font font;
    private static bool initialized;
    public static void EnsureFont()
    {
        if (!initialized)
        {
            initialized = true;
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 18);
        }
        if (font != null) GUI.skin.font = font;
    }
}
