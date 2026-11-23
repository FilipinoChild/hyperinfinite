using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// The player's own settings that aren't sound, kept between sessions: how fast the view turns, and the most
    /// frames drawn each second. Menus set them (OptionsSliders draws the sliders). PlayerControls reads the
    /// sensitivity every frame, so a change applies at once; the frame limit is handed to Unity when it's set and
    /// when the game starts. Music and sound volume live with the audio, in GameAudio.
    /// </summary>
    public static class GameOptions
    {
        const string SensitivityKey = "HyperInfinite.LookSensitivity", MaxFpsKey = "HyperInfinite.MaxFps";

        /// <summary>The slowest and fastest the look sensitivity can be set.</summary>
        public const float MinSensitivity = 0.1f, MaxSensitivity = 2f;

        /// <summary>The lowest and highest frame limit, and the steps between. Past the highest is no limit.</summary>
        public const int MinFps = 10, MaxFpsLimit = 250, FpsStep = 10;

        /// <summary>The frame limit until the player picks their own.</summary>
        public const int DefaultMaxFps = 240;

        static float sensitivity = -1f;   // not read yet
        static int maxFps = -1;           // not read yet

        /// <summary>
        /// Multiplies how fast the view turns, with the mouse and the stick alike: 1 is the speed the game was
        /// tuned at, 0.5 half of it, 2 twice. Kept between sessions.
        /// </summary>
        public static float LookSensitivity
        {
            get
            {
                if (sensitivity < 0f) sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
                return sensitivity;
            }
            set
            {
                sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            }
        }

        /// <summary>
        /// The most frames drawn each second, from 10 to 250 in tens; 0 is no limit. It starts at 240. Kept
        /// between sessions. Unity only keeps to it while VSync is off, as it is in the PC quality level: with
        /// VSync on, the screen's refresh rate decides.
        /// </summary>
        public static int MaxFps
        {
            get
            {
                if (maxFps < 0) maxFps = Tidy(PlayerPrefs.GetInt(MaxFpsKey, DefaultMaxFps));
                return maxFps;
            }
            set
            {
                maxFps = Tidy(value);
                PlayerPrefs.SetInt(MaxFpsKey, maxFps);
                ApplyMaxFps();
            }
        }

        /// <summary>0 for no limit; anything else rounded to a step between the lowest and highest limit.</summary>
        static int Tidy(int fps)
        {
            if (fps <= 0 || fps > MaxFpsLimit) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(fps / (float)FpsStep) * FpsStep, MinFps, MaxFpsLimit);
        }

        /// <summary>Hands the frame limit to Unity: when it changes, and once as the game starts.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyMaxFps()
        {
            Application.targetFrameRate = MaxFps > 0 ? MaxFps : -1;   // -1 is Unity's "as fast as it can"
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // With domain reload off, read them again on the next Play
            sensitivity = -1f;
            maxFps = -1;
        }
    }
}
