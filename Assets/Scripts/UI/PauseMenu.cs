using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The pause menu. Press Esc (or Start on a gamepad) and the game freezes, sound pauses (the music plays
    /// on, muffled), the cursor is freed and the menu comes up with RESUME, MAIN MENU, MUSIC, SOUND and QUIT
    /// GAME. Music and Sound step the volume down 20% at a time, from 100% round to 0% and back, and are kept
    /// between sessions. Esc again, or Resume, carries
    /// on where you left off. Main Menu goes back to the title screen, and Quit Game closes the game (in
    /// the editor it stops Play mode instead).
    ///
    /// Put it on the Player, next to PlayerControls; Hyper Infinite > Build Test Level does. Like
    /// PlayerHud it makes its own canvas from UiKit's parts, drawn over the HUD, so every scene with the
    /// Player can be paused. Main Menu only shows when the title scene is in the build's scene list.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("The exact name of the title screen's scene file.")]
        [SerializeField] string titleScene = "Title";

        static readonly Color DimColor = new Color(0.05f, 0f, 0.02f, 0.7f);

        PlayerControls controls;
        StartScreen startScreen;
        Canvas canvas;
        GameObject menu;
        Button resumeButton;
        Text musicLabel, soundLabel;
        float timeScaleBefore = 1f;

        /// <summary>True while the game is paused.</summary>
        public bool IsPaused { get; private set; }

        void Awake()
        {
            controls = GetComponent<PlayerControls>();
            startScreen = GetComponent<StartScreen>();
            Build();
        }

        void Start()
        {
            UiKit.EnsureEventSystem();   // the buttons need one to be clicked
        }

        void Update()
        {
            if (!controls || !controls.PausePressed) return;
            if (startScreen && startScreen.IsShowing) return;   // the level hasn't started yet
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Sfx.Play(Sound.PauseOpen);
            menu.SetActive(true);
            if (controls) controls.Block(this);   // no walking or shooting, and the cursor is free for the buttons
            UiKit.Select(resumeButton);            // so Enter and the gamepad work at once
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBefore;
            AudioListener.pause = false;
            Sfx.Play(Sound.PauseClose);
            menu.SetActive(false);
            UiKit.Select(null);
            if (controls) controls.Unblock(this);   // anything else still open, such as the death screen, keeps its own block
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

        void Build()
        {
            canvas = UiKit.NewCanvas("Pause Menu (screen)", transform, 20);   // over the HUD and its death screen
            var middle = new Vector2(0.5f, 0.5f);
            RectTransform cover = UiKit.Cover("Pause Menu", canvas.transform);
            menu = cover.gameObject;
            var dim = menu.AddComponent<Image>();
            dim.color = DimColor;
            dim.raycastTarget = true;   // clicks beside the buttons don't reach the game

            RectTransform panel = UiKit.Panel("Panel", cover, middle, middle, Vector2.zero, new Vector2(480f, 642f));
            UiKit.Label("Title", panel, middle, middle, new Vector2(0f, 255f), new Vector2(420f, 72f), "PAUSED", 48, TextAnchor.MiddleCenter, Palette.Cyan);
            var size = new Vector2(360f, 72f);
            resumeButton = UiKit.NewButton("Resume", panel, new Vector2(0f, 156f), size, "RESUME", Palette.Cyan, Resume);
            Button mainMenu = UiKit.NewButton("Main Menu", panel, new Vector2(0f, 66f), size, "MAIN MENU", Palette.Cyan, GoToMainMenu);
            Button music = UiKit.NewButton("Music", panel, new Vector2(0f, -24f), size, "", Palette.Cyan, StepMusic);
            Button sound = UiKit.NewButton("Sound", panel, new Vector2(0f, -114f), size, "", Palette.Cyan, StepSound);
            UiKit.NewButton("Quit Game", panel, new Vector2(0f, -204f), size, "QUIT GAME", Palette.Red, QuitGame);
            mainMenu.gameObject.SetActive(Application.CanStreamedLevelBeLoaded(titleScene));   // only if the build has it
            UiKit.Label("Hint", panel, middle, middle, new Vector2(0f, -282f), new Vector2(420f, 30f), "ESC TO RESUME", 16, TextAnchor.MiddleCenter, Palette.Dim);
            musicLabel = music.GetComponentInChildren<Text>();
            soundLabel = sound.GetComponentInChildren<Text>();
            ShowVolumes();
            menu.SetActive(false);
        }

        /// <summary>Music volume down a step, from 100% round to 0% and back.</summary>
        public void StepMusic()
        {
            GameAudio.MusicVolume = NextStep(GameAudio.MusicVolume);
            ShowVolumes();
        }

        /// <summary>Sound volume down a step, from 100% round to 0% and back.</summary>
        public void StepSound()
        {
            GameAudio.SfxVolume = NextStep(GameAudio.SfxVolume);
            ShowVolumes();
        }

        static float NextStep(float volume)
        {
            int step = Mathf.RoundToInt(volume * 5f);   // 0 to 5, a fifth each
            return (step <= 0 ? 5 : step - 1) / 5f;
        }

        void ShowVolumes()
        {
            if (musicLabel) musicLabel.text = "MUSIC " + Mathf.RoundToInt(GameAudio.MusicVolume * 100f) + "%";
            if (soundLabel) soundLabel.text = "SOUND " + Mathf.RoundToInt(GameAudio.SfxVolume * 100f) + "%";
        }
    }
}
