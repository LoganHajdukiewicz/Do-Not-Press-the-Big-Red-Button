using System.Collections;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Loops a level's music across scene loads. Each day scene carries one of these,
    /// but only the first survives: later days hand their clip to the player that is
    /// already running, so the track keeps playing unbroken from Day 1 to Day 30
    /// instead of restarting every morning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackgroundMusic : MonoBehaviour
    {
        [Header("Track")]
        [Tooltip("Corporate Background Music.mp3 for the working days. A different clip " +
            "replaces the current one. Left empty, this day fades the music out instead.")]
        [SerializeField] private AudioClip music;
        [Tooltip("Volume once the fade-in finishes. Kept low so the buttons stay audible.")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.32f;
        [SerializeField] private bool loop = true;

        [Header("Timing")]
        [Tooltip("Seconds before the music starts. Day 1 waits for the opening narration.")]
        [SerializeField, Min(0f)] private float startDelay;
        [Tooltip("Seconds to fade up when the music first starts.")]
        [SerializeField, Min(0f)] private float fadeInDuration = 2.5f;
        [Tooltip("Seconds to cross-fade when a later day supplies a different clip.")]
        [SerializeField, Min(0f)] private float crossFadeDuration = 1.5f;

        [Header("Behaviour")]
        [Tooltip("Survives day changes so the track does not restart between days. " +
            "Uncheck for music that belongs to one scene only.")]
        [SerializeField] private bool continueAcrossDays = true;
        [Tooltip("Keeps playing while the game is paused.")]
        [SerializeField] private bool ignorePause = true;
        [SerializeField] private bool playOnStart = true;

        /// <summary>The player that survives scene loads, if one is running.</summary>
        public static BackgroundMusic Active { get; private set; }

        private AudioSource source;
        private Coroutine fade;
        private bool isPersistentInstance;
        private bool handedOver;
        /// <summary>The persistent carrier this day's component delegates to.</summary>
        private BackgroundMusic player;

        /// <summary>
        /// Whichever instance actually owns the AudioSource. A day component that
        /// created the carrier points at it; one that handed over to an already-running
        /// carrier finds it through Active. Without this, a day's own Stop/SetVolume
        /// would silently do nothing, because its source is null.
        /// </summary>
        private BackgroundMusic Voice
        {
            get
            {
                if (player != null)
                    return player;
                if (handedOver && Active != null)
                    return Active;
                return this;
            }
        }
        public AudioClip CurrentClip => Voice.source == null ? null : Voice.source.clip;
        public bool IsPlaying => Voice.source != null && Voice.source.isPlaying;
        public float CurrentVolume => Voice.source == null ? 0f : Voice.source.volume;
        public float TargetVolume => volume;
        /// <summary>Seconds into the track, so a continued day can be told apart from a restart.</summary>
        public float PlaybackTime => Voice.source == null ? 0f : Voice.source.time;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnPlay() => Active = null;

        private void Awake()
        {
            if (!continueAcrossDays)
            {
                // Scene-only music, or the persistent carrier itself.
                source = ConfigureSource(gameObject);
                return;
            }

            if (Active != null)
            {
                // The music is already running. Hand over the settings so the track
                // continues from where it is rather than starting again.
                Active.AdoptFrom(this);
                handedOver = true;
                return;
            }

            // The player lives on its own object. This component usually shares the
            // "Day" object with DayLevel, DayTitle, TimedReveal and OpeningSequence,
            // so it must never mark that object DontDestroyOnLoad or destroy it: that
            // would carry a stale DayLevel between days, or delete the day's own logic.
            var host = new GameObject("Background Music");
            host.SetActive(false); // Configure before its Awake caches the clip.
            DontDestroyOnLoad(host);
            player = host.AddComponent<BackgroundMusic>();
            player.continueAcrossDays = false; // The carrier never re-enters this branch.
            player.isPersistentInstance = true;
            player.music = music;
            player.volume = volume;
            player.loop = loop;
            player.startDelay = startDelay;
            player.fadeInDuration = fadeInDuration;
            player.crossFadeDuration = crossFadeDuration;
            player.ignorePause = ignorePause;
            player.playOnStart = false; // Started from Start(), after configuration.
            Active = player;
            host.SetActive(true);
        }

        private AudioSource ConfigureSource(GameObject host)
        {
            var audio = host.AddComponent<AudioSource>();
            audio.clip = music;
            audio.loop = loop;
            audio.playOnAwake = false;
            audio.spatialBlend = 0f; // Music is not positional.
            audio.volume = 0f;
            audio.ignoreListenerPause = ignorePause;
            return audio;
        }

        private void Start()
        {
            if (handedOver)
                return;
            if (player != null)
            {
                if (playOnStart)
                    player.Play();
                return;
            }

            if (source != null && playOnStart)
                Play();
        }

        /// <summary>
        /// Takes the settings a later day asked for. The same clip keeps playing
        /// untouched; a different clip cross-fades in, for the ending's own music.
        /// </summary>
        private void AdoptFrom(BackgroundMusic request)
        {
            volume = request.volume;
            if (source == null)
                source = ConfigureSource(gameObject);
            if (request.music == null)
            {
                // An empty slot means this day wants its own music, or none. Either way
                // the previous day's track does not belong here, so it fades out.
                StopFade();
                fade = StartCoroutine(FadeOutAndStop());
                return;
            }

            if (request.music == source.clip)
            {
                // Same track: do not restart, do not even reset the volume ramp.
                if (!source.isPlaying && request.playOnStart)
                    Play();
                return;
            }

            crossFadeDuration = request.crossFadeDuration;
            fadeInDuration = request.fadeInDuration;
            startDelay = 0f; // The hand-over already happened mid-game.
            music = request.music;
            loop = request.loop;
            StopFade();
            fade = StartCoroutine(CrossFadeTo(request.music));
        }

        public void Play()
        {
            BackgroundMusic voice = Voice;
            if (voice != this)
            {
                voice.Play();
                return;
            }

            if (source == null)
                source = ConfigureSource(gameObject);
            if (music == null)
                return;
            if (source.clip != music)
                source.clip = music;
            StopFade();
            fade = StartCoroutine(FadeIn());
        }

        /// <summary>Fades the music out and stops it, for the ending or a menu return.</summary>
        public void Stop()
        {
            BackgroundMusic voice = Voice;
            if (voice != this)
            {
                voice.Stop();
                return;
            }

            if (source == null)
                return;
            StopFade();
            fade = StartCoroutine(FadeOutAndStop());
        }

        /// <summary>Cuts the music dead, with no fade. Day 31 calls this on the gunshot.</summary>
        public void StopImmediately()
        {
            BackgroundMusic voice = Voice;
            if (voice != this)
            {
                voice.StopImmediately();
                return;
            }

            StopFade();
            if (source == null)
                return;
            source.Stop();
            source.volume = 0f;
        }

        /// <summary>Removes the persistent player, so the next start plays from the top.</summary>
        public static void ClearPersistent()
        {
            if (Active == null)
                return;
            BackgroundMusic active = Active;
            Active = null;
            if (active.isPersistentInstance)
                Destroy(active.gameObject);
            else
                active.StopImmediately();
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            BackgroundMusic voice = Voice;
            if (voice != this)
            {
                voice.SetVolume(volume);
                return;
            }

            if (source != null && source.isPlaying && fade == null)
                source.volume = volume;
        }

        private void StopFade()
        {
            if (fade == null)
                return;
            StopCoroutine(fade);
            fade = null;
        }

        private IEnumerator FadeIn()
        {
            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);

            if (!source.isPlaying)
            {
                source.volume = 0f;
                source.Play();
            }

            yield return Ramp(source.volume, volume, fadeInDuration);
            fade = null;
        }

        private IEnumerator CrossFadeTo(AudioClip next)
        {
            yield return Ramp(source.volume, 0f, crossFadeDuration);
            source.Stop();
            source.clip = next;
            source.loop = loop;
            source.Play();
            yield return Ramp(0f, volume, crossFadeDuration);
            fade = null;
        }

        private IEnumerator FadeOutAndStop()
        {
            yield return Ramp(source.volume, 0f, crossFadeDuration);
            source.Stop();
            fade = null;
        }

        private IEnumerator Ramp(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                source.volume = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.volume = to;
        }

        private void OnDestroy()
        {
            // Only the persistent carrier owns the shared slot. A day's own component
            // being destroyed with its scene must not clear it, or the next day would
            // start the track over from the beginning.
            if (Active == this)
                Active = null;
        }
    }
}
