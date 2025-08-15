// Assets/Scripts/EEGMetricsUdpReceiver.cs
// Receives EEG JSON over UDP and records samples into the *same* UserData.json.
// Uses shared models (UserJsonModels.cs) so sessions never get wiped.
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
using ExperimentData; // <-- shared models

public class EEGMetricsUdpReceiver : MonoBehaviour
{
    [Header("UDP")]
    public string listenAddress = "127.0.0.1";
    public int listenPort = 7788;

    [Header("UI Window")]
    public bool showOverlay = true;              // K or J toggles this
    public Vector2 windowPosition = new Vector2(10, 10);
    public Vector2 windowSize = new Vector2(560, 300);
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
    [Tooltip("Unified user + sessions JSON file name.")]
    public string userFileName = "UserData.json";

    // ---- runtime ----
    private UdpClient _udp;
    private Thread _thread;
    private volatile bool _running;

    private readonly object _lock = new object();
    private EegMetricPacket _latest;
    private string _status = "Waiting for metrics…";

    private Rect _windowRect;
    private Vector2 _uiScroll = Vector2.zero;

    private string _username = "";
    private string _sceneName = "";
    private string _sessionId = "";
    private float _flushTimer = 0f;

    private readonly List<Sample> _pending = new List<Sample>(); // guarded by _lock

    [Serializable] public class EegMetricPacket
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

    void Awake()
    {
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        _sceneName  = SceneManager.GetActiveScene().name;
        _username   = ResolveUsername();
        _sessionId  = Guid.NewGuid().ToString("N");

        EnsureDataFolderExists();
        EnsureUnifiedFileExists();

        _status = string.IsNullOrEmpty(_username)
            ? "No username found — press R disabled."
            : "Ready. Press R to start recording.";
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
        if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.J))
            showOverlay = !showOverlay;

        if (Input.GetKeyDown(recordingToggleKey))
            ToggleRecording();

        if (recording && !string.IsNullOrEmpty(_username) && _running)
        {
            _flushTimer += Time.deltaTime;
            if (_flushTimer >= 1.0f)
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
                    _status = pkt.type switch
                    {
                        "concentration"     => recording ? "Recording…" : "OK (not recording)",
                        "baseline_ready"    => recording ? "Recording… (baseline ready)" : "Baseline ready",
                        "baseline_progress" => recording ? $"Recording…  Baseline {pkt.count}/{pkt.target}" : $"Baseline {pkt.count}/{pkt.target}",
                        "veto"              => recording ? "Recording… (artefact veto)" : "Artefact veto",
                        _                   => _status
                    };
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
    }

    private void DrawWindow(int id)
    {
        var pkt = GetLatest();

        if (enableScroll)
            _uiScroll = GUILayout.BeginScrollView(_uiScroll, GUILayout.Height(scrollHeight));

        GUILayout.Label($"User: {(_username ?? "<none>")}");
        GUILayout.Label($"Scene: {_sceneName}");
        GUILayout.Label($"Session: {_sessionId}");
        GUILayout.Label($"File: {GetUserFilePath()}");
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
        FlushPendingToDisk();
    }

    void OnApplicationQuit() => FlushPendingToDisk();

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
            _sessionId = Guid.NewGuid().ToString("N");
            _status = "Recording…";
            _flushTimer = 0f;
            recording = true;
        }
        else
        {
            recording = false;
            FlushPendingToDisk();
            _status = "Stopped. Not recording.";
        }
    }

    // Public for PythonStreamerRunner (End Scene)
    public void FlushNow() => FlushPendingToDisk();

    // ---------- helpers: JSON ----------

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
            // Read-modify-write with merge; never drop other sessions
            var path = GetUserFilePath();
            var ext  = LoadExtendedWithMigration(path);

            EnsureUserEntry(ext, _username);
            var sess = FindOrCreateSession(ext.sessions, _username, _sceneName, _sessionId);
            if (sess.samples == null) sess.samples = new List<Sample>();
            sess.samples.AddRange(dump);
            TouchUpdated(ext, _username);

            WriteJsonAtomic(path, JsonUtility.ToJson(ext, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EEGMetricsUDP] Failed to write JSON: {ex.Message}");
        }
    }

    private static ExtendedUserDataContainer LoadExtendedWithMigration(string path)
    {
        if (!File.Exists(path)) return new ExtendedUserDataContainer();

        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new ExtendedUserDataContainer();

        // Extended?
        try
        {
            var ext = JsonUtility.FromJson<ExtendedUserDataContainer>(text);
            if (ext != null && ext.entries != null && ext.sessions != null) return ext;
        }
        catch { }

        // Legacy single-entry formats could be migrated here if needed…
        return new ExtendedUserDataContainer();
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

    private static UserSession FindOrCreateSession(List<UserSession> list, string user, string scene, string sid)
    {
        if (list == null) list = new List<UserSession>();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s.username == user && s.scene == scene && s.sessionId == sid) return s;
        }
        var created = new UserSession
        {
            username = user,
            scene = scene,
            sessionId = sid,
            startedAtIso = DateTime.UtcNow.ToString("o"),
            samples = new List<Sample>()
        };
        list.Add(created);
        return created;
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

    // Safe write: temp file + replace; refresh Project in Editor.
    private static void WriteJsonAtomic(string path, string json)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, new UTF8Encoding(false));
#if UNITY_EDITOR
        // Try best-effort atomic move
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

        // Fallback: last entry in unified file
        try
        {
            var ext = LoadExtendedWithMigration(GetUserFilePath());
            if (ext.entries != null && ext.entries.Count > 0)
            {
                var last = ext.entries[ext.entries.Count - 1];
                if (last != null && !string.IsNullOrWhiteSpace(last.username))
                    return last.username.Trim();
            }
        }
        catch { }

        return "";
    }

    private string GetDataFolderPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, dataFolderName);
#else
        return Path.Combine(Application.persistentDataPath, dataFolderName);
#endif
    }
    private string GetUserFilePath() => Path.Combine(GetDataFolderPath(), userFileName);
}
