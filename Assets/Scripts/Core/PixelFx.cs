using UnityEngine;
using UnityEngine.Rendering;

namespace HyperInfinite
{
    /// <summary>
    /// The game's particle effects, in the same chunky pixel style as its sprites: energy bolts, sparks,
    /// splashes of goo, shockwave rings and flashes. Everything is a flat-colored square or bar with hard
    /// edges. Nothing fades or blurs; pieces shrink in steps and pop out, like frames of pixel animation.
    ///
    /// Call the static methods from anywhere. The first call makes the pool. Every particle lives in one
    /// mesh, so the whole lot costs a single draw call however much is going on, and nothing is allocated
    /// while playing. When the pool is full the oldest particle is reused. It draws with the built-in
    /// Sprites/Default shader, which every Unity build includes.
    /// </summary>
    public class PixelFx : MonoBehaviour
    {
        const int Capacity = 512;
        const byte KindChunk = 0, KindFlat = 1, KindBolt = 2;

        struct Particle
        {
            public Vector3 a, b;   // a chunk is at a, moving at b; a bolt runs from a to b
            public float age, life, size, gravity;
            public Color32 color;
            public byte kind;
            public bool active;
        }

        static PixelFx instance;
        static bool unavailable;

        readonly Particle[] pool = new Particle[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity * 4];
        readonly Color32[] colors = new Color32[Capacity * 4];
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        Transform view;
        int next, live;
        bool colorsChanged, linearColors;

        /// <summary>An energy bolt racing from <paramref name="from"/> to <paramref name="to"/>: a bright bar with a pale core.</summary>
        public static void Bolt(Vector3 from, Vector3 to, Color color, float width = 0.06f, float life = 0.1f)
        {
            PixelFx fx = Get();
            if (!fx) return;
            fx.Spawn(KindBolt, from, to, color, width, life, 0f);
            fx.Spawn(KindBolt, from, to, Color.Lerp(color, Color.white, 0.75f), width * 0.4f, life, 0f);
        }

        /// <summary>Small squares thrown off a surface, mostly along its normal.</summary>
        public static void Sparks(Vector3 point, Vector3 normal, Color color, int count, float speed = 6f, float life = 0.25f)
        {
            PixelFx fx = Get();
            if (!fx) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = (normal + Random.insideUnitSphere * 0.9f).normalized;
                fx.Spawn(KindChunk, point, direction * (speed * Random.Range(0.5f, 1f)), color, 0.07f, life * Random.Range(0.6f, 1f), 9f);
            }
        }

