using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// A ranged Arachturn. It keeps its distance from the player, stops and flashes to
    /// warn before each shot, then spits an acid glob aimed slightly ahead of the player.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class SpitterAI : MonoBehaviour
    {
        // The three things the Spitter can be doing. Only one at a time.
        enum State { Moving, Aiming, Recovering }

        [Header("Positioning")]
        [Tooltip("The distance, in meters, it tries to keep from the player.")]
        [SerializeField, Min(1f)] float preferredRange = 12f;
        [Tooltip("Closer than this and it backs away.")]
        [SerializeField, Min(0f)] float tooCloseRange = 6f;
        [SerializeField, Min(0.05f)] float repathInterval = 0.3f;

        [Header("Spitting")]
        [SerializeField] SpitterGlob globPrefab;
        [Tooltip("How high above its feet the glob comes out.")]
        [SerializeField, Min(0f)] float spitHeight = 1.2f;
        [Tooltip("Seconds of warning (flashing) before it fires.")]
        [SerializeField, Min(0f)] float aimTime = 0.8f;
        [Tooltip("Seconds it stands still after firing.")]
        [SerializeField, Min(0f)] float recoverTime = 0.6f;
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0.1f)] float fireCooldown = 2.5f;
        [Tooltip("How far ahead of the player it aims, in seconds of the player's movement.")]
        [SerializeField, Min(0f)] float leadTime = 0.4f;

        NavMeshAgent agent;
        SpriteRenderer sprite;
        Color baseColor;
        Transform player;
        Vector3 lastPlayerPosition, playerVelocity;
        State state = State.Moving;
        float stateTimer, fireTimer, repathTimer;

        // The player, found once and shared, so a wave of Spitters doesn't search the scene once each
        static PlayerControls sharedPlayer;
        // Seconds between checks for a clear line to the player. The check is a raycast, so it isn't made every frame.
        const float SightInterval = 0.15f;
        float sightTimer, repathStagger;
        bool seesPlayer;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite) baseColor = sprite.color;
        }

        void Start()
        {
            if (!sharedPlayer) sharedPlayer = FindAnyObjectByType<PlayerControls>();
            PlayerControls controls = sharedPlayer;
            if (controls == null)
            {
                Debug.LogWarning("SpitterAI: no player found in the scene.", this);
                enabled = false;
                return;
            }
            player = controls.transform;
            lastPlayerPosition = player.position;
            fireTimer = fireCooldown * 0.5f;   // first shot comes a little sooner
            // Each Spitter looks and plans at its own moment, so a wave that arrives together doesn't all do it on the same frame
            repathStagger = Random.value * repathInterval;
            sightTimer = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Work out how fast the player is moving, so the Spitter can aim ahead of them
            playerVelocity = (player.position - lastPlayerPosition) / dt;
            lastPlayerPosition = player.position;

            fireTimer -= dt;

            switch (state)
            {
                case State.Moving:
                    sightTimer -= dt;
                    if (sightTimer <= 0f)
                    {
                        sightTimer = SightInterval;
                        seesPlayer = CanSeePlayer();
                    }
                    repathTimer -= dt;
                    if (repathTimer <= 0f)
                    {
                        repathTimer = repathInterval + repathStagger;   // the first plan comes at once; after that, at its own moment
                        repathStagger = 0f;
                        agent.SetDestination(PickSpot());
                    }
                    if (fireTimer <= 0f && seesPlayer && FlatDistanceToPlayer() <= preferredRange + 4f)
                        StartAiming();
                    break;

                case State.Aiming:
                    stateTimer -= dt;
                    // Flash between red and its normal color as a warning
                    if (sprite)
                    {
                        bool flashOn = Mathf.Repeat(stateTimer, 0.16f) < 0.08f;
                        sprite.color = flashOn ? Color.red : baseColor;
                    }
                    if (stateTimer <= 0f) Fire();
                    break;

                case State.Recovering:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        state = State.Moving;
                        agent.isStopped = false;
                        sightTimer = 0f;   // look again at once: what it saw before the shot is old
                    }
                    break;
            }
        }

        /// Where it wants to stand: at its preferred range from the player, with a clear shot.
        Vector3 PickSpot()
        {
            if (!seesPlayer) return player.position;   // no clear shot: move in until it has one

            Vector3 away = transform.position - player.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance < 0.01f) away = Vector3.forward;

            // Already in the comfortable band with a clear shot: stay put
            if (distance >= tooCloseRange && distance <= preferredRange) return transform.position;

            // Otherwise move to the preferred range, on the same side it's already on
            Vector3 spot = player.position + away.normalized * preferredRange;
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                return hit.position;
            return transform.position;
        }

        /// True if nothing is blocking a straight line from its mouth to the player's chest.
        bool CanSeePlayer()
        {
            Vector3 mouth = transform.position + Vector3.up * spitHeight;
            Vector3 chest = player.position + Vector3.up * 1.2f;
            if (Physics.Linecast(mouth, chest, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(player);   // the first thing in the way is the player
            return true;
        }

        void StartAiming()
        {
            state = State.Aiming;
            stateTimer = aimTime;
            agent.isStopped = true;   // plant its feet
            Sfx.At(Sound.SpitterAim, transform.position + Vector3.up * spitHeight);   // a gurgle: the warning before the shot
        }

        void Fire()
        {
            state = State.Recovering;
            stateTimer = recoverTime;
            fireTimer = fireCooldown;
            if (sprite) sprite.color = baseColor;
            if (!globPrefab) return;

            // Aim at the player's chest, plus a little ahead of where they're moving
            Vector3 mouth = transform.position + Vector3.up * spitHeight;
            Vector3 aimPoint = player.position + Vector3.up * 1.2f + playerVelocity * leadTime;

            Sfx.At(Sound.SpitterSpit, mouth);
            SpitterGlob glob = Instantiate(globPrefab, mouth, Quaternion.identity);
            glob.Launch(aimPoint - mouth, gameObject);
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