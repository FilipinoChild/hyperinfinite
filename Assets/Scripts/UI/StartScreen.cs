using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The screen you see before a level starts: the controls, and CLICK TO START. The game is frozen
    /// behind it, so no wave arrives and nothing can hurt you until you're ready. Clicking (or Enter,
    /// Space, or a gamepad's A or Start) closes it, captures the mouse and starts the level. That click
    /// never fires the shotgun: PlayerControls ignores Fire until the button has been let go.
    ///
    /// Put it on the Player, next to PlayerControls; Hyper Infinite > Build Test Level does. It makes its
    /// own canvas from UiKit's parts, drawn over everything else, and throws it away once play begins.
    /// The keys listed are the ones in Assets/Settings/HyperInfiniteControls; if those bindings change,
    /// change the list in Rows to match.
    /// </summary>
    [DisallowMultipleComponent]
    public class StartScreen : MonoBehaviour
    {
        [Tooltip("Show the controls and wait for a click when the level loads. Switch it off to drop straight into play while testing.")]
        [SerializeField] bool showOnStart = true;

        [Tooltip("The level has started: the screen was clicked away.")]
        public UnityEvent onBegin = new UnityEvent();

        // Key, what it does, and a note for the ones that need it.
        static readonly string[,] Rows =
        {
            { "W A S D", "MOVE", "" },
            { "MOUSE", "LOOK", "" },
            { "SPACE", "JUMP", "AGAIN IN THE AIR TO DOUBLE JUMP" },
            { "LEFT SHIFT", "DASH", "A BURST OF SPEED. RECHARGES IN 1 S" },
            { "LEFT MOUSE", "FIRE", "HOLD TO KEEP FIRING. BUILDS HEAT" },
            { "RIGHT MOUSE", "AIM", "HOLD TO TIGHTEN THE SPREAD" },
            { "R", "VENT HEAT", "DUMPS ALL HEAT. NOTHING ELSE COOLS IT" },
            { "ESC", "PAUSE", "" },
        };

        const float IgnoreInputFor = 0.3f;   // seconds, so a click left over from the title screen can't skip it

        static readonly Color DimColor = new Color(0.03f, 0.01f, 0.05f, 0.8f);

        PlayerControls controls;
        Canvas canvas;
        Text prompt;
        float shownAt, timeScaleBefore = 1f;
        bool promptBright;

        /// <summary>True until the player clicks to start.</summary>
        public bool IsShowing { get; private set; }

        void Awake()
        {
            controls = GetComponent<PlayerControls>();
        }

        void Start()
        {
            if (showOnStart) Show();
        }

        /// <summary>Freezes the game and puts the controls up.</summary>
        public void Show()
        {
            if (IsShowing) return;
            IsShowing = true;
            shownAt = Time.unscaledTime;
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (controls) controls.Block(this);
            Build();
        }

        /// <summary>Closes the screen and starts the level.</summary>
        public void Begin()
        {
            if (!IsShowing) return;
            IsShowing = false;
            Time.timeScale = timeScaleBefore;
            if (canvas) Destroy(canvas.gameObject);
            if (controls) controls.Unblock(this);   // play begins with the mouse captured
            onBegin.Invoke();
        }

        void Update()
        {
            if (!IsShowing) return;
            bool bright = Mathf.Repeat(Time.unscaledTime * 1.5f, 1f) < 0.6f;   // the prompt blinks
            if (bright != promptBright)
            {
                promptBright = bright;
                prompt.color = bright ? Palette.Cyan : Palette.White;
            }
            if (Time.unscaledTime - shownAt >= IgnoreInputFor && StartPressed()) Begin();
        }

        static bool StartPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                                     || keyboard.spaceKey.wasPressedThisFrame))
                return true;
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        void OnDestroy()
        {
            if (IsShowing) Time.timeScale = timeScaleBefore;   // never leave the next scene frozen
            if (canvas) Destroy(canvas.gameObject);
        }

        void Build()
        {
            canvas = UiKit.NewCanvas("Start Screen (screen)", transform, 30);   // over the HUD and the pause menu
            var middle = new Vector2(0.5f, 0.5f);
            var left = new Vector2(0f, 0.5f);
            RectTransform cover = UiKit.Cover("Start Screen", canvas.transform);
            var dim = cover.gameObject.AddComponent<Image>();
            dim.color = DimColor;
            dim.raycastTarget = true;   // clicks don't reach anything behind it

            RectTransform panel = UiKit.Panel("Panel", cover, middle, middle, Vector2.zero, new Vector2(1020f, 690f));
            UiKit.Label("Title", panel, middle, middle, new Vector2(0f, 279f), new Vector2(900f, 72f), "CONTROLS", 48, TextAnchor.MiddleCenter, Palette.Cyan);

            // One row per control: the key on a keycap, what it does, and a note.
            int rows = Rows.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                float y = 195f - i * 51f;
                Image keycap = UiKit.Box("Key " + Rows[i, 0], panel, middle, middle, new Vector2(-330f, y), new Vector2(240f, 39f), Palette.Track);
                UiKit.Border(keycap.transform, Palette.Dim);
                UiKit.Label("Key", keycap.transform, middle, middle, Vector2.zero, new Vector2(240f, 39f), Rows[i, 0], 24, TextAnchor.MiddleCenter, Palette.White);
                UiKit.Label("Action", panel, middle, left, new Vector2(-186f, y), new Vector2(210f, 39f), Rows[i, 1], 24, TextAnchor.MiddleLeft, Palette.White);
                if (Rows[i, 2].Length > 0)
                    UiKit.Label("Note", panel, middle, left, new Vector2(30f, y), new Vector2(450f, 39f), Rows[i, 2], 16, TextAnchor.MiddleLeft, Palette.Dim);
            }

            prompt = UiKit.Label("Prompt", panel, middle, middle, new Vector2(0f, -246f), new Vector2(900f, 48f), "CLICK TO START", 32,
                                 TextAnchor.MiddleCenter, Palette.Cyan);
            promptBright = true;
            UiKit.Label("Hint", panel, middle, middle, new Vector2(0f, -294f), new Vector2(900f, 30f), "OR PRESS ENTER", 16, TextAnchor.MiddleCenter, Palette.Dim);
        }
    }
}
