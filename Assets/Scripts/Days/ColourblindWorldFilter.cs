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
        [Tooltip("Also tints unlit/emissive surfaces such as the ceiling strips.")]
        [SerializeField] private bool includeEmission = true;

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
            Tint(copy, "_BaseColor");
            Tint(copy, "_Color");
            if (includeEmission)
                Tint(copy, "_EmissionColor");
            return copy;
        }

        private static void Tint(Material material, string property)
        {
            if (!material.HasProperty(property))
                return;
            Color original = material.GetColor(property);
            material.SetColor(property, Deuteranopia(original));
        }

        // A standard red/green deficiency approximation. It preserves brightness but
        // brings red and green hues toward the same yellow-brown range.
        private static Color Deuteranopia(Color colour)
        {
            return new Color(
                colour.r * 0.625f + colour.g * 0.375f,
                colour.r * 0.700f + colour.g * 0.300f,
                colour.g * 0.300f + colour.b * 0.700f,
                colour.a);
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
