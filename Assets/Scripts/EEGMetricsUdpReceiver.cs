// Assets/Scripts/EEGMetricsUdpReceiver.cs
// Receives EEG JSON over UDP and (optionally) records samples into a user session JSON.
// IMGUI window (movable). K/J toggles overlay. R starts/stops recording.
// British English comments & UI text.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class EegMetricPacket
{
    public string type;
    public double t;
    public int score;
    public double z, raw, alpha, beta, theta, gamma;
    public string label;
    public int count, target;
    public double elapsed;
    public double mu_raw, sigma_raw, mu_gamma, sigma_gamma, mu_total, sigma_total;
}

public class EEGMetricsUdpReceiver : MonoBehaviour
{
    [Header("UDP")]
    public string listenAddress = "127.0.0.1";
    public int listenPort = 7788;

    [Header("UI Window")]
    public bool showOverlay = true;              // K or J toggles this
    public Vector2 windowPosition = new Vector2(10, 10);
    public Vector2 windowSize = new Vector2(560, 280);
    public bool enableScroll = false;
    public float scrollHeight = 140f;

    [Header("Recording")]
    [Tooltip("Press R (or click the button) to start/stop recording to JSON.")]
    public bool recording = false;
    public KeyCode recordingToggleKey = KeyCode.R;

    [Header("User & Files")]
    [Tooltip("If set, overrides the discovered username.")]
    public string overrideUsername = "";
    [Tooltip("Folder name under Assets/ (Editor) or persistentDataPath (Build).")]
    public string dataFolderName = "Data";
    [Tooltip("Main user file (entries).")]
    public string userFileName = "UserData.json";
    [Tooltip("Session file to store EEG samples when using separate file mode.")]
    public string sessionFileName = "UserData_Sessions.json";
    [Tooltip("If true, write sessions to a separate file to avoid clobbering by other scripts.")]
    public bool saveSessionsInSeparateFile = true;
    [Tooltip("How often to flush buffered samples to disk (seconds) whilst recording.")]
    public float flushIntervalSeconds = 1.0f;

    private UdpClient _udp;
    private Thread _thread;
    private volatile bool _running;

    private readonly object _lock = new object();
    private EegMetricPacket _latest;
    private string _status = "Waiting for metrics…";

    private Rect _windowRect;
    private Vector2 _uiScroll = Vector2.zero;

    // session/runtime
    private string _username = "";
    private string _sceneName = "";
    private string _sessionId = "";
    private string _targetFilePath = "";
    private float _flushTimer = 0f;

    private readonly List<Sample> _pending = new List<Sample>(); // guarded by _lock

    // ---------- JSON models (sessions) ----------
    [Serializable] private class SessionsFile { public List<UserSession> sessions = new List<UserSession>(); }
    [Serializable] private class UserSession { public string username; public string scene; public string sessionId; public string startedAtIso; public List<Sample> samples = new List<Sample>(); }
    [Serializable] private class Sample
    {
        public string tIso;  // ISO-8601 (UTC)
        public string type;
        public int score;
        public double z, raw, alpha, beta, theta, gamma;
        public string label;
        public int count, target;
        public double elapsed;
        public double mu_raw, sigma_raw, mu_gamma, sigma_gamma, mu_total, sigma_total;
    }

    // ---------- JSON models (users) ----------
    [Serializable] private class UserDataEntry { public string username; public string createdAtIso; public string updatedAtIso; public int version = 1; }
    [Serializable] private class UserDataContainer { public List<UserDataEntry> entries = new List<UserDataEntry>(); }
    [Serializable] private class LegacyUserDataModel { public string username; public string createdAtIso; public string updatedAtIso; public int version; }

    // For writing sessions inside the same UserData.json (optional mode)
    [Serializable] private class ExtendedUserDataContainer : UserDataContainer { public List<UserSession> sessions = new List<UserSession>(); }

