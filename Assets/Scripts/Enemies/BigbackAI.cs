using UnityEngine;
using UnityEngine.AI;

namespace HyperInfinite
{
    /// Heavy Arachturn with a directional shield. The root's +Z is always the armor side;
    /// the billboard chooses directional art without changing the actual facing.
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class BigbackAI : MonoBehaviour
    {
        [Header("Armor")]
        [SerializeField, Range(0.1f, 0.95f)] float rearDot = 0.4f;
        [SerializeField, Range(0f, 1f)] float reflectedDamageFraction = 0.35f;
        [SerializeField, Min(0f)] float reflectedRange = 30f;
        [Header("Movement and attack")]
        [SerializeField, Min(0f)] float turnSpeed = 100f;
        [SerializeField, Min(0.05f)] float repathInterval = 0.25f;
        [SerializeField, Min(0f)] float attackRange = 2.25f;
        [SerializeField, Min(0f)] float attackDamage = 14f;
        [Header("Charge attack")]
        [SerializeField, Min(1f)] float chargeTriggerRange = 10f;
        [SerializeField, Min(0.1f)] float chargeWindupSeconds = 1f;
        [SerializeField, Min(1f)] float chargeSpeedMultiplier = 2.5f;
        [SerializeField, Min(1f)] float chargeDistance = 13f;
        [SerializeField, Min(0.1f)] float wallDazeSeconds = 3f;
        [SerializeField, Min(0.1f)] float missedChargeDazeSeconds = 1.6f;
        [SerializeField, Min(0.1f)] float hitRecoverySeconds = 0.9f;
        [SerializeField, Min(0f)] float chargeCooldownSeconds = 5f;
        [SerializeField, Min(1f)] float dazedDamageMultiplier = 1.5f;
        [Header("Directional sprite (8 columns, 4 rows)")]
        [SerializeField] Transform body;
        [SerializeField] Renderer visual;

        NavMeshAgent agent;
        Health health, playerHealth;
        Transform player, cameraTransform;
        Material spriteMaterial;
        Vector3 bodyRest, bodyScale, lastPosition;
        enum AttackState { Pursue, Windup, Charge, Dazed, Recover }
        AttackState state;
        Vector3 chargeDirection, previousChargePosition;
        float repath, cooldown, phaseTime, chargeRemaining, walkSpeed, stride, flashUntil, nextFlash, nextFx;
        float chargeStallTime, previousChargeStep;
        readonly RaycastHit[] chargeHits = new RaycastHit[32];
        readonly Collider[] chargeOverlaps = new Collider[24];
        public bool IsDazed => state == AttackState.Dazed;
        public float IncomingDamageMultiplier => IsDazed ? Mathf.Max(1f, dazedDamageMultiplier) : 1f;
        int shownView = -1, shownFrame = -1;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Color shownTint = Color.white;   // the tint last sent to the material, so it's only sent when it changes

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            agent.updateRotation = false;
            walkSpeed = agent.speed;
            if (body) { bodyRest = body.localPosition; bodyScale = body.localScale; }
            lastPosition = transform.position;
            if (visual)
            {
                spriteMaterial = visual.material; // independent atlas offset per enemy
                spriteMaterial.SetTextureScale("_BaseMap", new Vector2(1f / 8f, 1f / 4f));
                spriteMaterial.SetTextureScale("_MainTex", new Vector2(1f / 8f, 1f / 4f));
            }
        }

        void Start() { FindPlayer(); }

