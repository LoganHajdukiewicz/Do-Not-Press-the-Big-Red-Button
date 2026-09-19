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
        [Header("Text")]
        [TextArea(1, 3)]
        [SerializeField] private string text = "DO NOT PRESS";
        [Tooltip("Forces the text to capitals, the way printed signage reads.")]
        [SerializeField] private bool uppercase;
        [SerializeField] private Color colour = new Color(0.9f, 0.9f, 0.87f);

        [Header("Placement")]
        [Tooltip("Metres above the button the sign floats.")]
        [SerializeField] private float height = 0.55f;
        [Tooltip("Extra offset in world space, for signs beside a button.")]
        [SerializeField] private Vector3 offset;

        [Header("Size and range")]
        [Tooltip("On-screen size at 3 metres away.")]
        [SerializeField, Min(4f)] private float baseFontSize = 22f;
        [Tooltip("Hides the sign beyond this distance, so far rooms stay quiet.")]
        [SerializeField, Min(1f)] private float visibleDistance = 14f;
        [Tooltip("Keeps the size fixed on screen instead of shrinking with distance.")]
        [SerializeField] private bool constantScreenSize;
        [Tooltip("Smallest on-screen size, so distant signs stay legible.")]
        [SerializeField, Min(4f)] private float minimumFontSize = 8f;

        [Header("Visibility")]
        [Tooltip("Keeps the sign readable through walls when unchecked.")]
        [SerializeField] private bool hideWhenOccluded = true;
        [Tooltip("Hides the sign once the button has been pressed.")]
        [SerializeField] private bool hideAfterPress;

        private GUIStyle style;
        private Camera viewer;
        private bool hidden;

        public string Text
        {
            get => text;
            set => text = value ?? string.Empty;
        }

        /// <summary>Hides the sign, for example from another button's event.</summary>
        public void Hide() => hidden = true;
        public void Show() => hidden = false;

        private void Awake()
        {
            if (!hideAfterPress)
                return;
            var button = GetComponent<ButtonInteractable>();
            if (button != null)
                button.Pressed += Hide;
        }

        private void OnDestroy()
        {
            var button = GetComponent<ButtonInteractable>();
            if (button != null)
                button.Pressed -= Hide;
        }

        private void OnGUI()
        {
            if (hidden || string.IsNullOrEmpty(text))
                return;

            if (viewer == null)
            {
                viewer = Camera.main;
                if (viewer == null)
                    return;
            }

            Vector3 worldPoint = transform.position + Vector3.up * height + offset;
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
            float scale = constantScreenSize ? 1f : 3f / screenPoint.z;
            int fontSize = Mathf.Max(Mathf.RoundToInt(minimumFontSize),
                Mathf.RoundToInt(baseFontSize * scale * (Screen.height / 720f)));
            if (style == null)
                style = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            style.fontSize = fontSize;

            float fade = Mathf.Clamp01(1f - screenPoint.z / visibleDistance);
            float alpha = colour.a * Mathf.Min(1f, fade + 0.35f);
            var area = new Rect(screenPoint.x - 200f, Screen.height - screenPoint.y - 20f, 400f, 40f);
            style.normal.textColor = new Color(colour.r, colour.g, colour.b, alpha);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(area, uppercase ? text.ToUpperInvariant() : text, style);
            GUI.color = previous;
        }
    }
}
