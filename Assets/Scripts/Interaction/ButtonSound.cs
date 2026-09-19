using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Plays the press sound for any button, plus an optional extra layer such as the
    /// green button's ding. Both play together so every button feels like a real press.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonInteractable))]
    public sealed class ButtonSound : MonoBehaviour
    {
        [Tooltip("Played on every press, red or green.")]
        [SerializeField] private AudioClip pressClip;
        [SerializeField, Range(0f, 1f)] private float pressVolume = 0.9f;

        [Tooltip("Layered on top of the press, for example the green button's ding.")]
        [SerializeField] private AudioClip extraClip;
        [SerializeField, Range(0f, 1f)] private float extraVolume = 1f;
        [Tooltip("Seconds to wait before the extra layer, so the click lands first.")]
        [SerializeField, Min(0f)] private float extraDelay = 0.06f;

        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.85f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.minDistance = 2f;
            source.maxDistance = 25f;

            // The plain event, so even a button whose inspector event is suppressed
            // still makes a physical click when the player pushes it.
            GetComponent<ButtonInteractable>().Pressed += Play;
        }

        /// <summary>Plays the press, then the extra layer. Safe to call repeatedly.</summary>
        public void Play()
        {
            if (source == null)
                return;

            if (pressClip != null)
                source.PlayOneShot(pressClip, pressVolume);

            if (extraClip == null)
                return;

            if (extraDelay <= 0f)
                source.PlayOneShot(extraClip, extraVolume);
            else
                StartCoroutine(PlayExtraAfterDelay());
        }

        private void OnDestroy()
        {
            var button = GetComponent<ButtonInteractable>();
            if (button != null)
                button.Pressed -= Play;
        }

        private System.Collections.IEnumerator PlayExtraAfterDelay()
        {
            yield return new WaitForSeconds(extraDelay);
            if (source != null)
                source.PlayOneShot(extraClip, extraVolume);
        }
    }
}
