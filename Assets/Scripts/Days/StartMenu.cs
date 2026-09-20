using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BigRedButton
{
    /// <summary>
    /// The front page. The game's title over a stack of manila folders, one option
    /// per folder, each sliding out a little when the pointer is over it. Drawn with
    /// IMGUI, like the day titles and the opening, so it needs no fonts, Canvas or
    /// TextMeshPro assets.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class StartMenu : MonoBehaviour
    {
        /// <summary>The options on the stack, in the order they are drawn.</summary>
        public enum Option { Start, TeamPlayerMode, Quit }

        [Header("Page")]
        [SerializeField] private Color pageColour = new Color(0.98f, 0.98f, 0.98f);

        [Header("Title")]
        [SerializeField] private string gameTitle = "EMPLOYEE OF THE MONTH";
        [Tooltip("Smaller line under the title. Leave empty for none.")]
        [SerializeField] private string subtitle = "";
        [SerializeField, Range(0.05f, 0.3f)] private float titleHeightFraction = 0.11f;
        [SerializeField, Range(0.01f, 0.08f)] private float subtitleHeightFraction = 0.028f;
        [SerializeField] private Color titleColour = Color.black;
        [SerializeField] private Color subtitleColour = new Color(0.35f, 0.35f, 0.38f);
        [Tooltip("Thin rule drawn around the title band. Alpha 0 hides it.")]
        [SerializeField] private Color titleRuleColour = new Color(0.55f, 0.35f, 0.96f);

        [Header("Folders")]
        [SerializeField] private Color folderTab = new Color(0.97f, 0.92f, 0.56f);
        [SerializeField] private Color folderBody = new Color(0.99f, 0.96f, 0.69f);
        [Tooltip("The folder under the pointer is tinted towards this.")]
        [SerializeField] private Color folderHover = new Color(1f, 0.99f, 0.82f);
        [SerializeField] private Color folderLabel = Color.black;
        [Tooltip("Width of the stack as a fraction of screen width.")]
        [SerializeField, Range(0.3f, 0.95f)] private float stackWidthFraction = 0.62f;
        [Tooltip("Height of one folder's visible band, as a fraction of screen height.")]
        [SerializeField, Range(0.06f, 0.25f)] private float bandHeightFraction = 0.138f;
        [Tooltip("Width of the labelled tab, as a fraction of the folder's width.")]
        [SerializeField, Range(0.25f, 0.9f)] private float tabWidthFraction = 0.55f;
        [Tooltip("How far the tab rises above the folder body, as a fraction of the " +
            "gap between folders. The rest of the gap shows the body's top edge.")]
        [SerializeField, Range(0.25f, 0.75f)] private float tabRiseFraction = 0.66f;
        [Tooltip("Thin edge drawn around each folder, so the stack does not merge.")]
        [SerializeField] private Color folderEdge = new Color(0.85f, 0.79f, 0.42f);
        [Tooltip("Where the top folder begins, as a fraction of screen height.")]
        [SerializeField, Range(0.15f, 0.5f)] private float stackTopFraction = 0.26f;
        [Tooltip("How far past the bottom of the screen the stack runs.")]
        [SerializeField, Range(0.8f, 1.3f)] private float stackBottomFraction = 1.06f;
        [SerializeField, Range(0.012f, 0.05f)] private float labelHeightFraction = 0.032f;

        [Header("Hover")]
        [Tooltip("Pixels the folder slides right when the pointer is over it.")]
        [SerializeField, Range(0f, 60f)] private float hoverSlideX = 14f;
        [Tooltip("Pixels the folder lifts when the pointer is over it.")]
        [SerializeField, Range(0f, 60f)] private float hoverLiftY = 9f;
        [Tooltip("How quickly the slide follows the pointer. Higher is snappier.")]
        [SerializeField, Range(1f, 30f)] private float hoverSpeed = 12f;

        [Header("Labels")]
        [SerializeField] private string startLabel = "START";
        [SerializeField] private string quitLabel = "QUIT";
        [SerializeField] private string teamPlayerLabel = "TEAM PLAYER MODE";
        [Tooltip("Explains what the mode does. Set on the body strip beside the tab.")]
        [TextArea(2, 3)]
        [SerializeField] private string teamPlayerNote =
            "Touching a red button returns you to Day 1.";

        [Header("Behaviour")]
        [Tooltip("Scene loaded by START. Day 1 plays the opening narration itself.")]
        [SerializeField] private int startAtDay = DayFlow.FirstDay;
        [SerializeField] private bool teamPlayerModeByDefault;
        [SerializeField, Min(0f)] private float fadeInDuration = 1.4f;
        [Tooltip("Lets Enter or the gamepad's south button start the month.")]
        [SerializeField] private bool keyboardAndGamepadCanStart = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onStartPressed = new UnityEvent();

        private const int OptionCount = 3;
        private readonly float[] hover = new float[OptionCount];
        private readonly Rect[] bands = new Rect[OptionCount];
        private float elapsed;
        private bool starting;
        private GUIStyle titleStyle, subtitleStyle, labelStyle, noteStyle;

        public float Alpha => fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);
        public bool HasStarted => starting;
        public string GameTitle => WorkerIdentity.Format(gameTitle);
        public UnityEvent OnStartPressed => onStartPressed;
        /// <summary>0 when settled, 1 when fully slid out. For tests and tuning.</summary>
        public float HoverAmount(Option option) => hover[(int)option];

        private void Awake()
        {
            // Returning to the menu must not inherit a half-finished month, nor the
            // music left running by it: a new month starts the track from the top.
            DayFlow.ResetToFirstDay();
            BackgroundMusic.ClearPersistent();
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
            AnimateHover();
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

        /// <summary>
        /// Eases each folder towards or away from its slid-out position, so the stack
        /// reacts smoothly rather than snapping between two states.
        /// </summary>
        private void AnimateHover()
        {
            Vector2 pointer = PointerPosition();
            float step = Mathf.Clamp01(Time.unscaledDeltaTime * hoverSpeed);
            for (int i = 0; i < OptionCount; i++)
            {
                bool over = !starting && bands[i].width > 0f && bands[i].Contains(pointer);
                hover[i] = Mathf.Lerp(hover[i], over ? 1f : 0f, step);
            }
        }

        /// <summary>Pointer position in GUI space, which has y growing downwards.</summary>
        private static Vector2 PointerPosition()
        {
            Vector2 screen = Mouse.current != null
                ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            return new Vector2(screen.x, Screen.height - screen.y);
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
        public void ToggleTeamPlayerMode() => GameSettings.TeamPlayerMode = !GameSettings.TeamPlayerMode;

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
            GUI.color = Color.white;

            Fill(new Rect(0f, 0f, Screen.width, Screen.height), Fade(pageColour, 1f));
            EnsureStyles(alpha);
            DrawTitle(alpha);
            DrawFolders(alpha);

            GUI.color = previous;
        }

        private void DrawTitle(float alpha)
        {
            float bandTop = Screen.height * 0.055f;
            float bandHeight = Screen.height * titleHeightFraction * 1.5f;
            var band = new Rect(0f, bandTop, Screen.width, bandHeight);

            if (titleRuleColour.a > 0f)
            {
                // A thin rule top and bottom, the way a form is boxed off.
                float rule = Mathf.Max(1f, Screen.height * 0.0022f);
                Fill(new Rect(band.x, band.y, band.width, rule), Fade(titleRuleColour, alpha));
                Fill(new Rect(band.x, band.yMax - rule, band.width, rule), Fade(titleRuleColour, alpha));
            }

            titleStyle.normal.textColor = Fade(titleColour, alpha);
            GUI.Label(band, GameTitle, titleStyle);

            if (string.IsNullOrEmpty(subtitle))
                return;
            subtitleStyle.normal.textColor = Fade(subtitleColour, alpha);
            GUI.Label(new Rect(0f, band.yMax + Screen.height * 0.012f, Screen.width,
                Screen.height * 0.06f), subtitle, subtitleStyle);
        }

        private void DrawFolders(float alpha)
        {
            float width = Mathf.Min(Screen.width * stackWidthFraction, 780f);
            float left = (Screen.width - width) * 0.5f;
            float band = Screen.height * bandHeightFraction;
            float top = Screen.height * stackTopFraction;
            // The stack runs off the bottom of the frame, like a drawer too full to close.
            float bottom = Screen.height * stackBottomFraction;
            float tabRise = band * tabRiseFraction;
            float radius = Mathf.Max(4f, band * 0.16f);

            // Back to front, so each folder overlaps the one behind it.
            for (int i = 0; i < OptionCount; i++)
            {
                float lift = hover[i];
                var folder = new Rect(
                    left + lift * hoverSlideX,
                    top + i * band - lift * hoverLiftY,
                    width,
                    bottom - (top + i * band));
                bands[i] = new Rect(folder.x, folder.y, folder.width,
                    i == OptionCount - 1 ? folder.height : band);

                float tabWidth = width * tabWidthFraction;
                Color tab = Color.Lerp(folderTab, folderHover, lift);
                Color body = Color.Lerp(folderBody, folderHover, lift);
                DrawFolder(folder, tabWidth, tabRise, radius,
                    Fade(tab, alpha), Fade(body, alpha), alpha);
                DrawLabel((Option)i, folder, tabWidth, tabRise, alpha);

                // The click is taken on the visible band, so a covered folder is safe.
                if (GUI.Button(bands[i], GUIContent.none, GUIStyle.none))
                    Activate((Option)i);
            }
        }

        /// <summary>
        /// One manila folder: the tab rises above the body on the left, and the body's
        /// top edge runs on to the right, giving the stepped folder silhouette. A thin
        /// edge keeps the stack from merging, since every sheet is the same colour.
        /// </summary>
        private void DrawFolder(Rect folder, float tabWidth, float tabRise, float radius,
            Color tab, Color body, float alpha)
        {
            Color edge = Fade(folderEdge, alpha);
            float line = Mathf.Max(1f, radius * 0.1f);
            var bodyRect = new Rect(folder.x, folder.y + tabRise,
                folder.width, folder.height - tabRise);
            // The tab runs down behind the body, so no seam shows where they meet.
            var tabRect = new Rect(folder.x, folder.y, tabWidth, tabRise + radius * 2f);

            // A soft drop shadow under the whole sheet.
            float drop = Mathf.Max(2f, radius * 0.5f);
            Rounded(new Rect(bodyRect.x + drop, bodyRect.y + drop * 0.7f,
                    bodyRect.width, bodyRect.height),
                new Color(0f, 0f, 0f, 0.2f * alpha), new Vector4(radius, radius, radius, radius));
            Rounded(new Rect(tabRect.x + drop, tabRect.y + drop * 0.7f,
                    tabRect.width, tabRect.height),
                new Color(0f, 0f, 0f, 0.16f * alpha), new Vector4(radius, radius, 0f, 0f));

            // Tab first, then the body over it: the body's square top left corner
            // becomes the shoulder where the two meet.
            Rounded(Grow(tabRect, line), edge, new Vector4(radius, radius, 0f, 0f));
            Rounded(tabRect, tab, new Vector4(radius, radius, 0f, 0f));
            Rounded(Grow(bodyRect, line), edge, new Vector4(0f, radius, radius, radius));
            Rounded(bodyRect, body, new Vector4(0f, radius, radius, radius));
        }

        private static Rect Grow(Rect rect, float by) =>
            new Rect(rect.x - by, rect.y - by, rect.width + by * 2f, rect.height + by * 2f);

        private void DrawLabel(Option option, Rect folder, float tabWidth, float tabRise,
            float alpha)
        {
            string text = option switch
            {
                Option.Start => startLabel,
                Option.Quit => quitLabel,
                _ => $"{teamPlayerLabel}   {(GameSettings.TeamPlayerMode ? "ON" : "OFF")}"
            };

            // Sits on the tab, left aligned, the way a folder is written on.
            float padding = tabWidth * 0.08f;
            float room = tabWidth - padding * 2f;
            labelStyle.normal.textColor = Fade(folderLabel, alpha);
            labelStyle.fontSize = FittedLabelSize(text, room);
            GUI.Label(new Rect(folder.x + padding, folder.y, room, tabRise), text, labelStyle);

            // Only the mode folder explains itself. It goes on the body strip beside
            // the tab, which stays visible under the folder in front.
            if (option != Option.TeamPlayerMode || string.IsNullOrEmpty(teamPlayerNote))
                return;
            float strip = Screen.height * bandHeightFraction;
            var noteArea = new Rect(folder.x + tabWidth + padding, folder.y + tabRise,
                folder.width - tabWidth - padding * 2f, strip - tabRise);
            if (noteArea.width <= 0f || noteArea.height <= 0f)
                return;
            noteStyle.normal.textColor = Fade(folderLabel, alpha * 0.6f);
            noteStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(labelStyle.fontSize * 0.46f));
            GUI.Label(noteArea, teamPlayerNote, noteStyle);
        }

        /// <summary>Shrinks a long label until it fits its tab, so nothing is clipped.</summary>
        private int FittedLabelSize(string text, float available)
        {
            int size = Mathf.Max(10, Mathf.RoundToInt(Screen.height * labelHeightFraction));
            for (; size > 10; size--)
            {
                labelStyle.fontSize = size;
                if (labelStyle.CalcSize(new GUIContent(text)).x <= available)
                    break;
            }

            return size;
        }

        private void Activate(Option option)
        {
            switch (option)
            {
                case Option.Start:
                    StartGame();
                    break;
                case Option.TeamPlayerMode:
                    ToggleTeamPlayerMode();
                    break;
                case Option.Quit:
                    Quit();
                    break;
            }
        }

        // Alpha blending stays on, so the drop shadows and the fade-in read correctly.
        private static void Fill(Rect rect, Color colour) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true,
                0f, colour, 0f, 0f);

        private static void Rounded(Rect rect, Color colour, Vector4 radii) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true,
                0f, colour, Vector4.zero, radii);

        private void EnsureStyles(float alpha)
        {
            titleStyle ??= new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                fontStyle = FontStyle.Bold
            };
            titleStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height * titleHeightFraction));

            subtitleStyle ??= new GUIStyle { alignment = TextAnchor.UpperCenter, wordWrap = true };
            subtitleStyle.fontSize =
                Mathf.Max(10, Mathf.RoundToInt(Screen.height * subtitleHeightFraction));

            labelStyle ??= new GUIStyle
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                fontStyle = FontStyle.Bold
            };

            noteStyle ??= new GUIStyle { alignment = TextAnchor.MiddleLeft, wordWrap = true };
        }

        private static Color Fade(Color colour, float alpha) =>
            new Color(colour.r, colour.g, colour.b, colour.a * alpha);
    }
}
