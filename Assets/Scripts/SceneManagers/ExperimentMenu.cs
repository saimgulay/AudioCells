using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// IMGUI-based experiment menu controlled by the currently signed-in user.
/// - Reads username from UserData.json (Assets/Data or persistentDataPath) or PlayerPrefs("LastUsername").
/// - Shows buttons for configured scenes: Tutorial, Sonification J/K/L/M, and "Quit Experiment".
/// - Disables scene buttons when no username is present; displays a helpful notice instead.
/// - Verifies scene availability in Build Settings before loading; shows an inline message if missing.
/// - British English comments & UI text; layout style matching your prior IMGUI windows.
/// </summary>
public class ExperimentMenu : MonoBehaviour
{
    [Header("Scenes (must be in File ▶ Build Settings…)")]
    public string tutorialSceneName      = "Tutorial";
    public string sonificationJSceneName = "Sonification_J";
    public string sonificationKSceneName = "Sonification_K";
    public string sonificationLSceneName = "Sonification_L";
    public string sonificationMSceneName = "Sonification_M";

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
    [Serializable] private class UserDataEntry { public string username; public string createdAtIso; public string updatedAtIso; public int version = 1; }
    [Serializable] private class UserDataContainer { public List<UserDataEntry> entries = new List<UserDataEntry>(); }
    [Serializable] private class LegacyUserDataModel { public string username; public string createdAtIso; public string updatedAtIso; public int version; }

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
        // örn. M tuşu ile gizle/göster (istersen değiştir)
        if (Input.GetKeyDown(KeyCode.M))
            showWindow = !showWindow;
    }

    void OnGUI()
    {
        if (!showWindow) return;
        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Experiment Menu");
        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize     = new Vector2(_windowRect.width, _windowRect.height);
    }

    private void DrawWindow(int id)
    {
        // Küçük bir dikey scroll alanı (uzarsa taşmasın)
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

        // Butonları toplu halde devre dışı bırak (kullanıcı yokken)
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

        GUI.enabled = true;

        GUILayout.Space(10);
        // 7) Quit Experiment
        if (GUILayout.Button("7) Quit Experiment", GUILayout.Height(28)))
        {
            QuitExperiment();
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

    private void QuitExperiment()
    {
#if UNITY_EDITOR
        // Editor'de Play modunu bırak
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // --- user resolution (matches your UserData.cs writing format) ---

    private void ResolveCurrentUser()
    {
        // 1) PlayerPrefs (en güvenilir, en son sahnenin bıraktığı)
        try
        {
            if (PlayerPrefs.HasKey("LastUsername"))
            {
                var pp = PlayerPrefs.GetString("LastUsername");
                if (!string.IsNullOrWhiteSpace(pp))
                {
                    _username = pp.Trim();
                    // Bilgi satırı için dosyadan tarihleri de deneriz
                    TryPopulateUserTimestamps(_username);
                    return;
                }
            }
        }
        catch { }

        // 2) UserData.json (son kayıt)
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

        // Modern list container
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

        // Unknown/corrupt → backup and return empty
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
