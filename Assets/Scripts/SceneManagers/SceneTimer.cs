// Assets/Scripts/SceneManagers/SceneTimer.cs
// Limits a scene's lifetime to X minutes. When time is up (or Esc is pressed),
// stops EEG recording (if present), then:
//  - If "Enable Survey" is ticked: shows a large IMGUI survey UI (NASA-TLX + short IEQ + Genomics Audibility),
//    asking ONE question per page, writes SPSS-friendly flat JSON + human summary,
//    then redirects to the target scene. At the end, additionally asks for ONE WORD
//    that best describes the scene (saved into both JSON outputs).
//  - If unticked: skips surveys, shows a brief opaque notice, and redirects after a delay.
//
// Logging (when surveys are enabled):
//  - SPSS-friendly (machine-readable) — one flat record in Sample.type "survey_flat" (label JSON has flat keys).
//  - Human-readable — multi-line text in Sample.type "survey_text".
//
// Integrates with the same unified JSON (UserData.json) used by EEGMetricsUdpReceiver.
// British English comments & UI text. Esc trigger has a 0.1 s delay. Opaque window background.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ExperimentData; // shared JSON models: ExtendedUserDataContainer, UserDataEntry, UserSession, Sample

[DisallowMultipleComponent]
public class SceneTimer : MonoBehaviour
{
    [Header("Timer")]
    [Tooltip("Maximum duration for this scene in minutes.")]
    [Min(0.1f)]
    public float maxDurationMinutes = 5f;

    [Tooltip("Delay (seconds) after finishing the survey (or showing notice) before switching scenes.")]
    [Min(0f)]
    public float postSurveyRedirectDelay = 2f;

    [Header("Destination")]
    [Tooltip("Scene to load after survey/notice (must be in Build Settings).")]
    public string redirectSceneName = "RestingLoby";

    [Tooltip("If true, the timer disables itself when the active scene already matches 'redirectSceneName'.")]
    public bool disableWhenAlreadyInTarget = true;

    [Header("Survey")]
    [Tooltip("If ticked, run the post-scene questionnaires. If not, skip surveys entirely.")]
    public bool enableSurvey = true;

    [Header("UI Window")]
    [Tooltip("Shown automatically when time is up (or Esc). Forced off while counting down.")]
    public bool showWindow = false;
    public Vector2 windowPosition = new Vector2(500, 250);   // requested default
    public Vector2 windowSize     = new Vector2(1100, 760);  // large for readability
    public bool allowDrag         = true;

    [Header("Hotkeys")]
    [Tooltip("When Esc is pressed, begin the survey (or notice) after this unscaled delay.")]
    [Min(0f)]
    public float escTriggerDelay = 0.1f;

    [Header("User & Files (align with EEGMetricsUdpReceiver)")]
    [Tooltip("If set, overrides the discovered username (PlayerPrefs 'LastUsername').")]
    public string overrideUsername = "";
    [Tooltip("Folder under Assets/ (Editor) or persistentDataPath (Build).")]
    public string dataFolderName = "Data";
    [Tooltip("Unified user + sessions JSON file name.")]
    public string userFileName = "UserData.json";

    // ---- runtime: timer/survey state ----
    private float  _deadlineUnscaled;
    private bool   _armed = false;
    private bool   _flowActive = false;            // survey OR notice flow is active
    private bool   _redirectScheduled = false;
    private float  _redirectAtUnscaled;
    private Rect   _windowRect;
    private Vector2 _scroll = Vector2.zero;
    private int    _windowId;
    private string _activeScene = "";
    private string _username = "";
    private string _sessionIdForWrite = "";        // session bound for survey writes
    private bool   _resolvedSession = false;
    private bool   _recordingStopped = false;

    // ---- flow state machine ----
    private enum FlowStage { None, Intro, NASA_TLX, IEQ_SF, GENOMICS_AUD, Submit, Thanks, Notice }
    private FlowStage _stage = FlowStage.None;

    // Per-page indices (ask one item per page)
    private int _tlxIndex = 0; // 0..5
    private int _ieqIndex = 0; // 0..7
    private int _gaIndex  = 0; // 0..1

    // ---- styles (built once) ----
    private GUIStyle _lbl, _hdr, _subtle, _btn, _note, _progress, _title, _windowStyle, _banner, _input;

    private Texture2D _windowBgTex;

    // NASA-TLX responses (0..20; higher = more demanding; Performance: higher = worse)
    private int _tlxMental      = 10;
    private int _tlxPhysical    = 10;
    private int _tlxTemporal    = 10;
    private int _tlxPerformance = 10; // Perfect (0) → Failure (20)
    private int _tlxEffort      = 10;
    private int _tlxFrustration = 10;

    // Short IEQ-SF (8 items; 1..5 Likert; item 5 is reverse-scored)
    private int _ieq1 = 3, _ieq2 = 3, _ieq3 = 3, _ieq4 = 3, _ieq5 = 3, _ieq6 = 3, _ieq7 = 3, _ieq8 = 3;

    // Genomics Audibility mini-scale (2 items; 1..5 Likert)
    // 1) "I could comfortably hear changes in the genomic parameters."
    // 2) "As the cells evolved, it was easy to listen to their changes."
    private int _ga1 = 3, _ga2 = 3;

    // One-word scene summary (free text; saved as a single token)
    private string _sceneOneWord = "";
    private bool _oneWordFocusRequested = false;

    // Persistent banner string (British English)
    private const string TopNotice = "Please answer only based on the scene you have just experienced.";

