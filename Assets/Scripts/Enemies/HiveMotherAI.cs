using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// The Hive Mother boss. She stalks the player, winds up and slams the ground when
    /// the player gets close, and summons Scuttlers every few seconds.
    ///
    /// The slam is telegraphed: the red ring shows where it will land and blinks faster and
    /// faster, rings of sparks sweep out to its edge, and she rears up and flashes red. When it
    /// lands she drops flat and a shockwave bursts across the ring.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class HiveMotherAI : MonoBehaviour
    {
        // The three things she can be doing. Only one at a time.
        enum State { Stalking, WindingUp, Recovering }

        // What her sprite flashes while a slam is coming
        static readonly Color WarningTint = new Color(1f, 0.3f, 0.2f);

        [Header("Ground slam")]
        [Tooltip("She starts a slam when the player is this close, in meters.")]
        [SerializeField, Min(0f)] float slamTriggerRange = 6f;
        [Tooltip("The slam hits everything within this many meters.")]
        [SerializeField, Min(0f)] float slamRadius = 7f;
        [SerializeField, Min(0f)] float slamDamage = 25f;
        [Tooltip("Seconds of warning before the slam lands. This is the player's chance to escape.")]
        [SerializeField, Min(0f)] float slamWindup = 1f;
        [Tooltip("Seconds she stands still after slamming.")]
        [SerializeField, Min(0f)] float slamRecovery = 1.2f;
        [Tooltip("Seconds before she can slam again.")]
        [SerializeField, Min(0f)] float slamCooldown = 3f;
        [Tooltip("The red ring on the floor that warns the player. Optional.")]
        [SerializeField] GameObject slamWarning;

        [Header("Summoning")]
        [SerializeField] GameObject scuttlerPrefab;
        [SerializeField, Min(0)] int summonCount = 3;
        [Tooltip("Seconds between summons.")]
        [SerializeField, Min(1f)] float summonInterval = 12f;
        [Tooltip("How far from her the Scuttlers appear.")]
        [SerializeField, Min(0f)] float summonRadius = 4f;

        NavMeshAgent agent;
        Transform player;
        IDamageable playerHealth;
        State state = State.Stalking;
        float stateTimer, slamCooldownTimer, summonTimer, repathTimer;
        FaceCamera sprite;           // shows her sprite: it rears up, flashes and drops flat
        bool secondPulse;            // the second ring of sparks has gone out this wind-up

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            sprite = GetComponentInChildren<FaceCamera>();
            if (slamWarning) slamWarning.SetActive(false);
        }

        void Start()
        {
            PlayerControls controls = FindAnyObjectByType<PlayerControls>();
            if (controls == null)
            {
                Debug.LogWarning("HiveMotherAI: no player found in the scene.", this);
                enabled = false;
                return;
            }
            player = controls.transform;
            playerHealth = controls.GetComponentInChildren<IDamageable>();
            summonTimer = summonInterval * 0.5f;   // first summon comes a little sooner
            Debug.Log("The Hive Mother has appeared");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            slamCooldownTimer -= dt;

            // Summoning runs on its own clock, but only while she's walking
            summonTimer -= dt;
            if (summonTimer <= 0f && state == State.Stalking)
            {
                Summon();
                summonTimer = summonInterval;
            }

            // The state machine: do whatever the current state says
            switch (state)
            {
                case State.Stalking:
                    repathTimer -= dt;
                    if (repathTimer <= 0f)
                    {
                        repathTimer = 0.3f;
                        agent.SetDestination(player.position);
                    }
                    if (FlatDistanceToPlayer() <= slamTriggerRange && slamCooldownTimer <= 0f)
                        StartWindup();
                    break;

                case State.WindingUp:
                    stateTimer -= dt;
                    AnimateWarning();
                    if (stateTimer <= 0f) Slam();
                    break;

                case State.Recovering:
                    stateTimer -= dt;
                    // A quarter of a second flat on the ground, then she picks herself up
                    if (sprite && stateTimer <= slamRecovery - 0.25f) sprite.Squash = 0f;
                    if (stateTimer <= 0f)
                    {
                        state = State.Stalking;
                        agent.isStopped = false;   // walk again
                    }
                    break;
            }
        }

        void StartWindup()
        {
            state = State.WindingUp;
            stateTimer = slamWindup;
            agent.isStopped = true;                   // plant her feet
            if (slamWarning) slamWarning.SetActive(true);
            // A ring of sparks sweeps out to the edge of the slam, and another halfway through
            secondPulse = false;
            PixelFx.Ring(transform.position, slamRadius, Palette.Yellow, 36, Mathf.Max(0.1f, slamWindup * 0.45f), 0.24f);
        }

        /// The warning while she winds up: the ring blinks faster and faster, and she
        /// rears up taller and flashes red until the slam lands.
        void AnimateWarning()
        {
            float progress = slamWindup > 0f ? 1f - Mathf.Clamp01(stateTimer / slamWindup) : 1f;   // 0 at the start, 1 as it lands
            bool lit = Mathf.Repeat(Time.time * (4f + 8f * progress), 1f) < 0.6f;
            if (slamWarning && slamWarning.activeSelf != lit) slamWarning.SetActive(lit);
            if (sprite)
            {
                sprite.Squash = 0.2f * progress;
                sprite.Tint = lit ? WarningTint : Color.white;
            }
            if (!secondPulse && progress >= 0.5f)
            {
                secondPulse = true;
                PixelFx.Ring(transform.position, slamRadius, Palette.Yellow, 36, Mathf.Max(0.1f, slamWindup * 0.45f), 0.24f);
            }
        }

        void Slam()
        {
            if (slamWarning) slamWarning.SetActive(false);
            state = State.Recovering;
            stateTimer = slamRecovery;
            slamCooldownTimer = slamCooldown;

            // She drops flat, and the shockwave bursts out across the ring
            if (sprite)
            {
                sprite.Tint = Color.white;
                sprite.Squash = -0.3f;
            }
            PixelFx.Ring(transform.position, slamRadius, Palette.Red, 44, 0.25f, 0.34f);
            PixelFx.Ring(transform.position, slamRadius * 0.55f, Palette.Yellow, 24, 0.2f, 0.24f);
            PixelFx.Burst(transform.position + Vector3.up * 0.3f, Palette.Navy, Palette.White, 24, 7f, 0.5f, 0.2f);

            // The slam hits along the ground, so a player in the air dodges it
            bool inRing = FlatDistanceToPlayer() <= slamRadius;
            bool airborne = player.position.y > transform.position.y + 0.6f;
            if (inRing && !airborne && playerHealth != null)
            {
                playerHealth.TakeDamage(new DamageInfo
                {
                    Amount = slamDamage,
                    Kind = DamageKind.Melee,
                    Point = player.position,
                    Direction = (player.position - transform.position).normalized,
                    Distance = FlatDistanceToPlayer(),
                    Hits = 1,
                    Source = gameObject,
                });
            }
        }

        void Summon()
        {
            if (!scuttlerPrefab) return;
            for (int i = 0; i < summonCount; i++)
            {
                // Space them evenly in a circle around her
                float angle = 360f * i / summonCount;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * summonRadius;
                Vector3 position = transform.position + offset;
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    Instantiate(scuttlerPrefab, hit.position, Quaternion.identity);
            }
        }

        /// Distance to the player along the ground, ignoring height
        float FlatDistanceToPlayer()
        {
            Vector3 difference = player.position - transform.position;
            difference.y = 0f;
            return difference.magnitude;
        }
    }
}