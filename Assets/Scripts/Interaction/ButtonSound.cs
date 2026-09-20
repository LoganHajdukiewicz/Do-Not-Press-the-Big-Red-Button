using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigRedButton
{
    /// <summary>
    /// Clicks on accepted presses. Dings only when an active green click completes
    /// the required sequence, using the state captured before wake/colour callbacks.
    /// Grey, suppressed and incomplete clicks never play an outcome layer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonInteractable))]
    public sealed class ButtonSound : MonoBehaviour
    {
        [Header("Press")]
        [Tooltip("Played on every press, red or green.")]
        [SerializeField] private AudioClip pressClip;
        [SerializeField, Range(0f, 1f)] private float pressVolume = 0.9f;

        [Header("Green ding")]
        [Tooltip("Played when the button is green as it is pressed.")]
        [FormerlySerializedAs("extraClip")]
        [SerializeField] private AudioClip greenClip;
        [FormerlySerializedAs("extraVolume")]
        [SerializeField, Range(0f, 1f)] private float greenVolume = 1f;
        [Tooltip("Seconds to wait before the ding, so the click lands first.")]
        [FormerlySerializedAs("extraDelay")]
        [SerializeField, Min(0f)] private float greenDelay = 0.06f;
        [Tooltip("Only ding on a completed green click. Uncheck to also ding on completed red clicks. " +
            "Grey, suppressed and incomplete clicks never ding.")]
        [SerializeField] private bool dingOnlyWhenGreen = true;

        [Header("Red")]
        [Tooltip("Optional sound for pressing it while red. Leave empty for none.")]
        [SerializeField] private AudioClip redClip;
        [SerializeField, Range(0f, 1f)] private float redVolume = 0.9f;
        [SerializeField, Min(0f)] private float redDelay = 0.06f;

        [Header("Output")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.85f;
        [SerializeField, Min(1f)] private float minDistance = 2f;
        [SerializeField, Min(1f)] private float maxDistance = 25f;

        private AudioSource source;
        private ButtonInteractable button;

        /// <summary>Live state for Inspector/debugging; actual clicks use their immutable snapshot.</summary>
        public bool CountsAsGreen => button != null && button.CountsAsGreen;
        /// <summary>Last outcome layer selected for playback; null for grey or incomplete clicks.</summary>
        public AudioClip LastLayerScheduled { get; private set; }

        private void Awake()
        {
            button = GetComponent<ButtonInteractable>();

            // Own a separate source: replacing click dialogue must not stop the click/ding,
            // regardless of whether TalkingButton's Awake ran before this component.
            source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance + 1f, maxDistance);

            // Includes wake-up clicks, but carries their state BEFORE WakeableButton ran.
            button.PressAccepted += PlayAcceptedPress;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.PressAccepted -= PlayAcceptedPress;
            if (source != null)
                Destroy(source);
        }

        /// <summary>Manual preview/event hook. Deactivated objects never play an outcome layer.</summary>
        public void Play()
        {
            if (button == null)
                return;
            PlayAcceptedPress(new ButtonPress(0, button.IsDeactivated, CountsAsGreen, true));
        }

        private void PlayAcceptedPress(ButtonPress press)
        {
            LastLayerScheduled = null;
            if (source == null || !isActiveAndEnabled)
                return;

            if (pressClip != null)
                source.PlayOneShot(pressClip, pressVolume);

            // This test precedes the "always ding" override. Waking during this callback
            // cannot turn a grey click into a green ding, regardless of component order.
            if (press.WasDisabled || !press.CompletesSequence)
                return;
            bool green = !dingOnlyWhenGreen || press.WasGreen;
            AudioClip layer = green ? greenClip : redClip;
            float volume = green ? greenVolume : redVolume;
            float delay = green ? greenDelay : redDelay;
            if (layer == null)
                return;

            LastLayerScheduled = layer;
            if (delay <= 0f)
                source.PlayOneShot(layer, volume);
            else
                StartCoroutine(PlayLayerAfterDelay(layer, volume, delay));
        }

        private IEnumerator PlayLayerAfterDelay(AudioClip layer, float volume, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (source != null && isActiveAndEnabled && button != null && !button.IsDeactivated)
                source.PlayOneShot(layer, volume);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            LastLayerScheduled = null;
            if (source != null)
                source.Stop();
        }
    }
}
