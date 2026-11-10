using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad] public static class CrystalReleaseSmoke
    {
        const string Key = "CrystalReleaseSmoke";
        static int phase;
        static double deadline;
        static readonly List<string> checks = new List<string>();
        static CrystalReleaseSmoke() { EditorApplication.update += Poll; }
        static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks.Add(text); }
        static void Finish(Exception error = null)
        {
            File.WriteAllText("Docs/Integration/unity-play-smoke.json", JsonConvert.SerializeObject(new { passed = error == null, time = DateTimeOffset.Now, checks, error = error?.ToString() }, Formatting.Indented));
            SessionState.SetBool(Key, false); EditorApplication.ExitPlaymode();
        }
        static void ExitLevel()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            var exit = UnityEngine.Object.FindFirstObjectByType<LevelExit>();
            Check(player && exit, "关卡玩家与出口已实例化");
            typeof(LevelExit).GetField("gates", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(exit, Array.Empty<MonoBehaviour>());
            typeof(LevelExit).GetMethod("CollectGates", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(exit, null);
            typeof(LevelExit).GetMethod("TryExit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(exit, new object[] {player.GetComponent<Collider>()});
        }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Docs/Integration/play.request"))
            {
                File.Delete("Docs/Integration/play.request");
                foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty))
                    EditorSceneManager.SaveScene(scene, "AssetStaging/CrystalRelease_UserScene_" + scene.handle + ".unity", true);
                SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode(); return;
            }
            if (!Application.isPlaying || !SessionState.GetBool(Key, false) || Time.timeSinceLevelLoad < .5f) return;
            try
            {
                if (phase == 0)
                {
                    Application.runInBackground = true; checks.Clear(); deadline = EditorApplication.timeSinceStartup + 90;
                    foreach (var go in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).SelectMany(s=>s.GetRootGameObjects())) go.SetActive(false);
                    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Soviet_1.unity", new LoadSceneParameters(LoadSceneMode.Additive)); phase = 1; return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play smoke timed out at phase " + phase);
                if (phase == 1)
                {
                    var scene = SceneManager.GetSceneByName("Soviet_1"); if (!scene.isLoaded) return; SceneManager.SetActiveScene(scene);
                    var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(); if (!player) return;
                    Check(GameStateManager.State == GameState.Running, "第一关载入后为 Running");
                    GameStateManager.RequestPause(); Check(GameplayGate.IsBlocked && Time.timeScale == 0, "暂停冻结时间并拦截输入");
                    GameStateManager.RequestResume(); Check(!GameplayGate.IsBlocked && Time.timeScale == 1, "恢复后时间与输入正常");
                    var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");
                    var enemy = UnityEngine.Object.Instantiate(enemyPrefab, player.transform.position + Vector3.forward * 3, Quaternion.identity).GetComponent<Resonator.AI.ResonatorEnemy>();
                    enemy.Initialize(Resonator.AI.RestActivity.RestlessIdle);
                    Check(enemy.GetComponent<ResonatorPunchTarget>(), "新版共鸣者自动接入关卡冲拳伤害接口");
                    float before = enemy.Health; enemy.GetComponent<ResonatorPunchTarget>().OnPunched(enemy.transform.position, Vector3.back, Vector3.forward, 1, 1);
                    Check(enemy.Health < before, "关卡冲拳可以伤害新版共鸣者");
                    ExitLevel(); phase = 2; return;
                }
                if (phase == 2)
                {
                    if (SceneManager.GetActiveScene().name != "Soviet_TheBridge") return;
                    Check(GameStateManager.State == GameState.Running, "第一关出口实际载入桥梁关"); ExitLevel(); phase = 3; return;
                }
                if (phase == 3)
                {
                    if (SceneManager.GetActiveScene().name != "UDK7MBossArena_ThreePhase") return;
                    var arena = UnityEngine.Object.FindFirstObjectByType<UDK7M.UDK7MArena>(); if (!arena || !arena.player) return;
                    Check(arena.player.vitals && arena.player.gauntlet, "桥梁出口实际载入三阶段 BOSS 与专用玩家");
                    GameStateManager.RequestPause(); Check(GameplayGate.IsBlocked && Time.timeScale == 0, "BOSS 关沿用统一暂停状态");
                    GameStateManager.RequestResume(); arena.BeginFight(); Check(arena.Started && arena.boss.Engaged, "BOSS 可以开始战斗");
                    arena.ResetEncounter(); Check(!arena.Won && arena.boss.Health > 0, "BOSS 可以重置"); Finish();
                }
            }
            catch(Exception e) { Finish(e); }
        }
    }
}
