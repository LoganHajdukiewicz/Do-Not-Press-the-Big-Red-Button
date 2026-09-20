using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Day 31: step outside, explore freely for five seconds, then fade to black
    /// and receive the Employee of the Month card. No gunshot or white flash.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class EndingSequence : MonoBehaviour
    {
        private enum Stage { Waiting, Exploring, Fading, Dark, Card, Finished }

        [Header("Outside trigger")]
        [Tooltip("World-space centre of the area just beyond the doorway.")]
        [SerializeField] private Vector3 doorwayCentre = new Vector3(0f, 0f, 9f);
        [SerializeField, Min(0.2f)] private float doorwayRadius = 1.5f;
        [SerializeField, Min(0f)] private float doorwayHeightTolerance = 2.5f;

        [Header("Exploration")]
        [Tooltip("Seconds of free exploration after stepping outside, before any fade begins.")]
        [SerializeField, Min(0f)] private float explorationDuration = 5f;

        [Header("Fade to black")]
        [Tooltip("Seconds to fade the world to black. Movement remains available during the fade.")]
        [SerializeField, Min(0f)] private float fadeToBlackDuration = 2f;
        [Tooltip("Seconds of darkness before the card begins to appear.")]
        [SerializeField, Min(0f)] private float darkHold = 0.5f;

        [Header("Employee of the month card")]
        [Tooltip("Uses {WORKER-FIRSTNAME} and {WORKER-LASTNAME} for the worker's name.")]
        [TextArea(2, 4)]
        [SerializeField] private string cardText =
            "Employee of the Month\n{WORKER-FIRSTNAME} {WORKER-LASTNAME}";
        [SerializeField, Min(0f)] private float cardFadeIn = 2.5f;
        [SerializeField, Range(0.02f, 0.12f)] private float cardHeightFraction = 0.045f;
        [SerializeField] private Color cardColour = new Color(0.86f, 0.86f, 0.83f);

        [Header("Player")]
        [Tooltip("Found from the player controller if left empty.")]
        [SerializeField] private Transform player;
        [Tooltip("Disabled only AFTER the screen becomes fully black, never during exploration.")]
        [SerializeField] private MonoBehaviour frozenDuringEnding;

        [Header("Events")]
        [Tooltip("Fires when the player steps outside and the exploration timer starts.")]
        [SerializeField] private UnityEvent onEndingStarted = new UnityEvent();
        [SerializeField] private UnityEvent onFadeStarted = new UnityEvent();
        [SerializeField] private UnityEvent onCardShown = new UnityEvent();

        private Stage stage = Stage.Waiting;
        private GUIStyle style;
        private float timer;
        private bool controlsFrozen;

        public float OverlayAlpha { get; private set; }
        public Color OverlayColour => Color.black;
        public float CardAlpha { get; private set; }
        public bool HasStarted => stage != Stage.Waiting;
        public bool IsExploring => stage == Stage.Exploring;
        public bool IsFading => stage == Stage.Fading;
        public string CardText => WorkerIdentity.Format(cardText);
        public UnityEvent OnEndingStarted => onEndingStarted;
        public UnityEvent OnFadeStarted => onFadeStarted;
        public UnityEvent OnCardShown => onCardShown;

        /// <summary>Starts the one-shot exploration timer. Does not take away control.</summary>
        public void Begin()
        {
            if (stage != Stage.Waiting)
                return;
            ResolvePlayer();
            stage = Stage.Exploring;
            timer = 0f;
            OverlayAlpha = 0f;
            onEndingStarted.Invoke();
        }

        private void ResolvePlayer()
        {
            if (frozenDuringEnding == null)
            {
                var controller = player != null
                    ? player.GetComponentInParent<FirstPersonController>()
                    : FindFirstObjectByType<FirstPersonController>();
                frozenDuringEnding = controller;
            }
            if (player == null && frozenDuringEnding != null)
                player = frozenDuringEnding.transform;
        }

        private bool CanExplore()
        {
            if (Time.timeScale <= 0f)
                return false;
            if (frozenDuringEnding != null && !frozenDuringEnding.isActiveAndEnabled)
                return false;
            // Escape/unfocused input must not use up the short exploration window.
            return !(frozenDuringEnding is FirstPersonController controller) || controller.HasControl;
        }

        private void Update()
        {
            if (stage == Stage.Waiting)
                CheckDoorway();
            else
                AdvanceSequence(Time.deltaTime);
        }

        private void CheckDoorway()
        {
            ResolvePlayer();
            if (player == null || !CanExplore())
                return;
            Vector3 offset = player.position - doorwayCentre;
            if (Mathf.Abs(offset.y) > doorwayHeightTolerance)
                return;
            offset.y = 0f;
            if (offset.sqrMagnitude <= doorwayRadius * doorwayRadius)
                Begin();
        }

        private void AdvanceSequence(float deltaTime)
        {
            if (deltaTime <= 0f || Time.timeScale <= 0f)
                return;
            if ((stage == Stage.Exploring || stage == Stage.Fading) && !CanExplore())
                return;

            timer += deltaTime;
            switch (stage)
            {
                case Stage.Exploring:
                    if (timer < explorationDuration)
                        return;
                    stage = Stage.Fading;
                    timer = 0f;
                    onFadeStarted.Invoke();
                    break;
                case Stage.Fading:
                    OverlayAlpha = fadeToBlackDuration <= 0f ? 1f :
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / fadeToBlackDuration));
                    if (OverlayAlpha < 1f)
                        return;
                    stage = Stage.Dark;
                    timer = 0f;
                    FreezeControls();
                    break;
                case Stage.Dark:
                    if (timer < darkHold)
                        return;
                    stage = Stage.Card;
                    timer = 0f;
                    onCardShown.Invoke();
                    break;
                case Stage.Card:
                    CardAlpha = cardFadeIn <= 0f ? 1f : Mathf.Clamp01(timer / cardFadeIn);
                    if (CardAlpha >= 1f)
                        stage = Stage.Finished;
                    break;
            }
        }

        private void FreezeControls()
        {
            if (controlsFrozen || frozenDuringEnding == null || !frozenDuringEnding.enabled)
                return;
            controlsFrozen = true;
            frozenDuringEnding.enabled = false;
        }

        private void OnEnable()
        {
            if (stage is Stage.Dark or Stage.Card or Stage.Finished)
                FreezeControls();
        }

        private void OnDisable()
        {
            if (controlsFrozen && frozenDuringEnding != null)
                frozenDuringEnding.enabled = true;
            controlsFrozen = false;
        }

        private void OnGUI()
        {
            if (OverlayAlpha <= 0f)
                return;
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previousColour = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -100;
            GUI.color = new Color(0f, 0f, 0f, OverlayAlpha);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            if (CardAlpha > 0f)
            {
                if (style == null)
                    style = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                style.fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * cardHeightFraction));
                style.normal.textColor =
                    new Color(cardColour.r, cardColour.g, cardColour.b, cardColour.a * CardAlpha);
                GUI.color = Color.white;
                GUI.Label(full, CardText, style);
            }
            GUI.color = previousColour;
            GUI.depth = previousDepth;
        }
    }
}
