using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperInfinite
{
    /// <summary>
    /// The game's sound: sets up the sound effects (Sfx), plays the music, and keeps both volumes.
    ///
    /// It makes itself when the game starts and lasts across scenes, so no scene needs anything added for it,
    /// and the title music carries on into the arena until the first wave.
    ///
    /// The music follows the game:
    ///  - The title theme on the title screen, and in the arena until the controls screen is closed.
    ///  - The arena theme through the waves, with a sting as each wave arrives and when they're all cleared.
    ///  - The boss theme once the Hive Mother is in the fight, starting with a sting of its own, and a fanfare
    ///    when she's beaten, after which the title theme comes back, quietly.
    ///  - Under HYPER, a second layer comes in over whichever theme is playing, in time with it: a racing
    ///    arpeggio and hats. It fades out when HYPER ends.
    ///  - Paused, the music drops and goes muffled; while you're down, more so.
    /// Each theme loops seamlessly and streams from disk, so it costs little memory. Changing theme crossfades.
    ///
    /// Music and sound volumes are kept between sessions; the pause menu changes them.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const string MusicKey = "HyperInfinite.MusicVolume", SfxKey = "HyperInfinite.SoundVolume";
        const float FadeTime = 1.5f, LayerFadeTime = 0.35f;

        enum Track { None, Title, Arena, Boss }

        /// <summary>One theme: its main loop and its HYPER layer, playing in step, each with a muffling filter.</summary>
        class Deck
        {
            public AudioSource main, layer;
            public AudioLowPassFilter mainFilter, layerFilter;
            public Track track;
            public float level, target;
        }

        static GameAudio instance;
        static float musicVolume = -1f, sfxVolume = -1f;

        readonly Deck[] decks = new Deck[2];
        AudioSource sting;
        int current;
        float layerBlend, duck = 1f, cutoff = 22000f, stingUntil, nextSearch, clearAt = -1f, calmUntil = -1f;

        // What's in the scene.
        TitleMenu titleMenu;
        ScuttlerSpawner spawner;
        StartScreen startScreen;
        PauseMenu pauseMenu;
        Health playerHealth;
        HyperPower hyper;
        int lastWave;
        bool clearPlayed, bossSeen, bossBeaten;

        /// <summary>Music volume, 0 to 1, kept between sessions.</summary>
        public static float MusicVolume
        {
            get
            {
                if (musicVolume < 0f) musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.8f));
                return musicVolume;
            }
            set
            {
                musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, musicVolume);
            }
        }

        /// <summary>Sound effects volume, 0 to 1, kept between sessions.</summary>
        public static float SfxVolume
        {
            get
            {
                if (sfxVolume < 0f) sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
                return sfxVolume;
            }
            set
            {
                float before = SfxVolume;
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxKey, sfxVolume);
                Sfx.VolumeChanged(before, sfxVolume);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Game Audio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameAudio>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            musicVolume = sfxVolume = -1f;   // read again from what was saved
        }

        void Awake()
        {
            var effects = new GameObject("Sound Effects");
            effects.transform.SetParent(transform, false);
            effects.AddComponent<Sfx>();

            for (int i = 0; i < 2; i++)
            {
                var deck = new Deck();
                deck.main = MusicSource("Music " + (i + 1), out deck.mainFilter);
                deck.layer = MusicSource("Music " + (i + 1) + " HYPER Layer", out deck.layerFilter);
                decks[i] = deck;
            }
            sting = MusicSource("Stings", out _);
            sting.loop = false;

            SceneManager.sceneLoaded += OnSceneLoaded;
            Look(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        AudioSource MusicSource(string name, out AudioLowPassFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;                  // music never makes way
            source.ignoreListenerPause = true;    // it plays on, muffled, while paused
            filter = go.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            return source;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Look(scene);
        }

        /// <summary>A new scene: forget the last one's state, give the Player its sounds, and see what's here.</summary>
        void Look(Scene scene)
        {
            titleMenu = null; spawner = null; startScreen = null; pauseMenu = null; playerHealth = null; hyper = null;
            lastWave = 0;
            clearPlayed = bossSeen = bossBeaten = false;
            clearAt = calmUntil = -1f;
            nextSearch = 0f;
            Search();
        }

        /// <summary>Finds what the music listens to. Repeated each second for anything not found, as some turn up later.</summary>
        void Search()
        {
            nextSearch = Time.unscaledTime + 1f;
            if (!titleMenu) titleMenu = FindAnyObjectByType<TitleMenu>();
            if (!spawner) spawner = FindAnyObjectByType<ScuttlerSpawner>();
            if (!startScreen) startScreen = FindAnyObjectByType<StartScreen>();
            if (!pauseMenu) pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (!playerHealth)
            {
                var controls = FindAnyObjectByType<PlayerControls>();
                if (controls)
                {
                    playerHealth = controls.GetComponent<Health>();
                    hyper = controls.GetComponent<HyperPower>();
                }
            }
        }

        // ---- The music --------------------------------------------------------------------------

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (now >= nextSearch) Search();
            Stings(now);
            Play(Wanted(now));

            // Muffled while paused, more so while down; a little lower under a sting.
            bool paused = pauseMenu && pauseMenu.IsPaused;
            bool down = playerHealth && playerHealth.IsDead;
            float wantDuck = (down ? 0.5f : paused ? 0.45f : 1f) * (now < stingUntil ? 0.55f : 1f);
            float wantCutoff = down ? 700f : paused ? 1200f : 22000f;
            duck = Mathf.MoveTowards(duck, wantDuck, dt * 2f);
            cutoff = Mathf.Lerp(cutoff, wantCutoff, 1f - Mathf.Exp(-6f * dt));
            layerBlend = Mathf.MoveTowards(layerBlend, hyper && hyper.IsActive ? 1f : 0f, dt / LayerFadeTime);

            float volume = MusicVolume;
            for (int i = 0; i < 2; i++)
            {
                Deck d = decks[i];
                if (d.track == Track.None) continue;
                d.level = Mathf.MoveTowards(d.level, d.target, dt / FadeTime);
                if (d.level <= 0f && d.target <= 0f)
                {
                    d.main.Stop(); d.layer.Stop();
                    d.main.clip = d.layer.clip = null;   // lets the streamed clip go
                    d.track = Track.None;
                    continue;
                }
                float gain = volume * d.level * duck * TrackGain(d.track);
                d.main.volume = gain;
                d.layer.volume = gain * layerBlend;
                d.mainFilter.cutoffFrequency = d.layerFilter.cutoffFrequency = cutoff;
            }
            sting.volume = volume * 0.9f;
        }

        /// <summary>Which theme the game calls for right now.</summary>
        Track Wanted(float now)
        {
            if (now < calmUntil) return Track.None;   // the fanfare plays alone
            if (bossBeaten) return Track.Title;
            if (titleMenu) return Track.Title;
            Boss boss = Boss.Current;
            if (boss && boss.Health && !boss.Health.IsDead) return Track.Boss;
            if (startScreen && startScreen.IsShowing) return Track.Title;
            if (playerHealth) return Track.Arena;
            return Track.None;
        }

        /// <summary>The waves arriving, being cleared, the Hive Mother joining in and being beaten.</summary>
        void Stings(float now)
        {
            Boss boss = Boss.Current;
            bool bossHere = boss && boss.Health && !boss.Health.IsDead;
            if (spawner && spawner.isActiveAndEnabled)
            {
                int wave = spawner.Wave;
                if (wave > lastWave && wave > 0 && !bossHere) PlaySting("sting_wave");
                lastWave = wave;
                if (spawner.AllCleared && !clearPlayed)
                {
                    clearPlayed = true;
                    clearAt = now + 0.35f;   // held a moment: if the Hive Mother wakes now, her sting plays instead
                }
            }
            if (bossHere && !bossSeen)
            {
                bossSeen = true;
                clearAt = -1f;
                PlaySting("sting_boss");
            }
            if (clearAt > 0f && now >= clearAt)
            {
                clearAt = -1f;
                PlaySting("sting_clear");
            }
            if (bossSeen && !bossHere && !bossBeaten)
            {
                bossBeaten = true;
                float length = PlaySting("sting_victory");
                calmUntil = now + Mathf.Max(0f, length - 1f);
            }
        }

        float PlaySting(string file)
        {
            var clip = Resources.Load<AudioClip>("Audio/Music/" + file);
            if (!clip) return 0f;
            sting.clip = clip;
            sting.Play();
            stingUntil = Time.unscaledTime + clip.length * 0.8f;
            return clip.length;
        }

        /// <summary>Crossfades to <paramref name="track"/> if it isn't already the one playing.</summary>
        void Play(Track track)
        {
            Deck live = decks[current];
            if (live.track == track && live.target > 0f) return;
            if (track == Track.None)
            {
                live.target = 0f;
                return;
            }
            // Fade out what's playing and start the new theme on the other deck, main and layer in step.
            live.target = 0f;
            current = 1 - current;
            Deck next = decks[current];
            if (next.track == track)   // it was fading out: bring it back
            {
                next.target = 1f;
                return;
            }
            AudioClip main = Resources.Load<AudioClip>("Audio/Music/" + File(track));
            if (!main)
            {
                next.track = Track.None;
                return;
            }
            AudioClip layer = Resources.Load<AudioClip>("Audio/Music/" + File(track) + "_hyper");
            next.main.Stop(); next.layer.Stop();
            next.main.clip = main;
            next.layer.clip = layer;
            next.track = track;
            next.level = 0f;
            next.target = 1f;
            next.main.volume = next.layer.volume = 0f;
            double at = AudioSettings.dspTime + 0.25;   // a moment for both streams to buffer, so they start together
            next.main.PlayScheduled(at);
            if (layer) next.layer.PlayScheduled(at);
        }

        static string File(Track track)
        {
            switch (track)
            {
                case Track.Title: return "music_title";
                case Track.Arena: return "music_arena";
                case Track.Boss: return "music_boss";
                default: return "";
            }
        }

        static float TrackGain(Track track) => track == Track.Title ? 0.85f : 0.8f;
    }
}
