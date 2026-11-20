using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// The arena's playable space, and what keeps the player in it.
    ///
    /// The arena's own walls are only 6 m tall, and a blast jump with a double jump on top, a climb onto one of the
    /// tall rock spires by the walls, or a jump from an arch could clear them. So when the scene starts this raises
    /// an invisible wall up to <see cref="wallHeight"/> along each side, over the arena's own. They're on the
    /// Ignore Raycast layer: they stop the player, but shots, bashes and enemies' checks pass straight through them,
    /// so nothing sparks against an invisible wall in the sky. They have no renderer, so the NavMesh ignores them.
    ///
    /// As a last resort, PlayerRespawn checks <see cref="Contains"/>: anyone who ends up outside is put back on the
    /// last ground they stood on inside, and ground outside is never remembered as safe.
    ///
    /// Put one on the arena's root, centered on the floor. The size is the inside of the walls.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayArea : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2;

        [Tooltip("Width and depth of the playable space, from the inside face of one wall to the other, in meters.")]
        [SerializeField] Vector2 size = new Vector2(63.7f, 63.7f);
        [Tooltip("How tall the invisible walls are, from the floor. Far more than any jump, blast jump or climb can reach.")]
        [SerializeField, Min(1f)] float wallHeight = 60f;
        [Tooltip("How thick the invisible walls are, in meters, built outward from the playable space.")]
        [SerializeField, Min(0.1f)] float wallThickness = 2f;
        [Tooltip("How far past the walls counts as outside, in meters, so brushing against one never does.")]
        [SerializeField, Min(0f)] float margin = 1f;

        static PlayArea current;

        /// <summary>The play area of the scene that's running, if it has one.</summary>
        public static PlayArea Current => current;

        void Awake()
        {
            BuildWalls();
        }

        void OnEnable()
        {
            current = this;
        }

        void OnDisable()
        {
            if (current == this) current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;   // with domain reload off, nothing from the last Play carries over
        }

        /// <summary>True if <paramref name="point"/> is inside the walls, give or take the margin. Height doesn't count.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 local = point - transform.position;
            return Mathf.Abs(local.x) <= size.x * 0.5f + margin && Mathf.Abs(local.z) <= size.y * 0.5f + margin;
        }

        /// <summary>Four tall boxes just outside the playable space, overlapping at the corners so there's no gap.</summary>
        void BuildWalls()
        {
            var holder = new GameObject("Invisible Walls");
            holder.layer = IgnoreRaycastLayer;
            holder.transform.SetParent(transform, false);
            float halfX = size.x * 0.5f, halfZ = size.y * 0.5f, t = wallThickness, h = wallHeight;
            Wall(holder.transform, "North", new Vector3(0f, h * 0.5f, halfZ + t * 0.5f), new Vector3(size.x + 2f * t, h, t));
            Wall(holder.transform, "South", new Vector3(0f, h * 0.5f, -halfZ - t * 0.5f), new Vector3(size.x + 2f * t, h, t));
            Wall(holder.transform, "East", new Vector3(halfX + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, size.y + 2f * t));
            Wall(holder.transform, "West", new Vector3(-halfX - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, size.y + 2f * t));
        }

        static void Wall(Transform parent, string name, Vector3 center, Vector3 extent)
        {
            var wall = new GameObject(name);
            wall.layer = IgnoreRaycastLayer;
            wall.transform.SetParent(parent, false);
            var box = wall.AddComponent<BoxCollider>();
            box.center = center;
            box.size = extent;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.19f, 0.78f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * wallHeight * 0.5f, new Vector3(size.x, wallHeight, size.y));
        }
    }
}
