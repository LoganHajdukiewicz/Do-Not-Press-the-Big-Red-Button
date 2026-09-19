using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class ButtonContactTests
    {
        private static readonly Vector3 Origin = new Vector3(100f, 100f, 100f);
        private readonly List<GameObject> objects = new List<GameObject>();
        private CharacterController character;
        private PlayerInteractor interactor;
        private PlayerButtonContact contact;
        private ButtonInteractable button;
        private int presses;
        private float previousTimeScale;
        private bool previousIgnoreLayers;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousIgnoreLayers = Physics.GetIgnoreLayerCollision(29, 30);
            Physics.IgnoreLayerCollision(29, 30, false);
            GameObject player = Track(new GameObject("Contact test player"));
            player.layer = 29;
            player.transform.position = Origin;
            character = player.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.3f;
            character.center = Vector3.up * 0.9f;
            character.skinWidth = 0.03f;
            character.minMoveDistance = 0f;
            interactor = player.AddComponent<PlayerInteractor>();
            contact = player.AddComponent<PlayerButtonContact>();
            button = Cube("Contact test button", new Vector3(2f, 0.9f, 0f)).AddComponent<ButtonInteractable>();
            SetButtonField("cooldown", 0f);
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
            Physics.IgnoreLayerCollision(29, 30, previousIgnoreLayers);
            Time.timeScale = previousTimeScale;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator FallingOntoButtonPressesIt()
        {
            MakeFloorButton();
            character.transform.position = Origin + Vector3.up * 2f;
            Physics.SyncTransforms();
            character.Move(Vector3.down * 3f);
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StandingOnButtonPressesOnlyOnceUntilSeparation()
        {
            MakeFloorButton();
            yield return null;
            yield return null;
            yield return null;
            Assert.That(presses, Is.EqualTo(1), "Continuous contact must not spam, even with zero cooldown.");
            character.transform.position = Origin + Vector3.up * 3f;
            yield return null;
            character.transform.position = Origin;
            yield return null;
            Assert.That(presses, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator WalkingIntoSideOfButtonPressesIt()
        {
            character.Move(Vector3.right * 2f);
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MovingButtonTouchesStationaryPlayerWithoutControllerMove()
        {
            yield return null;
            Assert.That(presses, Is.Zero);
            TouchSide();
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator KinematicButtonCanMoveIntoPlayer()
        {
            Rigidbody body = button.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.MovePosition(Origin + new Vector3(0.55f, 0.9f, 0f));
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NearbyButtonAndTriggerDoNotCountAsPhysicalContact()
        {
            button.transform.position = Origin + new Vector3(0.7f, 0.9f, 0f);
            yield return null;
            Assert.That(presses, Is.Zero, "A 15 cm gap is not touching.");
            button.GetComponent<Collider>().isTrigger = true;
            TouchSide();
            yield return null;
            Assert.That(presses, Is.Zero);
        }

        [UnityTest]
        public IEnumerator IgnoredCollisionPairsAndLayersDoNotPressButtons()
        {
            Collider collider = button.GetComponent<Collider>();
            Physics.IgnoreCollision(character, collider, true);
            TouchSide();
            yield return null;
            Assert.That(presses, Is.Zero);
            Physics.IgnoreCollision(character, collider, false);
            Physics.IgnoreLayerCollision(29, 30, true);
            yield return null;
            Assert.That(presses, Is.Zero);
            Physics.IgnoreLayerCollision(29, 30, false);
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ChildCollidersPressParentButtonOnlyOnce()
        {
            Object.DestroyImmediate(button.GetComponent<Collider>());
            for (int i = 0; i < 3; i++)
            {
                GameObject child = Cube("Button contact surface", new Vector3(0.55f, 0.9f, 0f));
                child.transform.SetParent(button.transform, true);
            }
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TouchingPedestalDoesNotPressItsChildButton()
        {
            GameObject pedestal = Cube("Pedestal", new Vector3(0.55f, 0.9f, 0f));
            button.transform.SetParent(pedestal.transform, true); // Button itself remains out of reach.
            yield return null;
            Assert.That(presses, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LockedAndDisabledButtonsWaitUntilAvailable()
        {
            button.SetInteractable(false);
            TouchSide();
            yield return null;
            Assert.That(presses, Is.Zero);
            button.SetInteractable(true);
            button.enabled = false;
            yield return null;
            Assert.That(presses, Is.Zero);
            button.enabled = true;
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContactAndManualInteractionShareCooldownAndOneShotRules()
        {
            SetButtonField("cooldown", 100f);
            button.Interact(interactor);
            yield return null; // Start contact on a later frame than the manual press.
            TouchSide();
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
            button.ResetButton();
            SetButtonField("oneShot", true);
            yield return null;
            Assert.That(presses, Is.EqualTo(2));
            button.Interact(interactor);
            Assert.That(presses, Is.EqualTo(2));
            character.transform.position = Origin + Vector3.left * 3f;
            yield return null;
            character.transform.position = Origin;
            yield return null;
            Assert.That(presses, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ManualPressAndTouchInSameFrameDoNotDoubleFire()
        {
            TouchSide();
            button.Interact(interactor);
            yield return null;
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContactCanBeDisabledWithoutDisablingManualInteraction()
        {
            SetButtonField("pressOnContact", false);
            TouchSide();
            yield return null;
            Assert.That(presses, Is.Zero);
            button.Interact(interactor);
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PausedOrDisabledPlayerDoesNotPressButtons()
        {
            Time.timeScale = 0f;
            TouchSide();
            yield return null;
            Assert.That(presses, Is.Zero);
            Time.timeScale = 1f;
            interactor.enabled = false;
            yield return null;
            Assert.That(presses, Is.Zero);
            interactor.enabled = true;
            character.detectCollisions = false;
            yield return null;
            Assert.That(presses, Is.Zero);
            character.detectCollisions = true;
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ButtonEventCanDisablePlayerSafely()
        {
            button.OnPressed.AddListener(() => contact.gameObject.SetActive(false));
            TouchSide();
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CrowdedContactsDoNotTruncateButtonDetection()
        {
            Object.DestroyImmediate(button.GetComponent<Collider>());
            for (int i = 0; i < 40; i++)
            {
                GameObject child = Cube("Crowded contact", new Vector3(0.55f, 0.9f, 0f));
                child.transform.SetParent(button.transform, true);
            }
            yield return null;
            Assert.That(presses, Is.EqualTo(1));
        }

        private void TouchSide() => button.transform.position = Origin + new Vector3(0.55f, 0.9f, 0f);

        private void MakeFloorButton()
        {
            button.transform.position = Origin + Vector3.down * 0.1f;
            button.transform.localScale = new Vector3(2f, 0.2f, 2f);
        }

        private void SetButtonField(string field, object value) =>
            typeof(ButtonInteractable).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(button, value);

        private GameObject Cube(string name, Vector3 offset)
        {
            GameObject item = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            item.name = name;
            item.layer = 30;
            item.transform.position = Origin + offset;
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
