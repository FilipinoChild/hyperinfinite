using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// The Hive Mother boss. She stalks the player, winds up and slams the ground when
    /// the player gets close, and summons Scuttlers every few seconds.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class HiveMotherAI : MonoBehaviour
    {
        // The three things she can be doing. Only one at a time.
        enum State { Stalking, WindingUp, Recovering }

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

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (slamWarning) slamWarning.SetActive(false);
        }

        void Start()
        {
            PlayerControls controls = FindFirstObjectByType<PlayerControls>();
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
                    if (stateTimer <= 0f) Slam();
                    break;

                case State.Recovering:
                    stateTimer -= dt;
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
        }

        void Slam()
        {
            if (slamWarning) slamWarning.SetActive(false);
            state = State.Recovering;
            stateTimer = slamRecovery;
            slamCooldownTimer = slamCooldown;

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