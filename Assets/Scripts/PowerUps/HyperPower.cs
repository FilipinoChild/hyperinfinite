using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// POWER-UP: HYPER. Picking up a HYPER core supercharges John's suit for 15 seconds:
    ///  - he runs and dashes 30% faster;
    ///  - the heavy bash reaches 40% further and 15 degrees wider either side, and blast jumps spread wider;
    ///  - chain lunges: the heavy bash's lunge goes further, and a heavy bash that kills refunds the dash, so he can
    ///    zip from pack to pack;
    ///  - thruster ram: dashing (or lunging) through enemies knocks them aside, hurts them and brands them;
    ///  - kills keep it going: every kill from a heavy bash, its brand bursts included, adds a second, up to
    ///    25 seconds in all.
    /// It ends at once if he goes down.
    ///
    /// While it's on, John leaves a trail of fading cyan pixel squares behind him whenever he runs flat out or
    /// dashes, and it starts with a cyan burst and ring at his feet. The HUD's HYPER panel shows the time left.
    ///
    /// Put it on the Player, next to PlayerMotor. ShotgunMelee and the HUD read it; HyperCore calls Activate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public class HyperPower : MonoBehaviour
    {
        [Header("Time")]
        [Tooltip("Seconds HYPER lasts from a pickup.")]
        [SerializeField, Min(1f)] float duration = 15f;
        [Tooltip("Seconds each heavy-bash kill adds.")]
        [SerializeField, Min(0f)] float perKill = 1f;
        [Tooltip("Longest it can last in all, counting from the pickup, however many kills there are.")]
        [SerializeField, Min(1f)] float maxDuration = 25f;

        [Header("Suit")]
        [Tooltip("Running and dashing speed is multiplied by this.")]
        [SerializeField, Range(1f, 2f)] float speedBoost = 1.3f;

        [Header("Heavy bash")]
        [Tooltip("The heavy bash's reach, and a blast jump's spread, are multiplied by this.")]
        [SerializeField, Range(1f, 2f)] float reachBoost = 1.4f;
        [Tooltip("Degrees added to either side of the heavy bash's cone.")]
        [SerializeField, Range(0f, 45f)] float extraWidth = 15f;
        [Tooltip("The lunge lasts this many times longer: 1.75 takes it from about 4 m to about 7 m before the speed boost.")]
        [SerializeField, Range(1f, 3f)] float lungeBoost = 1.75f;

        [Header("Thruster ram (dashing or lunging into enemies)")]
        [Tooltip("How close an enemy must come to be rammed, in meters from the player's middle.")]
        [SerializeField, Min(0.5f)] float ramRadius = 1.6f;
        [SerializeField, Min(0f)] float ramDamage = 15f;
        [Tooltip("How hard rammed enemies are thrown aside, in meters per second.")]
        [SerializeField, Min(0f)] float ramPush = 9f;

        [Header("Events (hook sounds up here)")]
        public UnityEvent onHyperStart = new UnityEvent();
        public UnityEvent onHyperEnd = new UnityEvent();
        [Tooltip("Kills added time. Sent with the seconds added.")]
        public UnityEvent<float> onExtended = new UnityEvent<float>();

        static readonly Collider[] nearby = new Collider[32];
        const int MaxRammed = 24;
        readonly IDamageable[] rammed = new IDamageable[MaxRammed];
        int rammedCount;

        PlayerMotor motor;
        Health health;
        ShotgunMelee melee;
        float startedAt = -100f, endsAt = -100f, trailDue;
        bool active, wasDashing;

        /// <summary>True while HYPER is on.</summary>
        public bool IsActive => active;
        /// <summary>Seconds left.</summary>
        public float Remaining => active ? Mathf.Max(0f, endsAt - Time.time) : 0f;
        /// <summary>Share of the longest it could last (Max Duration) that's left, for the HUD's bar.</summary>
        public float Remaining01 => Remaining / maxDuration;
        /// <summary>When it last started, and when kills last added time.</summary>
        public float StartedAt => startedAt;
        public float LastExtendTime { get; private set; } = -100f;
        /// <summary>Seconds the kills last added.</summary>
        public float LastExtendAmount { get; private set; }
        public float ReachBoost => active ? reachBoost : 1f;
        public float ExtraWidth => active ? extraWidth : 0f;
        public float LungeBoost => active ? lungeBoost : 1f;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<Health>();
            melee = GetComponentInChildren<ShotgunMelee>();
            if (health) health.onDied.AddListener(End);
            if (melee) melee.onHeavyKills.AddListener(OnHeavyKills);
        }

        void OnDestroy()
        {
            if (health) health.onDied.RemoveListener(End);
            if (melee) melee.onHeavyKills.RemoveListener(OnHeavyKills);
        }

        /// <summary>Turns HYPER on for its full time, or tops it back up to that if it's already on.</summary>
        public void Activate()
        {
            if (health && health.IsDead) return;
            float now = Time.time;
            if (!active) startedAt = now;
            else startedAt = Mathf.Max(startedAt, now + duration - maxDuration);   // a fresh pickup can't push past the cap either
            endsAt = Mathf.Max(endsAt, now + duration);
            if (!active)
            {
                active = true;
                motor.SpeedScale = speedBoost;
                Vector3 feet = transform.position;
                PixelFx.Ring(feet, 3f, Palette.Cyan, 30, 0.35f, 0.22f);
                PixelFx.Ring(feet, 1.8f, Palette.White, 20, 0.25f, 0.18f);
                PixelFx.Burst(feet + Vector3.up, Palette.Cyan, Palette.PaleCyan, 24, 5f, 0.45f, 0.14f);
                onHyperStart.Invoke();
            }
        }

        /// <summary>Turns HYPER off at once.</summary>
        public void End()
        {
            if (!active) return;
            active = false;
            endsAt = Time.time;
            motor.SpeedScale = 1f;
            onHyperEnd.Invoke();
        }

        void OnHeavyKills(int kills)
        {
            if (!active || kills <= 0) return;
            float before = endsAt;
            endsAt = Mathf.Min(endsAt + kills * perKill, startedAt + maxDuration);
            if (endsAt > before)
            {
                LastExtendTime = Time.time;
                LastExtendAmount = endsAt - before;
                onExtended.Invoke(LastExtendAmount);
            }
            motor.RefundDash();   // chain lunges: a kill puts the dash straight back
        }

        void Update()
        {
            if (!active) return;
            if (Time.time >= endsAt)
            {
                End();
                return;
            }
            Ram();
            Trail();
        }

        /// <summary>
        /// Fading cyan squares left hanging in the air behind him while he runs flat out or dashes: an afterimage,
        /// thicker while dashing. A few PixelFx squares at a time.
        /// </summary>
        void Trail()
        {
            bool dashing = motor.IsDashing;
            if (!dashing && motor.Speed < motor.RunSpeed * 0.9f) return;
            float now = Time.time;
            if (now < trailDue) return;
            trailDue = now + (dashing ? 0.02f : 0.05f);
            Vector3 at = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 1.7f), Random.Range(-0.3f, 0.3f));
            Color color = dashing && Random.value < 0.4f ? Palette.White : Random.value < 0.5f ? Palette.Cyan : Palette.PaleCyan;
            PixelFx.Mote(at, Vector3.up * 0.15f, color, Random.Range(0.07f, 0.11f), dashing ? 0.45f : 0.35f);
        }

        /// <summary>
        /// While dashing or lunging, enemies that come within reach are rammed, once each per dash: knocked aside,
        /// hurt and branded. One small overlap test a frame, and only while dashing.
        /// </summary>
        void Ram()
        {
            bool dashing = motor.IsDashing;
            if (dashing && !wasDashing) rammedCount = 0;   // a new dash rams everything afresh
            wasDashing = dashing;
            if (!dashing || !melee) return;

            Vector3 middle = transform.position + Vector3.up * 0.9f;
            Vector3 travel = motor.Velocity;
            travel.y = 0f;
            int count = Physics.OverlapSphereNonAlloc(middle, ramRadius, nearby, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider c = nearby[i];
                if (c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IDamageable>();
                if (target == null || WasRammed(target)) continue;
                if (rammedCount < MaxRammed) rammed[rammedCount++] = target;

                // Thrown out of the way: sideways from the line of the dash, and a little up.
                Vector3 away = c.bounds.center - middle;
                away.y = 0f;
                Vector3 side = travel.sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(away, travel.normalized) : away;
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(Vector3.up, travel.sqrMagnitude > 0.01f ? travel : transform.forward);
                melee.Ram(c, target, side.normalized * ramPush + Vector3.up * (ramPush * 0.25f), ramDamage);
            }
        }

        bool WasRammed(IDamageable target)
        {
            for (int i = 0; i < rammedCount; i++)
                if (ReferenceEquals(rammed[i], target)) return true;
            return false;
        }
    }
}
