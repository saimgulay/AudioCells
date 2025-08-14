using UnityEngine;

[CreateAssetMenu(fileName = "SynthChordPreset", menuName = "Audio/Synth Chord Preset")]
public class SynthChordPreset : ScriptableObject
{
    [Tooltip("Machine‑name of this preset.")]
    public string presetName;

    [Tooltip("Comma‑separated human‑readable labels or tags.")]
    public string label;

    [Header("Tetrachords")]
    public string tetrachord1;
    public string tetrachord2;
    public string tetrachord3;
    public string tetrachord4;
    public string tetrachord5;
    public float lastChordMultiplier;

    [Header("Gain & Timing")]
    public float chordMasterGain;
    public float sustainDuration;
    public float interChordPause;
    public float restDuration;

    [Header("Low‑Pass Filter")]
    public bool enableLowPass;
    public float maxCutoff;
    public float cutoffMultiplier;

    [Header("ADSR Envelope")]
    public float attackTime;
    public float decayTime;
    public float sustainLevel;
    public float releaseTime;

    [Header("Other")]
    public float baseFrequency;
}
