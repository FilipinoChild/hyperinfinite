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
            // Burst into bits of its sprite: more of them, thrown further, the tougher the enemy was
            int pieces = Mathf.Clamp(Mathf.RoundToInt(health.Max / 5f), 10, 60);
            Vector3 middle = transform.position + Vector3.up * 0.8f;
            PixelFx.Burst(middle, Palette.Red, Palette.Navy, pieces, 4f + pieces * 0.08f, 0.55f, 0.16f);
            PixelFx.Flash(middle, Palette.White, 0.9f);
            // A squelching crunch, deeper the tougher it was; the Hive Mother gets a death cry of her own.
            if (GetComponent<Boss>()) Sfx.At(Sound.HiveDeath, middle);
            else if (GetComponent<SpitterGlob>()) Sfx.At(Sound.GlobPop, transform.position);   // an acid glob shot out of the air
            else Sfx.At(Sound.ScuttlerDeath, middle, 1f, Mathf.Clamp(100f / health.Max, 0.7f, 1.3f));
            // Remove this enemy from the scene
            Destroy(gameObject);
        }
    }
}