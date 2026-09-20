using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class ForwardTrapFloorTests
    {
        private GameObject floor;
        private ForwardTrapFloor trap;

        [SetUp]
        public void SetUp()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trap = floor.AddComponent<ForwardTrapFloor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (floor != null)
                Object.DestroyImmediate(floor);
        }

        [Test]
        public void EitherForwardKeyCollapsesTheFloor()
        {
            // W and the Up arrow both walk forward, so both must spring the trap.
            trap.ProcessInput(true, false, true);
            Assert.That(trap.HasCollapsed, Is.True);
        }

        [UnityTest]
        public IEnumerator WRemovesCollisionImmediatelyThenDeletesTheFloor()
        {
            int collapsed = 0;
            trap.OnCollapsed.AddListener(() => collapsed++);
            trap.ProcessInput(false, false, true);
            Assert.That(floor.GetComponent<Collider>().enabled, Is.True);
            trap.ProcessInput(true, false, true);
            trap.ProcessInput(true, false, true);
            Assert.That(collapsed, Is.EqualTo(1));
            Assert.That(floor.GetComponent<Collider>().enabled, Is.False);
            Assert.That(floor.GetComponent<Renderer>().enabled, Is.False);
            yield return null;
            Assert.That(floor == null, Is.True);
        }

        [Test]
        public void PausedUncapturedOrDisarmedFloorDoesNotCollapse()
        {
            trap.ProcessInput(true, false, false);
            Assert.That(trap.HasCollapsed, Is.False);
            trap.Disarm();
            trap.ProcessInput(true, false, true);
            Assert.That(trap.HasCollapsed, Is.False);
        }

        [UnityTest]
        public IEnumerator GamepadRequiresFreshForwardEdge()
        {
            trap.ProcessInput(false, true, true);
            Assert.That(trap.HasCollapsed, Is.False, "Do not trigger on stick held from previous day.");
            trap.ProcessInput(false, false, true);
            trap.ProcessInput(false, true, true);
            Assert.That(trap.HasCollapsed, Is.True);
            yield return null;
            Assert.That(floor == null, Is.True);
        }

        [UnityTest]
        public IEnumerator RemovingFloorAllowsACharacterControllerToFallThrough()
        {
            floor.transform.position = new Vector3(100f, -0.25f, 100f);
            floor.transform.localScale = new Vector3(12f, 0.5f, 12f);
            var player = new GameObject("Falling test player");
            try
            {
                var character = player.AddComponent<CharacterController>();
                character.height = 1.8f;
                character.center = Vector3.up * 0.9f;
                character.radius = 0.3f;
                player.transform.position = new Vector3(100f, 0.1f, 100f);
                Physics.SyncTransforms();
                character.Move(Vector3.down);
                Assert.That(player.transform.position.y, Is.GreaterThan(-0.2f));
                trap.ProcessInput(true, false, true);
                Physics.SyncTransforms();
                character.Move(Vector3.down * 2f);
                Assert.That(player.transform.position.y, Is.LessThan(-1f));
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
