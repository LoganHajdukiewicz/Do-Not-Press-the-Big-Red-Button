using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>Routes physical player/button contact through the normal button interaction.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerInteractor))]
    public sealed class PlayerButtonContact : MonoBehaviour
    {
        // A small physics contact tolerance, not an interaction/proximity radius.
        private const float ContactTolerance = 0.01f;
        private readonly HashSet<ButtonInteractable> contacts = new HashSet<ButtonInteractable>();
        private readonly HashSet<ButtonInteractable> pressedContacts = new HashSet<ButtonInteractable>();
        private readonly List<ButtonInteractable> candidates = new List<ButtonInteractable>();
        private Collider[] overlaps = new Collider[16];
        private CharacterController character;
        private PlayerInteractor interactor;

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            interactor = GetComponent<PlayerInteractor>();
        }

        private bool CanCheckContacts => isActiveAndEnabled && character != null &&
            character.enabled && character.detectCollisions && interactor != null && interactor.isActiveAndEnabled;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (CanCheckContacts && Time.timeScale > 0f)
                RecordContact(hit.collider);
        }

        private void LateUpdate()
        {
            if (!CanCheckContacts)
            {
                ClearContacts();
                return;
            }

            if (Time.timeScale <= 0f)
            {
                contacts.Clear();
                return; // Keep the held-contact latch when pausing, without firing events.
            }

            // CharacterController hit callbacks only happen during Move(). This catches
            // standing contact and a moving button touching an otherwise stationary player.
            // Sync once so buttons moved by Transform/animation are visible to the query.
            Physics.SyncTransforms();
            Vector3 scale = transform.lossyScale;
            float radius = character.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(character.height * Mathf.Abs(scale.y), radius * 2f);
            Vector3 center = transform.TransformPoint(character.center);
            Vector3 halfSegment = transform.up * (height * 0.5f - radius);
            int count;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(center - halfSegment, center + halfSegment,
                    radius + ContactTolerance, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count < overlaps.Length)
                    break;
                // Do not silently miss a button in a crowded area.
                Array.Resize(ref overlaps, overlaps.Length * 2);
            }

            for (int i = 0; i < count; i++)
            {
                RecordContact(overlaps[i]);
                overlaps[i] = null;
            }

            pressedContacts.IntersectWith(contacts);
            candidates.Clear();
            candidates.AddRange(contacts);
            contacts.Clear();

            // Invoke outside physics callbacks and outside HashSet enumeration: an OnPressed
            // listener is allowed to disable/destroy a button, the player, or change scenes.
            for (int i = 0; i < candidates.Count; i++)
            {
                ButtonInteractable button = candidates[i];
                if (button == null || pressedContacts.Contains(button))
                    continue;

                // E and collision during the same frame represent a single press, even with
                // zero cooldown. Manual E presses on later frames are still allowed.
                if (button.LastPressedFrame == Time.frameCount)
                {
                    pressedContacts.Add(button);
                    continue;
                }

                pressedContacts.Add(button);
                if (!button.TryPressFromContact(interactor))
                    pressedContacts.Remove(button); // Retry locked/cooling buttons while still touching.

                if (this == null || !CanCheckContacts)
                    return;
            }
            candidates.Clear();
        }

        private void RecordContact(Collider other)
        {
            if (other == null || other == character || other.isTrigger || !other.enabled ||
                other.transform.IsChildOf(transform) ||
                Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer) ||
                Physics.GetIgnoreCollision(character, other))
                return;

            // Only the actual hit collider or its ancestors count. A pedestal beside a
            // button is not a press unless the button component is on that pedestal's parent.
            ButtonInteractable button = other.GetComponentInParent<ButtonInteractable>();
            if (button != null)
                contacts.Add(button);
        }

        private void ClearContacts()
        {
            contacts.Clear();
            pressedContacts.Clear();
            candidates.Clear();
        }

        private void OnDisable() => ClearContacts();
    }
}
