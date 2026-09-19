using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BigRedButton.Tests
{
    public sealed class FirstPersonTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);
        private PlayerInteractor player;
        private ButtonInteractable button;
        private int presses;

        [SetUp]
        public void SetUp()
        {
            GameObject cameraObject = Track(new GameObject("Test camera", typeof(Camera)));
            cameraObject.transform.position = Origin;
            player = cameraObject.AddComponent<PlayerInteractor>();
            player.SetCamera(cameraObject.GetComponent<Camera>());
            // Restrict queries to the test layer to avoid hitting objects in an open user scene.
            var serialized = new SerializedObject(player);
            serialized.FindProperty("raycastLayers").intValue = 1 << 30;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            button = Cube("Button", 2f).AddComponent<ButtonInteractable>();
            presses = 0;
            button.OnPressed.AddListener(() => presses++);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void LookingAtButtonFindsPromptAndInvokesEvent()
        {
            player.RefreshFocus();
            Assert.That(player.FocusedTarget, Is.SameAs(button));
            Assert.That(player.CurrentPrompt, Is.EqualTo("Press button"));
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(presses, Is.EqualTo(1));
        }

        [Test]
        public void WallPreventsInteraction()
        {
            Cube("Wall", 1f);
            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(presses, Is.Zero);
        }

        [Test]
        public void OutOfRangeButtonCannotBeUsed()
        {
            button.transform.position = Origin + Vector3.forward * 4f;
            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.False);
        }

        [Test]
        public void InteractionRechecksPreviouslyFocusedTarget()
        {
            player.RefreshFocus();
            Assert.That(player.FocusedTarget, Is.SameAs(button));
            Cube("New obstruction", 1f);
            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.False);
        }

        [Test]
        public void DisabledButtonCannotBeUsed()
        {
            button.enabled = false;
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(player.CurrentPrompt, Is.Empty);
        }

        [Test]
        public void LockedButtonCanBeUnlocked()
        {
            button.SetInteractable(false);
            Assert.That(player.TryInteract(), Is.False);
            button.SetInteractable(true);
            Assert.That(player.TryInteract(), Is.True);
        }

        [Test]
        public void TriggerVolumesDoNotBlockInteraction()
        {
            Cube("Trigger", 1f).GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.True);
        }

        [Test]
        public void ChildColliderFindsParentInteractable()
        {
            Object.DestroyImmediate(button.GetComponent<Collider>());
            GameObject child = Cube("Child collider", 2f);
            child.transform.SetParent(button.transform, true);
            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.True);
        }

        [Test]
        public void CooldownPreventsRepeatedPresses()
        {
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(presses, Is.EqualTo(1));
            button.ResetButton();
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(presses, Is.EqualTo(2));
        }

        [Test]
        public void OneShotButtonRequiresReset()
        {
            var serialized = new SerializedObject(button);
            serialized.FindProperty("oneShot").boolValue = true;
            serialized.FindProperty("cooldown").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(player.TryInteract(), Is.False);
            button.ResetButton();
            Assert.That(player.TryInteract(), Is.True);
        }

        [Test]
        public void DestroyedTargetDoesNotLeaveAStalePrompt()
        {
            player.RefreshFocus();
            Object.DestroyImmediate(button.gameObject);
            Assert.That(player.CurrentPrompt, Is.Empty);
            Assert.That(player.TryInteract(), Is.False);
        }

        [Test]
        public void DisabledInteractorCannotUseButtons()
        {
            player.enabled = false;
            Assert.That(player.TryInteract(), Is.False);
        }

        [TestCase("Move")]
        [TestCase("Look")]
        [TestCase("Jump")]
        [TestCase("Sprint")]
        [TestCase("Interact")]
        public void ProjectHasRequiredInputActions(string actionName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(asset, Is.Not.Null);
            InputAction action = asset.FindAction("Player/" + actionName, true);
            Assert.That(action.bindings.Count, Is.GreaterThan(0));
            if (actionName == "Interact")
                Assert.That(action.interactions, Is.Null.Or.Empty, "Interaction should be a press, not a hold.");
        }

        private GameObject Cube(string name, float distance)
        {
            GameObject item = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            item.name = name;
            item.layer = 30;
            item.transform.position = Origin + Vector3.forward * distance;
            item.transform.localScale = Vector3.one * 0.5f;
            return item;
        }

        private GameObject Track(GameObject item)
        {
            objects.Add(item);
            return item;
        }
    }
}
