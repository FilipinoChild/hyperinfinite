using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The player's HUD, drawn in the game's pixel style with the parts from UiKit.
    ///
    /// Bottom left: the HP bar with its numbers, and the thruster dash's cooldown. The HP bar is green,
    /// then yellow, then red as health falls; a pale trail shows each hit for a moment before draining
    /// away; it blinks when health is low, and the whole panel jolts when you're hit.
    ///
    /// In the middle sits the crosshair. Its gap shows the shotgun's spread, so it tightens as you aim,
    /// and red marks flash at its corners when a blast or a bash lands. Below it is the shotgun's heat gauge:
    /// amber as heat builds, flashing when the next blast would redline the gun, and red once it has. While a
    /// heavy bash charges, the gauge strobes (it's how hard the bash will hit) and RELEASE TO BASH shows, or
    /// RELEASE TO OVERLOAD from a redlined gun. Under that, while the dash recharges, is a small copy of the bottom-left dash bar: it
    /// fills up, flashes when the dash is ready and then gets out of the way, so you can tell without
    /// looking down. LAST SHOT, VENTING or REDLINE! VENT OR OVERLOAD shows below both, only when there's something to do.
    /// Above the crosshair, WEBBED! SHOOT THE WEB blinks while a Web Trap has hold of you.
    ///
    /// Top left is the wave counter, for scenes with a ScuttlerSpawner: GET READY before the first wave,
    /// then the wave number with how many Scuttlers are left, a countdown between waves, and ALL CLEAR at
    /// the end. The number pops when a new wave arrives.
    ///
    /// Top middle, while a Boss is in the fight, is the boss bar with its name and health (BossBar).
    ///
    /// Getting hurt and going down are drawn by two helpers this makes: HurtOverlay (the red border and
    /// the arrows that point at whatever hit you) and DeathScreen (the Respawn button). PlayerRespawn
    /// does the actual respawning.
    ///
    /// Put it on the Player, next to Health and PlayerMotor; Hyper Infinite > Build Test Level does. It
    /// makes its own canvas, so every scene with the Player in it gets the HUD. Text only changes when
    /// the number it shows does, so the HUD allocates nothing from frame to frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHud : MonoBehaviour
    {
        [Tooltip("Below this share of health the HP bar blinks and the screen's border stays red.")]
        [SerializeField, Range(0f, 1f)] float lowHealth = 0.3f;
        [Tooltip("Seconds a hit's trail lingers before it drains.")]
        [SerializeField, Min(0f)] float trailDelay = 0.45f;

        const float HealthBarWidth = 396f, DashBarWidth = 180f, HeatBarWidth = 180f, CrosshairDashWidth = 120f;
        const float TickLength = 9f, TickThickness = 3f;

        static readonly Vector2 TopLeft = new Vector2(0f, 1f), TopRight = new Vector2(1f, 1f), Middle = new Vector2(0.5f, 0.5f);
        static readonly Color TrailColor = new Color(1f, 0.92f, 0.85f, 0.85f);

        Health health;
        PlayerRespawn respawn;
        PlayerMotor motor;
        EnergyBlastShotgun shotgun;
        ShotgunMelee melee;
        Camera view;
        Canvas canvas;
        HurtOverlay hurt;
        DeathScreen deathScreen;
        bool shownDead;

        // Bottom left.
        RectTransform meters;
        Vector2 metersHome;
        Image healthFill, healthTrail, dashFill;
        Text healthValue, dashValue;
        float trail = 1f, jolt;
        int shownHealth = -1, shownMaxHealth = -1, shownDash = -2;

        // The crosshair, with the heat gauge and the dash cooldown under it.
        GameObject crosshair, hitMarker, heatGauge, dashGauge;
        readonly RectTransform[] ticks = new RectTransform[4];
        Image heatFill, crosshairDashFill;
        Text heatLabel, snarePrompt;
        float shownGap = -1f, dashReadyAt = -10f;
        bool dashWasReady = true;
        int shownHeatLabel = -1;

        // Top left.
        ScuttlerSpawner waves;
        GameObject wavePanel;
        Text waveTitle, waveDetail;
        float wavePop, waveSearch;
        int lastWave = -1, shownTitle = int.MinValue, shownDetail = int.MinValue;

        // Top middle.
        BossBar bossBar;

        void Awake()
        {
            health = GetComponent<Health>();
            respawn = GetComponent<PlayerRespawn>();
            if (!respawn && health) respawn = gameObject.AddComponent<PlayerRespawn>();   // an older Player prefab without one
            motor = GetComponent<PlayerMotor>();
            shotgun = GetComponentInChildren<EnergyBlastShotgun>();
            melee = GetComponentInChildren<ShotgunMelee>();
            view = GetComponentInChildren<Camera>();
            Build();
        }

        void Start()
        {
            UiKit.EnsureEventSystem();   // the Respawn button needs one to be clicked
        }

        void OnEnable()
        {
            if (canvas) canvas.enabled = true;
            if (health) health.onDamaged.AddListener(OnDamaged);
        }

        void OnDisable()
        {
            if (canvas) canvas.enabled = false;
            if (health) health.onDamaged.RemoveListener(OnDamaged);
        }

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
        }

        /// <summary>Back on your feet where you fell, at full health. Nothing else resets. The Respawn button calls this.</summary>
        public void Respawn()
        {
            if (respawn) respawn.Respawn();   // the death screen comes down by itself once you're alive
        }

        void OnDamaged(float amount)
        {
            jolt = 0.16f;
            if (hurt != null) hurt.Hit(amount);
        }

        // ---- Building it -----------------------------------------------------------------------

        void Build()
        {
            canvas = UiKit.NewCanvas("Player HUD (screen)", transform, 10);   // over the gun sprite
            Transform root = canvas.transform;
            if (health && view) hurt = new HurtOverlay(root, health, respawn, view.transform, lowHealth);   // first, so it's under everything else
            BuildMeters(root);
            BuildWaveCounter(root);
            bossBar = new BossBar(root);
            BuildCrosshair(root);
            BuildHeatGauge(root);
            if (respawn) deathScreen = new DeathScreen(root, respawn, Respawn);   // last, so it covers everything else
        }

        /// <summary>Bottom left: HP on top, the dash under it.</summary>
        void BuildMeters(Transform parent)
        {
            meters = UiKit.Panel("Survival Meters", parent, Vector2.zero, Vector2.zero, new Vector2(36f, 36f), new Vector2(432f, 114f));
            metersHome = meters.anchoredPosition;

            UiKit.Label("HP Label", meters, TopLeft, TopLeft, new Vector2(18f, -9f), new Vector2(120f, 30f), "HP", 24, TextAnchor.MiddleLeft, Palette.White);
            healthValue = UiKit.Label("HP Value", meters, TopRight, TopRight, new Vector2(-18f, -9f), new Vector2(240f, 30f), "", 24,
                                      TextAnchor.MiddleRight, Palette.White);
            RectTransform healthTrack = UiKit.Track("HP Bar", meters, TopLeft, TopLeft, new Vector2(18f, -48f), new Vector2(HealthBarWidth, 18f));
            healthTrail = UiKit.Fill("Hit Trail", healthTrack, TrailColor);
            healthFill = UiKit.Fill("Health", healthTrack, Palette.Green);
            UiKit.Border(healthTrack, Palette.Ink);

            UiKit.Label("Dash Label", meters, TopLeft, TopLeft, new Vector2(18f, -75f), new Vector2(90f, 24f), "DASH", 16, TextAnchor.MiddleLeft, Palette.Dim);
            dashValue = UiKit.Label("Dash Value", meters, TopRight, TopRight, new Vector2(-18f, -75f), new Vector2(120f, 24f), "", 16,
                                    TextAnchor.MiddleRight, Palette.White);
            RectTransform dashTrack = UiKit.Track("Dash Bar", meters, TopLeft, TopLeft, new Vector2(96f, -81f), new Vector2(DashBarWidth, 12f));
            dashFill = UiKit.Fill("Dash", dashTrack, Palette.Cyan);
            UiKit.Border(dashTrack, Palette.Ink);
        }

        /// <summary>Top left: the wave in big letters, and what's left under it.</summary>
        void BuildWaveCounter(Transform parent)
        {
            RectTransform panel = UiKit.Panel("Wave Counter", parent, TopLeft, TopLeft, new Vector2(36f, -36f), new Vector2(348f, 96f));
            wavePanel = panel.gameObject;
            waveTitle = UiKit.Label("Wave", panel, TopLeft, new Vector2(0f, 0.5f), new Vector2(18f, -33f), new Vector2(312f, 45f), "", 32,
                                    TextAnchor.MiddleLeft, Palette.White);   // pops from its left edge
            waveDetail = UiKit.Label("Left", panel, TopLeft, TopLeft, new Vector2(18f, -60f), new Vector2(312f, 24f), "", 16,
                                     TextAnchor.MiddleLeft, Palette.Dim);
            wavePanel.SetActive(false);   // until a spawner turns up
        }

        /// <summary>Four ticks with dark edges around the screen's center, the hit marks at its corners, and the dash cooldown bar below.</summary>
        void BuildCrosshair(Transform parent)
        {
            RectTransform root = UiKit.Area("Crosshair", parent, Middle, Middle, Vector2.zero, Vector2.zero);
            crosshair = root.gameObject;
            for (int i = 0; i < 4; i++)
            {
                Vector2 size = i < 2 ? new Vector2(TickThickness, TickLength) : new Vector2(TickLength, TickThickness);
                ticks[i] = UiKit.Area("Tick " + (i + 1), root, Middle, Middle, Vector2.zero, size);
                UiKit.Box("Edge", ticks[i], Middle, Middle, Vector2.zero, size + Vector2.one * 6f, new Color(0f, 0f, 0f, 0.6f));
                UiKit.Box("Ink", ticks[i], Middle, Middle, Vector2.zero, size, Palette.White);
            }

            RectTransform marks = UiKit.Area("Hit Marker", root, Middle, Middle, Vector2.zero, Vector2.zero);
            hitMarker = marks.gameObject;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sy = i < 2 ? 1f : -1f;
                for (float d = 9f; d <= 21f; d += 6f)
                    UiKit.Box("Mark", marks, Middle, Middle, new Vector2(sx * d, sy * d), new Vector2(6f, 6f), Palette.Red);
            }
            hitMarker.SetActive(false);

            // The dash cooldown: a small copy of the bottom-left dash bar, in the slot just under the heat gauge.
            RectTransform dashTrack = UiKit.Track("Dash Cooldown", root, Middle, new Vector2(0.5f, 1f), new Vector2(0f, -111f),
                                                  new Vector2(CrosshairDashWidth, 9f));
            dashGauge = dashTrack.gameObject;
            crosshairDashFill = UiKit.Fill("Dash", dashTrack, Palette.Steel);
            UiKit.Border(dashTrack, Palette.Ink);
            dashGauge.SetActive(false);

            // Shown while something sticky has hold of the player, just above the crosshair.
            snarePrompt = UiKit.Label("Snare Prompt", root, Middle, Middle, new Vector2(0f, 120f), new Vector2(420f, 24f), "WEBBED! SHOOT THE WEB", 16,
                                      TextAnchor.MiddleCenter, Palette.White);
            snarePrompt.gameObject.SetActive(false);
        }

        /// <summary>The heat gauge, just below the crosshair: a slim bar marked in fifths, and a warning under it when needed.</summary>
        void BuildHeatGauge(Transform parent)
        {
            RectTransform track = UiKit.Track("Heat Gauge", parent, Middle, new Vector2(0.5f, 1f), new Vector2(0f, -93f), new Vector2(HeatBarWidth, 12f));
            heatGauge = track.gameObject;
            heatFill = UiKit.Fill("Heat", track, Palette.Amber);
            for (int i = 1; i < 5; i++)   // marks at every fifth, about one blast's worth of heat each
                UiKit.Box("Mark", track, new Vector2(0.2f * i, 0.5f), Middle, Vector2.zero, new Vector2(3f, 12f), Palette.Ink);
            UiKit.Border(track, Palette.Ink);
            // The warning sits below the dash cooldown's slot, so the two never overlap.
            heatLabel = UiKit.Label("Heat Warning", track, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -21f), new Vector2(300f, 24f), "", 16,
                                    TextAnchor.MiddleCenter, Palette.White);
            heatGauge.SetActive(shotgun);
        }

        // ---- Keeping it up to date -------------------------------------------------------------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool dead = health && health.IsDead;
            if (dead != shownDead)
            {
                shownDead = dead;
                crosshair.SetActive(!dead);
                heatGauge.SetActive(!dead && shotgun);
                if (deathScreen != null)
                {
                    if (dead) deathScreen.Show();
                    else deathScreen.Hide();
                }
            }
            if (deathScreen != null) deathScreen.Tick();
            if (hurt != null) hurt.Tick(dt);
            if (!dead)
            {
                UpdateCrosshair();
                UpdateHeatGauge();
            }
            UpdateWaveCounter(dt);
            bossBar.Tick(dt);
            UpdateMeters(dt);
        }

        void UpdateMeters(float dt)
        {
            if (health)
            {
                float f = Mathf.Clamp01(health.Fraction);
                UiKit.SetFill(healthFill, f, HealthBarWidth);
                bool low = f > 0f && f <= lowHealth;
                Color color = f > 0.6f ? Palette.Green : f > lowHealth ? Palette.Yellow : Palette.Red;
                if (low && Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f) color = Palette.White;   // blinking
                healthFill.color = color;

                // The trail holds where health was for a moment after a hit, then drains down to it.
                if (f >= trail) trail = f;
                else if (Time.time - health.LastDamageTime >= trailDelay) trail = Mathf.MoveTowards(trail, f, dt * 0.8f);
                UiKit.SetFill(healthTrail, trail, HealthBarWidth);

                int now = Mathf.CeilToInt(health.Current), max = Mathf.CeilToInt(health.Max);
                if (now != shownHealth || max != shownMaxHealth)
                {
                    shownHealth = now;
                    shownMaxHealth = max;
                    healthValue.text = now + " / " + max;
                }

                // A hit jolts the panel sideways for a moment, a few pixels each way.
                if (jolt > 0f)
                {
                    jolt -= Time.unscaledDeltaTime;
                    float side = jolt <= 0f ? 0f : (int)(jolt * 25f) % 2 == 0 ? 2f * UiKit.Pixel : -2f * UiKit.Pixel;
                    meters.anchoredPosition = metersHome + new Vector2(side, 0f);
                }
            }
            if (motor)
            {
                float ready = motor.DashReadiness;
                bool isReady = ready >= 1f;
                UiKit.SetFill(dashFill, ready, DashBarWidth);
                int tenths = isReady ? -1 : Mathf.CeilToInt(motor.DashCooldownLeft * 10f);
                if (tenths != shownDash)
                {
                    shownDash = tenths;
                    dashFill.color = isReady ? Palette.Cyan : Palette.Steel;
                    dashValue.color = isReady ? Palette.Cyan : Palette.White;
                    dashValue.text = isReady ? "READY" : (tenths / 10) + "." + (tenths % 10) + " S";
                }
            }
        }

        /// <summary>Opens the crosshair to the shotgun's spread, flashes the hit marks after a blast lands, and fills the dash cooldown bar.</summary>
        void UpdateCrosshair()
        {
            float gap = shotgun && view ? Mathf.Max(6f, SpreadInPixels(shotgun.Spread)) : 6f;
            gap = Mathf.Round(gap / UiKit.Pixel) * UiKit.Pixel;   // moves a pixel at a time
            float reach = gap + TickLength * 0.5f;
            if (!Mathf.Approximately(gap, shownGap))
            {
                shownGap = gap;
                ticks[0].anchoredPosition = new Vector2(0f, reach);
                ticks[1].anchoredPosition = new Vector2(0f, -reach);
                ticks[2].anchoredPosition = new Vector2(-reach, 0f);
                ticks[3].anchoredPosition = new Vector2(reach, 0f);
            }
            bool hit = (shotgun && shotgun.LastShot.BoltsOnTarget > 0 && Time.time - shotgun.LastShot.Time < 0.18f)
                       || (melee && Time.time - melee.LastHitTime < 0.18f);
            if (hitMarker.activeSelf != hit) hitMarker.SetActive(hit);
            UpdateCrosshairDash();

            bool snared = motor && motor.IsSnared;
            if (snarePrompt.gameObject.activeSelf != snared) snarePrompt.gameObject.SetActive(snared);
            if (snared) snarePrompt.color = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? Palette.White : Palette.Cyan;   // blinking
        }

        /// <summary>
        /// The dash cooldown under the crosshair. It looks and fills like the bottom-left dash bar: grey
        /// while it recharges, cyan when ready. It flashes white the moment the dash is ready, stays for
        /// a beat, then disappears until the next dash.
        /// </summary>
        void UpdateCrosshairDash()
        {
            if (!motor) return;
            float ready = motor.DashReadiness;
            bool isReady = ready >= 1f;
            if (isReady && !dashWasReady) dashReadyAt = Time.time;
            dashWasReady = isReady;
            float since = Time.time - dashReadyAt;
            bool show = !isReady || since < 0.6f;
            if (dashGauge.activeSelf != show) dashGauge.SetActive(show);
            if (!show) return;
            UiKit.SetFill(crosshairDashFill, ready, CrosshairDashWidth);
            crosshairDashFill.color = !isReady ? Palette.Steel : since < 0.12f ? Palette.White : Palette.Cyan;
        }

        /// <summary>How far from the center a cone of this many degrees reaches, on the 1080-pixel-tall canvas.</summary>
        float SpreadInPixels(float degrees)
        {
            return Mathf.Tan(degrees * Mathf.Deg2Rad) / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 540f;
        }

        void UpdateHeatGauge()
        {
            if (!shotgun) return;
            float heat = Mathf.Clamp01(shotgun.Heat01);
            bool flashing = shotgun.AboutToRedline && Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;
            bool redlined = shotgun.IsRedlined;
            bool charging = melee && melee.IsCharging;
            bool strobe = Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f;
            UiKit.SetFill(heatFill, heat, HeatBarWidth);
            heatFill.color = charging ? (strobe ? Palette.White : Palette.Amber)
                           : redlined ? Palette.Red : flashing ? Palette.White : heat > 0.5f ? Palette.Amber : Palette.Yellow;
            int label = charging ? (redlined ? 5 : 4) : shotgun.IsVenting ? 2 : redlined ? 3 : shotgun.AboutToRedline ? 1 : 0;   // nothing while all is well
            if (label != shownHeatLabel)
            {
                shownHeatLabel = label;
                heatLabel.text = label == 5 ? "RELEASE TO OVERLOAD" : label == 4 ? "RELEASE TO BASH" : label == 3 ? "REDLINE! VENT OR OVERLOAD"
                               : label == 2 ? "VENTING" : label == 1 ? "LAST SHOT" : "";
            }
            heatLabel.color = charging ? (strobe ? Palette.White : redlined ? Palette.Red : Palette.Amber)
                            : redlined && !shotgun.IsVenting ? (Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? Palette.Red : Palette.White)
                            : flashing ? Palette.Red : Palette.White;
        }

        void UpdateWaveCounter(float dt)
        {
            if (!waves)
            {
                waveSearch -= dt;
                if (waveSearch > 0f) return;
                waveSearch = 1f;   // look again each second, in case one is added later
                waves = FindAnyObjectByType<ScuttlerSpawner>();
                wavePanel.SetActive(waves);
                if (!waves) return;
            }

            int wave = waves.Wave, total = waves.TotalWaves, remaining = waves.ScuttlersLeft;
            if (wave != lastWave)
            {
                if (wave > 0) wavePop = 1f;   // a new wave just arrived
                lastWave = wave;
            }
            if (wavePop > 0f)
            {
                wavePop = Mathf.MoveTowards(wavePop, 0f, dt * 2.5f);
                waveTitle.rectTransform.localScale = Vector3.one * (1f + 0.3f * wavePop * wavePop);
                waveTitle.color = wavePop > 0.5f ? Palette.Yellow : Palette.White;
            }

            // Each line of text gets a number that stands for what it says, so it's only rewritten when that changes.
            bool cleared = waves.AllCleared;
            int seconds = Mathf.CeilToInt(waves.NextWaveIn);
            int title = cleared ? -1 : wave * 1000 + total;
            int detail = cleared ? -1 - total : remaining > 0 ? remaining : -100000 - seconds;
            if (title != shownTitle)
            {
                shownTitle = title;
                waveTitle.text = cleared ? "ALL CLEAR" : wave == 0 ? "GET READY" : total > 0 ? "WAVE " + wave + " / " + total : "WAVE " + wave;
                if (cleared) waveTitle.color = Palette.Cyan;
            }
            if (detail != shownDetail)
            {
                shownDetail = detail;
                waveDetail.text = cleared ? (total > 0 ? "ALL " + total + " WAVES DOWN" : "")
                                : remaining > 0 ? remaining + (remaining == 1 ? " SCUTTLER LEFT" : " SCUTTLERS LEFT")
                                : (wave == 0 ? "FIRST WAVE IN " : "NEXT WAVE IN ") + seconds + " S";
            }
        }
    }
}