    void OnValidate()
    {
        if (maxDurationMinutes < 0.1f) maxDurationMinutes = 0.1f;
        if (postSurveyRedirectDelay < 0f) postSurveyRedirectDelay = 0f;
        if (escTriggerDelay < 0f) escTriggerDelay = 0f;
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
    }

    void OnEnable()
    {
        _windowId = GetInstanceID();
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        _activeScene = SceneManager.GetActiveScene().name;
        _username = ResolveUsername();
        ResetAndArm();
        BuildStyles(); // builds opaque window style
    }

    private void ResetAndArm()
    {
        if (disableWhenAlreadyInTarget &&
            !string.IsNullOrEmpty(redirectSceneName) &&
            string.Equals(_activeScene, redirectSceneName, StringComparison.OrdinalIgnoreCase))
        {
            _armed = false;
            _flowActive = false;
            _redirectScheduled = false;
            _recordingStopped = false;
            showWindow = false;
            enabled = false;
            return;
        }

        _armed = true;
        _flowActive = false;
        _redirectScheduled = false;
        _recordingStopped = false;
        _stage = FlowStage.None;
        _tlxIndex = 0;
        _ieqIndex = 0;
        _gaIndex = 0;
        showWindow = false; // hidden until time-up / Esc
        _deadlineUnscaled = Time.unscaledTime + maxDurationMinutes * 60f;
    }

    void Update()
    {
        if (!_armed) return;

        // Esc → begin survey OR notice after a short unscaled delay
        if (!_flowActive && Input.GetKeyDown(KeyCode.Escape))
            Invoke(nameof(BeginFlow), Mathf.Max(0f, escTriggerDelay));

        if (_flowActive)
        {
            if (_redirectScheduled && Time.unscaledTime >= _redirectAtUnscaled)
                DoRedirect();
            return;
        }

        // Count down using unscaled time so pausing Time.timeScale doesn't affect us.
        if (Time.unscaledTime >= _deadlineUnscaled)
            BeginFlow();
    }

    private void BeginFlow()
    {
        // Always stop EEG recording BEFORE any UI
        StopEegRecordingIfAny();

        _flowActive = true;
        showWindow = true;

        if (enableSurvey)
        {
            _stage = FlowStage.Intro;
            EnsureSessionBinding(); // we’ll write survey entries later
        }
        else
        {
            // No survey: show simple opaque notice + schedule redirect
            _stage = FlowStage.Notice;
            _redirectScheduled = true;
            _redirectAtUnscaled = Time.unscaledTime + postSurveyRedirectDelay;
        }
    }

    void OnGUI()
    {
        if (!showWindow) return;

        if (_lbl == null || _windowStyle == null) BuildStyles(); // hot-rebuild if skin reloaded

        // Ensure no hidden alpha tints
        var prevBG = GUI.backgroundColor; var prevCol = GUI.color;
        GUI.backgroundColor = Color.white; GUI.color = Color.white;

        _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow,
            enableSurvey ? "Scene Wrap-Up & Survey" : "Scene Wrap-Up", _windowStyle);

