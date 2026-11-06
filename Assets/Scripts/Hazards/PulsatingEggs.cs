using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    public enum EggState { Dormant, Swelling, Spent }

    /// <summary>
    /// For whatever Pulsating Eggs release. TinyScuttler implements it, and the
    /// eggs tell each one whom to chase and throw it out in a leap. Anything without it just appears
    /// beside the eggs.
    /// </summary>
    public interface IHatchling
    {
        void Hatch(Transform target, Vector3 launchVelocity);
    }

    /// <summary>
    /// HAZARD 1. Pulsating Arachturn Eggs, which "erupt into a swarm of
    /// Scuttlers if shot, touched, or walked past".
    ///
    /// A dormant cluster pulses quietly. Walk past and it starts to wake, faster the closer you are, and
    /// its pulse quickens and reddens to show it. Fully awake, it swells for a moment (the warning) and
    /// then erupts. Touch it or shoot it and it erupts at once, with no warning. The eruption splashes and
    /// shoves anyone right next to it. Move away before it's fully awake and it settles down again.
    /// 
    /// HAZARD UNIQUE MECHANIC (1): The eruption releases a swarm of tiny Scuttlers (TinyScuttler),
    /// hatchlings far smaller than the regular Scuttler, drawn with the same pixel art. The eggs spawn
    /// whatever prefab is in Scuttler Prefab. Set them off from range with a blast and the swarm has to
    /// cross open ground to reach you. Walk past and it bursts out at your feet.
    /// </summary>
    [DisallowMultipleComponent]
    public class PulsatingEggs : MonoBehaviour, IDamageable
    {
        [Header("Wiring")]
        [Tooltip("The egg meshes: they pulse, swell, erupt and grow back.")]
        [SerializeField] Transform[] eggs = new Transform[0];
        [Tooltip("The solid collider shots hit. It's switched off once the eggs have erupted.")]
        [SerializeField] Collider hitbox;

        [Header("Walked past")]
        [Tooltip("Walking closer than this wakes the eggs. The closer you are, the faster.")]
        [SerializeField, Min(0.1f)] float senseRadius = 5f;
        [Tooltip("How much they wake per second with someone right beside them. They swell at 1. At 4, running past within about 2.5 m sets them off, and the edge of the radius only stirs them.")]
        [SerializeField, Min(0f)] float proximityRate = 4f;
        [Tooltip("How much they settle per second, once nobody has been near for a second.")]
        [SerializeField, Min(0f)] float calmRate = 0.25f;
        [Tooltip("Seconds of swelling before eggs woken this way erupt. This is the player's warning.")]
        [SerializeField, Min(0f)] float warningTime = 0.8f;

        [Header("Touched")]
        [Tooltip("Getting this close to the middle of the cluster, across the ground, counts as touching it: it erupts at once. The eggs' own collider stops the player about 1.25 m out.")]
        [SerializeField, Min(0f)] float touchRadius = 1.4f;

        [Header("Eruption")]
        [Tooltip("The eruption hurts anyone this close.")]
        [SerializeField, Min(0f)] float burstRadius = 2.5f;
        [SerializeField, Min(0f)] float burstDamage = 10f;
        [Tooltip("Knock-back from the eruption, in meters per second.")]
        [SerializeField, Min(0f)] float burstPush = 7f;

        [Header("Swarm (the unique mechanic)")]
        [Tooltip("What erupts out: the Tiny Scuttler prefab (Prefabs/Hazards).")]
        [SerializeField] GameObject scuttlerPrefab;
        [Tooltip("How many tiny Scuttlers each eruption releases.")]
        [SerializeField, Range(0, 30)] int swarmSize = 10;

        [Header("Regrowing")]
        [Tooltip("Seconds until erupted eggs grow back. 0 means never; the test level uses 12.")]
        [SerializeField, Min(0f)] float regrowTime = 0f;

        [Header("Look")]
        [Tooltip("The dormant glow: the original prototype's dark crimson egg glow, #5a1030.")]
        [SerializeField, ColorUsage(false, true)] Color calmGlow = new Color(0.353f, 0.063f, 0.188f);
        [Tooltip("The faint red a dormant egg's glow gains at the top of each swell.")]
        [SerializeField, ColorUsage(false, true)] Color calmPulse = new Color(0.3f, 0.02f, 0.04f);
        [SerializeField, ColorUsage(false, true)] Color angryGlow = new Color(2.4f, 0.3f, 0.12f);
        [SerializeField] Color huskColor = new Color(0.13f, 0.09f, 0.11f);
        [SerializeField] Color gooColor = new Color(0.62f, 0.22f, 0.32f, 1f);

        [Header("Events (hook sounds and effects up here)")]
        [Tooltip("They started swelling: the warning before they erupt.")]
        public UnityEvent onSwelling = new UnityEvent();
        [Tooltip("They erupted and released the swarm.")]
        public UnityEvent onErupt = new UnityEvent();
        public UnityEvent onRegrown = new UnityEvent();

        Transform player;
        float playerSearchTimer;
        float quietTime, warningTimer, regrowTimer, hitFlash, collapse;
        Vector3[] eggScale, eggPosition;
        float[] eggPhase;
        Renderer[] eggRenderers;
        Color eggColor = Color.white;
        MaterialPropertyBlock block;

        /// <summary>Every cluster of eggs in the scene, for readouts and level scripts.</summary>
        public static readonly List<PulsatingEggs> All = new List<PulsatingEggs>();

        public EggState State { get; private set; } = EggState.Dormant;
        /// <summary>How awake walking past has made them: 0 calm, 1 swelling.</summary>
        public float Disturbance { get; private set; }
        /// <summary>0 to 1 through the warning swell.</summary>
        public float WarningProgress => State == EggState.Swelling && warningTime > 0f ? 1f - warningTimer / warningTime : 0f;
        public float SenseRadius => senseRadius;

        void Awake()
        {
            if (!hitbox) hitbox = GetComponent<Collider>();
            block = new MaterialPropertyBlock();
            eggScale = new Vector3[eggs.Length];
            eggPosition = new Vector3[eggs.Length];
            eggPhase = new float[eggs.Length];
            eggRenderers = new Renderer[eggs.Length];
            for (int i = 0; i < eggs.Length; i++)
            {
                if (!eggs[i]) continue;
                eggScale[i] = eggs[i].localScale;
                eggPosition[i] = eggs[i].localPosition;
                eggPhase[i] = i * 2.39f;   // the golden angle, so neighbors never pulse together
                eggRenderers[i] = eggs[i].GetComponent<Renderer>();
            }
            var first = eggRenderers.Length > 0 && eggRenderers[0] ? eggRenderers[0].sharedMaterial : null;
            if (first) eggColor = first.HasProperty("_BaseColor") ? first.GetColor("_BaseColor") : first.HasProperty("_Color") ? first.GetColor("_Color") : Color.white;
            if (!scuttlerPrefab && swarmSize > 0)
                Debug.LogWarning(name + ": these Pulsating Eggs have no Scuttler Prefab, so nothing will hatch out of them.", this);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case EggState.Dormant:
                    if (Touched()) Erupt();
                    else Sense(dt);
                    break;
                case EggState.Swelling:
                    warningTimer -= dt;
                    if (warningTimer <= 0f || Touched()) Erupt();
                    break;
                default:
                    if (regrowTime > 0f)
                    {
                        regrowTimer -= dt;
                        if (regrowTimer <= 0f) Regrow();
                    }
                    break;
            }
            Animate(dt);
        }

        /// <summary>Shot, or hit any other way: they erupt at once.</summary>
        public void TakeDamage(DamageInfo damage)
        {
            if (State == EggState.Spent) return;
            hitFlash = 1f;
            Erupt();
        }

        /// <summary>Wakes them a little, the way walking past does. At 1 they start to swell.</summary>
        public void Disturb(float amount)
        {
            if (State != EggState.Dormant || amount <= 0f) return;
            Disturbance = Mathf.Clamp01(Disturbance + amount);
            quietTime = 0f;
            if (Disturbance >= 1f) StartSwelling();
        }

        /// <summary>
        /// Erupts at once, splashing and shoving anyone right next to it and releasing the swarm. Shots and
        /// touches call this, and level scripts can too.
        /// </summary>
        public void Erupt()
        {
            if (State == EggState.Spent) return;
            State = EggState.Spent;
            Disturbance = 0f;
            regrowTimer = regrowTime;
            if (hitbox) hitbox.enabled = false;
            Vector3 center = transform.position + Vector3.up * 0.5f;
            StreakFx.Burst(center, gooColor, 26, 7f, 0.6f);
            Splash(center);
            SpawnSwarm();
            onErupt.Invoke();
        }

        /// <summary>Walked past: the closer the player, the faster they wake. Left alone, they settle.</summary>
        void Sense(float dt)
        {
            Transform target = Player();
            float closeness = 0f;
            if (target)
            {
                float d = Vector3.Distance(target.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.5f);
                closeness = Mathf.Clamp01(1f - d / senseRadius);
            }
            if (closeness > 0f) Disturb(proximityRate * closeness * dt);
            else
            {
                quietTime += dt;
                if (quietTime > 1f) Disturbance = Mathf.Max(0f, Disturbance - calmRate * dt);
            }
        }

        /// <summary>Touched: the player is right up against the cluster, at about the same height.</summary>
        bool Touched()
        {
            Transform target = Player();
            if (!target || touchRadius <= 0f) return false;
            Vector3 to = target.position - transform.position;
            if (to.y < -0.5f || to.y > 1.6f) return false;   // on a level above or below
            to.y = 0f;
            return to.magnitude <= touchRadius;
        }

        void StartSwelling()
        {
            State = EggState.Swelling;
            Disturbance = 1f;
            warningTimer = warningTime;
            onSwelling.Invoke();
            if (warningTime <= 0f) Erupt();
        }

        void Regrow()
        {
            Transform target = Player();
            if (target && Vector3.Distance(target.position, transform.position) < 2.5f)
            {
                regrowTimer = 1f;   // not while someone is standing in it
                return;
            }
            State = EggState.Dormant;
            Disturbance = 0f;
            quietTime = 0f;
            if (hitbox) hitbox.enabled = true;
            onRegrown.Invoke();
        }

        /// <summary>The eruption splashes and shoves anyone right next to it.</summary>
        void Splash(Vector3 center)
        {
            Transform target = Player();
            if (!target) return;
            Vector3 to = target.position + Vector3.up * 0.9f - center;
            float d = to.magnitude;
            if (d > burstRadius) return;
            float closeness = 1f - d / burstRadius;
            var victim = target.GetComponentInParent<IDamageable>();
            if (victim != null && burstDamage > 0f)
                victim.TakeDamage(new DamageInfo
                {
                    Amount = burstDamage * (0.5f + 0.5f * closeness), Kind = DamageKind.Burst, Point = center,
                    Direction = d > 0.01f ? to / d : Vector3.up, Distance = d, Hits = 1, Source = gameObject,
                });
            var pushable = target.GetComponentInParent<IPushable>();
            Vector3 away = new Vector3(to.x, 0f, to.z);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            if (pushable != null && burstPush > 0f) pushable.Push((away + Vector3.up * 0.45f) * (burstPush * closeness));
        }

        /// <summary>Scuttlers pour out all around the cluster. Any that implement IHatchling leap out and give chase.</summary>
        void SpawnSwarm()
        {
            if (!scuttlerPrefab) return;
            Transform target = Player();
            for (int i = 0; i < swarmSize; i++)
            {
                float angle = (i + Random.value * 0.6f) / Mathf.Max(1, swarmSize) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 at = transform.position + dir * 0.5f + Vector3.up * 0.4f;
                GameObject scuttler = Instantiate(scuttlerPrefab, at, Quaternion.LookRotation(dir));
                var hatchling = scuttler.GetComponent<IHatchling>();
                if (hatchling != null)
                    hatchling.Hatch(target, dir * Random.Range(2.5f, 4.5f) + Vector3.up * Random.Range(3.5f, 5.5f));
            }
        }

        Transform Player()
        {
            if (player && player.gameObject.activeInHierarchy) return player;
            playerSearchTimer -= Time.deltaTime;
            if (playerSearchTimer > 0f) return null;
            playerSearchTimer = 1f;
            var found = GameObject.FindWithTag("Player");
            player = found ? found.transform : null;
            return player;
        }

        /// <summary>Pulse while dormant, faster and redder as they wake; swell and tremble before erupting; slump into a husk once spent.</summary>
        void Animate(float dt)
        {
            float time = Time.time;
            hitFlash = Mathf.Max(0f, hitFlash - dt * 6f);
            bool husk = State == EggState.Spent;
            collapse = Mathf.MoveTowards(collapse, husk ? 1f : 0f, dt * (husk ? 8f : 0.7f));   // slumps fast, grows back slowly

            float d = Disturbance;
            // At rest: the prototype's beat (2.4 radians a second) and size (7%). Waking speeds it up.
            float frequency = Mathf.Lerp(0.38f, 3.2f, d);
            float amplitude = Mathf.Lerp(0.07f, 0.09f, d);
            float calm = State == EggState.Dormant ? (1f - d) * (1f - collapse) : 0f;   // the red throb fades as anger takes over
            float swell = 1f;
            Color glow = Color.Lerp(calmGlow, angryGlow, d * d);
            if (State == EggState.Swelling)
            {
                float p = WarningProgress;
                swell = 1f + 0.35f * p;
                frequency = 6f;
                glow = angryGlow * (1.2f + 0.6f * Mathf.Sin(time * 3f * Mathf.PI * 2f));   // a slow throb, not a strobe
            }
            glow *= 1f - collapse;
            Color surface = Color.Lerp(Color.Lerp(eggColor, huskColor, collapse), Color.white, hitFlash * 0.7f);

            for (int i = 0; i < eggs.Length; i++)
            {
                if (!eggs[i]) continue;
                float wave = Mathf.Sin(time * frequency * Mathf.PI * 2f + eggPhase[i]);
                Vector3 alive = eggScale[i] * (swell * (1f + amplitude * wave));
                Vector3 slumped = Vector3.Scale(eggScale[i], new Vector3(0.9f, 0.22f, 0.9f));
                eggs[i].localScale = Vector3.Lerp(alive, slumped, collapse);
                Vector3 position = Vector3.Lerp(eggPosition[i], Vector3.Scale(eggPosition[i], new Vector3(0.8f, 0.25f, 0.8f)), collapse);
                if (State == EggState.Swelling) position += Random.insideUnitSphere * (0.03f * WarningProgress);   // trembling
                eggs[i].localPosition = position;

                if (!eggRenderers[i]) continue;
                block.SetColor("_EmissionColor", glow * (0.85f + 0.15f * wave) + calmPulse * (calm * Mathf.Max(0f, wave)));
                block.SetColor("_BaseColor", surface);
                block.SetColor("_Color", surface);
                eggRenderers[i].SetPropertyBlock(block);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, senseRadius);                       // walked past
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, touchRadius);                       // touched
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, burstRadius);   // the eruption's reach
        }
    }
}
