using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// Going down and getting back up. When the player's health runs out the controls switch off, and
    /// after a short beat (so a click meant for the fight can't skip the death screen) they can respawn.
    ///
    /// Respawning puts you back on your feet right where you fell, at full health, with a cold gun and a
    /// ready dash. Nothing else resets: the waves, the boss and the eggs carry on. So that getting up in
    /// the middle of a pack isn't a death sentence, the thrusters fire a burst as you rise: it throws
    /// back everything nearby that can be shoved, pops any hatchlings in reach, and leaves you a few
    /// seconds of protection to get clear. The HUD shows the shield while it lasts.
    ///
    /// It also catches falls out of the level, putting you back on the last solid ground you stood on.
    ///
    /// Put it on the Player, next to Health. PlayerHud draws the death screen and calls Respawn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class PlayerRespawn : MonoBehaviour
    {
        [Header("Respawning")]
        [Tooltip("Seconds after going down before you can respawn.")]
        [SerializeField, Min(0f)] float respawnDelay = 1f;
        [Tooltip("Seconds of immunity after respawning, to get clear of whatever took you down.")]
        [SerializeField, Min(0f)] float protection = 3f;

        [Header("Thruster burst")]
        [Tooltip("How far the burst reaches as you get up, in meters.")]
        [SerializeField, Min(0f)] float burstRadius = 7f;
        [Tooltip("How hard it throws enemies back, in meters per second, right beside you. Less further out.")]
        [SerializeField, Min(0f)] float burstPush = 14f;

        [Header("Falling out of the level")]
        [Tooltip("Falling below this height puts you back on the last solid ground you stood on. So does getting outside the scene's PlayArea, if it has one.")]
        [SerializeField] float killHeight = -20f;

        [Header("Events (hook sounds up here)")]
        public UnityEvent onDown = new UnityEvent();
        public UnityEvent onRespawn = new UnityEvent();

        static readonly Collider[] nearby = new Collider[64];

        Health health;
        PlayerMotor motor;
        PlayerControls controls;
        EnergyBlastShotgun shotgun;
        Vector3 safePosition;
        float safeYaw, safeTimer;
        float downAt, protectedUntil = -1f;

        /// <summary>True from going down until respawning.</summary>
        public bool IsDown => health.IsDead;
        /// <summary>Seconds since going down; 0 when you're not.</summary>
        public float DownFor => IsDown ? Time.unscaledTime - downAt : 0f;
        /// <summary>Seconds until Respawn will work; 0 when it does, or when you're not down.</summary>
        public float ReadyIn => IsDown ? Mathf.Max(0f, downAt + respawnDelay - Time.unscaledTime) : 0f;
        public bool CanRespawn => IsDown && ReadyIn <= 0f;
        /// <summary>1 the moment you respawn, falling to 0 as the protection runs out.</summary>
        public float Protection01 => protection > 0f ? Mathf.Clamp01((protectedUntil - Time.time) / protection) : 0f;

        void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<PlayerMotor>();
            controls = GetComponent<PlayerControls>();
            shotgun = GetComponentInChildren<EnergyBlastShotgun>();
            safePosition = transform.position;
            safeYaw = transform.eulerAngles.y;
        }

        void OnEnable()
        {
            health.onDied.AddListener(GoDown);
        }

        void OnDisable()
        {
            health.onDied.RemoveListener(GoDown);
            if (controls) controls.Unblock(this);
        }

        void Update()
        {
            PlayArea area = PlayArea.Current;
            bool inside = !area || area.Contains(transform.position);
            if (transform.position.y < killHeight || !inside)
            {
                BackToSolidGround();
                return;
            }
            // Remember where you last stood, a few times a second; only ever inside the arena.
            if (!motor || !motor.IsGrounded || health.IsDead) return;
            safeTimer -= Time.deltaTime;
            if (safeTimer > 0f) return;
            safeTimer = 0.25f;
            safePosition = transform.position;
            safeYaw = transform.eulerAngles.y;
        }

        /// <summary>
        /// Back on your feet where you fell, at full health, with the thruster burst and a spell of
        /// protection. Returns false if you're not down, or went down too recently.
        /// </summary>
        public bool Respawn()
        {
            if (!CanRespawn) return false;
            health.Revive(protection);
            protectedUntil = Time.time + protection;
            if (motor) motor.Teleport(transform.position, transform.eulerAngles.y);   // same spot, but standing still with a ready dash
            if (shotgun) shotgun.CoolCompletely();
            Burst();
            if (controls) controls.Unblock(this);
            onRespawn.Invoke();
            return true;
        }

        void GoDown()
        {
            downAt = Time.unscaledTime;
            if (controls) controls.Block(this);   // the dead don't walk or shoot, and the cursor is free for the death screen
            onDown.Invoke();
        }

        /// <summary>The thrusters fire as you rise: two rings across the ground, and everything in reach is thrown back.</summary>
        void Burst()
        {
            Vector3 center = transform.position;
            PixelFx.Ring(center, burstRadius, Palette.Cyan, 36, 0.35f, 0.26f);
            PixelFx.Ring(center, burstRadius * 0.6f, Palette.PaleCyan, 20, 0.3f, 0.18f);
            PixelFx.Burst(center + Vector3.up * 0.9f, Palette.Cyan, Palette.White, 18, 7f, 0.5f, 0.12f);
            if (burstRadius <= 0f) return;

            // Hatchlings are too small to survive it.
            float reach = burstRadius * burstRadius;
            for (int i = TinyScuttler.Active.Count - 1; i >= 0; i--)
            {
                TinyScuttler tiny = TinyScuttler.Active[i];
                if (!tiny || (tiny.transform.position - center).sqrMagnitude > reach) continue;
                tiny.TakeDamage(new DamageInfo
                {
                    Amount = 1f, Kind = DamageKind.Burst, Point = tiny.transform.position, Direction = Vector3.up,
                    Hits = 1, Source = gameObject,
                });
            }

            // Anything else that can be shoved is thrown back, hardest right beside you.
            int count = Physics.OverlapSphereNonAlloc(center, burstRadius, nearby, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider other = nearby[i];
                nearby[i] = null;   // don't hold on to it
                if (other.transform.IsChildOf(transform)) continue;
                var pushable = other.GetComponentInParent<IPushable>();
                if (pushable == null) continue;
                Vector3 away = other.transform.position - center;
                away.y = 0f;
                float distance = away.magnitude;
                away = distance > 0.01f ? away / distance : transform.forward;
                pushable.Push(away * (burstPush * (1f - 0.5f * Mathf.Clamp01(distance / burstRadius))));
            }
        }

        void BackToSolidGround()
        {
            if (motor) motor.Teleport(safePosition + Vector3.up * 0.1f, safeYaw);
            else transform.position = safePosition;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.19f, 0.78f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, burstRadius);   // the respawn burst's reach
        }
    }
}
