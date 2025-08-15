// Assets/Scripts/SceneManagers/SceneTimer.cs
// Limits a scene's lifetime to X minutes. When time is up, shows a compact
// IMGUI notice and redirects to the target scene after a short delay.
// Robust against Inspector-serialised 'showWindow' leftovers and ignores itself
// if already running in the destination scene.
// British English comments & UI text.

using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

[DisallowMultipleComponent]
public class SceneTimer : MonoBehaviour
{
    [Header("Timer")]
    [Tooltip("Maximum duration for this scene in minutes.")]
    [Min(0.1f)]
    public float maxDurationMinutes = 5f;

    [Tooltip("Delay (seconds) after showing the message before switching scenes.")]
    [Min(0f)]
    public float autoRedirectDelay = 2f;

    [Header("Destination")]
    [Tooltip("Scene to load after time is up (must be in Build Settings).")]
    public string redirectSceneName = "RestingLoby";

    [Tooltip("If true, the timer disables itself when the active scene already matches 'redirectSceneName'.")]
    public bool disableWhenAlreadyInTarget = true;

    [Header("UI Window")]
    [Tooltip("Shown automatically when time is up. Forced off while counting down.")]
    public bool showWindow = false;                 // will be forced false at start
    public Vector2 windowPosition = new Vector2(20, 20);
    public Vector2 windowSize = new Vector2(520, 150);
    public bool allowDrag = true;

    private float _deadlineUnscaled;
    private bool _expired = false;
    private bool _redirectScheduled = false;
    private float _redirectAtUnscaled;
    private Rect _windowRect;
    private Vector2 _scroll = Vector2.zero;
    private int _windowId;
    private bool _armed = false; // only arm when not already in target

    void OnValidate()
    {
        if (maxDurationMinutes < 0.1f) maxDurationMinutes = 0.1f;
        if (autoRedirectDelay < 0f) autoRedirectDelay = 0f;
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
    }

    void OnEnable()
    {
        _windowId = GetInstanceID();
        _windowRect = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        ResetAndArm();
    }

    private void ResetAndArm()
    {
        // If we are already in the destination scene, opt out (prevents unwanted UI).
        var activeName = SceneManager.GetActiveScene().name;
        if (disableWhenAlreadyInTarget &&
            !string.IsNullOrEmpty(redirectSceneName) &&
            string.Equals(activeName, redirectSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            _armed = false;
            _expired = false;
            _redirectScheduled = false;
            showWindow = false; // hard off
            enabled = false;    // do nothing in the target scene
            return;
        }

        // Fresh state every time this component enables.
        _armed = true;
        _expired = false;
        _redirectScheduled = false;
        showWindow = false; // Force off even if the scene had it serialised as true
        _deadlineUnscaled = Time.unscaledTime + maxDurationMinutes * 60f;
    }

    void Update()
    {
        if (!_armed) return;

        if (_expired)
        {
            if (_redirectScheduled && Time.unscaledTime >= _redirectAtUnscaled)
                DoRedirect();
            return;
        }

        // Count down using unscaled time so pausing Time.timeScale doesn't affect us.
        if (Time.unscaledTime >= _deadlineUnscaled)
        {
            _expired = true;
            showWindow = true;
            _redirectScheduled = true;
            _redirectAtUnscaled = Time.unscaledTime + autoRedirectDelay;
        }
    }

    void OnGUI()
    {
        if (!showWindow) return;
        _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "Scene Timer");
        windowPosition = new Vector2(_windowRect.x, _windowRect.y);
        windowSize     = new Vector2(_windowRect.width, _windowRect.height);
    }

    private void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label("Time is up, redirecting to Resting Loby");
        if (_redirectScheduled)
        {
            float secsLeft = Mathf.Max(0f, _redirectAtUnscaled - Time.unscaledTime);
            GUILayout.Space(4);
            GUILayout.Label($"Switching in {secsLeft:0.0} s…");
        }

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Go now", GUILayout.Width(120)))
        {
            DoRedirect();
        }
        GUILayout.EndHorizontal();

        // If the destination scene is not set or missing, inform clearly.
        if (!CanLoadSceneByName(redirectSceneName))
        {
            GUILayout.Space(6);
            GUILayout.Label($"<color=#FF8040>Scene '{redirectSceneName}' is not available in Build Settings.</color>");
        }

        GUILayout.EndScrollView();

        if (allowDrag)
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

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
            // Keep the window open so the user sees the warning.
            return;
        }

        try
        {
            SceneManager.LoadScene(redirectSceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SceneTimer] Failed to load scene '{redirectSceneName}': {ex.Message}");
        }
    }

    private bool CanLoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private string ListBuildScenesForDebug()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder("Scenes In Build: ");
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            if (i > 0) sb.Append(", ");
            sb.Append(Path.GetFileNameWithoutExtension(path));
        }
        return sb.ToString();
    }
}
