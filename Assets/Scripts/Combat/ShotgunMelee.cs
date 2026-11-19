using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// COMBAT MECHANIC 2. Melee with the shotgun itself: a quick bash and a heavy bash that both feed on its
    /// heat. Shooting builds heat, and heat never fades by itself, so the melee is how the player cashes it in.
    ///
    /// Tap Melee (Q) to bash the nearest thing in front. With 10 or more heat in the gun it sears, spending
    /// that heat for extra damage and a stagger, and it brands what it hits. Branded enemies smoulder with
    /// embers for a few seconds. Bash a Scuttler while it crouches to bite and the bite is countered: it takes
    /// extra damage and is thrown clear.
    ///
    /// Hold Melee to charge a heavy bash (you move slower while charging), and let go to throw it. It dumps
    /// every point of heat in the gun into one explosion out of the emitters: the more heat, the harder and
    /// wider it hits and the harder it throws enemies back. A cold one lunges you forward; a full one kicks you
    /// back. Every brand within reach bursts at the same moment, splashing whatever is near it. If the Hive
    /// Mother is winding up a slam and the bash carries enough heat, the slam is cancelled. Aim at the floor to
    /// blast-jump: the explosion goes off underfoot and throws you up, higher the hotter it was.
    ///
    /// Put it next to EnergyBlastShotgun, under the camera; ShotgunSprite draws the swings. Hits are found with
    /// one overlap test into a reused buffer, so nothing is allocated from frame to frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnergyBlastShotgun))]
    public class ShotgunMelee : MonoBehaviour
    {
        /// <summary>What the last bash was.</summary>
        public enum BashKind { Plain, Seared, Counter }

        [Header("Bash (tap Melee)")]
        [SerializeField, Min(0f)] float bashDamage = 15f;
        [Tooltip("How far the bash reaches from the eyes, in meters.")]
        [SerializeField, Min(0.5f)] float bashRange = 2.4f;
        [Tooltip("How far off the crosshair, in degrees, a target can be and still be bashed.")]
        [SerializeField, Range(5f, 90f)] float bashAngle = 45f;
        [Tooltip("Seconds before the shotgun can bash again.")]
        [SerializeField, Min(0.05f)] float bashCooldown = 0.35f;

        [Header("Searing bash")]
        [Tooltip("Heat a searing bash spends. Below this much heat in the gun, the bash is plain.")]
        [SerializeField, Min(0f)] float searHeat = 10f;
        [Tooltip("Damage a searing bash adds.")]
        [SerializeField, Min(0f)] float searDamage = 20f;
        [Tooltip("How hard a searing bash staggers what it hits, in meters per second.")]
        [SerializeField, Min(0f)] float searPush = 4f;

        [Header("Counter (bash a Scuttler as it winds up a bite)")]
        [SerializeField, Min(0f)] float counterDamage = 25f;
        [SerializeField, Min(0f)] float counterPush = 10f;

        [Header("Brand")]
        [Tooltip("Seconds a brand smoulders before it fades.")]
        [SerializeField, Min(0.5f)] float brandTime = 8f;
        [Tooltip("A heavy bash sets off every brand within this many meters of you.")]
        [SerializeField, Min(0f)] float brandReach = 14f;
        [Tooltip("Each brand's burst does this much...")]
        [SerializeField, Min(0f)] float brandDamage = 15f;
        [Tooltip("...plus this much for each point of heat the bash dumped.")]
        [SerializeField, Min(0f)] float brandHeatDamage = 0.5f;
        [Tooltip("How far each brand's burst splashes, in meters.")]
        [SerializeField, Min(0f)] float brandRadius = 2.5f;

        [Header("Heavy bash (hold Melee)")]
        [SerializeField, Min(0f)] float heavyDamage = 30f;
        [Tooltip("Damage added for each point of heat dumped: 2.2 makes a full gauge hit for 250.")]
        [SerializeField, Min(0f)] float heavyHeatDamage = 2.2f;
        [Tooltip("How far the blast reaches with no heat, and with a full gauge, in meters.")]
        [SerializeField] Vector2 heavyRange = new Vector2(3f, 4.5f);
        [Tooltip("Half-width of the blast's cone, in degrees.")]
        [SerializeField, Range(5f, 90f)] float heavyAngle = 38f;
        [Tooltip("How hard it throws enemies back with no heat, and with a full gauge, in meters per second.")]
        [SerializeField] Vector2 heavyPush = new Vector2(5f, 14f);
        [Tooltip("Share of your speed you keep while charging.")]
        [SerializeField, Range(0.1f, 1f)] float chargeSlow = 0.6f;
        [Tooltip("Forward push a cold bash gives you, in meters per second...")]
        [SerializeField, Min(0f)] float lunge = 6f;
        [Tooltip("...and the backward kick a full one gives you instead.")]
        [SerializeField, Min(0f)] float recoil = 4f;
        [Tooltip("Seconds you can't be hurt as the bash goes off, so a swarm can't punish every swing.")]
        [SerializeField, Min(0f)] float protection = 0.25f;
        [Tooltip("Seconds before the shotgun can be used again after a heavy bash.")]
        [SerializeField, Min(0.05f)] float heavyCooldown = 0.6f;

        [Header("Blast jump (heavy bash at the floor)")]
        [Tooltip("How far below level, in degrees, you must aim for the bash to hit the floor and launch you.")]
        [SerializeField, Range(10f, 89f)] float blastJumpPitch = 50f;
        [Tooltip("How high it throws you with no heat, and with a full gauge, in meters.")]
        [SerializeField] Vector2 blastJumpHeight = new Vector2(1.6f, 4.2f);
        [Tooltip("How far the explosion underfoot reaches with no heat, and with a full gauge, in meters.")]
        [SerializeField] Vector2 blastJumpRadius = new Vector2(2.5f, 4f);

        [Header("Hive Mother")]
        [Tooltip("Heat a bash must dump to cancel her slam while she winds up.")]
        [SerializeField, Range(0f, 100f)] float staggerHeat = 50f;
        [Tooltip("Seconds she reels when her slam is cancelled.")]
        [SerializeField, Min(0f)] float staggerTime = 1.6f;

        [Header("Events (hook sounds up here)")]
        public UnityEvent onBash = new UnityEvent();
        [Tooltip("A bash that spent heat.")]
        public UnityEvent onSear = new UnityEvent();
        [Tooltip("A bash that caught a Scuttler mid wind-up.")]
        public UnityEvent onCounter = new UnityEvent();
        [Tooltip("A heavy bash went off. Sent with its power: 0 cold, 1 a full gauge.")]
        public UnityEvent<float> onHeavyBash = new UnityEvent<float>();
        public UnityEvent onBlastJump = new UnityEvent();
        [Tooltip("A brand burst.")]
        public UnityEvent onBrandBurst = new UnityEvent();
        [Tooltip("Sent with the kick in degrees, for the camera.")]
        public UnityEvent<float> onRecoil = new UnityEvent<float>();

        struct Brand
        {
            public Collider collider;
            public IDamageable target;
            public float until, nextEmber;
        }

        const int MaxBrands = 12;
        static readonly Collider[] overlap = new Collider[64];

        readonly Brand[] brands = new Brand[MaxBrands];
        readonly List<IDamageable> struck = new List<IDamageable>(16);
        readonly List<Collider> struckBy = new List<Collider>(16);
        EnergyBlastShotgun shotgun;
        PlayerControls controls;
        PlayerMotor motor;
        Health health;
        Transform view, player;
        float readyAt;

        /// <summary>True while Melee is held long enough that letting go throws a heavy bash.</summary>
        public bool IsCharging => controls && controls.HeavyMeleeCharging && !IsDown;
        /// <summary>Seconds Melee has been held, while it's down.</summary>
        public float HoldTime => controls ? controls.MeleeHoldTime : 0f;
        /// <summary>When the last bash was thrown, and what kind it was.</summary>
        public float LastBashTime { get; private set; } = -100f;
        public BashKind LastBash { get; private set; }
        /// <summary>When the last heavy bash went off, and its power: 0 cold, 1 a full gauge.</summary>
        public float LastHeavyTime { get; private set; } = -100f;
        public float LastHeavyPower { get; private set; }
        /// <summary>How many enemies are branded right now.</summary>
        public int BrandCount { get; private set; }

        bool IsDown => health && health.IsDead;

        void Awake()
        {
            shotgun = GetComponent<EnergyBlastShotgun>();
            controls = GetComponentInParent<PlayerControls>();
            motor = GetComponentInParent<PlayerMotor>();
            health = GetComponentInParent<Health>();
            var eyes = GetComponentInParent<Camera>();
            view = eyes ? eyes.transform : transform;
            player = controls ? controls.transform : transform.root;
        }

        void Update()
        {
            TendBrands();
            if (!controls || IsDown) return;

            if (controls.HeavyMeleeCharging && motor) motor.Slow(chargeSlow);
            if (Time.time < readyAt) return;
            if (controls.QuickMeleePressed) Bash();
            else if (controls.HeavyMeleeReleased) HeavyBash();
        }

        // ---- The bash ---------------------------------------------------------------------------

        void Bash()
        {
            readyAt = Time.time + bashCooldown;
            LastBashTime = Time.time;
            Vector3 origin = view.position, forward = view.forward;

            // The nearest thing in front that can be hurt and isn't behind a wall.
            Gather(origin + forward * (bashRange * 0.5f), bashRange * 0.5f + 0.5f, origin);
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < struck.Count; i++)
            {
                Vector3 point = struckBy[i].bounds.ClosestPoint(origin);
                float distance = Vector3.Distance(origin, point);
                if (distance > bashRange || distance >= bestDistance) continue;
                if (distance > 0.4f && Vector3.Angle(forward, point - origin) > bashAngle) continue;
                if (!InSight(origin, point, struck[i])) continue;
                best = i;
                bestDistance = distance;
            }

            bool seared = shotgun.Heat >= searHeat;
            if (best < 0)
            {
                LastBash = BashKind.Plain;   // a swing at nothing spends no heat
                onBash.Invoke();
                onRecoil.Invoke(0.6f);
                return;
            }

            IDamageable target = struck[best];
            Collider hitCollider = struckBy[best];
            Vector3 hitPoint = hitCollider.bounds.ClosestPoint(origin);
            var scuttler = hitCollider.GetComponentInParent<ScuttlerAI>();
            bool counter = scuttler && scuttler.IsWindingUp;
            float damage = bashDamage;
            float push = 0f;
            if (seared)
            {
                shotgun.SpendHeat(searHeat);
                damage += searDamage;
                push = searPush;
                ApplyBrand(hitCollider, target);
            }
            if (counter)
            {
                damage += counterDamage;
                push = Mathf.Max(push, counterPush);
            }

            Hit(target, damage, hitPoint, forward, bestDistance);
            if (push > 0f)
            {
                var pushable = hitCollider.GetComponentInParent<IPushable>();
                if (pushable != null) pushable.Push(Flat(forward) * push);
            }

            LastBash = counter ? BashKind.Counter : seared ? BashKind.Seared : BashKind.Plain;
            if (counter)
            {
                PixelFx.Flash(hitPoint, Palette.Yellow, 0.7f, 0.08f);
                PixelFx.Burst(hitPoint, Palette.Yellow, Palette.White, 14, 6f, 0.35f, 0.12f);
                onCounter.Invoke();
            }
            if (seared)
            {
                PixelFx.Flash(hitPoint, Palette.Amber, 0.45f, 0.06f);
                PixelFx.Cone(hitPoint, forward, 35f, Palette.Amber, Palette.Red, 10, 6f, 0.3f, 0.1f);
                onSear.Invoke();
            }
            else PixelFx.Sparks(hitPoint, -forward, Palette.White, 5, 4f, 0.2f);
            onBash.Invoke();
            onRecoil.Invoke(counter ? 2.2f : seared ? 1.6f : 1f);
        }

        // ---- The heavy bash -------------------------------------------------------------------

        void HeavyBash()
        {
            readyAt = Time.time + heavyCooldown;
            float heat = shotgun.DumpHeat();
            float power = Mathf.Clamp01(heat / 100f);
            LastHeavyTime = Time.time;
            LastHeavyPower = power;
            if (health) health.Protect(protection);

            Vector3 origin = view.position, forward = view.forward;
            float damage = heavyDamage + heavyHeatDamage * heat;
            float push = Mathf.Lerp(heavyPush.x, heavyPush.y, power);

            // Aimed at the floor, close enough to touch it: the blast goes off underfoot and throws you up.
            float reach = Mathf.Lerp(heavyRange.x, heavyRange.y, power);
            RaycastHit floor = default;
            bool atFloor = forward.y < -Mathf.Sin(blastJumpPitch * Mathf.Deg2Rad)
                           && Raycasts.Nearest(origin, forward, reach + 0.5f, player, out floor, false)
                           && floor.normal.y > 0.6f;
            if (atFloor) BlastJump(floor.point, power, damage, push);
            else Blast(origin, forward, reach, power, damage, push);

            int burst = Detonate(heat);
            onHeavyBash.Invoke(power);
            onRecoil.Invoke(Mathf.Lerp(2.5f, 7f, power) + (burst > 0 ? 1f : 0f));
        }

        /// <summary>The bash thrown forward: everything in the cone in front is hit, the hotter the harder.</summary>
        void Blast(Vector3 origin, Vector3 forward, float reach, float power, float damage, float push)
        {
            Gather(origin + forward * (reach * 0.5f), reach * 0.5f + 1f, origin);
            for (int i = 0; i < struck.Count; i++)
            {
                Collider c = struckBy[i];
                Vector3 point = c.bounds.ClosestPoint(origin);
                float distance = Vector3.Distance(origin, point);
                if (distance > reach) continue;
                if (distance > 0.5f && Vector3.Angle(forward, point - origin) > heavyAngle) continue;
                if (!InSight(origin, point, struck[i])) continue;
                float closeness = 1f - 0.4f * Mathf.Clamp01(distance / reach);   // full damage up close, 60% at the edge
                Hit(struck[i], damage * closeness, point, forward, distance);
                Shove(c, Flat(point - origin, forward) * (push * closeness) + Vector3.up * (push * 0.2f));
                TryStagger(c, power);
            }

            // A cold bash carries you into it; a full one kicks you back.
            if (motor) motor.Push(Flat(forward) * Mathf.Lerp(lunge, -recoil, power));

            Vector3 muzzle = origin + forward * 0.9f - view.up * 0.15f;
            PixelFx.Flash(muzzle, Palette.White, 0.4f + 0.5f * power, 0.07f);
            PixelFx.Cone(muzzle, forward, heavyAngle * 0.8f, power > 0.5f ? Palette.Red : Palette.Amber, Palette.Yellow,
                         Mathf.RoundToInt(12f + 36f * power), reach / 0.28f, 0.28f, 0.12f + 0.08f * power);
        }

        /// <summary>The bash into the floor: a shockwave all round where it lands, and you're thrown upward.</summary>
        void BlastJump(Vector3 ground, float power, float damage, float push)
        {
            float radius = Mathf.Lerp(blastJumpRadius.x, blastJumpRadius.y, power);
            Gather(ground, radius, ground);
            for (int i = 0; i < struck.Count; i++)
            {
                Collider c = struckBy[i];
                Vector3 point = c.bounds.ClosestPoint(ground);
                float distance = Vector3.Distance(ground, point);
                if (distance > radius || !InSight(ground + Vector3.up * 0.3f, point, struck[i])) continue;
                float closeness = 1f - 0.4f * Mathf.Clamp01(distance / radius);
                Hit(struck[i], damage * closeness, point, point - ground, distance);
                Shove(c, Flat(point - ground, view.forward) * (push * closeness) + Vector3.up * (push * 0.3f));
                TryStagger(c, power);
            }

            if (motor) motor.Launch(Mathf.Sqrt(2f * motor.Gravity * Mathf.Lerp(blastJumpHeight.x, blastJumpHeight.y, power)));

            PixelFx.Flash(ground + Vector3.up * 0.2f, Palette.White, 0.6f + 0.5f * power, 0.07f);
            PixelFx.Ring(ground, radius, power > 0.5f ? Palette.Red : Palette.Amber, Mathf.RoundToInt(20f + 20f * power), 0.3f, 0.24f);
            PixelFx.Ring(ground, radius * 0.55f, Palette.Yellow, 16, 0.22f, 0.18f);
            PixelFx.Burst(ground + Vector3.up * 0.2f, Palette.Amber, Palette.Red, Mathf.RoundToInt(12f + 24f * power), 5f + 5f * power, 0.4f, 0.14f);
            onBlastJump.Invoke();
        }

        /// <summary>A bash with enough heat in it, landing while the Hive Mother winds up, cancels her slam.</summary>
        void TryStagger(Collider c, float power)
        {
            if (power * 100f < staggerHeat) return;
            var boss = c.GetComponentInParent<HiveMotherAI>();
            if (boss) boss.Stagger(staggerTime);
        }

        // ---- Brands ----------------------------------------------------------------------------

        /// <summary>Marks what a searing bash hit, if it's something with health. Brands on it already are renewed.</summary>
        void ApplyBrand(Collider c, IDamageable target)
        {
            if (!c.GetComponentInParent<Health>()) return;   // eggs, webs and hatchlings go at the first hit anyway
            int slot = -1;
            float oldest = float.PositiveInfinity;
            for (int i = 0; i < MaxBrands; i++)
            {
                if (ReferenceEquals(brands[i].target, target) && brands[i].collider) { slot = i; break; }
                float until = brands[i].collider ? brands[i].until : float.NegativeInfinity;   // a free slot is the oldest
                if (until < oldest)
                {
                    oldest = until;
                    slot = i;
                }
            }
            brands[slot] = new Brand { collider = c, target = target, until = Time.time + brandTime, nextEmber = 0f };
        }

        /// <summary>Brands smoulder: embers rise off each branded enemy until it fades or the enemy is gone.</summary>
        void TendBrands()
        {
            int count = 0;
            float now = Time.time;
            for (int i = 0; i < MaxBrands; i++)
            {
                ref Brand b = ref brands[i];
                if (!b.collider || now >= b.until)
                {
                    b.collider = null;
                    b.target = null;
                    continue;
                }
                count++;
                if (now < b.nextEmber) continue;
                b.nextEmber = now + 0.12f;
                Bounds bounds = b.collider.bounds;
                Vector3 at = bounds.center + new Vector3(Random.Range(-0.5f, 0.5f) * bounds.extents.x, bounds.extents.y * 0.6f,
                                                         Random.Range(-0.5f, 0.5f) * bounds.extents.z);
                PixelFx.Mote(at, new Vector3(0f, Random.Range(0.7f, 1.1f), 0f), (i + (int)(now * 8f)) % 3 == 0 ? Palette.Red : Palette.Amber,
                             Random.Range(0.06f, 0.09f), Random.Range(0.4f, 0.6f));
            }
            BrandCount = count;
        }

        /// <summary>Every brand in reach bursts, hurting its enemy and splashing anything near it. Returns how many burst.</summary>
        int Detonate(float heat)
        {
            if (BrandCount == 0) return 0;
            float damage = brandDamage + brandHeatDamage * heat;
            Vector3 me = player.position;
            int burst = 0;
            for (int b = 0; b < MaxBrands; b++)
            {
                Collider marked = brands[b].collider;
                if (!marked) continue;
                Vector3 center = marked.bounds.center;
                if ((center - me).sqrMagnitude > brandReach * brandReach) continue;   // out of reach: it keeps smouldering
                brands[b].collider = null;
                brands[b].target = null;
                burst++;

                Gather(center, brandRadius, center);
                for (int i = 0; i < struck.Count; i++)
                {
                    Vector3 point = struckBy[i].bounds.ClosestPoint(center);
                    float distance = Vector3.Distance(center, point);
                    if (distance > brandRadius) continue;
                    Hit(struck[i], damage * (1f - 0.5f * Mathf.Clamp01(distance / brandRadius)), point, point - center, distance);
                }
                PixelFx.Flash(center, Palette.Amber, 0.8f, 0.07f);
                PixelFx.Burst(center, Palette.Amber, Palette.Red, 16, 6f, 0.4f, 0.13f);
                PixelFx.Ring(new Vector3(center.x, marked.bounds.min.y, center.z), brandRadius, Palette.Amber, 18, 0.25f, 0.18f);
                onBrandBurst.Invoke();
            }
            BrandCount -= burst;
            return burst;
        }

        // ---- Shared ----------------------------------------------------------------------------

        /// <summary>
        /// Fills <c>struck</c> with everything that can be hurt within <paramref name="radius"/> of <paramref name="center"/>,
        /// once each, leaving out the player, with the collider that was found for each in <c>struckBy</c>.
        /// </summary>
        void Gather(Vector3 center, float radius, Vector3 from)
        {
            struck.Clear();
            struckBy.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider c = overlap[i];
                if (c.transform.IsChildOf(player)) continue;
                var target = c.GetComponentInParent<IDamageable>();
                if (target == null) continue;
                int seen = struck.IndexOf(target);
                if (seen < 0)
                {
                    struck.Add(target);
                    struckBy.Add(c);
                }
                else if ((c.bounds.ClosestPoint(from) - from).sqrMagnitude < (struckBy[seen].bounds.ClosestPoint(from) - from).sqrMagnitude)
                    struckBy[seen] = c;   // keep whichever of its colliders is nearest
            }
        }

        /// <summary>True if nothing solid stands between <paramref name="from"/> and <paramref name="point"/> on <paramref name="target"/>.</summary>
        bool InSight(Vector3 from, Vector3 point, IDamageable target)
        {
            Vector3 to = point - from;
            float distance = to.magnitude;
            if (distance < 0.05f) return true;
            if (!Raycasts.Nearest(from, to / distance, distance, player, out RaycastHit hit, false)) return true;
            return hit.distance >= distance - 0.1f || ReferenceEquals(hit.collider.GetComponentInParent<IDamageable>(), target);
        }

        void Hit(IDamageable target, float amount, Vector3 point, Vector3 direction, float distance)
        {
            target.TakeDamage(new DamageInfo
            {
                Amount = amount, Kind = DamageKind.Melee, Point = point, Direction = direction.normalized,
                Distance = distance, Hits = 1, Source = player.gameObject,
            });
        }

        static void Shove(Collider c, Vector3 velocity)
        {
            var pushable = c.GetComponentInParent<IPushable>();
            if (pushable != null) pushable.Push(velocity);
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback = default)
        {
            v.y = 0f;
            if (v.sqrMagnitude > 1e-4f) return v.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 1e-4f ? fallback.normalized : Vector3.forward;
        }
    }
}
