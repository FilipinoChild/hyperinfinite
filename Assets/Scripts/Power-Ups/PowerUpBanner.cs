using TMPro;
using UnityEngine;

namespace HyperInfinite
{
    /// A line of text at the bottom of the screen. Any script can show a message on it
    /// with PowerUpBanner.Show(...). It stays up for a few seconds, then fades out.
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PowerUpBanner : MonoBehaviour
    {
        // The banner in the current scene, so other scripts don't need a reference to it
        static PowerUpBanner instance;

        [Tooltip("Seconds the text takes to fade out at the end.")]
        [SerializeField, Min(0.01f)] float fadeTime = 0.5f;

        TextMeshProUGUI label;
        float timeLeft;

        void Awake()
        {
            instance = this;
            label = GetComponent<TextMeshProUGUI>();
            label.alpha = 0f;   // hidden until a message is shown
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// Shows a message for the given number of seconds.
        public static void Show(string message, float seconds)
        {
            if (!instance) return;   // no banner in this scene: do nothing
            instance.label.text = message;
            instance.timeLeft = seconds;
        }

        void Update()
        {
            if (timeLeft > 0f) timeLeft -= Time.deltaTime;
            // Fully visible until the last moments, then fades to invisible
            label.alpha = Mathf.Clamp01(timeLeft / fadeTime);
        }
    }
}