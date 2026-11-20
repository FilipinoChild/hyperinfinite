using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// Sends packs of Scuttlers in waves, each bigger than the one before. The next pack doesn't wait for the
    /// last to be wiped out: it comes once only a few are left, so the fight never goes quiet. It also waits
    /// while there's no room under a cap on how many can be alive at once. A big pack splits between two
    /// spawn points.
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
        [Tooltip("Seconds after there's room for the next pack before it arrives.")]
        [SerializeField, Min(0f)] float timeBetweenPacks = 3f;
        [Tooltip("The next pack comes while this many of the last are still alive, so the fight never goes quiet. 0 waits until they're all dead.")]
        [SerializeField, Min(0)] int nextPackWhenLeft = 3;
        [Tooltip("Never more than this many Scuttlers alive at once; the next pack waits for room. A pack bigger than this still comes once the arena is empty.")]
        [SerializeField, Min(1)] int maxAlive = 22;
        [Tooltip("How spread out a pack is when it appears, in meters.")]
        [SerializeField, Min(0f)] float clusterRadius = 1.5f;
        [Tooltip("A pack bigger than this splits between two spawn points, so it comes at you from two sides. 0 never splits.")]
        [SerializeField, Min(0)] int splitAbove = 8;

        [Header("Events")]
        [Tooltip("Sent with the pack number, starting at 1.")]
        public UnityEvent<int> onPackSpawned = new UnityEvent<int>();
        [Tooltip("Every pack has been sent and killed. The boss fight can start here.")]
        public UnityEvent onAllPacksCleared = new UnityEvent();

        readonly List<GameObject> alive = new List<GameObject>();   // every Scuttler this spawner sent that's still alive
        int packsSpawned;
        float timer;
        bool cleared, waiting;

        // Read-only, for the HUD's wave counter.
        /// <summary>The wave in progress (the pack last sent), starting at 1; 0 before the first.</summary>
        public int Wave => packsSpawned;
        /// <summary>How many waves there are in all; 0 means they keep coming.</summary>
        public int TotalWaves => packsToSpawn;
        /// <summary>Scuttlers this spawner sent that are still alive, from any wave.</summary>
        public int ScuttlersLeft
        {
            get
            {
                int count = 0;
                for (int i = 0; i < alive.Count; i++) if (alive[i]) count++;
                return count;
            }
        }
        /// <summary>Seconds until the next wave arrives, while it's on its way; 0 otherwise.</summary>
        public float NextWaveIn => waiting && !cleared ? Mathf.Max(0f, timer) : 0f;
        /// <summary>Every wave has been sent and killed.</summary>
        public bool AllCleared => cleared;

        void Update()
        {
            // A destroyed object counts as null in Unity, so this drops the dead ones from the list
            alive.RemoveAll(scuttler => scuttler == null);

            // Every pack sent and killed: announce it once, then stop
            int total = TotalWaves;
            if (total > 0 && packsSpawned >= total)
            {
                if (alive.Count == 0 && !cleared)
                {
                    cleared = true;
                    onAllPacksCleared.Invoke();
                    Debug.Log("All Scuttler packs cleared");
                }
                return;
            }

            // The next pack comes once the last is down to a few, and there's room for it under the cap.
            int next = PackSize(packsSpawned);
            bool room = alive.Count <= nextPackWhenLeft && (alive.Count == 0 || alive.Count + next <= maxAlive);
            if (!room)
            {
                waiting = false;
                return;
            }
            if (!waiting)
            {
                waiting = true;
                timer = packsSpawned == 0 ? 1f : timeBetweenPacks;   // a short pause before the first pack
            }
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            waiting = false;
            SpawnPack();
        }

        /// <summary>How big pack <paramref name="index"/> is, counting from 0.</summary>
        int PackSize(int index) => Mathf.Min(maxPackSize, firstPackSize + packGrowth * index);

        void SpawnPack()
        {
            if (!scuttlerPrefab || spawnPoints.Length == 0)
            {
                Debug.LogWarning("ScuttlerSpawner needs a Scuttler prefab and at least one spawn point.", this);
                enabled = false;
                return;
            }

            int count = spawnPoints.Length;
            int first = Random.Range(0, count);
            int size = PackSize(packsSpawned);
            // A big pack splits: every other Scuttler comes from a second, different spawn point.
            int second = splitAbove > 0 && size > splitAbove && count > 1 ? (first + Random.Range(1, count)) % count : first;

            for (int i = 0; i < size; i++)
            {
                Transform point = spawnPoints[i % 2 == 0 ? first : second];
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