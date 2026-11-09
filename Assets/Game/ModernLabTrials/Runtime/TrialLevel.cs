using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson;
using Resonator.AI;
using Unity.AI.Navigation;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ModernLabTrials
{
    // One scene = one modern-lab trial. Tracks impact receivers, Resonator enemies
    // and drone rides; opens the exit only when all three are satisfied.
    [DefaultExecutionOrder(-500)]
    public sealed class TrialLevel : MonoBehaviour
    {
        [Header("Identity")]
        public string levelCode = "ML-01";
        public string levelTitle = "冲击校准";
        public string levelKind = "解谜关";
        public string subtitle = "用冲量把载荷送进接收器，并清除守卫。";
        public string nextSceneName = "";
        public string firstSceneName = "MLT_01_CrystalIntake";
        public bool finalLevel;

        [Header("Player / spawn")]
        public FirstPersonVitals vitals;
        public FirstPersonMotor motor;
        public GauntletCombat combat;
        public Camera view;
        public Transform spawn;

        [Header("Objectives")]
        public ImpactReceiver[] receivers;
        public ResonatorEnemy[] enemies;
        public NeutralDrone[] drones;
        [Min(0)] public int requiredDroneRides = 1;
        public CrystalParkour.CrystalGrowthSite[] growthSites;
        [Tooltip("-1 means every entry in the matching array is required.")]
        public int requiredReceivers = -1;
        public int requiredEnemyKills = -1;
        public int requiredGrowths = -1;

        [Header("Crystal traversal")]
        public CrystalParkour.CrystalTraversal traversal;
        public bool enableCrystalTraversal = true;

        [Header("Exit")]
        public TrialDoor exitDoor;
        public Transform exitPoint;

        private int receiverDone, enemyDone, rideDone, growthDone;
        private readonly HashSet<ImpactReceiver> receiverSet = new HashSet<ImpactReceiver>();
        private readonly HashSet<ResonatorEnemy> enemySet = new HashSet<ResonatorEnemy>();
        private readonly HashSet<NeutralDrone> rideSet = new HashSet<NeutralDrone>();
        private bool complete, paused, dead, finalShown, transitioning;
        private float fade, messageUntil;
        private string message;
        private Font font;

        public int DebugReceivers => receiverDone;
        public int DebugEnemies => enemyDone;
        public int DebugRides => rideDone;
        public int DebugGrowths => growthDone;
        public bool DebugComplete => complete;

        public int RequiredReceivers => Quota(requiredReceivers, receivers);
        public int RequiredEnemies => Quota(requiredEnemyKills, enemies);
        public int RequiredGrowths => Quota(requiredGrowths, growthSites);
        public int RequiredRides => Mathf.Max(0, requiredDroneRides);

        private static int Quota<T>(int requested, T[] entries)
        {
            int available = entries == null ? 0 : entries.Length;
            if (requested < 0) return available;
            return Mathf.Min(requested, available);
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Start()
        {
            if (traversal != null)
            {
                traversal.enabled = enableCrystalTraversal;
                // R already reloads the whole trial, so the parkour demo's
                // checkpoint respawn must not swallow the key.
                traversal.respawnKeyEnabled = false;
            }
            EnsureNavigation();
            if (spawn != null && motor != null)
            {
                motor.Body.enabled = false;
                motor.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                motor.Body.enabled = true;
                motor.StopMomentum();
                motor.SetLookRotation(spawn.rotation);
            }
            if (vitals != null)
            {
                vitals.Restore();
                vitals.Died += OnPlayerDied;
            }
            if (receivers != null)
                foreach (ImpactReceiver receiver in receivers)
                    if (receiver != null) receiver.Activated += OnReceiverActivated;
            if (enemies != null)
                foreach (ResonatorEnemy enemy in enemies)
                {
                    if (enemy == null) continue;
                    ResonatorEnemy captured = enemy;
                    captured.Died += () => OnEnemyDied(captured);
                }
            if (drones != null)
                foreach (NeutralDrone drone in drones)
                    if (drone != null) drone.RideCompleted += OnDroneRide;
            ShowMessage(levelCode + " · " + levelTitle + "  |  " + subtitle, 5f);
            Evaluate();
        }

        private void EnsureNavigation()
        {
            if (NavMesh.SamplePosition(transform.position, out _, 3f, NavMesh.AllAreas)) return;
            NavMeshSurface surface = FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) return;
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (NavMeshAgent agent in agents) agent.enabled = false;
            surface.BuildNavMesh();
            foreach (NavMeshAgent agent in agents) agent.enabled = true;
            Debug.Log("TrialLevel: rebuilt NavMesh at runtime for " + SceneManager.GetActiveScene().name, this);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (transitioning) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (finalShown) return;
                if (paused) Resume();
                else if (!dead) Pause();
            }
            if (dead)
            {
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
                return;
            }
            if (paused || finalShown) return;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Restart();
                return;
            }
            if (motor != null && motor.transform.position.y < -12f)
            {
                OnPlayerDied();
                return;
            }
            PollGrowthSites();
            if (Time.unscaledTime > messageUntil) message = null;
        }

        private void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Resume()
        {
            paused = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnPlayerDied()
        {
            if (dead) return;
            dead = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnReceiverActivated(ImpactReceiver receiver)
        {
            if (receiver != null && receiverSet.Add(receiver)) receiverDone = receiverSet.Count;
            ShowMessage("冲击接收器已激活 · " + receiverDone + "/" + RequiredReceivers, 2.2f);
            Evaluate();
        }

        private void OnEnemyDied(ResonatorEnemy enemy)
        {
            if (enemy != null && enemySet.Add(enemy)) enemyDone = enemySet.Count;
            ShowMessage("共鸣者已清除 · " + enemyDone + "/" + RequiredEnemies, 2f);
            Evaluate();
        }

        private void OnDroneRide(NeutralDrone drone)
        {
            if (drone != null && rideSet.Add(drone)) rideDone = rideSet.Count;
            if (RequiredRides > 0)
                ShowMessage("无人机借力完成 · " + rideDone + "/" + RequiredRides, 2.2f);
            Evaluate();
        }

        private void PollGrowthSites()
        {
            if (growthSites == null || growthSites.Length == 0) return;
            int grown = 0;
            foreach (CrystalParkour.CrystalGrowthSite site in growthSites)
                if (site != null && site.HasGrown) grown++;
            if (grown == growthDone) return;
            growthDone = grown;
            ShowMessage("晶体平台生长 · " + growthDone + "/" + RequiredGrowths, 2.2f);
            Evaluate();
        }

        // Explicit entry point used by the automated smoke test and by future
        // drone variants that are not NeutralDrone instances.
        public void NotifyDroneRide(NeutralDrone drone) => OnDroneRide(drone);

        private void Evaluate()
        {
            if (complete) return;
            if (receiverDone < RequiredReceivers || enemyDone < RequiredEnemies ||
                growthDone < RequiredGrowths || rideDone < RequiredRides)
                return;
            complete = true;
            if (exitDoor != null) exitDoor.SetOpen(true);
            ShowMessage("全部目标完成 · 出口已开启", 4f);
        }

        public void TryExit()
        {
            if (!complete)
            {
                ShowMessage("出口尚未开启：先完成本关全部目标", 2.5f);
                return;
            }
            if (transitioning || finalShown) return;
            if (finalLevel)
            {
                finalShown = true;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            if (string.IsNullOrEmpty(nextSceneName)) return;
            StartCoroutine(TransitionTo(nextSceneName));
        }

        private IEnumerator TransitionTo(string sceneName)
        {
            transitioning = true;
            Time.timeScale = 1f;
            float time = 0f;
            while (time < .45f)
            {
                time += Time.unscaledDeltaTime;
                fade = Mathf.Clamp01(time / .45f);
                yield return null;
            }
            fade = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ShowMessage(string text, float seconds = 2.5f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        private void OnGUI()
        {
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 18);
            GUIStyle title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 25, fontStyle = FontStyle.Bold };
            GUIStyle body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
            GUIStyle small = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14 };
            Color white = new Color(.9f, .96f, .96f, 1f);
            title.normal.textColor = body.normal.textColor = small.normal.textColor = white;

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f,
                (Screen.height - 720f * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);

            GUI.Label(new Rect(30, 22, 760, 34), "零界 · 现代实验室 / " + levelCode +
                (string.IsNullOrEmpty(levelKind) ? "" : " · " + levelKind), title);
            GUI.Label(new Rect(30, 58, 900, 30), levelTitle, body);
            float y = 96f;
            if (RequiredReceivers > 0)
            {
                DrawObjective(new Rect(30, y, 420, 24), "碰撞接收器", receiverDone, RequiredReceivers, small);
                y += 26;
            }
            if (RequiredEnemies > 0)
            {
                DrawObjective(new Rect(30, y, 420, 24), "清除共鸣者", enemyDone, RequiredEnemies, small);
                y += 26;
            }
            if (RequiredGrowths > 0)
            {
                DrawObjective(new Rect(30, y, 420, 24), "晶体平台生长", growthDone, RequiredGrowths, small);
                y += 26;
            }
            if (RequiredRides > 0)
                DrawObjective(new Rect(30, y, 420, 24), "无人机借力", rideDone, RequiredRides, small);

            if (vitals != null)
            {
                GUI.Label(new Rect(30, 632, 300, 22), "生命 " + Mathf.CeilToInt(vitals.Health) + " / " + Mathf.CeilToInt(vitals.maxHealth), small);
                Fill(new Rect(30, 656, 220, 7), new Color(.12f, .18f, .2f));
                Fill(new Rect(30, 656, 220 * Mathf.Clamp01(vitals.Health / Mathf.Max(1f, vitals.maxHealth)), 7), new Color(.55f, .8f, .7f));
            }
            if (combat != null)
            {
                GUI.Label(new Rect(930, 610, 300, 22), "冲量储存 " + combat.BlockEnergy + " / 3", small);
                for (int i = 0; i < 3; i++)
                    Fill(new Rect(930 + i * 68, 640, 56, 10),
                        i < combat.BlockEnergy ? new Color(.3f, .8f, 1f) : new Color(.1f, .2f, .24f));
            }

            if (!dead && !paused && !finalShown)
            {
                Fill(new Rect(637, 357, 6, 6), new Color(.85f, .95f, .95f, .8f));
                if (!string.IsNullOrEmpty(message))
                    GUI.Label(new Rect(300, 585, 680, 60), message, body);
                GUI.Label(new Rect(30, 680, 1220, 24), "WASD 移动   Shift 奔跑   空格 跳跃   左键出拳/蓄力   右键格挡   R 重开   Esc 暂停", small);
            }

            if (complete && !dead && !paused && !finalShown)
                GUI.Label(new Rect(300, 545, 680, 30), "出口已开启 · 前往高台出口", body);

            if (paused)
            {
                Fill(new Rect(0, 0, 1280, 720), new Color(.01f, .03f, .04f, .9f));
                GUI.Label(new Rect(430, 210, 500, 60), "已暂停", title);
                if (GUI.Button(new Rect(480, 300, 320, 48), "继续")) Resume();
                if (GUI.Button(new Rect(480, 362, 320, 48), "重开本关")) Restart();
                GUI.Label(new Rect(430, 440, 500, 30), "Esc 返回游戏", small);
            }
            else if (dead)
            {
                Fill(new Rect(0, 0, 1280, 720), new Color(.04f, .01f, .01f, .92f));
                GUI.Label(new Rect(430, 220, 500, 60), "行动中断", title);
                GUI.Label(new Rect(380, 290, 560, 30), "共鸣者压制了实验员。", body);
                if (GUI.Button(new Rect(480, 350, 320, 48), "重试本关（R）")) Restart();
            }
            else if (finalShown)
            {
                Fill(new Rect(0, 0, 1280, 720), new Color(.01f, .04f, .05f, .94f));
                GUI.Label(new Rect(330, 210, 700, 60), "现代实验室 · 全部测试完成", title);
                GUI.Label(new Rect(330, 280, 700, 60), "晶体、碰撞、无人机与共鸣者战斗的测试均已记录。", body);
                if (GUI.Button(new Rect(470, 380, 340, 48), "从第一关重新开始")) SceneManager.LoadScene(firstSceneName);
            }

            if (fade > 0f) Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, fade));
            GUI.matrix = old;
        }

        private static void DrawObjective(Rect rect, string label, int done, int total, GUIStyle style)
        {
            bool complete = done >= total;
            GUI.Label(rect, (complete ? "■ " : "□ ") + label + "   " + Mathf.Min(done, total) + " / " + total, style);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
