using UnityEngine;

namespace HyperInfinite
{
    /// Gives an enemy a chance to drop a pickup when it dies.
    [RequireComponent(typeof(Health))]
    public class PowerUpDropper : MonoBehaviour
    {
        [SerializeField] GameObject pickupPrefab;
        [Tooltip("0 = never, 1 = always.")]
        [SerializeField, Range(0f, 1f)] float dropChance = 0.15f;

        Health health;

        void Awake() { health = GetComponent<Health>(); }
        void OnEnable() { health.onDied.AddListener(Drop); }
        void OnDisable() { health.onDied.RemoveListener(Drop); }

        void Drop()
        {
            if (!pickupPrefab || Random.value > dropChance) return;
            Instantiate(pickupPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }
    }
}