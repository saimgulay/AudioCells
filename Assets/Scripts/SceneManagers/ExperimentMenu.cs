using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// IMGUI-based experiment menu controlled by the currently signed-in user.
/// - Reads username from UserData.json (Assets/Data or persistentDataPath) or PlayerPrefs("LastUsername").
/// - Shows buttons for configured scenes: Tutorial, Sonification J/K/L/M/N, and "Quit Experiment".
/// - Disables scene buttons when no username is present; displays a helpful notice instead.
/// - Verifies scene availability in Build Settings before loading; shows an inline message if missing.
/// - British English comments & UI text; layout style matching your prior IMGUI windows.
///
/// NEW (v2):
/// - Intercepts "Quit Experiment" and shows a modal prompt (English) asking:
///     "Before we close: if you were to use one of these sonification styles in your own project,
///      which scene would you pick?"
///   Options are derived from the configured Sonification scenes (J/K/L/M/N) that have non-empty names.
/// - On "Confirm & Quit", appends a preference record to the *same* UserData.json under `preferences`.
///   (Structure is backward-compatible; older files without `preferences` remain valid.)
/// </summary>
public class ExperimentMenu : MonoBehaviour
{
    [Header("Scenes (must be in File ▶ Build Settings…)")]
    public string tutorialSceneName       = "Tutorial";
    public string sonificationJSceneName  = "Sonification_J";
    public string sonificationKSceneName  = "Sonification_K";
    public string sonificationLSceneName  = "Sonification_L";
    public string sonificationMSceneName  = "Sonification_M";
    public string sonificationNSceneName  = "Sonification_N"; // NEW

    [Header("UI Window")]
    public bool showWindow = true;                 // Toggle if you like (e.g., with a key)
    public Vector2 windowPosition = new Vector2(40, 80);
    public Vector2 windowSize     = new Vector2(520, 300);
    public bool allowDrag = true;

    [Header("User & Files")]
    [Tooltip("Folder name under Assets/ (Editor) or persistentDataPath (Build).")]
    public string dataFolderName = "Data";
    [Tooltip("The user listing JSON created by your UserData scene.")]
    public string userFileName   = "UserData.json";

    private Rect _windowRect;
    private Vector2 _scroll = Vector2.zero;

    // current user snapshot
    private string _username = "";
    private string _userInfoLine = "";  // created/updated if available
    private string _message = "";       // inline info/warnings (e.g., scene missing)

    // --- JSON models for reading last user ---
    [Serializable] private class UserDataEntry
    {
        public string username;
        public string createdAtIso;
        public string updatedAtIso;
        public int version = 1;
    }

    /// <summary>
    /// NEW: preference records appended when the user confirms a sonification choice at quit time.
    /// </summary>
    [Serializable] private class UserPreferenceEntry
    {
        public string username;          // may be "(none)" if no username was set
        public string choiceLabel;       // e.g., "Sonification J"
        public string sceneName;         // e.g., "Sonification_J"
        public string recordedAtIso;     // ISO 8601 (UTC)
        public string source = "quit_experiment_prompt";
        public int version = 1;
    }

    [Serializable] private class UserDataContainer
    {
        public List<UserDataEntry> entries = new List<UserDataEntry>();

        // NEW (optional; absent in legacy files):
        public List<UserPreferenceEntry> preferences = new List<UserPreferenceEntry>();
    }

    [Serializable] private class LegacyUserDataModel
    {
        public string username;
        public string createdAtIso;
        public string updatedAtIso;
        public int version;
    }

    // --- Quit prompt state ---
    private bool _showQuitPrompt = false;
    private int _quitSelectedIndex = -1;

    private struct SonificationOption
    {
        public string label;     // "Sonification J"
        public string sceneName; // "Sonification_J"
    }
    private List<SonificationOption> _quitOptions = new List<SonificationOption>();

    void Awake()
    {
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        ResolveCurrentUser();
        if (!string.IsNullOrWhiteSpace(_username))
        {
            try { PlayerPrefs.SetString("LastUsername", _username); PlayerPrefs.Save(); } catch { }
        }
    }

