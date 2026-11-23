using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The building blocks every screen in the game is made from (the HUD, the death screen, the pause
    /// menu and the controls screen), so they all look alike: the pixel font, dark plates with a
    /// one-pixel border, bars that fill a pixel at a time, and buttons that light up cyan when selected.
    /// Sizes are in units of a 1920 x 1080 canvas, which scales to any screen.
    /// </summary>
    public static class UiKit
    {
        /// <summary>One pixel of the UI's art, in canvas units. Sizes and positions look best as multiples of it.</summary>
        public const float Pixel = PixelArt.Pixel;

        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        static readonly Color ButtonPlate = new Color(0.055f, 0.031f, 0.067f, 0.96f);
        static readonly Color ButtonEdge = new Color(0.36f, 0.33f, 0.4f);
        static Font font;

        /// <summary>
        /// The game's pixel font, Silkscreen (Assets/Resources/Fonts). If it's missing, Unity's built-in
        /// font stands in, so text still shows.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font) return font;
                font = Resources.Load<Font>("Fonts/Silkscreen-Regular");
                if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>A screen-space canvas laid out for 1920 x 1080 and scaled with the screen's height. Higher sorting orders draw on top.</summary>
        public static Canvas NewCanvas(string name, Transform parent, int sortingOrder)
        {
            var canvasObject = new GameObject(name);
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Buttons need an EventSystem to be clicked or picked with the keyboard. This makes one if the scene has none.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>()) return;
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();   // finds its default actions (mouse, Enter, gamepad) by itself
        }

        /// <summary>An empty rectangle: <paramref name="anchor"/> is where on its parent it hangs, <paramref name="pivot"/> which point of it hangs there.</summary>
        public static RectTransform Area(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>An empty rectangle that always covers its whole parent.</summary>
        public static RectTransform Cover(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A flat-colored rectangle.</summary>
        public static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            return Paint(Area(name, parent, anchor, pivot, position, size), null, color);
        }

        /// <summary>A pixel-art image from PixelArt, tinted. Its size is up to the caller.</summary>
        public static Image Picture(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            return Paint(Area(name, parent, anchor, pivot, position, size), sprite, color);
        }

        /// <summary>A one-pixel border just inside the edge of <paramref name="parent"/>.</summary>
        public static Image Border(Transform parent, Color color)
        {
            Image border = Paint(Cover("Border", parent), PixelArt.Outline, color);
            border.type = Image.Type.Sliced;
            border.fillCenter = false;
            return border;
        }

        /// <summary>The dark plate HUD readouts sit on, with its border.</summary>
        public static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            Image plate = Box(name, parent, anchor, pivot, position, size, Palette.Panel);
            Border(plate.transform, Palette.Track);
            return plate.rectTransform;
        }

        /// <summary>The empty track of a bar. Add fills to it with Fill, bottom one first.</summary>
        public static RectTransform Track(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            return Box(name, parent, anchor, pivot, position, size, Palette.Track).rectTransform;
        }

        /// <summary>A bar's fill: it stretches across its track from the left, as far as SetFill says.</summary>
        public static Image Fill(string name, RectTransform track, Color color)
        {
            return Paint(Cover(name, track), null, color);
        }

        /// <summary>
        /// Fills a bar to <paramref name="fraction"/> of its track, a whole pixel at a time so it moves in
        /// steps like the rest of the art. Does nothing if that's where it already is.
        /// </summary>
        public static void SetFill(Image fill, float fraction, float trackWidth)
        {
            float steps = Mathf.Max(1f, trackWidth / Pixel);
            float snapped = Mathf.Round(Mathf.Clamp01(fraction) * steps) / steps;
            RectTransform rect = fill.rectTransform;
            if (Mathf.Approximately(rect.anchorMax.x, snapped)) return;
            rect.anchorMax = new Vector2(snapped, 1f);
        }

        /// <summary>Text in the pixel font, with a hard one-pixel shadow so it reads over anything.</summary>
        public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
                                 string text, int fontSize, TextAnchor alignment, Color color)
        {
            RectTransform rect = Area(name, parent, anchor, pivot, position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(Pixel, -Pixel);
            return label;
        }

        /// <summary>
        /// A menu button, centered on <paramref name="position"/> in its parent: a dark plate whose border
        /// turns <paramref name="accent"/> and whose marker appears while it's selected. Pointing at it selects
        /// it, so the mouse, the keyboard and a gamepad all move the same highlight.
        /// </summary>
        public static Button NewButton(string name, Transform parent, Vector2 position, Vector2 size, string text, Color accent, UnityAction onClick)
        {
            Image plate = Box(name, parent, Middle, Middle, position, size, ButtonPlate);
            plate.raycastTarget = true;
            Image border = Border(plate.transform, Color.white);
            Image marker = Picture("Marker", plate.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f * Pixel, 0f),
                                   new Vector2(5f, 7f) * Pixel, PixelArt.Chevron, accent);
            Text label = Label("Label", plate.transform, Middle, Middle, Vector2.zero, size, text, 32, TextAnchor.MiddleCenter, Palette.Dim);

            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = border;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonEdge;
            colors.highlightedColor = accent;
            colors.selectedColor = accent;
            colors.pressedColor = Color.white;
            colors.disabledColor = new Color(ButtonEdge.r, ButtonEdge.g, ButtonEdge.b, 0.5f);
            colors.fadeDuration = 0f;   // snaps, like the rest of the art
            button.colors = colors;
            button.onClick.AddListener(onClick);
            plate.gameObject.AddComponent<MenuButton>().SetUp(marker, label, Palette.Dim, Palette.White);
            return button;
        }

        /// <summary>
        /// A slider in the menus' style, centered on <paramref name="position"/> in its parent: the same dark
        /// plate and border as a button, with its name on the left, its value on the right and a chunky track
        /// underneath. Drag it, click anywhere along it, or select it and press left and right. It moves in whole
        /// steps from <paramref name="min"/> to <paramref name="max"/>. The caller listens to its onValueChanged
        /// and writes the value into <paramref name="valueLabel"/>.
        /// </summary>
        public static Slider NewSlider(string name, Transform parent, Vector2 position, Vector2 size, string text, Color accent,
                                       int min, int max, out Text valueLabel)
        {
            const float Pad = 8f * Pixel, HandleWidth = 4f * Pixel, TrackHeight = 4f * Pixel;
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var bottom = new Vector2(0.5f, 0f);

            Image plate = Box(name, parent, Middle, Middle, position, size, ButtonPlate);
            plate.raycastTarget = true;   // the whole plate takes the drag
            Image border = Border(plate.transform, Color.white);
            Image marker = Picture("Marker", plate.transform, topLeft, topLeft, new Vector2(Pad, -4f * Pixel),
                                   new Vector2(5f, 7f) * Pixel, PixelArt.Chevron, accent);
            Text label = Label("Label", plate.transform, topLeft, topLeft, new Vector2(Pad + 7f * Pixel, -3f * Pixel),
                               new Vector2(size.x * 0.6f, 9f * Pixel), text, 24, TextAnchor.MiddleLeft, Palette.Dim);
            valueLabel = Label("Value", plate.transform, topRight, topRight, new Vector2(-Pad, -3f * Pixel),
                               new Vector2(size.x * 0.3f, 9f * Pixel), "", 24, TextAnchor.MiddleRight, Palette.White);

            // The track, with the fill and the handle in an area half a handle narrower each side, so the handle
            // stays on the track at both ends. The fill reaches back to the track's start.
            RectTransform track = Track("Track", plate.transform, bottom, bottom, new Vector2(0f, 5f * Pixel),
                                        new Vector2(size.x - 2f * Pad, TrackHeight));
            RectTransform area = Cover("Slide Area", track);
            area.offsetMin = new Vector2(HandleWidth * 0.5f, 0f);
            area.offsetMax = new Vector2(-HandleWidth * 0.5f, 0f);
            Image fill = Paint(Cover("Fill", area), null, accent);
            fill.rectTransform.offsetMin = new Vector2(-HandleWidth * 0.5f, 0f);
            Image handle = Paint(Cover("Handle", area), null, Palette.White);
            handle.rectTransform.sizeDelta = new Vector2(HandleWidth, 4f * Pixel);   // 2 pixels taller than the track, above and below

            var slider = plate.gameObject.AddComponent<Slider>();
            slider.targetGraphic = border;
            ColorBlock colors = slider.colors;
            colors.normalColor = ButtonEdge;
            colors.highlightedColor = accent;
            colors.selectedColor = accent;
            colors.pressedColor = Color.white;
            colors.disabledColor = new Color(ButtonEdge.r, ButtonEdge.g, ButtonEdge.b, 0.5f);
            colors.fadeDuration = 0f;   // snaps, like the rest of the art
            slider.colors = colors;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.wholeNumbers = true;
            slider.minValue = min;
            slider.maxValue = max;

            var highlight = plate.gameObject.AddComponent<MenuButton>();
            highlight.SetUp(marker, label, Palette.Dim, Palette.White);
            highlight.PressSound = false;   // a slider ticks as it moves instead
            return slider;
        }

        /// <summary>Puts the menu highlight on <paramref name="button"/>, for the keyboard and gamepad to start from.</summary>
        public static void Select(Selectable button)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(button ? button.gameObject : null);
        }

        static Image Paint(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
