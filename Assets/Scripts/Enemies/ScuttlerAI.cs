using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// Scuttlers hunt in packs: each one flanks to its own spot on a ring around the player,
    /// then rushes in to bite. Shooting one scatters it and its neighbors, and then they regroup.
    /// If the player is up on a platform, a Scuttler pounces up to reach them.
    ///
    /// A bite is telegraphed: the Scuttler crouches and flashes red for a moment, then leaps.
    /// The bite only lands if the player is still in reach when it does, so dashing away
    /// during the warning dodges it.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class ScuttlerAI : MonoBehaviour, IPushable
    {
        // Every living Scuttler in the scene. "static" means one shared list for all of them.
        static readonly List<ScuttlerAI> pack = new List<ScuttlerAI>();
        // The player, found once and shared, so a new Scuttler doesn't search the scene again.
        static PlayerControls sharedPlayer;
        // What the sprite flashes while a bite is coming
        static readonly Color WarningTint = new Color(1f, 0.25f, 0.2f);
        // How much further than the attack range the leap itself reaches, in meters
        const float LungeReach = 0.6f;

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
        [Tooltip("How close the Scuttler must be to bite, measured along the ground, in meters.")]
        [SerializeField, Min(0f)] float attackRange = 1.8f;
        [Tooltip("How high above the Scuttler the player can be and still get bitten. A double jump reaches about 3 m.")]
        [SerializeField, Min(0f)] float pounceHeight = 3.2f;
        [SerializeField, Min(0f)] float attackDamage = 8f;
        [Tooltip("Seconds between bites.")]
        [SerializeField, Min(0.1f)] float attackCooldown = 1f;
        [Tooltip("Seconds it crouches and flashes red before the bite lands. This is the player's warning; 0 bites at once.")]
        [SerializeField, Min(0f)] float biteWindup = 0.3f;

        [Header("Look")]
        [Tooltip("The visible body (the sprite). It hops up in an arc when the Scuttler pounces. Optional.")]
        [SerializeField] Transform body;
        [Tooltip("Seconds a pounce takes, up and back down.")]
        [SerializeField, Min(0.05f)] float pounceTime = 0.35f;

        NavMeshAgent agent;          // moves the Scuttler along the NavMesh
        Health health;               // this Scuttler's own health
        Transform player;            // where the player is
        IDamageable playerHealth;    // what the bite damages
        float repathTimer, attackTimer, scatterTimer;
        bool committed;              // false while flanking, true once it rushes in

        Vector3 bodyRest;            // where the body sits when not pouncing
        float pounceTimer, pounceArc;
        float chitterTimer = 2f;
        FaceCamera sprite;           // shows the body's sprite: it crouches, stretches and flashes
        float windupTimer;
        bool windingUp;              // true during the warning before a bite

        /// <summary>True while it crouches and flashes red before a bite: the moment a well-timed bash counters it.</summary>
        public bool IsWindingUp => windingUp;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            if (body) bodyRest = body.localPosition;
            sprite = body ? body.GetComponentInChildren<FaceCamera>() : GetComponentInChildren<FaceCamera>();
        }

        void OnEnable()
        {
            pack.Add(this);                            // join the pack
            health.onDied.AddListener(OnHurt);      // react when shot
        }

        void OnDisable()
        {
            pack.Remove(this);                         // leave the pack when killed
            health.onDied.RemoveListener(OnHurt);
        }

        void Start()
        {
            // Find the player by looking for the one object that has PlayerControls
            if (!sharedPlayer) sharedPlayer = FindAnyObjectByType<PlayerControls>();
            PlayerControls controls = sharedPlayer;
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
            AnimatePounce(dt);
            Chitter(dt);

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

            // Start a bite when close enough along the ground and the player isn't too high up
            float flatDistance = FlatDistanceToPlayer();
            float heightAbove = player.position.y - transform.position.y;
            if (windingUp)
            {
                // Crouched and flashing red: the bite is coming
                windupTimer -= dt;
                if (sprite) sprite.Tint = Mathf.Repeat(windupTimer * 14f, 1f) < 0.5f ? WarningTint : Color.white;
                if (windupTimer <= 0f) Bite(flatDistance, heightAbove);
            }
            else if (flatDistance <= attackRange && heightAbove <= pounceHeight && attackTimer <= 0f)
                StartBite(flatDistance, heightAbove);
        }

        /// Flanking: head for this Scuttler's spot on the ring. Once there (or already
        /// right next to the player), commit and rush the player directly.
        Vector3 PickDestination()
        {
            if (!committed)
            {
                Vector3 slot = SlotPosition();
                bool atSlot = Vector3.Distance(transform.position, slot) < slotReachDistance;
                bool closeToPlayer = FlatDistanceToPlayer() < attackRange + 1f;
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
        void OnHurt()
        {
            // Scatter every Scuttler near this one, including itself. Scattering never changes the pack
            // list, so it can be walked as it is, without copying it for every death.
            Vector3 here = transform.position;
            for (int i = 0; i < pack.Count; i++)
                if ((pack[i].transform.position - here).sqrMagnitude <= scatterRadius * scatterRadius)
                    pack[i].Scatter();
        }

        /// Run away from the player at an angle, then regroup.
        void Scatter()
        {
            committed = false;           // it has to flank again afterward
            scatterTimer = scatterTime;
            CancelBite();                // a bite it was winding up is off

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

        /// Thrown back by a blast, such as the burst when the player respawns. It skids away,
        /// then has to regroup and flank again.
        public void Push(Vector3 velocityChange)
        {
            if (!player || !agent.isOnNavMesh) return;
            Scatter();
            velocityChange.y = 0f;           // it stays on the ground
            agent.velocity = velocityChange;
        }

        /// The warning before a bite: crouch down and flash red for a moment.
        void StartBite(float flatDistance, float heightAbove)
        {
            attackTimer = attackCooldown;
            if (biteWindup <= 0f)
            {
                Bite(flatDistance, heightAbove);   // no warning wanted
                return;
            }
            windingUp = true;
            windupTimer = biteWindup;
            if (sprite) sprite.Squash = -0.3f;     // crouched, ready to spring
            Sfx.At(Sound.ScuttlerWindup, transform.position);   // a hiss: the warning you can hear behind you
        }

        void CancelBite()
        {
            if (!windingUp) return;
            windingUp = false;
            if (sprite)
            {
                sprite.Squash = 0f;
                sprite.Tint = Color.white;
            }
        }

        void Bite(float flatDistance, float heightAbove)
        {
            windingUp = false;
            Sfx.At(Sound.ScuttlerBite, transform.position);
            if (sprite)
            {
                sprite.Tint = Color.white;
                sprite.Squash = 0.2f;              // stretched out in the leap
            }

            // Leap: higher when the player is up on something, a small hop otherwise
            pounceTimer = pounceTime;
            pounceArc = Mathf.Clamp(heightAbove + 0.5f, 0.4f, pounceHeight + 0.5f);

            // The bite only lands if the player is still within the leap's reach
            bool inReach = flatDistance <= attackRange + LungeReach && heightAbove <= pounceHeight;
            if (!inReach || playerHealth == null) return;
            playerHealth.TakeDamage(new DamageInfo
            {
                Amount = attackDamage,
                Kind = DamageKind.Bite,
                Point = player.position,
                Direction = (player.position - transform.position).normalized,
                Distance = flatDistance,
                Hits = 1,
                Source = gameObject,
            });
        }

        /// Now and then each Scuttler clicks and chitters, so a pack can be heard closing in. Sfx keeps only a few
        /// going at once however big the pack.
        void Chitter(float dt)
        {
            chitterTimer -= dt;
            if (chitterTimer > 0f) return;
            chitterTimer = Random.Range(3f, 8f);
            Sfx.At(Sound.ScuttlerChitter, transform.position);
        }

        /// Moves the visible body up and back down in an arc while pouncing.
        void AnimatePounce(float dt)
        {
            if (pounceTimer <= 0f) return;   // not pouncing: nothing to move
            pounceTimer -= dt;
            float t = 1f - Mathf.Clamp01(pounceTimer / pounceTime);   // 0 at the start, 1 at the end
            if (body) body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(t * Mathf.PI) * pounceArc);
            if (pounceTimer > 0f) return;

            // Landed: back where it sits, and back to its normal shape
            if (body) body.localPosition = bodyRest;
            if (sprite && !windingUp) sprite.Squash = 0f;
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