using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The boss bar across the top of the screen, built from UiKit's parts like the rest of the HUD.
    ///
    /// When a Boss joins the fight the panel drops in, the boss's name flashes red, and the bar fills up
    /// a pixel at a time while the numbers count up. Each hit flashes the bar white and jolts the panel,
    /// and a pale trail holds where health was for a moment before draining away, as on the player's HP
    /// bar. Marks at every quarter show how far along the fight is. When the boss dies the bar empties,
    /// DEFEATED shows for a few seconds, and the panel lifts away.
    ///
    /// PlayerHud makes one and ticks it. It sits on a canvas of its own inside the HUD's, so the bar
    /// changing through a fight only redraws the bar, not the whole HUD. Text is only rewritten when what
    /// it says changes, and with no boss around a tick is one check.
    /// </summary>
    public class BossBar
    {
        const float PanelWidth = 768f, PanelHeight = 78f, BarWidth = 732f, Margin = 36f;
        const float SlideTime = 0.25f, FillTime = 1.2f, TrailDelay = 0.45f, HitFlash = 0.09f, DefeatedTime = 3f;

        static readonly Vector2 TopLeft = new Vector2(0f, 1f), TopRight = new Vector2(1f, 1f), TopMiddle = new Vector2(0.5f, 1f);
        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        static readonly Color TrailColor = new Color(1f, 0.92f, 0.85f, 0.85f);

        readonly GameObject root;
        readonly RectTransform panel;
        readonly Image fill, trailFill;
        readonly Text title, value;

        Boss boss;
        Health health;
        bool defeated;
        float age, defeatedFor, trail, lastFraction, seenHit, flashUntil, jolt;
        int shownValue = int.MinValue;
        Vector2 shownPosition = new Vector2(float.NaN, float.NaN);

        public BossBar(Transform canvas)
        {
            RectTransform cover = UiKit.Cover("Boss Bar", canvas);
            root = cover.gameObject;
            root.AddComponent<Canvas>();   // a canvas of its own, so its changes don't redraw the rest of the HUD

            panel = UiKit.Panel("Panel", cover, TopMiddle, TopMiddle, new Vector2(0f, -Margin), new Vector2(PanelWidth, PanelHeight));
            // A spider in the Arachturns' red, with their yellow eyes laid on top.
            var markSize = new Vector2(15f, 9f) * UiKit.Pixel;
            UiKit.Picture("Mark", panel, TopLeft, TopLeft, new Vector2(18f, -12f), markSize, PixelArt.BossMark, Palette.Red);
            UiKit.Picture("Eyes", panel, TopLeft, TopLeft, new Vector2(18f, -12f), markSize, PixelArt.BossEyes, Palette.Yellow);
            title = UiKit.Label("Name", panel, TopLeft, TopLeft, new Vector2(78f, -9f), new Vector2(456f, 30f), "", 24, TextAnchor.MiddleLeft, Palette.White);
            value = UiKit.Label("Value", panel, TopRight, TopRight, new Vector2(-18f, -9f), new Vector2(240f, 30f), "", 16, TextAnchor.MiddleRight, Palette.Dim);

            RectTransform track = UiKit.Track("Bar", panel, TopLeft, TopLeft, new Vector2(18f, -48f), new Vector2(BarWidth, 18f));
            trailFill = UiKit.Fill("Hit Trail", track, TrailColor);
            fill = UiKit.Fill("Health", track, Palette.Red);
            for (int i = 1; i < 4; i++)   // a mark at every quarter
                UiKit.Box("Mark", track, new Vector2(0.25f * i, 0.5f), Middle, Vector2.zero, new Vector2(UiKit.Pixel, 18f), Palette.Ink);
            UiKit.Border(track, Palette.Ink);
            root.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (!root.activeSelf)
            {
                Boss next = Boss.Current;
                if (!next) return;
                Begin(next);
            }
            age += dt;

            // Where the fight stands. A boss that's gone with health left was switched off, not beaten.
            bool gone = !boss || !boss.isActiveAndEnabled;
            float fraction = gone ? lastFraction : Mathf.Clamp01(health.Fraction);
            if (gone && fraction > 0f)
            {
                Hide();
                return;
            }
            lastFraction = fraction;
            if (!gone && health.LastDamageTime > seenHit)
            {
                seenHit = health.LastDamageTime;
                flashUntil = Time.time + HitFlash;
                jolt = 0.12f;
            }
            if (!defeated && fraction <= 0f) defeated = true;
            if (defeated)
            {
                defeatedFor += dt;
                if (defeatedFor >= DefeatedTime + SlideTime)
                {
                    Hide();
                    return;
                }
            }

            // Drops in, and once beaten lifts away again, a pixel at a time.
            float slide = defeated ? Mathf.Clamp01((defeatedFor - DefeatedTime) / SlideTime) : 1f - Mathf.Clamp01(age / SlideTime);
            if (jolt > 0f) jolt -= dt;
            float side = jolt <= 0f ? 0f : (int)(jolt * 25f) % 2 == 0 ? UiKit.Pixel : -UiKit.Pixel;
            SetPosition(side, Mathf.Round(slide * (PanelHeight + Margin) / UiKit.Pixel) * UiKit.Pixel);

            // Once it has landed the bar fills up, then follows the boss's health.
            float filling = Mathf.Clamp01((age - SlideTime) / FillTime);
            float shown = Mathf.Min(fraction, filling);
            UiKit.SetFill(fill, shown, BarWidth);
            fill.color = Time.time < flashUntil ? Palette.White : Palette.Red;
            if (shown >= trail || filling < 1f) trail = shown;
            else if (Time.time - seenHit >= TrailDelay) trail = Mathf.MoveTowards(trail, shown, dt * 0.8f);
            UiKit.SetFill(trailFill, trail, BarWidth);

            bool blink = Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f;
            title.color = filling < 1f && blink ? Palette.Red : Palette.White;   // flashes while it arrives
            if (defeated)
            {
                SetValue(-1, "DEFEATED");
                value.color = defeatedFor < 1f && blink ? Palette.White : Palette.Cyan;
            }
            else
            {
                int max = Mathf.CeilToInt(health.Max);
                int now = filling < 1f ? Mathf.CeilToInt(shown * health.Max) : Mathf.CeilToInt(health.Current);   // counts up as it fills
                if (now != shownValue) SetValue(now, now + " / " + max);
            }
        }

        void Begin(Boss next)
        {
            boss = next;
            health = next.Health;
            defeated = false;
            age = defeatedFor = trail = jolt = 0f;
            lastFraction = 1f;
            seenHit = health.LastDamageTime;   // hits from before it showed up don't count
            flashUntil = -1f;
            shownValue = int.MinValue;
            title.text = next.DisplayName;
            value.color = Palette.Dim;
            SetPosition(0f, PanelHeight + Margin);   // starts above the screen
            UiKit.SetFill(fill, 0f, BarWidth);
            UiKit.SetFill(trailFill, 0f, BarWidth);
            root.SetActive(true);
        }

        void Hide()
        {
            root.SetActive(false);
            boss = null;
            health = null;
        }

        void SetPosition(float x, float raised)
        {
            var position = new Vector2(x, -Margin + raised);
            if (position == shownPosition) return;
            shownPosition = position;
            panel.anchoredPosition = position;
        }

        void SetValue(int key, string text)
        {
            if (key == shownValue) return;
            shownValue = key;
            value.text = text;
        }
    }
}
