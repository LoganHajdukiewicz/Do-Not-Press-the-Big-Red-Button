using System.Collections.Generic;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// A small, non-interactive paint bucket placed beside a button. Its open top
    /// shows the exact paint colour used on that button, which makes Day 17's lie
    /// tangible without giving the player a separate UI clue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintBucket : MonoBehaviour
    {
        [Header("Paint")]
        [SerializeField] private Color paintColour = new Color(0.72f, 0.05f, 0.04f);
        [SerializeField] private string bucketName = "Paint Bucket";

        [Header("Placement")]
        [SerializeField] private Vector3 offset = new Vector3(1.2f, 0f, -0.15f);
        [SerializeField, Min(0.1f)] private float size = 0.42f;

        private const string GeneratedName = "Paint Bucket (generated)";
        private readonly List<Material> materials = new List<Material>();

        private void Awake() => Build();

        /// <summary>Rebuilds the bucket. It has no colliders and cannot affect play.</summary>
        public void Build()
        {
            Clear();
            var root = new GameObject(string.IsNullOrWhiteSpace(bucketName)
                ? GeneratedName : bucketName + " (generated)");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = offset;

            Material metal = Material(new Color(0.42f, 0.43f, 0.45f), 0.72f);
            Material paint = Material(paintColour, 0.25f, emission: 0.08f);

            Cylinder("Bucket", root.transform, new Vector3(0f, size * 0.46f, 0f),
                new Vector3(size, size * 0.92f, size), metal);
            Cylinder("Paint surface", root.transform, new Vector3(0f, size * 0.93f, 0f),
                new Vector3(size * 0.83f, size * 0.06f, size * 0.83f), paint);
            Cylinder("Bucket rim", root.transform, new Vector3(0f, size * 0.96f, 0f),
                new Vector3(size * 1.06f, size * 0.09f, size * 1.06f), metal);
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.EndsWith(" (generated)"))
                    Destroy(child.gameObject);
            }
            foreach (Material material in materials)
                if (material != null)
                    Destroy(material);
            materials.Clear();
        }

        private GameObject Cylinder(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = item.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            return item;
        }

        private Material Material(Color colour, float smoothness, float emission = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = colour };
            materials.Add(material);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * emission);
            }
            return material;
        }

        private void OnDestroy() => Clear();
    }
}
