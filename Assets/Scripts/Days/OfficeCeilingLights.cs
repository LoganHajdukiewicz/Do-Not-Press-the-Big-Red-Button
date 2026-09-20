using System.Collections.Generic;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Builds a grid of recessed LED office panels under a ceiling, the flat even
    /// lighting of a room nobody chose to be in. Generated at runtime from the
    /// ceiling's own size, so it fits whatever room it is dropped into.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class OfficeCeilingLights : MonoBehaviour
    {
        [Header("Ceiling")]
        [Tooltip("The ceiling slab to line with panels. Defaults to this object's renderer.")]
        [SerializeField] private Renderer ceiling;
        [Tooltip("Metres below the ceiling's underside that the panels sit.")]
        [SerializeField, Min(0f)] private float dropBelowCeiling = 0.02f;
        [Tooltip("Keeps panels this far inside the room's edges.")]
        [SerializeField, Min(0f)] private float edgeMargin = 1.6f;

        [Header("Panel grid")]
        [Tooltip("Metres between panel centres along X. Fewer, wider spacings mean fewer fittings.")]
        [SerializeField, Min(1f)] private float spacingX = 4.5f;
        [Tooltip("Metres between panel centres along Z.")]
        [SerializeField, Min(1f)] private float spacingZ = 4.5f;
        [Tooltip("Panel size in metres: a standard long office fitting.")]
        [SerializeField] private Vector2 panelSize = new Vector2(1.2f, 0.3f);
        [Tooltip("Hard cap, so an enormous outdoor floor cannot spawn thousands of lights.")]
        [SerializeField, Min(1)] private int maxPanels = 80;

        [Header("Look")]
        [Tooltip("The cool white of an office fitting.")]
        [SerializeField] private Color lightColour = new Color(0.94f, 0.96f, 1f);
        [Tooltip("How brightly the panel face itself glows.")]
        [SerializeField, Min(0f)] private float emission = 2.6f;
        [Tooltip("Thin housing frame around each panel.")]
        [SerializeField] private bool showHousing = true;

        [Header("Real lights")]
        [Tooltip("Adds a point light per panel. Uncheck to keep only the glowing panels.")]
        [SerializeField] private bool castLight = true;
        [SerializeField, Min(0f)] private float lightIntensity = 1.5f;
        [SerializeField, Min(0.5f)] private float lightRange = 7f;
        [Tooltip("Total real lights allowed, so forward rendering never runs out of slots.")]
        [SerializeField, Min(0)] private int maxRealLights = 12;

        private const string ContainerName = "LED Office Lights";
        private readonly List<GameObject> built = new List<GameObject>();

        public int PanelCount { get; private set; }
        public int RealLightCount { get; private set; }

        private void Awake()
        {
            if (Application.isPlaying)
                Build();
        }

        /// <summary>Creates the fittings. Safe to call again; it replaces what it made.</summary>
        public void Build()
        {
            Clear();
            if (ceiling == null)
                ceiling = GetComponent<Renderer>();
            if (ceiling == null)
                return;

            Bounds bounds = ceiling.bounds;
            var container = new GameObject(ContainerName);
            container.transform.SetParent(transform, false);
            built.Add(container);

            float height = bounds.min.y - dropBelowCeiling;
            float usableX = Mathf.Max(0f, bounds.size.x - edgeMargin * 2f);
            float usableZ = Mathf.Max(0f, bounds.size.z - edgeMargin * 2f);
            int columns = Mathf.Max(1, Mathf.FloorToInt(usableX / spacingX) + 1);
            int rows = Mathf.Max(1, Mathf.FloorToInt(usableZ / spacingZ) + 1);
            while (columns * rows > maxPanels && (columns > 1 || rows > 1))
            {
                if (columns >= rows)
                    columns--;
                else
                    rows--;
            }

            // Centre the grid on the ceiling so the room reads as deliberately fitted.
            float startX = bounds.center.x - (columns - 1) * spacingX * 0.5f;
            float startZ = bounds.center.z - (rows - 1) * spacingZ * 0.5f;
            Material face = BuildPanelMaterial();
            Material housing = showHousing ? BuildHousingMaterial() : null;
            int lightBudget = castLight ? maxRealLights : 0;
            // Light the middle of the room first; corners are covered by the glow.
            var positions = new List<Vector3>();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    positions.Add(new Vector3(startX + column * spacingX, height,
                        startZ + row * spacingZ));
            positions.Sort((a, b) =>
                Vector3.SqrMagnitude(a - bounds.center).CompareTo(
                    Vector3.SqrMagnitude(b - bounds.center)));

            PanelCount = 0;
            RealLightCount = 0;
            foreach (Vector3 position in positions)
            {
                CreatePanel(container.transform, position, face, housing,
                    RealLightCount < lightBudget);
                if (RealLightCount < lightBudget)
                    RealLightCount++;
                PanelCount++;
            }
        }

        /// <summary>Removes the generated fittings, leaving hand-made children alone.</summary>
        public void Clear()
        {
            foreach (GameObject item in built)
                if (item != null)
                    DestroyObject(item);
            built.Clear();

            // Also clear a container left by an earlier build or a saved scene.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == ContainerName)
                    DestroyObject(child.gameObject);
            }

            PanelCount = 0;
            RealLightCount = 0;
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void CreatePanel(Transform parent, Vector3 position, Material face,
            Material housing, bool withLight)
        {
            if (housing != null)
            {
                GameObject frame = Slab("LED Panel Housing", parent, position + Vector3.up * 0.015f,
                    new Vector3(panelSize.x + 0.1f, 0.03f, panelSize.y + 0.1f), housing);
                frame.transform.SetParent(parent, true);
            }

            GameObject panel = Slab("LED Panel", parent, position,
                new Vector3(panelSize.x, 0.04f, panelSize.y), face);
            if (!withLight)
                return;

            var lamp = new GameObject("LED Panel Light");
            lamp.transform.SetParent(panel.transform, false);
            lamp.transform.localPosition = new Vector3(0f, -1.2f, 0f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColour;
            light.intensity = lightIntensity;
            light.range = lightRange;
            light.shadows = LightShadows.None; // Ceiling fittings do not need to cast shadows.
        }

        private static GameObject Slab(string name, Transform parent, Vector3 worldPosition,
            Vector3 scale, Material material)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, true);
            slab.transform.position = worldPosition;
            slab.transform.localScale = scale;
            slab.GetComponent<Renderer>().sharedMaterial = material;
            // Fittings are decoration; they must never block a raycast or the player.
            Collider box = slab.GetComponent<Collider>();
            if (box != null)
                DestroyObject(box);
            return slab;
        }

        private Material BuildPanelMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = lightColour };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", lightColour);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.1f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", lightColour * emission);
            }

            return material;
        }

        private Material BuildHousingMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var frame = new Color(0.78f, 0.79f, 0.8f);
            var material = new Material(shader) { color = frame };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", frame);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.55f);
            return material;
        }
    }
}
