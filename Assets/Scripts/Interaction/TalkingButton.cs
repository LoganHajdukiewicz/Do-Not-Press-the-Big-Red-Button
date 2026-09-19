using System.Collections.Generic;
using UnityEngine;

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
            DayStart
        }

        [SerializeField] private Trigger speaksWhen = Trigger.PlayerIsNear;
        [Tooltip("Lines are shown in order, one after another.")]
        [SerializeField] private List<string> lines = new List<string>();
        [Tooltip("Seconds each line stays on screen.")]
        [SerializeField, Min(0.5f)] private float secondsPerLine = 3.5f;
        [Tooltip("Metres the player must be within to set it talking.")]
        [SerializeField, Min(0.5f)] private float triggerDistance = 5f;
        [Tooltip("Repeats the lines from the start once they finish.")]
        [SerializeField] private bool loop;
        [Tooltip("Stops talking once the button has been pressed.")]
        [SerializeField] private bool silenceOnPress = true;

        [SerializeField] private AudioClip voice;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.9f;

        private GUIStyle style;
        private Transform player;
        private AudioSource source;
        private int lineIndex;
        private float lineTimer;
        private bool talking;
        private bool finished;

        public string CurrentLine => talking && lineIndex < lines.Count ? lines[lineIndex] : string.Empty;
        public bool IsTalking => talking;

        private void Awake()
        {
            if (voice != null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.clip = voice;
                source.playOnAwake = false;
                source.volume = voiceVolume;
                source.spatialBlend = 0.8f;
            }

            var button = GetComponentInChildren<ButtonInteractable>();
            if (silenceOnPress && button != null)
                button.OnPressed.AddListener(Silence);

            if (speaksWhen == Trigger.DayStart)
                StartTalking();
        }

        public void StartTalking()
        {
            if (finished || lines.Count == 0)
                return;

            if (!talking)
            {
                talking = true;
                lineTimer = 0f;
                if (source != null)
                    source.Play();
            }
        }

        public void Silence()
        {
            talking = false;
            finished = true;
            if (source != null)
                source.Stop();
        }

        private void Update()
        {
            if (finished)
                return;

            if (speaksWhen != Trigger.DayStart && !talking && ShouldStart())
                StartTalking();

            if (!talking)
                return;

            lineTimer += Time.deltaTime;
            if (lineTimer < secondsPerLine)
                return;

            lineTimer = 0f;
            lineIndex++;
            if (lineIndex < lines.Count)
                return;

            if (loop)
                lineIndex = 0;
            else
                Silence();
        }

        private bool ShouldStart()
        {
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
                Vector3.Dot(main.transform.forward, toButton.normalized) > 0.9f;
        }

        private void OnGUI()
        {
            string line = CurrentLine;
            if (string.IsNullOrEmpty(line))
                return;

            int fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.032f));
            if (style == null)
                style = CorporateText.CreateStyle(fontSize, wordWrap: true);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Normal;

            // Sits low on the screen so it never covers the button being talked about.
            float width = Mathf.Min(Screen.width * 0.8f, 900f);
            var area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.74f, width,
                Screen.height * 0.2f);
            CorporateText.DrawWithShadow(area, line.ToUpperInvariant(), style, CorporateText.Ink);
        }
    }
}
