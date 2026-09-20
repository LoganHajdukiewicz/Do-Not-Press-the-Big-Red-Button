using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigRedButton
{
    /// <summary>
    /// A non-interactive paint bucket placed beside a button. Every visible dimension,
    /// colour, and position is deliberately exposed in the Inspector. Use the bucket's
    /// Inspector preview button to tune it directly in the Scene view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintBucket : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Position from the button cap that owns this bucket. Set Y to place its base on the floor.")]
        [SerializeField] private Vector3 offset = new Vector3(-0.92f, -1.15f, 0f);
        [SerializeField] private string bucketName = "Paint Bucket";

        [Header("Bucket body")]
        [Tooltip("Outside diameter of the metal pail in metres.")]
        [FormerlySerializedAs("size")]
        [SerializeField, Min(0.1f)] private float bucketDiameter = 0.9f;
        [Tooltip("Body height in metres, measured from its base on the floor.")]
        [SerializeField, Min(0.05f)] private float bucketHeight = 0.36f;
        [SerializeField] private Color bodyColour = new Color(0.42f, 0.43f, 0.45f);
        [SerializeField] private Color rimColour = new Color(0.28f, 0.29f, 0.31f);
        [SerializeField, Min(0.01f)] private float rimThickness = 0.07f;

        [Header("Paint bar on top")]
        [SerializeField] private Color paintColour = new Color(0.72f, 0.05f, 0.04f);
        [Tooltip("Diameter of the coloured paint bar on top. 1 matches the pail diameter.")]
        [SerializeField, Range(0.1f, 1.2f)] private float paintBarDiameterFraction = 0.88f;
        [SerializeField, Min(0.01f)] private float paintBarThickness = 0.11f;
        [Tooltip("Extra rise above the bucket rim, in metres.")]
        [SerializeField, Min(0f)] private float paintBarLift = 0.02f;

        private const string GeneratedName = "Paint Bucket Preview (generated)";
        private readonly List<Material> materials = new List<Material>();

        private void Awake() => Build();

        /// <summary>Rebuilds the pail. It never has colliders or gameplay behaviour.</summary>
        public void Build()
        {
            Clear();
            var root = new GameObject(string.IsNullOrWhiteSpace(bucketName)
                ? GeneratedName : bucketName + " Preview (generated)");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = offset;
            if (!Application.isPlaying)
                root.hideFlags = HideFlags.DontSaveInEditor;

            Material body = Material(bodyColour, 0.72f);
            Material rim = Material(rimColour, 0.6f);
            Material paint = Material(paintColour, 0.25f, emission: 0.08f);

            // Unity cylinders are two units tall, so their Y scale is half the height.
            Cylinder("Paint bucket body", root.transform,
                new Vector3(0f, bucketHeight * 0.5f, 0f),
                new Vector3(bucketDiameter, bucketHeight * 0.5f, bucketDiameter), body);
            Cylinder("Paint bucket rim", root.transform,
                new Vector3(0f, bucketHeight + rimThickness * 0.5f, 0f),
                new Vector3(bucketDiameter * 1.08f, rimThickness * 0.5f, bucketDiameter * 1.08f), rim);
            Cylinder("Paint bar", root.transform,
                new Vector3(0f, bucketHeight + rimThickness + paintBarLift + paintBarThickness * 0.5f, 0f),
                new Vector3(bucketDiameter * paintBarDiameterFraction, paintBarThickness * 0.5f,
                    bucketDiameter * paintBarDiameterFraction), paint);
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.EndsWith(" Preview (generated)"))
                    DestroyObject(child.gameObject);
            }
            foreach (Material material in materials)
                if (material != null)
                    DestroyObject(material);
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
            if (!Application.isPlaying)
                item.hideFlags = HideFlags.DontSaveInEditor;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = item.GetComponent<Collider>();
            if (collider != null)
                DestroyObject(collider);
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

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void OnDestroy() => Clear();
    }
}
