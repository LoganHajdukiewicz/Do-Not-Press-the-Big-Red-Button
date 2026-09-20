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

        public AudioClip CurrentClip => source == null ? null : source.clip;
        public bool IsPlaying => source != null && source.isPlaying;
        public float CurrentVolume => source == null ? 0f : source.volume;
        public float TargetVolume => volume;
        /// <summary>Seconds into the track, so a continued day can be told apart from a restart.</summary>
        public float PlaybackTime => source == null ? 0f : source.time;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnPlay() => Active = null;

        private void Awake()
        {
            if (continueAcrossDays && Active != null && Active != this)
            {
                // Another day already has the music running. Hand over the clip and go,
                // so the track continues from where it is rather than starting again.
                Active.AdoptFrom(this);
                Destroy(gameObject);
                return;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.clip = music;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // Music is not positional.
            source.volume = 0f;
            source.ignoreListenerPause = ignorePause;

            if (!continueAcrossDays)
                return;

            // Detach from the day, or loading the next day would destroy the music with it.
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            isPersistentInstance = true;
            Active = this;
        }

        private void Start()
        {
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
            if (source == null || music == null)
                return;
            StopFade();
            fade = StartCoroutine(FadeIn());
        }

        /// <summary>Fades the music out and stops it, for the ending or a menu return.</summary>
        public void Stop()
        {
            if (source == null)
                return;
            StopFade();
            fade = StartCoroutine(FadeOutAndStop());
        }

        public void StopImmediately()
        {
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
            if (Active == this)
                Active = null;
        }
    }
}
