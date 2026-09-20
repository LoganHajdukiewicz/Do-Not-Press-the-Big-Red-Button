using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BigRedButton.Tests
{
    public sealed class MagnetRegressionTests
    {
        private GameObject cameraObject, buttonObject;
        private Camera camera;
        private CursorRepellingButton magnet;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Test camera", typeof(Camera));
            camera = cameraObject.GetComponent<Camera>();
            buttonObject = new GameObject("Magnet", typeof(CursorRepellingButton));
            magnet = buttonObject.GetComponent<CursorRepellingButton>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(buttonObject);
            Object.DestroyImmediate(cameraObject);
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(0.1f, 0f, 3f)]
        [TestCase(-0.1f, 0f, 3f)]
        [TestCase(0f, 0.1f, 3f)]
        [TestCase(0f, -0.1f, 3f)]
        public void DeflectionIncreasesAngleAwayFromButton(float x, float y, float z)
        {
            buttonObject.transform.position = new Vector3(x, y, z);
            float before = Vector3.Angle(camera.transform.forward, buttonObject.transform.position);
            Vector2 offset = magnet.GetLookOffset(camera, 1f / 60f);
            camera.transform.rotation = Quaternion.Euler(offset.y, offset.x, 0f);
            Assert.That(Vector3.Angle(camera.transform.forward, buttonObject.transform.position),
                Is.GreaterThan(before));
        }

        [Test]
        public void DeadCentrePushIsThreeTimesOriginalAndWorksAtCloseRange()
        {
            buttonObject.transform.position = Vector3.forward;
            Assert.That(magnet.GetLookOffset(camera, 0.01f).magnitude, Is.EqualTo(2.1f).Within(0.001f));
            Assert.That(magnet.GetLookOffset(camera, 0.02f).magnitude, Is.EqualTo(4.2f).Within(0.001f));
        }

        [Test]
        public void NoPushBehindCameraOutsideRangeOrAfterStop()
        {
            buttonObject.transform.position = Vector3.back;
            Assert.That(magnet.GetLookOffset(camera, 0.02f), Is.EqualTo(Vector2.zero));
            buttonObject.transform.position = Vector3.forward * 13f;
            Assert.That(magnet.GetLookOffset(camera, 0.02f), Is.EqualTo(Vector2.zero));
            buttonObject.transform.position = Vector3.forward;
            magnet.Stop();
            Assert.That(magnet.GetLookOffset(camera, 0.02f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ExternalPitchPersistsAndIsClampedByController()
        {
            var root = new GameObject("Inactive player");
            root.SetActive(false); // Configure before Awake; no incomplete live controller.
            try
            {
                camera.transform.SetParent(root.transform, false);
                var controller = root.AddComponent<FirstPersonController>();
                typeof(FirstPersonController).GetField("playerCamera", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(controller, camera);
                controller.ApplyLookOffset(0f, -10f);
                controller.ApplyLookOffset(0f, 0f);
                Assert.That(Mathf.DeltaAngle(0f, camera.transform.localEulerAngles.x), Is.EqualTo(-10f).Within(0.001f));
                controller.ApplyLookOffset(0f, -1000f);
                Assert.That(Mathf.DeltaAngle(0f, camera.transform.localEulerAngles.x), Is.EqualTo(-85f).Within(0.001f));
            }
            finally
            {
                camera.transform.SetParent(null);
                Object.DestroyImmediate(root);
            }
        }
    }
}
