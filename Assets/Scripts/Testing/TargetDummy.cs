using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// TEMPORARY. A target for tuning the shotgun: it takes any amount of damage without dying,
    /// rocks back and flashes when hit, and remembers what the last hit did. It can also slide side
    /// to side for practice against a moving target.
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetDummy : MonoBehaviour, IDamageable
    {
        [Tooltip("The part that rocks back when hit. Its pivot should be at the dummy's feet.")]
        [SerializeField] Transform wobble;
        [Tooltip("Slides this many meters to either side. 0 stands still.")]
        [SerializeField, Min(0f)] float strafeDistance;
        [Tooltip("Meters per second while sliding.")]
        [SerializeField, Min(0f)] float strafeSpeed = 3f;

        public DamageInfo LastHit { get; private set; }
        public float LastHitTime { get; private set; } = -100f;
        public float TotalDamage { get; private set; }

        Vector3 home;
        float tilt, tiltSpeed, flash, strafeClock;
        Renderer[] parts;
        Color[] baseColors;
        MaterialPropertyBlock block;

        void Awake()
        {
            home = transform.position;
            if (!wobble) wobble = transform;
            parts = GetComponentsInChildren<Renderer>();
            baseColors = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var mat = parts[i].sharedMaterial;
                baseColors[i] = mat && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                               : mat && mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            }
            block = new MaterialPropertyBlock();
        }

        public void TakeDamage(DamageInfo damage)
        {
            LastHit = damage;
            LastHitTime = Time.time;
            TotalDamage += damage.Amount;
            tiltSpeed += Mathf.Min(damage.Amount, 80f) * 5f;   // a bigger hit rocks it further
            flash = 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (strafeDistance > 0f)
            {
                strafeClock += dt * strafeSpeed / strafeDistance;
                transform.position = home + transform.right * (Mathf.Sin(strafeClock) * strafeDistance);
            }

            // A damped spring rocks it back, away from the player, and upright again.
            tiltSpeed += (-tilt * 140f - tiltSpeed * 10f) * dt;
            tilt += tiltSpeed * dt;
            wobble.localRotation = Quaternion.Euler(Mathf.Clamp(tilt, -35f, 35f), 0f, 0f);

            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 6f);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (flash <= 0f)
                    {
                        parts[i].SetPropertyBlock(null);
                        continue;
                    }
                    Color c = Color.Lerp(baseColors[i], Color.white, flash * 0.8f);
                    block.Clear();
                    block.SetColor("_BaseColor", c);   // URP Lit
                    block.SetColor("_Color", c);       // Built-in Standard
                    parts[i].SetPropertyBlock(block);
                }
            }
        }
    }
}
