using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// Scuttlers hunt in packs: each one flanks to its own spot on a ring around the player,
    /// then rushes in to bite. Shooting one scatters it and its neighbors, and then they regroup.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class ScuttlerAI : MonoBehaviour
    {
        // Every living Scuttler in the scene. "static" means one shared list for all of them.
        static readonly List<ScuttlerAI> pack = new List<ScuttlerAI>();

        [Header("Chasing")]
        [Tooltip("Seconds between path updates. Lower follows the player more closely.")]
        [SerializeField, Min(0.05f)] float repathInterval = 0.2f;

        [Header("Surrounding")]
        [Tooltip("Radius, in meters, of the ring the pack spreads out on before rushing in.")]
        [SerializeField, Min(1f)] float surroundRadius = 5f;
        [Tooltip("How close a Scuttler must get to its spot on the ring before it rushes in.")]
        [SerializeField, Min(0.5f)] float slotReachDistance = 1.5f;

        [Header("Scattering")]
        [Tooltip("Scuttlers within this many meters of one that gets shot scatter too.")]
        [SerializeField, Min(0f)] float scatterRadius = 4f;
        [Tooltip("How far they run when they scatter.")]
        [SerializeField, Min(0f)] float scatterDistance = 5f;
        [Tooltip("Seconds they spend scattering before regrouping.")]
        [SerializeField, Min(0f)] float scatterTime = 0.7f;

        [Header("Attacking")]
        [Tooltip("How close the Scuttler must be to bite, in meters.")]
        [SerializeField, Min(0f)] float attackRange = 1.6f;
        [SerializeField, Min(0f)] float attackDamage = 8f;
        [Tooltip("Seconds between bites.")]
        [SerializeField, Min(0.1f)] float attackCooldown = 1f;

        NavMeshAgent agent;          // moves the Scuttler along the NavMesh
        Health health;               // this Scuttler's own health
        Transform player;            // where the player is
        IDamageable playerHealth;    // what the bite damages
        float repathTimer, attackTimer, scatterTimer;
        bool committed;              // false while flanking, true once it rushes in

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            pack.Add(this);                            // join the pack
            health.onDamaged.AddListener(OnHurt);      // react when shot
        }

        void OnDisable()
        {
            pack.Remove(this);                         // leave the pack when killed
            health.onDamaged.RemoveListener(OnHurt);
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

            // While scattering, keep running and don't bite
            if (scatterTimer > 0f)
            {
                scatterTimer -= dt;
                return;
            }

            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                agent.SetDestination(PickDestination());
            }

            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= attackRange && attackTimer <= 0f)
                Bite(distance);
        }

        /// Flanking: head for this Scuttler's spot on the ring. Once there (or already
        /// right next to the player), commit and rush the player directly.
        Vector3 PickDestination()
        {
            if (!committed)
            {
                Vector3 slot = SlotPosition();
                bool atSlot = Vector3.Distance(transform.position, slot) < slotReachDistance;
                bool closeToPlayer = Vector3.Distance(transform.position, player.position) < attackRange + 1f;
                if (atSlot || closeToPlayer) committed = true;
                else return slot;
            }
            return player.position;
        }

        /// This Scuttler's spot on a ring around the player. The spots are spread evenly,
        /// centered on the side the pack is coming from, so they fan out instead of
        /// circling all the way around first.
        Vector3 SlotPosition()
        {
            int count = pack.Count;
            int index = pack.IndexOf(this);

            // Which direction is the pack, as seen from the player?
            Vector3 center = Vector3.zero;
            foreach (ScuttlerAI s in pack) center += s.transform.position;
            center /= count;
            Vector3 toPack = center - player.position;
            float baseAngle = Mathf.Atan2(toPack.x, toPack.z) * Mathf.Rad2Deg;

            // Spread the spots evenly around the circle, centered on that direction
            float step = 360f / count;
            float angle = baseAngle + (index - (count - 1) * 0.5f) * step;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * surroundRadius;
            Vector3 point = player.position + offset;

            // If the spot is inside a wall, use the nearest walkable point instead
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return hit.position;
            return player.position;
        }

        /// Called by Health whenever this Scuttler takes damage.
        void OnHurt(float amount)
        {
            // Scatter every Scuttler near this one, including itself.
            // ToArray() makes a copy, so the pack list can't change while we loop.
            foreach (ScuttlerAI s in pack.ToArray())
                if (Vector3.Distance(s.transform.position, transform.position) <= scatterRadius)
                    s.Scatter();
        }

        /// Run away from the player at an angle, then regroup.
        void Scatter()
        {
            committed = false;           // it has to flank again afterward
            scatterTimer = scatterTime;

            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;   // standing on the player: pick any direction
            away.y = 0f;
            away.Normalize();

            // Add some sideways randomness so they don't all run in a straight line
            Vector3 side = Vector3.Cross(Vector3.up, away) * Random.Range(-1f, 1f);
            Vector3 target = transform.position + (away + side).normalized * scatterDistance;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
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