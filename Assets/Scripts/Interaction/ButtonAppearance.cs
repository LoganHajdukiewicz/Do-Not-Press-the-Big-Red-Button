using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Controls the colour a button *looks* like, separately from what it actually does.
    /// Every trick in the design that lies about a button's colour goes through this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonAppearance : MonoBehaviour
    {
        [Header("Colours")]
        [SerializeField] private Color redColour = new Color(0.72f, 0.05f, 0.04f);
        [SerializeField] private Color greenColour = new Color(0.09f, 0.55f, 0.16f);
        [SerializeField] private Color disabledColour = new Color(0.42f, 0.43f, 0.44f);

        [Header("Starting look")]
        [Range(0f, 1f)]
        [Tooltip("0 is fully red, 1 is fully green. Values in between blend the two.")]
        [SerializeField] private float greenness;
        [SerializeField] private bool startDisabled;

        [Header("References")]
        [Tooltip("Renderer to tint. Defaults to this object's renderer.")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Extra renderers tinted with the same colour, such as a housing ring.")]
        [SerializeField] private Renderer[] additionalRenderers = new Renderer[0];

        [Header("Look")]
        [Tooltip("Brightens the button so its colour reads clearly in a dim room.")]
        [SerializeField, Range(0f, 3f)] private float emissionStrength;

        private Material instanceMaterial;
        private Material[] additionalMaterials;
        private bool isDisabled;

        /// <summary>0 is fully red, 1 is fully green.</summary>
        public float Greenness
        {
            get => greenness;
            set
            {
                greenness = Mathf.Clamp01(value);
                Apply();
            }
        }

        public bool IsDisabledLook
        {
            get => isDisabled;
            set
            {
                isDisabled = value;
                Apply();
            }
        }

        public Color CurrentColour => isDisabled
            ? disabledColour : Color.Lerp(redColour, greenColour, greenness);

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            isDisabled = startDisabled;
            if (targetRenderer == null)
                return;

            // Own the material so tinting one button never recolours every other button.
            instanceMaterial = new Material(targetRenderer.sharedMaterial);
            targetRenderer.material = instanceMaterial;

            additionalMaterials = new Material[additionalRenderers.Length];
            for (int i = 0; i < additionalRenderers.Length; i++)
            {
                if (additionalRenderers[i] == null)
                    continue;
                additionalMaterials[i] = new Material(additionalRenderers[i].sharedMaterial);
                additionalRenderers[i].material = additionalMaterials[i];
            }

            Apply();
        }

        public void SetRed() => Greenness = 0f;
        public void SetGreen() => Greenness = 1f;

        /// <summary>Clears the grey look, as on the day the button has to be woken up first.</summary>
        public void Enable() => IsDisabledLook = false;

        private void Apply()
        {
            if (instanceMaterial == null)
                return;

            Color colour = CurrentColour;
            Tint(instanceMaterial, colour);
            if (additionalMaterials == null)
                return;
            foreach (Material extra in additionalMaterials)
                Tint(extra, colour);
        }

        private void Tint(Material material, Color colour)
        {
            if (material == null)
                return;

            material.color = colour;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colour);

            if (emissionStrength <= 0f)
                return;
            if (!material.HasProperty("_EmissionColor"))
                return;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * emissionStrength);
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null)
                Destroy(instanceMaterial);
            if (additionalMaterials == null)
                return;
            foreach (Material extra in additionalMaterials)
                if (extra != null)
                    Destroy(extra);
        }
    }
}
