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
        [SerializeField, Range(0.05f, 0.5f)] private float textHeightFraction = 0.16f;
        [SerializeField] private Color textColor = Color.white;
        [Tooltip("Darkens the scene behind the title while it is visible.")]
        [SerializeField, Range(0f, 1f)] private float backdropOpacity = 0.55f;

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
                style = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = false };
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

            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * textHeightFraction));
            style.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, textColor.a * alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(full, $"DAY {day}", style);
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