        GUI.backgroundColor = prevBG; GUI.color = prevCol;

        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize     = new Vector2(_windowRect.width, _windowRect.height);
    }

    private void DrawWindow(int id)
    {
        float margin = 16f;
        float contentWidth = Mathf.Max(600f, _windowRect.width - 2f * margin);

        GUILayout.BeginVertical();
        GUILayout.Space(4);
        _scroll = GUILayout.BeginScrollView(_scroll);

        // Persistent banner for ALL survey panes (not for the simple notice)
        if (enableSurvey && _stage != FlowStage.Notice)
            DrawTopNotice(contentWidth);

        switch (_stage)
        {
            case FlowStage.Intro:
                DrawIntro(contentWidth);
                break;
            case FlowStage.NASA_TLX:
                DrawNasaTlxOnePerPage(contentWidth);
                break;
            case FlowStage.IEQ_SF:
                DrawIeqSfOnePerPage(contentWidth);
                break;
            case FlowStage.GENOMICS_AUD:
                DrawGenomicsAudibilityOnePerPage(contentWidth);
                break;
            case FlowStage.Submit:
                DrawSubmit(contentWidth);
                break;
            case FlowStage.Thanks:
                DrawThanks(contentWidth);
                break;
            case FlowStage.Notice:
                DrawNotice(contentWidth);
                break;
            default:
                GUILayout.Label("Awaiting…", _lbl, GUILayout.MaxWidth(contentWidth));
                break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        if (allowDrag)
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    private void DrawTopNotice(float w)
    {
        GUILayout.Label(TopNotice, _banner, GUILayout.MaxWidth(w));
        GUILayout.Space(8);
    }

    // ---- UI panes ------------------------------------------------------------

    private void DrawNotice(float w)
    {
        GUILayout.Label("The scene has finished. We are moving on shortly.", _lbl, GUILayout.MaxWidth(w));
        if (!string.IsNullOrEmpty(redirectSceneName))
        {
            float secsLeft = Mathf.Max(0f, _redirectAtUnscaled - Time.unscaledTime);
            GUILayout.Space(6);
            GUILayout.Label($"Switching to ‘{redirectSceneName}’ in {secsLeft:0.0} s…", _hdr, GUILayout.MaxWidth(w));
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Go now", _btn, GUILayout.Width(180)))
                DoRedirect();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Space(6);
            GUILayout.Label("No destination scene configured.", _hdr, GUILayout.MaxWidth(w));
        }
    }

    private void DrawIntro(float w)
    {
        GUILayout.Label("Thank you. This scene has finished. Before we move on, please answer three short questionnaires. You will be shown one question per page.", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Space(10);
        GUILayout.Label("• <b>NASA Task Load Index (TLX)</b> — six items with 21 gradations from <i>Very Low</i> to <i>Very High</i>. ‘Performance’ is anchored <i>Perfect</i> → <i>Failure</i>.", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Label("• <b>IEQ-SF</b> (Short Immersive Experience Questionnaire) — eight statements rated 1 (Strongly disagree) … 5 (Strongly agree).", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Label("• <b>Genomics Audibility</b> — two statements rated 1 (Strongly disagree) … 5 (Strongly agree).", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Space(14);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Start NASA-TLX", _btn, GUILayout.Width(260)))
        {
            _stage = FlowStage.NASA_TLX;
            _tlxIndex = 0;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label($"User: {(_username ?? "<none>")}   |   Scene: {_activeScene}", _subtle, GUILayout.MaxWidth(w));
    }

    // NASA-TLX — ONE ITEM PER PAGE
    private void DrawNasaTlxOnePerPage(float w)
    {
        int total = 6;
        string title = GetTlxQuestion(_tlxIndex);
        string left = GetTlxLeftAnchor(_tlxIndex);
        string right = GetTlxRightAnchor(_tlxIndex);
        int val = GetTlxVal(_tlxIndex);

        GUILayout.Label("NASA-TLX", _title, GUILayout.MaxWidth(w));
        GUILayout.Label($"Item {_tlxIndex + 1} of {total}", _progress, GUILayout.MaxWidth(w));
        GUILayout.Space(4);

        GUILayout.Label(title, _hdr, GUILayout.MaxWidth(w));
        GUILayout.Space(6);

        // Slider row
        GUILayout.BeginHorizontal();
        GUILayout.Label(left, _note, GUILayout.Width(120));
        float f = GUILayout.HorizontalSlider(val, 0f, 20f, GUILayout.Height(24), GUILayout.ExpandWidth(true));
        val = Mathf.RoundToInt(f);
        GUILayout.Label(right, _note, GUILayout.Width(120));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Selected: {val} / 20", _lbl);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        SetTlxVal(_tlxIndex, val);

        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_tlxIndex == 0 ? "Back" : "Previous", _btn, GUILayout.Width(160)))
        {
            if (_tlxIndex == 0) _stage = FlowStage.Intro;
            else _tlxIndex = Mathf.Max(0, _tlxIndex - 1);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_tlxIndex == total - 1 ? "Continue to IEQ-SF" : "Next", _btn, GUILayout.Width(240)))
        {
            if (_tlxIndex < total - 1) _tlxIndex++;
            else { _stage = FlowStage.IEQ_SF; _ieqIndex = 0; }
        }
        GUILayout.EndHorizontal();
    }

    // IEQ-SF — ONE ITEM PER PAGE
    private void DrawIeqSfOnePerPage(float w)
    {
        int total = 8;
        string text = GetIeqText(_ieqIndex);
        bool reverse = GetIeqReverse(_ieqIndex);
        int val = GetIeqVal(_ieqIndex);

        GUILayout.Label("IEQ-SF", _title, GUILayout.MaxWidth(w));
        GUILayout.Label($"Item {_ieqIndex + 1} of {total}", _progress, GUILayout.MaxWidth(w));
        GUILayout.Space(4);

        GUILayout.Label(text + (reverse ? "  <i>(Reverse-scored)</i>" : ""), _hdr, GUILayout.MaxWidth(w));
        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Strongly disagree", _note, GUILayout.Width(180));
        int idx = Mathf.Clamp(val - 1, 0, 4);
        idx = GUILayout.Toolbar(idx, Likert, GUILayout.MinWidth(400), GUILayout.Height(28), GUILayout.ExpandWidth(true));
        val = idx + 1;
        GUILayout.Label("Strongly agree", _note, GUILayout.Width(160));
        GUILayout.EndHorizontal();

        SetIeqVal(_ieqIndex, val);

        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_ieqIndex == 0 ? "Back" : "Previous", _btn, GUILayout.Width(160)))
        {
            if (_ieqIndex == 0) { _stage = FlowStage.NASA_TLX; _tlxIndex = 5; }
            else _ieqIndex = Mathf.Max(0, _ieqIndex - 1);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_ieqIndex == total - 1 ? "Continue to Genomics Audibility" : "Next", _btn, GUILayout.Width(300)))
        {
            if (_ieqIndex < total - 1) _ieqIndex++;
            else { _stage = FlowStage.GENOMICS_AUD; _gaIndex = 0; }
        }
        GUILayout.EndHorizontal();
    }

    // Genomics Audibility — ONE ITEM PER PAGE
    private void DrawGenomicsAudibilityOnePerPage(float w)
    {
        int total = 2;
        string text = GetGaText(_gaIndex);
        int val = GetGaVal(_gaIndex);

        GUILayout.Label("Genomics Audibility", _title, GUILayout.MaxWidth(w));
        GUILayout.Label($"Item {_gaIndex + 1} of {total}", _progress, GUILayout.MaxWidth(w));
        GUILayout.Space(4);

        GUILayout.Label(text, _hdr, GUILayout.MaxWidth(w));
        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Strongly disagree", _note, GUILayout.Width(180));
        int idx = Mathf.Clamp(val - 1, 0, 4);
        idx = GUILayout.Toolbar(idx, Likert, GUILayout.MinWidth(400), GUILayout.Height(28), GUILayout.ExpandWidth(true));
        val = idx + 1;
        GUILayout.Label("Strongly agree", _note, GUILayout.Width(160));
        GUILayout.EndHorizontal();

        SetGaVal(_gaIndex, val);

        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_gaIndex == 0 ? "Back" : "Previous", _btn, GUILayout.Width(160)))
        {
            if (_gaIndex == 0) { _stage = FlowStage.IEQ_SF; _ieqIndex = 7; }
            else _gaIndex = Mathf.Max(0, _gaIndex - 1);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_gaIndex == total - 1 ? "Review & Submit" : "Next", _btn, GUILayout.Width(220)))
        {
            if (_gaIndex < total - 1) _gaIndex++;
            else { _stage = FlowStage.Submit; _oneWordFocusRequested = false; }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSubmit(float w)
    {
        GUILayout.Label("<b>Review</b>", _title, GUILayout.MaxWidth(w));
        GUILayout.Space(4);
        GUILayout.Label($"User: {(_username ?? "<none>")}    |    Scene: {_activeScene}", _lbl, GUILayout.MaxWidth(w));

        // Quick review (human visible)
        float tlxMean20 = (_tlxMental + _tlxPhysical + _tlxTemporal + _tlxPerformance + _tlxEffort + _tlxFrustration) / 6f;
        float tlxMean100 = tlxMean20 / 20f * 100f;

        float ieqRawMean = (_ieq1 + _ieq2 + _ieq3 + _ieq4 + _ieq5 + _ieq6 + _ieq7 + _ieq8) / 8f;
        int ieq5Rev = 6 - _ieq5; // reverse 1..5
        float ieqRevMean = (_ieq1 + _ieq2 + _ieq3 + _ieq4 + ieq5Rev + _ieq6 + _ieq7 + _ieq8) / 8f;
        float ieqRev100 = (ieqRevMean - 1f) / 4f * 100f;

        float gaMean = (_ga1 + _ga2) / 2f;

        GUILayout.Space(6);
        GUILayout.Label("<b>NASA-TLX</b>", _hdr);
        GUILayout.Label($"Mental:{_tlxMental}/20  •  Physical:{_tlxPhysical}/20  •  Temporal:{_tlxTemporal}/20  •  Performance:{_tlxPerformance}/20  •  Effort:{_tlxEffort}/20  •  Frustration:{_tlxFrustration}/20", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Label($"Mean workload: {tlxMean20:0.0}/20  ({tlxMean100:0} /100)", _lbl);

        GUILayout.Space(6);
        GUILayout.Label("<b>IEQ-SF</b>", _hdr);
        GUILayout.Label($"1:{_ieq1}  2:{_ieq2}  3:{_ieq3}  4:{_ieq4}  5(R):{_ieq5}  6:{_ieq6}  7:{_ieq7}  8:{_ieq8}", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Label($"Means → raw: {ieqRawMean:0.00}/5   •   reverse-applied: {ieqRevMean:0.00}/5  ({ieqRev100:0}/100)", _lbl);

        GUILayout.Space(6);
        GUILayout.Label("<b>Genomics Audibility</b>", _hdr);
        GUILayout.Label($"1:{_ga1}  2:{_ga2}", _lbl, GUILayout.MaxWidth(w));
        GUILayout.Label($"Mean: {gaMean:0.00}/5", _lbl);

        // --- One-word scene summary (free text, saved as a single token) ---
        GUILayout.Space(12);
        GUILayout.Label("<b>One-word summary</b>", _hdr, GUILayout.MaxWidth(w));
        GUILayout.Label("Please enter a single word that best describes this scene (no spaces). Only the first word and letters/digits/‘-’/‘_’ will be saved.", _note, GUILayout.MaxWidth(w));

        if (!_oneWordFocusRequested)
        {
            GUI.SetNextControlName("sceneOneWordField");
            _oneWordFocusRequested = true;
        }
        _sceneOneWord = GUILayout.TextField(_sceneOneWord ?? "", 64, _input, GUILayout.MaxWidth(Mathf.Min(360f, w)));

        string willSaveAs = SanitizeOneWord(_sceneOneWord);
        GUILayout.Label($"Will be saved as: <b>{(string.IsNullOrEmpty(willSaveAs) ? "<empty>" : willSaveAs)}</b>", _subtle, GUILayout.MaxWidth(w));

        GUILayout.Space(14);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Back", _btn, GUILayout.Width(160))) _stage = FlowStage.GENOMICS_AUD;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Submit & Continue", _btn, GUILayout.Width(260)))
        {
            TryWriteSurveysToJson(); // SPSS-friendly flat + human summary (includes one-word)
            _stage = FlowStage.Thanks;
            _redirectScheduled = true;
            _redirectAtUnscaled = Time.unscaledTime + postSurveyRedirectDelay;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawThanks(float w)
    {
        GUILayout.Label("<b>Thank you — responses saved.</b>", _title, GUILayout.MaxWidth(w));
        if (!string.IsNullOrEmpty(redirectSceneName))
        {
            float secsLeft = Mathf.Max(0f, _redirectAtUnscaled - Time.unscaledTime);
            GUILayout.Label($"Switching to ‘{redirectSceneName}’ in {secsLeft:0.0} s…", _lbl, GUILayout.MaxWidth(w));
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Go now", _btn, GUILayout.Width(180)))
                DoRedirect();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("No destination scene configured.", _lbl, GUILayout.MaxWidth(w));
        }
    }

    // ---- small IMGUI widgets & styles ---------------------------------------

    private static readonly string[] Likert = { "1", "2", "3", "4", "5" };

    private void BuildStyles()
    {
        _lbl = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true, fontSize = 16 };
        _hdr = new GUIStyle(_lbl) { fontStyle = FontStyle.Bold, fontSize = 18 };
        _title = new GUIStyle(_hdr) { fontSize = 20 };
        _subtle = new GUIStyle(_lbl) { fontSize = 14, normal = { textColor = new Color(0.88f, 0.88f, 0.88f, 0.95f) } };
        _note = new GUIStyle(_lbl) { fontSize = 15, normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 0.95f) } };
        _progress = new GUIStyle(_subtle) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Italic };
        _btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 38, padding = new RectOffset(14, 14, 8, 8) };

        _input = new GUIStyle(GUI.skin.textField) { fontSize = 16, padding = new RectOffset(8, 8, 6, 6) };

        // OPAQUE window background (no translucency)
        if (_windowBgTex == null)
            _windowBgTex = MakeTex(8, 8, new Color(0.09f, 0.09f, 0.09f, 1f)); // dark, fully opaque

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            padding = new RectOffset(10, 10, 24, 10)
        };
        _windowStyle.normal.background = _windowBgTex;
        _windowStyle.onNormal.background = _windowBgTex;
        _windowStyle.hover.background = _windowBgTex;
        _windowStyle.active.background = _windowBgTex;
        _windowStyle.focused.background = _windowBgTex;
        _windowStyle.onHover.background = _windowBgTex;
        _windowStyle.onActive.background = _windowBgTex;
        _windowStyle.onFocused.background = _windowBgTex;

        // Persistent banner style
        _banner = new GUIStyle(_note)
        {
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15
        };
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var fill = new Color32[(int)(width * height)];
        var c32 = (Color32)col;
        for (int i = 0; i < fill.Length; i++) fill[i] = c32;
        tex.SetPixels32(fill);
        tex.Apply(false, true);
        return tex;
    }

    // ---- TLX helpers ---------------------------------------------------------

    private string GetTlxQuestion(int i)
    {
        switch (i)
        {
            case 0: return "Mental Demand — How mentally demanding was the task?";
            case 1: return "Physical Demand — How physically demanding was the task?";
            case 2: return "Temporal Demand — How hurried or rushed was the pace?";
            case 3: return "Performance — How successful were you? (Perfect → Failure)";
            case 4: return "Effort — How hard did you have to work to achieve performance?";
            case 5: return "Frustration — How insecure, discouraged, irritated, stressed, and annoyed were you?";
            default: return "TLX item";
        }
    }
    private string GetTlxLeftAnchor(int i)  => (i == 3) ? "Perfect" : "Very Low";
    private string GetTlxRightAnchor(int i) => (i == 3) ? "Failure" : "Very High";

    private int GetTlxVal(int i)
    {
        switch (i)
        {
            case 0: return _tlxMental;
            case 1: return _tlxPhysical;
            case 2: return _tlxTemporal;
            case 3: return _tlxPerformance;
            case 4: return _tlxEffort;
            case 5: return _tlxFrustration;
            default: return 10;
        }
    }
    private void SetTlxVal(int i, int v)
    {
        switch (i)
        {
            case 0: _tlxMental = v; break;
            case 1: _tlxPhysical = v; break;
            case 2: _tlxTemporal = v; break;
            case 3: _tlxPerformance = v; break;
            case 4: _tlxEffort = v; break;
            case 5: _tlxFrustration = v; break;
        }
    }

    // ---- IEQ helpers ---------------------------------------------------------

    private string GetIeqText(int i)
    {
        switch (i)
        {
            case 0: return "I felt focused on the game.";
            case 1: return "The game was something that I was experiencing, rather than just doing.";
            case 2: return "I felt motivated when playing the game.";
            case 3: return "I enjoyed playing the game.";
            case 4: return "I felt consciously aware of being in the real world whilst playing.";
            case 5: return "I forgot about my everyday concerns.";
            case 6: return "I felt that I was separated from the real-world environment.";
            case 7: return "I found myself so involved that I was unaware I was using controls.";
            default: return "IEQ item";
        }
    }
    private bool GetIeqReverse(int i) => (i == 4);

    private int GetIeqVal(int i)
    {
        switch (i)
        {
            case 0: return _ieq1;
            case 1: return _ieq2;
            case 2: return _ieq3;
            case 3: return _ieq4;
            case 4: return _ieq5;
            case 5: return _ieq6;
            case 6: return _ieq7;
            case 7: return _ieq8;
            default: return 3;
        }
    }
    private void SetIeqVal(int i, int v)
    {
        switch (i)
        {
            case 0: _ieq1 = v; break;
            case 1: _ieq2 = v; break;
            case 2: _ieq3 = v; break;
            case 3: _ieq4 = v; break;
            case 4: _ieq5 = v; break;
            case 5: _ieq6 = v; break;
            case 6: _ieq7 = v; break;
            case 7: _ieq8 = v; break;
        }
    }

    // ---- Genomics Audibility helpers ----------------------------------------

    private string GetGaText(int i)
    {
        switch (i)
        {
            case 0: return "I could comfortably hear changes in the genomic parameters.";
            case 1: return "As the cells evolved, it was easy to listen to their changes.";
            default: return "Audibility item";
        }
    }

    private int GetGaVal(int i)
    {
        switch (i)
        {
            case 0: return _ga1;
            case 1: return _ga2;
            default: return 3;
        }
    }

    private void SetGaVal(int i, int v)
    {
        switch (i)
        {
            case 0: _ga1 = v; break;
            case 1: _ga2 = v; break;
        }
    }

    // ---- stop EEG recording (robust, reflection-based) ----------------------

    private void StopEegRecordingIfAny()
    {
        if (_recordingStopped) return;
        try
        {
            var all = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name != "EEGMetricsUdpReceiver") continue;

                bool changed = false;

                // recording field (bool)
                var recField = t.GetField("recording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (recField != null && recField.FieldType == typeof(bool))
                {
                    try
                    {
                        bool isOn = (bool)recField.GetValue(mb);
                        if (isOn) { recField.SetValue(mb, false); changed = true; }
                    }
                    catch { }
                }

                // recording property (bool)
                if (!changed)
                {
                    var recProp = t.GetProperty("recording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (recProp != null && recProp.CanWrite && recProp.PropertyType == typeof(bool))
                    {
                        try
                        {
                            bool isOn = (bool)(recProp.GetValue(mb) ?? false);
                            if (isOn) { recProp.SetValue(mb, false); changed = true; }
                        }
                        catch { }
                    }
                }

                // SetRecording(false) / StopRecording()
                if (!changed)
                {
                    var setRec = t.GetMethod("SetRecording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
                    if (setRec != null)
                    {
                        try { setRec.Invoke(mb, new object[] { false }); changed = true; } catch { }
                    }
                }
                if (!changed)
                {
                    var stopRec = t.GetMethod("StopRecording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (stopRec != null)
                    {
                        try { stopRec.Invoke(mb, null); changed = true; } catch { }
                    }
                }

                // FlushNow() if available; else SendMessage
                var flush = t.GetMethod("FlushNow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (flush != null)
                {
                    try { flush.Invoke(mb, null); } catch { }
                }
                else
                {
                    try { mb.gameObject.SendMessage("FlushNow", SendMessageOptions.DontRequireReceiver); } catch { }
                }

                if (changed)
                    Debug.Log("[SceneTimer] EEG recording stopped before wrap-up.");
                else
                    Debug.Log("[SceneTimer] EEG receiver found; recording already off (flushed).");

                _recordingStopped = true;
                break; // assume single receiver
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SceneTimer] EEG stop attempt failed: " + ex.Message);
        }
    }

    // ---- redirection ---------------------------------------------------------

    private void DoRedirect()
    {
        _redirectScheduled = false;

        if (string.IsNullOrEmpty(redirectSceneName))
        {
            Debug.LogWarning("[SceneTimer] redirectSceneName is empty.");
            return;
        }

        if (!CanLoadSceneByName(redirectSceneName))
        {
            Debug.LogWarning($"[SceneTimer] Scene '{redirectSceneName}' is not in Build Settings. "
                           + "Open File ▶ Build Settings… and add it under 'Scenes In Build'. "
                           + ListBuildScenesForDebug());
            return;
        }

        try { SceneManager.LoadScene(redirectSceneName); }
        catch (Exception ex) { Debug.LogError($"[SceneTimer] Failed to load scene '{redirectSceneName}': {ex.Message}"); }
    }

    private bool CanLoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private string ListBuildScenesForDebug()
    {
        var sb = new StringBuilder("Scenes In Build: ");
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            if (i > 0) sb.Append(", ");
            sb.Append(Path.GetFileNameWithoutExtension(path));
        }
        return sb.ToString();
    }

    // ---- survey → JSON (attach to existing user/session) ---------------------

    private void EnsureSessionBinding()
    {
        if (_resolvedSession) return;

        var path = GetUserFilePath();
        EnsureDataFolderExists();
        EnsureUnifiedFileExists();

        ExtendedUserDataContainer ext = LoadExtendedWithMigration(path);

        var user = string.IsNullOrWhiteSpace(_username) ? ResolveUsernameFromFile(ext) : _username;
        if (string.IsNullOrWhiteSpace(user)) user = "anonymous";
        _username = user;

        EnsureUserEntry(ext, user);

        var sess = FindLatestSessionForUserAndScene(ext.sessions, user, _activeScene);
        if (sess == null)
        {
            sess = new UserSession
            {
                username = user,
                scene = _activeScene,
                sessionId = Guid.NewGuid().ToString("N"),
                startedAtIso = DateTime.UtcNow.ToString("o"),
                samples = new List<Sample>()
            };
            if (ext.sessions == null) ext.sessions = new List<UserSession>();
            ext.sessions.Add(sess);
            TouchUpdated(ext, user);
            WriteJsonAtomic(path, JsonUtility.ToJson(ext, true));
        }

        _sessionIdForWrite = sess.sessionId;
        _resolvedSession = true;
    }

    private void TryWriteSurveysToJson()
    {
        try
        {
            var path = GetUserFilePath();
            var ext = LoadExtendedWithMigration(path);

            var user = string.IsNullOrWhiteSpace(_username) ? ResolveUsernameFromFile(ext) : _username;
            if (string.IsNullOrWhiteSpace(user)) user = "anonymous";
            EnsureUserEntry(ext, user);

            var sess = FindOrCreateSession(ext.sessions, user, _activeScene, _sessionIdForWrite);
            if (sess.samples == null) sess.samples = new List<Sample>();

            // ---- Compute stats once ----
            var nowIso = DateTime.UtcNow.ToString("o");

            float tlxMean20  = (_tlxMental + _tlxPhysical + _tlxTemporal + _tlxPerformance + _tlxEffort + _tlxFrustration) / 6f;
            float tlxMean100 = tlxMean20 / 20f * 100f;

            int ieq5Rev = 6 - _ieq5;
            float ieqMeanRaw = (_ieq1 + _ieq2 + _ieq3 + _ieq4 + _ieq5 + _ieq6 + _ieq7 + _ieq8) / 8f;
            float ieqMeanRev = (_ieq1 + _ieq2 + _ieq3 + _ieq4 + ieq5Rev + _ieq6 + _ieq7 + _ieq8) / 8f;
            float ieqMeanRev100 = (ieqMeanRev - 1f) / 4f * 100f;

            float gaMean = (_ga1 + _ga2) / 2f;

            string sceneWordSaved = SanitizeOneWord(_sceneOneWord);

            // ---- SPSS-friendly FLAT payload (one row) ----
            var flat = new CombinedSurveyFlat
            {
                username = user,
                scene = _activeScene,
                sessionId = _sessionIdForWrite,
                capturedAtIso = nowIso,

                tlx_mental = _tlxMental,
                tlx_physical = _tlxPhysical,
                tlx_temporal = _tlxTemporal,
                tlx_performance = _tlxPerformance,
                tlx_effort = _tlxEffort,
                tlx_frustration = _tlxFrustration,
                tlx_mean20 = tlxMean20,
                tlx_mean100 = tlxMean100,

                ieq_1 = _ieq1,
                ieq_2 = _ieq2,
                ieq_3 = _ieq3,
                ieq_4 = _ieq4,
                ieq_5_raw = _ieq5,
                ieq_5_rev = ieq5Rev,
                ieq_6 = _ieq6,
                ieq_7 = _ieq7,
                ieq_8 = _ieq8,
                ieq_mean_raw = ieqMeanRaw,
                ieq_mean_rev = ieqMeanRev,
                ieq_mean_rev100 = ieqMeanRev100,

                aud_1 = _ga1,
                aud_2 = _ga2,
                aud_mean = gaMean,

                scene_one_word = sceneWordSaved
            };

            // ---- HUMAN-readable combined text ----
            var text = new StringBuilder()
                .AppendLine("SURVEY SUMMARY")
                .AppendLine($"User: {user}    Scene: {_activeScene}    Session: {_sessionIdForWrite}")
                .AppendLine($"Time: {nowIso}")
                .AppendLine()
                .AppendLine("NASA-TLX (0–20; Performance: Perfect→Failure)")
                .AppendLine($"Mental:      {_tlxMental}/20")
                .AppendLine($"Physical:    {_tlxPhysical}/20")
                .AppendLine($"Temporal:    {_tlxTemporal}/20")
                .AppendLine($"Performance: {_tlxPerformance}/20")
                .AppendLine($"Effort:      {_tlxEffort}/20")
                .AppendLine($"Frustration: {_tlxFrustration}/20")
                .AppendLine($"Mean workload: {tlxMean20:0.0}/20  ({tlxMean100:0}/100)")
                .AppendLine()
                .AppendLine("IEQ-SF (1–5; item 5 reverse-scored)")
                .AppendLine($"1. Focused on the game — {_ieq1}/5")
                .AppendLine($"2. Experiencing rather than just doing — {_ieq2}/5")
                .AppendLine($"3. Motivated — {_ieq3}/5")
                .AppendLine($"4. Enjoyed playing — {_ieq4}/5")
                .AppendLine($"5. Aware of real world (R) — raw:{_ieq5}/5  rev:{ieq5Rev}/5")
                .AppendLine($"6. Forgot everyday concerns — {_ieq6}/5")
                .AppendLine($"7. Separated from real world — {_ieq7}/5")
                .AppendLine($"8. Unaware of using controls — {_ieq8}/5")
                .AppendLine($"Means → raw: {ieqMeanRaw:0.00}/5   •   reverse-applied: {ieqMeanRev:0.00}/5  ({ieqMeanRev100:0}/100)")
                .AppendLine()
                .AppendLine("Genomics Audibility (1–5)")
                .AppendLine($"1. Comfortably heard genomic parameter changes — {_ga1}/5")
                .AppendLine($"2. Easy to listen to cells’ changes as they evolved — {_ga2}/5")
                .AppendLine($"Mean: {gaMean:0.00}/5")
                .AppendLine()
                .AppendLine($"One-word scene summary: {(string.IsNullOrEmpty(sceneWordSaved) ? "<empty>" : sceneWordSaved)}")
                .ToString();

            // ---- Write samples: FLAT + TEXT ----
            var flatSample = new Sample
            {
                tIso = nowIso,
                type = "survey_flat",
                label = JsonUtility.ToJson(flat), // flat keys only (SPSS-friendly)
                count = 17, // 6 TLX + 8 IEQ + 2 Audibility + 1 one-word
                score = Mathf.RoundToInt(ieqMeanRev100) // quick-glance (keep IEQ as overall immersion score)
            };
            var textSample = new Sample
            {
                tIso = nowIso,
                type = "survey_text",
                label = text,
                count = 17
            };

            sess.samples.Add(flatSample);
            sess.samples.Add(textSample);

            TouchUpdated(ext, user);
            WriteJsonAtomic(path, JsonUtility.ToJson(ext, true));
            Debug.Log("[SceneTimer] Survey responses written: survey_flat + survey_text (includes one-word).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SceneTimer] Failed to write survey responses: " + ex.Message);
        }
    }

    // ---- JSON helpers --------------------------------------------------------

    private ExtendedUserDataContainer LoadExtendedWithMigration(string path)
    {
        if (!File.Exists(path)) return new ExtendedUserDataContainer();
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new ExtendedUserDataContainer();

        try
        {
            var ext = JsonUtility.FromJson<ExtendedUserDataContainer>(text);
            if (ext != null)
            {
                if (ext.entries == null)  ext.entries  = new List<UserDataEntry>();
                if (ext.sessions == null) ext.sessions = new List<UserSession>();
                return ext;
            }
        }
        catch { }
        return new ExtendedUserDataContainer { entries = new List<UserDataEntry>(), sessions = new List<UserSession>() };
    }

    private static void EnsureUserEntry(ExtendedUserDataContainer ext, string username)
    {
        if (ext.entries == null) ext.entries = new List<UserDataEntry>();
        var now = DateTime.UtcNow.ToString("o");
        for (int i = ext.entries.Count - 1; i >= 0; i--)
        {
            var e = ext.entries[i];
            if (e != null && string.Equals(e.username, username, StringComparison.OrdinalIgnoreCase))
                return;
        }
        ext.entries.Add(new UserDataEntry { username = username, createdAtIso = now, updatedAtIso = now, version = 1 });
    }

    private static void TouchUpdated(ExtendedUserDataContainer ext, string username)
    {
        var now = DateTime.UtcNow.ToString("o");
        if (ext.entries == null) return;
        for (int i = ext.entries.Count - 1; i >= 0; i--)
        {
            var e = ext.entries[i];
            if (e != null && string.Equals(e.username, username, StringComparison.OrdinalIgnoreCase))
            { e.updatedAtIso = now; return; }
        }
    }

    private static UserSession FindLatestSessionForUserAndScene(List<UserSession> list, string user, string scene)
    {
        if (list == null || list.Count == 0) return null;
        UserSession best = null;
        DateTime bestStart = DateTime.MinValue;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s == null) continue;
            if (!string.Equals(s.username, user, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(s.scene, scene, StringComparison.OrdinalIgnoreCase)) continue;
            if (!DateTime.TryParse(s.startedAtIso, null, DateTimeStyles.RoundtripKind, out var dt)) dt = DateTime.MinValue;
            if (dt > bestStart) { best = s; bestStart = dt; }
        }
        return best;
    }

    private static UserSession FindOrCreateSession(List<UserSession> list, string user, string scene, string sessionId)
    {
        if (list == null) list = new List<UserSession>();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s == null) continue;
            if (s.username == user && s.scene == scene && s.sessionId == sessionId) return s;
        }
        var created = new UserSession
        {
            username = user,
            scene = scene,
            sessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString("N") : sessionId,
            startedAtIso = DateTime.UtcNow.ToString("o"),
            samples = new List<Sample>()
        };
        list.Add(created);
        return created;
    }

    private static void WriteJsonAtomic(string path, string json)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, new UTF8Encoding(false));
