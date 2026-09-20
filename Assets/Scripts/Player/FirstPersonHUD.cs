using UnityEngine;

namespace BigRedButton
{
    /// <summary>Optional prototype HUD; remove when replacing it with the game's UI.</summary>
    [RequireComponent(typeof(FirstPersonController), typeof(PlayerInteractor))]
    public sealed class FirstPersonHUD : MonoBehaviour
    {
        private FirstPersonController controller;
        private GUIStyle centered;

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
        }

        private void OnGUI()
        {
            if (!controller.isActiveAndEnabled || Time.timeScale <= 0f)
                return;

            if (centered == null)
                centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };

            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            // Deliberately nonverbal: interaction text reveals button states and spoils
            // the day's surprise. The crosshair remains as a simple aiming reference.
            if (controller.HasControl)
                GUI.Label(new Rect(x - 10f, y - 10f, 20f, 20f), "+", centered);
        }
    }
}
