using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace BigRedButton
{
    /// <summary>
    /// Day 31: five seconds outside, a gunshot and white flash, then black
    /// and the Employee of the Month card.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class EndingSequence : MonoBehaviour
    {
        private enum Stage { Waiting, Exploring, Flash, Fading, Dark, Card, CardHold, Returned }

        [Header("Outside trigger")]
        [Tooltip("World-space centre of the area just beyond the doorway.")]
        [SerializeField] private Vector3 doorwayCentre = new Vector3(0f, 0f, 9f);
        [SerializeField, Min(0.2f)] private float doorwayRadius = 1.5f;
        [SerializeField, Min(0f)] private float doorwayHeightTolerance = 2.5f;

        [Header("Exploration")]
        [Tooltip("Seconds of free exploration after stepping outside, before any fade begins.")]
        [SerializeField, Min(0f)] private float explorationDuration = 5f;

        [Header("Gunshot and white flash")]
        [SerializeField] private AudioClip gunshot;
        [SerializeField, Range(0f, 1f)] private float gunshotVolume = 1f;
        [SerializeField, Min(0f)] private float flashDuration = 0.12f;

        [Header("Blackout")]
        [Tooltip("Seconds to take the white flash to black.")]
        [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.06f;
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
        [Tooltip("Seconds the fully visible card remains on screen before returning home.")]
        [SerializeField, Min(0f)] private float cardHoldDuration = 5f;
        [Tooltip("Build-settings scene to load after the ending card.")]
        [SerializeField] private string returnSceneName = "Start Menu";

        [Header("Player")]
        [Tooltip("Found from the player controller if left empty.")]
        [SerializeField] private Transform player;
        [Tooltip("Disabled at the gunshot/flash, after free exploration.")]
        [SerializeField] private MonoBehaviour frozenDuringEnding;

        [Header("Events")]
        [Tooltip("Fires when the player steps outside and the exploration timer starts.")]
        [SerializeField] private UnityEvent onEndingStarted = new UnityEvent();
        [SerializeField] private UnityEvent onGunshot = new UnityEvent();
        [SerializeField] private UnityEvent onFadeStarted = new UnityEvent();
        [SerializeField] private UnityEvent onCardShown = new UnityEvent();

        private Stage stage = Stage.Waiting;
        private GUIStyle style;
        private float timer;
        private bool controlsFrozen;
        private AudioSource shotSource;

        public float OverlayAlpha { get; private set; }
        public Color OverlayColour { get; private set; } = Color.black;
        public bool HasFiredGunshot { get; private set; }
        public bool IsFlashing => stage == Stage.Flash;
        public float CardAlpha { get; private set; }
        public bool HasStarted => stage != Stage.Waiting;
        public bool IsExploring => stage == Stage.Exploring;
        public bool IsFading => stage == Stage.Fading;
        public string CardText => WorkerIdentity.Format(cardText);
        public UnityEvent OnEndingStarted => onEndingStarted;
        public UnityEvent OnGunshot => onGunshot;
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
            if (stage == Stage.Exploring && !CanExplore())
                return;

            timer += deltaTime;
            switch (stage)
            {
                case Stage.Exploring:
                    if (timer < explorationDuration)
                        return;
                    stage = Stage.Flash;
                    timer = 0f;
                    OverlayAlpha = 1f;
                    OverlayColour = Color.white;
                    FreezeControls();
                    HasFiredGunshot = true;
                    if (gunshot != null)
                    {
                        if (shotSource == null)
                        {
                            shotSource = gameObject.AddComponent<AudioSource>();
                            shotSource.playOnAwake = false;
                            shotSource.spatialBlend = 0f;
                        }
                        shotSource.PlayOneShot(gunshot, gunshotVolume);
                    }
                    onGunshot.Invoke();
                    break;
                case Stage.Flash:
                    if (timer < flashDuration)
                        return;
                    stage = Stage.Fading;
                    timer = 0f;
                    onFadeStarted.Invoke();
                    break;
                case Stage.Fading:
                    float fade = fadeToBlackDuration <= 0f ? 1f : Mathf.Clamp01(timer / fadeToBlackDuration);
                    OverlayColour = Color.Lerp(Color.white, Color.black, fade);
                    if (fade < 1f)
                        return;
                    stage = Stage.Dark;
                    timer = 0f;
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
                    {
                        stage = Stage.CardHold;
                        timer = 0f;
                    }
                    break;
                case Stage.CardHold:
                    if (timer < cardHoldDuration)
                        return;
                    stage = Stage.Returned;
                    if (!string.IsNullOrEmpty(returnSceneName))
                        SceneManager.LoadScene(returnSceneName);
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
            if (stage is Stage.Flash or Stage.Fading or Stage.Dark or Stage.Card or Stage.CardHold or Stage.Returned)
                FreezeControls();
        }

        private void OnDisable()
        {
            if (controlsFrozen && frozenDuringEnding != null)
                frozenDuringEnding.enabled = true;
            controlsFrozen = false;
            if (shotSource != null)
                shotSource.Stop();
        }

        private void OnGUI()
        {
            if (OverlayAlpha <= 0f)
                return;
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previousColour = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -100;
            GUI.color = new Color(OverlayColour.r, OverlayColour.g, OverlayColour.b, OverlayAlpha);
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
