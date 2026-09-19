using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    [DisallowMultipleComponent]
    public sealed class ButtonInteractable : Interactable
    {
        [SerializeField] private string prompt = "Press button";
        [SerializeField] private bool interactable = true;
        [SerializeField] private bool oneShot;
        [SerializeField, Min(0f)] private float cooldown = 0.25f;
        [SerializeField] private UnityEvent onPressed = new UnityEvent();

        private bool hasBeenPressed;
        private float nextPressTime = float.NegativeInfinity;

        public override string Prompt => prompt;
        public override bool CanInteract => base.CanInteract && interactable &&
            !(oneShot && hasBeenPressed) && Time.time >= nextPressTime;
        public UnityEvent OnPressed => onPressed;

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract)
                return;

            hasBeenPressed = true;
            nextPressTime = Time.time + cooldown;
            onPressed.Invoke();
        }

        // These methods can also be connected in another button's Inspector UnityEvent.
        public void SetInteractable(bool value) => interactable = value;

        public void ResetButton()
        {
            hasBeenPressed = false;
            nextPressTime = float.NegativeInfinity;
        }

        public void LogPress() => Debug.Log($"{name} pressed.", this);
    }
}
