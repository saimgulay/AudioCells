// Assets/Scripts/PythonStreamerRunner.cs
// Launch/stop Python streamer from inside Unity.
// L = start/stop, K = show/hide window, Esc = Stop EEG recording (no scene change).
// Sends UDP {"cmd":"shutdown"} to Python for graceful release before Kill().
// Movable window + scroll, robust path resolution.
// British English comments and logs.

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using UnityEngine;
using Diag = System.Diagnostics;

public class PythonStreamerRunner : MonoBehaviour
{
    [Header("Python script")]
    [Tooltip("Path to brainbit_streamer.py. Leave empty to auto-resolve to Assets/StreamingAssets/brainbit_streamer.py")]
    public string pythonScriptPath = "";

    [Header("Conda (macOS/Linux recommended)")]
    public string condaShPath = "/opt/anaconda3/etc/profile.d/conda.sh";
    public string condaEnvName = "td-conda";
    public bool useConda = true;

    [Header("Script arguments")]
    [TextArea] public string extraArgs = "--timeout 45 --metric-port 7788";
    public string serialNumber = "";

    [Header("Control (graceful shutdown)")]
    public string controlHost = "127.0.0.1";
    public int controlPort = 7790; // must match Python --control-port

    [Header("Behaviour & UI")]
    public bool autoStart = false;
    public bool logPythonOutput = true;
    public bool showWindow = true;                // K toggles this
    public Vector2 windowPosition = new Vector2(10, 200);
    public Vector2 windowSize = new Vector2(640, 360);
    public float logHeight = 160f;

    private Diag.Process _proc;
    private StringBuilder _lastLines = new StringBuilder(4096);
    private readonly object _lock = new object();

    private string _resolvedPath = "";
    private Rect _windowRect;
    private Vector2 _logScroll = Vector2.zero;

    void Awake()
    {
        _resolvedPath = ResolveScriptPath(pythonScriptPath);
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
    }

    void OnValidate()
    {
        _resolvedPath = ResolveScriptPath(pythonScriptPath);
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
    }

    void Start()
    {
        if (autoStart) StartPython();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (IsRunning()) StopPython();
            else StartPython();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            showWindow = !showWindow;
        }
        // Esc now ONLY stops EEG recording; it does NOT change scene.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopEegRecordingOnly();
        }
    }

    public bool IsRunning() => _proc != null && !_proc.HasExited;

    [ContextMenu("Start Python")]
    public void StartPython()
    {
        if (IsRunning())
        {
            UnityEngine.Debug.LogWarning("[PythonRunner] Already running.");
            return;
        }

        _resolvedPath = ResolveScriptPath(pythonScriptPath);

        if (!File.Exists(_resolvedPath))
        {
            UnityEngine.Debug.LogError($"[PythonRunner] Python script not found.\nEntered: {pythonScriptPath}\nResolved: {_resolvedPath}");
            return;
        }

        try
        {
            string args = BuildArgs();
            Diag.ProcessStartInfo psi;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            if (useConda)
            {
                string safeScript = "'" + _resolvedPath.Replace("'", "'\"'\"'") + "'";
                string cmd = $"source \"{condaShPath}\" && conda activate {condaEnvName} && python {safeScript} {args}";
                psi = new Diag.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-lc \"" + cmd.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_resolvedPath) ?? Environment.CurrentDirectory
                };
            }
            else
            {
                psi = new Diag.ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"\"{_resolvedPath}\" {args}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_resolvedPath) ?? Environment.CurrentDirectory
                };
            }
#else
            if (useConda)
            {
                string safeScript = "\"" + _resolvedPath + "\"";
                string cmd = $"conda activate {condaEnvName} && python {safeScript} {args}";
                psi = new Diag.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + cmd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_resolvedPath) ?? Environment.CurrentDirectory
                };
            }
            else
            {
                psi = new Diag.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{_resolvedPath}\" {args}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_resolvedPath) ?? Environment.CurrentDirectory
                };
            }
