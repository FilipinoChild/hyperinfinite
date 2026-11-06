using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The player's HUD, in the bottom-left corner: an HP bar with the numbers, and the thruster dash's cooldown. 
    /// The HP bar's color runs from green to red as health falls, a pale trail shows each hit for a moment
    ///  before draining away, and it pulses when health is low. The dash bar fills as the cooldown 
    /// runs down, then turns cyan and reads READY.
    ///
    /// In the middle sits the crosshair. Its gap shows the shotgun's spread, so it tightens as you aim,
    /// and red marks flash at its corners when a blast lands. Right below it is the shotgun's heat gauge,
    /// kept small so it stays out of the way: amber as heat builds, flashing just
    /// before the next blast would overheat the gun, and red while it's locked. Words appear under it
    /// only when there's something to do: VENT NOW, VENTING or OVERHEATED.
    ///
    /// In the top-left corner is the wave counter, for scenes with a ScuttlerSpawner: GET READY before
    /// the first wave, then the wave number with how many Scuttlers are left, a countdown between waves,
    /// and WAVES CLEARED at the end. The number pops when a new wave arrives.
    ///
    /// When you die, the screen dims and a Respawn button comes up (click it or press Enter). 
    /// It brings you back right where you fell, at full health, with a moment of
    /// protection. It doesn't move you or reset anything else: the waves, the boss and the eggs carry on.
    /// While you're down, the controls are off and the cursor is free to click.
    ///
    /// Put it on the Player, next to Health and PlayerMotor; Hyper Infinite > Build Test Level does. It
    /// makes its own screen-space canvas, laid out for 1920 x 1080 and scaled to any screen, so every
    /// scene with the Player in it gets the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHud : MonoBehaviour
    {
        [Tooltip("Below this share of health the HP bar pulses.")]
        [SerializeField, Range(0f, 1f)] float lowHealth = 0.3f;
        [Tooltip("Seconds a hit's trail lingers before it drains.")]
        [SerializeField, Min(0f)] float trailDelay = 0.45f;
        [Tooltip("Seconds of immunity after respawning, so nothing can knock you straight back down.")]
        [SerializeField, Min(0f)] float respawnProtection = 2f;

        static readonly Color PanelColor = new Color(0.04f, 0.03f, 0.06f, 0.7f);
        static readonly Color TrackColor = new Color(0.16f, 0.14f, 0.2f, 1f);
        static readonly Color HealthyColor = new Color(0.36f, 0.9f, 0.46f);
        static readonly Color HurtColor = new Color(1f, 0.72f, 0.2f);
        static readonly Color DyingColor = new Color(0.93f, 0.17f, 0.16f);
        static readonly Color TrailColor = new Color(1f, 0.92f, 0.85f, 0.85f);
        static readonly Color DashReadyColor = new Color(0.19f, 0.78f, 1f);
        static readonly Color DashCoolingColor = new Color(0.36f, 0.42f, 0.55f);

        Health health;
        PlayerMotor motor;
        PlayerControls controls;
        EnergyBlastShotgun shotgun;
        Camera view;
        GameObject crosshair, hitMarker, heatGauge;
        RectTransform heatFill;
        Image heatFillImage;
        Text heatLabel;
        readonly RectTransform[] ticks = new RectTransform[4];
        Canvas canvas;
        GameObject deathScreen;
        Button respawnButton;
        bool shownDead, hadCursor;
        ScuttlerSpawner waves;
        GameObject wavePanel;
        Text waveTitle, waveDetail;
        int lastWave = -1;
        float wavePop, waveSearch;
        RectTransform healthFill, healthTrail, dashFill;
        Image healthFillImage, dashFillImage;
        Text healthValue, dashValue;
        float trail = 1f;
        Font font;

        void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<PlayerMotor>();
            controls = GetComponent<PlayerControls>();
            shotgun = GetComponentInChildren<EnergyBlastShotgun>();
            view = GetComponentInChildren<Camera>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        void Start()
        {
            // The Respawn button needs an EventSystem to be clicked. The Input System's UI module finds
            // its default actions (mouse and Enter) by itself.
            if (!FindAnyObjectByType<EventSystem>())
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>Back on your feet where you fell, at full health. Nothing else resets.</summary>
        public void Respawn()
        {
            if (!health || !health.IsDead) return;
            health.Revive(respawnProtection);
            HideDeathScreen();
        }

        void ShowDeathScreen()
        {
            shownDead = true;
            deathScreen.SetActive(true);
            if (controls)
            {
                hadCursor = controls.CursorCaptured;
                controls.GameplayEnabled = false;   // the dead don't walk or shoot
                controls.SetCursorCaptured(false);  // so the button can be clicked
            }
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(respawnButton.gameObject);   // for Enter
        }

        void HideDeathScreen()
        {
            shownDead = false;
            deathScreen.SetActive(false);
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == respawnButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            if (controls)
            {
                controls.GameplayEnabled = true;
                if (hadCursor) controls.SetCursorCaptured(true);
            }
        }

        void OnEnable()
        {
            if (canvas) canvas.enabled = true;
        }

        void OnDisable()
        {
            if (canvas) canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
        }

        void BuildCanvas()
        {
            var canvasObject = new GameObject("Player HUD (screen)");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // over the gun sprite
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;   // scale with the screen's height
            canvasObject.AddComponent<GraphicRaycaster>();

            // Bottom left: HP on top, the dash under it.
            RectTransform panel = Box("Survival Meters", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                      new Vector2(36f, 36f), new Vector2(420f, 112f), PanelColor);

            Label("HP Label", panel, new Vector2(16f, -12f), new Vector2(120f, 28f), "HP", 24, TextAnchor.UpperLeft, FontStyle.Bold);
            healthValue = Label("HP Value", panel, new Vector2(-16f, -12f), new Vector2(240f, 28f), "", 24, TextAnchor.UpperRight, FontStyle.Bold);
            healthValue.rectTransform.anchorMin = healthValue.rectTransform.anchorMax = healthValue.rectTransform.pivot = new Vector2(1f, 1f);
            RectTransform healthTrack = Box("HP Bar", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -44f),
                                            new Vector2(388f, 20f), TrackColor);
            healthTrail = Fill("Hit Trail", healthTrack, TrailColor, out _);
            healthFill = Fill("Health", healthTrack, HealthyColor, out healthFillImage);

            Label("Dash Label", panel, new Vector2(16f, -72f), new Vector2(120f, 22f), "DASH", 18, TextAnchor.UpperLeft, FontStyle.Bold);
            dashValue = Label("Dash Value", panel, new Vector2(-16f, -72f), new Vector2(200f, 22f), "", 18, TextAnchor.UpperRight, FontStyle.Bold);
            dashValue.rectTransform.anchorMin = dashValue.rectTransform.anchorMax = dashValue.rectTransform.pivot = new Vector2(1f, 1f);
            RectTransform dashTrack = Box("Dash Bar", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -79f),
                                          new Vector2(170f, 10f), TrackColor);
            dashFill = Fill("Dash", dashTrack, DashReadyColor, out dashFillImage);

            BuildWaveCounter(canvasObject.transform);
            BuildCrosshair(canvasObject.transform);
            BuildHeatGauge(canvasObject.transform);
            BuildDeathScreen(canvasObject.transform);   // last, so it covers everything else
        }

        /// <summary>The wave counter in the top-left corner: the wave in big letters, and what's left under it.</summary>
        void BuildWaveCounter(Transform parent)
        {
            RectTransform panel = Box("Wave Counter", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f),
                                      new Vector2(320f, 92f), PanelColor);
            wavePanel = panel.gameObject;
            waveTitle = Label("Wave", panel, new Vector2(16f, -8f), new Vector2(288f, 48f), "", 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            waveTitle.rectTransform.pivot = new Vector2(0f, 0.5f);   // pops from its left edge
            waveTitle.rectTransform.anchoredPosition = new Vector2(16f, -32f);
            waveDetail = Label("Left", panel, new Vector2(16f, -58f), new Vector2(288f, 26f), "", 20, TextAnchor.UpperLeft, FontStyle.Normal);
            wavePanel.SetActive(false);   // until a spawner turns up
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
            wavePop = Mathf.MoveTowards(wavePop, 0f, dt * 2.5f);
            waveTitle.rectTransform.localScale = Vector3.one * (1f + 0.3f * wavePop * wavePop);

            if (waves.AllCleared)
            {
                waveTitle.text = "WAVES CLEARED";
                waveTitle.color = DashReadyColor;
                waveDetail.text = total > 0 ? "All " + total + " waves down" : "";
                return;
            }
            waveTitle.color = Color.Lerp(Color.white, HurtColor, wavePop);
            if (wave == 0)
            {
                waveTitle.text = "GET READY";
                waveDetail.text = "First wave in " + Mathf.CeilToInt(waves.NextWaveIn) + " s";
            }
            else
            {
                waveTitle.text = total > 0 ? "WAVE " + wave + " / " + total : "WAVE " + wave;
                waveDetail.text = remaining > 0 ? remaining + (remaining == 1 ? " Scuttler left" : " Scuttlers left")
                                : "Next wave in " + Mathf.CeilToInt(waves.NextWaveIn) + " s";
            }
        }

        /// <summary>Four ticks with dark edges around the screen's center, and the hit marks at its corners.</summary>
        void BuildCrosshair(Transform parent)
        {
            RectTransform root = Rect("Crosshair", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            crosshair = root.gameObject;
            const float length = 9f, thick = 2f;
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2;
                var size = vertical ? new Vector2(thick, length) : new Vector2(length, thick);
                ticks[i] = Rect("Tick " + (i + 1), root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + Vector2.one * 2f);
                Box("Edge", ticks[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + Vector2.one * 2f, new Color(0f, 0f, 0f, 0.6f));
                Box("Ink", ticks[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(1f, 1f, 1f, 0.9f));
            }

            RectTransform marks = Rect("Hit Marker", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            hitMarker = marks.gameObject;
            var ink = new Color(1f, 0.32f, 0.25f, 0.95f);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sy = i < 2 ? 1f : -1f;
                for (float d = 9f; d <= 17f; d += 4f)
                    Box("Mark", marks, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sx * d, sy * d), new Vector2(4f, 4f), ink);
            }
            hitMarker.SetActive(false);
        }

        /// <summary>The heat gauge, just below the crosshair: a slim bar marked in fifths, and a warning under it when needed.</summary>
        void BuildHeatGauge(Transform parent)
        {
            RectTransform back = Box("Heat Gauge", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -93f),
                                     new Vector2(180f, 11f), PanelColor);
            heatGauge = back.gameObject;
            RectTransform track = Box("Heat Bar", back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                      new Vector2(176f, 7f), TrackColor);
            heatFill = Fill("Heat", track, HurtColor, out heatFillImage);
            for (int i = 1; i < 5; i++)   // marks at every fifth, about one blast's worth of heat each
                Box("Mark", track, new Vector2(0.2f * i, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 7f), PanelColor);
            heatLabel = Label("Heat Warning", back, Vector2.zero, new Vector2(300f, 18f), "", 14, TextAnchor.UpperCenter, FontStyle.Bold);
            heatLabel.rectTransform.anchorMin = heatLabel.rectTransform.anchorMax = heatLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            heatLabel.rectTransform.anchoredPosition = new Vector2(0f, -21f);
            heatGauge.SetActive(shotgun);
        }

        void UpdateHeatGauge()
        {
            if (!shotgun) return;
            heatGauge.SetActive(!shownDead);
            if (shownDead) return;
            float heat = Mathf.Clamp01(shotgun.Heat01);
            bool flashing = shotgun.AboutToOverheat && Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;
            SetFill(heatFill, heat);
            heatFillImage.color = shotgun.IsOverheated ? new Color(1f, 0.28f, 0.2f)
                                : flashing ? Color.white
                                : Color.Lerp(new Color(0.95f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.15f), heat);
            heatLabel.text = shotgun.IsOverheated ? "OVERHEATED" : shotgun.IsVenting ? "VENTING"
                           : shotgun.AboutToOverheat ? "VENT NOW" : "";   // nothing while all is well
            heatLabel.color = shotgun.IsOverheated || flashing ? new Color(1f, 0.4f, 0.3f) : new Color(0.85f, 0.88f, 0.92f);
        }

        /// <summary>Opens the crosshair to the shotgun's spread and flashes the hit marks after a blast lands.</summary>
        void UpdateCrosshair()
        {
            crosshair.SetActive(!shownDead);
            if (shownDead) return;
            const float length = 9f;
            float gap = shotgun && view ? Mathf.Max(6f, SpreadInPixels(shotgun.Spread)) : 6f;
            float reach = gap + length * 0.5f;
            ticks[0].anchoredPosition = new Vector2(0f, reach);
            ticks[1].anchoredPosition = new Vector2(0f, -reach);
            ticks[2].anchoredPosition = new Vector2(-reach, 0f);
            ticks[3].anchoredPosition = new Vector2(reach, 0f);
            hitMarker.SetActive(shotgun && shotgun.LastShot.BoltsOnTarget > 0 && Time.time - shotgun.LastShot.Time < 0.18f);
        }

        /// <summary>How far from the center a cone of this many degrees reaches, on the 1080-pixel-tall canvas.</summary>
        float SpreadInPixels(float degrees)
        {
            return Mathf.Tan(degrees * Mathf.Deg2Rad) / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 540f;
        }

        /// <summary>A dimmed screen with the Respawn button, hidden until you die.</summary>
        void BuildDeathScreen(Transform parent)
        {
            RectTransform dim = Box("Death Screen", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                                    new Color(0.05f, 0f, 0.02f, 0.62f));
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.sizeDelta = Vector2.zero;
            dim.GetComponent<Image>().raycastTarget = true;   // so clicks beside the button don't reach anything else
            deathScreen = dim.gameObject;

            Text title = Label("Title", dim, Vector2.zero, new Vector2(900f, 80f), "YOU WENT DOWN", 64, TextAnchor.MiddleCenter, FontStyle.Bold);
            Center(title.rectTransform, new Vector2(0f, 120f));
            title.color = DyingColor;
            Text note = Label("Note", dim, Vector2.zero, new Vector2(900f, 40f),
                              "Respawn right where you fell, at full health. The fight carries on.", 26, TextAnchor.MiddleCenter, FontStyle.Normal);
            Center(note.rectTransform, new Vector2(0f, 50f));

            RectTransform buttonRect = Box("Respawn Button", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                                           new Vector2(320f, 76f), Color.white);
            var buttonImage = buttonRect.GetComponent<Image>();
            buttonImage.raycastTarget = true;
            respawnButton = buttonRect.gameObject.AddComponent<Button>();
            respawnButton.targetGraphic = buttonImage;
            ColorBlock colors = respawnButton.colors;
            colors.normalColor = new Color(0.1f, 0.55f, 0.75f);
            colors.highlightedColor = DashReadyColor;
            colors.selectedColor = DashReadyColor;
            colors.pressedColor = new Color(0.08f, 0.36f, 0.5f);
            respawnButton.colors = colors;
            respawnButton.onClick.AddListener(Respawn);
            Text label = Label("Label", buttonRect, Vector2.zero, new Vector2(320f, 76f), "RESPAWN", 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            Center(label.rectTransform, Vector2.zero);

            Text hint = Label("Hint", dim, Vector2.zero, new Vector2(900f, 30f), "Click, or press Enter", 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            Center(hint.rectTransform, new Vector2(0f, -112f));
            hint.color = new Color(1f, 1f, 1f, 0.7f);

            deathScreen.SetActive(false);
        }

        static void Center(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            UpdateCrosshair();
            UpdateHeatGauge();
            UpdateWaveCounter(dt);
            if (health)
            {
                if (health.IsDead && !shownDead) ShowDeathScreen();
                else if (!health.IsDead && shownDead) HideDeathScreen();   // revived by something else

                float f = Mathf.Clamp01(health.Fraction);
                SetFill(healthFill, f);
                Color color = f > 0.5f ? Color.Lerp(HurtColor, HealthyColor, (f - 0.5f) * 2f) : Color.Lerp(DyingColor, HurtColor, f * 2f);
                if (f > 0f && f <= lowHealth) color = Color.Lerp(color, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f)));
                healthFillImage.color = color;

                // The trail holds where health was for a moment after a hit, then drains down to it.
                if (f >= trail) trail = f;
                else if (Time.time - health.LastDamageTime >= trailDelay) trail = Mathf.MoveTowards(trail, f, dt * 0.8f);
                SetFill(healthTrail, trail);
                healthValue.text = Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.Max);
            }
            if (motor)
            {
                float ready = motor.DashReadiness;
                bool isReady = ready >= 1f;
                SetFill(dashFill, ready);
                dashFillImage.color = isReady ? DashReadyColor : DashCoolingColor;
                dashValue.text = isReady ? "READY" : motor.DashCooldownLeft.ToString("0.0") + " s";
                dashValue.color = isReady ? DashReadyColor : Color.white;
            }
        }

        static void SetFill(RectTransform fill, float fraction)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        // ---- Building blocks -------------------------------------------------------------------

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>A bar's fill: it stretches across its track from the left, as far as SetFill says.</summary>
        static RectTransform Fill(string name, RectTransform track, Color color, out Image image)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(track, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, TextAnchor alignment, FontStyle style)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = value;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }
    }
}
