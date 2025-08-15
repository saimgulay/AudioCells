// Assets/Scripts/SynthPreset.cs
using UnityEngine;

[CreateAssetMenu(fileName = "SynthPreset", menuName = "Synthesizer/Synth Preset")]
public class SynthPreset : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Displayed name of this preset")]
    public string presetName;

    [Tooltip("Enter one or more labels, separated by commas")]
    public string label;  // e.g. "Lead,Pad,Bass"

    [Header("Synth Parameters")]
    public OscillatorSettings oscillator1;
    public OscillatorSettings oscillator2;
    public OperationSettings audioOperations;
    public WaveshaperSettings waveshaper;
    public FrequencyFilterSettings[] frequencyFilters;
    public ParametricEQBand[] eqBands;
    public CombFilterSettings combFilter;
    public OscillatorSettings lfo1;
    public OscillatorSettings lfo2;
    public OperationSettings lfoOperations;
    public ModMatrixEntry[] modMatrix;

    [Header("Limiter")]
    public float Limiter_Threshold_dB;
    public float Limiter_Attack_ms;
    public float Limiter_Release_ms;

    [SerializeField, HideInInspector]
    private float Output_Peak_dB;
}
