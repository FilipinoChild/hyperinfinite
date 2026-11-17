using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// TEMPORARY.
    ///
    /// Currently a simple first-person camera that:
    ///  - it sits on a child of the Player at eye height;
    ///  - it turns the Player root left and right (PlayerMotor moves the way the root faces);
    ///  - it tilts itself up and down;
    ///  - it narrows the view while EnergyBlastShotgun.AimBlend rises and kicks up on its onRecoil event;
    ///  - it drops to the ground and tips over when the player goes down, and gets back up on respawn;
    ///  - it jolts sideways when the player is hit;
    ///  - it bobs gently with each step while running, in time with the gun.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]   // after PlayerControls reads input, before movement and shooting
    public class TempFirstPersonCamera : MonoBehaviour
    {
        [SerializeField] PlayerControls controls;
        [Tooltip("Turned left and right by Look. Defaults to the object with PlayerControls.")]
        [SerializeField] Transform body;
        [SerializeField, Range(45f, 89f)] float pitchLimit = 85f;
        [SerializeField, Range(50f, 110f)] float fieldOfView = 75f;

        [Header("Feel")]
        [Tooltip("Field of view when fully aimed down the sights.")]
        [SerializeField, Range(30f, 90f)] float aimFieldOfView = 58f;
        [Tooltip("Field of view while thruster-dashing; the widening sells the speed.")]
        [SerializeField, Range(50f, 120f)] float dashFieldOfView = 88f;
        [Tooltip("How quickly the field of view follows. Higher is snappier.")]
        [SerializeField, Min(0f)] float fieldOfViewSpeed = 12f;
        [Tooltip("Meters the view dips on a hard landing.")]
        [SerializeField, Range(0f, 0.3f)] float landingDip = 0.08f;
        [Tooltip("Meters the view bobs with each step while running. 0 switches it off.")]
        [SerializeField, Range(0f, 0.08f)] float viewBob = 0.025f;
        [Tooltip("Eye height while down, in meters above the feet.")]
        [SerializeField, Range(0.1f, 1f)] float downEyeHeight = 0.4f;
        [Tooltip("Degrees the view tips over while down.")]
        [SerializeField, Range(0f, 60f)] float downTilt = 24f;

        Camera view;
        PlayerMotor motor;
        EnergyBlastShotgun shotgun;
        Health health;
        float down;    // 0 standing, 1 flat on the ground
        float jolt;    // degrees of sideways tilt from the last hit, settling back to 0
        float bobTime, bobAmount;
        float pitch;   // degrees; positive looks down, as Unity rotates about X
        float recoil;  // degrees of upward kick that settles back down
        float eyeHeight, dip;

        public Camera View => view;

        void Awake()
        {
            view = GetComponent<Camera>();
            if (!controls) controls = GetComponentInParent<PlayerControls>();
            if (!body) body = controls ? controls.transform : transform.parent;
            motor = GetComponentInParent<PlayerMotor>();
            shotgun = GetComponentInChildren<EnergyBlastShotgun>();
            health = GetComponentInParent<Health>();
            view.fieldOfView = fieldOfView;
            float x = transform.localEulerAngles.x;
            pitch = x > 180f ? x - 360f : x;
            eyeHeight = transform.localPosition.y;
        }

        void OnEnable()
        {
            if (motor) motor.onLand.AddListener(OnLand);
            if (shotgun) shotgun.onRecoil.AddListener(OnRecoil);
            if (health) health.onDamaged.AddListener(OnDamaged);
        }

        void OnDisable()
        {
            if (motor) motor.onLand.RemoveListener(OnLand);
            if (shotgun) shotgun.onRecoil.RemoveListener(OnRecoil);
            if (health) health.onDamaged.RemoveListener(OnDamaged);
        }

        void Update()
        {
            if (!controls) return;
            Vector2 look = controls.Look;
            if (body) body.Rotate(0f, look.x, 0f, Space.World);
            pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
            float settle = Mathf.Pow(0.0008f, Time.deltaTime);   // the web prototype's settle: about half gone in 0.1 s
            recoil *= settle;
            jolt *= settle;
            transform.localRotation = Quaternion.Euler(pitch - recoil, 0f, downTilt * Mathf.SmoothStep(0f, 1f, down) + jolt);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float target = Mathf.Lerp(fieldOfView, aimFieldOfView, shotgun ? shotgun.AimBlend : 0f);
            if (motor && motor.IsDashing) target = dashFieldOfView;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, target, 1f - Mathf.Exp(-fieldOfViewSpeed * dt));

            // Going down drops the view to the ground quickly; getting up is quicker still.
            bool dead = health && health.IsDead;
            down = Mathf.MoveTowards(down, dead ? 1f : 0f, dt * (dead ? 2.5f : 5f));

            // Running bobs the view on the same beat as ShotgunSprite bobs the gun.
            bool running = !dead && motor && motor.IsGrounded && motor.Speed > 0.5f;
            bobAmount = Mathf.MoveTowards(bobAmount, running ? 1f : 0f, dt * 6f);
            if (running) bobTime += dt * 11f;
            float bob = Mathf.Abs(Mathf.Sin(bobTime * 0.5f)) * viewBob * bobAmount;

            dip = Mathf.Lerp(dip, 0f, 1f - Mathf.Exp(-10f * dt));
            Vector3 p = transform.localPosition;
            p.y = Mathf.Lerp(eyeHeight, downEyeHeight, Mathf.SmoothStep(0f, 1f, down)) - dip - bob;
            transform.localPosition = p;
        }

        void OnRecoil(float degrees)
        {
            recoil += degrees;
        }

        /// <summary>A hit knocks the view to one side or the other, harder for a bigger hit.</summary>
        void OnDamaged(float amount)
        {
            jolt = (Random.value < 0.5f ? -1f : 1f) * Mathf.Clamp(amount * 0.35f, 1.5f, 6f);
        }

        void OnLand(float impactSpeed)
        {
            dip = Mathf.Max(dip, landingDip * Mathf.Clamp01((impactSpeed - 4f) / 12f));
        }
    }
}
