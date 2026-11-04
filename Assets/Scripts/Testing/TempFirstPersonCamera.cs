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
    ///  - it narrows the view while LeverShotgun.AimBlend rises and kicks up on its onRecoil event.
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

        Camera view;
        PlayerMotor motor;
        EnergyBlastShotgun shotgun;
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
            view.fieldOfView = fieldOfView;
            float x = transform.localEulerAngles.x;
            pitch = x > 180f ? x - 360f : x;
            eyeHeight = transform.localPosition.y;
        }

        void OnEnable()
        {
            if (motor) motor.onLand.AddListener(OnLand);
            if (shotgun) shotgun.onRecoil.AddListener(OnRecoil);
        }

        void OnDisable()
        {
            if (motor) motor.onLand.RemoveListener(OnLand);
            if (shotgun) shotgun.onRecoil.RemoveListener(OnRecoil);
        }

        void Update()
        {
            if (!controls) return;
            Vector2 look = controls.Look;
            if (body) body.Rotate(0f, look.x, 0f, Space.World);
            pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
            recoil *= Mathf.Pow(0.0008f, Time.deltaTime);   // the web prototype's settle: about half gone in 0.1 s
            transform.localRotation = Quaternion.Euler(pitch - recoil, 0f, 0f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float target = Mathf.Lerp(fieldOfView, aimFieldOfView, shotgun ? shotgun.AimBlend : 0f);
            if (motor && motor.IsDashing) target = dashFieldOfView;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, target, 1f - Mathf.Exp(-fieldOfViewSpeed * dt));

            dip = Mathf.Lerp(dip, 0f, 1f - Mathf.Exp(-10f * dt));
            Vector3 p = transform.localPosition;
            p.y = eyeHeight - dip;
            transform.localPosition = p;
        }

        void OnRecoil(float degrees)
        {
            recoil += degrees;
        }

        void OnLand(float impactSpeed)
        {
            dip = Mathf.Max(dip, landingDip * Mathf.Clamp01((impactSpeed - 4f) / 12f));
        }
    }
}
