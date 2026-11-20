using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// Sends packs of Scuttlers in waves, each bigger than the one before. The next pack doesn't wait for the
    /// last to be wiped out: it comes once only a few are left, so the fight never goes quiet. It also waits
    /// while there's no room under a cap on how many can be alive at once. A big pack splits between two
    /// spawn points. From a chosen pack on, Spitters come along and hang back behind the Scuttlers.
    ///
    /// Every newcomer is put somewhere it can walk to from its spawn point in a straight line, on the NavMesh and
    /// inside the play area, so however many come, none ends up inside a wall or rock, on top of one, or outside
    /// the arena. Spitters fill rings around the spawn point, the side away from the player first.
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

        [Header("Ranged support")]   // SPITTER
        [Tooltip("The Spitter prefab. Leave empty for Scuttler-only packs.")]
        [SerializeField] GameObject spitterPrefab;
        [Tooltip("Spitters join packs from this pack number on (1 = the first pack).")]
        [SerializeField, Min(1)] int spittersFromPack = 2;
        [SerializeField, Min(0)] int spittersPerPack = 1;

        [Tooltip("How many more Spitters each new pack has.")]
        [SerializeField, Min(0)] int spitterGrowth = 1;
        [SerializeField, Min(0)] int maxSpittersPerPack = 4;
        [Tooltip("How far apart Spitters stand when they appear, in meters. A big group fills rings around the spawn point.")]
        [SerializeField, Min(0.5f)] float spitterSpacing = 2f;

        [Header("Armored support")]
        [Tooltip("Bigback prefab. Leave empty to disable armored enemies.")]
        [SerializeField] GameObject bigbackPrefab;
        [SerializeField, Min(1)] int bigbacksFromPack = 3;
        [SerializeField, Min(0)] int bigbacksPerPack = 1;
        [SerializeField, Min(0)] int maxBigbacksPerPack = 2;

        int BigbackCount(int index) => bigbackPrefab && index + 1 >= bigbacksFromPack
            ? Mathf.Min(maxBigbacksPerPack, bigbacksPerPack + (index + 1 - bigbacksFromPack) / 2) : 0;

        [Header("Events")]
        [Tooltip("Sent with the pack number, starting at 1.")]
        public UnityEvent<int> onPackSpawned = new UnityEvent<int>();
        [Tooltip("Every pack has been sent and killed. The boss fight can start here.")]
        public UnityEvent onAllPacksCleared = new UnityEvent();
        [Tooltip("Every enemy a pack brought is dead, even if the next pack is already in. Sent with its wave number.")]
        public UnityEvent<int> onWaveCleared = new UnityEvent<int>();

        /// <summary>One wave's enemies, to tell when they're all dead.</summary>
        class PackRecord
        {
            public int wave;
            public readonly List<GameObject> members = new List<GameObject>(24);
        }
        readonly List<PackRecord> packs = new List<PackRecord>(8);
        readonly Stack<PackRecord> spareRecords = new Stack<PackRecord>(8);

        readonly List<GameObject> alive = new List<GameObject>();   // every enemy this spawner sent that's still alive
        readonly List<GameObject> lastPack = new List<GameObject>(24);   // the pack just sent, Spitters included
        readonly List<Vector3> placed = new List<Vector3>(32);   // where this pack's Spitters will stand
        int packsSpawned;
        float timer;
        bool cleared, waiting;
        Transform player;

        const float WallInset = 1f;      // newcomers stand at least this far inside the play area's walls
        const float MaxStep = 1.5f;      // nor higher or lower than their spawn point by more than this
        const int SpitterRings = 6;      // rings tried around the spawn point before the rest share it

        // Read-only, for the HUD's wave counter.
        /// <summary>The wave in progress (the pack last sent), starting at 1; 0 before the first.</summary>
        public int Wave => packsSpawned;
        /// <summary>How many waves there are in all; 0 means they keep coming.</summary>
        public int TotalWaves => packsToSpawn;
        /// <summary>Enemies this spawner sent that are still alive, from any wave.</summary>
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
        /// <summary>The last wave whose enemies are all dead, and when; 0 and long ago before the first. For the HUD's WAVE N CLEARED.</summary>
        public int LastClearedWave { get; private set; }
        public float LastClearedTime { get; private set; } = -100f;
        /// <summary>The enemies of the pack just sent, Spitters included, for whatever listens to onPackSpawned (PowerUpDrops' safety net).</summary>
        public IReadOnlyList<GameObject> LastPack => lastPack;

        void Update()
        {
            // A destroyed object counts as null in Unity, so this drops the dead ones from the list
            alive.RemoveAll(scuttler => scuttler == null);
            CheckWavesCleared();

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
            int next = PackSize(packsSpawned) + BigbackCount(packsSpawned);
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

        /// <summary>A wave is cleared once every enemy it brought is dead, whatever else is still alive.</summary>
        void CheckWavesCleared()
        {
            for (int p = packs.Count - 1; p >= 0; p--)
            {
                PackRecord pack = packs[p];
                bool anyAlive = false;
                for (int i = 0; i < pack.members.Count; i++)
                    if (pack.members[i]) { anyAlive = true; break; }
                if (anyAlive) continue;
                packs.RemoveAt(p);
                spareRecords.Push(pack);
                LastClearedWave = pack.wave;
                LastClearedTime = Time.time;
                onWaveCleared.Invoke(pack.wave);
            }
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
            lastPack.Clear();
            int size = PackSize(packsSpawned);
            // A big pack splits: every other Scuttler comes from a second, different spawn point.
            int second = splitAbove > 0 && size > splitAbove && count > 1 ? (first + Random.Range(1, count)) % count : first;

            Vector3 firstAnchor = Anchor(spawnPoints[first]);
            Vector3 secondAnchor = second == first ? firstAnchor : Anchor(spawnPoints[second]);
            for (int i = 0; i < size; i++)
            {
                bool even = i % 2 == 0;
                Transform point = spawnPoints[even ? first : second];
                Vector3 anchor = even ? firstAnchor : secondAnchor;
                // Scatter each Scuttler a little around the spawn point, on ground it can walk to from there
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                Vector3 position = Walkable(anchor, anchor + new Vector3(offset.x, 0f, offset.y), out Vector3 spot) ? spot : anchor;

                GameObject scuttler = Instantiate(scuttlerPrefab, position, point.rotation);
                alive.Add(scuttler);
                lastPack.Add(scuttler);
            }

            // SPITTER: from a chosen pack on, Spitters hang back behind the pack's first spawn point
            int spitterCount = 0;
            if (spitterPrefab && packsSpawned + 1 >= spittersFromPack)
            {
                Transform spitterPoint = spawnPoints[first];
                // How many Spitters this pack gets: starts at Spitters Per Pack, then grows each pack
                int packsWithSpitters = packsSpawned + 1 - spittersFromPack;
                spitterCount = Mathf.Min(maxSpittersPerPack, spittersPerPack + spitterGrowth * packsWithSpitters);
                PlaceSpitters(firstAnchor, spitterCount);
                for (int i = 0; i < spitterCount; i++)
                {
                    GameObject spitter = Instantiate(spitterPrefab, placed[i], spitterPoint.rotation);
                    alive.Add(spitter);
                    lastPack.Add(spitter);   // a Spitter is part of its wave, for the HYPER safety net too
                }
            }

            // Use the same NavMesh and play-area checks as the other enemies.
            // Keep Bigbacks in the pack records so wave completion and drops include them.
            int bigbacks = BigbackCount(packsSpawned);
            int armoredSpawned = 0;
            if (spitterCount == 0) placed.Clear();
            for (int i = 0; i < bigbacks; i++)
            {
                int pointIndex = (first + i) % count;
                Transform point = spawnPoints[pointIndex];
                Vector3 anchor = Anchor(point);
                Vector3 position = anchor;
                bool found = false;
                for (int ring = 1; ring <= 3 && !found; ring++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        float angle = (k + i * 3) * Mathf.PI / 4f;
                        Vector3 wanted = anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (clusterRadius + ring * 2f);
                        if (Walkable(anchor, wanted, out Vector3 spot) && Clear(spot, 2f))
                        {
                            position = spot;
                            found = true;
                            break;
                        }
                    }
                }
                // If no separated spot exists, fall back to the anchor only if it is valid.
                if (!found && !Walkable(anchor, anchor, out position)) continue;
                GameObject bigback = Instantiate(bigbackPrefab, position, point.rotation);
                placed.Add(position);
                alive.Add(bigback);
                lastPack.Add(bigback);
                armoredSpawned++;
            }

            packsSpawned++;
            PackRecord record = spareRecords.Count > 0 ? spareRecords.Pop() : new PackRecord();
            record.wave = packsSpawned;
            record.members.Clear();
            record.members.AddRange(lastPack);
            packs.Add(record);
            onPackSpawned.Invoke(packsSpawned);
            Debug.Log("Pack " + packsSpawned + ": " + size + " Scuttlers, " + spitterCount + " Spitters, " + armoredSpawned + " Bigbacks");
        }

        /// <summary>The spawn point's place on the NavMesh, which every newcomer from it must be able to walk to.</summary>
        static Vector3 Anchor(Transform point)
        {
            return NavMesh.SamplePosition(point.position, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : point.position;
        }

        /// <summary>
        /// True if something can stand near <paramref name="wanted"/>, and where: on the NavMesh, inside the play area,
        /// about as high as <paramref name="anchor"/>, and with a straight walk to it from there that crosses no
        /// wall, rock or drop. A spot past a wall, inside a rock or on top of one fails.
        /// </summary>
        static bool Walkable(Vector3 anchor, Vector3 wanted, out Vector3 spot)
        {
            spot = anchor;
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 1f, NavMesh.AllAreas)) return false;
            if (Mathf.Abs(hit.position.y - anchor.y) > MaxStep) return false;
            PlayArea area = PlayArea.Current;
            if (area && !area.Contains(hit.position, -WallInset)) return false;
            if (NavMesh.Raycast(anchor, hit.position, out _, NavMesh.AllAreas)) return false;
            spot = hit.position;
            return true;
        }

        /// <summary>
        /// Fills <see cref="placed"/> with where <paramref name="count"/> Spitters stand: rings around the spawn point,
        /// from the inside out, about <see cref="spitterSpacing"/> apart, each on ground it can walk to. The half of
        /// each ring away from the player fills first, so they hang back behind the Scuttlers. If the spawn point is
        /// too hemmed in for them all, the rest share it, and their agents push them apart.
        /// </summary>
        void PlaceSpitters(Vector3 anchor, int count)
        {
            placed.Clear();
            if (!player)
            {
                var controls = FindAnyObjectByType<PlayerControls>();
                if (controls) player = controls.transform;
            }
            // The direction away from the player, as an angle; any if there's no player.
            float back = Random.value * Mathf.PI * 2f;
            if (player)
            {
                Vector3 away = anchor - player.position;
                if (away.x * away.x + away.z * away.z > 0.01f) back = Mathf.Atan2(away.z, away.x);
            }
            float apart = spitterSpacing * 0.8f;   // a spot can move a little onto the NavMesh
            for (int pass = 0; pass < 2 && placed.Count < count; pass++)   // the back halves, then the rest
            {
                for (int ring = 1; ring <= SpitterRings && placed.Count < count; ring++)
                {
                    float radius = clusterRadius + ring * spitterSpacing;
                    int slots = Mathf.Max(6, Mathf.FloorToInt(2f * Mathf.PI * radius / spitterSpacing));
                    for (int k = 0; k < slots && placed.Count < count; k++)
                    {
                        // Out from straight behind, alternating sides: 0, +1, -1, +2, -2 ...
                        int step = (k + 1) / 2 * (k % 2 == 0 ? 1 : -1);
                        float turn = step * 2f * Mathf.PI / slots;
                        if ((Mathf.Abs(turn) <= Mathf.PI * 0.5f) != (pass == 0)) continue;
                        float angle = back + turn + (ring % 2) * Mathf.PI / slots;   // every other ring staggered
                        Vector3 wanted = anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                        if (Walkable(anchor, wanted, out Vector3 spot) && Clear(spot, apart)) placed.Add(spot);
                    }
                }
            }
            while (placed.Count < count) placed.Add(anchor);
        }

        bool Clear(Vector3 spot, float apart)
        {
            for (int i = 0; i < placed.Count; i++)
                if ((placed[i] - spot).sqrMagnitude < apart * apart) return false;
            return true;
        }
    }
}