#if UNITY_EDITOR
        try { File.Delete(path); } catch { }
#endif
        File.Copy(tmp, path, true);
        try { File.Delete(tmp); } catch { }
#if UNITY_EDITOR
        try { UnityEditor.AssetDatabase.Refresh(); } catch { }
#endif
    }

    private string ResolveUsername()
    {
        if (!string.IsNullOrWhiteSpace(overrideUsername))
            return overrideUsername.Trim();

        try
        {
            if (PlayerPrefs.HasKey("LastUsername"))
            {
                var pp = PlayerPrefs.GetString("LastUsername");
                if (!string.IsNullOrWhiteSpace(pp)) return pp.Trim();
            }
        }
        catch { }
        // Fallback to file last entry (deferred until EnsureSessionBinding)
        return "";
    }

    private static string ResolveUsernameFromFile(ExtendedUserDataContainer ext)
    {
        try
        {
            if (ext?.entries != null && ext.entries.Count > 0)
            {
                var last = ext.entries[ext.entries.Count - 1];
                if (last != null && !string.IsNullOrWhiteSpace(last.username))
                    return last.username.Trim();
            }
        }
        catch { }
        return "";
    }

    private void EnsureDataFolderExists()
    {
        string folder = GetDataFolderPath();
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
    }

    private void EnsureUnifiedFileExists()
    {
        var path = GetUserFilePath();
        if (!File.Exists(path))
            WriteJsonAtomic(path, JsonUtility.ToJson(new ExtendedUserDataContainer(), true));
    }

    private string GetUserFilePath() => Path.Combine(GetDataFolderPath(), userFileName);

    private string GetDataFolderPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, dataFolderName);
