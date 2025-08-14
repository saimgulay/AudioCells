using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to any GameObject in your end scene.
/// Wire your UI Button's OnClick to OnReturnToMenuButton().
/// Before loading the "Menu" scene, this will:
/// 1) If an EEGMetricsUdpReceiver exists, stop recording + flush pending JSON to disk (via reflection).
/// 2) If a PythonStreamerRunner exists, stop the Python process gracefully without freezing the main thread.
/// Uses British English logs; avoids scene switch if target scene is missing in Build Settings.
/// </summary>
public class EndScene : MonoBehaviour
{
    [Header("Navigation")]
    [Tooltip("Scene name to load for the menu.")]
    public string menuSceneName = "Menu";

    [Header("Pre-exit actions")]
    [Tooltip("If true, tries to stop PythonStreamerRunner before leaving the scene.")]
    public bool stopPythonOnExit = true;

    [Tooltip("If true, tries to flush EEGMetricsUdpReceiver pending samples before leaving the scene.")]
    public bool flushEegOnExit = true;

    [Tooltip("Max seconds to wait for all pre-exit work before giving up.")]
    public float maxTotalWaitSeconds = 6f;

    [Header("Optional UI")]
    [Tooltip("Optional: will be set interactable=false whilst busy.")]
    public Button returnButton;

    private bool _busy;

    /// <summary>
    /// Hook this to your UI Button.
    /// </summary>
    public void OnReturnToMenuButton()
    {
        if (_busy) return;
        StartCoroutine(ReturnFlow());
    }

    private System.Collections.IEnumerator ReturnFlow()
    {
        _busy = true;
        if (returnButton != null) returnButton.interactable = false;

        float start = Time.realtimeSinceStartup;

        // 1) Flush EEG (JSON) if present
        if (flushEegOnExit)
        {
            try { yield return StartCoroutine(FlushEegIfAny()); }
            catch (Exception ex) { Debug.LogWarning("[EndScene] EEG flush failed: " + ex.Message); }
        }

        // 2) Stop Python if present (non-blocking on main thread)
        if (stopPythonOnExit)
        {
            bool done = false;
            Exception stopEx = null;

            var runner = FindObjectOfType<PythonStreamerRunner>(includeInactive: true);
            if (runner != null)
            {
                Task.Run(() =>
                {
                    try { runner.StopPython(); } catch (Exception ex) { stopEx = ex; }
                    finally { done = true; }
                });

                while (!done && (Time.realtimeSinceStartup - start) < maxTotalWaitSeconds)
                    yield return null;

                if (stopEx != null)
                    Debug.LogWarning("[EndScene] Python stop reported: " + stopEx.Message);
            }
        }

        // 3) Time-out guard
        if ((Time.realtimeSinceStartup - start) >= maxTotalWaitSeconds)
            Debug.LogWarning("[EndScene] Pre-exit work reached timeout; continuing to menu.");

        // 4) Load Menu scene (only if present in Build Settings)
        if (!SceneExistsInBuild(menuSceneName))
        {
            Debug.LogError($"[EndScene] Scene '{menuSceneName}' is not in Build Settings. " +
                           "Open File ▶ Build Settings… and add it under 'Scenes In Build'.");
        }
        else
        {
            try { SceneManager.LoadScene(menuSceneName); }
            catch (Exception ex)
            {
                Debug.LogError("[EndScene] Failed to load menu scene: " + ex.Message);
            }
        }

        _busy = false;
        if (returnButton != null) returnButton.interactable = true;
    }

    private System.Collections.IEnumerator FlushEegIfAny()
    {
        // Try to find EEGMetricsUdpReceiver (your UDP JSON writer)
        var eeg = FindObjectOfType<MonoBehaviour>(includeInactive: true);
        EEGMetricsUdpReceiver receiver = null;

        // Faster/cleaner direct find if the type is available:
        receiver = FindObjectOfType<EEGMetricsUdpReceiver>(includeInactive: true);
        if (receiver == null) yield break;

        // Try to turn off recording if such a flag exists
        try
        {
            var t = receiver.GetType();
            // public or private bool recordingEnabled / _recordingEnabled
            var f = t.GetField("recordingEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 ?? t.GetField("_recordingEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool))
                f.SetValue(receiver, false);

            var mStop = t.GetMethod("StopRecording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mStop != null) mStop.Invoke(receiver, null);
        }
        catch (Exception ex) { Debug.Log("[EndScene] EEG stop-recording reflection note: " + ex.Message); }

        // Invoke private FlushPendingToDisk() via reflection (synchronous file write)
        bool invoked = false;
        try
        {
            var m = receiver.GetType().GetMethod("FlushPendingToDisk", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m != null)
            {
                m.Invoke(receiver, null);
                invoked = true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[EndScene] EEG flush reflection failed: " + ex.Message);
        }

        if (invoked)
        {
            // Give the OS a frame for IO to settle (belt & braces)
            yield return null;
        }
    }

    private bool SceneExistsInBuild(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
