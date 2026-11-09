using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ModernLabTrials.Editor
{
    // Builds a development player used by ModernLabSelfTest.
    public static class ModernLabTrialBuildTest
    {
        private const string OutputFolder = "builds/ModernLabTrials";
        private const string OutputExe = "builds/ModernLabTrials/ModernLabTrialsTest.exe";

        [MenuItem("Tools/Modern Lab Trials/Build Test Player")]
        public static void BuildMenu()
        {
            Build();
        }

        public static void BuildBatch()
        {
            try { BuildResult result = Build(); EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(2); }
        }

        private static BuildResult Build()
        {
            string root = Directory.GetCurrentDirectory();
            string folder = Path.Combine(root, OutputFolder);
            string exe = Path.Combine(root, OutputExe);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            string[] scenes = new[]
            {
                "Assets/Game/ModernLabTrials/Scenes/MLT_01_ImpactCalibration.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_02_CrosswindGallery.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_03_VerticalShaft.unity",
                "Assets/Game/ModernLabTrials/Scenes/MLT_04_SynthesisCore.unity",
            };
            AssetDatabase.Refresh();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"MODERNLAB_TEST_BUILD: {report.summary.result} errors={report.summary.totalErrors} " +
                      $"size={report.summary.totalSize / (1024 * 1024)}MB");
            return report.summary.result;
        }
    }
}
