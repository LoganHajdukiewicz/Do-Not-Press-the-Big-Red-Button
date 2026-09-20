using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BigRedButton.Tests
{
    public sealed class ButtonWallCollisionTests
    {
        private static readonly Vector3 Origin = new Vector3(200f, 0f, 200f);
        private readonly List<GameObject> objects = new List<GameObject>();
        private GameObject root;
        private ButtonMover mover;
        private ButtonInteractable button;
        private Transform cap;

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private GameObject Box(string name, Vector3 localPosition, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(box);
            box.name = name;
            box.transform.position = Origin + localPosition;
            box.transform.localScale = size;
            return box;
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Behind button assembly");
            objects.Add(root);
            root.transform.position = Origin;
            var pedestal = Box("Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
            pedestal.transform.SetParent(root.transform, true);
            var top = Box("Button", new Vector3(0f, 1.2f, 0f), new Vector3(0.7f, 0.2f, 0.7f));
            top.transform.SetParent(root.transform, true);
            cap = top.transform;
            button = top.AddComponent<ButtonInteractable>();
            mover = root.AddComponent<ButtonMover>();
            Set(mover, "mode", ButtonMover.MoveMode.StayBehindPlayer);
            Set(mover, "faceThePlayer", false);
            Box("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(100f, 0.5f, 100f));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private void MoveTo(Vector3 localDestination)
        {
            Physics.SyncTransforms();
            typeof(ButtonMover).GetMethod("MoveWithWalls", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(mover, new object[] { Origin + localDestination });
            Physics.SyncTransforms();
        }

        [Test]
        public void LargeMovementCannotTunnelThroughThinWall()
        {
            Box("Thin wall", new Vector3(2f, 2f, 0f), new Vector3(0.1f, 4f, 10f));
            MoveTo(new Vector3(30f, 0f, 0f));
            Assert.That(root.transform.position.x - Origin.x, Is.LessThan(1.3f));
            Assert.That(mover.IsBlockedByWall, Is.True);
            Assert.That(root.transform.position.y, Is.EqualTo(Origin.y));
        }

        [Test]
        public void SlidesAlongWallRatherThanPassingThroughIt()
        {
            Box("Wall", new Vector3(2f, 2f, 0f), new Vector3(0.2f, 4f, 20f));
            MoveTo(new Vector3(5f, 0f, 3f));
            Assert.That(root.transform.position.x - Origin.x, Is.LessThan(1.3f));
            Assert.That(root.transform.position.z - Origin.z, Is.GreaterThan(2f));
        }

        [Test]
        public void CornerPinsTheButtonAndItRemainsClickable()
        {
            Box("East wall", new Vector3(2f, 2f, 0f), new Vector3(0.2f, 4f, 10f));
            Box("North wall", new Vector3(0f, 2f, 2f), new Vector3(10f, 4f, 0.2f));
            MoveTo(new Vector3(5f, 0f, 5f));
            Vector3 corner = root.transform.position;
            MoveTo(new Vector3(20f, 0f, 20f));
            Assert.That(Vector3.Distance(corner, root.transform.position), Is.LessThan(0.08f));
            Assert.That(root.transform.position.x - Origin.x, Is.LessThan(1.3f));
            Assert.That(root.transform.position.z - Origin.z, Is.LessThan(1.3f));
            var eye = new GameObject("Click camera", typeof(Camera), typeof(PlayerInteractor));
            objects.Add(eye);
            eye.transform.position = cap.position + Vector3.back * 2f;
            eye.transform.rotation = Quaternion.identity;
            var interactor = eye.GetComponent<PlayerInteractor>();
            interactor.SetCamera(eye.GetComponent<Camera>());
            int clicks = 0;
            button.OnPressed.AddListener(() => clicks++);
            Assert.That(interactor.TryInteract(), Is.True);
            Assert.That(clicks, Is.EqualTo(1));
        }

        [Test]
        public void FloorsAndTriggerVolumesDoNotBlockHorizontalMovement()
        {
            Box("Trigger", new Vector3(2f, 1f, 0f), new Vector3(0.2f, 2f, 5f))
                .GetComponent<Collider>().isTrigger = true;
            MoveTo(new Vector3(5f, 0f, 0f));
            Assert.That(root.transform.position.x - Origin.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(mover.IsBlockedByWall, Is.False);
        }

        [Test]
        public void WallMaskAndToggleAreRespected()
        {
            Box("Wall", new Vector3(2f, 2f, 0f), new Vector3(0.2f, 4f, 10f));
            Set(mover, "wallLayers", (LayerMask)0);
            MoveTo(new Vector3(5f, 0f, 0f));
            Assert.That(root.transform.position.x - Origin.x, Is.EqualTo(5f).Within(0.001f));
            root.transform.position = Origin;
            Set(mover, "wallLayers", (LayerMask)Physics.DefaultRaycastLayers);
            Set(mover, "collideWithWalls", false);
            MoveTo(new Vector3(5f, 0f, 0f));
            Assert.That(root.transform.position.x - Origin.x, Is.EqualTo(5f).Within(0.001f));
        }
    }
}
