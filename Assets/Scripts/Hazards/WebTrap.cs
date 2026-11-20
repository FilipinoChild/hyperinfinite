using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// HAZARD. Web Traps: a patch of sticky webbing on the ground. Walking into it slows the player to a
    /// crawl, weakens the dash and stops them jumping, until they shoot through it. Aim down and fire:
    /// a solid blast tears it apart, and it stays gone unless Regrow Time says otherwise. Enemies cross
    /// it freely.
    ///
    /// Like the eggs, the web is a solid low-poly object in the world, not a sprite. This builds its
    /// mesh when play starts: chunky strands running out from a hub, with rings sagging between them,
    /// pinned down at a few points around the rim by spikes of silk tall enough to spot from a distance. Every web is laid out a little differently, from its Seed or, with Seed at 0, from
    /// where it sits. In the Scene view it shows as a wire drawing of the same strands, so you can see
    /// what you're placing. Its effects are PixelFx squares in the game's palette.
    ///
    /// It needs a MeshFilter, a MeshRenderer with the web's material, and a BoxCollider on the same
    /// object; the Web Trap prefab has them. The collider is a trigger, so it never blocks anyone, and
    /// it's what bolts hit. Leave the object's scale alone and set Radius instead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class WebTrap : MonoBehaviour, IDamageable
    {
        [Header("The web")]
        [Tooltip("How far the web reaches from its middle, in meters.")]
        [SerializeField, Min(0.5f)] float radius = 2.4f;
        [Tooltip("Picks this web's layout. 0 takes it from the web's position, so every web placed looks different.")]
        [SerializeField] int seed;

        [Header("Being caught")]
        [Tooltip("The share of their normal speed the player keeps while caught: 0.35 is 35%.")]
        [SerializeField, Range(0.05f, 1f)] float speedScale = 0.35f;
        [Tooltip("How far above the web the player's feet can be and still be caught, in meters. Jumping over it clears this.")]
        [SerializeField, Min(0.1f)] float catchHeight = 0.75f;

        [Header("Shooting through it")]
        [Tooltip("Damage it takes to tear the web apart. One solid blast up close does about 80.")]
        [SerializeField, Min(1f)] float health = 45f;
        [Tooltip("Seconds until a torn web grows back. 0 leaves it torn for good.")]
        [SerializeField, Min(0f)] float regrowTime;

        [Header("Events (hook sounds up here)")]
        [Tooltip("The player has just walked into the web.")]
        public UnityEvent onCaught = new UnityEvent();
        [Tooltip("The web has been shot through.")]
        public UnityEvent onTorn = new UnityEvent();

        const float Lift = 0.03f;            // how far the strands sit above the floor, so they never flicker against it
        const float PuffEvery = 0.6f;        // meters of struggling between puffs of snapped strands

        /// <summary>One strand of the web, from a to b in the web's own space, with its width at each end and its height.</summary>
        struct Strand
        {
            public Vector3 a, b;
            public float widthA, widthB, height;
        }

        const int MostSpokes = 10;

        static readonly List<Strand> plan = new List<Strand>(128);
        static readonly Vector3[] tips = new Vector3[MostSpokes];
        static readonly List<Vector3> spikes = new List<Vector3>(MostSpokes);   // where each silk spike stands; y is its height
        static readonly List<Vector3> vertices = new List<Vector3>(1024);
        static readonly List<int> triangles = new List<int>(1536);
        static PlayerMotor sharedPlayer;      // found once and shared by every web
        static float nextPlayerSearch;

        MeshRenderer meshRenderer;
        BoxCollider box;
        Mesh mesh;
        Vector3 restScale;
        float strength, size = 1f, shownScale = 1f, shake, regrowTimer, struggle;
        bool holding;

        /// <summary>True from being shot through until it has grown back.</summary>
        public bool IsTorn { get; private set; }
        /// <summary>True while the player is caught in this web.</summary>
        public bool IsHoldingPlayer => holding;
        public float Radius => radius;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            box = GetComponent<BoxCollider>();
            restScale = transform.localScale;
            strength = health;

            // A flat trigger over the web: bolts hit it, nothing bumps into it.
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(radius * 1.7f, 0.4f, radius * 1.7f);

            mesh = BuildMesh();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Animate(dt);

            if (IsTorn)
            {
                if (regrowTime <= 0f) return;
                regrowTimer -= dt;
                if (regrowTimer <= 0f) Regrow();
                return;
            }

            PlayerMotor player = Player();
            if (!player) return;
            Vector3 feet = player.transform.position;
            Vector3 offset = feet - transform.position;
            bool inside = offset.y > -0.5f && offset.y < catchHeight && offset.x * offset.x + offset.z * offset.z <= radius * radius;
            if (!inside)
            {
                holding = false;
                return;
            }

            player.Snare(speedScale);   // asked again every frame, so it lets go the moment they're out or it's torn
            if (!holding)
            {
                holding = true;
                struggle = PuffEvery;
                PixelFx.Sparks(feet + Vector3.up * 0.1f, Vector3.up, Palette.White, 6, 3f, 0.3f);
                onCaught.Invoke();
            }
            // Strands snap around the feet as they drag themselves through it.
            struggle -= player.Speed * dt;
            if (struggle <= 0f)
            {
                struggle = PuffEvery;
                PixelFx.Sparks(feet + Vector3.up * 0.1f, Vector3.up, Palette.White, 2, 2.5f, 0.25f);
            }
        }

        /// <summary>Blasts and melee tear at the web. Bites, bursts and other hazards leave it alone.</summary>
        public void TakeDamage(DamageInfo damage)
        {
            if (IsTorn) return;
            if (damage.Kind == DamageKind.Bite || damage.Kind == DamageKind.Burst || damage.Kind == DamageKind.Hazard) return;
            strength -= damage.Amount;
            shake = 0.15f;
            PixelFx.Sparks(damage.Point, Vector3.up, Palette.White, 4, 4f, 0.3f);
            if (strength <= 0f) Tear();
        }

        void Tear()
        {
            IsTorn = true;
            holding = false;
            box.enabled = false;
            regrowTimer = regrowTime;
            Vector3 middle = transform.position + Vector3.up * 0.2f;
            PixelFx.Burst(middle, Palette.White, Palette.Dim, 26, 5f, 0.5f, 0.14f);
            PixelFx.Ring(transform.position, radius, Palette.White, 20, 0.25f, 0.16f);
            onTorn.Invoke();
        }

        void Regrow()
        {
            IsTorn = false;
            strength = health;
            box.enabled = true;
            meshRenderer.enabled = true;
        }

        /// <summary>
        /// The web shrinks away in a few steps when torn and grows back the same way, and jolts when
        /// shot. In steps, like the rest of the game's animation.
        /// </summary>
        void Animate(float dt)
        {
            size = Mathf.MoveTowards(size, IsTorn ? 0f : 1f, dt * (IsTorn ? 6f : 1.5f));
            shake = Mathf.Max(0f, shake - dt);
            float scale = Mathf.Ceil(size * 4f) / 4f * (shake > 0f ? 1.05f : 1f);
            if (Mathf.Approximately(scale, shownScale)) return;
            shownScale = scale;
            if (scale <= 0f)
            {
                meshRenderer.enabled = false;   // gone
                return;
            }
            transform.localScale = restScale * scale;
        }

        static PlayerMotor Player()
        {
            if (sharedPlayer || Time.time < nextPlayerSearch) return sharedPlayer;
            nextPlayerSearch = Time.time + 1f;   // look again each second until there is one
            sharedPlayer = FindAnyObjectByType<PlayerMotor>();
            return sharedPlayer;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sharedPlayer = null;
            nextPlayerSearch = 0f;
        }

        // ---- The web's shape -------------------------------------------------------------------

        /// <summary>
        /// Lays the web out as a list of strands: spokes running out from the middle, and rings of short
        /// strands sagging between them, with the odd one missing. Every third spoke ends at a spike of
        /// silk. The same radius and seed always give the same web.
        /// </summary>
        void Plan()
        {
            plan.Clear();
            spikes.Clear();
            uint state = (uint)(seed != 0 ? seed : Mathf.RoundToInt(transform.position.x * 73f) * 31 + Mathf.RoundToInt(transform.position.z * 191f));
            state = state * 747796405u + 2891336453u;

            int spokes = Mathf.Min(MostSpokes, 8 + (int)(Next(ref state) * 3f));   // 8 to 10
            for (int i = 0; i < spokes; i++)
            {
                float angle = (i + (Next(ref state) - 0.5f) * 0.5f) / spokes * Mathf.PI * 2f;
                float reach = radius * (0.82f + 0.18f * Next(ref state));
                tips[i] = new Vector3(Mathf.Cos(angle) * reach, Lift, Mathf.Sin(angle) * reach);
                plan.Add(new Strand { a = new Vector3(0f, Lift, 0f), b = tips[i], widthA = 0.22f, widthB = 0.1f, height = 0.1f });
                float tall = 0.55f + 0.4f * Next(ref state);
                if (i % 3 == 0) spikes.Add(new Vector3(tips[i].x, tall, tips[i].z));
            }

            for (int ring = 0; ring < 4; ring++)
            {
                float along = 0.22f + 0.2f * ring;   // how far out along each spoke this ring sits
                for (int i = 0; i < spokes; i++)
                {
                    float sag = 0.1f + 0.08f * Next(ref state);
                    if (Next(ref state) < 0.12f) continue;   // a gap in the web
                    Vector3 from = Vector3.Lerp(new Vector3(0f, Lift, 0f), tips[i], along);
                    Vector3 to = Vector3.Lerp(new Vector3(0f, Lift, 0f), tips[(i + 1) % spokes], along);
                    Vector3 middle = (from + to) * (0.5f * (1f - sag));   // pulled in toward the hub
                    middle.y = Lift;
                    plan.Add(new Strand { a = from, b = middle, widthA = 0.11f, widthB = 0.11f, height = 0.06f });
                    plan.Add(new Strand { a = middle, b = to, widthA = 0.11f, widthB = 0.11f, height = 0.06f });
                }
            }
        }

        /// <summary>A small repeatable random number from 0 up to (not including) 1.</summary>
        static float Next(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) / 16777216f;
        }

        /// <summary>
        /// Turns the plan into a mesh. Each strand is a low ridge, two sloping faces meeting along the
        /// top; the hub is a flat-sided mound and the spikes are thin four-sided points. No two faces
        /// share corners, so every face is shaded flat, like the rest of the world.
        /// </summary>
        Mesh BuildMesh()
        {
            Plan();
            vertices.Clear();
            triangles.Clear();

            for (int i = 0; i < plan.Count; i++)
            {
                Strand s = plan[i];
                Vector3 along = s.b - s.a;
                along.y = 0f;
                if (along.sqrMagnitude < 1e-6f) continue;
                Vector3 side = new Vector3(-along.z, 0f, along.x).normalized;
                Vector3 up = Vector3.up * s.height;
                Vector3 aLeft = s.a - side * (s.widthA * 0.5f), aRight = s.a + side * (s.widthA * 0.5f);
                Vector3 bLeft = s.b - side * (s.widthB * 0.5f), bRight = s.b + side * (s.widthB * 0.5f);
                Face(aLeft, s.a + up, s.b + up, bLeft);
                Face(s.a + up, aRight, bRight, s.b + up);
            }

            // The hub: six flat sides rising to a point.
            Point(Vector3.zero, 0.4f, 0.16f, 6);
            // The spikes of silk around the rim.
            for (int i = 0; i < spikes.Count; i++)
                Point(new Vector3(spikes[i].x, 0f, spikes[i].z), 0.16f, spikes[i].y, 4);

            var built = new Mesh { name = "Web" };
            built.SetVertices(vertices);
            built.SetTriangles(triangles, 0);
            built.RecalculateNormals();
            built.RecalculateBounds();
            return built;
        }

        /// <summary>A flat-sided point standing on the floor at <paramref name="foot"/>: a mound if it's wide, a spike if it's tall.</summary>
        static void Point(Vector3 foot, float width, float height, int sides)
        {
            foot.y = Lift;
            Vector3 top = foot + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Triangle(top, foot + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * width,
                              foot + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * width);
            }
        }

        /// <summary>A four-cornered face, as two triangles.</summary>
        static void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        /// <summary>One triangle with its own three corners, turned so that it faces up.</summary>
        static void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
            }
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        // ---- In the Scene view -----------------------------------------------------------------

        /// <summary>The web isn't built until play starts, so the Scene view shows a wire drawing of the same strands.</summary>
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;   // the real one is there to see
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.95f, 0.93f, 0.96f, 0.9f);
            Plan();
            for (int i = 0; i < plan.Count; i++) Gizmos.DrawLine(plan[i].a, plan[i].b);
            for (int i = 0; i < spikes.Count; i++)
                Gizmos.DrawLine(new Vector3(spikes[i].x, 0f, spikes[i].z), spikes[i]);
        }

        void OnDrawGizmosSelected()
        {
            // The space the player is caught in: the web's reach, up to the catch height.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.19f, 0.78f, 1f, 0.9f);
            const int Steps = 32;
            Vector3 last = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Steps; i++)
            {
                float angle = i * Mathf.PI * 2f / Steps;
                var next = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(last, next);
                Gizmos.DrawLine(last + Vector3.up * catchHeight, next + Vector3.up * catchHeight);
                last = next;
            }
        }
    }
}
