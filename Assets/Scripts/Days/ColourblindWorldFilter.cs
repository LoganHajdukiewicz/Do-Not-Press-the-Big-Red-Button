using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Applies a deuteranopia-style colour transform to the complete 3D room.
    /// Day 16 uses this so the filter is a property of the world, not merely a
    /// trick applied to the two buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColourblindWorldFilter : MonoBehaviour
    {
        [Tooltip("The single indistinguishable red/green colour used by the entire room.")]
        [SerializeField] private Color colourblindColour = new Color(0.62f, 0.55f, 0.16f);
        [Tooltip("Also colours emissive surfaces such as the ceiling strips.")]
        [SerializeField] private bool includeEmission = true;
        [SerializeField, Min(0f)] private float emissionStrength = 1.8f;

        private readonly List<Material> generatedMaterials = new List<Material>();

        private void Start()
        {
            // Ceiling strips are made by another Awake. Waiting a frame includes them too.
            StartCoroutine(ApplyAfterSceneStarts());
        }

        private IEnumerator ApplyAfterSceneStarts()
        {
            yield return null;
            ApplyToRoom();
        }

        /// <summary>Applies the filter to every visible renderer in this loaded day.</summary>
        public void ApplyToRoom()
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                Material[] source = renderer.sharedMaterials;
                var filtered = new Material[source.Length];
                bool changed = false;
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null)
                        continue;
                    filtered[i] = FilteredCopy(source[i]);
                    changed = true;
                }

                if (changed)
                    renderer.materials = filtered;
            }
        }

        private Material FilteredCopy(Material source)
        {
            var copy = new Material(source);
            generatedMaterials.Add(copy);
            SetUniformColour(copy, "_BaseColor");
            SetUniformColour(copy, "_Color");
            if (includeEmission)
                SetUniformColour(copy, "_EmissionColor", emissionStrength);
            return copy;
        }

        private void SetUniformColour(Material material, string property, float brightness = 1f)
        {
            if (!material.HasProperty(property))
                return;
            // Keep a transparent material transparent, but make every visible hue
            // the same. There is no remaining wall/floor/button colour information.
            float alpha = material.GetColor(property).a;
            Color uniform = colourblindColour * brightness;
            uniform.a = alpha;
            material.SetColor(property, uniform);
        }

        private void OnDestroy()
        {
            foreach (Material material in generatedMaterials)
                if (material != null)
                    Destroy(material);
            generatedMaterials.Clear();
        }
    }
}
