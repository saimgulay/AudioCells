// Assets/Scripts/SceneManagers/UserData.cs
// IMGUI username capture using the same unified JSON (entries + sessions).
// Now collects: (a) participant country (single free-text line) and
// (b) exposure to Western classical music (1..5 Likert).
// Writes both into the unified JSON as profile_flat (SPSS-friendly) and profile_text (human summary).
// British English comments & UI.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ExperimentData; // <-- shared models: ExtendedUserDataContainer, UserDataEntry, UserSession, Sample

public class UserData : MonoBehaviour
{
    [Header("Navigation")]
    public string nextSceneName = "Main";

    [Header("Behaviour & UI")]
    public bool showWindow = true;
    public Vector2 windowPosition = new Vector2(40, 120);
    public Vector2 windowSize = new Vector2(560, 340);
    public bool allowDrag = true;

    [Header("File Options")]
    public string dataFolderName = "Data";
    public string fileName = "UserData.json";

    private Rect _windowRect;
    private Vector2 _scroll = Vector2.zero;
    private int _windowId;

    private enum Step { EditUsername, Country, Exposure, Confirm, Info }
    private Step _step = Step.EditUsername;

    // Inputs
    private string _username = "";
    private string _country = "";
    private int _wcmExposure = 3; // 1..5 Likert, default midpoint

    // Messages
    private string _error = "";
    private string _info = "";

