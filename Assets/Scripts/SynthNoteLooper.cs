using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Synth))]
public class SynthNoteLooper : MonoBehaviour
{
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

    void Awake()
    {
        // Cache references to the Synth and its first oscillator settings
        synth = GetComponent<Synth>();
        osc1 = synth.oscillator1;
    }

    void Start()
    {
        // Begin the looping coroutine
        StartCoroutine(NoteLoop());
    }

    private IEnumerator NoteLoop()
    {
        while (true)
        {
            yield return StartCoroutine(PlayOneNote());
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator PlayOneNote()
    {
        float elapsed = 0f;

        // Attack: ramp amplitude from 0 to 1
        while (elapsed < attackTime)
        {
            osc1.Amplitude = Mathf.Lerp(0f, 1f, elapsed / attackTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        osc1.Amplitude = 1f;

        // Decay: ramp amplitude from 1 to sustainLevel
        elapsed = 0f;
        while (elapsed < decayTime)
        {
            osc1.Amplitude = Mathf.Lerp(1f, sustainLevel, elapsed / decayTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        osc1.Amplitude = sustainLevel;

        // Sustain: hold level for the remainder of noteDuration
        float sustainTime = noteDuration - (attackTime + decayTime + releaseTime);
        if (sustainTime > 0f)
            yield return new WaitForSeconds(sustainTime);

        // Release: ramp amplitude from sustainLevel to 0
        elapsed = 0f;
        float startLevel = osc1.Amplitude;
        while (elapsed < releaseTime)
        {
            osc1.Amplitude = Mathf.Lerp(startLevel, 0f, elapsed / releaseTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        osc1.Amplitude = 0f;
    }
}
