using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HyperInfinite
{
    /// <summary>A summary of the last blast, for readouts and tuning.</summary>
    public struct ShotReport
    {
        public int Bolts, BoltsOnTarget;
        public float Damage, Distance;
        public string Target;
        public float Time;
    }

    /// <summary>
    /// COMBAT MECHANIC 1. The Energy Blast shotgun: a lever-action
    /// shotgun that fires heavy energy blasts instead of shells. Each blast sends a cone of energy bolts
    /// from the center of the screen. Every bolt does full damage up close and less further out, so the
    /// gun hits hardest up close. Between blasts the lever has to cycle.
    ///
    /// Heat is the only resource; there's no ammo. Every blast adds heat, and heat never fades by itself:
    /// getting rid of it is the player's choice. Reach 100 and the gun redlines. It won't fire again until
    /// the heat goes, but nothing else is locked. There are two ways out. Venting (R) is safe but wastes
    /// the heat, and you can't fire while it drains. ShotgunMelee spends it instead: a searing bash burns a
    /// little, and a heavy bash dumps the lot in one explosion. Aiming (right mouse) tightens the cone so
    /// more bolts land at range.
    ///
    /// Put this under the camera, next to ShotgunSprite, which draws the gun. Bolts fly from the camera's
    /// center, so they land around the crosshair; the bolts you see start at the emitters so they look
    /// right. Anything with an IDamageable component takes the hits.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnergyBlastShotgun : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] PlayerControls controls;
        [Tooltip("Bolts fly from the center of this camera. Defaults to the camera above this object.")]
        [SerializeField] Transform aimOrigin;
        [Tooltip("The tips of the emitters. The bolts you see start from them in turn. Without any, they start here.")]
        [SerializeField] Transform[] emitters = new Transform[0];
        [Tooltip("Lights up for each blast. Optional.")]
        [SerializeField] Light muzzleFlash;

        [Header("Blast")]
        [SerializeField, Range(1, 30)] int bolts = 9;
        [Tooltip("Damage per bolt at close range.")]
        [SerializeField, Min(0f)] float boltDamage = 9f;
        [Tooltip("Half-angle of the bolt cone, in degrees, firing from the hip.")]
        [SerializeField, Range(0f, 20f)] float hipSpread = 5.7f;
        [SerializeField, Min(1f)] float range = 70f;
        [Tooltip("Bolts do full damage out to this distance...")]
        [SerializeField, Min(0f)] float falloffStart = 6f;
        [Tooltip("...then less and less, down to the minimum at this distance and beyond.")]
        [SerializeField, Min(0f)] float falloffEnd = 26f;
        [Tooltip("Share of the damage a bolt still does past the falloff end.")]
        [SerializeField, Range(0f, 1f)] float falloffMinimum = 0.3f;

        [Header("Lever action")]
        [Tooltip("Seconds from one blast to the next while the lever cycles.")]
        [SerializeField, Min(0.05f)] float cycleTime = 0.55f;
        [Tooltip("Holding the trigger fires again as soon as the lever has cycled.")]
        [SerializeField] bool holdToRefire = true;

        [Header("Aiming (hold Aim)")]
        [Tooltip("Half-angle of the bolt cone while aiming, in degrees.")]
        [SerializeField, Range(0f, 20f)] float aimSpread = 2.6f;
        [Tooltip("How fast the gun comes up to aim and goes back down; 6 takes about a sixth of a second.")]
        [SerializeField, Min(0.1f)] float aimSpeed = 6f;

        [Header("Heat")]
        [Tooltip("Heat each blast adds. Heat never fades by itself, and at 100 the gun redlines, so 14 allows seven blasts and the eighth redlines it.")]
        [SerializeField, Range(1f, 100f)] float heatPerShot = 14f;
        [Tooltip("Heat dumped per second while venting. You can't fire until the vent finishes.")]
        [SerializeField, Min(1f)] float ventRate = 140f;

        [Header("Recoil")]
        [Tooltip("Degrees the view kicks up per blast from the hip.")]
        [SerializeField, Range(0f, 15f)] float recoilHip = 4.6f;
        [Tooltip("Degrees the view kicks up per blast while aiming.")]
        [SerializeField, Range(0f, 15f)] float recoilAim = 2.9f;

        [Header("Energy glow (GlowColor)")]
        [Tooltip("Ready to fire and cool.")]
        [SerializeField, ColorUsage(false, true)] Color chargedGlow = new Color(0.3f, 1.5f, 2.4f);
        [Tooltip("Close to redlining.")]
        [SerializeField, ColorUsage(false, true)] Color hotGlow = new Color(2.6f, 0.75f, 0.15f);
        [Tooltip("Redlined.")]
        [SerializeField, ColorUsage(false, true)] Color overheatGlow = new Color(3.5f, 0.25f, 0.08f);

        [Header("Effects")]
        [Tooltip("How brightly the muzzle light flashes on each blast.")]
        [SerializeField, Min(0f)] float flashIntensity = 3f;

        [Header("Events (hook sounds up here)")]
        [Tooltip("A blast went off: deep, cracking boom.")]
        public UnityEvent onFire = new UnityEvent();
        [Tooltip("The trigger was pulled while the gun couldn't fire: redlined or venting.")]
        public UnityEvent onDryFire = new UnityEvent();
        [Tooltip("A blast took the heat to 100: the gun won't fire until it's vented or bashed out.")]
        [FormerlySerializedAs("onOverheat")] public UnityEvent onRedline = new UnityEvent();
        public UnityEvent onVent = new UnityEvent();
        [Tooltip("The heat has all gone, by venting or a heavy bash, and the gun is ready again.")]
        public UnityEvent onCooled = new UnityEvent();
        [Tooltip("Sent with the kick in degrees, for the camera.")]
        public UnityEvent<float> onRecoil = new UnityEvent<float>();

        struct Pending
        {
            public IDamageable target;
            public string name;
            public float damage, distance;
            public int bolts;
            public Vector3 point;
        }

        readonly List<Pending> pending = new List<Pending>(16);
        float cycleTimer, flash, heat;
        int nextEmitter;
        bool venting;

        public int Bolts => bolts;
        public float MaxRange => range;
        /// <summary>Half-angle of the bolt cone right now, in degrees: it tightens as the gun comes up to aim.</summary>
        public float Spread => Mathf.Lerp(hipSpread, aimSpread, AimBlend);
        public bool IsCycling => cycleTimer > 0f;
        /// <summary>At 100 heat: it won't fire until the heat is vented or bashed out.</summary>
        public bool IsRedlined => heat >= 100f;
        public bool IsVenting => venting;
        public bool CanFire => !IsCycling && !IsRedlined && !venting;
        /// <summary>0 cold, 1 redlined.</summary>
        public float Heat01 => heat / 100f;
        /// <summary>Heat right now, 0 to 100.</summary>
        public float Heat => heat;
        /// <summary>True when the next blast would redline the gun, so the heat gauge can flash a warning.</summary>
        public bool AboutToRedline => !IsRedlined && !venting && heat + heatPerShot >= 100f;
        public bool IsAiming { get; private set; }
        /// <summary>0 at the hip, 1 fully aimed.</summary>
        public float AimBlend { get; private set; }
        public ShotReport LastShot { get; private set; }
        /// <summary>1 the instant a blast goes off, easing back to 0, for the gun's kick on screen.</summary>
        public float Kick { get; private set; }
        /// <summary>0 to 1 and back while the lever swings through the end of its cycle.</summary>
        public float LeverSwing { get; private set; }
        /// <summary>The emitters' glow: cyan when charged and cool, amber as heat builds, a red flicker when redlined, dim while recharging.</summary>
        public Color GlowColor { get; private set; }

        /// <summary>What one bolt does at this distance.</summary>
        public float DamageAt(float distance) => boltDamage * Falloff(distance);

        void Awake()
        {
            if (!controls) controls = GetComponentInParent<PlayerControls>();
            if (!aimOrigin)
            {
                var view = GetComponentInParent<Camera>();
                aimOrigin = view ? view.transform : transform;
            }
            if (muzzleFlash)
            {
                muzzleFlash.intensity = 0f;
                muzzleFlash.enabled = false;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cycleTimer -= dt;
            Vent(dt);
            if (!controls) return;

            IsAiming = controls.AimHeld;
            AimBlend = Mathf.MoveTowards(AimBlend, IsAiming ? 1f : 0f, aimSpeed * dt);
            if (controls.VentPressed) TryVent();

            bool trigger = holdToRefire ? controls.FireHeld : controls.FirePressed;
            if (trigger && CanFire) Fire();
            else if (controls.FirePressed && (IsRedlined || venting)) onDryFire.Invoke();
        }

        /// <summary>Starts dumping heat, redlined or not. Returns false if there's nothing to vent or it's already venting.</summary>
        public bool TryVent()
        {
            if (heat <= 0f || venting) return false;
            venting = true;
            onVent.Invoke();
            return true;
        }

        /// <summary>
        /// Takes up to <paramref name="amount"/> heat out of the gun and returns how much it took, for a searing
        /// bash. Spending heat brings a redlined gun back.
        /// </summary>
        public float SpendHeat(float amount)
        {
            float spent = Mathf.Clamp(amount, 0f, heat);
            if (spent <= 0f) return 0f;
            heat -= spent;
            if (heat <= 0f) Emptied();
            return spent;
        }

        /// <summary>Takes all the heat out at once and returns how much there was, for a heavy bash. Stops a vent under way.</summary>
        public float DumpHeat()
        {
            float dumped = heat;
            if (dumped > 0f) Emptied();
            return dumped;
        }

        /// <summary>Cold and ready to fire at once, whatever it was doing: for respawns.</summary>
        public void CoolCompletely()
        {
            heat = 0f;
            cycleTimer = 0f;
            venting = false;
        }

        /// <summary>Heat only goes down while venting: nothing cools the gun by itself.</summary>
        void Vent(float dt)
        {
            if (!venting) return;
            heat -= ventRate * dt;
            if (heat <= 0f) Emptied();
        }

        void Emptied()
        {
            heat = 0f;
            venting = false;
            onCooled.Invoke();
        }

        void LateUpdate()
        {
            Animate(Time.deltaTime);
        }

        void Fire()
        {
            cycleTimer = cycleTime;
            Kick = 1f;
            flash = flashIntensity;
            heat = Mathf.Min(100f, heat + heatPerShot);
            bool redlinedNow = heat >= 100f;   // the blast that redlines still goes off

            Physics.SyncTransforms();   // targets moved this frame; make sure the rays see where they are now
            Transform shooter = controls ? controls.transform : transform.root;
            Vector3 origin = aimOrigin.position, forward = aimOrigin.forward, right = aimOrigin.right, up = aimOrigin.up;
            float spread = Mathf.Tan(Spread * Mathf.Deg2Rad);

            pending.Clear();
            int landed = 0;
            for (int i = 0; i < bolts; i++)
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
                        PixelFx.Sparks(hit.point, hit.normal, Palette.Yellow, 3, 5f, 0.3f);   // it hurt something
                    }
                    else PixelFx.Sparks(hit.point, hit.normal, Palette.PaleCyan, 2, 6f, 0.22f);
                }
                PixelFx.Bolt(NextEmitter().position, end, Palette.Cyan, 0.06f, 0.11f);
            }

            // One hit per target with all its bolts added up, so a target sees the whole blast at once.
            var report = new ShotReport { Bolts = bolts, BoltsOnTarget = landed, Time = Time.time, Target = "" };
            float most = -1f;
            foreach (Pending p in pending)
            {
                p.target.TakeDamage(new DamageInfo
                {
                    Amount = p.damage, Kind = DamageKind.Shotgun, Point = p.point, Direction = forward,
                    Distance = p.distance, Hits = p.bolts, Source = shooter.gameObject,
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
            if (redlinedNow) onRedline.Invoke();
        }

        /// <summary>The emitter tip the next bolt starts from, taking turns.</summary>
        Transform NextEmitter()
        {
            if (emitters.Length == 0) return transform;
            if (nextEmitter >= emitters.Length) nextEmitter = 0;   // the array was shortened in the Inspector
            Transform tip = emitters[nextEmitter];
            nextEmitter = (nextEmitter + 1) % emitters.Length;
            return tip ? tip : transform;
        }

        void Add(IDamageable target, RaycastHit hit, float damage)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (!ReferenceEquals(pending[i].target, target)) continue;
                Pending p = pending[i];
                p.damage += damage;
                p.bolts++;
                pending[i] = p;
                return;
            }
            var component = target as Component;
            pending.Add(new Pending
            {
                target = target, name = component ? component.name : "target", damage = damage,
                distance = hit.distance, bolts = 1, point = hit.point,
            });
        }

        /// <summary>
        /// The emitters glow cyan when the gun is charged and cool. A blast drains them and they charge back
        /// up while the lever cycles. As heat builds they run amber, and they flicker red while redlined.
        /// </summary>
        void Glow()
        {
            Color glow;
            if (IsRedlined) glow = overheatGlow * (0.7f + 0.3f * Mathf.Sin(Time.time * 40f));   // flickers until the heat goes
            else
            {
                float h = Heat01;
                glow = Color.Lerp(chargedGlow, hotGlow, h * h);   // stays cyan until it's properly warm
                float charge = cycleTimer > 0f && cycleTime > 0f ? 1f - cycleTimer / cycleTime : 1f;
                glow *= Mathf.Lerp(0.25f, 1f, charge);
            }
            GlowColor = glow;
        }

        float Falloff(float distance)
        {
            if (distance <= falloffStart || falloffEnd <= falloffStart) return 1f;
            return Mathf.Lerp(1f, falloffMinimum, (distance - falloffStart) / (falloffEnd - falloffStart));
        }

        /// <summary>The gun comes up to aim, kicks back on each blast, then the lever swings down and back. The emitters glow with charge and heat.</summary>
        void Animate(float dt)
        {
            Kick = Mathf.Max(0f, Kick - dt * 5f);
            const float leverThrow = 0.35f;   // the last part of the cycle
            LeverSwing = cycleTimer > 0f && cycleTimer < leverThrow
                ? Mathf.Sin((leverThrow - cycleTimer) / leverThrow * Mathf.PI) : 0f;
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