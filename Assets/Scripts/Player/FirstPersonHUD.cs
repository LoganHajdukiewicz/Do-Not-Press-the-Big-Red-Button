using UnityEngine;

namespace BigRedButton
{
    /// <summary>Optional prototype HUD; remove when replacing it with the game's UI.</summary>
    [RequireComponent(typeof(FirstPersonController), typeof(PlayerInteractor))]
    public sealed class FirstPersonHUD : MonoBehaviour
    {
        private FirstPersonController controller;
        private PlayerInteractor interactor;
        private GUIStyle centered;

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            interactor = GetComponent<PlayerInteractor>();
        }

        private void OnGUI()
        {
            if (!controller.isActiveAndEnabled || Time.timeScale <= 0f)
                return;

            if (centered == null)
                centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };

            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            if (!controller.HasControl)
            {
                GUI.Label(new Rect(0f, y, Screen.width, 40f), "Click / Esc / Start to resume", centered);
                return;
            }

            GUI.Label(new Rect(x - 10f, y - 10f, 20f, 20f), "+", centered);
            if (!string.IsNullOrEmpty(interactor.CurrentPrompt))
                GUI.Label(new Rect(0f, y + 28f, Screen.width, 40f),
                    $"[{controller.InteractionHint}] {interactor.CurrentPrompt}", centered);
        }
    }
}
