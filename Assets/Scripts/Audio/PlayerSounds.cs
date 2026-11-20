using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// The Player's sounds, hooked up to the events its parts already send: the shotgun, the melee, the motor,
    /// Health, HYPER, respawning and the heart shards. Nothing else needs wiring.
    ///
    /// It also keeps two loops going: a hum that climbs while a heavy bash charges (higher with more heat
    /// behind it), and a heartbeat while health is low. And it adds the small confirmations: a tick when a
    /// blast lands, a tink for each kill, and heart shards that chime higher the more come in at once.
    ///
    /// GameAudio adds one to the Player in every scene, so the Player prefab doesn't need it. It can be added
    /// by hand too.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSounds : MonoBehaviour
    {
        const float LowHealth = 0.3f;

        PlayerMotor motor;
        EnergyBlastShotgun shotgun;
        ShotgunMelee melee;
        Health health;
        HyperPower hyper;
        PlayerRespawn respawn;
        HeartShards hearts;
        int chargeLoop, heartLoop, heartRun;
        float lastHeart = -10f, lastVent = -10f;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            shotgun = GetComponentInChildren<EnergyBlastShotgun>();
            melee = GetComponentInChildren<ShotgunMelee>();
            health = GetComponent<Health>();
            hyper = GetComponent<HyperPower>();
            respawn = GetComponent<PlayerRespawn>();
            hearts = GetComponent<HeartShards>();
        }

        void OnEnable()
        {
            if (motor)
            {
                motor.onJump.AddListener(OnJump);
                motor.onDash.AddListener(OnDash);
                motor.onLand.AddListener(OnLand);
            }
            if (shotgun)
            {
                shotgun.onFire.AddListener(OnFire);
                shotgun.onDryFire.AddListener(OnDryFire);
                shotgun.onRedline.AddListener(OnRedline);
                shotgun.onVent.AddListener(OnVent);
                shotgun.onCooled.AddListener(OnCooled);
                shotgun.onVentHeal.AddListener(OnVentHeal);
                shotgun.onVentHealCut.AddListener(OnVentHealCut);
            }
            if (melee)
            {
                melee.onBash.AddListener(OnBash);
                melee.onSear.AddListener(OnSear);
                melee.onCounter.AddListener(OnCounter);
                melee.onHeavyBash.AddListener(OnHeavyBash);
                melee.onBlastJump.AddListener(OnBlastJump);
                melee.onBrandBurst.AddListener(OnBrandBurst);
            }
            if (health)
            {
                health.onDamaged.AddListener(OnDamaged);
            }
            if (hyper)
            {
                hyper.onHyperStart.AddListener(OnHyperStart);
                hyper.onHyperEnd.AddListener(OnHyperEnd);
                hyper.onExtended.AddListener(OnExtended);
            }
            if (respawn)
            {
                respawn.onDown.AddListener(OnDown);
                respawn.onRespawn.AddListener(OnRespawn);
            }
            if (hearts) hearts.onCollected.AddListener(OnHeartCollected);
            Health.AnyDied += OnAnyDied;
        }

        void OnDisable()
        {
            if (motor)
            {
                motor.onJump.RemoveListener(OnJump);
                motor.onDash.RemoveListener(OnDash);
                motor.onLand.RemoveListener(OnLand);
            }
            if (shotgun)
            {
                shotgun.onFire.RemoveListener(OnFire);
                shotgun.onDryFire.RemoveListener(OnDryFire);
                shotgun.onRedline.RemoveListener(OnRedline);
                shotgun.onVent.RemoveListener(OnVent);
                shotgun.onCooled.RemoveListener(OnCooled);
                shotgun.onVentHeal.RemoveListener(OnVentHeal);
                shotgun.onVentHealCut.RemoveListener(OnVentHealCut);
            }
            if (melee)
            {
                melee.onBash.RemoveListener(OnBash);
                melee.onSear.RemoveListener(OnSear);
                melee.onCounter.RemoveListener(OnCounter);
                melee.onHeavyBash.RemoveListener(OnHeavyBash);
                melee.onBlastJump.RemoveListener(OnBlastJump);
                melee.onBrandBurst.RemoveListener(OnBrandBurst);
            }
            if (health) health.onDamaged.RemoveListener(OnDamaged);
            if (hyper)
            {
                hyper.onHyperStart.RemoveListener(OnHyperStart);
                hyper.onHyperEnd.RemoveListener(OnHyperEnd);
                hyper.onExtended.RemoveListener(OnExtended);
            }
            if (respawn)
            {
                respawn.onDown.RemoveListener(OnDown);
                respawn.onRespawn.RemoveListener(OnRespawn);
            }
            if (hearts) hearts.onCollected.RemoveListener(OnHeartCollected);
            Health.AnyDied -= OnAnyDied;
            Sfx.Stop(chargeLoop);
            Sfx.Stop(heartLoop);
            chargeLoop = heartLoop = 0;
        }

        /// <summary>The two loops: the heavy bash's charge, and the heartbeat at low health.</summary>
        void Update()
        {
            bool charging = melee && melee.IsCharging;
            if (charging)
            {
                if (chargeLoop == 0) chargeLoop = Sfx.Loop(Sound.HeavyCharge);
                float heat = shotgun ? shotgun.Heat01 : 0f;
                float held = Mathf.Clamp01(melee.HoldTime / 0.8f);
                Sfx.SetLoop(chargeLoop, 0.5f + 0.5f * held, 0.8f + 0.25f * held + 0.35f * heat);   // climbs as it charges, higher with more heat
            }
            else if (chargeLoop != 0)
            {
                Sfx.Stop(chargeLoop);
                chargeLoop = 0;
            }

            bool low = health && !health.IsDead && health.Fraction <= LowHealth;
            if (low)
            {
                if (heartLoop == 0) heartLoop = Sfx.Loop(Sound.Heartbeat);
                float danger = 1f - health.Fraction / LowHealth;   // louder and quicker the lower it gets
                Sfx.SetLoop(heartLoop, 0.5f + 0.5f * danger, 1f + 0.25f * danger);
            }
            else if (heartLoop != 0)
            {
                Sfx.Stop(heartLoop);
                heartLoop = 0;
            }
        }

        // ---- Moving ---------------------------------------------------------------------------

        void OnJump(int count) => Sfx.Play(count <= 1 ? Sound.Jump : Sound.DoubleJump);
        void OnDash() => Sfx.Play(Sound.Dash);

        void OnLand(float speed)
        {
            if (speed < 3f) return;   // a step down makes no sound
            Sfx.Play(Sound.Land, Mathf.Clamp01(speed / 14f));
        }

        // ---- The shotgun ----------------------------------------------------------------------

        void OnFire()
        {
            Sfx.Play(Sound.ShotgunFire);
            Sfx.Play(Sound.Lever, 1f, 1f, 0.28f);   // as the lever swings back through the end of its cycle
            if (shotgun.LastShot.BoltsOnTarget > 0) Sfx.Play(Sound.HitTick, 1f, 1f, 0.03f);
        }

        void OnDryFire() => Sfx.Play(Sound.DryFire);
        void OnRedline() => Sfx.Play(Sound.Redline);
        void OnVent()
        {
            lastVent = Time.time;
            Sfx.Play(Sound.Vent);
        }

        /// <summary>A chime once a vent has finished and the gun is ready; a heavy bash empties it with a bang instead.</summary>
        void OnCooled()
        {
            if (Time.time - lastVent < 2f) Sfx.Play(Sound.Cooled);
        }
        void OnVentHeal() => Sfx.Play(Sound.VentHeal);
        void OnVentHealCut() => Sfx.Play(Sound.HealCut);

        // ---- Melee ----------------------------------------------------------------------------

        /// <summary>Plain bashes only: a searing bash and a counter have sounds of their own.</summary>
        void OnBash()
        {
            if (melee.LastBash != ShotgunMelee.BashKind.Plain) return;
            Sfx.Play(melee.LastBashLanded ? Sound.Bash : Sound.Swing);
        }

        void OnSear() => Sfx.Play(Sound.Sear);
        void OnCounter() => Sfx.Play(Sound.Counter);

        /// <summary>Bigger and deeper the more heat went in; an overload adds its crackle.</summary>
        void OnHeavyBash(float power)
        {
            Sfx.Play(Sound.HeavyBash, 0.55f + 0.45f * power, 1.12f - 0.24f * power);
            if (melee.LastHeavyOverload) Sfx.Play(Sound.Overload);
        }

        void OnBlastJump() => Sfx.Play(Sound.BlastJump);
        void OnBrandBurst() => Sfx.Play(Sound.BrandBurst);

        // ---- Health ---------------------------------------------------------------------------

        void OnDamaged(float amount)
        {
            if (health.IsDead) return;   // going down has its own sound
            Sfx.Play(Sound.Hurt, Mathf.Clamp01(0.6f + amount / 40f));
        }

        void OnDown() => Sfx.Play(Sound.PlayerDown);
        void OnRespawn() => Sfx.Play(Sound.Respawn);

        /// <summary>Each shard that lands chimes a step higher than the last, up to a fifth, while they keep coming.</summary>
        void OnHeartCollected()
        {
            heartRun = Time.time - lastHeart < 0.5f ? Mathf.Min(heartRun + 1, 7) : 0;
            lastHeart = Time.time;
            Sfx.Play(Sound.HeartCollect, 1f, Mathf.Pow(2f, heartRun / 12f));
        }

        void OnAnyDied(Health died)
        {
            if (died != health) Sfx.Play(Sound.KillTick);
        }

        // ---- HYPER ----------------------------------------------------------------------------

        void OnHyperStart() => Sfx.Play(Sound.HyperStart);
        void OnHyperEnd() => Sfx.Play(Sound.HyperEnd);
        void OnExtended(float seconds) => Sfx.Play(Sound.HyperExtend);
    }
}
