using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MainSynth))]
public class SyntaxSonification : MonoBehaviour
{
    [System.Serializable]
    public struct NoteData
    {
        [Tooltip("Frequency of this note in Hz")]
        public float frequency;
        [Tooltip("Amplitude (0–1) of this note")]
        [Range(0f, 1f)]
        public float amplitude;
    }

    [Header("Melody Template")]
    [Tooltip("List of notes: frequency + amplitude")]
    public List<NoteData> notes = new List<NoteData>();

    [Header("Timing (seconds)")]
    [Tooltip("How long each note plays")]
    public float noteDuration = 0.2f;
    [Tooltip("Min pause between full melodies")]
    public float minWaitTime = 1f;
    [Tooltip("Max pause between full melodies")]
    public float maxWaitTime = 2f;

    [Header("ADSR Settings")]
    [Tooltip("Attack time for all notes")]
    public float attackTime = 0.02f;
    [Tooltip("Decay time for all notes")]
    public float decayTime = 0.15f;
    [Range(0f, 1f)]
    [Tooltip("Sustain level for all notes")]
    public float sustainLevel = 0.8f;
    [Tooltip("Release time for all notes")]
    public float releaseTime = 0.5f;

    private MainSynth _synth;

    void Awake()
    {
        _synth = GetComponent<MainSynth>();
        if (_synth == null)
            Debug.LogError("SyntaxSonification requires a MainSynth on the same GameObject.", this);
    }

    void Start()
    {
        StartCoroutine(MelodyLoop());
    }

    private IEnumerator MelodyLoop()
    {
        while (true)
        {
            // play each note in sequence
            foreach (var note in notes)
            {
                PlayNote(note.frequency, note.amplitude);
                yield return new WaitForSeconds(noteDuration);
            }

            // wait a random interval before repeating
            float wait = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(wait);
        }
    }

    private void PlayNote(float freq, float amp)
    {
        // configure MainSynth parameters
        _synth.frequency    = freq;
        _synth.sustainLevel = amp;
        _synth.attackTime   = attackTime;
        _synth.decayTime    = decayTime;
        _synth.releaseTime  = releaseTime;

        // retrigger ADSR envelope
        _synth.enabled = false;
        // one frame delay to register disable
        StartCoroutine(EnableNextFrame());
    }

    private IEnumerator EnableNextFrame()
    {
        yield return null;
        _synth.enabled = true;
    }
}
