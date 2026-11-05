using UnityEngine;

namespace HyperInfinite
{
    // Unity adds a Health component automatically if one is missing
    [RequireComponent(typeof(Health))]
    public class EnemyDeath : MonoBehaviour
    {
        Health health;   // the Health on this same enemy

        void Awake()
        {
            // Find the Health component on this object
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            // Start listening: when Health announces death, run Die
            health.onDied.AddListener(Die);
        }

        void OnDisable()
        {
            // Stop listening when this object is turned off or destroyed
            health.onDied.RemoveListener(Die);
        }

        void Die()
        {
            Debug.Log(name + " died");
            // Remove this enemy from the scene
            Destroy(gameObject);
        }
    }
}