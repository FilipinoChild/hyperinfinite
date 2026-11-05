using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// Chases the player across the NavMesh and bites when close enough.
    [RequireComponent(typeof(NavMeshAgent))]
    public class ScuttlerAI : MonoBehaviour
    {
        [Header("Chasing")]
        [Tooltip("Seconds between path updates. Lower follows the player more closely.")]
        [SerializeField, Min(0.05f)] float repathInterval = 0.2f;

        [Header("Attacking")]
        [Tooltip("How close the Scuttler must be to bite, in meters.")]
        [SerializeField, Min(0f)] float attackRange = 1.6f;
        [SerializeField, Min(0f)] float attackDamage = 8f;
        [Tooltip("Seconds between bites.")]
        [SerializeField, Min(0.1f)] float attackCooldown = 1f;

        NavMeshAgent agent;          // moves the Scuttler along the NavMesh
        Transform player;            // where the player is
        IDamageable playerHealth;    // what the bite damages
        float repathTimer, attackTimer;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void Start()
        {
            // Find the player by looking for the one object that has PlayerControls
            PlayerControls controls = FindFirstObjectByType<PlayerControls>();
            if (controls == null)
            {
                Debug.LogWarning("ScuttlerAI: no player found in the scene.", this);
                enabled = false;
                return;
            }
            player = controls.transform;
            playerHealth = controls.GetComponentInChildren<IDamageable>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            repathTimer -= dt;
            attackTimer -= dt;

            // Every so often, point the agent at the player's current position
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                agent.SetDestination(player.position);
            }

            // Bite when close enough and the cooldown has run out
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= attackRange && attackTimer <= 0f)
                Bite(distance);
        }

        void Bite(float distance)
        {
            attackTimer = attackCooldown;
            if (playerHealth == null) return;

            playerHealth.TakeDamage(new DamageInfo
            {
                Amount = attackDamage,
                Kind = DamageKind.Bite,
                Point = player.position,
                Direction = (player.position - transform.position).normalized,
                Distance = distance,
                Hits = 1,
                Source = gameObject,
            });
        }
    }
}
