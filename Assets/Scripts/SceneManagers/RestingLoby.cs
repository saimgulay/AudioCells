// Assets/Scripts/SceneManagers/RestingLoby.cs
// Resting lobby with countdown. You can freely select and change the next
// scene whilst the timer is running; the *current* selection at 00:00 loads.
// British English comments & IMGUI style matching previous windows.

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RestingLoby : MonoBehaviour
{
    [Header("Scenes (must be in File ▶ Build Settings…)")]
    public string tutorialSceneName      = "Tutorial";
    public string sonificationJSceneName = "Sonification_J";
    public string sonificationKSceneName = "Sonification_K";
    public string sonificationLSceneName = "Sonification_L";
    public string sonificationMSceneName = "Sonification_M";

    [Header("Timer")]
    [Tooltip("Rest period length in minutes before the next scene starts automatically.")]
    [Min(0.1f)]
    public float restDurationMinutes = 2.0f;

    [Header("UI Window")]
    public bool showWindow = true;
    public Vector2 windowPosition = new Vector2(40, 80);
    public Vector2 windowSize     = new Vector2(580, 360);
    public bool allowDrag = true;

    [Header("User & Files")]
    [Tooltip("Folder name under Assets/ (Editor) or persistentDataPath (Build).")]
    public string dataFolderName = "Data";
    [Tooltip("The user listing JSON created by your UserData scene.")]
    public string userFileName   = "UserData.json";

    private Rect _windowRect;
    private Vector2 _scroll = Vector2.zero;

    // user snapshot
    private string _username = "";
    private string _userInfoLine = "";

    // selection + flow
    private string _selectedScene = "";   // last chosen scene (updates live)
    private string _message = "";         // inline info/warnings
    private bool _expired = false;        // timer reached zero
    private bool _redirected = false;     // to avoid multiple loads
    private float _deadlineUnscaled;      // Time.unscaledTime at which we expire

    // JSON models for reading user file
    [Serializable] private class UserDataEntry { public string username; public string createdAtIso; public string updatedAtIso; public int version = 1; }
    [Serializable] private class UserDataContainer { public List<UserDataEntry> entries = new List<UserDataEntry>(); }
    [Serializable] private class LegacyUserDataModel { public string username; public string createdAtIso; public string updatedAtIso; public int version; }

    void Awake()
    {
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        ResolveCurrentUser();

        // Set the deadline now; selection is allowed until we actually expire.
        _deadlineUnscaled = Time.unscaledTime + restDurationMinutes * 60f;

        // Provide a sensible default selection (shown and changeable).
        if (!string.IsNullOrWhiteSpace(tutorialSceneName))
            _selectedScene = tutorialSceneName;
    }

    void OnValidate()
    {
        if (restDurationMinutes < 0.1f) restDurationMinutes = 0.1f;
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
    }

    void Update()
    {
        if (_redirected) return;

        // When timer ends, try to load the current selection once.
        if (!_expired && Time.unscaledTime >= _deadlineUnscaled)
        {
            _expired = true;
            TryLoadSelectedAtExpiry();
        }
    }

    void OnGUI()
    {
        if (!showWindow) return;
        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Resting Loby");
        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize     = new Vector2(_windowRect.width, _windowRect.height);
    }

    private void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        // Header: user + countdown
        GUILayout.Label($"Current user: {(string.IsNullOrEmpty(_username) ? "<none>" : _username)}");
        if (!string.IsNullOrEmpty(_userInfoLine))
            GUILayout.Label(_userInfoLine);

        var remaining = Mathf.Max(0f, _deadlineUnscaled - Time.unscaledTime);
        var mm = Mathf.FloorToInt(remaining / 60f);
        var ss = Mathf.FloorToInt(remaining % 60f);

        GUILayout.Space(6);
        GUILayout.Label($"Time remaining: <b>{mm:00}:{ss:00}</b>");
        GUILayout.Label("You may select or change the next scene whilst the timer runs. "
                      + "The final selection at 00:00 will start automatically.");

        if (!string.IsNullOrEmpty(_message))
        {
            GUILayout.Space(6);
            GUILayout.Label("<color=#FF8040>" + _message + "</color>");
        }

        GUILayout.Space(10);

        // Selection rows — remain enabled until the timer truly expires.
        bool canSelect = !string.IsNullOrEmpty(_username) && !_expired;
        GUI.enabled = canSelect;

        DrawSelectRow("2) Tutorial",        tutorialSceneName);
        DrawSelectRow("3) Sonification J",  sonificationJSceneName);
        DrawSelectRow("4) Sonification K",  sonificationKSceneName);
        DrawSelectRow("5) Sonification L",  sonificationLSceneName);
        DrawSelectRow("6) Sonification M",  sonificationMSceneName);

        GUI.enabled = true;

        GUILayout.Space(10);
        // Prominent “current selection” readout (always visible)
        GUILayout.Label($"Current selection: <b>{(string.IsNullOrEmpty(_selectedScene) ? "— none —" : _selectedScene)}</b>");

        GUILayout.Space(12);
        // 7) Quit Experiment (allowed anytime)
        if (GUILayout.Button("7) Quit Experiment", GUILayout.Height(28)))
            QuitExperiment();

        GUILayout.EndScrollView();

        if (allowDrag)
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    private void DrawSelectRow(string label, string sceneName)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label, GUILayout.Width(180));
        GUILayout.FlexibleSpace();

        bool missingName   = string.IsNullOrWhiteSpace(sceneName);
        bool existsInBuild = !missingName && SceneExistsInBuild(sceneName);
        bool isSelected    = !missingName && string.Equals(_selectedScene, sceneName, StringComparison.OrdinalIgnoreCase);

        // Button stays enabled whilst counting down; it only *changes selection*.
        GUI.enabled = !missingName && existsInBuild && !_expired && !string.IsNullOrEmpty(_username);
        string btnText = missingName ? "(unset)"
                      : isSelected ? $"Selected: {sceneName}"
                                   : $"Select: {sceneName}";
        if (GUILayout.Button(btnText, GUILayout.Width(280), GUILayout.Height(24)))
        {
            _selectedScene = sceneName;    // live update
            _message = "";                 // clear any old warnings
        }
        GUI.enabled = true;

        string hint = missingName ? "— unset"
                    : existsInBuild ? (isSelected ? "— ready (selected)" : "— ready")
                                    : "— not in Build Settings";
        GUILayout.Label(hint, GUILayout.Width(180));

        GUILayout.EndHorizontal();
    }

    // Attempt to load when countdown hits zero
    private void TryLoadSelectedAtExpiry()
    {
        if (_redirected) return;

        if (string.IsNullOrEmpty(_username))
        {
            _message = "No username present — cannot start the next scene.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedScene))
        {
            _message = "No scene has been selected. Please set one next time.";
            return;
        }

        if (!SceneExistsInBuild(_selectedScene))
        {
            _message =
                $"The scene '<b>{_selectedScene}</b>' is not present in Build Settings.\n" +
                "Open File ▶ Build Settings… and add it under 'Scenes In Build'.";
            return;
        }

        try
        {
            _redirected = true;
            SceneManager.LoadScene(_selectedScene);
        }
        catch (Exception ex)
        {
            _redirected = false;
            _message = $"Failed to load '<b>{_selectedScene}</b>': {ex.Message}";
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

    private void QuitExperiment()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- user resolution (matches your UserData.cs) ----------
    private void ResolveCurrentUser()
    {
        // PlayerPrefs (preferred)
        try
        {
            if (PlayerPrefs.HasKey("LastUsername"))
            {
                var pp = PlayerPrefs.GetString("LastUsername");
                if (!string.IsNullOrWhiteSpace(pp))
                {
                    _username = pp.Trim();
                    TryPopulateUserTimestamps(_username);
                    return;
                }
            }
        }
        catch { }

        // Fallback: last entry in file
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

        // Modern list
        try
        {
            var c = JsonUtility.FromJson<UserDataContainer>(text);
            if (c != null && c.entries != null) return c;
        }
        catch { }

        // Legacy single object
        try
        {
            var legacy = JsonUtility.FromJson<LegacyUserDataModel>(text);
            if (legacy != null && !string.IsNullOrEmpty(legacy.username))
            {
                var migrated = new UserDataContainer();
                migrated.entries.Add(new UserDataEntry
                {
                    username    = legacy.username,
                    createdAtIso= string.IsNullOrEmpty(legacy.createdAtIso) ? DateTime.UtcNow.ToString("o") : legacy.createdAtIso,
                    updatedAtIso= string.IsNullOrEmpty(legacy.updatedAtIso) ? legacy.createdAtIso : legacy.updatedAtIso,
                    version     = legacy.version <= 0 ? 1 : legacy.version
                });
                return migrated;
            }
        }
        catch { }

        // Unknown/corrupt → backup and empty
        try { File.WriteAllText(path + ".bak_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), text); } catch { }
        return new UserDataContainer();
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
