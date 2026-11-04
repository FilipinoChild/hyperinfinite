using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>A summary of the last blast, for readouts and tuning.</summary>
    public struct ShotReport
    {
        public int Pellets, PelletsOnTarget;
        public float Damage, Distance;
        public string Target;
        public float Time;
    }

    /// <summary>
    /// COMBAT MECHANIC 1. John Infinite's Winchester Model 1887 (temp -> moving to energy blast), a lever-action
    /// shotgun. Each blast sends a cone of pellets from the center of the screen; every pellet does
    /// full damage up close and less further out. Between shots the lever has to cycle.
    ///
    /// Heat is the resource to manage. Every shot adds heat and the gun cools when rested. Reach
    /// 100 and it overheats, locking up for a few seconds. Venting (R) dumps heat fast, but you
    /// can't fire while it does. Aiming (right mouse) tightens the cone so more pellets land at range.
    ///
    /// Put this on the gun model under the camera. Pellets fly from the camera's center, so they
    /// land around the crosshair; tracers start at the muzzle so they look right.
    /// Anything with an IDamageable component takes the hits.
    /// </summary>
    [DisallowMultipleComponent]
    public class LeverShotgun : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] PlayerControls controls;
        [Tooltip("Pellets fly from the center of this camera. Defaults to the camera above this object.")]
        [SerializeField] Transform aimOrigin;
        [Tooltip("Tracers start here.")]
        [SerializeField] Transform muzzle;
        [Tooltip("Swings down and back while the lever cycles. Optional.")]
        [SerializeField] Transform lever;
        [Tooltip("Lights up for each shot. Optional.")]
        [SerializeField] Light muzzleFlash;

        [Header("Blast")]
        [SerializeField, Range(1, 30)] int pellets = 9;
        [Tooltip("Damage per pellet at close range.")]
        [SerializeField, Min(0f)] float pelletDamage = 9f;
        [Tooltip("Half-angle of the pellet cone, in degrees, firing from the hip.")]
        [SerializeField, Range(0f, 20f)] float hipSpread = 5.7f;
        [SerializeField, Min(1f)] float range = 70f;
        [Tooltip("Pellets do full damage out to this distance...")]
        [SerializeField, Min(0f)] float falloffStart = 6f;
        [Tooltip("...then less and less, down to the minimum at this distance and beyond.")]
        [SerializeField, Min(0f)] float falloffEnd = 26f;
        [Tooltip("Share of the damage a pellet still does past the falloff end.")]
        [SerializeField, Range(0f, 1f)] float falloffMinimum = 0.3f;

        [Header("Lever action")]
        [Tooltip("Seconds from one shot to the next while the lever cycles.")]
        [SerializeField, Min(0.05f)] float cycleTime = 0.55f;
        [Tooltip("Holding the trigger fires again as soon as the lever has cycled.")]
        [SerializeField] bool holdToRefire = true;

        [Header("Aiming (hold Aim)")]
        [Tooltip("Half-angle of the pellet cone while aiming, in degrees.")]
        [SerializeField, Range(0f, 20f)] float aimSpread = 2.6f;
        [Tooltip("Where the gun sits while aiming, relative to the camera.")]
        [SerializeField] Vector3 aimPosition = new Vector3(0f, -0.065f, 0.17f);
        [Tooltip("How fast the gun comes up to aim and goes back down; 6 takes about a sixth of a second.")]
        [SerializeField, Min(0.1f)] float aimSpeed = 6f;

        [Header("Heat")]
        [Tooltip("Heat each shot adds. The gun overheats at 100, so 21 allows four quick shots and the fifth overheats.")]
        [SerializeField, Range(1f, 100f)] float heatPerShot = 21f;
        [Tooltip("Heat lost per second while the gun rests.")]
        [SerializeField, Min(0f)] float coolingRate = 22f;
        [Tooltip("Seconds after the lever finishes cycling before resting starts to cool it.")]
        [SerializeField, Min(0f)] float coolingDelay = 0.25f;
        [Tooltip("Seconds the gun is locked after overheating while the heat drains away.")]
        [SerializeField, Min(0f)] float overheatLockout = 2.6f;
        [Tooltip("Heat dumped per second while venting. You can't fire until the vent finishes.")]
        [SerializeField, Min(1f)] float ventRate = 140f;

        [Header("Recoil")]
        [Tooltip("Degrees the view kicks up per shot from the hip.")]
        [SerializeField, Range(0f, 15f)] float recoilHip = 4.6f;
        [Tooltip("Degrees the view kicks up per shot while aiming.")]
        [SerializeField, Range(0f, 15f)] float recoilAim = 2.9f;

        [Header("Heat glow")]
        [Tooltip("Renderers that glow as the gun heats up. Their material needs emission switched on.")]
        [SerializeField] Renderer[] heatVents = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color hotGlow = new Color(2.6f, 0.75f, 0.15f);
        [SerializeField, ColorUsage(false, true)] Color overheatGlow = new Color(3.5f, 0.25f, 0.08f);

        [Header("Effects")]
        [SerializeField] Color tracerColor = new Color(1f, 0.85f, 0.45f, 0.9f);
        [SerializeField] Color sparkColor = new Color(1f, 0.75f, 0.35f, 1f);
        [Tooltip("Sparks where a pellet hits something that takes damage.")]
        [SerializeField] Color hitColor = new Color(0.55f, 1f, 0.35f, 1f);
        [SerializeField, Min(0f)] float flashIntensity = 3f;

        [Header("Events (hook sounds up here)")]
        public UnityEvent onFire = new UnityEvent();
        [Tooltip("The trigger was pulled while the gun couldn't fire: overheated or venting.")]
        public UnityEvent onDryFire = new UnityEvent();
        public UnityEvent onOverheat = new UnityEvent();
        public UnityEvent onVent = new UnityEvent();
        [Tooltip("The heat has gone after overheating or venting, and the gun is ready again.")]
        public UnityEvent onCooled = new UnityEvent();
        [Tooltip("Sent with the kick in degrees, for the camera.")]
        public UnityEvent<float> onRecoil = new UnityEvent<float>();

        struct Pending
        {
            public IDamageable target;
            public string name;
            public float damage, distance;
            public int pellets;
            public Vector3 point;
        }

        readonly List<Pending> pending = new List<Pending>(16);
        MaterialPropertyBlock glowBlock;
        Vector3 restPosition;
        Quaternion restRotation, leverRest;
        float cycleTimer, kick, flash, heat, overheatTimer;
        bool venting;

        public int Pellets => pellets;
        public float MaxRange => range;
        /// <summary>Half-angle of the pellet cone right now, in degrees: it tightens as the gun comes up to aim.</summary>
        public float Spread => Mathf.Lerp(hipSpread, aimSpread, AimBlend);
        public bool IsCycling => cycleTimer > 0f;
        public bool IsOverheated => overheatTimer > 0f;
        public bool IsVenting => venting;
        public bool CanFire => !IsCycling && !IsOverheated && !venting;
        /// <summary>0 cold, 1 about to overheat.</summary>
        public float Heat01 => heat / 100f;
        /// <summary>Seconds left before an overheated gun works again.</summary>
        public float OverheatRemaining => Mathf.Max(0f, overheatTimer);
        public bool IsAiming { get; private set; }
        /// <summary>0 at the hip, 1 fully aimed.</summary>
        public float AimBlend { get; private set; }
        public ShotReport LastShot { get; private set; }

        /// <summary>What one pellet does at this distance.</summary>
        public float DamageAt(float distance) => pelletDamage * Falloff(distance);

        void Awake()
        {
            if (!controls) controls = GetComponentInParent<PlayerControls>();
            if (!aimOrigin)
            {
                var view = GetComponentInParent<Camera>();
                aimOrigin = view ? view.transform : transform;
            }
            if (!muzzle) muzzle = transform;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            if (lever) leverRest = lever.localRotation;
            if (muzzleFlash)
            {
                muzzleFlash.intensity = 0f;
                muzzleFlash.enabled = false;
            }
            glowBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cycleTimer -= dt;
            Cool(dt);
            if (!controls) return;

            IsAiming = controls.AimHeld;
            AimBlend = Mathf.MoveTowards(AimBlend, IsAiming ? 1f : 0f, aimSpeed * dt);
            if (controls.VentPressed) TryVent();

            bool trigger = holdToRefire ? controls.FireHeld : controls.FirePressed;
            if (trigger && CanFire) Fire();
            else if (controls.FirePressed && (IsOverheated || venting)) onDryFire.Invoke();
        }

        /// <summary>Starts dumping heat. Returns false if there's nothing to vent, or it's overheated or already venting.</summary>
        public bool TryVent()
        {
            if (heat <= 0f || IsOverheated || venting) return false;
            venting = true;
            onVent.Invoke();
            return true;
        }

        void Cool(float dt)
        {
            if (overheatTimer > 0f)
            {
                overheatTimer -= dt;
                heat = overheatLockout > 0f ? 100f * Mathf.Clamp01(overheatTimer / overheatLockout) : 0f;   // drains as you wait
                if (overheatTimer <= 0f)
                {
                    heat = 0f;
                    onCooled.Invoke();
                }
            }
            else if (venting)
            {
                heat = Mathf.Max(0f, heat - ventRate * dt);
                if (heat <= 0f)
                {
                    venting = false;
                    onCooled.Invoke();
                }
            }
            else if (cycleTimer < -coolingDelay) heat = Mathf.Max(0f, heat - coolingRate * dt);
        }

        void LateUpdate()
        {
            Animate(Time.deltaTime);
        }

        void Fire()
        {
            cycleTimer = cycleTime;
            kick = 1f;
            flash = flashIntensity;
            heat = Mathf.Min(100f, heat + heatPerShot);
            bool overheatedNow = heat >= 100f;   // the shot that overheats still goes off
            if (overheatedNow) overheatTimer = overheatLockout;

            Physics.SyncTransforms();   // targets moved this frame; make sure the rays see where they are now
            Transform shooter = controls ? controls.transform : transform.root;
            Vector3 origin = aimOrigin.position, forward = aimOrigin.forward, right = aimOrigin.right, up = aimOrigin.up;
            Vector3 muzzlePoint = muzzle.position;
            float spread = Mathf.Tan(Spread * Mathf.Deg2Rad);

            pending.Clear();
            int landed = 0;
            for (int i = 0; i < pellets; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spread;   // spread evenly over the cone
                Vector3 dir = (forward + right * offset.x + up * offset.y).normalized;
                Vector3 end = origin + dir * range;
                if (Raycasts.Nearest(origin, dir, range, shooter, out RaycastHit hit))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null)
                    {
                        Add(target, hit, DamageAt(hit.distance));
                        landed++;
                        StreakFx.Sparks(hit.point, hit.normal, hitColor, 3, 5f, 0.3f);
                    }
                    else StreakFx.Sparks(hit.point, hit.normal, sparkColor, 2, 6f, 0.22f);
                }
                StreakFx.Tracer(muzzlePoint, end, tracerColor, 0.02f, 0.09f);
            }

            // One hit per target with all its pellets added up, so a target sees the whole blast at once.
            var report = new ShotReport { Pellets = pellets, PelletsOnTarget = landed, Time = Time.time, Target = "" };
            float most = -1f;
            foreach (Pending p in pending)
            {
                p.target.TakeDamage(new DamageInfo
                {
                    Amount = p.damage, Kind = DamageKind.Shotgun, Point = p.point, Direction = forward,
                    Distance = p.distance, Hits = p.pellets, Source = shooter.gameObject,
                });
                report.Damage += p.damage;
                if (p.damage > most)
                {
                    most = p.damage;
                    report.Distance = p.distance;
                    report.Target = p.name;
                }
            }
            LastShot = report;
            onFire.Invoke();
            onRecoil.Invoke(Mathf.Lerp(recoilHip, recoilAim, AimBlend));
            if (overheatedNow) onOverheat.Invoke();
        }

        void Add(IDamageable target, RaycastHit hit, float damage)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (!ReferenceEquals(pending[i].target, target)) continue;
                Pending p = pending[i];
                p.damage += damage;
                p.pellets++;
                pending[i] = p;
                return;
            }
            var component = target as Component;
            pending.Add(new Pending
            {
                target = target, name = component ? component.name : "target", damage = damage,
                distance = hit.distance, pellets = 1, point = hit.point,
            });
        }

        void Glow()
        {
            if (heatVents.Length == 0) return;
            Color glow;
            if (IsOverheated) glow = overheatGlow * (0.7f + 0.3f * Mathf.Sin(Time.time * 40f));   // flickers while locked
            else
            {
                float h = Heat01;
                glow = hotGlow * (h * h);   // stays dark until it's properly warm
            }
            glowBlock.SetColor("_EmissionColor", glow);
            foreach (Renderer vent in heatVents)
                if (vent) vent.SetPropertyBlock(glowBlock);
        }

        float Falloff(float distance)
        {
            if (distance <= falloffStart || falloffEnd <= falloffStart) return 1f;
            return Mathf.Lerp(1f, falloffMinimum, (distance - falloffStart) / (falloffEnd - falloffStart));
        }

        /// <summary>The gun comes up to aim, kicks back on each shot, then the lever swings down and back. The vents glow with heat.</summary>
        void Animate(float dt)
        {
            kick = Mathf.Max(0f, kick - dt * 5f);
            float k = kick * kick * (1f - 0.5f * AimBlend);   // aimed shots kick less
            const float leverThrow = 0.35f;   // the last part of the cycle
            float leverSwing = cycleTimer > 0f && cycleTimer < leverThrow
                ? Mathf.Sin((leverThrow - cycleTimer) / leverThrow * Mathf.PI) : 0f;
            float aim = Mathf.SmoothStep(0f, 1f, AimBlend);

            Vector3 basePosition = Vector3.Lerp(restPosition, aimPosition, aim);
            transform.localPosition = basePosition + new Vector3(0f, 0.01f * k - 0.012f * leverSwing, -0.07f * k);
            transform.localRotation = restRotation * Quaternion.Euler(-10f * k + 6f * leverSwing, 0f, -8f * leverSwing * (1f - 0.6f * aim));
            if (lever) lever.localRotation = leverRest * Quaternion.Euler(-55f * leverSwing, 0f, 0f);
            Glow();

            if (muzzleFlash)
            {
                flash = Mathf.Max(0f, flash - dt * 30f);
                muzzleFlash.intensity = flash;
                muzzleFlash.enabled = flash > 0f;
            }
        }
    }
}
