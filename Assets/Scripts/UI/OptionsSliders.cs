using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The options every menu shares, each a slider: MUSIC and SOUND volume, look SENSITIVITY and MAX FPS. The
    /// pause menu and the title screen's options both show these same four, built here from UiKit's parts.
    ///
    /// Drag a slider, click anywhere along it, or select it and press left and right.
    ///  - Music and Sound move 5% a step, from 0% to 100%.
    ///  - Sensitivity goes from 0% to 100%, a percent a step. 50%, the middle, is the speed the game was tuned
    ///    at; 100% is twice that, and 0% a tenth of it, so the view never stops turning altogether.
    ///  - Max FPS goes from 10 to 250 in tens, and one step past 250 is UNLIMITED: no limit at all.
    /// Every change applies at once, ticks so you hear the new sound volume, and is kept between sessions
    /// (GameAudio keeps the volumes, GameOptions the rest).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionsSliders : MonoBehaviour
    {
        /// <summary>How many sliders there are, for a menu laying itself out around them.</summary>
        public const int Count = 4;

        const int VolumeSteps = 20;            // 5% each
        const int SensitivityPercent = 100;    // the slider's top; its middle is the tuned speed
        const int FpsSteps = GameOptions.MaxFpsLimit / GameOptions.FpsStep;   // 25 steps of 10, up to 250...
        const int FpsUnlimited = FpsSteps + 1;                                // ...and one more for no limit

        Slider music, sound, sensitivity, maxFps;
        Text musicValue, soundValue, sensitivityValue, maxFpsValue;
        int shownMusic = -1, shownSound = -1, shownSensitivity = -1, shownMaxFps = -1;

        /// <summary>The top slider, for a menu to put its highlight on.</summary>
        public Selectable First => music;

        /// <summary>
        /// Builds the sliders under <paramref name="parent"/>: the first centered on <paramref name="first"/>
        /// (measured from the parent's middle), the others below it, <paramref name="pitch"/> apart.
        /// </summary>
        public static OptionsSliders Build(Transform parent, Vector2 first, Vector2 size, float pitch, Color accent)
        {
            var middle = new Vector2(0.5f, 0.5f);
            RectTransform holder = UiKit.Area("Options", parent, middle, middle, Vector2.zero, Vector2.zero);
            var options = holder.gameObject.AddComponent<OptionsSliders>();
            Vector2 step = Vector2.down * pitch;
            options.music = UiKit.NewSlider("Music", holder, first, size, "MUSIC", accent, 0, VolumeSteps, out options.musicValue);
            options.sound = UiKit.NewSlider("Sound", holder, first + step, size, "SOUND", accent, 0, VolumeSteps, out options.soundValue);
            options.sensitivity = UiKit.NewSlider("Sensitivity", holder, first + step * 2f, size, "SENSITIVITY", accent,
                                                  0, SensitivityPercent, out options.sensitivityValue);
            options.maxFps = UiKit.NewSlider("Max FPS", holder, first + step * 3f, size, "MAX FPS", accent,
                                             GameOptions.MinFps / GameOptions.FpsStep, FpsUnlimited, out options.maxFpsValue);
            options.Refresh();
            options.music.onValueChanged.AddListener(options.OnMusic);
            options.sound.onValueChanged.AddListener(options.OnSound);
            options.sensitivity.onValueChanged.AddListener(options.OnSensitivity);
            options.maxFps.onValueChanged.AddListener(options.OnMaxFps);
            return options;
        }

        void OnEnable()
        {
            if (music) Refresh();   // another menu may have changed them since this one was last open
        }

        /// <summary>Puts every slider where its setting is now, without counting that as a change.</summary>
        public void Refresh()
        {
            music.SetValueWithoutNotify(Mathf.RoundToInt(GameAudio.MusicVolume * VolumeSteps));
            sound.SetValueWithoutNotify(Mathf.RoundToInt(GameAudio.SfxVolume * VolumeSteps));
            sensitivity.SetValueWithoutNotify(PercentOf(GameOptions.LookSensitivity));
            int fps = GameOptions.MaxFps;
            maxFps.SetValueWithoutNotify(fps > 0 ? fps / GameOptions.FpsStep : FpsUnlimited);
            Show();
        }

        /// <summary>
        /// The sensitivity a percent stands for. The slider's middle is 1, the tuned speed; below it runs down to
        /// the slowest allowed at 0%, and above it up to the fastest at the top.
        /// </summary>
        static float SensitivityOf(int percent)
        {
            float t = percent / (SensitivityPercent * 0.5f);   // 0 at the bottom, 1 in the middle, 2 at the top
            return t <= 1f ? Mathf.Lerp(GameOptions.MinSensitivity, 1f, t) : Mathf.Lerp(1f, GameOptions.MaxSensitivity, t - 1f);
        }

        /// <summary>The percent that stands for a sensitivity: <see cref="SensitivityOf"/> the other way round.</summary>
        static int PercentOf(float sensitivity)
        {
            float t = sensitivity <= 1f ? Mathf.InverseLerp(GameOptions.MinSensitivity, 1f, sensitivity)
                                        : 1f + Mathf.InverseLerp(1f, GameOptions.MaxSensitivity, sensitivity);
            return Mathf.Clamp(Mathf.RoundToInt(t * SensitivityPercent * 0.5f), 0, SensitivityPercent);
        }

        void OnMusic(float value)
        {
            GameAudio.MusicVolume = value / VolumeSteps;
            Changed();
        }

        void OnSound(float value)
        {
            GameAudio.SfxVolume = value / VolumeSteps;
            Changed();
        }

        void OnSensitivity(float value)
        {
            GameOptions.LookSensitivity = SensitivityOf(Mathf.RoundToInt(value));
            Changed();
        }

        void OnMaxFps(float value)
        {
            int step = Mathf.RoundToInt(value);
            GameOptions.MaxFps = step >= FpsUnlimited ? 0 : step * GameOptions.FpsStep;
            Changed();
        }

        void Changed()
        {
            Show();
            Sfx.Play(Sound.UiMove);   // a tick for every step, at the new sound volume
        }

        /// <summary>Writes each value beside its slider. Text is only rebuilt for a value that changed.</summary>
        void Show()
        {
            int m = Mathf.RoundToInt(music.value), s = Mathf.RoundToInt(sound.value);
            int look = Mathf.RoundToInt(sensitivity.value), fps = Mathf.RoundToInt(maxFps.value);
            if (m != shownMusic)
            {
                shownMusic = m;
                musicValue.text = (m * 100 / VolumeSteps) + "%";
            }
            if (s != shownSound)
            {
                shownSound = s;
                soundValue.text = (s * 100 / VolumeSteps) + "%";
            }
            if (look != shownSensitivity)
            {
                shownSensitivity = look;
                sensitivityValue.text = look + "%";
            }
            if (fps != shownMaxFps)
            {
                shownMaxFps = fps;
                maxFpsValue.text = fps >= FpsUnlimited ? "UNLIMITED" : (fps * GameOptions.FpsStep).ToString();
            }
        }
    }
}
