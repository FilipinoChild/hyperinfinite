using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// What the HUD shows for HYPER, built from UiKit's parts like the rest of the HUD.
    ///  - A HYPER panel slides in above the HP panel: its name in cyan, the seconds left, and a cyan bar that
    ///    empties as time runs out (full means the 25-second cap). Each kill that adds time flashes the bar white
    ///    and pops a "+1 S" beside the seconds. In the last 3 seconds the panel blinks, then it slides away.
    ///  - HYPER! pops up in big letters over the middle of the screen as it starts.
    ///  - The screen's edges glow cyan: a bright flash as it starts, a smaller one for each extension, and a
    ///    faint pulse the rest of the time, faster in the last seconds.
    ///
    /// PlayerHud makes one and ticks it. It sits on a canvas of its own, so it never redraws the rest of the HUD,
    /// and while HYPER is off a tick is one check.
    /// </summary>
    public class HyperHud
    {
        const float Width = 432f, Height = 72f, BarWidth = 396f, Margin = 36f, Above = 162f, Chunk = 12f;
        const float BannerTime = 1f, WarnTime = 3f, BonusTime = 0.8f;

        static readonly Vector2 TopLeft = new Vector2(0f, 1f), TopRight = new Vector2(1f, 1f), Middle = new Vector2(0.5f, 0.5f);

        readonly HyperPower hyper;
        readonly GameObject root;
        readonly RectTransform panel;
        readonly Image fill;
        readonly Text title, seconds, bonus, banner;
        readonly Image[] edges = new Image[4];

        bool wasActive;
        float slide, startedAt = -100f, seenExtend = -100f;
        int shownSeconds = -1;
        float shownX = float.NaN;

        public HyperHud(Transform canvas, HyperPower hyper)
        {
            this.hyper = hyper;
            RectTransform cover = UiKit.Cover("HYPER", canvas);
            root = cover.gameObject;
            root.AddComponent<Canvas>();   // a canvas of its own, so the ticking bar only redraws itself

            // The edges, one chunk thick, all the way round.
            edges[0] = Edge(cover, new Vector2(0f, 1f), new Vector2(1f, 1f), true);
            edges[1] = Edge(cover, new Vector2(0f, 0f), new Vector2(1f, 0f), true);
            edges[2] = Edge(cover, new Vector2(0f, 0f), new Vector2(0f, 1f), false);
            edges[3] = Edge(cover, new Vector2(1f, 0f), new Vector2(1f, 1f), false);

            panel = UiKit.Panel("HYPER Panel", cover, Vector2.zero, Vector2.zero, new Vector2(Margin, Above), new Vector2(Width, Height));
            title = UiKit.Label("Name", panel, TopLeft, TopLeft, new Vector2(18f, -9f), new Vector2(150f, 30f), "HYPER", 24, TextAnchor.MiddleLeft, Palette.Cyan);
            bonus = UiKit.Label("Bonus", panel, TopLeft, TopLeft, new Vector2(150f, -12f), new Vector2(120f, 24f), "", 16, TextAnchor.MiddleLeft, Palette.PaleCyan);
            seconds = UiKit.Label("Seconds", panel, TopRight, TopRight, new Vector2(-18f, -9f), new Vector2(120f, 30f), "", 24, TextAnchor.MiddleRight, Palette.White);
            RectTransform track = UiKit.Track("Bar", panel, TopLeft, TopLeft, new Vector2(18f, -48f), new Vector2(BarWidth, 12f));
            fill = UiKit.Fill("Time", track, Palette.Cyan);
            UiKit.Border(track, Palette.Ink);

            banner = UiKit.Label("Banner", cover, Middle, Middle, new Vector2(0f, 170f), new Vector2(600f, 72f), "HYPER!", 48, TextAnchor.MiddleCenter, Palette.Cyan);
            banner.enabled = false;
            root.SetActive(false);
        }

        static Image Edge(RectTransform cover, Vector2 anchorMin, Vector2 anchorMax, bool horizontal)
        {
            Image side = UiKit.Box("Edge", cover, anchorMin, anchorMin, Vector2.zero, Vector2.zero, Palette.Cyan);
            RectTransform rect = side.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.sizeDelta = horizontal ? new Vector2(0f, Chunk) : new Vector2(Chunk, 0f);
            return side;
        }

        public void Tick(float dt)
        {
            if (!hyper) return;
            bool on = hyper.IsActive;
            if (!on && !root.activeSelf) return;   // nothing to show: one check a frame
            float now = Time.time;
            if (on && !wasActive)
            {
                startedAt = now;
                root.SetActive(true);
            }
            wasActive = on;

            // Slides in from the left a pixel step at a time, and back out when it's over.
            slide = Mathf.MoveTowards(slide, on ? 1f : 0f, dt * 5f);
            if (!on && slide <= 0f)
            {
                root.SetActive(false);
                banner.enabled = false;
                return;
            }
            float x = Margin - (Width + Margin) * Mathf.Round((1f - slide) * 12f) / 12f;
            if (x != shownX)
            {
                shownX = x;
                panel.anchoredPosition = new Vector2(x, Above);
            }

            float left = hyper.Remaining;
            bool warning = on && left < WarnTime;
            bool blink = Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;
            bool extended = now - hyper.LastExtendTime < 0.12f;
            UiKit.SetFill(fill, hyper.Remaining01, BarWidth);
            fill.color = extended || (warning && blink) ? Palette.White : Palette.Cyan;
            title.color = warning && blink ? Palette.White : Palette.Cyan;

            int whole = Mathf.CeilToInt(left);
            if (whole != shownSeconds)
            {
                shownSeconds = whole;
                seconds.text = whole + " S";
            }

            // "+1 S" pops up beside the seconds for each kill that adds time.
            if (hyper.LastExtendTime > seenExtend)
            {
                seenExtend = hyper.LastExtendTime;
                bonus.text = "+" + Mathf.RoundToInt(hyper.LastExtendAmount) + " S";
            }
            float sinceBonus = now - seenExtend;
            bonus.enabled = sinceBonus < BonusTime;
            if (bonus.enabled) bonus.rectTransform.localScale = Vector3.one * (sinceBonus < 0.1f ? 1.3f : 1f);

            // HYPER! over the middle as it starts: pops big, settles, blinks white and cyan.
            float sinceStart = now - startedAt;
            banner.enabled = on && sinceStart < BannerTime;
            if (banner.enabled)
            {
                banner.rectTransform.localScale = Vector3.one * (sinceStart < 0.08f ? 1.5f : sinceStart < 0.16f ? 1.2f : 1f);
                banner.color = blink ? Palette.White : Palette.Cyan;
            }

            // The edges: a bright flash to start, a smaller one per extension, otherwise a faint pulse. All in steps.
            float glow;
            if (sinceStart < 0.12f) glow = 0.5f;
            else if (sinceStart < 0.24f) glow = 0.32f;
            else if (sinceStart < 0.36f) glow = 0.16f;
            else if (extended) glow = 0.24f;
            else glow = Mathf.Repeat(now * (warning ? 4f : 1.5f), 1f) < 0.5f ? 0.1f : 0.06f;
            if (!on) glow = 0f;
            Color edge = Palette.Cyan;
            edge.a = glow;
            for (int i = 0; i < edges.Length; i++) edges[i].color = edge;
        }
    }
}
