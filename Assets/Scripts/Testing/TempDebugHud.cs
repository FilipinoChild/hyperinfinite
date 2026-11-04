using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperInfinite
{
    /// <summary>
    /// TEMPORARY on-screen readouts for testing combat and movement;
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
        LeverShotgun shotgun;
        Camera view;
        string lookingAt = "";
        float lookingAtDistance = -1f;
        bool showPanel = true;
        float findTimer;
        float dashFlash, ventFlash, meleeFlash, interactFlash;

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
            dashFlash -= dt; ventFlash -= dt; meleeFlash -= dt; interactFlash -= dt;
            if (controls)
            {
                if (controls.DashPressed) dashFlash = 0.2f;
                if (controls.VentPressed) ventFlash = 0.2f;
                if (controls.MeleePressed) meleeFlash = 0.2f;
                if (controls.InteractPressed) interactFlash = 0.2f;
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
            if (!controls) controls = FindAnyObjectByType<PlayerControls>();
            if (!motor) motor = FindAnyObjectByType<PlayerMotor>();
            if (!shotgun) shotgun = FindAnyObjectByType<LeverShotgun>();
            if (!view && controls) view = controls.GetComponentInChildren<Camera>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            MakeStyles();
            float scale = Screen.height / 1080f;          // lay out on a 1080-pixel-tall canvas at any resolution
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = 1080f;

            var center = new Vector2(width * 0.5f, height * 0.5f);
            DrawCrosshair(center, shotgun ? Mathf.Max(6f, AngleToPixels(shotgun.Spread)) : 6f);
            if (shotgun && shotgun.LastShot.PelletsOnTarget > 0 && Time.time - shotgun.LastShot.Time < 0.18f) DrawHitMarker(center);
            if (shotgun) DrawHeatGauge(new Vector2(width * 0.5f, height - 96f));

            cursorY = 14f;
            if (!showPanel)
            {
                Line("F1: show test readouts", small);
                return;
            }
            Line("<b>COMBAT & MOVEMENT TEST</b>  ·  temporary readouts, F1 hides", small);
            if (!controls)
            {
                Line("No PlayerControls in the scene.");
                return;
            }
            DrawControls();
            DrawMovement();
            DrawShotgun();
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
            Chip(ref x, "USE", interactFlash > 0f);
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
            Line($"<b>Shotgun</b>  {shotgun.Pellets} pellets  ·  {shotgun.Spread:0.0}° spread  ·  heat {shotgun.Heat01 * 100f:0}%  ·  {state}{aim}");
            ShotReport shot = shotgun.LastShot;
            if (shot.Time > 0f)
            {
                if (shot.PelletsOnTarget > 0)
                    Line($"Last blast: {shot.PelletsOnTarget} of {shot.Pellets} pellets hit {shot.Target}  ·  {shot.Damage:0} damage at {shot.Distance:0.0} m");
                else Line("Last blast: nothing that takes damage was hit", small);
            }
            if (lookingAtDistance >= 0f)
                Line($"Looking at {lookingAt}  ·  {lookingAtDistance:0.0} m  ·  one pellet does {shotgun.DamageAt(lookingAtDistance):0.0} there", small);
        }

        /// <summary>Heat under the crosshair, where a player would glance at it: amber as it builds, red when locked.</summary>
        void DrawHeatGauge(Vector2 bottomCenter)
        {
            const float w = 300f, h = 14f;
            var bar = new Rect(bottomCenter.x - w / 2f, bottomCenter.y, w, h);
            Fill(new Rect(bar.x - 3f, bar.y - 3f, bar.width + 6f, bar.height + 6f), PanelColor);
            Fill(bar, ChipOff);
            float heat = shotgun.Heat01;
            Color color = shotgun.IsOverheated ? new Color(1f, 0.28f, 0.2f)
                         : Color.Lerp(new Color(0.95f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.15f), heat);
            Fill(new Rect(bar.x, bar.y, bar.width * heat, bar.height), color);
            for (int i = 1; i < 5; i++) Fill(new Rect(bar.x + bar.width * 0.2f * i - 1f, bar.y, 2f, bar.height), PanelColor);
            string label = shotgun.IsOverheated ? "OVERHEATED" : shotgun.IsVenting ? "VENTING" : "HEAT";
            content.text = label;
            var labelRect = new Rect(bar.x, bar.y - 24f, bar.width, 22f);
            chipText.normal.textColor = shotgun.IsOverheated ? new Color(1f, 0.4f, 0.3f) : new Color(0.85f, 0.88f, 0.92f);
            GUI.Label(labelRect, content, chipText);
        }

        /// <summary>Converts an angle off the center of the view into pixels on the 1080-tall canvas.</summary>
        float AngleToPixels(float degrees)
        {
            if (!view) return 0f;
            return Mathf.Tan(degrees * Mathf.Deg2Rad) / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 540f;
        }

        /// <summary>An X of dots around the crosshair when pellets land on something that takes damage.</summary>
        static void DrawHitMarker(Vector2 c)
        {
            Color ink = new Color(1f, 0.32f, 0.25f, 0.95f);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sy = i < 2 ? 1f : -1f;
                for (float d = 9f; d <= 17f; d += 4f) Fill(new Rect(c.x + sx * d - 2f, c.y + sy * d - 2f, 4f, 4f), ink);
            }
        }

        /// <summary>Four ticks; the gap shows the pellet cone, so it widens and tightens with the spread.</summary>
        void DrawCrosshair(Vector2 c, float gap)
        {
            const float length = 9f, thick = 2f;
            Color ink = new Color(1f, 1f, 1f, 0.9f), edge = new Color(0f, 0f, 0f, 0.6f);
            for (int pass = 0; pass < 2; pass++)
            {
                float grow = pass == 0 ? 1f : 0f;
                Color col = pass == 0 ? edge : ink;
                Fill(new Rect(c.x - thick / 2f - grow, c.y - gap - length - grow, thick + 2f * grow, length + 2f * grow), col);
                Fill(new Rect(c.x - thick / 2f - grow, c.y + gap - grow, thick + 2f * grow, length + 2f * grow), col);
                Fill(new Rect(c.x - gap - length - grow, c.y - thick / 2f - grow, length + 2f * grow, thick + 2f * grow), col);
                Fill(new Rect(c.x + gap - grow, c.y - thick / 2f - grow, length + 2f * grow, thick + 2f * grow), col);
            }
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
