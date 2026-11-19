using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// Makes a light breathe: it dims and brightens slowly, in a few steps like the rest of the game's
    /// animation. Each light starts at a different point in the cycle, so a field of crystals never
    /// pulses in unison. The light is only touched when it moves to the next step.
    ///
    /// It also sheds the odd glowing speck in the light's color, a PixelFx square that rises and
    /// shrinks away, but only while the camera is near enough to see it.
    ///
    /// Put it on any Light. The brightness set on the Light is its brightest.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class GlowPulse : MonoBehaviour
    {
        [Tooltip("How far it dims at its lowest: 0.35 dims to 65% of the Light's brightness.")]
        [SerializeField, Range(0f, 1f)] float depth = 0.35f;
        [Tooltip("Seconds for one full dim and brighten.")]
        [SerializeField, Min(0.2f)] float period = 3.5f;
        [Tooltip("How many brightness steps there are between dimmest and brightest.")]
        [SerializeField, Range(1, 16)] int steps = 6;
        [Tooltip("Glowing specks shed each second. 0 for none.")]
        [SerializeField, Range(0f, 5f)] float specksPerSecond = 0.7f;
        [Tooltip("Specks appear within this distance of the light, in meters.")]
        [SerializeField, Min(0f)] float speckSpread = 1.1f;
        [Tooltip("No specks are made while the camera is farther away than this, in meters.")]
        [SerializeField, Min(0f)] float speckViewRange = 32f;

        Light glow;
        Transform view;
        float brightest, offset, owed;
        int shownStep = -1;

        void Awake()
        {
            glow = GetComponent<Light>();
            brightest = glow.intensity;
            offset = Random.value * period;
            owed = Random.value;
        }

        void Update()
        {
            float wave = 0.5f + 0.5f * Mathf.Sin((Time.time + offset) / period * Mathf.PI * 2f);   // 0 at its dimmest, 1 at its brightest
            int step = Mathf.RoundToInt(wave * steps);
            if (step != shownStep)
            {
                shownStep = step;
                glow.intensity = brightest * (1f - depth * (1f - (float)step / steps));
            }
            if (specksPerSecond > 0f) Shed();
        }

        void Shed()
        {
            owed += specksPerSecond * Time.deltaTime;
            if (owed < 1f) return;
            owed -= 1f;
            if (!view)
            {
                Camera main = Camera.main;
                if (!main) return;
                view = main.transform;
            }
            Vector3 position = transform.position;
            if ((view.position - position).sqrMagnitude > speckViewRange * speckViewRange) return;
            position += Random.insideUnitSphere * speckSpread;
            var rise = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.3f, 0.55f), Random.Range(-0.1f, 0.1f));
            PixelFx.Mote(position, rise, glow.color, Random.Range(0.045f, 0.07f), Random.Range(2f, 3.2f));
        }
    }
}
