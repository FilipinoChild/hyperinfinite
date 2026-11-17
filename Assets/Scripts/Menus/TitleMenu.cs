using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// Runs the title screen: shows the main menu or the level select,
    /// and loads the level when the player picks how to start.
    ///
    /// The menus have one select bar, the cyan frame, and it is always on exactly one
    /// button. Pointing at a button with the mouse moves it there, and so do the arrow
    /// keys or a gamepad; Enter or a click presses the button it's on. Each button swaps
    /// between its plain and its framed picture (Textures/UI) as the bar comes and goes.
    public class TitleMenu : MonoBehaviour
    {
        [Tooltip("The exact name of the level's scene file.")]
        [SerializeField] string levelScene = "Aucturn_Arena";
        [Tooltip("The PLAY and QUIT buttons.")]
        [SerializeField] GameObject mainMenu;
        [Tooltip("The level select panel.")]
        [SerializeField] GameObject levelSelect;

        GameObject lastSelected;   // the button the select bar was last on

        void Awake()
        {
            // MenuButton makes pointing at a button select it, so the mouse and the keys share one bar
            AddMenuButtons(mainMenu);
            AddMenuButtons(levelSelect);
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
            SelectFirst(mainMenu);      // the bar starts on PLAY
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
            SceneManager.LoadScene(levelScene);
        }

        public void StartAtBoss()
        {
            LevelStart.StartAtBoss = true;
            SceneManager.LoadScene(levelScene);
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