// Assets/Scripts/GenomeSonification/SynthChordPlayer.cs

using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Plays a repeating five-chord sequence (the first four tetrachords plus a final
/// stretched chord), driving up to four Synth voices. Provides:
///  • Per-voice ADSR envelopes
///  • A global low-pass filter
///  • An oscillator-2 frequency multiplier
///  • Master gain controls
///  • Runtime preset changes (immediately applied)
///  • External override of the first chord via the public `tetrachord` setter
///  • MuteLoop()/UnmuteLoop() to pause or resume the sequence
///  • A public IsRestPeriod flag so callers know when the final “rest” is in effect
///  • A public OnSequenceFinished event, fired once per full 5-chord + rest cycle
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class SynthChordPlayer : MonoBehaviour
{
    // PUBLIC API

    /// <summary>
    /// Fired each time the 5-chord run plus its rest period completes.
    /// Subscribers can wait on this before advancing to the next cell.
    /// </summary>
    public event Action OnSequenceFinished;

    /// <summary>
    /// If non-empty, this overrides chord 1 at runtime.
    /// Setting it immediately rebuilds the chord.
    /// </summary>
    public string tetrachord
    {
        get => _overrideTetrachord;
        set
        {
            _overrideTetrachord = value;
            if (!string.IsNullOrEmpty(_overrideTetrachord))
            {
                tetrachord1 = _overrideTetrachord;
                RebuildFromCurrentSettings();
            }
        }
    }
    private string _overrideTetrachord;

    /// <summary>
    /// Pause the chord-loop coroutine immediately.
    /// </summary>
    public void MuteLoop()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
    }

    /// <summary>
    /// If it’s currently muted, restart the loop coroutine.
    /// </summary>
    public void UnmuteLoop()
    {
        if (sequenceCoroutine == null)
            sequenceCoroutine = StartCoroutine(PlayChordSequence());
    }

    /// <summary>
    /// True during the final restDuration of each 5-chord cycle.
    /// Check this before switching cells so you don’t cut a chord in half.
    /// </summary>
    public bool IsRestPeriod => _isRestPeriod;


    [Header("Presets")]
    [Tooltip("Preset assets defining chords, envelopes, filter, etc.")]
    public SynthChordPreset[] presets;

    [Tooltip("Which preset is selected in the Inspector dropdown.")]
    public int selectedPresetIndex;
    private int lastAppliedPresetIndex = -1;


    [Header("Oscillator 2 Frequency")]
    [Tooltip("Osc2 freq = Osc1 freq * this multiplier.")]
    public float osc2Multiplier = 1f;


    [Header("Master Gain")]
    [Tooltip("Per-voice gain from the preset (0–1).")]
    public float chordMasterGain = 1f;
    [Range(0f, 2f), Tooltip("Overall volume multiplier.")]
    public float masterGain = 1f;


    [Header("ADSR (seconds)")]
    public float attackTime     = 0.1f;
    public float decayTime      = 0.1f;
    [Range(0f,1f)] public float sustainLevel = 0.7f;
    public float releaseTime    = 0.2f;


    [Header("Tetrachords (semitones)")]
    public string tetrachord1 = "[0,2,4,7]";
    public string tetrachord2 = "[0,3,7,10]";
    public string tetrachord3 = "[0,5,9,12]";
    public string tetrachord4 = "[0,7,11,14]";
    public string tetrachord5 = "[0,9,11,18]";

    [Tooltip("Stretch factor for chords 4 & 5.")]
    public float lastChordMultiplier = 2f;


    [Header("Rhythm & Timing")]
    [Tooltip("Base sustain length for chords 1–3.")]
    public float sustainDuration = 1.0f;
    [Tooltip("Pause between each chord.")]
    public float interChordPause = 0.5f;
    [Tooltip("Pause after the full 5-chord run.")]
    public float restDuration = 1.0f;


    [Header("Low-Pass Filter")]
    public bool  enableLowPass             = true;
    [Range(500f,20000f)] public float maximumCutoffFrequency   = 12000f;
    [Range(1f,4f)]       public float cutoffFrequencyMultiplier = 1.2f;


    [Header("Synth Voices")]
    [Tooltip("Up to four Synth components, one voice each.")]
    public Synth[] synthVoices = new Synth[4];

    [Tooltip("Frequency for semitone 0 (e.g. C4 = 261.63Hz).")]
    public float baseFrequency = 261.63f;


    // INTERNAL FIELDS

    private AudioLowPassFilter    lowPassFilter;
    private OscillatorSettings[]  osc1Settings;
    private OscillatorSettings[]  osc2Settings;
    private float[][]             allNoteFrequencies = new float[5][]; // 5 chords × voice count
    private Coroutine             sequenceCoroutine;
    private float                 voiceGainPerOscillator;
    private bool                  _isRestPeriod = false;


    void Awake()
    {
        // Cache LPF
        lowPassFilter = GetComponent<AudioLowPassFilter>();

        // Cache each Synth’s oscillators
        int vCount = synthVoices.Length;
        osc1Settings = new OscillatorSettings[vCount];
        osc2Settings = new OscillatorSettings[vCount];
        for (int i = 0; i < vCount; i++)
        {
            osc1Settings[i] = synthVoices[i].oscillator1;
            osc2Settings[i] = synthVoices[i].oscillator2;
        }

        // Allocate our frequency buffers
        for (int i = 0; i < 5; i++)
            allNoteFrequencies[i] = new float[vCount];

        // Apply initial preset if present
        if (presets != null && presets.Length > 0)
        {
            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presets.Length - 1);
            ApplyPreset(presets[selectedPresetIndex]);
            lastAppliedPresetIndex = selectedPresetIndex;
        }

        // Build note arrays from tetrachord strings + baseFrequency
        RebuildFromCurrentSettings();

        // Start the playback loop
        sequenceCoroutine = StartCoroutine(PlayChordSequence());
    }


    /// <summary>
    /// Apply a new preset _without_ restarting the loop.
    /// Used for external on-the-fly changes.
    /// </summary>
    public void SetPresetIndexWithoutRestart(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return;
        selectedPresetIndex = index;
        ApplyPreset(presets[index]);
        RebuildFromCurrentSettings();
    }


    /// <summary>
    /// Called by a UI dropdown in the editor—applies then restarts the loop.
    /// </summary>
    public void OnPresetChanged(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return;

        selectedPresetIndex    = index;
        lastAppliedPresetIndex = index;
        ApplyPreset(presets[index]);
        RebuildFromCurrentSettings();

        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(PlayChordSequence());
    }


    private void ApplyPreset(SynthChordPreset p)
    {
        tetrachord1             = p.tetrachord1;
        tetrachord2             = p.tetrachord2;
        tetrachord3             = p.tetrachord3;
        tetrachord4             = p.tetrachord4;
        tetrachord5             = p.tetrachord5;
        lastChordMultiplier     = p.lastChordMultiplier;

        sustainDuration         = p.sustainDuration;
        interChordPause         = p.interChordPause;
        restDuration            = p.restDuration;

        enableLowPass           = p.enableLowPass;
        maximumCutoffFrequency  = p.maxCutoff;
        cutoffFrequencyMultiplier = p.cutoffMultiplier;

        attackTime              = p.attackTime;
        decayTime               = p.decayTime;
        sustainLevel            = p.sustainLevel;
        releaseTime             = p.releaseTime;

        baseFrequency           = p.baseFrequency;
        chordMasterGain         = p.chordMasterGain;
    }


    private void RebuildFromCurrentSettings()
    {
        ParseTetrachord(tetrachord1, allNoteFrequencies[0]);
        ParseTetrachord(tetrachord2, allNoteFrequencies[1]);
        ParseTetrachord(tetrachord3, allNoteFrequencies[2]);
        ParseTetrachord(tetrachord4, allNoteFrequencies[3]);
        ParseTetrachord(tetrachord5, allNoteFrequencies[4]);

        voiceGainPerOscillator = (chordMasterGain * masterGain) / synthVoices.Length;
        lowPassFilter.enabled = enableLowPass;
        lowPassFilter.cutoffFrequency = maximumCutoffFrequency;
    }


    private IEnumerator PlayChordSequence()
    {
        while (true)
        {
            // Clear the rest flag at the start of each run
            _isRestPeriod = false;

            for (int i = 0; i < 5; i++)
            {
                // Re-parse in case tetrachord1 was overridden
                RebuildFromCurrentSettings();

                float hold = sustainDuration;
                if (i >= 3) hold *= lastChordMultiplier;

                yield return StartCoroutine(PlayChord(allNoteFrequencies[i], hold));
                // pause between each chord (except after chord 5)
                if (i < 4)
                    yield return new WaitForSeconds(interChordPause);
            }

            // now we’ve played all 5 chords—enter rest
            _isRestPeriod = true;
            yield return new WaitForSeconds(restDuration);

            // Notify listeners that the full cycle (5 chords + rest) has finished
            OnSequenceFinished?.Invoke();
        }
    }


    private IEnumerator PlayChord(float[] freqs, float holdDuration)
    {
        int vCount = synthVoices.Length;

        // set both oscillators per voice
        for (int v = 0; v < vCount; v++)
        {
            float f = freqs[v];
            osc1Settings[v].Frequency = f;
            osc2Settings[v].Frequency = f * osc2Multiplier;
        }

        // adapt LPF cutoff to highest note
        if (enableLowPass)
        {
            float maxF = 0f;
            foreach (var f in freqs) if (f > maxF) maxF = f;
            lowPassFilter.cutoffFrequency = Mathf.Min(
                maxF * cutoffFrequencyMultiplier,
                maximumCutoffFrequency
            );
        }

        // ATTACK
        float t = 0f;
        while (t < attackTime)
        {
            float amp = Mathf.Lerp(0f, voiceGainPerOscillator, t / attackTime);
            ApplyAmplitude(amp);
            t += Time.deltaTime;
            yield return null;
        }

        // DECAY
        t = 0f;
        while (t < decayTime)
        {
            float amp = Mathf.Lerp(
                voiceGainPerOscillator,
                voiceGainPerOscillator * sustainLevel,
                t / decayTime
            );
            ApplyAmplitude(amp);
            t += Time.deltaTime;
            yield return null;
        }

        // SUSTAIN
        ApplyAmplitude(voiceGainPerOscillator * sustainLevel);
        yield return new WaitForSeconds(holdDuration);

        // RELEASE
        t = 0f;
        float startAmp = voiceGainPerOscillator * sustainLevel;
        while (t < releaseTime)
        {
            float amp = Mathf.Lerp(startAmp, 0f, t / releaseTime);
            ApplyAmplitude(amp);
            t += Time.deltaTime;
            yield return null;
        }

        // OFF
        ApplyAmplitude(0f);
    }


    private void ApplyAmplitude(float amp)
    {
        foreach (var o in osc1Settings) o.Amplitude = amp;
        foreach (var o in osc2Settings) o.Amplitude = amp;
    }


    private void ParseTetrachord(string str, float[] dest)
    {
        var s = str.Trim();
        if (s.StartsWith("[") && s.EndsWith("]"))
            s = s.Substring(1, s.Length - 2);

        var parts = s
            .Split(new[]{',',' '}, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < dest.Length; i++)
        {
            if (i < parts.Length && float.TryParse(parts[i], out float sem))
                dest[i] = baseFrequency * Mathf.Pow(2f, sem / 12f);
            else
                dest[i] = baseFrequency;
        }
    }


#if UNITY_EDITOR
    void OnValidate()
    {
        // in-editor preset dropdown handling
        if (!Application.isPlaying) return;
        if (presets == null || presets.Length == 0) return;
        if (selectedPresetIndex != lastAppliedPresetIndex &&
            selectedPresetIndex >= 0 &&
            selectedPresetIndex < presets.Length)
        {
            ApplyPreset(presets[selectedPresetIndex]);
            RebuildFromCurrentSettings();
            lastAppliedPresetIndex = selectedPresetIndex;

            if (sequenceCoroutine != null)
                StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(PlayChordSequence());
        }
    }
#endif

}
