using UnityEngine;

namespace BigRedButton
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [Tooltip("Include walls as well as interactables so objects cannot be used through walls.")]
        [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;

        public Interactable FocusedTarget { get; private set; }
        public string CurrentPrompt => FocusedTarget != null && FocusedTarget.CanInteract
            ? FocusedTarget.Prompt : string.Empty;

        public void SetCamera(Camera cameraToUse)
        {
            viewCamera = cameraToUse;
            ClearFocus();
        }

        public void RefreshFocus()
        {
            ClearFocus();
            if (!isActiveAndEnabled || viewCamera == null)
                return;

            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Start at the eye, rather than the near clip plane, to respect close occluders.
            ray.origin = viewCamera.transform.position;
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, raycastLayers,
                    QueryTriggerInteraction.Ignore))
                return;

            Interactable target = hit.collider.GetComponentInParent<Interactable>();
            if (target != null && target.CanInteract)
                FocusedTarget = target;
        }

        public bool TryInteract()
        {
            // Recheck range, occlusion, and availability instead of trusting a stale target.
            RefreshFocus();
            if (FocusedTarget == null)
                return false;

            FocusedTarget.Interact(this);
            RefreshFocus();
            return true;
        }

        public void ClearFocus() => FocusedTarget = null;

        private void OnDisable() => ClearFocus();
    }
}
