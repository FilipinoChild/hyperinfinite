using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// Runs the title screen: shows the main menu, the level select or the options,
    /// and loads the level when the player picks how to start.
    ///
    /// The menus have one select bar, the cyan frame, and it is always on exactly one
    /// button. Pointing at a button with the mouse moves it there, and so do the arrow
    /// keys or a gamepad; Enter or a click presses the button it's on. Each button swaps
    /// between its plain and its framed picture (Textures/UI) as the bar comes and goes.
    ///
    /// OPTIONS opens a panel with the same four sliders the pause menu has: MUSIC, SOUND,
    /// SENSITIVITY and MAX FPS (see OptionsSliders), and BACK. It's built here in code from UiKit's parts,
    /// over the menu's corner of the picture. Esc, or B on a gamepad, also goes back.
    public class TitleMenu : MonoBehaviour
    {
        [Tooltip("The exact name of the level's scene file.")]
        [SerializeField] string levelScene = "Aucturn_Arena";
        [Tooltip("The PLAY and QUIT buttons.")]
        [SerializeField] GameObject mainMenu;
        [Tooltip("The level select panel.")]
        [SerializeField] GameObject levelSelect;

        GameObject lastSelected;   // the button the select bar was last on
        GameObject options;        // the options panel, made in Awake
        OptionsSliders sliders;

        // The picture has PLAY and QUIT painted into it, so the options panel is solid: nothing shows through.
        static readonly Color OptionsPlate = new Color(0.055f, 0.031f, 0.067f, 1f);
        static readonly Color OptionsEdge = new Color(0.36f, 0.33f, 0.4f);
        const float OptionsTop = 0.515f;      // where the panel's top sits, as a share of the screen's height: just over PLAY
        const float OptionsHeight = 530f;

        void Awake()
        {
            // MenuButton makes pointing at a button select it, so the mouse and the keys share one bar
            AddMenuButtons(mainMenu);
            AddMenuButtons(levelSelect);
            BuildOptions();
        }

        void Start()
        {
            // The level locks the mouse for aiming, so free it for the menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowMainMenu();
        }

        void Update()
        {
            // Clicking empty space takes the select bar off every button. Put it back where it was.
            if (options.activeSelf && BackPressed()) ShowMainMenu();

            EventSystem events = EventSystem.current;
            if (!events) return;
            GameObject selected = events.currentSelectedGameObject;
            if (selected && selected.activeInHierarchy) lastSelected = selected;
            else if (lastSelected && lastSelected.activeInHierarchy) events.SetSelectedGameObject(lastSelected);
        }

        public void ShowMainMenu()
        {
            mainMenu.SetActive(true);
            levelSelect.SetActive(false);
            options.SetActive(false);
            SelectFirst(mainMenu);      // the bar starts on PLAY
        }

        /// The OPTIONS button calls this.
        public void OpenOptions()
        {
            mainMenu.SetActive(false);
            levelSelect.SetActive(false);
            // On a wide, short screen the panel would run off the bottom: lift it just enough to fit.
            var panel = (RectTransform)options.transform;
            float room = ((RectTransform)panel.parent).rect.height * OptionsTop - OptionsHeight - 16f;
            panel.anchoredPosition = new Vector2(0f, room < 0f ? -room : 0f);
            options.SetActive(true);
            UiKit.Select(sliders.First);   // the bar starts on MUSIC
            lastSelected = sliders.First.gameObject;
        }

        /// Esc on the keyboard, or B on a gamepad.
        static bool BackPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                   || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        /// Makes the options panel: a solid plate where the menu's buttons sit, wide enough to cover the
        /// PLAY and QUIT painted into the picture, with the four sliders and BACK. Its top is pinned
        /// just above the PLAY button's place, so it lines up with the picture at any screen shape.
        void BuildOptions()
        {
            var middle = new Vector2(0.5f, 0.5f);
            var size = new Vector2(504f, 72f);

            RectTransform panel = UiKit.Area("Options", mainMenu.transform.parent, middle, middle, Vector2.zero, Vector2.zero);
            panel.anchorMin = new Vector2(0.11f, OptionsTop);
            panel.anchorMax = new Vector2(0.41f, OptionsTop);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(0f, OptionsHeight);
            options = panel.gameObject;
            var plate = options.AddComponent<Image>();
            plate.color = OptionsPlate;
            plate.raycastTarget = true;
            UiKit.Border(panel, OptionsEdge);

            UiKit.Label("Title", panel, middle, middle, new Vector2(0f, 219f), new Vector2(480f, 48f), "OPTIONS", 32, TextAnchor.MiddleCenter, Palette.Cyan);
            sliders = OptionsSliders.Build(panel, new Vector2(0f, 141f), size, 86f, Palette.Cyan);
            UiKit.NewButton("Back", panel, new Vector2(0f, -203f), size, "BACK", Palette.Cyan, ShowMainMenu);
            options.SetActive(false);
        }

        public void OpenLevelSelect()
        {
            mainMenu.SetActive(false);
            levelSelect.SetActive(true);
            SelectFirst(levelSelect);   // the bar starts on START
        }

        /// Puts the select bar on the first button in a panel.
        void SelectFirst(GameObject panel)
        {
            Selectable first = panel.GetComponentInChildren<Selectable>();
            if (!first || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(first.gameObject);
            lastSelected = first.gameObject;
        }

        static void AddMenuButtons(GameObject panel)
        {
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
                if (!button.GetComponent<MenuButton>()) button.gameObject.AddComponent<MenuButton>();
        }

        public void StartFromBeginning()
        {
            LevelStart.StartAtBoss = false;
            // The story plays over the title scene; the arena only loads after the last panel.
            if (IntroCutscene.TryPlay(levelScene))
            {
                mainMenu.SetActive(false);
                levelSelect.SetActive(false);
                lastSelected = null;
            }
            else SceneManager.LoadScene(levelScene);
        }

        public void StartAtBoss()
        {
            LevelStart.StartAtBoss = true;
            if (IntroCutscene.TryPlay(levelScene, true))
            {
                mainMenu.SetActive(false);
                levelSelect.SetActive(false);
                lastSelected = null;
            }
            else SceneManager.LoadScene(levelScene);
        }

        public void Quit()
        {
            Application.Quit();   // closes the built game
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // stops Play mode in the editor
#endif
        }
    }
}