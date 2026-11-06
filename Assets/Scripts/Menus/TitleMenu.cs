using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperInfinite
{
    /// Runs the title screen: shows the main menu or the level select,
    /// and loads the level when the player picks how to start.
    public class TitleMenu : MonoBehaviour
    {
        [Tooltip("The exact name of the level's scene file.")]
        [SerializeField] string levelScene = "Aucturn_Arena";
        [Tooltip("The PLAY and QUIT buttons.")]
        [SerializeField] GameObject mainMenu;
        [Tooltip("The level select panel.")]
        [SerializeField] GameObject levelSelect;

        void Start()
        {
            // The level locks the mouse for aiming, so free it for the menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowMainMenu();
        }

        public void ShowMainMenu()
        {
            mainMenu.SetActive(true);
            levelSelect.SetActive(false);
        }

        public void OpenLevelSelect()
        {
            mainMenu.SetActive(false);
            levelSelect.SetActive(true);
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