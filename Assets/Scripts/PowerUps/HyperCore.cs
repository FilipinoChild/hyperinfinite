using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// A HYPER core lying on the ground, waiting to be picked up: a spinning low-poly crystal in cyan, hovering
    /// over where it fell, with a beam of pixel squares rising off it that can be seen across the arena, rings
    /// pulsing across the ground under it and a cyan light. Walk into it to turn HYPER on. It only waits so long:
    /// it blinks for its last few seconds, then breaks up and is gone.
    ///
    /// The crystal's mesh is built in code, once, and shared by every core. PowerUpDrops makes cores from the
    /// Hyper Core prefab.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HyperCore : MonoBehaviour
    {
        [Tooltip("Seconds before it fades away if nobody picks it up.")]
        [SerializeField, Min(1f)] float lifetime = 8f;
        [Tooltip("It blinks for this many of its last seconds.")]
        [SerializeField, Min(0f)] float blinkFor = 2.5f;
        [Tooltip("How close the player must come to pick it up, in meters across the ground.")]
        [SerializeField, Min(0.2f)] float pickupRadius = 1.6f;
        [Tooltip("How high it hovers, in meters.")]
        [SerializeField, Min(0f)] float hover = 1.1f;
        [Tooltip("Degrees it turns each second.")]
        [SerializeField] float spin = 120f;
        [Tooltip("How high the beam of squares rises, in meters.")]
        [SerializeField, Min(0f)] float beamHeight = 7f;
        [Tooltip("Squares the beam sends up each second.")]
        [SerializeField, Min(0f)] float beamRate = 26f;

        public UnityEvent onPickedUp = new UnityEvent();
        public UnityEvent onFaded = new UnityEvent();

        static Mesh shape;
        Transform player;
        HyperPower hyper;
        MeshRenderer body;
        Light lamp;
        Vector3 ground;
        float born, owed, nextRing;
        int hum;

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = Shape();
            body = GetComponent<MeshRenderer>();
            var glow = new GameObject("Glow");
            glow.transform.SetParent(transform, false);
            lamp = glow.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = Palette.Cyan;
            lamp.range = 7f;
            lamp.intensity = 3f;
            lamp.shadows = LightShadows.None;
        }

        void Start()
        {
            ground = transform.position;
            born = Time.time;
            var controls = FindAnyObjectByType<PlayerControls>();
            if (controls)
            {
                player = controls.transform;
                hyper = controls.GetComponent<HyperPower>();
            }
            // It arrives with a flash and a ring, so it's noticed in the middle of a fight.
            PixelFx.Flash(ground + Vector3.up * hover, Palette.White, 1.2f, 0.08f);
            PixelFx.Ring(ground, 2.2f, Palette.Cyan, 24, 0.35f, 0.2f);
            PixelFx.Burst(ground + Vector3.up * hover, Palette.Cyan, Palette.PaleCyan, 16, 4f, 0.4f, 0.12f);
            // A chime heard across the arena, then a hum that follows it, so it can be found by ear too.
            Sfx.At(Sound.CoreDrop, ground + Vector3.up * hover);
            hum = Sfx.Loop(Sound.CoreHum, transform);
        }

        void OnDestroy()
        {
            Sfx.Stop(hum);
        }

        /// <summary>Seconds left before it fades.</summary>
        public float TimeLeft => Mathf.Max(0f, lifetime - (Time.time - born));

        /// <summary>
        /// Another core was earned while this one waits: rather than a second, this one starts its wait over, with a
        /// flash and a ring like its arrival, so the earned core is never lost.
        /// </summary>
        public void Renew()
        {
            born = Time.time - 0.4f;   // past the pop-up, so it doesn't jump
            PixelFx.Ring(ground, 2.2f, Palette.Cyan, 24, 0.35f, 0.2f);
            PixelFx.Flash(transform.position, Palette.White, 1f, 0.08f);
            PixelFx.Burst(transform.position, Palette.Cyan, Palette.PaleCyan, 12, 3.5f, 0.35f, 0.1f);
            Sfx.At(Sound.CoreDrop, transform.position);   // its chime again: another was earned
        }

        void Update()
        {
            float age = Time.time - born;
            if (age >= lifetime)
            {
                Fade();
                return;
            }

            // Pops up out of the kill, then hovers, bobbing and turning.
            float rise = 1f - (1f - Mathf.Clamp01(age / 0.35f)) * (1f - Mathf.Clamp01(age / 0.35f));
            transform.SetPositionAndRotation(ground + Vector3.up * (hover * rise + 0.12f * Mathf.Sin(age * 2.5f)),
                                             Quaternion.Euler(0f, age * spin, 0f));

            // The light breathes in steps; in its last seconds the core blinks.
            bool shown = age < lifetime - blinkFor || Mathf.Repeat(age * 6f, 1f) < 0.6f;
            body.enabled = lamp.enabled = shown;
            lamp.intensity = Mathf.Repeat(age * 2f, 1f) < 0.5f ? 3f : 2.4f;

            Beam(age);
            if (player && hyper && InReach()) PickUp();
        }

        /// <summary>A column of cyan and white squares rising off it, and rings pulsing out across the ground.</summary>
        void Beam(float age)
        {
            owed += beamRate * Time.deltaTime;
            float speed = beamHeight / 1.4f;
            while (owed >= 1f)
            {
                owed -= 1f;
                Vector2 jitter = Random.insideUnitCircle * 0.15f;
                Vector3 at = ground + new Vector3(jitter.x, Random.Range(0.3f, 1.4f), jitter.y);
                float pick = Random.value;
                Color color = pick < 0.5f ? Palette.Cyan : pick < 0.85f ? Palette.PaleCyan : Palette.White;
                PixelFx.Mote(at, Vector3.up * speed, color, Random.Range(0.08f, 0.12f), 1.4f);
            }
            if (age >= nextRing)
            {
                nextRing = age + 0.7f;
                PixelFx.Ring(ground, 1.3f, Palette.Cyan, 14, 0.4f, 0.14f);
            }
        }

        bool InReach()
        {
            Vector3 offset = player.position - ground;
            float up = offset.y;
            offset.y = 0f;
            return offset.sqrMagnitude <= pickupRadius * pickupRadius && up > -1.5f && up < 2.5f;
        }

        void PickUp()
        {
            hyper.Activate();
            Vector3 middle = transform.position;
            PixelFx.Flash(middle, Palette.White, 1.6f, 0.1f);
            PixelFx.Burst(middle, Palette.Cyan, Palette.White, 30, 7f, 0.5f, 0.16f);
            PixelFx.Ring(ground, 3.5f, Palette.PaleCyan, 30, 0.35f, 0.22f);
            onPickedUp.Invoke();
            Destroy(gameObject);
        }

        void Fade()
        {
            PixelFx.Burst(transform.position, Palette.Cyan, Palette.Steel, 14, 3f, 0.4f, 0.1f);
            Sfx.At(Sound.CoreFade, transform.position);
            onFaded.Invoke();
            Destroy(gameObject);
        }

        /// <summary>
        /// The crystal: a point on top, a six-sided girdle, a narrower waist and a point below, every face flat so it
        /// reads as low-poly like the eggs and webs. About a meter tall. Built once and shared.
        /// </summary>
        static Mesh Shape()
        {
            if (shape) return shape;
            const int sides = 6;
            var top = new Vector3(0f, 0.55f, 0f);
            var bottom = new Vector3(0f, -0.5f, 0f);
            var girdle = new Vector3[sides];
            var waist = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = a + Mathf.PI / sides;
                girdle[i] = new Vector3(Mathf.Cos(a) * 0.32f, 0.08f, Mathf.Sin(a) * 0.32f);
                waist[i] = new Vector3(Mathf.Cos(b) * 0.2f, -0.18f, Mathf.Sin(b) * 0.2f);
            }

            var vertices = new Vector3[sides * 4 * 3];
            var triangles = new int[vertices.Length];
            int n = 0;
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                // Unity treats clockwise as the front; swap so the face points away from the middle.
                Vector3 middle = (a + b + c) / 3f;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), middle) < 0f) (b, c) = (c, b);
                vertices[n] = a; vertices[n + 1] = b; vertices[n + 2] = c;
                triangles[n] = n; triangles[n + 1] = n + 1; triangles[n + 2] = n + 2;
                n += 3;
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Face(top, girdle[i], girdle[j]);
                Face(girdle[i], waist[i], girdle[j]);
                Face(girdle[j], waist[i], waist[j]);
                Face(waist[i], bottom, waist[j]);
            }

            shape = new Mesh { name = "Hyper Core", vertices = vertices, triangles = triangles };
            shape.RecalculateNormals();   // no shared corners, so every face is flat
            shape.RecalculateBounds();
            return shape;
        }
    }
}
