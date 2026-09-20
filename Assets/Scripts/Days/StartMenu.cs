using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BigRedButton
{
    /// <summary>
    /// The front page. Shows the game's title, starts a new month, and holds the
    /// Team Player Mode switch. Drawn with IMGUI, like the day titles and the opening,
    /// so it needs no fonts, Canvas or TextMeshPro assets.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class StartMenu : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private string gameTitle = "EMPLOYEE OF THE MONTH";
        [Tooltip("Smaller line under the title. Leave empty for none.")]
        [SerializeField] private string subtitle = "SISYPHUSIAN COMPANY CO.";
        [SerializeField, Range(0.05f, 0.3f)] private float titleHeightFraction = 0.11f;
        [SerializeField, Range(0.01f, 0.08f)] private float subtitleHeightFraction = 0.028f;
        [SerializeField] private Color titleColour = new Color(0.93f, 0.93f, 0.9f);
        [SerializeField] private Color subtitleColour = new Color(0.62f, 0.63f, 0.6f);

        [Header("Buttons")]
        [SerializeField] private string startLabel = "START";
        [SerializeField] private string quitLabel = "QUIT";
        [Tooltip("Shown beside the Team Player Mode switch.")]
        [SerializeField] private string teamPlayerLabel = "TEAM PLAYER MODE";
        [Tooltip("Explains what the mode does, under its switch.")]
        [TextArea(2, 3)]
        [SerializeField] private string teamPlayerNote =
            "Touching a red button returns you to Day 1.";
        [SerializeField, Range(0.012f, 0.05f)] private float labelHeightFraction = 0.026f;

        [Header("Behaviour")]
        [Tooltip("Scene loaded by START. Day 1 plays the opening narration itself.")]
        [SerializeField] private int startAtDay = DayFlow.FirstDay;
        [Tooltip("Team Player Mode's state when the menu opens.")]
        [SerializeField] private bool teamPlayerModeByDefault;
        [Tooltip("Seconds the title takes to fade in when the menu opens.")]
        [SerializeField, Min(0f)] private float fadeInDuration = 1.4f;
        [Tooltip("Lets Enter or the gamepad's south button start the month.")]
        [SerializeField] private bool keyboardAndGamepadCanStart = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onStartPressed = new UnityEvent();

        private float elapsed;
        private bool starting;
        private GUIStyle titleStyle, subtitleStyle, buttonStyle, noteStyle, toggleStyle;

        public float Alpha => fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);
        public bool HasStarted => starting;
        public string GameTitle => WorkerIdentity.Format(gameTitle);
        public UnityEvent OnStartPressed => onStartPressed;

        private void Awake()
        {
            // Returning to the menu must not inherit a half-finished month.
            DayFlow.ResetToFirstDay();
            GameSettings.TeamPlayerMode = teamPlayerModeByDefault;
        }

        private void OnEnable()
        {
            // The menu is a pointer screen, so release the gameplay cursor lock.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (starting || !keyboardAndGamepadCanStart)
                return;

            Keyboard keys = Keyboard.current;
            bool submit = (keys != null && (keys.enterKey.wasPressedThisFrame ||
                    keys.numpadEnterKey.wasPressedThisFrame ||
                    keys.spaceKey.wasPressedThisFrame)) ||
                Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
            if (submit)
                StartGame();
        }

        /// <summary>Starts a new month at the first day. Also usable from a UI button.</summary>
        public void StartGame()
        {
            if (starting)
                return;
            starting = true;
            onStartPressed.Invoke();
            DayFlow.ResetToFirstDay();
            if (!DayFlow.LoadDay(startAtDay))
            {
                // The menu stays usable rather than trapping the player on a dead screen.
                starting = false;
                Debug.LogError($"Could not load day {startAtDay}. Run " +
                    "Tools > Big Red Button > Refresh Day Scene List so the day scenes are " +
                    "registered in build settings.");
            }
        }

        public void SetTeamPlayerMode(bool value) => GameSettings.TeamPlayerMode = value;

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            float alpha = Alpha;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // A plain dark page, so the title is the only thing competing for attention.
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, false, 0f, new Color(0.06f, 0.06f, 0.07f, alpha), 0f, 0f);

            EnsureStyles(alpha);
            float width = Mathf.Min(Screen.width * 0.86f, 900f);
            float left = (Screen.width - width) * 0.5f;

            float titleHeight = Screen.height * titleHeightFraction * 1.5f;
            GUI.Label(new Rect(left, Screen.height * 0.16f, width, titleHeight),
                GameTitle, titleStyle);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(left, Screen.height * 0.16f + titleHeight, width,
                    Screen.height * 0.06f), subtitle, subtitleStyle);

            float buttonWidth = Mathf.Min(width * 0.46f, 320f);
            float buttonHeight = Mathf.Max(34f, Screen.height * 0.062f);
            float buttonLeft = (Screen.width - buttonWidth) * 0.5f;
            float y = Screen.height * 0.47f;

            if (GUI.Button(new Rect(buttonLeft, y, buttonWidth, buttonHeight), startLabel, buttonStyle))
                StartGame();

            // The switch sits under START, so its meaning is read before starting.
            y += buttonHeight * 1.5f;
            bool wanted = GUI.Toggle(new Rect(buttonLeft, y, buttonWidth, buttonHeight * 0.7f),
                GameSettings.TeamPlayerMode, "  " + teamPlayerLabel, toggleStyle);
            if (wanted != GameSettings.TeamPlayerMode)
                GameSettings.TeamPlayerMode = wanted;

            if (!string.IsNullOrEmpty(teamPlayerNote))
                GUI.Label(new Rect(buttonLeft, y + buttonHeight * 0.72f, buttonWidth,
                    Screen.height * 0.08f), teamPlayerNote, noteStyle);

            y += buttonHeight * 1.9f;
            if (GUI.Button(new Rect(buttonLeft, y, buttonWidth, buttonHeight), quitLabel, buttonStyle))
                Quit();

            GUI.color = previous;
        }

        private void EnsureStyles(float alpha)
        {
            titleStyle ??= new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            titleStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height * titleHeightFraction));
            titleStyle.normal.textColor = Fade(titleColour, alpha);

            subtitleStyle ??= new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            subtitleStyle.fontSize =
                Mathf.Max(10, Mathf.RoundToInt(Screen.height * subtitleHeightFraction));
            subtitleStyle.normal.textColor = Fade(subtitleColour, alpha);

            int labelSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * labelHeightFraction));
            buttonStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter };
            buttonStyle.fontSize = labelSize;

            toggleStyle ??= new GUIStyle(GUI.skin.toggle);
            toggleStyle.fontSize = labelSize;
            toggleStyle.normal.textColor = Fade(titleColour, alpha);
            toggleStyle.onNormal.textColor = Fade(titleColour, alpha);
            toggleStyle.hover.textColor = Fade(titleColour, alpha);
            toggleStyle.onHover.textColor = Fade(titleColour, alpha);

            noteStyle ??= new GUIStyle { alignment = TextAnchor.UpperCenter, wordWrap = true };
            noteStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(labelSize * 0.8f));
            noteStyle.normal.textColor = Fade(subtitleColour, alpha);
        }

        private static Color Fade(Color colour, float alpha) =>
            new Color(colour.r, colour.g, colour.b, colour.a * alpha);
    }
}