        void FindPlayer()
        {
            PlayerControls controls = FindAnyObjectByType<PlayerControls>();
            if (!controls) return;
            player = controls.transform;
            playerHealth = controls.GetComponentInChildren<Health>();
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public bool IsRearSource(Vector3 position)
        {
            Vector3 side = Flat(position - transform.position);
            return side.sqrMagnitude > 0.0001f && Vector3.Dot(transform.forward, side.normalized) <= -rearDot;
        }

        public bool IsRearHit(Vector3 point, Vector3 incoming)
        {
            Vector3 side = Flat(point - transform.position);
            if (side.sqrMagnitude < 0.02f) side = Flat(-incoming);
            return side.sqrMagnitude > 0.0001f && Vector3.Dot(transform.forward, side.normalized) <= -rearDot;
        }

        // Health remains the IDamageable so all existing kill/drop/vent systems keep working.
        public bool AllowsDamage(DamageInfo damage)
        {
            if (damage.Kind == DamageKind.Shotgun)
                return IsRearHit(damage.Point, damage.Direction);
            return damage.Source && IsRearSource(damage.Source.transform.position);
        }

        public void ArmorFeedback(Vector3 point)
        {
            flashUntil = Time.time + 0.12f;
            if (Time.time < nextFlash) return;
            nextFlash = Time.time + 0.06f;
            PixelFx.Flash(point, Palette.PaleCyan, 0.36f, 0.09f);
            PixelFx.Sparks(point, transform.forward, Palette.Amber, 9, 6f, 0.28f);
        }

        /// Called per shotgun pellet, before the weapon aggregates the damage to targets.
        /// A blocked pellet never enters that aggregation. Ricochets are hitscan just like the original gun.
        public bool TryDeflect(RaycastHit hit, Vector3 incoming, float damage, float remainingRange,
                               Transform shooter, out float returnedDamage)
        {
            returnedDamage = 0f;
            if (health.IsDead || IsRearHit(hit.point, incoming)) return false;
            ArmorFeedback(hit.point);
            Vector3 face = Flat(hit.point - transform.position);
            // A broad, flat front plate reflects head-on fire straight back; its shoulders curve around.
            Vector3 normal = face.sqrMagnitude > 0.0001f && Vector3.Dot(face.normalized, transform.forward) < 0.65f
                ? Flat(hit.normal).normalized : transform.forward;
            if (normal.sqrMagnitude < 0.01f || Vector3.Dot(incoming, normal) >= -0.01f)
                normal = hit.normal;
            Vector3 direction = Vector3.Reflect(incoming, normal).normalized;
            float distance = Mathf.Min(reflectedRange, remainingRange);
            if (distance <= 0.06f) return true;
            Vector3 start = hit.point + direction * 0.055f;
            Vector3 end = start + direction * distance;
            if (Raycasts.Nearest(start, direction, distance, transform, out RaycastHit rebound))
            {
                end = rebound.point;
                if (shooter && rebound.collider.transform.IsChildOf(shooter))
                    returnedDamage = damage * reflectedDamageFraction;
                PixelFx.Sparks(end, rebound.normal, Palette.Amber, 2, 4f, 0.18f);
            }
            PixelFx.Bolt(hit.point, end, Palette.Amber, 0.075f, 0.16f);
            return true;
        }

        void Update()
        {
            if (health.IsDead)
            {
                StopMotion();
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            repath -= dt;
            if (!player && repath <= 0f) { FindPlayer(); repath = 0.5f; }
            if (!player || !playerHealth || playerHealth.IsDead)
            {
                StopMotion();
                state = AttackState.Pursue;
                return;
            }
            if (!agent.enabled || !agent.isOnNavMesh) return;

            switch (state)
            {
                case AttackState.Windup:
                    phaseTime -= dt;
                    if (phaseTime <= 0f)
                    {
                        state = AttackState.Charge;
                        chargeRemaining = Mathf.Max(1f, chargeDistance);
                        previousChargePosition = agent.nextPosition;
                        previousChargeStep = chargeStallTime = 0f;
                        agent.speed = walkSpeed * chargeSpeedMultiplier;
                        agent.isStopped = false; // no path: movement is driven only by the locked direction
                        nextFx = 0f;
                        Sfx.At(Sound.ScuttlerBite, transform.position, 0.9f, 0.75f);
                    }
                    return;
                case AttackState.Charge:
                    StepCharge(dt);
                    return;
                case AttackState.Dazed:
                    DazeFeedback();
                    phaseTime -= dt;
                    if (phaseTime <= 0f) ResumePursuit();
                    return;
                case AttackState.Recover:
                    phaseTime -= dt;
                    if (phaseTime <= 0f) ResumePursuit();
                    return;
            }

            cooldown = Mathf.Max(0f, cooldown - dt);
            Vector3 to = Flat(player.position - transform.position);
            float distance = to.magnitude;
            bool close = distance <= attackRange;
            if (distance > 0.05f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * dt);
            if (!agent.isOnOffMeshLink && cooldown <= 0f && distance <= chargeTriggerRange && distance > 0.01f
                && Mathf.Abs(player.position.y - transform.position.y) <= 1.75f
                && Vector3.Dot(transform.forward, to.normalized) > 0.90f && HasAttackSight())
            {
                // Lock the direction at the START of the warning, not at the end. A dodge during
                // the windup must work. Neither charging nor dazed states rotate toward the player.
                chargeDirection = to.normalized;
                transform.rotation = Quaternion.LookRotation(chargeDirection);
                StopMotion();
                state = AttackState.Windup;
                phaseTime = chargeWindupSeconds;
                PixelFx.Fan(transform.position + Vector3.up * 0.08f, chargeDirection,
                    Mathf.Min(chargeDistance, 7f), 8f, Palette.Amber, 16, 0.38f, 0.15f);
                Sfx.At(Sound.ScuttlerWindup, transform.position, 0.9f, 0.75f);
                return;
            }
            agent.isStopped = close;
            if (!close && repath <= 0f)
            {
                if (NavMesh.SamplePosition(player.position, out NavMeshHit target, 3f, agent.areaMask))
                    agent.SetDestination(target.position);
                repath = repathInterval;
            }
        }

        void StopMotion()
        {
            if (!agent || !agent.enabled) return;
            agent.speed = walkSpeed;
            if (!agent.isOnNavMesh) return;
            agent.isStopped = true;
            if (agent.hasPath || agent.pathPending) agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        void ResumePursuit()
        {
            state = AttackState.Pursue;
            cooldown = chargeCooldownSeconds;
            repath = 0f;
        }

        bool IsPlayerCollider(Collider collider)
        {
            return collider && player && (collider.transform.IsChildOf(player)
                || collider.GetComponentInParent<Health>() == playerHealth);
        }

        void StepCharge(float dt)
        {
            Vector3 from = agent.nextPosition;
            // If local navigation blocks actual movement despite the sweeps, still provide a stun window.
            if (previousChargeStep > 0.001f)
            {
                float progress = Vector3.Dot(Flat(from - previousChargePosition), chargeDirection);
                chargeStallTime = progress < previousChargeStep * 0.2f ? chargeStallTime + dt : 0f;
                if (chargeStallTime >= 0.18f)
                {
                    EndCharge(false, true, from + chargeDirection * agent.radius + Vector3.up * 0.6f, -chargeDirection);
                    return;
                }
            }
            float step = Mathf.Min(chargeRemaining, walkSpeed * chargeSpeedMultiplier * dt);
            if (step <= 0.0001f) { EndCharge(false, false, from, -chargeDirection); return; }

            // Stay on the NavMesh rather than pathfinding around the obstacle or running off a ledge.
            // The physics sweep catches solid geometry that is not baked into the NavMesh, and the
            // player. A little clearance prevents the capsule clipping into walls at low frame rates.
            const float skin = 0.035f;
            float radius = Mathf.Max(0.1f, agent.radius * 0.95f);
            Vector3 low = from + Vector3.up * (radius + 0.12f);
            Vector3 high = from + Vector3.up * Mathf.Max(radius + 0.12f, 1.6f - radius);
            float travel = step;
            bool blocked = false, hitPlayer = false;
            Vector3 impact = from + chargeDirection * (step + radius), normal = -chargeDirection;
            if (agent.Raycast(from + chargeDirection * step, out NavMeshHit boundary))
            {
                travel = Mathf.Clamp(Vector3.Dot(boundary.position - from, chargeDirection) - skin, 0f, step);
                blocked = true;
                impact = boundary.position + Vector3.up * 0.6f;
                normal = boundary.normal.sqrMagnitude > 0.01f ? boundary.normal : -chargeDirection;
            }

            // Cast all relevant hits and pick the closest one; a wall wins over a player behind it.
            int count = Physics.CapsuleCastNonAlloc(low, high, radius, chargeDirection, chargeHits,
                step + skin, ~0, QueryTriggerInteraction.Collide);
            RaycastHit[] hits = chargeHits;
            if (count == chargeHits.Length)
            {
                hits = Physics.CapsuleCastAll(low, high, radius, chargeDirection, step + skin, ~0, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                Collider c = h.collider;
                if (!c || c.transform.IsChildOf(transform)) continue;
                bool isPlayer = IsPlayerCollider(c);
                if (!isPlayer && (c.isTrigger || c.GetComponentInParent<IDamageable>() != null || h.normal.y > 0.65f)) continue;
                float stopAt = Mathf.Max(0f, h.distance - skin);
                if (stopAt > travel || (blocked && stopAt >= travel - 0.001f)) continue;
                travel = stopAt;
                hitPlayer = isPlayer;
                blocked = !isPlayer;
                impact = h.point;
                normal = h.normal.sqrMagnitude > 0.01f ? h.normal : -chargeDirection;
            }

            // Capsule casts do not report colliders that are already overlapping at their start.
            // Count a close player only if they are in front/alongside the charge, not behind the armor.
            if (!blocked && !hitPlayer && Vector3.Dot(Flat(player.position - from), chargeDirection) >= -0.15f)
            {
                int overlapCount = Physics.OverlapCapsuleNonAlloc(low, high, radius, chargeOverlaps, ~0, QueryTriggerInteraction.Collide);
                Collider[] overlaps = chargeOverlaps;
                if (overlapCount == chargeOverlaps.Length)
                {
                    overlaps = Physics.OverlapCapsule(low, high, radius, ~0, QueryTriggerInteraction.Collide);
                    overlapCount = overlaps.Length;
                }
                for (int i = 0; i < overlapCount; i++)
                {
                    if (!IsPlayerCollider(overlaps[i])) continue;
                    hitPlayer = true;
                    travel = 0f;
                    impact = player.position + Vector3.up * 0.75f;
                    break;
                }
            }

            previousChargePosition = from;
            previousChargeStep = travel;
            if (travel > 0f) agent.Move(chargeDirection * travel);
            chargeRemaining -= step;
            if (hitPlayer || blocked || chargeRemaining <= 0.001f)
            {
                EndCharge(hitPlayer, blocked, impact, normal);
                return;
            }
            if (Time.time >= nextFx)
            {
                nextFx = Time.time + 0.10f;
                PixelFx.Cone(transform.position + Vector3.up * 0.15f - chargeDirection * 0.6f,
                    -chargeDirection + Vector3.up * 0.25f, 32f, Palette.Steel, Palette.Amber, 4, 3f, 0.35f, 0.12f);
            }
        }

        void EndCharge(bool hitPlayer, bool blocked, Vector3 impact, Vector3 normal)
        {
            StopMotion();
            if (hitPlayer)
            {
                state = AttackState.Recover;
                phaseTime = hitRecoverySeconds;
                if (playerHealth && !playerHealth.IsDead)
                    playerHealth.TakeDamage(new DamageInfo { Amount = attackDamage, Kind = DamageKind.Bite,
                        Point = impact, Direction = chargeDirection, Distance = Vector3.Distance(transform.position, player.position),
                        Hits = 1, Source = gameObject });
                PixelFx.Sparks(impact, -chargeDirection, Palette.Amber, 10, 5f, 0.25f);
                return;
            }
            state = AttackState.Dazed;
            phaseTime = blocked ? wallDazeSeconds : missedChargeDazeSeconds;
            nextFx = 0f;
            if (blocked)
            {
                PixelFx.Sparks(impact, normal, Palette.Amber, 16, 7f, 0.38f);
                PixelFx.Burst(transform.position + Vector3.up * 0.25f, Palette.Steel, Palette.Amber, 12, 3.5f, 0.4f, 0.14f);
                PixelFx.Ring(transform.position, 2f, Palette.Amber, 18, 0.3f, 0.15f);
                Sfx.At(Sound.ScuttlerBite, transform.position, 0.8f, 0.6f);
            }
        }

        void DazeFeedback()
        {
            if (Time.time < nextFx) return;
            nextFx = Time.time + 0.065f;
            for (int i = 0; i < 3; i++)
            {
                float angle = Time.time * 5f + i * Mathf.PI * 2f / 3f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 0.65f, 1.85f + Mathf.Sin(angle * 2f) * 0.1f,
                    Mathf.Sin(angle) * 0.65f);
                PixelFx.Mote(transform.position + offset, Vector3.zero, i == 0 ? Palette.White : Palette.Yellow, 0.15f, 0.09f);
            }
        }

        bool HasAttackSight()
        {
            Vector3 from = transform.position + Vector3.up * 1.0f;
            Vector3 to = player.position + Vector3.up * 0.9f - from;
            if (to.sqrMagnitude < 0.01f) return true;
            return !Raycasts.Nearest(from, to.normalized, to.magnitude, transform, out RaycastHit hit)
                || hit.collider.transform.IsChildOf(player);
        }

        void LateUpdate()
        {
            Vector3 movement = Flat(transform.position - lastPosition);
            lastPosition = transform.position;
            stride += movement.magnitude * 2.7f;
            if (!body || !spriteMaterial) return;
            if (!cameraTransform)
            {
                Camera cam = Camera.main ? Camera.main : FindAnyObjectByType<Camera>();
                if (!cam) return;
                cameraTransform = cam.transform;
            }
            Vector3 towardCamera = Flat(cameraTransform.position - transform.position);
            if (towardCamera.sqrMagnitude < 0.001f) return;
            int view = (Mathf.RoundToInt(Vector3.SignedAngle(transform.forward, towardCamera, Vector3.up) / 45f) + 8) % 8;
            int frame = IsDazed ? 0 : movement.sqrMagnitude > 0.000001f ? Mathf.FloorToInt(stride) % 4 : (Mathf.FloorToInt(Time.time * 2f) % 2);
            if (view != shownView || frame != shownFrame)
            {
                Vector2 offset = new Vector2(view / 8f, (3 - frame) / 4f);
                spriteMaterial.SetTextureOffset("_BaseMap", offset);
                spriteMaterial.SetTextureOffset("_MainTex", offset);
                shownView = view; shownFrame = frame;
            }
            body.rotation = Quaternion.LookRotation(-towardCamera, Vector3.up);
            bool windingUp = state == AttackState.Windup;
            float braced = windingUp ? 0.13f : IsDazed ? 0.07f : 0f;
            float shake = windingUp ? Mathf.Sin(Time.time * 46f) * 0.035f : IsDazed ? Mathf.Sin(Time.time * 13f) * 0.035f : 0f;
            body.localPosition = bodyRest - Vector3.up * braced + Vector3.right * shake;
            body.localScale = new Vector3(bodyScale.x * (1f + braced), bodyScale.y * (1f - braced), bodyScale.z);
            Color tint = Time.time < flashUntil ? new Color(0.65f, 0.90f, 1f) :
                windingUp && Mathf.FloorToInt(Time.time * 12f) % 2 == 0 ? new Color(1f, 0.65f, 0.38f) : Color.white;
            if (tint != shownTint)
            {
                shownTint = tint;
                spriteMaterial.SetColor(BaseColorId, tint);
            }
        }

        void OnDestroy() { if (spriteMaterial) Destroy(spriteMaterial); }
    }
}
