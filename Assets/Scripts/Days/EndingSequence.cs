using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// The last day. The player steps through the door into sunshine, a gunshot rings
    /// out, the screen goes dark, and the company names its employee of the year.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class EndingSequence : MonoBehaviour
    {
        private enum Stage
        {
            Waiting,
            Sunshine,
            Dark,
            Card,
            Finished
        }

        [Header("Trigger")]
        [Tooltip("Walking into this area starts the ending. Usually the doorway.")]
        [SerializeField] private Vector3 doorwayCentre = new Vector3(0f, 0f, 7f);
        [SerializeField, Min(0.2f)] private float doorwayRadius = 1.6f;

        [Header("Sunshine")]
        [Tooltip("Seconds the screen takes to bleach out to white.")]
        [SerializeField, Min(0f)] private float sunshineFadeIn = 1.4f;
        [Tooltip("Seconds of held sunshine before the gunshot.")]
        [SerializeField, Min(0f)] private float sunshineHold = 1.1f;
        [SerializeField] private Color sunshineColour = new Color(1f, 0.99f, 0.93f);

        [Header("Gunshot")]
        [SerializeField] private AudioClip gunshot;
        [SerializeField, Range(0f, 1f)] private float gunshotVolume = 1f;
        [Tooltip("Seconds the screen takes to go from sunshine to black. Keep it fast.")]
        [SerializeField, Min(0f)] private float cutToBlack = 0.06f;
        [Tooltip("Seconds of silent darkness before the card appears.")]
        [SerializeField, Min(0f)] private float darkHold = 2.2f;

        [Header("Employee of the year card")]
        [Tooltip("Uses {WORKER-FIRSTNAME} and {WORKER-LASTNAME} for the worker's name.")]
        [TextArea(2, 4)]
        [SerializeField] private string cardText =
            "Employee of the Year\n{WORKER-FIRSTNAME} {WORKER-LASTNAME}";
        [SerializeField, Min(0f)] private float cardFadeIn = 2.5f;
        [SerializeField, Range(0.02f, 0.12f)] private float cardHeightFraction = 0.045f;
        [SerializeField] private Color cardColour = new Color(0.86f, 0.86f, 0.83f);

        [Header("Player")]
        [Tooltip("Disabled once the ending starts, so the player cannot walk back.")]
        [SerializeField] private MonoBehaviour frozenDuringEnding;

        [Header("Events")]
        [SerializeField] private UnityEvent onEndingStarted = new UnityEvent();
        [SerializeField] private UnityEvent onGunshot = new UnityEvent();
        [SerializeField] private UnityEvent onCardShown = new UnityEvent();

        private Stage stage = Stage.Waiting;
        private Transform player;
        private GUIStyle style;
        private Texture2D pixel;
        private float timer;
        private bool playerWasEnabled;

        /// <summary>0 when clear, 1 when fully bleached or fully black.</summary>
        public float OverlayAlpha { get; private set; }
        public Color OverlayColour { get; private set; } = Color.white;
        public float CardAlpha { get; private set; }
        public bool HasStarted => stage != Stage.Waiting;
        public bool HasFiredGunshot => stage is Stage.Dark or Stage.Card or Stage.Finished;
        public string CardText => WorkerIdentity.Format(cardText);
        public UnityEvent OnEndingStarted => onEndingStarted;
        public UnityEvent OnGunshot => onGunshot;
        public UnityEvent OnCardShown => onCardShown;

        /// <summary>Starts the ending. Can be wired to a door or a trigger volume.</summary>
        public void Begin()
        {
            if (stage != Stage.Waiting)
                return;

            stage = Stage.Sunshine;
            timer = 0f;
            OverlayColour = sunshineColour;

            if (frozenDuringEnding == null)
                frozenDuringEnding = FindFirstObjectByType<FirstPersonController>();
            if (frozenDuringEnding != null)
            {
                playerWasEnabled = frozenDuringEnding.enabled;
                frozenDuringEnding.enabled = false;
            }

            onEndingStarted.Invoke();
        }

        private void Update()
        {
            switch (stage)
            {
                case Stage.Waiting:
                    CheckDoorway();
                    return;

                case Stage.Sunshine:
                    UpdateSunshine();
                    return;

                case Stage.Dark:
                    UpdateDark();
                    return;

                case Stage.Card:
                    UpdateCard();
                    return;
            }
        }

        private void CheckDoorway()
        {
            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null)
                    return;
                player = controller.transform;
            }

            Vector3 offset = player.position - doorwayCentre;
            offset.y = 0f;
            if (offset.magnitude <= doorwayRadius)
                Begin();
        }

        private void UpdateSunshine()
        {
            timer += Time.unscaledDeltaTime;
            OverlayColour = sunshineColour;
            OverlayAlpha = sunshineFadeIn <= 0f ? 1f : Mathf.Clamp01(timer / sunshineFadeIn);

            if (timer < sunshineFadeIn + sunshineHold)
                return;

            // The gunshot lands on the brightest frame, then the light is taken away.
            stage = Stage.Dark;
            timer = 0f;
            if (gunshot != null)
                AudioSource.PlayClipAtPoint(gunshot, player != null
                    ? player.position : transform.position, gunshotVolume);
            onGunshot.Invoke();
        }

        private void UpdateDark()
        {
            timer += Time.unscaledDeltaTime;
            float toBlack = cutToBlack <= 0f ? 1f : Mathf.Clamp01(timer / cutToBlack);
            OverlayColour = Color.Lerp(sunshineColour, Color.black, toBlack);
            OverlayAlpha = 1f;

            if (timer < cutToBlack + darkHold)
                return;

            stage = Stage.Card;
            timer = 0f;
            onCardShown.Invoke();
        }

        private void UpdateCard()
        {
            timer += Time.unscaledDeltaTime;
            OverlayColour = Color.black;
            OverlayAlpha = 1f;
            CardAlpha = cardFadeIn <= 0f ? 1f : Mathf.Clamp01(timer / cardFadeIn);
            if (CardAlpha >= 1f)
                stage = Stage.Finished;
        }

        private void OnGUI()
        {
            if (OverlayAlpha <= 0f)
                return;

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;

            GUI.color = new Color(OverlayColour.r, OverlayColour.g, OverlayColour.b, OverlayAlpha);
            GUI.DrawTexture(full, pixel);

            if (CardAlpha > 0f)
            {
                if (style == null)
                    style = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                style.fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * cardHeightFraction));
                style.normal.textColor =
                    new Color(cardColour.r, cardColour.g, cardColour.b, cardColour.a * CardAlpha);
                GUI.color = new Color(1f, 1f, 1f, CardAlpha);
                GUI.Label(full, CardText, style);
            }

            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (pixel != null)
                Destroy(pixel);
            if (frozenDuringEnding != null && playerWasEnabled)
                frozenDuringEnding.enabled = true;
        }
    }
}