    void Update()
    {
        // Example toggle key for showing/hiding the menu (feel free to change)
        if (Input.GetKeyDown(KeyCode.M))
            showWindow = !showWindow;
    }

    void OnGUI()
    {
        if (!showWindow) return;

        // Allow <b>, <i>, <color> tags in labels (Unity rich text uses American spelling internally).
        var prevRich = GUI.skin.label.richText;
        GUI.skin.label.richText = true;

        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Experiment Menu");
        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize     = new Vector2(_windowRect.width, _windowRect.height);

        if (_showQuitPrompt)
            DrawQuitPromptModal();

        GUI.skin.label.richText = prevRich;
    }

    private void DrawWindow(int id)
    {
        // Scroll area so long content doesn’t overflow
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label($"Current user: {(string.IsNullOrEmpty(_username) ? "<none>" : _username)}");
        if (!string.IsNullOrEmpty(_userInfoLine))
            GUILayout.Label(_userInfoLine);

        if (string.IsNullOrEmpty(_username))
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>No username found.</b>\n" +
                            "Please run the user entry scene first to create/confirm a username.\n" +
                            "(That scene writes to Data/UserData.json and sets PlayerPrefs.)");
        }

        GUILayout.Space(8);
        if (!string.IsNullOrEmpty(_message))
        {
            GUILayout.Label("<color=#FF8040>" + _message + "</color>");
            GUILayout.Space(6);
        }

        // Disable scene buttons when there is no username
        bool canEnter = !string.IsNullOrEmpty(_username);
        GUI.enabled = canEnter;

        // 2) Tutorial
        DrawSceneButton("2) Tutorial", tutorialSceneName);

        // 3) Sonification J
        DrawSceneButton("3) Sonification J", sonificationJSceneName);

        // 4) Sonification K
        DrawSceneButton("4) Sonification K", sonificationKSceneName);

        // 5) Sonification L
        DrawSceneButton("5) Sonification L", sonificationLSceneName);

        // 6) Sonification M
        DrawSceneButton("6) Sonification M", sonificationMSceneName);

        // 7) Sonification N (NEW)
        DrawSceneButton("7) Sonification N", sonificationNSceneName);

        GUI.enabled = true;

        GUILayout.Space(10);
        // 8) Quit Experiment (renumbered)
        if (GUILayout.Button("8) Quit Experiment", GUILayout.Height(28)))
        {
            BeginQuitFlow();
        }

        GUILayout.EndScrollView();

        if (allowDrag)
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    // --- helpers: draw a single scene button with availability check ---
    private void DrawSceneButton(string label, string sceneName)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(160));
            GUILayout.FlexibleSpace();

            bool missingName = string.IsNullOrWhiteSpace(sceneName);
            bool existsInBuild = !missingName && SceneExistsInBuild(sceneName);

            GUI.enabled = !missingName && existsInBuild && !string.IsNullOrEmpty(_username);
            if (GUILayout.Button(missingName ? "(unset)" : sceneName, GUILayout.Width(220), GUILayout.Height(24)))
            {
                SafeLoadScene(sceneName);
            }
            GUI.enabled = true;

            string hint = missingName ? "— unset" : (existsInBuild ? "— ready" : "— not in Build Settings");
            GUILayout.Label(hint, GUILayout.Width(180));
        }
    }

    private void SafeLoadScene(string sceneName)
    {
        _message = "";
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            _message = "No scene name set.";
            return;
        }

        if (!SceneExistsInBuild(sceneName))
        {
            _message =
                $"The scene '<b>{sceneName}</b>' is not present in Build Settings.\n" +
                "Open File ▶ Build Settings… and add it under 'Scenes In Build'.";
            return;
        }

        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (Exception ex)
        {
            _message = $"Failed to load '<b>{sceneName}</b>': {ex.Message}";
        }
    }

    private bool SceneExistsInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // =========================
    // Quit flow (modal prompt)
    // =========================

    private void BeginQuitFlow()
    {
        // Build options list from configured sonification scenes that have non-empty names.
        _quitOptions.Clear();
        AddOptionIfValid("Sonification J", sonificationJSceneName);
        AddOptionIfValid("Sonification K", sonificationKSceneName);
        AddOptionIfValid("Sonification L", sonificationLSceneName);
        AddOptionIfValid("Sonification M", sonificationMSceneName);
        AddOptionIfValid("Sonification N", sonificationNSceneName);

        _quitSelectedIndex = -1;
        _showQuitPrompt = true;
    }

    private void AddOptionIfValid(string label, string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            _quitOptions.Add(new SonificationOption { label = label, sceneName = sceneName });
        }
    }

    private void DrawQuitPromptModal()
    {
        // Darken background a touch (simple overlay)
        var overlay = new Rect(0, 0, Screen.width, Screen.height);
        GUI.Box(overlay, GUIContent.none); // minimal blocker

        // Centre the modal window
        float w = 640f, h = 360f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUILayout.Window(GetInstanceID() ^ 0x5A5A, rect, DrawQuitPromptContents, "Before we close…");
    }

    private void DrawQuitPromptContents(int id)
    {
        GUILayout.Label(
            "Before we close: if you were to use one of these sonification styles in your own project,\n" +
            "which scene would you pick? Please choose one.");

        GUILayout.Space(6);

        if (_quitOptions.Count == 0)
        {
            GUILayout.Label("No sonification scenes are configured. You may proceed to quit.");
        }
        else
        {
            // Radio-like exclusive toggles
            for (int i = 0; i < _quitOptions.Count; i++)
            {
                bool selected = (_quitSelectedIndex == i);
                bool now = GUILayout.Toggle(selected, $"{_quitOptions[i].label} — scene “{_quitOptions[i].sceneName}”");
                if (now && !selected) _quitSelectedIndex = i;
            }
        }

        GUILayout.FlexibleSpace();

        using (new GUILayout.HorizontalScope())
        {
            // Cancel (return to menu)
            if (GUILayout.Button("Cancel", GUILayout.Height(26), GUILayout.Width(120)))
            {
                _showQuitPrompt = false;
            }

            GUILayout.FlexibleSpace();

            // Skip & Quit (do not record)
            if (GUILayout.Button("Skip & Quit", GUILayout.Height(26), GUILayout.Width(140)))
            {
                FinaliseQuit(recordChoice: false);
            }

            // Confirm & Quit (record)
            GUI.enabled = (_quitSelectedIndex >= 0 && _quitOptions.Count > 0);
            if (GUILayout.Button("Confirm & Quit", GUILayout.Height(28), GUILayout.Width(160)))
            {
                FinaliseQuit(recordChoice: true);
            }
            GUI.enabled = true;
        }
    }

    private void FinaliseQuit(bool recordChoice)
    {
        if (recordChoice && _quitSelectedIndex >= 0 && _quitSelectedIndex < _quitOptions.Count)
        {
            var opt = _quitOptions[_quitSelectedIndex];
            string uname = string.IsNullOrWhiteSpace(_username) ? "(none)" : _username;
            TryAppendPreference(uname, opt.label, opt.sceneName);
        }

        // Proceed to actually quit
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void TryAppendPreference(string username, string choiceLabel, string sceneName)
    {
        try
        {
            var container = LoadContainerWithMigration();
            if (container.preferences == null)
                container.preferences = new List<UserPreferenceEntry>();

            container.preferences.Add(new UserPreferenceEntry
            {
                username      = username,
                choiceLabel   = choiceLabel,
                sceneName     = sceneName,
                recordedAtIso = DateTime.UtcNow.ToString("o"),
                source        = "quit_experiment_prompt",
                version       = 1
            });

            SaveContainer(container);
        }
        catch (Exception ex)
        {
            // Non-fatal: carry on quitting even if we fail to write
            Debug.LogWarning($"ExperimentMenu: failed to append preference to JSON: {ex.Message}");
        }
    }

    // --- user resolution (matches your UserData.cs writing format) ---

    private void ResolveCurrentUser()
    {
        // 1) PlayerPrefs (most recent, set by your user entry scene)
        try
        {
            if (PlayerPrefs.HasKey("LastUsername"))
            {
                var pp = PlayerPrefs.GetString("LastUsername");
                if (!string.IsNullOrWhiteSpace(pp))
                {
                    _username = pp.Trim();
                    // Try to populate created/updated timestamps from file
                    TryPopulateUserTimestamps(_username);
                    return;
                }
            }
        }
        catch { }

        // 2) UserData.json (last entry)
        try
        {
            var last = TryGetLastUserFromFile();
            if (!string.IsNullOrWhiteSpace(last))
            {
                _username = last.Trim();
                TryPopulateUserTimestamps(_username);
                return;
            }
        }
        catch { }

        _username = "";
        _userInfoLine = "";
    }

    private void TryPopulateUserTimestamps(string username)
    {
        try
        {
            var c = LoadContainerWithMigration();
            if (c.entries != null)
            {
                for (int i = c.entries.Count - 1; i >= 0; i--)
                {
                    var e = c.entries[i];
                    if (e != null && !string.IsNullOrEmpty(e.username) &&
                        string.Equals(e.username, username, StringComparison.OrdinalIgnoreCase))
                    {
                        _userInfoLine = $"Created: {e.createdAtIso}    Updated: {e.updatedAtIso}";
                        return;
                    }
                }
            }
        }
        catch
        {
            _userInfoLine = "";
        }
    }

    private string TryGetLastUserFromFile()
    {
        var c = LoadContainerWithMigration();
        if (c.entries != null && c.entries.Count > 0)
            return c.entries[c.entries.Count - 1].username;
        return null;
    }

    private UserDataContainer LoadContainerWithMigration()
    {
        string path = GetUserFilePath();
        if (!File.Exists(path)) return new UserDataContainer();

        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new UserDataContainer();

        // Modern list container (may or may not include 'preferences')
        try
        {
            var c = JsonUtility.FromJson<UserDataContainer>(text);
            if (c != null && c.entries != null) return c;
        }
        catch { }

        // Legacy single object -> migrate into list container
        try
        {
            var legacy = JsonUtility.FromJson<LegacyUserDataModel>(text);
            if (legacy != null && !string.IsNullOrEmpty(legacy.username))
            {
                var migrated = new UserDataContainer();
                migrated.entries.Add(new UserDataEntry
                {
                    username = legacy.username,
                    createdAtIso = string.IsNullOrEmpty(legacy.createdAtIso) ? DateTime.UtcNow.ToString("o") : legacy.createdAtIso,
                    updatedAtIso = string.IsNullOrEmpty(legacy.updatedAtIso) ? legacy.createdAtIso : legacy.updatedAtIso,
                    version = legacy.version <= 0 ? 1 : legacy.version
                });
                // preferences remains empty on migration (none recorded in legacy files)
                return migrated;
            }
        }
        catch { }

        // Unknown/corrupt → back up and return empty
        try { File.WriteAllText(path + ".bak_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), text); } catch { }
        return new UserDataContainer();
    }

    private void SaveContainer(UserDataContainer container)
    {
        string path = GetUserFilePath();
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch { /* ignore */ }

        try
        {
            string serialized = JsonUtility.ToJson(container, prettyPrint: true);
            File.WriteAllText(path, serialized);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ExperimentMenu: failed to write UserData.json: {ex.Message}");
        }
    }

    private string GetUserFilePath()
    {
#if UNITY_EDITOR
        var folder = Path.Combine(Application.dataPath, dataFolderName);
#else
        var folder = Path.Combine(Application.persistentDataPath, dataFolderName);
#endif
        return Path.Combine(folder, userFileName);
    }
}
