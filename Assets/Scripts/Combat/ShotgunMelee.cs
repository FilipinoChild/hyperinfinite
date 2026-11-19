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
    /// that heat for extra damage and a stagger, and it brands what it hits. A branded enemy carries a pixel
    /// flame over its head and smoulders with embers for a few seconds; the flame blinks white as it runs out. Bash a Scuttler while it crouches to bite and the bite is countered: it takes
    /// extra damage and is thrown clear.
    ///
    /// Hold Melee to charge a heavy bash (you move slower while charging), and let go to throw it: you lunge
    /// about 4 m forward, safe from harm, and as the lunge ends the bash lands. It dumps every point of heat in
    /// the gun into one explosion out of the emitters: the more heat, the harder, wider
    /// and further it hits and the harder it throws enemies back, so a full gauge clears a crowd. Enemies don't
    /// shield each other from it; only walls and rocks do. From a redlined gun it overloads, hitting harder and
    /// wider still. A full gauge kicks you back a little as it goes off. Heat bolts fan out across the
    /// cone, a shockwave sweeps the floor, and every enemy hit flares. Every brand within reach bursts at the
    /// same moment, each linked to the gun by a bolt of heat, splashing whatever is near it. If the Hive
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
        [SerializeField] Vector2 heavyRange = new Vector2(3.5f, 7f);
        [Tooltip("Half-width of the blast's cone with no heat, and with a full gauge, in degrees.")]
        [SerializeField] Vector2 heavyWidth = new Vector2(40f, 65f);
        [Tooltip("Share of the damage still done at the far edge of the cone.")]
        [SerializeField, Range(0f, 1f)] float heavyEdgeDamage = 0.8f;
        [Tooltip("How hard it throws enemies back with no heat, and with a full gauge, in meters per second.")]
        [SerializeField] Vector2 heavyPush = new Vector2(5f, 14f);
        [Tooltip("Share of your speed you keep while charging.")]
        [SerializeField, Range(0.1f, 1f)] float chargeSlow = 0.6f;
        [Tooltip("Speed of the lunge forward when you let go, in meters per second.")]
        [SerializeField, Min(0f)] float lungeSpeed = 22f;
        [Tooltip("Seconds the lunge lasts; the bash lands as it ends. 0.18 at 22 m/s covers about 4 m. 0 lands it at once, with no lunge.")]
        [SerializeField, Min(0f)] float lungeTime = 0.18f;
        [Tooltip("The backward kick a full gauge gives you as the bash lands, in meters per second.")]
        [SerializeField, Min(0f)] float recoil = 4f;
        [Tooltip("Seconds you can't be hurt after letting go, covering the lunge and the blast, so a swarm can't punish every swing.")]
        [SerializeField, Min(0f)] float protection = 0.4f;
        [Tooltip("Seconds before the shotgun can be used again after a heavy bash.")]
        [SerializeField, Min(0.05f)] float heavyCooldown = 0.6f;

        [Header("Overload (a heavy bash from a redlined gun)")]
        [Tooltip("Damage is multiplied by this.")]
        [SerializeField, Min(1f)] float overloadDamage = 1.5f;
        [Tooltip("Meters added to the reach.")]
        [SerializeField, Min(0f)] float overloadReach = 1.5f;
        [Tooltip("Degrees added to the cone's half-width.")]
        [SerializeField, Min(0f)] float overloadWidth = 10f;
        [Tooltip("Meters per second added to the throw.")]
        [SerializeField, Min(0f)] float overloadPush = 4f;

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
            public float until, nextEmber, since;
        }

        const int MaxBrands = 12;
        static readonly Collider[] overlap = new Collider[128];
        static readonly RaycastHit[] sight = new RaycastHit[16];

        readonly Brand[] brands = new Brand[MaxBrands];
        // The flame over each brand: one mark per slot, made once and reused.
        readonly Transform[] marks = new Transform[MaxBrands];
        readonly SpriteRenderer[] markFlames = new SpriteRenderer[MaxBrands], markCores = new SpriteRenderer[MaxBrands];
        readonly bool[] markShown = new bool[MaxBrands];
        GameObject markHolder;
        Material markMaterial;
        readonly List<IDamageable> struck = new List<IDamageable>(16);
        readonly List<Collider> struckBy = new List<Collider>(16);
        EnergyBlastShotgun shotgun;
        PlayerControls controls;
        PlayerMotor motor;
        Health health;
        Transform view, player;
        float readyAt, landAt, pendingHeat;
        bool lunging, pendingOverload;

        /// <summary>True during the lunge between letting go of a heavy bash and the bash landing.</summary>
        public bool IsLunging => lunging;
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
        /// <summary>True if the last heavy bash came from a redlined gun.</summary>
        public bool LastHeavyOverload { get; private set; }
        /// <summary>When a bash or heavy bash last hurt something.</summary>
        public float LastHitTime { get; private set; } = -100f;
        /// <summary>True if the last bash connected.</summary>
        public bool LastBashLanded => LastHitTime >= LastBashTime;
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
            BuildMarks();
        }

        void OnDestroy()
        {
            if (markHolder) Destroy(markHolder);
            if (markMaterial) Destroy(markMaterial);
        }

        /// <summary>
        /// The brand flames: pixel sprites facing the camera with a black outline, like the creatures. They use the
        /// same unlit sprite shader as PixelFx, so they glow evenly in any light.
        /// </summary>
        void BuildMarks()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (!shader) return;   // no flames, but brands still smoulder and burst
            markMaterial = new Material(shader) { name = "Brand Marks" };
            markHolder = new GameObject("Brand Marks");
            for (int i = 0; i < MaxBrands; i++)
            {
                Transform mark = new GameObject("Brand Mark").transform;
                mark.SetParent(markHolder.transform, false);
                MarkLayer(mark, "Outline", PixelArt.BrandOutline, Palette.Ink, 1, markMaterial);
                markFlames[i] = MarkLayer(mark, "Flame", PixelArt.BrandFlame, Palette.Amber, 2, markMaterial);
                markCores[i] = MarkLayer(mark, "Core", PixelArt.BrandCore, Palette.Yellow, 3, markMaterial);
                mark.gameObject.SetActive(false);
                marks[i] = mark;
            }
        }

        static SpriteRenderer MarkLayer(Transform mark, string layerName, Sprite art, Color color, int order, Material material)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(mark, false);
            var sprite = layer.AddComponent<SpriteRenderer>();
            sprite.sharedMaterial = material;
            sprite.sprite = art;
            sprite.color = color;
            sprite.sortingOrder = order;   // outline, flame, core, back to front
            return sprite;
        }

        void Update()
        {
            TendBrands();
            if (IsDown) lunging = false;   // going down mid-lunge calls the bash off
            if (lunging && Time.time >= landAt)
            {
                lunging = false;
                Land(pendingHeat, pendingOverload, false);
            }
            if (!controls || IsDown) return;

            if (controls.HeavyMeleeCharging && motor) motor.Slow(chargeSlow);
            if (Time.time < readyAt) return;
            if (controls.QuickMeleePressed) Bash();
            else if (controls.HeavyMeleeReleased) Release();
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

        /// <summary>
        /// Letting go of a charged heavy bash: the heat comes out of the gun now, you're protected, and you lunge
        /// forward with the bash landing as the lunge ends. Aimed at the floor, it lands at once instead.
        /// </summary>
        void Release()
        {
            bool overload = shotgun.IsRedlined;
            float heat = shotgun.DumpHeat();
            if (health) health.Protect(protection);
            Vector3 forward = view.forward;
            bool atFloor = forward.y < -Mathf.Sin(blastJumpPitch * Mathf.Deg2Rad);
            if (atFloor || lungeTime <= 0f || !motor)
            {
                readyAt = Time.time + heavyCooldown;
                Land(heat, overload, true);
                return;
            }
            motor.Lunge(forward, lungeSpeed, lungeTime);
            lunging = true;
            landAt = Time.time + lungeTime;
            pendingHeat = heat;
            pendingOverload = overload;
            readyAt = landAt + heavyCooldown;
        }

        /// <summary>The heavy bash lands: a blast ahead, or underfoot if aimed at the floor, and every brand in reach bursts.</summary>
        void Land(float heat, bool overload, bool mayHitFloor)
        {
            float power = Mathf.Clamp01(heat / 100f);
            LastHeavyTime = Time.time;
            LastHeavyPower = power;
            LastHeavyOverload = overload;

            Vector3 origin = view.position, forward = view.forward;
            float damage = (heavyDamage + heavyHeatDamage * heat) * (overload ? overloadDamage : 1f);
            float push = Mathf.Lerp(heavyPush.x, heavyPush.y, power) + (overload ? overloadPush : 0f);

            // Aimed at the floor, close enough to touch it: the blast goes off underfoot and throws you up.
            float reach = Mathf.Lerp(heavyRange.x, heavyRange.y, power) + (overload ? overloadReach : 0f);
            RaycastHit floor = default;
            bool atFloor = mayHitFloor && forward.y < -Mathf.Sin(blastJumpPitch * Mathf.Deg2Rad)
                           && Raycasts.Nearest(origin, forward, reach + 0.5f, player, out floor, false)
                           && floor.normal.y > 0.6f;
            if (atFloor) BlastJump(floor.point, power, damage, push, overload);
            else Blast(origin, forward, reach, power, damage, push, overload);

            int burst = Detonate(heat);
            onHeavyBash.Invoke(power);
            onRecoil.Invoke(Mathf.Lerp(2.5f, 7f, power) + (burst > 0 ? 1f : 0f));
        }

        /// <summary>The bash thrown forward: everything in the cone in front is hit, the hotter the harder, wider and further.</summary>
        void Blast(Vector3 origin, Vector3 forward, float reach, float power, float damage, float push, bool overload)
        {
            float width = Mathf.Lerp(heavyWidth.x, heavyWidth.y, power) + (overload ? overloadWidth : 0f);
            Gather(origin, reach, origin);   // all round, then trimmed to the cone, so even the widest cone is covered
            int hits = 0;
            for (int i = 0; i < struck.Count; i++)
            {
                Collider c = struckBy[i];
                Vector3 point = c.bounds.ClosestPoint(origin);
                float distance = Vector3.Distance(origin, point);
                if (distance > reach) continue;
                if (distance > 0.5f && Vector3.Angle(forward, point - origin) > width) continue;
                if (!InSight(origin, point, struck[i])) continue;
                float closeness = Mathf.Lerp(1f, heavyEdgeDamage, distance / reach);
                Hit(struck[i], damage * closeness, point, forward, distance);
                Shove(c, Flat(point - origin, forward) * (push * closeness) + Vector3.up * (push * 0.2f));
                TryStagger(c, power);
                Flare(point, point - origin, power, overload, hits++);
            }

            // A full gauge kicks you back a little as it goes off.
            if (motor && power > 0f) motor.Push(-Flat(forward) * (recoil * power));

            Vector3 muzzle = origin + forward * 0.9f - view.up * 0.15f;
            PixelFx.Flash(muzzle, Palette.White, 0.4f + 0.5f * power + (overload ? 0.3f : 0f), 0.07f);
            PixelFx.Cone(muzzle, forward, width * 0.8f, power > 0.5f ? Palette.Red : Palette.Amber, Palette.Yellow,
                         Mathf.RoundToInt(16f + 40f * power), reach / 0.28f, 0.28f, 0.12f + 0.08f * power);
            HeatBolts(origin, muzzle, forward, reach, width, power, overload);
            Shockwave(forward, reach, width, power, overload);
            Embers(origin, forward, reach, width, power);
        }

        /// <summary>Bolts of heat fanned out across the cone from the emitters, the gun's own blast drawn in amber and red, stopping at walls.</summary>
        void HeatBolts(Vector3 origin, Vector3 muzzle, Vector3 forward, float reach, float width, float power, bool overload)
        {
            int count = Mathf.RoundToInt(7f + 10f * power) + (overload ? 4 : 0);
            Vector3 up = view.up;
            for (int i = 0; i < count; i++)
            {
                float across = count > 1 ? Mathf.Lerp(-width, width, (i + Random.Range(-0.3f, 0.3f)) / (count - 1)) : 0f;
                Vector3 direction = Quaternion.AngleAxis(across, up) * Quaternion.AngleAxis(Random.Range(-6f, 6f), view.right) * forward;
                float length = reach * Random.Range(0.75f, 1f);
                Vector3 end = Raycasts.Nearest(origin, direction, length, player, out RaycastHit wall, false) ? wall.point : origin + direction * length;
                Color color = i % 3 == 0 ? Palette.Yellow : i % 3 == 1 ? Palette.Amber : Palette.Red;
                PixelFx.Bolt(muzzle, end, overload && i % 4 == 0 ? Palette.White : color, 0.07f + 0.05f * power, 0.12f + 0.06f * power);
            }
        }

        /// <summary>The shockwave sweeping the floor across the cone: yellow, then amber, then red, each a little wider and slower.</summary>
        void Shockwave(Vector3 forward, float reach, float width, float power, bool overload)
        {
            Vector3 feet = player.position;
            int count = Mathf.RoundToInt(16f + 22f * power);
            PixelFx.Fan(feet, forward, reach * 0.85f, width, Palette.Yellow, count, 0.2f, 0.18f);
            PixelFx.Fan(feet, forward, reach, width, Palette.Amber, count + 6, 0.28f, 0.22f);
            if (power > 0.4f) PixelFx.Fan(feet, forward, reach * 1.1f, width, Palette.Red, count + 10, 0.36f, 0.26f);
            if (overload) PixelFx.Fan(feet, forward, reach * 0.6f, width + 10f, Palette.White, count, 0.14f, 0.2f);
        }

        /// <summary>Heat left hanging in the air across the cone: embers that rise and fade over a second.</summary>
        void Embers(Vector3 origin, Vector3 forward, float reach, float width, float power)
        {
            int count = Mathf.RoundToInt(8f + 28f * power);
            Vector3 flat = Flat(forward), ground = player.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(Random.Range(-width, width), Vector3.up) * flat;
                Vector3 at = ground + direction * Random.Range(1f, reach) + Vector3.up * Random.Range(0.2f, 1.8f);
                Color color = Random.value < 0.4f ? Palette.Amber : Random.value < 0.6f ? Palette.Red : Palette.Yellow;
                PixelFx.Mote(at, Vector3.up * Random.Range(0.4f, 0.9f) + direction * 0.5f, color, Random.Range(0.05f, 0.08f), Random.Range(0.6f, 1f));
            }
        }

        /// <summary>
        /// Where the blast meets an enemy: a flash, chunks thrown on through it, and sparks. Fewer pieces each the
        /// more there are, so a whole crowd still fits in the effects pool.
        /// </summary>
        void Flare(Vector3 point, Vector3 travel, float power, bool overload, int index)
        {
            int pieces = index < 6 ? 8 : index < 14 ? 5 : 3;
            PixelFx.Flash(point, index % 2 == 0 ? Palette.White : Palette.Yellow, 0.4f + 0.3f * power + (overload ? 0.15f : 0f), 0.07f);
            PixelFx.Cone(point, travel, 40f, Palette.Amber, Palette.Red, pieces, 4f + 4f * power, 0.35f, 0.11f);
            PixelFx.Sparks(point, -travel.normalized, Palette.Yellow, pieces / 2 + 1, 5f, 0.25f);
        }

        /// <summary>The bash into the floor: a shockwave all round where it lands, and you're thrown upward.</summary>
        void BlastJump(Vector3 ground, float power, float damage, float push, bool overload)
        {
            int hits = 0;
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
                Flare(point, point - ground, power, overload, hits++);
            }

            if (motor) motor.Launch(Mathf.Sqrt(2f * motor.Gravity * Mathf.Lerp(blastJumpHeight.x, blastJumpHeight.y, power)));

            PixelFx.Flash(ground + Vector3.up * 0.2f, Palette.White, 0.6f + 0.5f * power, 0.07f);
            PixelFx.Ring(ground, radius, power > 0.5f ? Palette.Red : Palette.Amber, Mathf.RoundToInt(20f + 20f * power), 0.3f, 0.24f);
            PixelFx.Ring(ground, radius * 0.55f, Palette.Yellow, 16, 0.22f, 0.18f);
            PixelFx.Burst(ground + Vector3.up * 0.2f, Palette.Amber, Palette.Red, Mathf.RoundToInt(12f + 24f * power), 5f + 5f * power, 0.4f, 0.14f);
            if (overload) PixelFx.Ring(ground, radius * 1.3f, Palette.White, 24, 0.18f, 0.2f);
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
            brands[slot] = new Brand { collider = c, target = target, until = Time.time + brandTime, nextEmber = 0f, since = Time.time };
        }

        /// <summary>
        /// Brands smoulder: a flame floats over each branded enemy, flickering in whole frames and blinking white
        /// in its last second and a half, and embers rise off it, until the brand fades or the enemy is gone.
        /// </summary>
        void TendBrands()
        {
            int count = 0;
            float now = Time.time;
            Quaternion facing = view ? view.rotation : Quaternion.identity;
            for (int i = 0; i < MaxBrands; i++)
            {
                ref Brand b = ref brands[i];
                if (!b.collider || now >= b.until)
                {
                    b.collider = null;
                    b.target = null;
                    ShowMark(i, false);
                    continue;
                }
                count++;
                Bounds bounds = b.collider.bounds;
                if (markHolder)
                {
                    ShowMark(i, true);
                    float age = now - b.since;
                    int beat = (int)(now * 6f) + i;   // each flame flickers on its own beat
                    bool fading = b.until - now < 1.5f && Mathf.Repeat(now * 8f, 1f) < 0.5f;
                    marks[i].SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + 0.45f, bounds.center.z), facing);
                    marks[i].localScale = Vector3.one * (age < 0.1f ? 2.1f : beat % 2 == 0 ? 1.5f : 1.6f);   // pops on, then flickers
                    markFlames[i].color = age < 0.1f || fading ? Palette.White : beat % 3 == 0 ? Palette.Red : Palette.Amber;
                    markCores[i].color = fading ? Palette.Amber : Palette.Yellow;
                }

                if (now < b.nextEmber) continue;
                b.nextEmber = now + 0.12f;
                Vector3 at = bounds.center + new Vector3(Random.Range(-0.5f, 0.5f) * bounds.extents.x, bounds.extents.y * 0.6f,
                                                         Random.Range(-0.5f, 0.5f) * bounds.extents.z);
                PixelFx.Mote(at, new Vector3(0f, Random.Range(0.7f, 1.1f), 0f), (i + (int)(now * 8f)) % 3 == 0 ? Palette.Red : Palette.Amber,
                             Random.Range(0.06f, 0.09f), Random.Range(0.4f, 0.6f));
            }
            BrandCount = count;
        }

        void ShowMark(int i, bool show)
        {
            if (markShown[i] == show || !marks[i]) return;
            markShown[i] = show;
            marks[i].gameObject.SetActive(show);
        }

        /// <summary>Every brand in reach bursts, hurting its enemy and splashing anything near it. Returns how many burst.</summary>
        int Detonate(float heat)
        {
            if (BrandCount == 0) return 0;
            float damage = brandDamage + brandHeatDamage * heat;
            Vector3 me = player.position;
            Vector3 muzzle = view.position + view.forward * 0.9f - view.up * 0.15f;
            int burst = 0;
            for (int b = 0; b < MaxBrands; b++)
            {
                Collider marked = brands[b].collider;
                if (!marked) continue;
                Vector3 center = marked.bounds.center;
                if ((center - me).sqrMagnitude > brandReach * brandReach) continue;   // out of reach: it keeps smouldering
                brands[b].collider = null;
                brands[b].target = null;
                ShowMark(b, false);
                burst++;
                PixelFx.Bolt(muzzle, center, Palette.Amber, 0.07f, 0.16f);   // the heat races from the gun to the brand

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

        /// <summary>
        /// True if no wall, rock or other scenery stands between <paramref name="from"/> and <paramref name="point"/>.
        /// Enemies and other things that can be hurt don't count, so a crowd doesn't shield its back rows.
        /// </summary>
        bool InSight(Vector3 from, Vector3 point, IDamageable target)
        {
            Vector3 to = point - from;
            float distance = to.magnitude - 0.1f;
            if (distance <= 0.05f) return true;
            int count = Physics.RaycastNonAlloc(from, to.normalized, sight, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = sight[i].collider;
                if (c.transform.IsChildOf(player)) continue;
                if (c.GetComponentInParent<IDamageable>() != null) continue;
                return false;
            }
            return true;
        }

        void Hit(IDamageable target, float amount, Vector3 point, Vector3 direction, float distance)
        {
            LastHitTime = Time.time;
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
