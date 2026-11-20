using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// A dialogue-free opening told through three small animated pixel-art panels.
    /// It lives in the title scene, so no arena, player, or wave starts early.
    /// Each texture is a 6 x 8 atlas of 400 x 225 frames, read left-to-right, top-to-bottom.
    /// The final frame holds until the player presses NEXT (START on the last panel).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntroCutscene : MonoBehaviour
    {
        static readonly string[] OpeningPanels = { "Orbit", "Impact", "Hatch" };
        static readonly string[] BossPanels = { "BossWalk", "BossEyes", "BossReady" };
        string[] panels;
        bool startAtBoss;
        const int Columns = 6, Rows = 8, FrameCount = 48, FrameWidth = 400, FrameHeight = 225;
        const float FramesPerSecond = 12f;
        const float ClickCooldown = 0.25f;
        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        string destination;
        RawImage picture;
        Button next;
        Text label;
        Texture2D atlas;
        int panel, lastFrame = -1;
        float startedAt;
        bool loading;

        /// <summary>Return false if the first art asset is unavailable, allowing the normal start as fallback.</summary>
        public static bool TryPlay(string destinationScene, bool bossIntro = false)
        {
            if (FindAnyObjectByType<IntroCutscene>()) return true;
            string[] sequence = bossIntro ? BossPanels : OpeningPanels;
            Texture2D first = LoadPanel(sequence[0]);
            if (!first) return false;
            var root = new GameObject(bossIntro ? "Boss Intro Cutscene" : "Opening Cutscene");
            var intro = root.AddComponent<IntroCutscene>();
            intro.destination = destinationScene;
            intro.panels = sequence;
            intro.startAtBoss = bossIntro;
            intro.Build();
            intro.ShowPanel(0, first);
            return true;
        }

        static Texture2D LoadPanel(string panelName)
        {
            Texture2D texture = Resources.Load<Texture2D>("IntroCutscene/" + panelName);
            if (!texture || texture.width != Columns * FrameWidth || texture.height != Rows * FrameHeight)
            {
                Debug.LogError("Cutscene atlas missing or incorrect size: " + panelName);
                return null;
            }
            return texture;
        }

        void Build()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UiKit.EnsureEventSystem();
            Canvas canvas = UiKit.NewCanvas("Opening Cutscene Canvas", transform, 100);
            // Keep the entire frame and button inside even on a narrow display.
            canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            RectTransform black = UiKit.Cover("Black background", canvas.transform);
            Image cover = black.gameObject.AddComponent<Image>();
            cover.color = Color.black;
            cover.raycastTarget = true;

            // A single rectangular view, surrounded by black, as in a retro story screen.
            Image border = UiKit.Box("Film frame", black, Middle, Middle, new Vector2(0f, 55f),
                                    new Vector2(1160f, 656f), new Color(0.78f, 0.77f, 0.80f));
            RectTransform imageRect = UiKit.Area("Animated panel", border.transform, Middle, Middle,
                                                 Vector2.zero, new Vector2(1152f, 648f));
            picture = imageRect.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;

            Image plate = UiKit.Box("Next", black, Middle, Middle, new Vector2(0f, -330f),
                                   new Vector2(204f, 58f), Color.white);
            plate.raycastTarget = true;
            next = plate.gameObject.AddComponent<Button>();
            next.targetGraphic = plate;
            ColorBlock colors = next.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.80f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.48f, 0.78f, 0.87f);
            colors.fadeDuration = 0.06f;
            next.colors = colors;
            next.onClick.AddListener(Advance);
            RectTransform labelRect = UiKit.Cover("Label", plate.transform);
            label = labelRect.gameObject.AddComponent<Text>();
            label.font = UiKit.Font;
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.black;
            label.raycastTarget = false;
            UiKit.Select(next);
        }

        void ShowPanel(int index, Texture2D texture)
        {
            Texture2D previous = atlas;
            atlas = texture;
            picture.texture = atlas;
            panel = index;
            startedAt = Time.unscaledTime;
            lastFrame = -1;
            label.text = index == panels.Length - 1 ? "START" : "NEXT";
            SetFrame(0);
            if (previous && previous != atlas) Resources.UnloadAsset(previous);
        }

        void Update()
        {
            if (loading || !atlas) return;
            int elapsedFrames = Mathf.Max(0, Mathf.FloorToInt((Time.unscaledTime - startedAt) * FramesPerSecond));
            // The Hive Mother keeps blinking until NEXT; the other shots hold their final pose.
            int frame = startAtBoss && panel == 1 ? elapsedFrames % FrameCount : Mathf.Min(FrameCount - 1, elapsedFrames);
            SetFrame(Mathf.Max(0, frame));
        }

        void SetFrame(int frame)
        {
            if (frame == lastFrame) return;
            lastFrame = frame;
            int column = frame % Columns;
            int row = frame / Columns;
            // Unity's texture origin is bottom-left; the authored atlas starts at the top-left.
            // Half-texel inset prevents sampling neighboring frames at the edge.
            float x = (column * FrameWidth + 0.5f) / atlas.width;
            float y = ((Rows - 1 - row) * FrameHeight + 0.5f) / atlas.height;
            picture.uvRect = new Rect(x, y, (FrameWidth - 1f) / atlas.width, (FrameHeight - 1f) / atlas.height);
        }

        void Advance()
        {
            if (loading || Time.unscaledTime - startedAt < ClickCooldown) return;
            Sfx.Play(Sound.UiSelect);
            int index = panel + 1;
            if (index < panels.Length)
            {
                Texture2D texture = LoadPanel(panels[index]);
                if (texture)
                {
                    ShowPanel(index, texture);
                    return;
                }
            }
            // Also use the existing gameplay path if a later art asset failed to import.
            loading = true;
            next.interactable = false;
            LevelStart.StartAtBoss = startAtBoss;
            SceneManager.LoadScene(destination);
        }
    }
}
