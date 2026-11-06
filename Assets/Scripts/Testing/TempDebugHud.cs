using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperInfinite
{
    /// <summary>
    /// TEMPORARY on-screen readouts for testing combat and controls.
    /// Drawn with IMGUI (OnGUI), so it needs no Canvas or UI package. F1 shows or hides the panel.
    /// It finds what it reports on by itself, so it works in any scene that has the Player in it.
    /// </summary>
    public class TempDebugHud : MonoBehaviour
    {
        [SerializeField] Key togglePanel = Key.F1;

        static readonly Color PanelColor = new Color(0.04f, 0.04f, 0.06f, 0.62f);
        static readonly Color ChipOff = new Color(0.2f, 0.21f, 0.25f, 0.85f);
        static readonly Color ChipOn = new Color(0.19f, 0.78f, 1f, 0.95f);
        const float Left = 18f;

        PlayerControls controls;
        PlayerMotor motor;
        EnergyBlastShotgun shotgun;
        Health health;
        Camera view;
        ScuttlerSpawner waves;
        string lookingAt = "";
        float lookingAtDistance = -1f;
        bool showPanel = true;
        float findTimer;
        float dashFlash, ventFlash, meleeFlash, heavyFlash;

        GUIStyle text, small, chipText;
        readonly GUIContent content = new GUIContent();
        float cursorY;

        void Update()
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f)
            {
                FindParts();
                findTimer = 1f;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && togglePanel != Key.None && keyboard[togglePanel].wasPressedThisFrame) showPanel = !showPanel;

            float dt = Time.unscaledDeltaTime;
            dashFlash -= dt; ventFlash -= dt; meleeFlash -= dt; heavyFlash -= dt;
            if (controls)
            {
                if (controls.DashPressed) dashFlash = 0.2f;
                if (controls.VentPressed) ventFlash = 0.2f;
                if (controls.QuickMeleePressed) meleeFlash = 0.2f;
                if (controls.HeavyMeleeReleased) heavyFlash = 0.2f;
            }

            // What's under the crosshair, and how far: handy for checking damage falloff.
            lookingAtDistance = -1f;
            if (view && controls && Raycasts.Nearest(view.transform.position, view.transform.forward, 200f, controls.transform, out RaycastHit hit))
            {
                lookingAt = hit.collider.name;
                lookingAtDistance = hit.distance;
            }
        }

        void FindParts()
        {
            if (!waves) waves = FindAnyObjectByType<ScuttlerSpawner>();
            if (!controls) controls = FindAnyObjectByType<PlayerControls>();
            if (!motor) motor = FindAnyObjectByType<PlayerMotor>();
            if (!shotgun) shotgun = FindAnyObjectByType<EnergyBlastShotgun>();
            if (!health && controls) health = controls.GetComponent<Health>();
            if (!view && controls) view = controls.GetComponentInChildren<Camera>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            MakeStyles();
            float scale = Screen.height / 1080f;          // lay out on a 1080-pixel-tall canvas at any resolution
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = 1080f;

            if (health) DrawHurt(width, height);
            DrawEggMarkers(scale);

            cursorY = waves ? 150f : 14f;   // below PlayerHud's wave counter when there is one
            if (!showPanel)
            {
                Line("F1: show test readouts", small);
                return;
            }
            Line("<b>COMBAT & CONTROLS</b>  ·  temporary readouts, F1 hides", small);
            if (!controls)
            {
                Line("No PlayerControls in the scene.");
                return;
            }
            DrawControls();
            DrawMovement();
            DrawShotgun();
            DrawHazard();
        }

        // ---- Sections -------------------------------------------------------------------

        void DrawControls()
        {
            cursorY += 6f;
            Line("<b>Controls</b>  " + (controls.UsingGamepad ? "Gamepad" : "Keyboard and mouse"));
            if (!controls.GameplayEnabled) Line("<color=#ffb347>Gameplay input switched off by a menu</color>");
            else if (!controls.CursorCaptured && !controls.UsingGamepad) Line("<color=#ffb347>Cursor free: click the game to play</color>");
            else Line("Cursor captured: Esc frees it", small);

            Vector2 m = controls.Move, l = controls.Look;
            float rowTop = cursorY;
            Line($"Move  {m.x:+0.00;-0.00}  {m.y:+0.00;-0.00}");
            Line($"Look  {l.x:+0.0;-0.0}°  {l.y:+0.0;-0.0}°");
            DrawStick(new Rect(Left + 250f, rowTop, 48f, 48f), m);

            cursorY += 4f;
            float x = Left;
            Chip(ref x, "JUMP", controls.JumpHeld);
            Chip(ref x, "DASH", dashFlash > 0f);
            Chip(ref x, "FIRE", controls.FireHeld);
            Chip(ref x, "AIM", controls.AimHeld);
            Chip(ref x, "VENT", ventFlash > 0f);
            Chip(ref x, "MELEE", meleeFlash > 0f);
            Chip(ref x, "HEAVY", controls.HeavyMeleeCharging || heavyFlash > 0f);
            cursorY += 30f;
        }

        void DrawMovement()
        {
            if (!motor) return;
            cursorY += 6f;
            string state = motor.IsDashing ? "<color=#31c8ff>DASHING</color>" : motor.IsGrounded ? "on the ground" : "in the air";
            Line($"<b>Movement</b>  {motor.Speed:0.0} m/s  ·  {state}  ·  air jumps left {motor.AirJumpsLeft}");
            bool ready = motor.DashReadiness >= 1f;
            Meter("Dash", motor.DashReadiness, ready ? ChipOn : ChipOff, ready ? "ready" : "cooling");
        }

        void DrawShotgun()
        {
            if (!shotgun) return;
            cursorY += 6f;
            string state = shotgun.IsOverheated ? $"<color=#ff5a3c>OVERHEATED {shotgun.OverheatRemaining:0.0} s</color>"
                         : shotgun.IsVenting ? "<color=#ffb347>venting</color>"
                         : shotgun.CanFire ? "ready" : "lever cycling";
            string aim = shotgun.IsAiming ? "  ·  <color=#31c8ff>aiming</color>" : "";
            Line($"<b>Energy Blast shotgun</b>  {shotgun.Bolts} bolts  ·  {shotgun.Spread:0.0}° spread  ·  heat {shotgun.Heat01 * 100f:0}%  ·  {state}{aim}");
            ShotReport shot = shotgun.LastShot;
            if (shot.Time > 0f)
            {
                if (shot.BoltsOnTarget > 0)
                    Line($"Last blast: {shot.BoltsOnTarget} of {shot.Bolts} bolts hit {shot.Target}  ·  {shot.Damage:0} damage at {shot.Distance:0.0} m");
                else Line("Last blast: nothing that takes damage was hit", small);
            }
            if (lookingAtDistance >= 0f)
                Line($"Looking at {lookingAt}  ·  {lookingAtDistance:0.0} m  ·  one bolt does {shotgun.DamageAt(lookingAtDistance):0.0} there", small);
        }

        void DrawHazard()
        {
            if (PulsatingEggs.All.Count == 0) return;
            PulsatingEggs nearest = null;
            float best = float.MaxValue;
            Vector3 from = controls.transform.position;
            foreach (PulsatingEggs eggs in PulsatingEggs.All)
            {
                float d = Vector3.Distance(from, eggs.transform.position);
                if (d < best) { best = d; nearest = eggs; }
            }
            cursorY += 6f;
            Line($"<b>Hazard</b>  nearest eggs {best:0.0} m  ·  {Describe(nearest)}  ·  tiny Scuttlers out {TinyScuttler.Active.Count}");
        }

        static string Describe(PulsatingEggs eggs)
        {
            switch (eggs.State)
            {
                case EggState.Swelling: return "<color=#ff5a3c>SWELLING</color>";
                case EggState.Spent: return "erupted";
                default: return eggs.Disturbance > 0.01f ? $"<color=#ffb347>waking {eggs.Disturbance * 100f:0}%</color>" : "dormant";
            }
        }

        /// <summary>A label and wake-up bar floating over each cluster of eggs in view, out to 45 m.</summary>
        void DrawEggMarkers(float scale)
        {
            if (!view || !showPanel) return;
            foreach (PulsatingEggs eggs in PulsatingEggs.All)
            {
                Vector3 screen = view.WorldToScreenPoint(eggs.transform.position + Vector3.up * 1.7f);
                if (screen.z <= 0f || screen.z > 45f) continue;
                var p = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
                content.text = Describe(eggs);
                Vector2 size = small.CalcSize(content);
                Fill(new Rect(p.x - size.x / 2f - 5f, p.y - 22f, size.x + 10f, 34f), PanelColor);
                GUI.Label(new Rect(p.x - size.x / 2f, p.y - 22f, size.x + 4f, 22f), content, small);
                if (eggs.State != EggState.Spent)
                {
                    var bar = new Rect(p.x - 30f, p.y + 2f, 60f, 5f);
                    Fill(bar, ChipOff);
                    Fill(new Rect(bar.x, bar.y, bar.width * eggs.Disturbance, bar.height), Color.Lerp(new Color(0.75f, 0.45f, 1f), new Color(1f, 0.3f, 0.2f), eggs.Disturbance));
                }
            }
        }

        /// <summary>A red wash over the screen for a moment after each hit.</summary>
        void DrawHurt(float width, float height)
        {
            float t = (Time.time - health.LastDamageTime) / 0.35f;
            if (t < 0f || t >= 1f) return;
            Fill(new Rect(0f, 0f, width, height), new Color(0.75f, 0.04f, 0.04f, 0.28f * (1f - t)));
        }

        // ---- Drawing helpers --------------------------------------------------------------

        void MakeStyles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = false };
            text.normal.textColor = Color.white;
            small = new GUIStyle(text) { fontSize = 15 };
            small.normal.textColor = new Color(0.82f, 0.85f, 0.9f);
            chipText = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        }

        /// <summary>One line of text on its own dark strip, then moves down.</summary>
        void Line(string s, GUIStyle style = null)
        {
            style = style ?? text;
            content.text = s;
            Vector2 size = style.CalcSize(content);
            Fill(new Rect(Left - 6f, cursorY - 1f, size.x + 12f, size.y + 2f), PanelColor);
            GUI.Label(new Rect(Left, cursorY, size.x + 4f, size.y), content, style);
            cursorY += size.y + 3f;
        }

        void Chip(ref float x, string label, bool on)
        {
            content.text = label;
            float w = chipText.CalcSize(content).x + 14f;
            var r = new Rect(x, cursorY, w, 24f);
            Fill(r, on ? ChipOn : ChipOff);
            chipText.normal.textColor = on ? new Color(0.02f, 0.08f, 0.12f) : new Color(0.75f, 0.78f, 0.84f);
            GUI.Label(r, content, chipText);
            x += w + 5f;
        }

        /// <summary>A labelled bar from 0 to 1 with a note after it.</summary>
        void Meter(string label, float fill, Color color, string note)
        {
            const float labelWidth = 74f, barWidth = 180f;
            Fill(new Rect(Left - 6f, cursorY - 1f, labelWidth + barWidth + 110f, 24f), PanelColor);
            content.text = label;
            GUI.Label(new Rect(Left, cursorY, labelWidth, 22f), content, small);
            var bar = new Rect(Left + labelWidth, cursorY + 5f, barWidth, 12f);
            Fill(bar, ChipOff);
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height), color);
            content.text = note;
            GUI.Label(new Rect(bar.xMax + 8f, cursorY, 110f, 22f), content, small);
            cursorY += 27f;
        }

        void DrawStick(Rect r, Vector2 v)
        {
            Fill(r, PanelColor);
            Fill(new Rect(r.center.x - 0.5f, r.y + 4f, 1f, r.height - 8f), ChipOff);
            Fill(new Rect(r.x + 4f, r.center.y - 0.5f, r.width - 8f, 1f), ChipOff);
            Vector2 p = r.center + new Vector2(v.x, -v.y) * (r.width * 0.5f - 6f);
            Fill(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), ChipOn);
        }

        static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
