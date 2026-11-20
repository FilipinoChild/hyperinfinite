using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace HyperInfinite
{
    /// <summary>
    /// Kill an enemy while it's branded and it bursts into a few small heart shards that fly to you and heal
    /// you, so the brand-and-bash loop keeps you alive as well as clearing the room. It counts however the
    /// branded enemy dies: a blast, a bash, a heavy bash or a brand's own burst.
    ///
    /// The shards are pixel hearts like the HUD's, green with a pale shine and a black outline like the
    /// creatures. Each pops out of the body, hangs for a moment, then curves in to you faster and faster,
    /// leaving green specks behind. Going down makes any still flying break up.
    ///
    /// Put it on the Player, next to Health; the ShotgunMelee under the camera says who's branded. Deaths come
    /// from Health's static AnyDied event, so nothing searches the scene. Every shard is four corners of one
    /// mesh drawn with one material, so all of them together are a single draw call, and nothing is allocated
    /// while playing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class HeartShards : MonoBehaviour
    {
        [Tooltip("Shards each branded kill drops.")]
        [SerializeField, Range(1, 6)] int perKill = 3;
        [Tooltip("Health each shard gives back.")]
        [SerializeField, Min(0f)] float healEach = 2f;
        [Tooltip("Seconds a shard hangs where it popped out before it flies to you.")]
        [SerializeField, Min(0f)] float popTime = 0.4f;
        [Tooltip("How fast a shard flies in at its fastest, in meters per second.")]
        [SerializeField, Min(1f)] float flySpeed = 20f;
        [Tooltip("How wide a shard is, in meters.")]
        [SerializeField, Min(0.05f)] float size = 0.36f;

        [Header("Events (hook sounds up here)")]
        [Tooltip("A branded kill dropped shards.")]
        public UnityEvent onDropped = new UnityEvent();
        [Tooltip("A shard reached you.")]
        public UnityEvent onCollected = new UnityEvent();

        const int Capacity = 36;            // three each from all twelve brands at once
        const int ArtWidth = 9, ArtHeight = 8;   // the heart with its outline
        const float Gravity = 12f, Reach = 0.5f, MostTime = 4f;

        struct Shard
        {
            public Vector3 position, velocity;
            public float age, speed, nextSpeck;
            public bool active;
        }

        readonly Shard[] shards = new Shard[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity * 4];
        readonly Vector2[] uvs = new Vector2[Capacity * 4];
        Health health;
        ShotgunMelee melee;
        Transform view;
        GameObject holder;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        Texture2D texture;
        int live;

        /// <summary>Shards flying right now.</summary>
        public int Flying => live;

        void Awake()
        {
            health = GetComponent<Health>();
            melee = GetComponentInChildren<ShotgunMelee>();
            Build();
            Health.AnyDied += OnDied;   // listens for as long as it exists: it switches itself off between drops
        }

        void OnDestroy()
        {
            Health.AnyDied -= OnDied;
            if (holder) Destroy(holder);
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
            if (texture) Destroy(texture);
        }

        void OnDied(Health died)
        {
            if (died == health || !melee || !melee.IsBranded(died)) return;
            Collider body = died.GetComponentInChildren<Collider>();
            Drop(body ? body.bounds.center : died.transform.position + Vector3.up * 0.5f);
        }

        /// <summary>Pops shards out at <paramref name="point"/>, spread evenly round it. With the pool full, the rest are skipped.</summary>
        public void Drop(Vector3 point)
        {
            if (!mesh || health.IsDead) return;
            float turn = Random.Range(0f, 360f);
            int dropped = 0;
            for (int i = 0; i < Capacity && dropped < perKill; i++)
            {
                ref Shard s = ref shards[i];
                if (s.active) continue;
                Vector3 outward = Quaternion.Euler(0f, turn + 360f * dropped / perKill + Random.Range(-20f, 20f), 0f) * Vector3.forward;
                s.active = true;
                s.position = point;
                s.velocity = outward * Random.Range(2f, 3f) + Vector3.up * Random.Range(3.5f, 4.5f);
                s.age = 0f;
                s.speed = 0f;
                s.nextSpeck = popTime;
                dropped++;
                live++;
            }
            if (dropped == 0) return;
            PixelFx.Flash(point, Palette.PaleGreen, 0.35f, 0.06f);
            PixelFx.Burst(point, Palette.Green, Palette.PaleGreen, 10, 4f, 0.3f, 0.1f);
            Sfx.At(Sound.HeartDrop, point);
            meshRenderer.enabled = true;
            enabled = true;
            onDropped.Invoke();
        }

        /// <summary>After the player and camera have moved, so the shards chase where you are and face you squarely.</summary>
        void LateUpdate()
        {
            if (live <= 0)
            {
                meshRenderer.enabled = false;
                enabled = false;   // asleep until the next drop
                return;
            }
            if (!view)
            {
                Camera main = Camera.main;
                if (!main) return;
                view = main.transform;
            }

            float dt = Time.deltaTime;
            Vector3 chest = transform.position + Vector3.up * 1.1f;
            Vector3 right = view.right, up = view.up;
            float halfWidth = size * 0.5f, halfHeight = halfWidth * ArtHeight / ArtWidth;
            bool down = health.IsDead;

            for (int i = 0; i < Capacity; i++)
            {
                ref Shard s = ref shards[i];
                int v = i * 4;
                if (!s.active) continue;
                s.age += dt;

                if (down)
                {
                    PixelFx.Burst(s.position, Palette.Green, Palette.Ink, 4, 2f, 0.25f, 0.06f);   // breaks up
                    Retire(ref s, v);
                    continue;
                }

                if (s.age < popTime)   // thrown out of the body, slowing as it rises
                {
                    s.velocity.y -= Gravity * dt;
                    s.velocity *= 1f - 2f * dt;
                    s.position += s.velocity * dt;
                }
                else   // then in to you, faster and faster, curving round from the way it was going
                {
                    Vector3 to = chest - s.position;
                    float distance = to.magnitude;
                    s.speed = Mathf.Min(flySpeed, s.speed + flySpeed * 3f * dt);
                    Vector3 want = distance > 1e-4f ? to / distance * s.speed : Vector3.zero;
                    s.velocity = Vector3.Lerp(s.velocity, want, 1f - Mathf.Exp(-10f * dt));
                    float step = s.velocity.magnitude * dt;
                    if (distance <= Reach + step || s.age > popTime + MostTime)
                    {
                        Collect(ref s, v);
                        continue;
                    }
                    s.position += s.velocity * dt;
                    if (s.age >= s.nextSpeck)
                    {
                        s.nextSpeck = s.age + 0.06f;
                        PixelFx.Mote(s.position, Vector3.zero, (i + (int)(s.age * 16f)) % 3 == 0 ? Palette.PaleGreen : Palette.Green, 0.06f, 0.2f);
                    }
                }

                // A glint now and then, each shard on its own beat, and a bigger first frame as it pops out.
                bool bright = s.age < 0.08f || ((int)((s.age + i * 0.37f) * 8f)) % 4 == 0;
                float grow = s.age < 0.08f ? 1.3f : 1f;
                Vector3 across = right * (halfWidth * grow), along = up * (halfHeight * grow);
                vertices[v] = s.position - across - along;
                vertices[v + 1] = s.position - across + along;
                vertices[v + 2] = s.position + across - along;
                vertices[v + 3] = s.position + across + along;
                SetFrame(v, bright);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
        }

        void Collect(ref Shard s, int v)
        {
            health.Heal(healEach);
            PixelFx.Burst(s.position, Palette.Green, Palette.PaleGreen, 4, 2f, 0.2f, 0.05f);
            Retire(ref s, v);
            onCollected.Invoke();
        }

        void Retire(ref Shard s, int v)
        {
            s.active = false;
            live--;
            vertices[v] = vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = Vector3.zero;   // a shard at rest draws nothing
        }

        /// <summary>The heart's two frames sit side by side in the texture: plain, then bright.</summary>
        void SetFrame(int v, bool bright)
        {
            float left = bright ? 0.5f : 0f, rightEdge = left + 0.5f;
            uvs[v] = new Vector2(left, 0f);
            uvs[v + 1] = new Vector2(left, 1f);
            uvs[v + 2] = new Vector2(rightEdge, 0f);
            uvs[v + 3] = new Vector2(rightEdge, 1f);
        }

        // ---- Making it --------------------------------------------------------------------------

        void Build()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                Debug.LogWarning("HeartShards: the Sprites/Default shader is missing, so branded kills drop no hearts.");
                enabled = false;
                return;
            }
            texture = BuildTexture();
            material = new Material(shader) { name = "Heart Shards", mainTexture = texture };

            var triangles = new int[Capacity * 6];
            var colors = new Color32[Capacity * 4];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = v + 3;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = new Color32(255, 255, 255, 255);
                SetFrame(v, false);
            }
            mesh = new Mesh { name = "Heart Shards" };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);   // never culled, never recalculated

            holder = new GameObject("Heart Shards");
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = holder.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
            enabled = false;   // nothing to do until the first drop
        }

        /// <summary>
        /// The heart from PixelArt, colored in: green with a pale shine, a black outline round it, and a brighter
        /// copy beside it for the glints.
        /// </summary>
        static Texture2D BuildTexture()
        {
            var pixels = new Color32[ArtWidth * 2 * ArtHeight];
            Color32 ink = Palette.Ink, green = Palette.Green, pale = Palette.PaleGreen, white = Palette.White, clear = new Color32(0, 0, 0, 0);
            for (int frame = 0; frame < 2; frame++)
            {
                Color32 fill = frame == 0 ? green : pale, shine = frame == 0 ? pale : white;
                for (int y = 0; y < ArtHeight; y++)
                    for (int x = 0; x < ArtWidth; x++)
                    {
                        int row = ArtHeight - 1 - y;   // textures start at the bottom row
                        Color32 c = clear;
                        if (Heart(x - 1, row - 1)) c = Shine(x - 1, row - 1) ? shine : fill;
                        else if (Heart(x - 2, row - 1) || Heart(x, row - 1) || Heart(x - 1, row - 2) || Heart(x - 1, row)) c = ink;
                        pixels[y * ArtWidth * 2 + frame * ArtWidth + x] = c;
                    }
            }
            var texture = new Texture2D(ArtWidth * 2, ArtHeight, TextureFormat.RGBA32, false)
            {
                name = "Heart Shard", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool Heart(int x, int row) => Lit(PixelArt.HeartRows, x, row);
        static bool Shine(int x, int row) => Lit(PixelArt.HeartShineRows, x, row);

        static bool Lit(string[] rows, int x, int row)
        {
            return row >= 0 && row < rows.Length && x >= 0 && x < rows[row].Length && rows[row][x] == '#';
        }
    }
}
