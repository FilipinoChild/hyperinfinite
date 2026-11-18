using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The HUD's damage indicators. A hit lights a ragged pixel border around the screen, brighter for
    /// a harder hit, and an arrow near the crosshair points at whatever did it; the arrow keeps pointing
    /// at the attacker as you turn. At low health the border keeps blinking. After a respawn the same
    /// border glows cyan for as long as the protection lasts, blinking as it runs out.
    ///
    /// Like the rest of the art nothing fades smoothly: the border steps through a few brightness
    /// levels and the arrows turn in sixteenths of a circle. PlayerHud makes one and ticks it.
    /// </summary>
    public class HurtOverlay
    {
        const int ArrowCount = 4;
        const float ArrowLife = 1.2f, ArrowRadius = 174f, DirectionStep = 22.5f;
        const float Chunk = 9f;          // one pixel of the border art, in canvas units
        const float CornerSize = 16f * Chunk;
        const int Levels = 4;

        readonly Health health;
        readonly PlayerRespawn respawn;
        readonly Transform view;
        readonly float lowHealth;
        readonly GameObject border;
        readonly Image[] borderParts = new Image[8];
        readonly Image[] arrows = new Image[ArrowCount];
        readonly Transform[] arrowSource = new Transform[ArrowCount];
        readonly Vector3[] arrowFrom = new Vector3[ArrowCount];
        readonly float[] arrowUntil = new float[ArrowCount];
        float hurt;
        int nextArrow, shownLevel = -1;
        bool shownShield;

        public HurtOverlay(Transform canvas, Health health, PlayerRespawn respawn, Transform view, float lowHealth)
        {
            this.health = health;
            this.respawn = respawn;
            this.view = view;
            this.lowHealth = lowHealth;

            RectTransform root = UiKit.Cover("Hurt Border", canvas);
            border = root.gameObject;
            // Four corners, one image flipped three ways, and a one-pixel line along each side between them.
            for (int i = 0; i < 4; i++)
            {
                bool right = i % 2 == 1, bottom = i >= 2;
                var corner = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
                borderParts[i] = UiKit.Picture("Corner", root, corner, new Vector2(0f, 1f), Vector2.zero, Vector2.one * CornerSize,
                                               PixelArt.Corner, Palette.Red);
                borderParts[i].rectTransform.localScale = new Vector3(right ? -1f : 1f, bottom ? -1f : 1f, 1f);
            }
            borderParts[4] = Side(root, new Vector2(0f, 1f), new Vector2(1f, 1f), true);    // top
            borderParts[5] = Side(root, new Vector2(0f, 0f), new Vector2(1f, 0f), true);    // bottom
            borderParts[6] = Side(root, new Vector2(0f, 0f), new Vector2(0f, 1f), false);   // left
            borderParts[7] = Side(root, new Vector2(1f, 0f), new Vector2(1f, 1f), false);   // right
            border.SetActive(false);

            RectTransform center = UiKit.Area("Damage Arrows", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < ArrowCount; i++)
            {
                arrows[i] = UiKit.Picture("Arrow", center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                          new Vector2(9f, 6f) * 6f, PixelArt.Arrow, Palette.Red);
                arrows[i].enabled = false;
                arrowUntil[i] = -1f;
            }
        }

        static Image Side(RectTransform root, Vector2 anchorMin, Vector2 anchorMax, bool horizontal)
        {
            Image side = UiKit.Box("Side", root, anchorMin, anchorMin, Vector2.zero, Vector2.zero, Palette.Red);
            RectTransform rect = side.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            // Stretched along its edge, stopping short of the corners; one chunk thick.
            rect.sizeDelta = horizontal ? new Vector2(-2f * CornerSize, Chunk) : new Vector2(Chunk, -2f * CornerSize);
            return side;
        }

        /// <summary>The player took <paramref name="amount"/> damage: flash the border and point at the attacker.</summary>
        public void Hit(float amount)
        {
            hurt = Mathf.Max(hurt, Mathf.Clamp01(0.4f + amount / 30f));
            GameObject source = health.LastDamage.Source;
            if (!source || source.transform.IsChildOf(health.transform)) return;   // nothing to point at

            // Reuse the arrow that already points at this attacker, or at another one in the same direction,
            // so a swarm biting from one side shows as one arrow instead of four.
            float direction = DirectionTo(source.transform.position);
            int slot = -1;
            for (int i = 0; i < ArrowCount; i++)
            {
                if (arrowUntil[i] <= Time.time) continue;
                if (arrowSource[i] == source.transform || Mathf.Approximately(DirectionTo(arrowFrom[i]), direction)) slot = i;
            }
            if (slot < 0)
            {
                slot = nextArrow;
                nextArrow = (nextArrow + 1) % ArrowCount;
            }
            arrowSource[slot] = source.transform;
            arrowFrom[slot] = source.transform.position;
            arrowUntil[slot] = Time.time + ArrowLife;
        }

        /// <summary>
        /// Which way <paramref name="point"/> lies from where the player is looking, in degrees: 0 ahead, 90 to
        /// the right, 180 behind. Rounded to a sixteenth of a circle, the steps the arrows turn in.
        /// </summary>
        float DirectionTo(Vector3 point)
        {
            Vector3 to = point - view.position, forward = view.forward, right = view.right;
            float angle = Mathf.Atan2(to.x * right.x + to.z * right.z, to.x * forward.x + to.z * forward.z) * Mathf.Rad2Deg;
            return Mathf.Round(angle / DirectionStep) * DirectionStep;
        }

        public void Tick(float dt)
        {
            hurt = Mathf.Max(0f, hurt - dt * 1.6f);
            bool alive = !health.IsDead;

            // The border: red for hurt, cyan for the respawn shield. Hurt wins.
            int level = Mathf.CeilToInt(hurt * Levels);
            bool shield = false;
            float blink = Mathf.Repeat(Time.unscaledTime * 2f, 1f);
            if (alive && health.Fraction <= lowHealth) level = Mathf.Max(level, blink < 0.5f ? 2 : 1);
            if (!alive) level = Mathf.Max(level, 2);
            if (level == 0 && respawn && respawn.Protection01 > 0f)
            {
                shield = true;
                bool runningOut = respawn.Protection01 < 0.34f;
                level = runningOut && Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f ? 1 : 3;
            }
            if (level != shownLevel || shield != shownShield)
            {
                shownLevel = level;
                shownShield = shield;
                border.SetActive(level > 0);
                Color color = shield ? Palette.Cyan : Palette.Red;
                color.a = 0.9f * level / Levels;
                for (int i = 0; i < borderParts.Length; i++) borderParts[i].color = color;
            }

            // The arrows: each sits around the crosshair on the side its attacker is on.
            for (int i = 0; i < ArrowCount; i++)
            {
                float remaining = arrowUntil[i] - Time.time;
                if (remaining <= 0f || !alive)
                {
                    if (arrows[i].enabled) arrows[i].enabled = false;
                    continue;
                }
                if (arrowSource[i]) arrowFrom[i] = arrowSource[i].position;   // follow it while it lives
                bool visible = remaining > 0.4f || (int)(remaining * 12f) % 2 == 0;   // blinks out at the end
                arrows[i].enabled = visible;
                if (!visible) continue;
                float angle = DirectionTo(arrowFrom[i]);
                float radians = angle * Mathf.Deg2Rad;
                RectTransform rect = arrows[i].rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * ArrowRadius;
                rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }
    }
}
