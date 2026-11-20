using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// Plain hit points for anything that can be hurt and killed. The test Player uses it so the
    /// hazard has something to hurt. The shotgun and the eggs only talk to IDamageable, so anything implementing that works.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [Tooltip("Seconds of immunity after each hit, so a swarm can't shred you within a single frame. 0 = none.")]
        [SerializeField, Min(0f)] float hitImmunity = 0f;

        [Header("Events")]
        [Tooltip("Sent with the damage taken.")]
        public UnityEvent<float> onDamaged = new UnityEvent<float>();
        public UnityEvent onDied = new UnityEvent();
        [Tooltip("Back on its feet after Revive.")]
        public UnityEvent onRevived = new UnityEvent();
        [Tooltip("Sent with the health actually given back, so nothing is sent at full health.")]
        public UnityEvent<float> onHealed = new UnityEvent<float>();

        /// <summary>
        /// Raised when anything with Health dies, with the Health that died, so kills can be counted (for power-up
        /// drops, say) without wiring up every enemy.
        /// </summary>
        public static event System.Action<Health> AnyDied;

        float current;
        float immuneUntil = -1f;

        public float Current => current;
        public float Max => maxHealth;
        public float Fraction => current / maxHealth;
        public bool IsDead => current <= 0f;
        public DamageInfo LastDamage { get; private set; }
        public float LastDamageTime { get; private set; } = -100f;

        void Awake()
        {
            current = maxHealth;
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (IsDead || damage.Amount <= 0f || Time.time < immuneUntil) return;
            current = Mathf.Max(0f, current - damage.Amount);
            immuneUntil = Time.time + hitImmunity;
            LastDamage = damage;
            LastDamageTime = Time.time;
            onDamaged.Invoke(damage.Amount);
            if (current <= 0f)
            {
                onDied.Invoke();
                AnyDied?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float before = current;
            current = Mathf.Min(maxHealth, current + amount);
            if (current > before) onHealed.Invoke(current - before);
        }

        /// <summary>
        /// Back to full health, for respawning. Protection is seconds of immunity afterwards, so whatever was
        /// attacking can't knock you straight back down.
        /// </summary>
        public void Revive(float protection = 0f)
        {
            current = maxHealth;
            immuneUntil = Time.time + protection;
            onRevived.Invoke();
        }

        /// <summary>Immune for the next <paramref name="seconds"/>, or longer if it already was: a moment of cover while committing to an attack.</summary>
        public void Protect(float seconds)
        {
            immuneUntil = Mathf.Max(immuneUntil, Time.time + seconds);
        }

        /// <summary>True while immune after a hit or a revive.</summary>
        public bool IsProtected => Time.time < immuneUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AnyDied = null;   // with domain reload off, nothing from the last Play stays subscribed
        }
    }
}
