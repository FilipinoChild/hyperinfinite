using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The pause menu. Press Esc and the game freezes, sound pauses, the cursor is
    /// freed and the menu comes up with RESUME, MAIN MENU and QUIT GAME. Esc again, or Resume, carries on
    /// where you left off. Main Menu goes back to the title screen, and Quit Game closes the game (in the
    /// editor it stops Play mode instead).
    ///
    /// Put it on the Player, next to PlayerControls; Hyper Infinite > Build Test Level does. Like
    /// PlayerHud it makes its own screen-space canvas, drawn over the HUD, so every scene with the Player
    /// can be paused. Main Menu only shows when the title scene is in the build's scene list.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("The exact name of the title screen's scene file.")]
        [SerializeField] string titleScene = "Title";

        static readonly Color DimColor = new Color(0.05f, 0f, 0.02f, 0.7f);
        static readonly Color PanelColor = new Color(0.04f, 0.03f, 0.06f, 0.85f);
        static readonly Color AccentColor = new Color(0.19f, 0.78f, 1f);
        static readonly Color ButtonColor = new Color(0.1f, 0.55f, 0.75f);
        static readonly Color QuitColor = new Color(0.7f, 0.16f, 0.15f);

        PlayerControls controls;
        Health health;
        Canvas canvas;
        GameObject menu;
        Button resumeButton;
        Font font;
        bool hadCursor, cursorLastFrame;
        float timeScaleBefore = 1f;

        /// <summary>True while the game is paused.</summary>
        public bool IsPaused { get; private set; }

        void Awake()
        {
            controls = GetComponent<PlayerControls>();
            health = GetComponent<Health>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        void Start()
        {
            // The buttons need an EventSystem to be clicked; PlayerHud adds the same one if it gets there first.
            if (!FindAnyObjectByType<EventSystem>())
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }
        }

        void Update()
        {
            if (controls && controls.PausePressed)
            {
                if (IsPaused) Resume();
                else Pause();
            }
        }

        void LateUpdate()
        {
            // Esc frees the cursor before this script hears it, so remember how it was at the end of each frame.
            if (controls && !IsPaused) cursorLastFrame = controls.CursorCaptured;
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            menu.SetActive(true);
            if (controls)
            {
                hadCursor = cursorLastFrame || controls.CursorCaptured;
                controls.GameplayEnabled = false;
                controls.SetCursorCaptured(false);
            }
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);   // for Enter
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBefore;
            AudioListener.pause = false;
            menu.SetActive(false);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            if (controls)
            {
                bool dead = health && health.IsDead;   // PlayerHud's death screen keeps the controls off
                controls.GameplayEnabled = !dead;
                if (hadCursor && !dead) controls.SetCursorCaptured(true);
            }
        }

        public void GoToMainMenu()
        {
            Unfreeze();
            SceneManager.LoadScene(titleScene);
        }

        public void QuitGame()
        {
            Unfreeze();
            Application.Quit();   // closes the built game
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // stops Play mode in the editor
#endif
        }

        /// <summary>Time and sound back to normal, so whatever comes next isn't frozen.</summary>
        void Unfreeze()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void OnDestroy()
        {
            if (IsPaused) Unfreeze();
            if (canvas) Destroy(canvas.gameObject);
        }

        // ---- The menu -------------------------------------------------------------------------

        void BuildCanvas()
        {
            var canvasObject = new GameObject("Pause Menu (screen)");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;   // over the HUD and its death screen
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform dim = Box("Pause Menu", canvasObject.transform, Vector2.zero, DimColor);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.sizeDelta = Vector2.zero;
            dim.GetComponent<Image>().raycastTarget = true;   // clicks beside the buttons don't reach the game
            menu = dim.gameObject;

            RectTransform panel = Box("Panel", dim, new Vector2(460f, 440f), PanelColor);
            Text title = Label("Title", panel, new Vector2(0f, 160f), new Vector2(420f, 70f), "PAUSED", 56);
            title.color = AccentColor;

            resumeButton = MenuButton("Resume", panel, new Vector2(0f, 60f), "RESUME", ButtonColor, Resume);
            Button mainMenu = MenuButton("Main Menu", panel, new Vector2(0f, -30f), "MAIN MENU", ButtonColor, GoToMainMenu);
            MenuButton("Quit Game", panel, new Vector2(0f, -120f), "QUIT GAME", QuitColor, QuitGame);
            mainMenu.gameObject.SetActive(Application.CanStreamedLevelBeLoaded(titleScene));   // only if the build has it

            Text hint = Label("Hint", panel, new Vector2(0f, -190f), new Vector2(420f, 30f), "Esc to resume", 20);
            hint.fontStyle = FontStyle.Normal;
            hint.color = new Color(1f, 1f, 1f, 0.7f);

            menu.SetActive(false);
        }

        Button MenuButton(string name, Transform parent, Vector2 position, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = Box(name, parent, new Vector2(320f, 70f), Color.white);
            rect.anchoredPosition = position;
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
            colors.selectedColor = Color.Lerp(color, Color.white, 0.3f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.3f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Label("Label", rect, Vector2.zero, new Vector2(320f, 70f), text, 30);
            return button;
        }

        /// <summary>A centered colored box.</summary>
        static RectTransform Box(string name, Transform parent, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>Centered bold text with a drop shadow, like PlayerHud's.</summary>
        Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
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
