// Assets/Scripts/Audio/SynthMelodyLooper.cs

using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Synth))]
public class SynthMelodyLooper : MonoBehaviour
{
    public event Action OnMelodyFinished;
    public bool IsPlayingMelody { get; private set; }

    [Header("ADSR Envelope (seconds)")]
    [Tooltip("Time taken for the attack phase.")]
    public float attackTime = 0.1f;
    [Tooltip("Time taken for the decay phase.")]
    public float decayTime = 0.1f;
    [Range(0f, 1f), Tooltip("Level to sustain at after decay.")]
    public float sustainLevel = 0.7f;
    [Tooltip("Time taken for the release phase.")]
    public float releaseTime = 0.2f;

    [Header("Tetrachord Semitones (0–11)")]
    [Range(0, 11), Tooltip("First semitone of the tetrachord.")]
    public int semitone0 = 0;
    [Range(0, 11), Tooltip("Second semitone of the tetrachord.")]
    public int semitone1 = 2;
    [Range(0, 11), Tooltip("Third semitone of the tetrachord.")]
    public int semitone2 = 4;
    [Range(0, 11), Tooltip("Fourth semitone of the tetrachord.")]
    public int semitone3 = 7;

    [Header("Base Frequency")]
    [Tooltip("Base pitch, in Hz, that semitone 0 will map to (e.g. C4 = 261.63).")]
    public float baseFrequency = 261.63f;

    [Header("Timing")]
    [Tooltip("Pause between successive notes (seconds).")]
    public float interNotePause = 0.5f;
    [Tooltip("Pause after the full melody before repeating (seconds).")]
    public float interMelodyPause = 1.0f;

    [Header("Oscillator 2 Multiplier")]
    [Tooltip("Multiplier to apply to oscillator 1's frequency when setting oscillator 2's frequency.")]
    public float osc2Multiplier = 1.0f;

    private Synth _synth;
    private OscillatorSettings _osc1;
    private OscillatorSettings _osc2;
    private float[] _notes = new float[4];

    void Awake()
    {
        _synth = GetComponent<Synth>();
        _osc1  = _synth.oscillator1;
        _osc2  = _synth.oscillator2;
        ParseTetrachord();
    }

    void OnValidate()
    {
        ParseTetrachord();
    }

    void Start()
    {
        StartCoroutine(PlayMelodyLoop());
    }

    private void ParseTetrachord()
    {
        int[] semitones = { semitone0, semitone1, semitone2, semitone3 };
        for (int i = 0; i < 4; i++)
        {
            float s = Mathf.Repeat(semitones[i], 12);
            _notes[i] = baseFrequency * Mathf.Pow(2f, s / 12f);
        }
    }

    public void ApplyTetrachord()
    {
        ParseTetrachord();
    }

    private IEnumerator PlayMelodyLoop()
    {
        while (true)
        {
            IsPlayingMelody = true;

            for (int i = 0; i < 4; i++)
            {
                yield return StartCoroutine(PlaySingleNote(_notes[i]));
                yield return new WaitForSeconds(interNotePause);
            }

            yield return new WaitForSeconds(interMelodyPause);

            IsPlayingMelody = false;
            OnMelodyFinished?.Invoke();
        }
    }

    private IEnumerator PlaySingleNote(float frequency)
    {
        _osc1.Frequency = frequency;
        _osc2.Frequency = frequency * osc2Multiplier;

        float elapsed = 0f;

        // Attack
        while (elapsed < attackTime)
        {
            float amp = Mathf.Lerp(0f, 1f, elapsed / attackTime);
            _osc1.Amplitude = amp;
            _osc2.Amplitude = amp;
            elapsed += Time.deltaTime;
            yield return null;
        }
        _osc1.Amplitude = 1f;
        _osc2.Amplitude = 1f;

        // Decay
        elapsed = 0f;
        while (elapsed < decayTime)
        {
            float amp = Mathf.Lerp(1f, sustainLevel, elapsed / decayTime);
            _osc1.Amplitude = amp;
            _osc2.Amplitude = amp;
            elapsed += Time.deltaTime;
            yield return null;
        }
        _osc1.Amplitude = sustainLevel;
        _osc2.Amplitude = sustainLevel;

        // Sustain
        float sustainDuration = Mathf.Max(0f, interNotePause - (attackTime + decayTime + releaseTime));
        if (sustainDuration > 0f)
            yield return new WaitForSeconds(sustainDuration);

        // Release
        elapsed = 0f;
        float startLevel = sustainLevel;
        while (elapsed < releaseTime)
        {
            float amp = Mathf.Lerp(startLevel, 0f, elapsed / releaseTime);
            _osc1.Amplitude = amp;
            _osc2.Amplitude = amp;
            elapsed += Time.deltaTime;
            yield return null;
        }
        _osc1.Amplitude = 0f;
        _osc2.Amplitude = 0f;
    }
}
