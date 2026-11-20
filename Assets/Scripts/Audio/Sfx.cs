using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// Plays the game's sound effects, the way PixelFx plays its particles: call the static methods from
    /// anywhere, and one pool of audio sources made at start-up does the rest.
    ///
    /// Each sound has its settings in SoundBank: how loud it is, how much its pitch varies, how many can play
    /// at once and how soon it can repeat. That keeps a crowd of Scuttlers dying at once from stacking into a
    /// wall of noise. When every source is busy, the least important sound makes way. Sounds from the world
    /// fade out with distance and stop being heard past their range; sounds that come from you, and the
    /// menus, are the same in both ears.
    ///
    /// Clips come from Resources/Audio/Sfx and are all loaded at start-up, so nothing loads mid-fight.
    /// Nothing is allocated while playing. GameAudio makes the pool.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Sources = 32;

        struct Voice
        {
            public AudioSource source;
            public Sound sound;
            public Transform follow;
            public float startedAt, baseVolume;
            public int loopId, priority;
            public bool following;
        }

        static Sfx instance;
        static AnimationCurve rolloff;

        readonly Voice[] voices = new Voice[Sources];
        AudioClip[] clips;
        float[] lastPlayed;
        int nextLoopId = 1;

        public static bool Ready => instance;

        /// <summary>A sound from you or the menus, the same in both ears.</summary>
        public static void Play(Sound sound, float volume = 1f, float pitch = 1f, float delay = 0f)
        {
            if (instance) instance.Begin(sound, false, Vector3.zero, null, volume, pitch, delay, false);
        }

        /// <summary>A sound from a point in the world. Sounds set up as 2D play the same in both ears wherever they are.</summary>
        public static void At(Sound sound, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (instance) instance.Begin(sound, true, position, null, volume, pitch, 0f, false);
        }

        /// <summary>
        /// Starts a sound looping, following <paramref name="follow"/> if it's from the world. Returns a number to
        /// change or stop it with; 0 if it couldn't start. Loops hold their source until stopped.
        /// </summary>
        public static int Loop(Sound sound, Transform follow = null, float volume = 1f, float pitch = 1f)
        {
            return instance ? instance.Begin(sound, follow != null, follow ? follow.position : Vector3.zero, follow, volume, pitch, 0f, true) : 0;
        }

        /// <summary>Changes a loop's volume and pitch while it plays.</summary>
        public static void SetLoop(int loop, float volume, float pitch)
        {
            if (!instance || loop == 0) return;
            int i = instance.Find(loop);
            if (i < 0) return;
            ref Voice v = ref instance.voices[i];
            v.source.volume = v.baseVolume * Mathf.Clamp01(volume) * GameAudio.SfxVolume;
            v.source.pitch = pitch;
        }

        /// <summary>Stops a loop. Safe to call with 0, or for one that has already stopped.</summary>
        public static void Stop(int loop)
        {
            if (!instance || loop == 0) return;
            int i = instance.Find(loop);
            if (i < 0) return;
            instance.voices[i].source.Stop();
            instance.voices[i].loopId = 0;
            instance.voices[i].follow = null;
            instance.voices[i].following = false;
        }

        /// <summary>Called when the sound volume changes, so loops already playing follow it.</summary>
        public static void VolumeChanged(float before, float after)
        {
            if (!instance || before <= 0f) return;
            for (int i = 0; i < Sources; i++)
            {
                ref Voice v = ref instance.voices[i];
                if (v.loopId != 0) v.source.volume = v.source.volume / before * after;
            }
        }

        // ---- The pool ---------------------------------------------------------------------------

        void Awake()
        {
            instance = this;
            if (rolloff == null)   // full volume close by, falling away, silent at the sound's range
                rolloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 0.9f), new Keyframe(0.35f, 0.42f),
                                             new Keyframe(0.65f, 0.14f), new Keyframe(1f, 0f));
            for (int i = 0; i < Sources; i++)
            {
                var go = new GameObject("Voice " + (i + 1));
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                source.spread = 0f;
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
                voices[i] = new Voice { source = source, sound = Sound.None };
            }

            int count = SoundBank.Count;
            clips = new AudioClip[count];
            lastPlayed = new float[count];
            for (int i = 1; i < count; i++)
            {
                lastPlayed[i] = -100f;
                string file = SoundBank.Get((Sound)i).file;
                if (string.IsNullOrEmpty(file)) continue;
                clips[i] = Resources.Load<AudioClip>("Audio/Sfx/" + file);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;   // with domain reload off, nothing from the last Play carries over
        }

        /// <summary>
        /// Loops that follow something, such as a HYPER core's hum, keep up with it, and stop by themselves if it's
        /// destroyed without stopping them, so a source is never held for good.
        /// </summary>
        void LateUpdate()
        {
            for (int i = 0; i < Sources; i++)
            {
                ref Voice v = ref voices[i];
                if (v.loopId == 0 || !v.following) continue;
                if (!v.follow)
                {
                    v.source.Stop();
                    v.loopId = 0;
                    v.following = false;
                    continue;
                }
                v.source.transform.position = v.follow.position;
            }
        }

        int Begin(Sound sound, bool inWorld, Vector3 position, Transform follow, float volume, float pitch, float delay, bool loop)
        {
            int index = (int)sound;
            if (index <= 0 || index >= clips.Length) return 0;
            AudioClip clip = clips[index];
            if (!clip) return 0;   // not made yet, or missing: silence rather than an error
            SoundSpec spec = SoundBank.Get(sound);
            float now = Time.unscaledTime;
            if (!loop && now - lastPlayed[index] < spec.gap) return 0;

            int slot = Pick(sound, spec, now);
            if (slot < 0) return 0;
            lastPlayed[index] = now;

            ref Voice v = ref voices[slot];
            AudioSource s = v.source;
            s.Stop();
            bool spatial = inWorld && spec.range > 0f;
            s.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                s.maxDistance = spec.range;
                s.minDistance = spec.range * 0.1f;
                s.transform.position = position;
            }
            float jitter = spec.jitter > 0f ? 1f + Random.Range(-spec.jitter, spec.jitter) : 1f;
            v.sound = sound;
            v.baseVolume = spec.volume;
            v.priority = spec.priority;
            v.startedAt = now;
            v.follow = loop ? follow : null;
            v.following = loop && follow;
            v.loopId = loop ? nextLoopId++ : 0;
            s.clip = clip;
            s.loop = loop;
            s.volume = spec.volume * Mathf.Clamp01(volume) * GameAudio.SfxVolume;
            s.pitch = pitch * jitter;
            s.priority = spec.priority;
            s.ignoreListenerPause = spec.ui;
            if (delay > 0f) s.PlayDelayed(delay);
            else s.Play();
            return v.loopId;
        }

        /// <summary>
        /// The source to play on: the oldest of this sound if it already has all its voices, else a free one,
        /// else the busy one doing the least important job. Loops are never taken.
        /// </summary>
        int Pick(Sound sound, SoundSpec spec, float now)
        {
            int same = 0, oldestSame = -1, free = -1, weakest = -1;
            float oldestTime = float.PositiveInfinity;
            int weakestPriority = -1;
            float weakestTime = float.PositiveInfinity;
            for (int i = 0; i < Sources; i++)
            {
                ref Voice v = ref voices[i];
                bool busy = v.loopId != 0 || v.source.isPlaying;
                if (!busy)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (v.loopId != 0) continue;
                if (v.sound == sound)
                {
                    same++;
                    if (v.startedAt < oldestTime) { oldestTime = v.startedAt; oldestSame = i; }
                }
                if (v.priority > weakestPriority || (v.priority == weakestPriority && v.startedAt < weakestTime))
                {
                    weakestPriority = v.priority;
                    weakestTime = v.startedAt;
                    weakest = i;
                }
            }
            if (same >= spec.voices && oldestSame >= 0) return oldestSame;
            if (free >= 0) return free;
            return weakest >= 0 && weakestPriority >= spec.priority ? weakest : -1;
        }

        int Find(int loop)
        {
            for (int i = 0; i < Sources; i++)
                if (voices[i].loopId == loop) return i;
            return -1;
        }
    }
}
