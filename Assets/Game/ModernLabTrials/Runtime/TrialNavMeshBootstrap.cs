using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ModernLabTrials
{
    // Builds the NavMesh in Awake, before any ResonatorAgent's NavMeshAgent is
    // enabled. The baked asset is also used when available; this is a safety net
    // for player builds where the NavMeshData is not loaded early enough.
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class TrialNavMeshBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            NavMeshSurface surface = GetComponent<NavMeshSurface>();
            if (surface == null) return;
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (NavMeshAgent agent in agents) agent.enabled = false;
            surface.BuildNavMesh();
            foreach (NavMeshAgent agent in agents) agent.enabled = true;
        }
    }
}
