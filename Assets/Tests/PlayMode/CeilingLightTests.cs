using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BigRedButton.Tests
{
    public sealed class CeilingLightTests
    {
        private static readonly Vector3 Origin = new Vector3(500f, 0f, 500f);
        private readonly List<GameObject> objects = new List<GameObject>();

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private OfficeCeilingLights BuildCeiling(Vector3 size)
        {
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(ceiling);
            ceiling.name = "Ceiling";
            ceiling.transform.position = Origin + Vector3.up * 4f;
            ceiling.transform.localScale = size;
            Physics.SyncTransforms();
            return ceiling.AddComponent<OfficeCeilingLights>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void PanelsAreBuiltUnderTheCeilingAndInsideTheRoom()
        {
            OfficeCeilingLights lights = BuildCeiling(new Vector3(12f, 0.4f, 12f));
            lights.Build();

            var panels = lights.GetComponentsInChildren<Renderer>()
                .Where(r => r.gameObject.name == "LED Panel").ToArray();
            Assert.That(panels, Is.Not.Empty, "A 12x12 room must get at least one fitting.");
            Assert.That(lights.PanelCount, Is.EqualTo(panels.Length));

            Bounds room = lights.GetComponent<Renderer>().bounds;
            foreach (Renderer panel in panels)
            {
                Assert.That(panel.transform.position.y, Is.LessThan(room.min.y + 0.001f),
                    "Panels hang below the ceiling, not inside or above it.");
                Assert.That(Mathf.Abs(panel.transform.position.x - room.center.x),
                    Is.LessThanOrEqualTo(room.extents.x), "Panels stay within the room.");
                Assert.That(Mathf.Abs(panel.transform.position.z - room.center.z),
                    Is.LessThanOrEqualTo(room.extents.z));
            }
        }

        [Test]
        public void FittingsNeverBlockInteractionOrMovement()
        {
            OfficeCeilingLights lights = BuildCeiling(new Vector3(18f, 0.4f, 20f));
            lights.Build();
            Physics.SyncTransforms();

            foreach (Transform part in lights.GetComponentsInChildren<Transform>())
            {
                if (part == lights.transform)
                    continue;
                Assert.That(part.GetComponent<Collider>(), Is.Null,
                    $"\"{part.name}\" must not have a collider; it would block the player or the E ray.");
            }
        }

        [Test]
        public void RealLightsAreCappedButPanelsStillFillTheRoom()
        {
            OfficeCeilingLights lights = BuildCeiling(new Vector3(120f, 0.4f, 120f));
            Set(lights, "maxRealLights", 4);
            Set(lights, "maxPanels", 40);
            lights.Build();

            Assert.That(lights.RealLightCount, Is.EqualTo(4),
                "Forward rendering has a per-object light limit, so real lights are capped.");
            Assert.That(lights.GetComponentsInChildren<Light>().Length, Is.EqualTo(4));
            Assert.That(lights.PanelCount, Is.GreaterThan(4), "The rest are glowing panels.");
            Assert.That(lights.PanelCount, Is.LessThanOrEqualTo(40), "The panel cap is respected.");
        }

        [Test]
        public void RebuildingReplacesTheFittingsInsteadOfStackingThem()
        {
            OfficeCeilingLights lights = BuildCeiling(new Vector3(12f, 0.4f, 12f));
            lights.Build();
            int first = lights.GetComponentsInChildren<Renderer>()
                .Count(r => r.gameObject.name == "LED Panel");

            lights.Build();
            lights.Build();
            int afterRebuilds = lights.GetComponentsInChildren<Renderer>()
                .Count(r => r.gameObject.name == "LED Panel");
            Assert.That(afterRebuilds, Is.EqualTo(first), "Re-running must not duplicate panels.");

            lights.Clear();
            Assert.That(lights.GetComponentsInChildren<Light>(), Is.Empty);
            Assert.That(lights.PanelCount, Is.Zero);
        }

        [Test]
        public void EveryPanelGlowsWithOfficeWhite()
        {
            OfficeCeilingLights lights = BuildCeiling(new Vector3(12f, 0.4f, 12f));
            lights.Build();

            Renderer panel = lights.GetComponentsInChildren<Renderer>()
                .First(r => r.gameObject.name == "LED Panel");
            Material material = panel.sharedMaterial;
            Assert.That(material, Is.Not.Null);
            if (!material.HasProperty("_EmissionColor"))
                Assert.Ignore("The active render pipeline's shader has no emission property.");

            Color emission = material.GetColor("_EmissionColor");
            Assert.That(emission.maxColorComponent, Is.GreaterThan(1f), "The panel face reads as lit.");
            Assert.That(emission.b, Is.GreaterThanOrEqualTo(emission.r),
                "Office lighting is cool white, not warm.");
        }
    }
}
