using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>Hides an object for a few seconds, then reveals it. Used by Day 3.</summary>
    [DisallowMultipleComponent]
    public sealed class TimedReveal : MonoBehaviour
    {
        [Tooltip("Object to reveal. Leave empty to reveal the object this is attached to.")]
        [SerializeField] private GameObject target;
        [Tooltip("Seconds the target stays hidden after the day starts.")]
        [SerializeField, Min(0f)] private float delay = 5f;
        [SerializeField] private UnityEvent onRevealed = new UnityEvent();

        private float remaining;
        private bool waiting;

        public float Remaining => waiting ? Mathf.Max(0f, remaining) : 0f;
        public UnityEvent OnRevealed => onRevealed;

        private GameObject Target => target != null ? target : gameObject;

        private void Awake()
        {
            remaining = delay;
            waiting = true;
            // Hide immediately so the object never flashes on the first frame.
            if (Target != gameObject)
                Target.SetActive(false);
        }

        private void Update()
        {
            if (!waiting)
                return;

            remaining -= Time.deltaTime;
            if (remaining > 0f)
                return;

            waiting = false;
            Target.SetActive(true);
            onRevealed.Invoke();
        }
    }
}
