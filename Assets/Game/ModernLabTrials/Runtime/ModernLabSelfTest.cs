using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FirstPerson;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModernLabTrials
{
    // Standalone smoke test. Enabled only by the -modernLabSelfTest command line.
    [UnityEngine.Scripting.Preserve]
    public sealed class ModernLabSelfTest : MonoBehaviour
    {
        private static bool bootstrapped;
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (bootstrapped) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-modernLabSelfTest") < 0) return;
            bootstrapped = true;
            GameObject runner = new GameObject("ModernLabSelfTest");
            DontDestroyOnLoad(runner);
            runner.AddComponent<ModernLabSelfTest>();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2f);
            string[] expected =
            {
                "MLT_01_ImpactCalibration", "MLT_02_CrosswindGallery",
                "MLT_03_VerticalShaft", "MLT_04_SynthesisCore",
            };
            for (int i = 0; i < expected.Length; i++)
            {
                TrialLevel level = Find<TrialLevel>();
                if (level == null)
                {
                    Fail("scene missing TrialLevel: " + SceneManager.GetActiveScene().name);
                    yield break;
                }
                Debug.Log($"SELFTEST: scene={SceneManager.GetActiveScene().name} code={level.levelCode} " +
                          $"receivers={level.receivers.Length} enemies={level.enemies.Length} drones={level.drones.Length}");
                Check(level);
                foreach (ImpactReceiver receiver in level.receivers) receiver.Activate();
                foreach (var enemy in level.enemies)
                    enemy.TakeDamage(9999f, enemy.transform.position + Vector3.forward, 0f);
                level.NotifyDroneRide(level.drones[0]);
                yield return new WaitForSecondsRealtime(4f);
                Debug.Log($"SELFTEST: completed receivers={level.DebugReceivers} enemies={level.DebugEnemies} rides={level.DebugRides} complete={level.DebugComplete}");
                level.TryExit();
                yield return new WaitForSecondsRealtime(2.0f);

                if (i < expected.Length - 1)
                {
                    string actual = SceneManager.GetActiveScene().name;
                    if (actual != expected[i + 1])
                    {
                        Fail($"expected {expected[i + 1]}, got {actual}");
                        yield break;
                    }
                    Debug.Log("SELFTEST: loaded " + actual);
                }
                else
                {
                    FieldInfo field = typeof(TrialLevel).GetField("finalShown", BindingFlags.Instance | BindingFlags.NonPublic);
                    bool finalShown = field != null && (bool)field.GetValue(level);
                    if (!finalShown)
                    {
                        Fail("final level did not reach final screen");
                        yield break;
                    }
                    Debug.Log("SELFTEST: final screen reached");
                }
            }
            Debug.Log(failures == 0 ? "MODERNLAB_SELFTEST: PASS" : $"MODERNLAB_SELFTEST: FAIL ({failures})");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private void Check(TrialLevel level)
        {
            Expect(level.vitals != null && level.motor != null && level.combat != null, "player components");
            Expect(level.receivers != null && level.receivers.Length > 0, "receivers");
            Expect(level.enemies != null && level.enemies.Length > 0, "enemies");
            Expect(level.drones != null && level.drones.Length > 0, "drones");
            Expect(level.exitDoor != null, "exit door");
            Expect(level.vitals != null && level.vitals.Health > 0, "player alive");
            foreach (var enemy in level.enemies)
            {
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Expect(agent != null && agent.isOnNavMesh, "enemy on NavMesh");
            }
            if (level.motor != null)
                Expect(level.motor.GetComponent<FirstPersonInput>() != null, "player input");
        }

        private void Expect(bool condition, string message)
        {
            if (condition) Debug.Log("SELFTEST PASS: " + message);
            else Fail(message);
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("SELFTEST FAIL: " + message);
            Debug.Log($"MODERNLAB_SELFTEST: FAIL ({failures})");
            Application.Quit(1);
        }

        private static T Find<T>() where T : Component
        {
            return UnityEngine.Object.FindFirstObjectByType<T>();
        }
    }
}