#else
        return Path.Combine(Application.persistentDataPath, dataFolderName);
#endif
    }

    // ---- serialisable payload: COMBINED, FLAT (SPSS-friendly) ---------------

    [Serializable]
    private class CombinedSurveyFlat
    {
        // identity
        public string username;
        public string scene;
        public string sessionId;
        public string capturedAtIso;

        // TLX (0..20; perf higher = worse)
        public int tlx_mental, tlx_physical, tlx_temporal, tlx_performance, tlx_effort, tlx_frustration;
        public float tlx_mean20;   // 0..20
        public float tlx_mean100;  // 0..100

        // IEQ-SF (1..5; item 5 reverse-scored)
        public int ieq_1, ieq_2, ieq_3, ieq_4, ieq_5_raw, ieq_5_rev, ieq_6, ieq_7, ieq_8;
        public float ieq_mean_raw;     // 1..5
        public float ieq_mean_rev;     // 1..5 (reverse applied)
        public float ieq_mean_rev100;  // 0..100

        // Genomics Audibility (1..5)
        public int aud_1, aud_2;
        public float aud_mean; // 1..5

        // One-word scene summary (sanitised single token)
        public string scene_one_word;
    }

    // ---- utilities -----------------------------------------------------------

    private static string SanitizeOneWord(string input, int maxLen = 32)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var trimmed = input.Trim();

        // Take first whitespace-delimited token
        var parts = trimmed.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string first = parts.Length > 0 ? parts[0] : "";

        // Keep only letters/digits and '-' or '_'
        var sb = new StringBuilder();
        for (int i = 0; i < first.Length; i++)
        {
            char ch = first[i];
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch);
        }
        string res = sb.ToString();
        if (res.Length > maxLen) res = res.Substring(0, maxLen);
        return res;
    }
}
