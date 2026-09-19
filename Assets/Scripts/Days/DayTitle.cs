using UnityEngine;

namespace BigRedButton
{
    /// <summary>Draws "DAY N" in large letters, then fades it out.</summary>
    [DisallowMultipleComponent]
    public sealed class DayTitle : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeInDuration = 0.4f;
        [SerializeField, Min(0f)] private float holdDuration = 1.6f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 1.2f;
        [Tooltip("Title height as a fraction of screen height, so it stays big on any display.")]
        [SerializeField, Range(0.05f, 0.5f)] private float textHeightFraction = 0.13f;
        [Tooltip("Letter spacing, which makes the title read as company signage.")]
        [SerializeField, Range(0, 6)] private int letterSpacing = 3;
        [SerializeField] private Color textColor = new Color(0.86f, 0.86f, 0.83f);
        [Tooltip("Darkens the scene behind the title while it is visible.")]
        [SerializeField, Range(0f, 1f)] private float backdropOpacity = 0.55f;
        [Tooltip("Uncheck when something else starts the title, such as the opening sequence.")]
        [SerializeField] private bool playOnStart = true;

        private GUIStyle style;
        private Texture2D backdrop;
        private float elapsed;
        private int day = DayFlow.FirstDay;
        private bool playing;

        public bool IsPlaying => playing;
        public float TotalDuration => fadeInDuration + holdDuration + fadeOutDuration;

        /// <summary>Current title opacity, 0 when hidden and 1 when fully visible.</summary>
        public float CurrentAlpha
        {
            get
            {
                if (!playing)
                    return 0f;
                if (elapsed < fadeInDuration)
                    return fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);

                float afterHold = elapsed - fadeInDuration - holdDuration;
                if (afterHold <= 0f)
                    return 1f;
                return fadeOutDuration <= 0f ? 0f : Mathf.Clamp01(1f - afterHold / fadeOutDuration);
            }
        }

        public bool PlayOnStart => playOnStart;

        /// <summary>Shows the title for the day the player is currently on.</summary>
        public void PlayCurrentDay() => Play(DayFlow.CurrentDay);

        public void Play(int dayNumber)
        {
            day = Mathf.Max(DayFlow.FirstDay, dayNumber);
            elapsed = 0f;
            playing = true;
        }

        public void Hide()
        {
            playing = false;
            elapsed = 0f;
        }

        private void Update()
        {
            if (!playing)
                return;

            // Unscaled time so the title still fades if the day pauses gameplay.
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= TotalDuration)
                Hide();
        }

        private void OnGUI()
        {
            float alpha = CurrentAlpha;
            if (alpha <= 0f)
                return;

            if (style == null)
                style = CorporateText.CreateStyle(16);
            if (backdrop == null)
            {
                backdrop = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                backdrop.SetPixel(0, 0, Color.white);
                backdrop.Apply();
            }

            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;
            if (backdropOpacity > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, backdropOpacity * alpha);
                GUI.DrawTexture(full, backdrop);
            }

            int fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * textHeightFraction));
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            CorporateText.DrawWithShadow(full,
                CorporateText.Tracked($"DAY {day}", letterSpacing), style,
                new Color(textColor.r, textColor.g, textColor.b, textColor.a * alpha),
                shadowOffset: Mathf.Max(2f, fontSize * 0.05f));
            GUI.color = previous;
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (backdrop != null)
                Destroy(backdrop);
        }
    }
}
