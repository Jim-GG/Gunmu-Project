using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RunModernResonatorRepairOnce
{
    private const string Key = "ModernResonatorRepair.20261110.v1";
    static RunModernResonatorRepairOnce() { EditorApplication.update += Run; }
    private static void Run()
    {
        if (SessionState.GetBool(Key, false)) { EditorApplication.update -= Run; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/Modern_01_DroneEscape.unity") return;
        EditorApplication.update -= Run;
        try
        {
            ModernFacilityProgressionBuilder.RepairResonatorEncounter();
            SessionState.SetBool(Key, true);
        }
        catch (System.Exception exception) { Debug.LogException(exception); }
    }
}