        /// <summary>
        /// Chunks thrown out in every direction, leaning upward: eggs erupting, Scuttlers bursting. They
        /// come in two colors, mixed at random, so the pieces look like bits of the sprite that burst.
        /// </summary>
        public static void Burst(Vector3 center, Color color, Color accent, int count, float speed = 6f, float life = 0.45f, float size = 0.14f)
        {
            PixelFx fx = Get();
            if (!fx) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
                fx.Spawn(KindChunk, center, direction * (speed * Random.Range(0.4f, 1f)), i % 3 == 2 ? accent : color,
                         size * Random.Range(0.6f, 1f), life * Random.Range(0.6f, 1f), 14f);
            }
        }

        /// <summary>A ring of squares spreading across the ground from <paramref name="center"/> out to <paramref name="radius"/>: a shockwave.</summary>
        public static void Ring(Vector3 center, float radius, Color color, int count = 28, float life = 0.35f, float size = 0.22f)
        {
            PixelFx fx = Get();
            if (!fx || count <= 0 || life <= 0f) return;
            center.y += 0.05f;   // just clear of the floor
            float offset = Random.value;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + offset) / count * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                fx.Spawn(KindFlat, center + direction * 0.4f, direction * ((radius - 0.4f) / life), color, size, life, 0f);
            }
        }

        /// <summary>One big square for an instant: an impact or a pop.</summary>
        public static void Flash(Vector3 point, Color color, float size = 0.4f, float life = 0.06f)
        {
            PixelFx fx = Get();
            if (fx) fx.Spawn(KindChunk, point, Vector3.zero, color, size, life, 0f);
        }

        void Spawn(byte kind, Vector3 a, Vector3 b, Color color, float size, float life, float gravity)
        {
            ref Particle p = ref pool[next];
            if (!p.active) live++;
            p.active = true;
            p.kind = kind;
            p.a = a;
            p.b = b;
            p.age = 0f;
            p.life = Mathf.Max(0.01f, life);
            p.size = size;
            p.gravity = gravity;
            p.color = linearColors ? color.linear : color;   // a mesh's colors aren't converted for us, unlike a sprite's
            int v = next * 4;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = p.color;
            colorsChanged = true;
            next = (next + 1) % Capacity;
            enabled = true;
        }

        static PixelFx Get()
        {
            if (instance) return instance;
            if (unavailable || !Application.isPlaying) return null;
            var go = new GameObject("PixelFx (effects pool)");
            instance = go.AddComponent<PixelFx>();
            if (!instance.mesh)   // Awake couldn't set it up
            {
                Destroy(go);
                instance = null;
                unavailable = true;
            }
            return instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            unavailable = false;
        }

        void Awake()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                Debug.LogWarning("PixelFx: the Sprites/Default shader is missing, so bolts, sparks and bursts are switched off.");
                return;
            }
            material = new Material(shader) { name = "PixelFx" };
            linearColors = QualitySettings.activeColorSpace == ColorSpace.Linear;

            // Every particle owns four corners of one mesh. The triangles never change; a resting
            // particle's corners all sit on one point, so it draws nothing.
            var triangles = new int[Capacity * 6];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "PixelFx" };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);   // never culled, never recalculated

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
            enabled = false;   // nothing to do until the first effect
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
            if (mesh) Destroy(mesh);
        }

        /// <summary>After everything has moved and the camera has turned, so the squares face it squarely.</summary>
        void LateUpdate()
        {
            if (!mesh) return;
            if (!view)
            {
                Camera main = Camera.main;
                if (!main) return;
                view = main.transform;
            }
            float dt = Time.deltaTime;
            Vector3 right = view.right, up = view.up, eye = view.position;

            for (int i = 0; i < Capacity; i++)
            {
                ref Particle p = ref pool[i];
                if (!p.active) continue;
                int v = i * 4;
                p.age += dt;
                float t = p.age / p.life;
                if (t >= 1f)
                {
                    p.active = false;
                    live--;
                    vertices[v] = vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = Vector3.zero;
                    continue;
                }

                if (p.kind == KindBolt)
                {
                    Vector3 head = Vector3.Lerp(p.a, p.b, Mathf.Min(1f, t * 2.5f));   // the front races out...
                    Vector3 tail = Vector3.Lerp(p.a, p.b, t);                          // ...and the back catches up
                    Vector3 side = Vector3.Cross(head - tail, eye - head).normalized * (p.size * 0.5f);
                    vertices[v] = tail - side;
                    vertices[v + 1] = tail + side;
                    vertices[v + 2] = head - side;
                    vertices[v + 3] = head + side;
                    continue;
                }

                p.b.y -= p.gravity * dt;
                p.a += p.b * dt;
                // Three sizes, like three frames of animation: whole, smaller, nearly gone.
                float half = p.size * (t < 0.5f ? 0.5f : t < 0.8f ? 0.33f : 0.17f);
                Vector3 across = p.kind == KindFlat ? Vector3.right * half : right * half;
                Vector3 along = p.kind == KindFlat ? Vector3.forward * half : up * half;
                vertices[v] = p.a - across - along;
                vertices[v + 1] = p.a - across + along;
                vertices[v + 2] = p.a + across - along;
                vertices[v + 3] = p.a + across + along;
            }

            mesh.SetVertices(vertices);
            if (colorsChanged)
            {
                mesh.SetColors(colors);
                colorsChanged = false;
            }
            meshRenderer.enabled = live > 0;
            if (live <= 0) enabled = false;   // asleep until the next effect
        }
    }
}
