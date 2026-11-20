using UnityEngine;

namespace HyperInfinite
{
    /// Lets an enemy catch fire when shot while INFINITE is active.
    /// Burning deals damage over time and flickers the sprite orange.
    /// If a burning enemy dies, the fire spreads to enemies near it.
    [RequireComponent(typeof(Health))]
    public class Burnable : MonoBehaviour
    {
        [SerializeField, Min(0f)] float burnDuration = 3f;
        [SerializeField, Min(0f)] float burnDamagePerTick = 4f;
        [SerializeField, Min(0.05f)] float tickInterval = 0.5f;
        [Tooltip("When a burning enemy dies, enemies within this many meters catch fire.")]
        [SerializeField, Min(0f)] float spreadRadius = 4f;

        Health health;
        SpriteRenderer sprite;
        Color baseColor;
        float burnTimeLeft, tickTimer;

        void Awake()
        {
            health = GetComponent<Health>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite) baseColor = sprite.color;
        }

        void OnEnable()
        {
            health.onDamaged.AddListener(OnDamaged);
            health.onDied.AddListener(OnDied);
        }

        void OnDisable()
        {
            health.onDamaged.RemoveListener(OnDamaged);
            health.onDied.RemoveListener(OnDied);
        }

        /// Sets this enemy on fire (or restarts the fire if it's already burning).
        public void Ignite()
        {
            burnTimeLeft = burnDuration;
        }

        /// Called by Health whenever this enemy takes damage.
        void OnDamaged(float amount)
        {
            // Only shotgun hits during INFINITE set it on fire.
            // Burn damage is Generic, so burning can't re-ignite itself.
            if (!InfiniteBuff.IsActive) return;
            if (health.LastDamage.Kind != DamageKind.Shotgun) return;
            Ignite();
        }

        /// Called by Health when this enemy dies. A burning enemy spreads its fire.
        void OnDied()
        {
            if (burnTimeLeft <= 0f) return;

            Collider[] nearby = Physics.OverlapSphere(transform.position, spreadRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in nearby)
            {
                Burnable other = hit.GetComponentInParent<Burnable>();
                if (other && other != this) other.Ignite();
            }
        }

        void Update()
        {
            if (burnTimeLeft <= 0f) return;

            float dt = Time.deltaTime;
            burnTimeLeft -= dt;
            tickTimer -= dt;

            // Flicker between its normal color and fire orange
            if (sprite)
            {
                float flicker = 0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
                sprite.color = Color.Lerp(baseColor, new Color(1f, 0.45f, 0.1f), flicker);
            }

            if (tickTimer <= 0f)
            {
                tickTimer = tickInterval;
                health.TakeDamage(new DamageInfo
                {
                    Amount = burnDamagePerTick,
                    Kind = DamageKind.Generic,
                    Point = transform.position,
                    Direction = Vector3.up,
                    Distance = 0f,
                    Hits = 1,
                    Source = gameObject,
                });
            }

            if (burnTimeLeft <= 0f && sprite) sprite.color = baseColor;   // fire's out
        }
    }
}