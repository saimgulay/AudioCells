// Assets/Scripts/SceneManagers/UserData.cs
// IMGUI window (OnGUI + GUILayout.Window) for collecting a "Given Username".
// - Edit → Confirm flow with Back.
// - Appends to JSON; BUT if username already exists (case-insensitive),
//   it does NOT create a new entry — it updates the existing record's updatedAtIso.
// - Checks scene availability before loading (no log spam).
// British English comments and UI text.

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserData : MonoBehaviour
{
    [Header("Navigation")]
    [Tooltip("Scene to load after saving. Must be added to File ▶ Build Settings…")]
    public string nextSceneName = "Main";

    [Header("Behaviour & UI")]
    public bool showWindow = true;                 // toggle with your own key if desired
    public Vector2 windowPosition = new Vector2(40, 120);
    public Vector2 windowSize = new Vector2(520, 260);
    public bool allowDrag = true;

    [Header("File Options")]
    [Tooltip("Folder name under Assets/ (Editor) or persistentDataPath (Build).")]
    public string dataFolderName = "Data";
    [Tooltip("JSON file name.")]
    public string fileName = "UserData.json";

    private Rect _windowRect;
    private Vector2 _scroll = Vector2.zero;
    private int _windowId;

    private enum Step { Edit, Confirm, Info }
    private Step _step = Step.Edit;

    private string _username = "";
    private string _error = "";
    private string _info = "";

    private enum SaveResult { Created, Updated }

    // ---- JSON models ----
    [Serializable]
    private class UserDataEntry
    {
        public string username;
        public string createdAtIso;
        public string updatedAtIso;
        public int version = 1;
    }
    [Serializable]
    private class UserDataContainer
    {
        public List<UserDataEntry> entries = new List<UserDataEntry>();
    }
    // legacy (for migration)
    [Serializable]
    private class LegacyUserDataModel
    {
        public string username;
        public string createdAtIso;
        public string updatedAtIso;
        public int version;
    }

    void Awake()
    {
        _windowId = GetInstanceID();
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);

        // prefill last known username (if any)
        string last = TryGetLastUsername();
        if (!string.IsNullOrEmpty(last)) _username = last;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U)) showWindow = !showWindow; // optional toggle
    }

    void OnGUI()
    {
        if (!showWindow) return;
        _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "User Data — Given Username");
        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize = new Vector2(_windowRect.width, _windowRect.height);
    }

    private void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        if (_step == Step.Edit)
        {
            GUILayout.Label("Please enter your given username, then press Continue.");
            GUILayout.Space(6);

            GUILayout.Label("Username:");
            GUI.SetNextControlName("usernameField");
            _username = GUILayout.TextField(_username ?? string.Empty, 64);
            GUILayout.Space(4);

            if (!string.IsNullOrEmpty(_error))
                GUILayout.Label("<color=#FF5050>" + _error + "</color>");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = ValidateUsername(_username, out _);
            if (GUILayout.Button("Continue", GUILayout.Width(120)))
            {
                if (ValidateUsername(_username, out string err))
                {
                    _error = string.Empty;
                    _step = Step.Confirm;
                }
                else _error = err;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.Repaint)
                GUI.FocusControl("usernameField");
        }
        else if (_step == Step.Confirm)
        {
            GUILayout.Label("Please confirm your details:");
            GUILayout.Space(4);
            GUILayout.Label($"Username: <b>{_username}</b>");
            GUILayout.Space(8);
            GUILayout.Label("<i>Use Back to make corrections if needed.</i>");
            GUILayout.Space(10);

            if (!string.IsNullOrEmpty(_error))
                GUILayout.Label("<color=#FF5050>" + _error + "</color>");
            if (!string.IsNullOrEmpty(_info))
                GUILayout.Label(_info);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save & Continue", GUILayout.Width(160)))
            {
                OnSaveAndContinue();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                _info = string.Empty;
                _error = string.Empty;
                _step = Step.Edit;
            }
            GUILayout.EndHorizontal();
        }
        else if (_step == Step.Info)
        {
            if (!string.IsNullOrEmpty(_info)) GUILayout.Label(_info);
            else GUILayout.Label("Saved.");

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK", GUILayout.Width(80)))
                _step = Step.Edit;
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        if (allowDrag)
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    // ---- flow ----
    private void OnSaveAndContinue()
    {
        _error = string.Empty;
        _info = string.Empty;

        if (!ValidateUsername(_username, out string err))
        {
            _error = err;
            return;
        }

        SaveResult result;
        try
        {
            result = AppendOrUpdateUserEntry(_username);
        }
        catch (Exception ex)
        {
            _error = "Failed to save user data: " + ex.Message;
            return;
        }

        // Helpful message if we remain on this scene
        string savedMsg = result == SaveResult.Updated
            ? "Saved: existing user updated."
            : "Saved: new user created.";

        if (string.IsNullOrEmpty(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            _info =
                $"{savedMsg}\n\n" +
                $"However, the scene '<b>{nextSceneName}</b>' is not available to load.\n" +
                $"Add it to <i>File ▶ Build Settings…</i> (Scenes In Build) or set a valid scene name in the Inspector.";
            _step = Step.Info;
            return;
        }

        try
        {
            SceneManager.LoadScene(nextSceneName);
        }
        catch (Exception ex)
        {
            _info = $"{savedMsg}\n\nFailed to load scene '<b>{nextSceneName}</b>':\n{ex.Message}";
            _step = Step.Info;
        }
    }

    // ---- validation ----
    private bool ValidateUsername(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name)) { error = "Username cannot be empty."; return false; }
        if (name.Length < 2) { error = "Username must be at least 2 characters."; return false; }
        foreach (char c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' '))
            { error = "Only letters, digits, spaces, '_' and '-' are allowed."; return false; }
        }
        error = string.Empty;
        return true;
    }

    // ---- paths ----
    private string GetDataFolderPath()
    {
#if UNITY_EDITOR
        // Editor: under Assets/Data as requested.
        return Path.Combine(Application.dataPath, dataFolderName);
#else
        // Builds: safe, writable location.
        return Path.Combine(Application.persistentDataPath, dataFolderName);
#endif
    }
    private string GetFilePath() => Path.Combine(GetDataFolderPath(), fileName);

    // ---- JSON I/O (append or update; preserve history; no duplicates) ----
    private SaveResult AppendOrUpdateUserEntry(string username)
    {
        string folder = GetDataFolderPath();
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var container = LoadContainerWithMigration();

        // Find existing user by case-insensitive match
        UserDataEntry existing = null;
        if (container.entries != null)
        {
            for (int i = container.entries.Count - 1; i >= 0; i--)
            {
                var e = container.entries[i];
                if (e != null && !string.IsNullOrEmpty(e.username) &&
                    string.Equals(e.username, username, StringComparison.OrdinalIgnoreCase))
                {
                    existing = e;
                    break;
                }
            }
        }

        var now = DateTime.UtcNow.ToString("o");

        if (existing != null)
        {
            // Update only metadata; do not create a duplicate
            existing.updatedAtIso = now;
            // keep createdAtIso as-is
            string jsonUpdate = JsonUtility.ToJson(container, true);
            File.WriteAllText(GetFilePath(), jsonUpdate);
            return SaveResult.Updated;
        }
        else
        {
            // Create a brand new record
            if (container.entries == null) container.entries = new List<UserDataEntry>();
            container.entries.Add(new UserDataEntry
            {
                username = username,
                createdAtIso = now,
                updatedAtIso = now,
                version = 1
            });
            string jsonNew = JsonUtility.ToJson(container, true);
            File.WriteAllText(GetFilePath(), jsonNew);
            return SaveResult.Created;
        }
    }

    private UserDataContainer LoadContainerWithMigration()
    {
        string path = GetFilePath();
        if (!File.Exists(path)) return new UserDataContainer();

        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new UserDataContainer();

        // Modern array container
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
                    username = legacy.username,
                    createdAtIso = string.IsNullOrEmpty(legacy.createdAtIso) ? DateTime.UtcNow.ToString("o") : legacy.createdAtIso,
                    updatedAtIso = string.IsNullOrEmpty(legacy.updatedAtIso) ? legacy.createdAtIso : legacy.updatedAtIso,
                    version = legacy.version <= 0 ? 1 : legacy.version
                });
                return migrated;
            }
        }
        catch { }

        // Unknown/corrupt: back up then start fresh
        try { File.WriteAllText(path + ".bak_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), text); } catch { }
        return new UserDataContainer();
    }

    private string TryGetLastUsername()
    {
        string path = GetFilePath();
        if (!File.Exists(path)) return null;
        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var c = JsonUtility.FromJson<UserDataContainer>(text);
            if (c != null && c.entries != null && c.entries.Count > 0)
                return c.entries[c.entries.Count - 1].username;
        }
        catch { }

        try
        {
            var legacy = JsonUtility.FromJson<LegacyUserDataModel>(text);
            return legacy?.username;
        }
        catch { }

        return null;
    }
}
