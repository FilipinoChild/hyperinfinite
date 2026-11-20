using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// Tints a sprite (or UI image) while INFINITE is active, with a gentle pulse,
    /// and puts the original colors back when it ends. Put it on the gun's visual.
    public class PowerUpTint : MonoBehaviour
    {
        [SerializeField] Color tint = new Color(0f, 0.9f, 1f);   // cyan
        [Tooltip("How strongly to tint. 1 = fully the tint color.")]
        [SerializeField, Range(0f, 1f)] float strength = 0.7f;
        [SerializeField, Min(0f)] float pulseSpeed = 6f;

        SpriteRenderer[] sprites;
        Color[] spriteColors;
        Graphic[] images;
        Color[] imageColors;
        bool wasActive;

        void Awake()
        {
            // Remember the original colors of every sprite and UI image on this object and its children
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            spriteColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;

            images = GetComponentsInChildren<Graphic>(true);
            imageColors = new Color[images.Length];
            for (int i = 0; i < images.Length; i++) imageColors[i] = images[i].color;
        }

        void LateUpdate()
        {
            bool active = InfiniteBuff.IsActive;
            if (!active && !wasActive) return;   // nothing to do

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);
            float amount = active ? strength * pulse : 0f;   // 0 puts the original colors back

            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) sprites[i].color = Color.Lerp(spriteColors[i], tint, amount);
            for (int i = 0; i < images.Length; i++)
                if (images[i]) images[i].color = Color.Lerp(imageColors[i], tint, amount);

            wasActive = active;
        }
    }
}