    // Likert labels for exposure
    private static readonly string[] Likert5 = { "1", "2", "3", "4", "5" };

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
        _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "Participant Details");
    }

    private void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        switch (_step)
        {
            case Step.EditUsername: DrawUsernameStep(); break;
            case Step.Country:      DrawCountryStep(); break;
            case Step.Exposure:     DrawExposureStep(); break;
            case Step.Confirm:      DrawConfirmStep(); break;
            case Step.Info:         DrawInfoStep(); break;
        }

        GUILayout.EndScrollView();

        if (allowDrag)
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    // ---- Step panes ----

    private void DrawUsernameStep()
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
                _step = Step.Country;
            }
            else _error = err;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (Event.current.type == EventType.Repaint)
            GUI.FocusControl("usernameField");
    }

    private void DrawCountryStep()
    {
        GUILayout.Label("Country Background");
        GUILayout.Space(4);
        GUILayout.Label("Which country best describes your background?");
        GUILayout.Label("<i>If more than one applies, please enter the single country that feels most relevant.</i>");
        GUILayout.Space(6);
        GUI.SetNextControlName("countryField");
        _country = GUILayout.TextField(_country ?? string.Empty, 64);

        if (!string.IsNullOrEmpty(_error))
            GUILayout.Label("<color=#FF5050>" + _error + "</color>");

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Back", GUILayout.Width(100)))
        {
            _error = string.Empty;
            _step = Step.EditUsername;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Continue", GUILayout.Width(120)))
        {
            // Country is optional, but lightly validate if provided
            if (!string.IsNullOrWhiteSpace(_country) && !ValidateCountry(_country, out string cerr))
            {
                _error = cerr;
            }
            else
            {
                _error = string.Empty;
                _step = Step.Exposure;
            }
        }
        GUILayout.EndHorizontal();

        if (Event.current.type == EventType.Repaint)
            GUI.FocusControl("countryField");
    }

    private void DrawExposureStep()
    {
        GUILayout.Label("Exposure to Western Classical Music");
        GUILayout.Space(4);
        GUILayout.Label("How much exposure have you had to Western classical music?");
        GUILayout.Space(2);
        GUILayout.Label("<i>We ask because aspects of this study draw—sometimes implicitly—on that musical tradition, and prior familiarity can subtly shape how sounds are perceived.</i>");
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        GUILayout.Label("None", GUILayout.Width(60));
        int idx = Mathf.Clamp(_wcmExposure - 1, 0, 4);
        idx = GUILayout.Toolbar(idx, Likert5, GUILayout.MinWidth(280), GUILayout.Height(28), GUILayout.ExpandWidth(true));
        _wcmExposure = idx + 1;
        GUILayout.Label("Extensive", GUILayout.Width(80));
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Back", GUILayout.Width(100))) _step = Step.Country;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Continue", GUILayout.Width(120))) _step = Step.Confirm;
        GUILayout.EndHorizontal();
    }

    private void DrawConfirmStep()
    {
        GUILayout.Label("Please confirm your details:");
        GUILayout.Space(6);
        GUILayout.Label($"Username: <b>{_username}</b>");
        GUILayout.Space(4);
        GUILayout.Label("<b>Country background</b>:");
        GUILayout.Label(string.IsNullOrWhiteSpace(_country) ? "<i>(not provided)</i>" : _country);
        GUILayout.Space(4);
        GUILayout.Label("<b>Exposure to Western classical music</b> (1–5): " + _wcmExposure);
        GUILayout.Space(10);
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
            _step = Step.Exposure;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawInfoStep()
    {
        if (!string.IsNullOrEmpty(_info)) GUILayout.Label(_info);
        else GUILayout.Label("Saved.");

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("OK", GUILayout.Width(80)))
            _step = Step.EditUsername;
        GUILayout.EndHorizontal();
    }

    // ---- flow ----
    private void OnSaveAndContinue()
    {
        _error = string.Empty;
        _info = string.Empty;

        if (!ValidateUsername(_username, out string err))
        {
            _error = err;
            _step = Step.EditUsername;
            return;
        }
        if (!string.IsNullOrWhiteSpace(_country) && !ValidateCountry(_country, out string cerr))
        {
            _error = cerr;
            _step = Step.Country;
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

            // Write profile samples (flat + text) into a session for this user & current scene
            WriteProfileSamples(ext, _username, _country, _wcmExposure);

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

    // ---- JSON write: profile samples ----
    private void WriteProfileSamples(ExtendedUserDataContainer ext, string username, string country, int wcmExposure)
    {
        if (ext.sessions == null) ext.sessions = new List<UserSession>();

        string scene = SceneManager.GetActiveScene().name;
        var session = FindOrCreateSession(ext.sessions, username, scene);

        var nowIso = DateTime.UtcNow.ToString("o");

        // Flat (SPSS-friendly) payload
        var flat = new ProfileFlat
        {
            username = username,
            scene = scene,
            capturedAtIso = nowIso,
            country = country ?? string.Empty,
            wcm_exposure_1to5 = Mathf.Clamp(wcmExposure, 1, 5)
        };

        // Human-readable text
        var sb = new StringBuilder()
            .AppendLine("PROFILE")
            .AppendLine($"User: {username}    Scene: {scene}")
            .AppendLine($"Time: {nowIso}")
            .AppendLine()
            .AppendLine("Country background:")
            .AppendLine(string.IsNullOrWhiteSpace(country) ? "(not provided)" : country)
            .AppendLine()
            .AppendLine("Exposure to Western classical music (1–5): " + Mathf.Clamp(wcmExposure, 1, 5));

        var flatSample = new Sample
        {
            tIso = nowIso,
            type = "profile_flat",
            label = JsonUtility.ToJson(flat),
            count = 2, // country + exposure
            score = Mathf.Clamp(wcmExposure, 1, 5) // optional quick glance
        };

        var textSample = new Sample
        {
            tIso = nowIso,
            type = "profile_text",
            label = sb.ToString(),
            count = 2
        };

        if (session.samples == null) session.samples = new List<Sample>();
        session.samples.Add(flatSample);
        session.samples.Add(textSample);

        TouchUpdated(ext, username);
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

    private bool ValidateCountry(string country, out string error)
    {
        // Allow letters, spaces, hyphens, apostrophes, and periods (e.g., Cote d'Ivoire variants, U.S.)
        foreach (char c in country)
        {
            if (!(char.IsLetter(c) || c == ' ' || c == '-' || c == '\'' || c == '.'))
            { error = "Country should contain only letters, spaces, hyphens, apostrophes or periods."; return false; }
        }
        if (country.Trim().Length < 2) { error = "Country seems too short."; return false; }
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
            if (ext != null)
            {
                if (ext.entries == null)  ext.entries  = new List<UserDataEntry>();
                if (ext.sessions == null) ext.sessions = new List<UserSession>();
                return ext;
            }
        }
        catch { }
        return new ExtendedUserDataContainer();
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

    // ---- minimal helpers mirroring SceneTimer patterns ----

    private static UserSession FindOrCreateSession(List<UserSession> list, string username, string scene)
    {
        if (list == null) list = new List<UserSession>();
        // Try to find the most recent matching session
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s != null &&
                string.Equals(s.username, username, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.scene, scene, StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }
        }
        // Create new
        var created = new UserSession
        {
            username = username,
            scene = scene,
            sessionId = Guid.NewGuid().ToString("N"),
            startedAtIso = DateTime.UtcNow.ToString("o"),
            samples = new List<Sample>()
        };
        list.Add(created);
        return created;
    }

    private static void TouchUpdated(ExtendedUserDataContainer ext, string username)
    {
        if (ext?.entries == null) return;
        var now = DateTime.UtcNow.ToString("o");
        for (int i = ext.entries.Count - 1; i >= 0; i--)
        {
            var e = ext.entries[i];
            if (e != null && string.Equals(e.username, username, StringComparison.OrdinalIgnoreCase))
            {
                e.updatedAtIso = now;
                return;
            }
        }
    }

    // ---- serialisable payload: flat profile for SPSS-friendly ingestion ----

    [Serializable]
    private class ProfileFlat
    {
        public string username;
        public string scene;
        public string capturedAtIso;

        public string country;         // free text (may be empty)
        public int    wcm_exposure_1to5; // 1..5
    }
}
