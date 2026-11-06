using System.Collections.Generic;
using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// HAZARD UNIQUE MECHANIC (1): the swarm the Pulsating Eggs erupt into. Tiny Scuttlers, fresh out of
    /// the eggs and far smaller than the regular Scuttler. Like everything alive in this game they're
    /// flat pixel art that always faces the camera: the original Scuttler sprite, at 30% of
    /// its 2.8 x 1.62 m. They hop after the player in a weaving swarm, nibble, and wither after a few
    /// seconds so the level never fills up. One bolt kills one.
    ///
    /// The collider is a trigger, so they never block the player's movement; shots still hit them because
    /// Raycasts counts triggers that can take damage.
    /// </summary>
    [DisallowMultipleComponent]
    public class TinyScuttler : MonoBehaviour, IDamageable, IHatchling
    {
        [Tooltip("The sprite: a quad showing the Scuttler pixel art. It turns to face the camera, and squashes and stretches as it hops.")]
        [SerializeField] Transform body;
        [Tooltip("Meters per second across the ground.")]
        [SerializeField, Min(0f)] float speed = 6.5f;
        [Tooltip("How quickly it turns toward its target. Higher is twitchier.")]
        [SerializeField, Min(0f)] float steering = 9f;
        [SerializeField, Min(0f)] float hopSpeed = 3.2f;
        [SerializeField, Min(0f)] float gravity = 22f;
        [Header("Bite")]
        [Tooltip("Each one is small, so it bites softly; the danger is how many there are.")]
        [SerializeField, Min(0f)] float biteDamage = 2.5f;
        [Tooltip("Seconds between one Scuttler's bites.")]
        [SerializeField, Min(0.05f)] float biteInterval = 0.6f;
        [Tooltip("How close it has to be to bite, in meters across the ground.")]
        [SerializeField, Min(0f)] float biteRange = 0.9f;
        [Header("Life")]
        [Tooltip("Seconds before it withers away on its own.")]
        [SerializeField, Min(0.1f)] float lifetime = 6f;
        [SerializeField, Min(0.05f)] float radius = 0.3f;
        [SerializeField] Color gooColor = new Color(0.62f, 0.85f, 0.25f, 1f);

        /// <summary>Every live one, so the swarm keeps its spacing and respawns can clear them.</summary>
        public static readonly List<TinyScuttler> Active = new List<TinyScuttler>();

        Transform target, view;
        IDamageable prey;
        Vector3 velocity, bodyScale;
        float age, biteTimer, hopTimer, wither, seed;
        bool grounded, withering;

        /// <summary>Called by the eggs: sets it loose with a leap, chasing <paramref name="target"/> (may be null).</summary>
        public void Hatch(Transform target, Vector3 launchVelocity)
        {
            this.target = target;
            prey = target ? target.GetComponentInParent<IDamageable>() : null;
            velocity = launchVelocity;
            grounded = false;
        }

        /// <summary>Removes every one at once without the splash, for respawns and resets.</summary>
        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i]) Destroy(Active[i].gameObject);
            Active.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
        }

        void Awake()
        {
            if (!body) body = transform;
            bodyScale = body.localScale;
            seed = Random.value * 100f;
            hopTimer = Random.Range(0.1f, 0.3f);
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public void TakeDamage(DamageInfo damage)
        {
            if (withering) return;
            StreakFx.Burst(transform.position + Vector3.up * 0.2f, gooColor, 8, 3.5f, 0.35f);
            Destroy(gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (withering)
            {
                wither += dt / 0.4f;
                body.localScale = bodyScale * Mathf.Max(0f, 1f - wither);
                if (wither >= 1f) Destroy(gameObject);
                return;
            }
            if (age >= lifetime)
            {
                withering = true;
                return;
            }

            Vector3 pos = transform.position;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Vector3.Lerp(horizontal, DesiredVelocity(pos), 1f - Mathf.Exp(-steering * dt));

            // Slide along walls rather than through them.
            float stepLength = horizontal.magnitude * dt;
            if (stepLength > 1e-4f && Raycasts.Nearest(pos + Vector3.up * radius, horizontal / horizontal.magnitude,
                                                       radius + stepLength, Ignore(), out RaycastHit wall, damageableTriggers: false))
            {
                Vector3 n = new Vector3(wall.normal.x, 0f, wall.normal.z);
                if (n.sqrMagnitude > 1e-4f)
                {
                    n.Normalize();
                    horizontal -= n * Mathf.Min(0f, Vector3.Dot(horizontal, n));
                }
            }

            if (grounded)
            {
                hopTimer -= dt;
                if (hopTimer <= 0f)
                {
                    velocity.y = hopSpeed;
                    grounded = false;
                    hopTimer = Random.Range(0.25f, 0.45f);
                }
            }
            velocity.y -= gravity * dt;
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            pos += velocity * dt;

            // Stand on whatever is underneath: floor, ramps, ledges.
            grounded = false;
            float drop = Mathf.Max(0f, -velocity.y * dt);
            if (velocity.y <= 0f && Raycasts.Nearest(pos + Vector3.up * 0.6f, Vector3.down, 0.6f + drop + 0.05f, Ignore(),
                                                     out RaycastHit ground, damageableTriggers: false))
            {
                if (pos.y <= ground.point.y + 0.01f)
                {
                    pos.y = ground.point.y;
                    velocity.y = 0f;
                    grounded = true;
                }
            }
            if (pos.y < -50f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = pos;

            if (horizontal.sqrMagnitude > 0.05f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horizontal), 1f - Mathf.Exp(-12f * dt));
            float stretch = Mathf.Clamp(velocity.y * 0.06f, -0.25f, 0.3f);
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f));

            Bite(pos, dt);
        }

        /// <summary>Turns the sprite to face the camera, the way the prototype's sprites do, after everything has moved.</summary>
        void LateUpdate()
        {
            if (!view)
            {
                Camera main = Camera.main;
                if (!main) return;
                view = main.transform;
            }
            body.rotation = view.rotation;
        }

        Vector3 DesiredVelocity(Vector3 pos)
        {
            Vector3 desired = Vector3.zero;
            if (target)
            {
                Vector3 to = target.position - pos;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > 0.05f) desired = to / distance * speed;
                if (distance < biteRange * 0.8f) desired *= 0.2f;   // already on top of you: just nibble
                // Weave a little so the swarm doesn't arrive in a straight line.
                desired = Quaternion.Euler(0f, Mathf.Sin(age * 6f + seed) * 25f, 0f) * desired;
            }
            // Keep a little apart from the rest of the swarm.
            foreach (TinyScuttler other in Active)
            {
                if (other == this) continue;
                Vector3 away = pos - other.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d > 0.001f && d < 0.7f) desired += away / d * (0.7f - d) * 6f;
            }
            return desired;
        }

        void Bite(Vector3 pos, float dt)
        {
            biteTimer -= dt;
            if (!target || prey == null || biteTimer > 0f) return;
            Vector3 to = target.position - pos;
            float across = new Vector2(to.x, to.z).magnitude;
            if (across > biteRange || to.y < -0.8f || to.y > 1.5f) return;
            prey.TakeDamage(new DamageInfo
            {
                Amount = biteDamage, Kind = DamageKind.Bite, Point = pos, Direction = to.normalized,
                Distance = across, Hits = 1, Source = gameObject,
            });
            biteTimer = biteInterval;
            if (grounded)
            {
                velocity.y = hopSpeed * 0.8f;   // a little lunge
                grounded = false;
            }
        }

        /// <summary>Ground and wall checks ignore the target, so a Scuttler doesn't stand on the player's head.</summary>
        Transform Ignore() => target ? target : transform;
    }
}
