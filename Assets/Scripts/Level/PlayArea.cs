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
    /// No perching: nothing can reach you higher than <see cref="perchHeight"/> above the floor (a Scuttler pounces
    /// about 3.2 m, the Hive Mother's slam reaches 2.5 m), so standing that high, on the tall pillars, the rock
    /// spires or the tops of the arches, slides you off, a little hop outward toward the middle of the arena at a
    /// time, with a spray of grit. Jumping through that space is fine; only standing there isn't. The terraces,
    /// mesas, boulders and cover up to 3 m tall are all below it.
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

        [Header("No perching")]
        [Tooltip("Standing higher than this above the floor slides you off: up there nothing could reach you. Scuttlers pounce about 3.2 m. 0 allows standing anywhere.")]
        [SerializeField, Min(0f)] float perchHeight = 3.3f;
        [Tooltip("How hard each nudge off a perch is, in meters per second, outward and a little up.")]
        [SerializeField, Min(0f)] float slideSpeed = 6f;

        static PlayArea current;
        static readonly RaycastHit[] below = new RaycastHit[8];
        PlayerMotor player;
        float nextSearch, nextNudge, lastY, stillFor;

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

        /// <summary>A player standing higher than anything can reach is nudged off, toward the middle of the arena.</summary>
        void Update()
        {
            if (perchHeight <= 0f) return;
            if (!player)
            {
                if (Time.time < nextSearch) return;
                nextSearch = Time.time + 1f;
                player = FindAnyObjectByType<PlayerMotor>();
                if (!player) return;
            }
            Vector3 feet = player.transform.position;
            bool still = Mathf.Abs(feet.y - lastY) < 0.02f;
            lastY = feet.y;
            if (feet.y - transform.position.y <= perchHeight)
            {
                stillFor = 0f;
                return;
            }
            stillFor = still ? stillFor + Time.deltaTime : 0f;
            if (Time.time < nextNudge || player.IsDashing) return;   // a level air dash passes through, it doesn't stand
            if (player.Velocity.y > 0.5f) return;                    // already on the way up: never hop someone who's rising
            if (!Resting(feet)) return;

            nextNudge = Time.time + 0.12f;
            stillFor = 0f;
            Vector3 inward = transform.position - feet;
            inward.y = 0f;
            if (inward.sqrMagnitude < 1f) inward = player.transform.forward;   // right in the middle: any way off will do
            inward.y = 0f;
            // A hop as well as a shove: on the ground the motor soaks up sideways speed, in the air it carries. The hop
            // sets the climb outright, so being wedged (when the motor's fall speed keeps building) doesn't swallow it.
            player.Launch(3f);
            player.Push(inward.normalized * slideSpeed);
            PixelFx.Burst(feet + Vector3.up * 0.1f, Palette.Steel, Palette.Dim, 6, 2.5f, 0.3f, 0.06f);   // grit kicked off the edge
        }

        /// <summary>
        /// True if something up here is holding the player: the motor says they're on the ground, something solid is
        /// right under their feet, or they've stayed at the same height for a moment. The last two catch the tip of a
        /// spire or a crack between steep rock faces, which hold you up without the character controller calling it
        /// ground. A jump's peak passes in a tenth of a second, so it doesn't count.
        /// </summary>
        bool Resting(Vector3 feet)
        {
            if (player.IsGrounded || stillFor > 0.25f) return true;
            // The sweep starts inside the player's own body, so its own collider has to be skipped, or it would always
            // find "ground" under itself.
            Transform self = player.transform;
            int count = Physics.SphereCastNonAlloc(feet + Vector3.up * 0.45f, 0.35f, Vector3.down, below, 0.55f,
                                                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = below[i].collider;
                if (c && !c.transform.IsChildOf(self)) return true;
            }
            return false;
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
