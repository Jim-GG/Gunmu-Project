using System;
using System.Reflection;
using FirstPerson;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModernLabTrials
{
    // Standalone smoke test. Enabled only by the -modernLabSelfTest command line.
    // It walks ML-01 -> ML-04, completes every objective (receivers, crystal
    // growth, drone rides and resonator kills), fires one drone bolt and checks
    // that the exit chain still reaches the final screen.
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

        private System.Collections.IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2f);
            string[] expected =
            {
                "MLT_01_CrystalIntake", "MLT_02_CrystalAscent",
                "MLT_03_ResonanceArena", "MLT_04_CoreBreach",
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
                          $"receivers={level.RequiredReceivers} enemies={level.RequiredEnemies} " +
                          $"growth={level.RequiredGrowths} rides={level.RequiredRides}");
                Check(level);
                CompleteObjectives(level);
                yield return new WaitForSecondsRealtime(4f);
                Debug.Log($"SELFTEST: completed receivers={level.DebugReceivers}/{level.RequiredReceivers} " +
                          $"enemies={level.DebugEnemies}/{level.RequiredEnemies} " +
                          $"growth={level.DebugGrowths}/{level.RequiredGrowths} " +
                          $"rides={level.DebugRides}/{level.RequiredRides} complete={level.DebugComplete}");
                if (!level.DebugComplete)
                {
                    Fail("level did not complete: " + level.levelCode);
                    yield break;
                }
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

        private void CompleteObjectives(TrialLevel level)
        {
            foreach (ImpactReceiver receiver in level.receivers) receiver.Activate();
            foreach (Resonator.AI.ResonatorEnemy enemy in level.enemies)
                enemy.TakeDamage(9999f, enemy.transform.position + Vector3.forward, 0f);

            // A crystal growth site needs a registered CrystalTraversal striker;
            // sending the hit from the player position mimics a real punch.
            Vector3 striker = level.motor != null ? level.motor.transform.position : Vector3.zero;
            foreach (CrystalParkour.CrystalGrowthSite site in level.growthSites)
                if (site != null) site.ReceiveActionHit(10f, striker, 0f);

            foreach (NeutralDrone drone in level.drones) level.NotifyDroneRide(drone);
        }

        private void Check(TrialLevel level)
        {
            Expect(level.vitals != null && level.motor != null && level.combat != null, "player components");
            Expect(level.vitals != null && level.vitals.Health > 0, "player alive");
            Expect(level.drones != null && level.drones.Length > 0, "drones");
            Expect(level.exitDoor != null, "exit door");
            Expect(level.RequiredReceivers + level.RequiredEnemies + level.RequiredGrowths + level.RequiredRides > 0,
                "at least one objective");
            foreach (var enemy in level.enemies)
            {
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Expect(agent != null && agent.isOnNavMesh, "enemy on NavMesh");
            }
            if (level.motor != null)
                Expect(level.motor.GetComponent<FirstPersonInput>() != null, "player input");

            if (level.drones != null && level.drones.Length > 0 && level.drones[0] != null)
            {
                NeutralDrone drone = level.drones[0];
                Expect(drone.AttackEnabled, "drone attack enabled");
                DroneBolt bolt = drone.FireForTest();
                Expect(bolt != null && bolt.Damage > 0f, "drone fired a damaging bolt");
                if (bolt != null) Destroy(bolt.gameObject);
            }
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
