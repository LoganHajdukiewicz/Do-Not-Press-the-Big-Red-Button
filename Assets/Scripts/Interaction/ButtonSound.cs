using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigRedButton
{
    /// <summary>
    /// Plays the press sound for any button, and the ding whenever the button was
    /// showing green at the moment it was pressed. The ding follows the state, not the
    /// object, so a button that turns green dings too.
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
        [Tooltip("Only ding while the button is showing green. Uncheck to always ding.")]
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
        private ButtonAppearance appearance;

        /// <summary>
        /// True when this button counts as green right now. A button with no colour
        /// control of its own is treated as green, so plain green buttons still ding.
        /// </summary>
        public bool CountsAsGreen
        {
            get
            {
                if (appearance == null)
                    return true;
                if (appearance.IsDisabledLook)
                    return false;
                return appearance.Greenness >= 0.5f;
            }
        }

        private void Awake()
        {
            button = GetComponent<ButtonInteractable>();
            appearance = GetComponent<ButtonAppearance>();

            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance + 1f, maxDistance);

            // The plain event, so even a button whose inspector event is suppressed
            // still makes a physical click when the player pushes it.
            button.Pressed += Play;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.Pressed -= Play;
        }

        /// <summary>Plays the click, then the ding or red sound for the current state.</summary>
        public void Play()
        {
            if (source == null)
                return;

            if (pressClip != null)
                source.PlayOneShot(pressClip, pressVolume);

            // Read the colour now, at the moment of the press, not when the layer plays.
            bool green = !dingOnlyWhenGreen || CountsAsGreen;
            AudioClip layer = green ? greenClip : redClip;
            float volume = green ? greenVolume : redVolume;
            float delay = green ? greenDelay : redDelay;
            if (layer == null)
                return;

            if (delay <= 0f || !gameObject.activeInHierarchy)
                source.PlayOneShot(layer, volume);
            else
                StartCoroutine(PlayLayerAfterDelay(layer, volume, delay));
        }

        private IEnumerator PlayLayerAfterDelay(AudioClip layer, float volume, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (source != null)
                source.PlayOneShot(layer, volume);
        }
    }
}
