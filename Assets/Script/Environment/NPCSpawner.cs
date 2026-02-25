using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Script.Environment
{
    /// <summary>
    /// Attach to an NPC spawn point (scene object).
    /// Listens for a door opening, then spawns NPC prefabs and immediately injects
    /// scene-specific waypoints into them (prefabs can't hold scene references themselves).
    /// </summary>
    public class NPCSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("NPC prefabs to spawn. One will be picked randomly each cycle.")]
        [SerializeField] private List<GameObject> npcPrefabs;

        [Tooltip("How many NPCs to spawn when the door is opened.")]
        [SerializeField] private int spawnCount = 1;

        [Tooltip("Delay (seconds) between each NPC when spawning more than one.")]
        [SerializeField] private float spawnInterval = 1.5f;

        [Tooltip("If true, NPCs only spawn the first time the door is opened.")]
        [SerializeField] private bool spawnOnce = true;

        [Header("Waypoints")]
        [Tooltip("Scene object the NPC walks to (e.g. the tavern counter marker). " +
                 "Drag any scene object here \u2014 the spawner will pass it to each NPC after spawning.")]
        [SerializeField] private GameObject counterWaypoint;

        [Tooltip("Scene object the NPC walks to when leaving. " +
                 "Drag any scene object here \u2014 the spawner will pass it to each NPC after spawning.")]
        [SerializeField] private GameObject exitWaypoint;

        private bool _hasSpawned = false;

        private void OnEnable()
        {
            DoorAnimation.OnDoorOpened += HandleDoorOpened;
        }

        private void OnDisable()
        {
            DoorAnimation.OnDoorOpened -= HandleDoorOpened;
        }

        private void HandleDoorOpened()
        {
            if (spawnOnce && _hasSpawned) return;
            if (npcPrefabs == null || npcPrefabs.Count == 0)
            {
                Debug.LogWarning("[NPCSpawner] No NPC prefabs assigned!", this);
                return;
            }

            _hasSpawned = true;
            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            // First wave of spawns immediately upon opening the door
            int initialSpawns = spawnCount;
            if (ShopManager.Instance != null)
            {
                initialSpawns *= ShopManager.Instance.CurrentMultiplier;
            }

            // Apply EventManager burst if active
            if (EventManager.Instance != null)
            {
                initialSpawns *= EventManager.Instance.GetBurstSpawnAmount();
            }

            float currentSpawnInterval = spawnInterval;
            if (EventManager.Instance != null)
            {
                currentSpawnInterval *= EventManager.Instance.GetSpawnIntervalMultiplier();
            }

            for (int i = 0; i < initialSpawns; i++)
            {
                if (QueueManager.Instance != null && QueueManager.Instance.IsQueueFull())
                {
                    Debug.Log("[NPCSpawner] Queue is full! Delaying initial spawn wave.");
                    break;
                }

                SpawnOne();
                if (i < initialSpawns - 1)
                    yield return new WaitForSeconds(currentSpawnInterval);
            }

            // Continuous spawning loop while the shop is open
            while (DayCycleManager.Instance != null && DayCycleManager.Instance.IsWorkingHours)
            {
                yield return new WaitForSeconds(currentSpawnInterval); // Wait before dropping the next customer/wave 
                
                // Recalculate intervals and stats in case they change mid-day
                if (EventManager.Instance != null)
                {
                    currentSpawnInterval = spawnInterval * EventManager.Instance.GetSpawnIntervalMultiplier();
                }

                // Recalculate spawn count in case the multiplier leveled up mid-day
                int currentSpawns = spawnCount;
                if (ShopManager.Instance != null)
                {
                    currentSpawns *= ShopManager.Instance.CurrentMultiplier;
                }
                if (EventManager.Instance != null)
                {
                    currentSpawns *= EventManager.Instance.GetBurstSpawnAmount();
                }

                for (int i = 0; i < currentSpawns; i++)
                {
                    if (QueueManager.Instance != null && QueueManager.Instance.IsQueueFull())
                    {
                        Debug.Log("[NPCSpawner] Queue is full! Skipping the rest of this spawn wave.");
                        break; 
                    }

                    SpawnOne();
                    if (i < currentSpawns - 1)
                        yield return new WaitForSeconds(1.0f); // Fast stagger between a single wave
                }
            }

            Debug.Log("[NPCSpawner] Working hours ended. Ceasing spawns.");
        }

        private void SpawnOne()
        {
            GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];
            if (prefab == null) return;

            GameObject npc = Instantiate(prefab, transform.position, transform.rotation);

            // The new NPC script perfectly auto-finds its waypoints by name
            NPC controller = npc.GetComponent<NPC>();
            if (controller == null)
            {
                Debug.LogWarning($"[NPCSpawner] Spawned prefab '{prefab.name}' has no NPC component!", this);
            }

            Debug.Log($"[NPCSpawner] Spawned {prefab.name} at {gameObject.name}.");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.3f);
            Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.8f);

            if (counterWaypoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, counterWaypoint.transform.position);
            }
            if (exitWaypoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, exitWaypoint.transform.position);
            }
        }
    }
}
