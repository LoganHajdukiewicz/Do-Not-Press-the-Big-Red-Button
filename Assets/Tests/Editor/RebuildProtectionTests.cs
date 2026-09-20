using System.IO;
using System.Linq;
using System.Reflection;
using BigRedButton.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BigRedButton.Tests
{
    public sealed class RebuildProtectionTests
    {
        [Test]
        public void RebuildPlanContainsOnlyDaysSixThroughThirtyOne()
        {
            Assert.That(DayLevelBuilder.RebuildDayNumbers().ToArray(),
                Is.EqualTo(Enumerable.Range(6, 26).ToArray()));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FinishedDayIsNeverInTheRebuildPlan(int day)
        {
            int[] plan = DayLevelBuilder.RebuildDayNumbers().ToArray();

            // Deliberately plain LINQ instead of Does.Not.Contain. That constraint has
            // both (object) and (string) overloads, so passing an int is an ambiguous
            // call, and passing a string compiles but always passes: a string never
            // equals an int, which would hide Days 1-5 becoming rebuildable.
            Assert.That(plan.Contains(day), Is.False,
                $"Day {day} is finished work and must never be regenerated. Plan: " +
                string.Join(", ", plan));
            Assert.That(plan, Is.Not.Empty, "A silently empty plan must not pass this test.");
        }

        [Test]
        public void TheGuardItselfIsTestable()
        {
            // Proves the assertion above can fail: the same check against a day that IS
            // in the plan must report true, so a passing suite means something.
            int[] plan = DayLevelBuilder.RebuildDayNumbers().ToArray();
            Assert.That(plan.Contains(6), Is.True, "Day 6 is rebuildable, so it is in the plan.");
            Assert.That(plan.Contains(31), Is.True, "Day 31 is rebuildable, so it is in the plan.");
        }

        [Test]
        public void ReusingAMaterialDoesNotResetItsAuthoredSettingsOrSavedBytes()
        {
            // Use an isolated asset, never an actual shared level material.
            const string folder = "Assets/LevelMaterials";
            bool createdFolder = !AssetDatabase.IsValidFolder(folder);
            if (createdFolder)
                AssetDatabase.CreateFolder("Assets", "LevelMaterials");
            string name = "RebuildProtectionTest_" + System.Guid.NewGuid().ToString("N");
            string path = folder + "/" + name + ".mat";
            try
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null);
                var original = new Material(shader) { color = Color.magenta };
                original.SetFloat("_Smoothness", 0.91f);
                AssetDatabase.CreateAsset(original, path);
                AssetDatabase.SaveAssetIfDirty(original);
                byte[] materialBefore = File.ReadAllBytes(path);
                byte[] metaBefore = File.ReadAllBytes(path + ".meta");

                MethodInfo factory = typeof(DayLevelBuilder).GetMethod("Material",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(factory, Is.Not.Null);
                var reused = (Material)factory.Invoke(null, new object[] { name, Color.red, 0.1f });

                Assert.That(reused, Is.SameAs(original));
                Assert.That(reused.shader, Is.SameAs(shader));
                Assert.That(reused.color, Is.EqualTo(Color.magenta));
                Assert.That(reused.GetFloat("_Smoothness"), Is.EqualTo(0.91f).Within(0.001f));
                Assert.That(EditorUtility.IsDirty(reused), Is.False);
                AssetDatabase.SaveAssetIfDirty(reused);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(materialBefore));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metaBefore));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (createdFolder)
                    AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
