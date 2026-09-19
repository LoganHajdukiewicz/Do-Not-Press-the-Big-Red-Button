using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Opens on a fully black screen, plays the opening narration, types the warning text,
    /// then fades the black away so the day begins.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class OpeningSequence : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("Opening.mp3. The sequence waits for this clip before fading in.")]
        [SerializeField] private AudioClip openingNarration;
        [SerializeField, Range(0f, 1f)] private float narrationVolume = 1f;

        [Header("Text")]
        [SerializeField] private string warningText = "DO NOT PRESS THE BIG RED BUTTON";
        [Tooltip("Seconds before the text starts appearing, matched to the narration.")]
        [SerializeField, Min(0f)] private float textStartDelay = 10.28f;
        [Tooltip("Seconds the text takes to finish appearing, as the words are spoken.")]
        [SerializeField, Min(0.1f)] private float textRevealDuration = 4f;
        [SerializeField, Range(0.02f, 0.3f)] private float textHeightFraction = 0.062f;
        [Tooltip("Letter spacing, which makes the warning read as printed company signage.")]
        [SerializeField, Range(0, 4)] private int letterSpacing = 2;

        [Header("Timing")]
        [Tooltip("Extra seconds to stay on black after the narration ends.")]
        [SerializeField, Min(0f)] private float holdAfterNarration = 1.2f;
        [Tooltip("Seconds for the black screen to fade away, revealing the red button.")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 3f;
        [Tooltip("Used when no narration clip is assigned.")]
        [SerializeField, Min(0.1f)] private float fallbackNarrationLength = 18f;

        [Header("Player")]
        [Tooltip("Disabled during the opening so the player cannot walk or look around. " +
            "Usually the First Person Player's controller.")]
        [SerializeField] private MonoBehaviour frozenDuringOpening;
        [Tooltip("Runs once the screen is fully clear.")]
        [SerializeField] private UnityEvent onOpeningFinished = new UnityEvent();

        private AudioSource source;
        private GUIStyle style;
        private Texture2D pixel;
        private float elapsed;
        private bool running = true;
        private bool playerWasEnabled;

        public UnityEvent OnOpeningFinished => onOpeningFinished;
        public bool IsRunning => running;
        public float BlackAlpha { get; private set; } = 1f;
        public float TextAlpha { get; private set; }
        public string VisibleText { get; private set; } = string.Empty;

        private float NarrationLength => openingNarration != null
            ? openingNarration.length : fallbackNarrationLength;
        private float FadeStartTime => NarrationLength + holdAfterNarration;
        public float TotalDuration => FadeStartTime + fadeOutDuration;

        private void Awake()
        {
            if (frozenDuringOpening == null)
                frozenDuringOpening = FindFirstObjectByType<FirstPersonController>();

            // Hold the player still and keep the cursor free while the intro plays.
            if (frozenDuringOpening != null)
            {
                playerWasEnabled = frozenDuringOpening.enabled;
                frozenDuringOpening.enabled = false;
            }

            if (openingNarration == null)
                return;

            source = gameObject.AddComponent<AudioSource>();
            source.clip = openingNarration;
            source.volume = narrationVolume;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // Narration is not positional.
            source.ignoreListenerPause = true;
        }

        private void Start()
        {
            if (source != null)
                source.Play();
        }

        private void Update()
        {
            if (!running)
                return;

            elapsed += Time.unscaledDeltaTime;

            float revealed = Mathf.Clamp01((elapsed - textStartDelay) / textRevealDuration);
            int characters = Mathf.RoundToInt(revealed * warningText.Length);
            VisibleText = warningText.Substring(0, Mathf.Clamp(characters, 0, warningText.Length));
            TextAlpha = elapsed < textStartDelay ? 0f : 1f;

            if (elapsed < FadeStartTime)
            {
                BlackAlpha = 1f;
                return;
            }

            float fade = fadeOutDuration <= 0f
                ? 1f : Mathf.Clamp01((elapsed - FadeStartTime) / fadeOutDuration);
            BlackAlpha = 1f - fade;
            // The text fades with the screen so it does not linger over gameplay.
            TextAlpha = BlackAlpha;

            if (fade < 1f)
                return;

            Finish();
        }

        /// <summary>Ends the opening immediately and hands control to the player.</summary>
        public void Finish()
        {
            if (!running)
                return;

            running = false;
            BlackAlpha = 0f;
            TextAlpha = 0f;
            VisibleText = string.Empty;

            if (frozenDuringOpening != null && playerWasEnabled)
                frozenDuringOpening.enabled = true;

            onOpeningFinished.Invoke();
        }

        private void OnGUI()
        {
            if (BlackAlpha <= 0f && TextAlpha <= 0f)
                return;

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;

            if (BlackAlpha > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, BlackAlpha);
                GUI.DrawTexture(full, pixel);
            }

            if (TextAlpha > 0f && !string.IsNullOrEmpty(VisibleText))
            {
                int fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * textHeightFraction));
                if (style == null)
                    style = CorporateText.CreateStyle(fontSize, wordWrap: true);
                style.fontSize = fontSize;
                GUI.color = new Color(1f, 1f, 1f, TextAlpha);
                CorporateText.DrawWithShadow(full,
                    CorporateText.Tracked(VisibleText, letterSpacing), style,
                    new Color(CorporateText.Ink.r, CorporateText.Ink.g, CorporateText.Ink.b, TextAlpha),
                    shadowOffset: Mathf.Max(2f, fontSize * 0.055f));
            }

            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (pixel != null)
                Destroy(pixel);
        }
    }
}
