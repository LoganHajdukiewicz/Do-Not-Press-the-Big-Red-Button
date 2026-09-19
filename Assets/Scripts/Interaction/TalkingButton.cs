using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// A button that talks at the player when they come near or look at it.
    /// Days 8, 9, 11 and 12: the green button begs, bribes and bargains.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TalkingButton : MonoBehaviour
    {
        public enum Trigger
        {
            PlayerIsNear,
            PlayerLooksAtIt,
            DayStart,
            Manual
        }

        [Header("What it says")]
        [Tooltip("Lines are shown in order, one after another.")]
        [TextArea(2, 4)]
        [SerializeField] private List<string> lines = new List<string>();
        [Tooltip("Seconds each line stays on screen.")]
        [SerializeField, Min(0.5f)] private float secondsPerLine = 3.5f;
        [Tooltip("Repeats the lines from the start once they finish.")]
        [SerializeField] private bool loop;
        [Tooltip("Seconds to wait before the first line.")]
        [SerializeField, Min(0f)] private float startDelay;

        [Header("When it speaks")]
        [SerializeField] private Trigger speaksWhen = Trigger.PlayerIsNear;
        [Tooltip("Metres the player must be within to set it talking.")]
        [SerializeField, Min(0.5f)] private float triggerDistance = 5f;
        [Tooltip("How closely the player must aim, for the look trigger.")]
        [SerializeField, Range(0.5f, 0.999f)] private float aimTolerance = 0.9f;
        [Tooltip("Can start talking again after it has finished.")]
        [SerializeField] private bool canRetrigger;
        [Tooltip("Stops talking once the button has been pressed.")]
        [SerializeField] private bool silenceOnPress = true;

        [Header("Voice")]
        [Tooltip("Optional voice line, played once when it starts talking.")]
        [SerializeField] private AudioClip voice;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.9f;
        [Tooltip("A clip per line, played as each line appears. Overrides Voice when set.")]
        [SerializeField] private List<AudioClip> lineClips = new List<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.8f;

        [Header("Text on screen")]
        [Tooltip("Height of the text as a fraction of screen height.")]
        [SerializeField, Range(0.015f, 0.08f)] private float textHeightFraction = 0.032f;
        [Tooltip("How far down the screen the text sits. 0 is the top, 1 the bottom.")]
        [SerializeField, Range(0.4f, 0.92f)] private float screenPosition = 0.74f;
        [SerializeField] private Color textColour = Color.white;
        [Tooltip("Hides the text and relies on the voice clip only.")]
        [SerializeField] private bool hideText;

        [Header("Events")]
        [SerializeField] private UnityEvent onStartedTalking = new UnityEvent();
        [SerializeField] private UnityEvent onFinishedTalking = new UnityEvent();

        private GUIStyle style;
        private Transform player;
        private AudioSource source;
        private int lineIndex;
        private float lineTimer;
        private float startTimer;
        private bool waitingToStart;
        private bool talking;
        private bool finished;

        public string CurrentLine => talking && !waitingToStart && lineIndex < lines.Count
            ? lines[lineIndex] : string.Empty;
        public bool IsTalking => talking;
        public UnityEvent OnStartedTalking => onStartedTalking;
        public UnityEvent OnFinishedTalking => onFinishedTalking;

        private void Awake()
        {
            if (voice != null || lineClips.Count > 0)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.volume = voiceVolume;
                source.spatialBlend = spatialBlend;
            }

            var button = GetComponentInChildren<ButtonInteractable>();
            if (silenceOnPress && button != null)
                button.OnPressed.AddListener(Silence);

            if (speaksWhen == Trigger.DayStart)
                StartTalking();
        }

        /// <summary>Starts the lines. Can be called from another button's event.</summary>
        public void StartTalking()
        {
            if (talking || (finished && !canRetrigger) || lines.Count == 0)
                return;

            talking = true;
            finished = false;
            lineIndex = 0;
            lineTimer = 0f;
            startTimer = 0f;
            waitingToStart = startDelay > 0f;
            if (!waitingToStart)
                BeginLine();
            onStartedTalking.Invoke();
        }

        public void Silence()
        {
            bool wasTalking = talking;
            talking = false;
            waitingToStart = false;
            finished = true;
            if (source != null)
                source.Stop();
            if (wasTalking)
                onFinishedTalking.Invoke();
        }

        private void Update()
        {
            if (!talking)
            {
                if (speaksWhen is Trigger.PlayerIsNear or Trigger.PlayerLooksAtIt && ShouldStart())
                    StartTalking();
                return;
            }

            if (waitingToStart)
            {
                startTimer += Time.deltaTime;
                if (startTimer < startDelay)
                    return;
                waitingToStart = false;
                BeginLine();
                return;
            }

            lineTimer += Time.deltaTime;
            if (lineTimer < secondsPerLine)
                return;

            lineTimer = 0f;
            lineIndex++;
            if (lineIndex < lines.Count)
            {
                BeginLine();
                return;
            }

            if (loop)
            {
                lineIndex = 0;
                BeginLine();
                return;
            }

            Silence();
        }

        private void BeginLine()
        {
            if (source == null)
                return;

            // A clip per line takes priority, so lines can be voiced individually.
            if (lineIndex < lineClips.Count && lineClips[lineIndex] != null)
            {
                source.PlayOneShot(lineClips[lineIndex], voiceVolume);
                return;
            }

            if (lineIndex == 0 && voice != null)
            {
                source.clip = voice;
                source.Play();
            }
        }

        private bool ShouldStart()
        {
            if (finished && !canRetrigger)
                return false;

            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null)
                    return false;
                player = controller.transform;
            }

            Vector3 toButton = transform.position - player.position;
            if (toButton.magnitude > triggerDistance)
                return false;

            if (speaksWhen == Trigger.PlayerIsNear)
                return true;

            Camera main = Camera.main;
            return main != null &&
                Vector3.Dot(main.transform.forward, toButton.normalized) > aimTolerance;
        }

        private void OnGUI()
        {
            if (hideText)
                return;

            string line = CurrentLine;
            if (string.IsNullOrEmpty(line))
                return;

            if (style == null)
                style = new GUIStyle { alignment = TextAnchor.UpperCenter, wordWrap = true };
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * textHeightFraction));
            style.normal.textColor = textColour;

            // Sits low on the screen so it never covers the button being talked about.
            float width = Mathf.Min(Screen.width * 0.8f, 900f);
            var area = new Rect((Screen.width - width) * 0.5f, Screen.height * screenPosition, width,
                Screen.height * 0.24f);
            GUI.Label(area, line, style);
        }
    }
}
