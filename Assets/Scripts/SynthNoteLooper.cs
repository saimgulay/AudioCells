using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Synth))]
public class SynthNoteLooper : MonoBehaviour
{
    [Header("Bypass")]
    [Tooltip("Tick to bypass the looper. When enabled, envelope and note playback are skipped and amplitude is forced to 0.")]
    public bool bypass = false;

    [Header("ADSR Envelope (seconds)")]
    [Tooltip("Time taken for the attack phase.")]
    public float attackTime = 0.1f;

    [Tooltip("Time taken for the decay phase.")]
    public float decayTime = 0.1f;

    [Range(0f, 1f), Tooltip("Level to sustain at after decay.")]
    public float sustainLevel = 0.7f;

    [Tooltip("Time taken for the release phase.")]
    public float releaseTime = 0.2f;

    [Header("Note Playback (seconds)")]
    [Tooltip("Total duration of each note (including ADSR).")]
    public float noteDuration = 1.0f;

    [Tooltip("Pause between successive notes.")]
    public float waitTime = 0.5f;

    private Synth synth;
    private OscillatorSettings osc1;
    private Coroutine loopRoutine;

    void Awake()
    {
        // Cache references to the Synth and its first oscillator settings
        synth = GetComponent<Synth>();
        osc1 = synth.oscillator1;
    }

    void OnEnable()
    {
        TryStartLoop();
    }

    void OnDisable()
    {
        StopLoopImmediate();
    }

    void Update()
    {
        // Live toggling support
        if (bypass)
        {
            if (loopRoutine != null)
                StopLoopImmediate(); // ensure envelope stops and amplitude is zeroed
        }
        else
        {
            if (loopRoutine == null)
                TryStartLoop(); // resume when un-bypassed
        }
    }

    private void TryStartLoop()
    {
        if (bypass) { ForceSilence(); return; }
        if (loopRoutine == null)
            loopRoutine = StartCoroutine(NoteLoop());
    }

    private void StopLoopImmediate()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }
        ForceSilence();
    }

    private void ForceSilence()
    {
        if (osc1 != null)
            osc1.Amplitude = 0f;
    }

    private IEnumerator NoteLoop()
    {
        while (true)
        {
            // If bypass turned on mid-loop, exit gracefully
            if (bypass) yield break;

            yield return StartCoroutine(PlayOneNote());

            if (bypass) yield break;

            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);
            else
                yield return null;
        }
    }

    private IEnumerator PlayOneNote()
    {
        // Early out if bypassed
        if (bypass) yield break;

        float elapsed = 0f;

        // Attack: ramp amplitude from 0 to 1
        if (attackTime > 0f)
        {
            while (elapsed < attackTime)
            {
                if (bypass) yield break;
                osc1.Amplitude = Mathf.Lerp(0f, 1f, elapsed / attackTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        osc1.Amplitude = 1f;

        // Decay: ramp amplitude from 1 to sustainLevel
        elapsed = 0f;
        if (decayTime > 0f)
        {
            while (elapsed < decayTime)
            {
                if (bypass) yield break;
                osc1.Amplitude = Mathf.Lerp(1f, sustainLevel, elapsed / decayTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        osc1.Amplitude = sustainLevel;

        // Sustain: hold level for the remainder of noteDuration
        float sustainTime = noteDuration - (attackTime + decayTime + releaseTime);
        if (sustainTime > 0f)
        {
            float t = 0f;
            while (t < sustainTime)
            {
                if (bypass) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // Release: ramp amplitude from sustainLevel to 0
        elapsed = 0f;
        if (releaseTime > 0f)
        {
            float startLevel = osc1.Amplitude;
            while (elapsed < releaseTime)
            {
                if (bypass) yield break;
                osc1.Amplitude = Mathf.Lerp(startLevel, 0f, elapsed / releaseTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        osc1.Amplitude = 0f;
    }
}
