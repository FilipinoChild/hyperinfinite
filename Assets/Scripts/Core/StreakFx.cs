using UnityEngine;
using UnityEngine.Rendering;

namespace HyperInfinite
{
    /// <summary>
    /// Cheap streak effects (tracers, sparks, splashes of goo) drawn with a pool of LineRenderers.
    /// Call the static methods from anywhere. The pool makes itself the first time it's needed,
    /// using the built-in Sprites/Default shader, which draws in both the Built-in pipeline and URP.
    /// </summary>
    public class StreakFx : MonoBehaviour
    {
        const int PoolSize = 160;

        struct Streak
        {
            public LineRenderer line;
            public bool active, tracer;
            public Vector3 a, b;   // a tracer runs from a to b; a spark is at a, moving at velocity b
            public float age, life, width, length, gravity;
            public Color color;
        }

        static StreakFx instance;
        static bool unavailable;

        Streak[] pool;
        int next;
        Material material;

        /// <summary>A streak racing from <paramref name="from"/> to <paramref name="to"/>, like a pellet's path.</summary>
        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.02f, float life = 0.09f)
        {
            var fx = Get();
            if (!fx) return;
            ref Streak s = ref fx.Take();
            s.tracer = true;
            s.a = from;
            s.b = to;
            s.life = life;
            s.width = width;
            s.length = 0f;
            s.gravity = 0f;
            s.color = color;
        }

        /// <summary>Sparks bouncing off a surface, mostly along its normal.</summary>
        public static void Sparks(Vector3 point, Vector3 normal, Color color, int count, float speed = 6f, float life = 0.25f)
        {
            var fx = Get();
            if (!fx) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (normal + Random.insideUnitSphere * 0.9f).normalized;
                fx.Fling(point, dir * (speed * Random.Range(0.5f, 1f)), color, life * Random.Range(0.6f, 1f), 0.025f, 0.12f, 9f);
            }
        }

        /// <summary>A splash thrown out in every direction, leaning upward: eggs bursting, larvae splatting.</summary>
        public static void Burst(Vector3 center, Color color, int count, float speed = 6f, float life = 0.45f)
        {
            var fx = Get();
            if (!fx) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
                fx.Fling(center, dir * (speed * Random.Range(0.4f, 1f)), color, life * Random.Range(0.6f, 1f), 0.06f, 0.22f, 14f);
            }
        }

        void Fling(Vector3 position, Vector3 velocity, Color color, float life, float width, float length, float gravity)
        {
            ref Streak s = ref Take();
            s.tracer = false;
            s.a = position;
            s.b = velocity;
            s.life = life;
            s.width = width;
            s.length = length;
            s.gravity = gravity;
            s.color = color;
        }

        static StreakFx Get()
        {
            if (instance) return instance;
            if (unavailable || !Application.isPlaying) return null;
            var go = new GameObject("StreakFx (effects pool)");
            instance = go.AddComponent<StreakFx>();
            if (instance.pool == null)
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
                Debug.LogWarning("StreakFx: the Sprites/Default shader is missing, so tracers and sparks are switched off.");
                return;
            }
            material = new Material(shader) { name = "StreakFx" };
            pool = new Streak[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Streak");
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                pool[i].line = line;
            }
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
        }

        ref Streak Take()
        {
            ref Streak s = ref pool[next];
            next = (next + 1) % pool.Length;
            s.active = true;
            s.age = 0f;
            s.line.enabled = true;
            return ref s;
        }

        void Update()
        {
            if (pool == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < pool.Length; i++)
            {
                ref Streak s = ref pool[i];
                if (!s.active) continue;
                s.age += dt;
                float t = s.life > 0f ? s.age / s.life : 1f;
                if (t >= 1f)
                {
                    s.active = false;
                    s.line.enabled = false;
                    continue;
                }

                Vector3 head, tail;
                if (s.tracer)
                {
                    head = Vector3.Lerp(s.a, s.b, Mathf.Min(1f, t * 2.5f));   // the front races out...
                    tail = Vector3.Lerp(s.a, s.b, t);                          // ...and the back catches up
                }
                else
                {
                    s.b += Vector3.down * (s.gravity * dt);
                    s.a += s.b * dt;
                    head = s.a;
                    float speed = s.b.magnitude;
                    Vector3 along = speed > 0.001f ? s.b / speed : Vector3.up;
                    tail = head - along * (s.length * (1f - t));
                }

                s.line.SetPosition(0, tail);
                s.line.SetPosition(1, head);
                Color front = s.color;
                front.a *= 1f - t;
                Color back = front;
                back.a *= 0.25f;
                s.line.startColor = back;
                s.line.endColor = front;
                s.line.startWidth = s.width * 0.5f;
                s.line.endWidth = s.width;
            }
        }
    }
}
