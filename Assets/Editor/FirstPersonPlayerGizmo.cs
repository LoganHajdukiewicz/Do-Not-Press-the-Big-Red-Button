using UnityEditor;
using UnityEngine;

namespace BigRedButton.Editor
{
    /// <summary>
    /// Scene-view-only representation of the otherwise camera-only first-person player.
    /// This lives in the Editor assembly, so no marker, renderer, or collider exists in
    /// Play Mode or in a build.
    /// </summary>
    public static class FirstPersonPlayerGizmo
    {
        private static readonly Color BodyColour = new Color(0.18f, 0.62f, 1f, 0.22f);
        private static readonly Color OutlineColour = new Color(0.18f, 0.72f, 1f, 0.95f);

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawPlayer(FirstPersonController player, GizmoType gizmoType)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null)
                return;

            Transform transform = player.transform;
            Vector3 centre = transform.TransformPoint(controller.center);
            float radius = controller.radius;
            float height = Mathf.Max(controller.height, radius * 2f);
            float cylinderHeight = Mathf.Max(0f, height - radius * 2f);

            // A translucent capsule at the actual collision dimensions makes the spawn
            // position clear without pretending that the first-person player has a body.
            Gizmos.color = BodyColour;
            Gizmos.DrawSphere(centre + Vector3.up * cylinderHeight * 0.5f, radius);
            Gizmos.DrawSphere(centre - Vector3.up * cylinderHeight * 0.5f, radius);
            Gizmos.DrawCube(centre, new Vector3(radius * 2f, cylinderHeight, radius * 2f));

            Gizmos.color = OutlineColour;
            Gizmos.DrawWireSphere(centre + Vector3.up * cylinderHeight * 0.5f, radius);
            Gizmos.DrawWireSphere(centre - Vector3.up * cylinderHeight * 0.5f, radius);
            Gizmos.DrawWireCube(centre, new Vector3(radius * 2f, cylinderHeight, radius * 2f));

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Handles.color = OutlineColour;
            Handles.ArrowHandleCap(0, eye, transform.rotation, 0.9f, EventType.Repaint);
            Handles.Label(eye + Vector3.up * 0.35f, "PLAYER");
        }
    }
}