    void Awake()
    {
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        _sceneName = SceneManager.GetActiveScene().name;
        _username = ResolveUsername();
        _sessionId = Guid.NewGuid().ToString("N");
        EnsureDataFolderExists();
        _targetFilePath = saveSessionsInSeparateFile ? GetSessionFilePath() : GetUserFilePath();

        if (string.IsNullOrEmpty(_username))
            _status = "No username found — logging disabled until a username is available.";
        else
            _status = "Ready. Press R to start recording.";
    }

    void Start()
    {
        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Parse(listenAddress), listenPort));
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "EEGMetricsUdpReceiver" };
            _thread.Start();
            Debug.Log($"[EEGMetricsUDP] Listening on {listenAddress}:{listenPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EEGMetricsUDP] Failed to bind UDP: {ex.Message}");
        }
    }

    void Update()
    {
        // Toggle overlay with K or J
        if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.J))
            showOverlay = !showOverlay;

        // Start/Stop recording with R
        if (Input.GetKeyDown(recordingToggleKey))
            ToggleRecording();

        // Periodic flush only whilst recording
        if (recording && !string.IsNullOrEmpty(_username) && _running)
        {
            _flushTimer += Time.deltaTime;
            if (_flushTimer >= flushIntervalSeconds)
            {
                _flushTimer = 0f;
                FlushPendingToDisk();
            }
        }
    }

    void ReceiveLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);
                var pkt = JsonUtility.FromJson<EegMetricPacket>(json);
                if (pkt != null && !string.IsNullOrEmpty(pkt.type))
                {
                    lock (_lock)
                    {
                        _latest = pkt;
                        if (recording && !string.IsNullOrEmpty(_username))
                            _pending.Add(ToSample(pkt));
                    }
                    if (pkt.type == "concentration")
                        _status = recording ? "Recording…" : "OK (not recording)";
                    else if (pkt.type == "baseline_ready")
                        _status = recording ? "Recording… (baseline ready)" : "Baseline ready";
                    else if (pkt.type == "baseline_progress")
                        _status = recording ? $"Recording…  Baseline {pkt.count}/{pkt.target}" : $"Baseline {pkt.count}/{pkt.target}";
                    else if (pkt.type == "veto")
                        _status = recording ? "Recording… (artefact veto)" : "Artefact veto";
                }
            }
            catch (SocketException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EEGMetricsUDP] Receive error: {ex.Message}");
            }
        }
    }

    public EegMetricPacket GetLatest() { lock (_lock) return _latest; }

    void OnGUI()
    {
        if (!showOverlay) return;
        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "EEG Metrics (UDP JSON)  —  K/J: show/hide, R: rec start/stop");
        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize = new Vector2(_windowRect.width, _windowRect.height);
    }

    private void DrawWindow(int id)
    {
        var pkt = GetLatest();

        if (enableScroll)
            _uiScroll = GUILayout.BeginScrollView(_uiScroll, GUILayout.Height(scrollHeight));

        GUILayout.Label($"User: {(_username ?? "<none>")}");
        GUILayout.Label($"Scene: {_sceneName}");
        GUILayout.Label($"Session: {_sessionId}");
        GUILayout.Label($"File: {_targetFilePath}");
        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Recording: {(recording ? "ON" : "OFF")}");
        GUILayout.FlexibleSpace();
        GUI.enabled = !string.IsNullOrEmpty(_username);
        if (GUILayout.Button(recording ? "Stop Recording" : "Start Recording", GUILayout.Width(160)))
            ToggleRecording();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Label($"Status: {_status}");
        GUILayout.Space(4);

        if (pkt != null)
        {
            if (pkt.type == "concentration")
            {
                GUILayout.Label($"Score: {pkt.score:0}  |  z: {pkt.z:+0.00}  ({pkt.label})");
                GUILayout.Label($"Bands  α:{pkt.alpha:0.###}  β:{pkt.beta:0.###}  θ:{pkt.theta:0.###}  γ:{pkt.gamma:0.###}");
                GUILayout.Label($"Raw ratio β/(α+θ): {pkt.raw:0.###}");
            }
            else if (pkt.type == "baseline_progress")
            {
                GUILayout.Label($"Baseline: {pkt.count}/{pkt.target}  elapsed: {pkt.elapsed:0}s");
            }
            else if (pkt.type == "baseline_ready")
            {
                GUILayout.Label($"Baseline ready  μ_raw={pkt.mu_raw:0.###}  σ_raw={pkt.sigma_raw:0.###}");
            }
            else if (pkt.type == "veto")
            {
                GUILayout.Label("Last window discarded (artefact).");
            }
        }
        else
        {
            GUILayout.Label("Awaiting first packet…");
        }

        if (enableScroll)
            GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    void OnDestroy()
    {
        _running = false;
        try { _udp?.Close(); } catch { }
        try { _thread?.Join(200); } catch { }
        // Final flush of any recorded-but-not-yet-written samples
        FlushPendingToDisk();
    }

    void OnApplicationQuit()
    {
        // Ensure pending data is persisted on quit
        FlushPendingToDisk();
    }

    // ---------- recording control ----------

    private void ToggleRecording()
    {
        if (string.IsNullOrEmpty(_username))
        {
            _status = "Cannot record — no username.";
            recording = false;
            return;
        }

        if (!recording)
        {
            // Start a new session ID for each recording run
            _sessionId = Guid.NewGuid().ToString("N");
            _status = "Recording…";
            _flushTimer = 0f;
            recording = true;
        }
        else
        {
            // Stop: flush what we have and stop appending
            recording = false;
            FlushPendingToDisk();
            _status = "Stopped. Not recording.";
        }
    }

    // ---------- helpers ----------

    private Sample ToSample(EegMetricPacket p)
    {
        return new Sample
        {
            tIso = DateTime.UtcNow.ToString("o"),
            type = p.type,
            score = p.score,
            z = p.z, raw = p.raw, alpha = p.alpha, beta = p.beta, theta = p.theta, gamma = p.gamma,
            label = p.label,
            count = p.count, target = p.target, elapsed = p.elapsed,
            mu_raw = p.mu_raw, sigma_raw = p.sigma_raw, mu_gamma = p.mu_gamma, sigma_gamma = p.sigma_gamma, mu_total = p.mu_total, sigma_total = p.sigma_total
        };
    }

    private void FlushPendingToDisk()
    {
        List<Sample> dump;
        lock (_lock)
        {
            if (_pending.Count == 0) return;
            dump = new List<Sample>(_pending);
            _pending.Clear();
        }

        try
        {
            EnsureDataFolderExists();

            if (saveSessionsInSeparateFile)
                WriteSessionsSeparate(dump);
            else
                WriteSessionsEmbedded(dump);

            // Also bump user's updatedAtIso in the main user file
            TouchUserUpdatedAt(_username);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EEGMetricsUDP] Failed to write JSON: {ex.Message}");
        }
    }

    // -------- separate file mode --------
    private void WriteSessionsSeparate(List<Sample> newSamples)
    {
        string path = GetSessionFilePath();
        SessionsFile file = File.Exists(path)
            ? JsonUtility.FromJson<SessionsFile>(File.ReadAllText(path))
            : new SessionsFile();
        if (file.sessions == null) file.sessions = new List<UserSession>();

        var sess = FindOrCreateSession(file.sessions);
        if (sess.samples == null) sess.samples = new List<Sample>();
        sess.samples.AddRange(newSamples);

        string json = JsonUtility.ToJson(file, true);
        File.WriteAllText(path, json);
    }

    // -------- embedded in UserData.json (optional) --------
    private void WriteSessionsEmbedded(List<Sample> newSamples)
    {
        string path = GetUserFilePath();
        ExtendedUserDataContainer ext;
        if (File.Exists(path))
        {
            // Try to read as extended first
            try
            {
                ext = JsonUtility.FromJson<ExtendedUserDataContainer>(File.ReadAllText(path));
                if (ext.entries == null) ext.entries = new List<UserDataEntry>();
                if (ext.sessions == null) ext.sessions = new List<UserSession>();
            }
            catch
            {
                // Fallback: migrate from plain container
                var plain = JsonUtility.FromJson<UserDataContainer>(File.ReadAllText(path)) ?? new UserDataContainer();
                ext = new ExtendedUserDataContainer { entries = plain.entries ?? new List<UserDataEntry>(), sessions = new List<UserSession>() };
            }
        }
        else
        {
            ext = new ExtendedUserDataContainer { entries = new List<UserDataEntry>(), sessions = new List<UserSession>() };
        }

        var sess = FindOrCreateSession(ext.sessions);
        if (sess.samples == null) sess.samples = new List<Sample>();
        sess.samples.AddRange(newSamples);

        string json = JsonUtility.ToJson(ext, true);
        File.WriteAllText(path, json);
    }

    private UserSession FindOrCreateSession(List<UserSession> list)
    {
        // Reuse same session if we find exact (user, scene, sessionId)
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s.username == _username && s.scene == _sceneName && s.sessionId == _sessionId)
                return s;
        }
        var created = new UserSession
        {
            username = _username,
            scene = _sceneName,
            sessionId = _sessionId,
            startedAtIso = DateTime.UtcNow.ToString("o"),
            samples = new List<Sample>()
        };
        list.Add(created);
        return created;
    }

    // -------- user file maintenance --------
    private void TouchUserUpdatedAt(string username)
    {
        if (string.IsNullOrEmpty(username)) return;
        string path = GetUserFilePath();
        UserDataContainer container = LoadUserContainerWithMigration();
        if (container.entries == null) container.entries = new List<UserDataEntry>();

        // Find existing (case-insensitive)
        UserDataEntry existing = null;
        for (int i = container.entries.Count - 1; i >= 0; i--)
        {
            var e = container.entries[i];
            if (e != null && !string.IsNullOrEmpty(e.username) &&
                string.Equals(e.username, username, StringComparison.OrdinalIgnoreCase))
            {
                existing = e; break;
            }
        }

        var now = DateTime.UtcNow.ToString("o");
        if (existing != null)
        {
            existing.updatedAtIso = now;
        }
        else
        {
            container.entries.Add(new UserDataEntry { username = username, createdAtIso = now, updatedAtIso = now, version = 1 });
        }

        string json = JsonUtility.ToJson(container, true);
        File.WriteAllText(path, json);
    }

    private UserDataContainer LoadUserContainerWithMigration()
    {
        string path = GetUserFilePath();
        if (!File.Exists(path)) return new UserDataContainer();

        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new UserDataContainer();

        try
        {
            var c = JsonUtility.FromJson<UserDataContainer>(text);
            if (c != null && c.entries != null) return c;
        }
        catch { }

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

    // -------- username & paths --------
    private string ResolveUsername()
    {
        // 1) explicit override
        if (!string.IsNullOrWhiteSpace(overrideUsername))
            return overrideUsername.Trim();

        // 2) PlayerPrefs from previous scene (optional)
        try
        {
            if (PlayerPrefs.HasKey("LastUsername"))
            {
                var pp = PlayerPrefs.GetString("LastUsername");
                if (!string.IsNullOrWhiteSpace(pp)) return pp.Trim();
            }
        }
        catch { }

        // 3) last entry in UserData.json
        try
        {
            var container = LoadUserContainerWithMigration();
            if (container.entries != null && container.entries.Count > 0)
            {
                var last = container.entries[container.entries.Count - 1];
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

    private string GetDataFolderPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, dataFolderName);
#else
        return Path.Combine(Application.persistentDataPath, dataFolderName);
#endif
    }
    private string GetUserFilePath()    => Path.Combine(GetDataFolderPath(), userFileName);
    private string GetSessionFilePath() => Path.Combine(GetDataFolderPath(), sessionFileName);
}