#endif

            _proc = new Diag.Process();
            _proc.StartInfo = psi;
            _proc.EnableRaisingEvents = true;
            _proc.OutputDataReceived += (s, e) => { if (e.Data != null) HandleLine(e.Data, false); };
            _proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) HandleLine(e.Data, true); };
            _proc.Exited += (s, e) => UnityEngine.Debug.Log("[PythonRunner] Process exited.");

            bool ok = _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            if (ok) UnityEngine.Debug.Log("[PythonRunner] Started.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[PythonRunner] Failed to start: " + ex.Message);
        }
    }

    [ContextMenu("Stop Python")]
    public void StopPython()
    {
        try
        {
            if (_proc != null && !_proc.HasExited)
            {
                try
                {
                    using (var udp = new UdpClient())
                    {
                        udp.Connect(controlHost, controlPort);
                        var payload = Encoding.UTF8.GetBytes("{\"cmd\":\"shutdown\"}");
                        udp.Send(payload, payload.Length);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[PythonRunner] Failed to send shutdown UDP: " + ex.Message);
                }

                const int totalWaitMs = 4000;
                int waited = 0;
                while (!_proc.HasExited && waited < totalWaitMs)
                {
                    System.Threading.Thread.Sleep(100);
                    waited += 100;
                }

                if (!_proc.HasExited)
                {
                    _proc.Kill();
                    _proc.WaitForExit(2000);
                }
            }
        }
        catch (Exception) { }
        finally
        {
            try { _proc?.Dispose(); } catch { }
            _proc = null;
            UnityEngine.Debug.Log("[PythonRunner] Stopped.");
        }
    }

    private string BuildArgs()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(extraArgs)) sb.Append(extraArgs.Trim());
        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("--serial ").Append(serialNumber);
        }
        if (sb.Length > 0) sb.Append(' ');
        sb.Append("--control-host ").Append(controlHost).Append(' ')
          .Append("--control-port ").Append(controlPort);
        return sb.ToString();
    }

    private void HandleLine(string line, bool isErr)
    {
        if (logPythonOutput)
        {
            if (isErr) UnityEngine.Debug.LogError("[py] " + line);
            else UnityEngine.Debug.Log("[py] " + line);
        }
        lock (_lock)
        {
            _lastLines.AppendLine(line);
            if (_lastLines.Length > 12000)
                _lastLines.Remove(0, _lastLines.Length - 9000);
        }
    }

    void OnGUI()
    {
        if (!showWindow) return;
        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Python Streamer Runner");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label("L = start/stop   |   K = show/hide   |   Esc = Stop EEG recording");
        GUILayout.Label("Entered path: " + (string.IsNullOrEmpty(pythonScriptPath) ? "(empty → auto)" : pythonScriptPath));
        GUILayout.Label("Resolved: " + _resolvedPath);
        GUILayout.Label($"Conda: {(useConda ? $"{condaEnvName} via {condaShPath}" : "Disabled (system python)")}");
        GUILayout.Label($"Control UDP: {controlHost}:{controlPort}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(IsRunning() ? "Stop" : "Start", GUILayout.Width(120)))
        {
            if (IsRunning()) StopPython(); else StartPython();
        }
#if UNITY_EDITOR
        if (GUILayout.Button("Browse…", GUILayout.Width(120)))
        {
            string file = UnityEditor.EditorUtility.OpenFilePanel("Select Python script", Application.dataPath, "py");
            if (!string.IsNullOrEmpty(file))
            {
                pythonScriptPath = file;
                _resolvedPath = ResolveScriptPath(pythonScriptPath);
            }
        }
#endif
        if (GUILayout.Button("Reveal Script", GUILayout.Width(120)))
        {
            var folder = Path.GetDirectoryName(_resolvedPath) ?? Application.dataPath;
            try
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                Diag.Process.Start("/usr/bin/open", folder);
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                Diag.Process.Start(new Diag.ProcessStartInfo("explorer.exe", folder.Replace('/', '\\')) { UseShellExecute = true });
#else
                Diag.Process.Start("xdg-open", folder);
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[PythonRunner] Reveal failed: " + ex.Message);
            }
        }

        if (GUILayout.Button("Stop Recording (Esc)", GUILayout.Width(160)))
        {
            StopEegRecordingOnly();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("<b>Last output</b>");
        string text; lock (_lock) text = _lastLines.ToString();
        _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(logHeight));
        GUILayout.TextArea(text);
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    void OnDestroy() => StopPython();

    // ---- Stop EEG recording ONLY (Esc) --------------------------------------

    [ContextMenu("Stop EEG Recording Only")]
    public void StopEegRecordingOnly()
    {
        try
        {
            var receivers = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in receivers)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name != "EEGMetricsUdpReceiver") continue;

                bool stopped = false;

                // 1) recording field (bool)
                var recField = t.GetField("recording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (recField != null && recField.FieldType == typeof(bool))
                {
                    try { recField.SetValue(mb, false); stopped = true; } catch { }
                }

                // 2) recording property (bool)
                if (!stopped)
                {
                    var recProp = t.GetProperty("recording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (recProp != null && recProp.CanWrite && recProp.PropertyType == typeof(bool))
                    {
                        try { recProp.SetValue(mb, false); stopped = true; } catch { }
                    }
                }

                // 3) SetRecording(false) / StopRecording() fallbacks
                if (!stopped)
                {
                    var setRec = t.GetMethod("SetRecording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
                    if (setRec != null)
                    {
                        try { setRec.Invoke(mb, new object[] { false }); stopped = true; } catch { }
                    }
                }
                if (!stopped)
                {
                    var stopRec = t.GetMethod("StopRecording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (stopRec != null)
                    {
                        try { stopRec.Invoke(mb, null); stopped = true; } catch { }
                    }
                }

                if (stopped)
                    Debug.Log("[PythonRunner] EEG recording stopped (Esc).");
                else
                    Debug.LogWarning("[PythonRunner] Could not stop EEG recording: no compatible API found.");

                break; // assume single receiver
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PythonRunner] EEG stop attempt failed: " + ex.Message);
        }
    }

    // ---- Helpers -------------------------------------------------------------

    private string ResolveScriptPath(string input)
    {
        const string defaultFile = "brainbit_streamer.py";

        if (string.IsNullOrWhiteSpace(input))
            return Path.Combine(Application.dataPath, "StreamingAssets", defaultFile);

        string trimmed = input.Trim();

        if (trimmed.StartsWith("Assets"))
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string rel = trimmed.Substring("Assets".Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string abs = Path.Combine(projectRoot ?? "", "Assets", rel);
            if (Directory.Exists(abs))
                return Path.Combine(abs, defaultFile);
            return abs;
        }

        if (Directory.Exists(trimmed))
            return Path.Combine(trimmed, defaultFile);

        if (Path.IsPathRooted(trimmed))
            return trimmed;

        string projRoot = Path.GetDirectoryName(Application.dataPath) ?? "";
        string candidate = Path.Combine(projRoot, trimmed);
        if (Directory.Exists(candidate))
            return Path.Combine(candidate, defaultFile);
        return candidate;
    }
}
