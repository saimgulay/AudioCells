// Assets/Scripts/SceneManagers/UserData.cs
// IMGUI username capture using the same unified JSON (entries + sessions).
// Uses shared models so sessions are preserved. British English comments & UI.

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ExperimentData; // <-- shared models

public class UserData : MonoBehaviour
{
    [Header("Navigation")]
    public string nextSceneName = "Main";

    [Header("Behaviour & UI")]
    public bool showWindow = true;
    public Vector2 windowPosition = new Vector2(40, 120);
    public Vector2 windowSize = new Vector2(520, 260);
    public bool allowDrag = true;

    [Header("File Options")]
    public string dataFolderName = "Data";
    public string fileName = "UserData.json";

    private Rect _windowRect;
    private Vector2 _scroll = Vector2.zero;
    private int _windowId;

    private enum Step { Edit, Confirm, Info }
    private Step _step = Step.Edit;

    private string _username = "";
    private string _error = "";
    private string _info = "";

    void Awake()
    {
        _windowId = GetInstanceID();
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);

        Directory.CreateDirectory(GetDataFolderPath());
        EnsureUnifiedFileExists();

        string last = TryGetLastUsername();
        if (!string.IsNullOrEmpty(last)) _username = last;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U)) showWindow = !showWindow;
    }

    void OnGUI()
    {
        if (!showWindow) return;
        _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "User Data — Given Username");
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

        try
        {
            var path = GetFilePath();
            var ext  = LoadExtended(path);

            // find or create user (preserving sessions as-is)
            var now = DateTime.UtcNow.ToString("o");
            bool found = false;
            if (ext.entries == null) ext.entries = new List<UserDataEntry>();
            for (int i = ext.entries.Count - 1; i >= 0; i--)
            {
                var e = ext.entries[i];
                if (e != null && !string.IsNullOrEmpty(e.username) &&
                    string.Equals(e.username, _username, StringComparison.OrdinalIgnoreCase))
                {
                    e.updatedAtIso = now;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                ext.entries.Add(new UserDataEntry { username = _username, createdAtIso = now, updatedAtIso = now, version = 1 });
            }

            WriteJsonAtomic(path, JsonUtility.ToJson(ext, true));
            try { PlayerPrefs.SetString("LastUsername", _username); PlayerPrefs.Save(); } catch { }
        }
        catch (Exception ex)
        {
            _error = "Failed to save user data: " + ex.Message;
            _step = Step.Info;
            return;
        }

        if (string.IsNullOrEmpty(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            _info =
                $"Saved.\n\nThe scene '<b>{nextSceneName}</b>' is not available to load.\n" +
                $"Add it to File ▶ Build Settings… (Scenes In Build) or set a valid scene name in the Inspector.";
            _step = Step.Info;
            return;
        }

        try { SceneManager.LoadScene(nextSceneName); }
        catch (Exception ex)
        {
            _info = $"Saved, but failed to load '<b>{nextSceneName}</b>':\n{ex.Message}";
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

    // ---- paths & I/O ----
    private string GetDataFolderPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, dataFolderName);
#else
        return Path.Combine(Application.persistentDataPath, dataFolderName);
#endif
    }
    private string GetFilePath() => Path.Combine(GetDataFolderPath(), fileName);

    private void EnsureUnifiedFileExists()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
            WriteJsonAtomic(path, JsonUtility.ToJson(new ExtendedUserDataContainer(), true));
    }

    private static ExtendedUserDataContainer LoadExtended(string path)
    {
        if (!File.Exists(path)) return new ExtendedUserDataContainer();
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new ExtendedUserDataContainer();

        try
        {
            var ext = JsonUtility.FromJson<ExtendedUserDataContainer>(text);
            if (ext != null && ext.entries != null && ext.sessions != null) return ext;
        }
        catch { }
        return new ExtendedUserDataContainer(); // fallback (preserves nothing if corrupt)
    }

    private static void WriteJsonAtomic(string path, string json)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, new System.Text.UTF8Encoding(false));
#if UNITY_EDITOR
        try { File.Delete(path); } catch { }
#endif
        File.Copy(tmp, path, true);
        try { File.Delete(tmp); } catch { }
#if UNITY_EDITOR
        try { UnityEditor.AssetDatabase.Refresh(); } catch { }
#endif
    }

    private string TryGetLastUsername()
    {
        string path = GetFilePath();
        if (!File.Exists(path)) return null;
        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var ext = JsonUtility.FromJson<ExtendedUserDataContainer>(text);
            if (ext != null && ext.entries != null && ext.entries.Count > 0)
                return ext.entries[ext.entries.Count - 1].username;
        }
        catch { }

        return null;
    }
}
