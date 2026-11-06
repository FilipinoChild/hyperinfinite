using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// Sends packs of Scuttlers in waves. A new pack spawns once the last one is dead,
    /// and each pack is bigger than the one before.
    public class ScuttlerSpawner : MonoBehaviour
    {
        [Header("What and where")]
        [SerializeField] GameObject scuttlerPrefab;
        [Tooltip("A pack appears at one of these, chosen at random.")]
        [SerializeField] Transform[] spawnPoints = new Transform[0];

        [Header("Packs")]
        [SerializeField, Min(1)] int firstPackSize = 3;
        [Tooltip("How many more Scuttlers each new pack has.")]
        [SerializeField, Min(0)] int packGrowth = 1;
        [SerializeField, Min(1)] int maxPackSize = 8;
        [Tooltip("How many packs to send in total. 0 keeps going forever.")]
        [SerializeField, Min(0)] int packsToSpawn = 5;
        [Tooltip("Seconds of rest after a pack is wiped out before the next one arrives.")]
        [SerializeField, Min(0f)] float timeBetweenPacks = 3f;
        [Tooltip("How spread out a pack is when it appears, in meters.")]
        [SerializeField, Min(0f)] float clusterRadius = 1.5f;

        [Header("Events")]
        [Tooltip("Sent with the pack number, starting at 1.")]
        public UnityEvent<int> onPackSpawned = new UnityEvent<int>();
        [Tooltip("Every pack has been sent and killed. The boss fight can start here.")]
        public UnityEvent onAllPacksCleared = new UnityEvent();

        readonly List<GameObject> alive = new List<GameObject>();   // Scuttlers from the current pack
        int packsSpawned;
        float timer = 1f;    // short pause before the first pack
        bool cleared;

        // Read-only, for the HUD's wave counter.
        /// <summary>The wave in progress (the pack last sent), starting at 1; 0 before the first.</summary>
        public int Wave => packsSpawned;
        /// <summary>How many waves there are in all; 0 means they keep coming.</summary>
        public int TotalWaves => packsToSpawn;
        /// <summary>Scuttlers from the current wave still alive.</summary>
        public int ScuttlersLeft
        {
            get
            {
                int count = 0;
                foreach (GameObject scuttler in alive) if (scuttler) count++;
                return count;
            }
        }
        /// <summary>Seconds until the next wave arrives, once this one is dead; 0 otherwise.</summary>
        public float NextWaveIn => cleared || ScuttlersLeft > 0 ? 0f : Mathf.Max(0f, timer);
        /// <summary>Every wave has been sent and killed.</summary>
        public bool AllCleared => cleared;

        void Update()
        {
            // A destroyed object counts as null in Unity, so this drops the dead ones from the list
            alive.RemoveAll(scuttler => scuttler == null);
            if (alive.Count > 0) return;   // wait until the current pack is dead

            // Every pack sent and killed: announce it once, then stop
            if (packsToSpawn > 0 && packsSpawned >= packsToSpawn)
            {
                if (!cleared)
                {
                    cleared = true;
                    onAllPacksCleared.Invoke();
                    Debug.Log("All Scuttler packs cleared");
                }
                return;
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            SpawnPack();
            timer = timeBetweenPacks;
        }

        void SpawnPack()
        {
            if (!scuttlerPrefab || spawnPoints.Length == 0)
            {
                Debug.LogWarning("ScuttlerSpawner needs a Scuttler prefab and at least one spawn point.", this);
                enabled = false;
                return;
            }

            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            int size = Mathf.Min(maxPackSize, firstPackSize + packGrowth * packsSpawned);

            for (int i = 0; i < size; i++)
            {
                // Scatter each Scuttler a little around the spawn point, on walkable ground
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                Vector3 position = point.position + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    position = hit.position;

                alive.Add(Instantiate(scuttlerPrefab, position, point.rotation));
            }

            packsSpawned++;
            onPackSpawned.Invoke(packsSpawned);
            Debug.Log("Pack " + packsSpawned + ": " + size + " Scuttlers");
        }
    }
}