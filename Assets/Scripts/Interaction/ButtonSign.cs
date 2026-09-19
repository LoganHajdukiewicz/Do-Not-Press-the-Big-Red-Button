using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// A word painted on or above a button, in the company's own dreary lettering.
    /// Day 5 ("DO NOT PRESS"), Day 10 and Day 17 (buttons mislabelled RED and GREEN).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonSign : MonoBehaviour
    {
        [SerializeField] private string text = "DO NOT PRESS";
        [Tooltip("Metres above the button the sign floats.")]
        [SerializeField] private float height = 0.55f;
        [Tooltip("On-screen size at 3 metres away.")]
        [SerializeField, Min(4f)] private float baseFontSize = 22f;
        [Tooltip("Hides the sign beyond this distance, so far rooms stay quiet.")]
        [SerializeField, Min(1f)] private float visibleDistance = 14f;
        [SerializeField] private Color colour = new Color(0.9f, 0.9f, 0.87f);
        [Tooltip("Keeps the sign readable through walls when unchecked.")]
        [SerializeField] private bool hideWhenOccluded = true;

        private GUIStyle style;
        private Camera viewer;

        public string Text
        {
            get => text;
            set => text = value ?? string.Empty;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (viewer == null)
            {
                viewer = Camera.main;
                if (viewer == null)
                    return;
            }

            Vector3 worldPoint = transform.position + Vector3.up * height;
            Vector3 screenPoint = viewer.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0.2f || screenPoint.z > visibleDistance)
                return;

            if (hideWhenOccluded)
            {
                Vector3 toSign = worldPoint - viewer.transform.position;
                if (Physics.Raycast(viewer.transform.position, toSign.normalized,
                        out RaycastHit hit, toSign.magnitude - 0.35f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !hit.collider.transform.IsChildOf(transform.root))
                    return;
            }

            // Shrink with distance, the way a real sign does.
            int fontSize = Mathf.Max(8, Mathf.RoundToInt(baseFontSize * (3f / screenPoint.z) *
                (Screen.height / 720f)));
            if (style == null)
                style = CorporateText.CreateStyle(fontSize);
            style.fontSize = fontSize;

            float fade = Mathf.Clamp01(1f - screenPoint.z / visibleDistance);
            var area = new Rect(screenPoint.x - 200f, Screen.height - screenPoint.y - 20f, 400f, 40f);
            CorporateText.DrawWithShadow(area, CorporateText.Tracked(text.ToUpperInvariant()),
                style, new Color(colour.r, colour.g, colour.b, colour.a * Mathf.Min(1f, fade + 0.35f)));
        }
    }
}
