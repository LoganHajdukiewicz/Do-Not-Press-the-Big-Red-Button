using UnityEngine;

namespace BigRedButton
{
    /// <summary>Attach an implementation to a solid collider or one of its parents.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public virtual bool CanInteract => isActiveAndEnabled;
        public abstract void Interact(PlayerInteractor player);
    }
}
