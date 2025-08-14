// PhoneticGenomeSynthController.cs
// Final, fully implemented and syntactically corrected version.
// Extended to support both Synth parameters and SynthNoteLooper parameters as outputs.

using UnityEngine;
using RTMLToolKit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

#region Parameter Enums

public enum GenomeParameter
{
    None,
    RunSpeedFactor, TumbleSensitivity, MetabolismRate, ReproductionThreshold, OptimalTemperature, TemperatureSensitivity, OptimalPH, PHSensitivity, ToxinResistance, UVResistance,
    NutrientEfficiencyA, NutrientEfficiencyB, NutrientEfficiencyC, NutrientEfficiencyD,
    ChemotaxisMemoryLength, BaseTumbleRate, TumbleSlopeSensitivity, TumbleAngleRange, ExplorationTendency, GradientTolerance, DecisionNoise,
    TemperaturePlasticity, PHPlasticity,
    StarvationTolerance, DeathDelayBias, RestBehaviour,
    NutrientDiscrimination, DeadCellPreference,
    ConjugationAggressiveness, GeneticStability,
    BaseRunSpeed, TumbleDuration,
    SensorRadius,
    DivisionDelay, SelfPreservation,
    DormancyThreshold, WakeUpEnergyCost, ToxinProductionRate, ToxinPotency, BiofilmTendency, BiofilmMatrixCost, QuorumSensingThreshold, QuorumToxinBoost, KinRecognitionFidelity, KinCooperationBonus, StressInducedMutabilityFactor, PlasmidCompatibilityThreshold
}

public enum SynthParameter
{
    None,
    Osc1_Sine_Mix, Osc1_Square_Mix, Osc1_Sawtooth_Mix, Osc1_Triangle_Mix, Osc1_Ramp_Mix, Osc1_Wavetable_Mix,
    Osc1_Harmonic_00, Osc1_Harmonic_01, Osc1_Harmonic_02, Osc1_Harmonic_03, Osc1_Harmonic_04, Osc1_Harmonic_05, Osc1_Harmonic_06, Osc1_Harmonic_07,
    Osc1_Harmonic_08, Osc1_Harmonic_09, Osc1_Harmonic_10, Osc1_Harmonic_11, Osc1_Harmonic_12, Osc1_Harmonic_13, Osc1_Harmonic_14, Osc1_Harmonic_15,
    Osc1_Frequency, Osc1_Amplitude, Osc1_Offset,
    Osc2_Sine_Mix, Osc2_Square_Mix, Osc2_Sawtooth_Mix, Osc2_Triangle_Mix, Osc2_Ramp_Mix, Osc2_Wavetable_Mix,
    Osc2_Harmonic_00, Osc2_Harmonic_01, Osc2_Harmonic_02, Osc2_Harmonic_03, Osc2_Harmonic_04, Osc2_Harmonic_05, Osc2_Harmonic_06, Osc2_Harmonic_07,
    Osc2_Harmonic_08, Osc2_Harmonic_09, Osc2_Harmonic_10, Osc2_Harmonic_11, Osc2_Harmonic_12, Osc2_Harmonic_13, Osc2_Harmonic_14, Osc2_Harmonic_15,
    Osc2_Frequency, Osc2_Amplitude, Osc2_Offset,
    AudioOp_PreOp_Multiply, AudioOp_PreOp_Add, AudioOp_PostOp_Multiply, AudioOp_PostOp_Add,
    Waveshaper_Enabled, Waveshaper_Shape, Waveshaper_Drive, Waveshaper_Mix,
    EQ1_Enabled, EQ1_Frequency, EQ1_Bandwidth, EQ1_Boost_dB, EQ1_Frequency_Mod, EQ1_Bandwidth_Mod, EQ1_Boost_Mod,
    EQ2_Enabled, EQ2_Frequency, EQ2_Bandwidth, EQ2_Boost_dB, EQ2_Frequency_Mod, EQ2_Bandwidth_Mod, EQ2_Boost_Mod,
    EQ3_Enabled, EQ3_Frequency, EQ3_Bandwidth, EQ3_Boost_dB, EQ3_Frequency_Mod, EQ3_Bandwidth_Mod, EQ3_Boost_Mod,
    Filter1_Enabled, Filter1_Type, Filter1_Cutoff, Filter1_Resonance, Filter1_Cutoff_Mod, Filter1_Resonance_Mod,
    Filter2_Enabled, Filter2_Type, Filter2_Cutoff, Filter2_Resonance, Filter2_Cutoff_Mod, Filter2_Resonance_Mod,
    Filter3_Enabled, Filter3_Type, Filter3_Cutoff, Filter3_Resonance, Filter3_Cutoff_Mod, Filter3_Resonance_Mod,
    Filter4_Enabled, Filter4_Type, Filter4_Cutoff, Filter4_Resonance, Filter4_Cutoff_Mod, Filter4_Resonance_Mod,
    CombFilter_Enabled, CombFilter_Delay_ms, CombFilter_Feedback, CombFilter_Mix,
    LFO1_Sine_Mix, LFO1_Square_Mix, LFO1_Sawtooth_Mix, LFO1_Triangle_Mix, LFO1_Ramp_Mix, LFO1_Wavetable_Mix,
    LFO1_Harmonic_00, LFO1_Harmonic_01, LFO1_Harmonic_02, LFO1_Harmonic_03, LFO1_Harmonic_04, LFO1_Harmonic_05, LFO1_Harmonic_06, LFO1_Harmonic_07,
    LFO1_Harmonic_08, LFO1_Harmonic_09, LFO1_Harmonic_10, LFO1_Harmonic_11, LFO1_Harmonic_12, LFO1_Harmonic_13, LFO1_Harmonic_14, LFO1_Harmonic_15,
    LFO1_Frequency, LFO1_Amplitude, LFO1_Offset,
    LFO2_Sine_Mix, LFO2_Square_Mix, LFO2_Sawtooth_Mix, LFO2_Triangle_Mix, LFO2_Ramp_Mix, LFO2_Wavetable_Mix,
    LFO2_Harmonic_00, LFO2_Harmonic_01, LFO2_Harmonic_02, LFO2_Harmonic_03, LFO2_Harmonic_04, LFO2_Harmonic_05, LFO2_Harmonic_06, LFO2_Harmonic_07,
    LFO2_Harmonic_08, LFO2_Harmonic_09, LFO2_Harmonic_10, LFO2_Harmonic_11, LFO2_Harmonic_12, LFO2_Harmonic_13, LFO2_Harmonic_14, LFO2_Harmonic_15,
    LFO2_Frequency, LFO2_Amplitude, LFO2_Offset,
    LFO_Op_PreOp_Multiply, LFO_Op_PreOp_Add, LFO_Op_PostOp_Multiply, LFO_Op_PostOp_Add,
    ModMatrix1_Enabled, ModMatrix1_Source, ModMatrix1_Destination, ModMatrix1_Amount,
    ModMatrix2_Enabled, ModMatrix2_Source, ModMatrix2_Destination, ModMatrix2_Amount,
    ModMatrix3_Enabled, ModMatrix3_Source, ModMatrix3_Destination, ModMatrix3_Amount,
    ModMatrix4_Enabled, ModMatrix4_Source, ModMatrix4_Destination, ModMatrix4_Amount,
    ModMatrix5_Enabled, ModMatrix5_Source, ModMatrix5_Destination, ModMatrix5_Amount,
    ModMatrix6_Enabled, ModMatrix6_Source, ModMatrix6_Destination, ModMatrix6_Amount,
    ModMatrix7_Enabled, ModMatrix7_Source, ModMatrix7_Destination, ModMatrix7_Amount,
    ModMatrix8_Enabled, ModMatrix8_Source, ModMatrix8_Destination, ModMatrix8_Amount,
    Master_Limiter_Threshold_dB, Master_Limiter_Attack_ms, Master_Limiter_Release_ms
}

public enum LooperParameter
{
    None,
    AttackTime,
    DecayTime,
    SustainLevel,
    ReleaseTime,
    NoteDuration,
    WaitTime
}

#endregion

[ExecuteAlways]
public class PhoneticGenomeSynthController : MonoBehaviour
{
    [Header("RTML Settings")]
    [Tooltip("Reference to your RTMLRunner's RTMLCore component.")]
    public RTMLCore rtmlCore;

    [Header("Data Sources")]
    [Tooltip("Provides the current sampled E. coli genome.")]
    public EColiGenomeLogger genomeLogger;

    [Tooltip("The Synth component whose parameters will be driven.")]
    public Synth synth;

    [Tooltip("The SynthNoteLooper component whose parameters will also be driven.")]
    public SynthNoteLooper noteLooper;

    [Header("Parameter Selection")]
    [Tooltip("Choose which Genome parameters to use as inputs for the model.")]
    public List<GenomeParameter> selectedGenomeInputs = new List<GenomeParameter>();
    
    [Tooltip("Choose which Synth parameters the model should learn to control.")]
    public List<SynthParameter> selectedSynthOutputs = new List<SynthParameter>();

    [Tooltip("Choose which Note Looper parameters the model should learn to control.")]
    public List<LooperParameter> selectedLooperOutputs = new List<LooperParameter>();

    [Header("Keyboard Shortcuts")]
    public KeyCode recordKey   = KeyCode.R;
    public KeyCode trainKey    = KeyCode.T;
    public KeyCode predictKey  = KeyCode.P;

    public void UpdateModelDimensions()
    {
        if (rtmlCore != null)
        {
            rtmlCore.inputSize  = selectedGenomeInputs.Count;
            rtmlCore.outputSize = selectedSynthOutputs.Count + selectedLooperOutputs.Count;
        }
    }

    void Awake()
    {
        if (rtmlCore != null)
        {
            rtmlCore.enableRun    = false;
            rtmlCore.enableRecord = true;
            UpdateModelDimensions();
        }
    }

    void Update()
    {
        if (rtmlCore == null || genomeLogger == null || synth == null || noteLooper == null) return;

        if (Input.GetKeyDown(recordKey)) HandleRecord();
        if (Input.GetKeyDown(trainKey)) HandleTrain();
        if (Input.GetKeyDown(predictKey)) HandlePredictToggle();

        if (rtmlCore.enableRun) RunPrediction();
    }
    
    #region Core ML Methods

    private void HandleRecord()
    {
        var genomeVec = BuildInputVector();
        var outputVec = BuildOutputVector();

        if (genomeVec.Length == 0 || outputVec.Length == 0)
        {
            Debug.LogWarning("[GenomeSynth] Cannot record sample. Input or Output parameter lists are empty.");
            return;
        }

        rtmlCore.RecordSample(genomeVec, outputVec);
        Debug.Log($"[GenomeSynth] Recorded sample successfully. (Inputs: {genomeVec.Length}, Outputs: {outputVec.Length})");
    }

    private void HandleTrain()
    {
        rtmlCore.TrainModel();
        Debug.Log("[GenomeSynth] Model training started.");
    }

    private void HandlePredictToggle()
    {
        rtmlCore.enableRun = !rtmlCore.enableRun;
        Debug.Log($"[GenomeSynth] Live synthesis toggled to: {rtmlCore.enableRun}");
    }

    private void RunPrediction()
    {
        var genomeVec = BuildInputVector();
        if (genomeVec.Length == 0) return;

        float[] pred = rtmlCore.PredictSample(genomeVec);
        
        if (pred != null && pred.Length > 0 && float.IsNaN(pred[0]))
        {
            Debug.LogError("[GenomeSynth] Prediction resulted in NaN! Check input normalisation and model learning rate."); 
            rtmlCore.enableRun = false; 
            return;
        }
        
        ApplyOutputVector(pred);
    }

    #endregion

    #region Vector Building and Applying

    private float[] BuildInputVector()
    {
        var g = genomeLogger.currentGenome;
        if (g == null) return new float[selectedGenomeInputs.Count];

        float[] v = new float[selectedGenomeInputs.Count];
        for (int i = 0; i < selectedGenomeInputs.Count; i++)
        {
            v[i] = GetGenomeValue(g, selectedGenomeInputs[i]);
        }
        return v;
    }

    private float[] BuildSynthOutputVector()
    {
        if (synth == null) return new float[selectedSynthOutputs.Count];

        float[] v = new float[selectedSynthOutputs.Count];
        for (int i = 0; i < selectedSynthOutputs.Count; i++)
        {
            v[i] = GetSynthValue(synth, selectedSynthOutputs[i]);
        }
        return v;
    }

    private float[] BuildLooperOutputVector()
    {
        if (noteLooper == null) return new float[selectedLooperOutputs.Count];

        float[] v = new float[selectedLooperOutputs.Count];
        for (int i = 0; i < selectedLooperOutputs.Count; i++)
        {
            v[i] = GetLooperValue(noteLooper, selectedLooperOutputs[i]);
        }
        return v;
    }

    private float[] BuildOutputVector()
    {
        var synthVec  = BuildSynthOutputVector();
        var looperVec = BuildLooperOutputVector();
        return synthVec.Concat(looperVec).ToArray();
    }

    private void ApplyOutputVector(float[] data)
    {
        int synthCount  = selectedSynthOutputs.Count;
        int looperCount = selectedLooperOutputs.Count;

        if (data == null || data.Length != synthCount + looperCount) return;

        int index = 0;
        for (int i = 0; i < synthCount; i++)
        {
            SetSynthValue(synth, selectedSynthOutputs[i], data[index++]);
        }
        for (int i = 0; i < looperCount; i++)
        {
            SetLooperValue(noteLooper, selectedLooperOutputs[i], data[index++]);
        }

        if (synth != null)
        {
            synth.SetWavetablesDirty();
        }
    }

    #endregion

    #region Parameter Getters and Setters

    private float Normalise(float value, float min, float max)
    {
        if (Mathf.Approximately(max, min)) return 0f;
        return (value - min) / (max - min);
    }

    private float GetGenomeValue(EColiGenome g, GenomeParameter param)
    {
        switch (param)
        {
            case GenomeParameter.RunSpeedFactor: return Normalise(g.runSpeedFactor, 0f, 5f);
            case GenomeParameter.TumbleSensitivity: return Normalise(g.tumbleSensitivity, 0f, 5f);
            case GenomeParameter.MetabolismRate: return Normalise(g.metabolismRate, 0f, 5f);
            case GenomeParameter.ReproductionThreshold: return Normalise(g.reproductionThreshold, 5f, 20f);
            case GenomeParameter.OptimalTemperature: return Normalise(g.optimalTemperature, 0f, 40f);
            case GenomeParameter.TemperatureSensitivity: return Normalise(g.temperatureSensitivity, 0f, 1f);
            case GenomeParameter.OptimalPH: return Normalise(g.optimalPH, 0f, 14f);
            case GenomeParameter.PHSensitivity: return Normalise(g.pHSensitivity, 0f, 1f);
            case GenomeParameter.ToxinResistance: return Normalise(g.toxinResistance, 0f, 1f);
            case GenomeParameter.UVResistance: return g.uvResistance;
            case GenomeParameter.NutrientEfficiencyA: return Normalise(g.nutrientEfficiencyA, 0f, 2f);
            case GenomeParameter.NutrientEfficiencyB: return Normalise(g.nutrientEfficiencyB, 0f, 2f);
            case GenomeParameter.NutrientEfficiencyC: return Normalise(g.nutrientEfficiencyC, 0f, 2f);
            case GenomeParameter.NutrientEfficiencyD: return Normalise(g.nutrientEfficiencyD, 0f, 2f);
            case GenomeParameter.ChemotaxisMemoryLength: return Normalise(g.chemotaxisMemoryLength, 1f, 20f);
            case GenomeParameter.BaseTumbleRate: return Normalise(g.baseTumbleRate, 0f, 1f);
            case GenomeParameter.TumbleSlopeSensitivity: return Normalise(g.tumbleSlopeSensitivity, 0f, 5f);
            case GenomeParameter.TumbleAngleRange: return Normalise(g.tumbleAngleRange, 10f, 180f);
            case GenomeParameter.ExplorationTendency: return g.explorationTendency;
            case GenomeParameter.GradientTolerance: return Normalise(g.gradientTolerance, 0f, 0.1f);
            case GenomeParameter.DecisionNoise: return Normalise(g.decisionNoise, 0f, 0.1f);
            case GenomeParameter.TemperaturePlasticity: return g.temperaturePlasticity;
            case GenomeParameter.PHPlasticity: return g.pHPlasticity;
            case GenomeParameter.StarvationTolerance: return Normalise(g.starvationTolerance, 0f, 10f);
            case GenomeParameter.DeathDelayBias: return Normalise(g.deathDelayBias, 0f, 1f);
            case GenomeParameter.RestBehaviour: return g.restBehavior;
            case GenomeParameter.NutrientDiscrimination: return Normalise(g.nutrientDiscrimination, 0f, 1f);
            case GenomeParameter.DeadCellPreference: return g.deadCellPreference;
            case GenomeParameter.ConjugationAggressiveness: return Normalise(g.conjugationAggressiveness, 0f, 1f);
            case GenomeParameter.GeneticStability: return g.geneticStability;
            case GenomeParameter.BaseRunSpeed: return Normalise(g.baseRunSpeed, 0f, 10f);
            case GenomeParameter.TumbleDuration: return Normalise(g.tumbleDuration, 0f, 5f);
            case GenomeParameter.SensorRadius: return Normalise(g.sensorRadius, 0f, 10f);
            case GenomeParameter.DivisionDelay: return Normalise(g.divisionDelay, 0f, 5f);
            case GenomeParameter.SelfPreservation: return Normalise(g.selfPreservation, 0f, 1f);
            case GenomeParameter.DormancyThreshold: return Normalise(g.dormancyThreshold, 0f, 5f);
            case GenomeParameter.WakeUpEnergyCost: return Normalise(g.wakeUpEnergyCost, 0f, 5f);
            case GenomeParameter.ToxinProductionRate: return Normalise(g.toxinProductionRate, 0f, 1f);
            case GenomeParameter.ToxinPotency: return Normalise(g.toxinPotency, 0f, 1f);
            case GenomeParameter.BiofilmTendency: return Normalise(g.biofilmTendency, 0f, 1f);
            case GenomeParameter.BiofilmMatrixCost: return Normalise(g.biofilmMatrixCost, 0f, 0.2f);
            case GenomeParameter.QuorumSensingThreshold: return Normalise(g.quorumSensingThreshold, 0f, 20f);
            case GenomeParameter.QuorumToxinBoost: return Normalise(g.quorumToxinBoost, 1f, 5f);
            case GenomeParameter.KinRecognitionFidelity: return Normalise(g.kinRecognitionFidelity, 0f, 1f);
            case GenomeParameter.KinCooperationBonus: return Normalise(g.kinCooperationBonus, 1f, 5f);
            case GenomeParameter.StressInducedMutabilityFactor: return Normalise(g.stressInducedMutabilityFactor, 1f, 5f);
            case GenomeParameter.PlasmidCompatibilityThreshold: return Normalise(g.plasmidCompatibilityThreshold, 0f, 5f);
            default: return 0f;
        }
    }

    private float GetSynthValue(Synth s, SynthParameter param)
    {
        switch (param)
        {
            case SynthParameter.Osc1_Sine_Mix:                return s.oscillator1.Sine_Mix;
            case SynthParameter.Osc1_Square_Mix:              return s.oscillator1.Square_Mix;
            case SynthParameter.Osc1_Sawtooth_Mix:            return s.oscillator1.Sawtooth_Mix;
            case SynthParameter.Osc1_Triangle_Mix:            return s.oscillator1.Triangle_Mix;
            case SynthParameter.Osc1_Ramp_Mix:                return s.oscillator1.Ramp_Mix;
            case SynthParameter.Osc1_Wavetable_Mix:           return s.oscillator1.Wavetable_Mix;
            case SynthParameter.Osc1_Harmonic_00:             return s.oscillator1.Harmonics[0];
            case SynthParameter.Osc1_Harmonic_01:             return s.oscillator1.Harmonics[1];
            case SynthParameter.Osc1_Harmonic_02:             return s.oscillator1.Harmonics[2];
            case SynthParameter.Osc1_Harmonic_03:             return s.oscillator1.Harmonics[3];
            case SynthParameter.Osc1_Harmonic_04:             return s.oscillator1.Harmonics[4];
            case SynthParameter.Osc1_Harmonic_05:             return s.oscillator1.Harmonics[5];
            case SynthParameter.Osc1_Harmonic_06:             return s.oscillator1.Harmonics[6];
            case SynthParameter.Osc1_Harmonic_07:             return s.oscillator1.Harmonics[7];
            case SynthParameter.Osc1_Harmonic_08:             return s.oscillator1.Harmonics[8];
            case SynthParameter.Osc1_Harmonic_09:             return s.oscillator1.Harmonics[9];
            case SynthParameter.Osc1_Harmonic_10:             return s.oscillator1.Harmonics[10];
            case SynthParameter.Osc1_Harmonic_11:             return s.oscillator1.Harmonics[11];
            case SynthParameter.Osc1_Harmonic_12:             return s.oscillator1.Harmonics[12];
            case SynthParameter.Osc1_Harmonic_13:             return s.oscillator1.Harmonics[13];
            case SynthParameter.Osc1_Harmonic_14:             return s.oscillator1.Harmonics[14];
            case SynthParameter.Osc1_Harmonic_15:             return s.oscillator1.Harmonics[15];
            case SynthParameter.Osc1_Frequency:               return s.oscillator1.Frequency;
            case SynthParameter.Osc1_Amplitude:               return s.oscillator1.Amplitude;
            case SynthParameter.Osc1_Offset:                  return s.oscillator1.Offset;
            case SynthParameter.Osc2_Sine_Mix:                return s.oscillator2.Sine_Mix;
            case SynthParameter.Osc2_Square_Mix:              return s.oscillator2.Square_Mix;
            case SynthParameter.Osc2_Sawtooth_Mix:            return s.oscillator2.Sawtooth_Mix;
            case SynthParameter.Osc2_Triangle_Mix:            return s.oscillator2.Triangle_Mix;
            case SynthParameter.Osc2_Ramp_Mix:                return s.oscillator2.Ramp_Mix;
            case SynthParameter.Osc2_Wavetable_Mix:           return s.oscillator2.Wavetable_Mix;
            case SynthParameter.Osc2_Harmonic_00:             return s.oscillator2.Harmonics[0];
            case SynthParameter.Osc2_Harmonic_01:             return s.oscillator2.Harmonics[1];
            case SynthParameter.Osc2_Harmonic_02:             return s.oscillator2.Harmonics[2];
            case SynthParameter.Osc2_Harmonic_03:             return s.oscillator2.Harmonics[3];
            case SynthParameter.Osc2_Harmonic_04:             return s.oscillator2.Harmonics[4];
            case SynthParameter.Osc2_Harmonic_05:             return s.oscillator2.Harmonics[5];
            case SynthParameter.Osc2_Harmonic_06:             return s.oscillator2.Harmonics[6];
            case SynthParameter.Osc2_Harmonic_07:             return s.oscillator2.Harmonics[7];
            case SynthParameter.Osc2_Harmonic_08:             return s.oscillator2.Harmonics[8];
            case SynthParameter.Osc2_Harmonic_09:             return s.oscillator2.Harmonics[9];
            case SynthParameter.Osc2_Harmonic_10:             return s.oscillator2.Harmonics[10];
            case SynthParameter.Osc2_Harmonic_11:             return s.oscillator2.Harmonics[11];
            case SynthParameter.Osc2_Harmonic_12:             return s.oscillator2.Harmonics[12];
            case SynthParameter.Osc2_Harmonic_13:             return s.oscillator2.Harmonics[13];
            case SynthParameter.Osc2_Harmonic_14:             return s.oscillator2.Harmonics[14];
            case SynthParameter.Osc2_Harmonic_15:             return s.oscillator2.Harmonics[15];
            case SynthParameter.Osc2_Frequency:               return s.oscillator2.Frequency;
            case SynthParameter.Osc2_Amplitude:               return s.oscillator2.Amplitude;
            case SynthParameter.Osc2_Offset:                  return s.oscillator2.Offset;
            case SynthParameter.AudioOp_PreOp_Multiply:       return s.audioOperations.PreOp_Multiply;
            case SynthParameter.AudioOp_PreOp_Add:            return s.audioOperations.PreOp_Add;
            case SynthParameter.AudioOp_PostOp_Multiply:      return s.audioOperations.PostOp_Multiply;
            case SynthParameter.AudioOp_PostOp_Add:           return s.audioOperations.PostOp_Add;
            case SynthParameter.Waveshaper_Enabled:           return s.waveshaper.Enabled ? 1f : 0f;
            case SynthParameter.Waveshaper_Shape:             return (float)s.waveshaper.Shape;
            case SynthParameter.Waveshaper_Drive:             return s.waveshaper.Drive;
            case SynthParameter.Waveshaper_Mix:               return s.waveshaper.Mix;
            case SynthParameter.EQ1_Enabled:                  return s.eqBands[0].Enabled ? 1f : 0f;
            case SynthParameter.EQ1_Frequency:                return s.eqBands[0].Frequency;
            case SynthParameter.EQ1_Bandwidth:                return s.eqBands[0].Bandwidth;
            case SynthParameter.EQ1_Boost_dB:                 return s.eqBands[0].Boost_dB;
            case SynthParameter.EQ1_Frequency_Mod:            return s.eqBands[0].Frequency_Mod_Amount;
            case SynthParameter.EQ1_Bandwidth_Mod:            return s.eqBands[0].Bandwidth_Mod_Amount;
            case SynthParameter.EQ1_Boost_Mod:                return s.eqBands[0].Boost_Mod_Amount;
            case SynthParameter.EQ2_Enabled:                  return s.eqBands[1].Enabled ? 1f : 0f;
            case SynthParameter.EQ2_Frequency:                return s.eqBands[1].Frequency;
            case SynthParameter.EQ2_Bandwidth:                return s.eqBands[1].Bandwidth;
            case SynthParameter.EQ2_Boost_dB:                 return s.eqBands[1].Boost_dB;
            case SynthParameter.EQ2_Frequency_Mod:            return s.eqBands[1].Frequency_Mod_Amount;
            case SynthParameter.EQ2_Bandwidth_Mod:            return s.eqBands[1].Bandwidth_Mod_Amount;
            case SynthParameter.EQ2_Boost_Mod:                return s.eqBands[1].Boost_Mod_Amount;
            case SynthParameter.EQ3_Enabled:                  return s.eqBands[2].Enabled ? 1f : 0f;
            case SynthParameter.EQ3_Frequency:                return s.eqBands[2].Frequency;
            case SynthParameter.EQ3_Bandwidth:                return s.eqBands[2].Bandwidth;
            case SynthParameter.EQ3_Boost_dB:                 return s.eqBands[2].Boost_dB;
            case SynthParameter.EQ3_Frequency_Mod:            return s.eqBands[2].Frequency_Mod_Amount;
            case SynthParameter.EQ3_Bandwidth_Mod:            return s.eqBands[2].Bandwidth_Mod_Amount;
            case SynthParameter.EQ3_Boost_Mod:                return s.eqBands[2].Boost_Mod_Amount;
            case SynthParameter.Filter1_Enabled:              return s.frequencyFilters[0].Enabled ? 1f : 0f;
            case SynthParameter.Filter1_Type:                 return (float)s.frequencyFilters[0].Type;
            case SynthParameter.Filter1_Cutoff:               return s.frequencyFilters[0].Cutoff;
            case SynthParameter.Filter1_Resonance:            return s.frequencyFilters[0].Resonance;
            case SynthParameter.Filter1_Cutoff_Mod:           return s.frequencyFilters[0].Cutoff_Mod_Amount;
            case SynthParameter.Filter1_Resonance_Mod:        return s.frequencyFilters[0].Resonance_Mod_Amount;
            case SynthParameter.Filter2_Enabled:              return s.frequencyFilters[1].Enabled ? 1f : 0f;
            case SynthParameter.Filter2_Type:                 return (float)s.frequencyFilters[1].Type;
            case SynthParameter.Filter2_Cutoff:               return s.frequencyFilters[1].Cutoff;
            case SynthParameter.Filter2_Resonance:            return s.frequencyFilters[1].Resonance;
            case SynthParameter.Filter2_Cutoff_Mod:           return s.frequencyFilters[1].Cutoff_Mod_Amount;
            case SynthParameter.Filter2_Resonance_Mod:        return s.frequencyFilters[1].Resonance_Mod_Amount;
            case SynthParameter.Filter3_Enabled:              return s.frequencyFilters[2].Enabled ? 1f : 0f;
            case SynthParameter.Filter3_Type:                 return (float)s.frequencyFilters[2].Type;
            case SynthParameter.Filter3_Cutoff:               return s.frequencyFilters[2].Cutoff;
            case SynthParameter.Filter3_Resonance:            return s.frequencyFilters[2].Resonance;
            case SynthParameter.Filter3_Cutoff_Mod:           return s.frequencyFilters[2].Cutoff_Mod_Amount;
            case SynthParameter.Filter3_Resonance_Mod:        return s.frequencyFilters[2].Resonance_Mod_Amount;
            case SynthParameter.Filter4_Enabled:              return s.frequencyFilters[3].Enabled ? 1f : 0f;
            case SynthParameter.Filter4_Type:                 return (float)s.frequencyFilters[3].Type;
            case SynthParameter.Filter4_Cutoff:               return s.frequencyFilters[3].Cutoff;
            case SynthParameter.Filter4_Resonance:            return s.frequencyFilters[3].Resonance;
            case SynthParameter.Filter4_Cutoff_Mod:           return s.frequencyFilters[3].Cutoff_Mod_Amount;
            case SynthParameter.Filter4_Resonance_Mod:        return s.frequencyFilters[3].Resonance_Mod_Amount;
            case SynthParameter.CombFilter_Enabled:           return s.combFilter.Enabled ? 1f : 0f;
            case SynthParameter.CombFilter_Delay_ms:          return s.combFilter.Delay_ms;
            case SynthParameter.CombFilter_Feedback:          return s.combFilter.Feedback;
            case SynthParameter.CombFilter_Mix:               return s.combFilter.Mix;
            case SynthParameter.LFO1_Sine_Mix:                return s.lfo1.Sine_Mix;
            case SynthParameter.LFO1_Square_Mix:              return s.lfo1.Square_Mix;
            case SynthParameter.LFO1_Sawtooth_Mix:            return s.lfo1.Sawtooth_Mix;
            case SynthParameter.LFO1_Triangle_Mix:            return s.lfo1.Triangle_Mix;
            case SynthParameter.LFO1_Ramp_Mix:                return s.lfo1.Ramp_Mix;
            case SynthParameter.LFO1_Wavetable_Mix:           return s.lfo1.Wavetable_Mix;
            case SynthParameter.LFO1_Harmonic_00:             return s.lfo1.Harmonics[0];
            case SynthParameter.LFO1_Harmonic_01:             return s.lfo1.Harmonics[1];
            case SynthParameter.LFO1_Harmonic_02:             return s.lfo1.Harmonics[2];
            case SynthParameter.LFO1_Harmonic_03:             return s.lfo1.Harmonics[3];
            case SynthParameter.LFO1_Harmonic_04:             return s.lfo1.Harmonics[4];
            case SynthParameter.LFO1_Harmonic_05:             return s.lfo1.Harmonics[5];
            case SynthParameter.LFO1_Harmonic_06:             return s.lfo1.Harmonics[6];
            case SynthParameter.LFO1_Harmonic_07:             return s.lfo1.Harmonics[7];
            case SynthParameter.LFO1_Harmonic_08:             return s.lfo1.Harmonics[8];
            case SynthParameter.LFO1_Harmonic_09:             return s.lfo1.Harmonics[9];
            case SynthParameter.LFO1_Harmonic_10:             return s.lfo1.Harmonics[10];
            case SynthParameter.LFO1_Harmonic_11:             return s.lfo1.Harmonics[11];
            case SynthParameter.LFO1_Harmonic_12:             return s.lfo1.Harmonics[12];
            case SynthParameter.LFO1_Harmonic_13:             return s.lfo1.Harmonics[13];
            case SynthParameter.LFO1_Harmonic_14:             return s.lfo1.Harmonics[14];
            case SynthParameter.LFO1_Harmonic_15:             return s.lfo1.Harmonics[15];
            case SynthParameter.LFO1_Frequency:               return s.lfo1.Frequency;
            case SynthParameter.LFO1_Amplitude:               return s.lfo1.Amplitude;
            case SynthParameter.LFO1_Offset:                  return s.lfo1.Offset;
            case SynthParameter.LFO2_Sine_Mix:                return s.lfo2.Sine_Mix;
            case SynthParameter.LFO2_Square_Mix:              return s.lfo2.Square_Mix;
            case SynthParameter.LFO2_Sawtooth_Mix:            return s.lfo2.Sawtooth_Mix;
            case SynthParameter.LFO2_Triangle_Mix:            return s.lfo2.Triangle_Mix;
            case SynthParameter.LFO2_Ramp_Mix:                return s.lfo2.Ramp_Mix;
            case SynthParameter.LFO2_Wavetable_Mix:           return s.lfo2.Wavetable_Mix;
            case SynthParameter.LFO2_Harmonic_00:             return s.lfo2.Harmonics[0];
            case SynthParameter.LFO2_Harmonic_01:             return s.lfo2.Harmonics[1];
            case SynthParameter.LFO2_Harmonic_02:             return s.lfo2.Harmonics[2];
            case SynthParameter.LFO2_Harmonic_03:             return s.lfo2.Harmonics[3];
            case SynthParameter.LFO2_Harmonic_04:             return s.lfo2.Harmonics[4];
            case SynthParameter.LFO2_Harmonic_05:             return s.lfo2.Harmonics[5];
            case SynthParameter.LFO2_Harmonic_06:             return s.lfo2.Harmonics[6];
            case SynthParameter.LFO2_Harmonic_07:             return s.lfo2.Harmonics[7];
            case SynthParameter.LFO2_Harmonic_08:             return s.lfo2.Harmonics[8];
            case SynthParameter.LFO2_Harmonic_09:             return s.lfo2.Harmonics[9];
            case SynthParameter.LFO2_Harmonic_10:             return s.lfo2.Harmonics[10];
            case SynthParameter.LFO2_Harmonic_11:             return s.lfo2.Harmonics[11];
            case SynthParameter.LFO2_Harmonic_12:             return s.lfo2.Harmonics[12];
            case SynthParameter.LFO2_Harmonic_13:             return s.lfo2.Harmonics[13];
            case SynthParameter.LFO2_Harmonic_14:             return s.lfo2.Harmonics[14];
            case SynthParameter.LFO2_Harmonic_15:             return s.lfo2.Harmonics[15];
            case SynthParameter.LFO2_Frequency:               return s.lfo2.Frequency;
            case SynthParameter.LFO2_Amplitude:               return s.lfo2.Amplitude;
            case SynthParameter.LFO2_Offset:                  return s.lfo2.Offset;
            case SynthParameter.LFO_Op_PreOp_Multiply:        return s.lfoOperations.PreOp_Multiply;
            case SynthParameter.LFO_Op_PreOp_Add:             return s.lfoOperations.PreOp_Add;
            case SynthParameter.LFO_Op_PostOp_Multiply:       return s.lfoOperations.PostOp_Multiply;
            case SynthParameter.LFO_Op_PostOp_Add:            return s.lfoOperations.PostOp_Add;
            case SynthParameter.ModMatrix1_Enabled:           return s.modMatrix[0].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix1_Source:            return (float)s.modMatrix[0].Source;
            case SynthParameter.ModMatrix1_Destination:       return (float)s.modMatrix[0].Destination;
            case SynthParameter.ModMatrix1_Amount:            return s.modMatrix[0].Amount;
            case SynthParameter.ModMatrix2_Enabled:           return s.modMatrix[1].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix2_Source:            return (float)s.modMatrix[1].Source;
            case SynthParameter.ModMatrix2_Destination:       return (float)s.modMatrix[1].Destination;
            case SynthParameter.ModMatrix2_Amount:            return s.modMatrix[1].Amount;
            case SynthParameter.ModMatrix3_Enabled:           return s.modMatrix[2].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix3_Source:            return (float)s.modMatrix[2].Source;
            case SynthParameter.ModMatrix3_Destination:       return (float)s.modMatrix[2].Destination;
            case SynthParameter.ModMatrix3_Amount:            return s.modMatrix[2].Amount;
            case SynthParameter.ModMatrix4_Enabled:           return s.modMatrix[3].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix4_Source:            return (float)s.modMatrix[3].Source;
            case SynthParameter.ModMatrix4_Destination:       return (float)s.modMatrix[3].Destination;
            case SynthParameter.ModMatrix4_Amount:            return s.modMatrix[3].Amount;
            case SynthParameter.ModMatrix5_Enabled:           return s.modMatrix[4].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix5_Source:            return (float)s.modMatrix[4].Source;
            case SynthParameter.ModMatrix5_Destination:       return (float)s.modMatrix[4].Destination;
            case SynthParameter.ModMatrix5_Amount:            return s.modMatrix[4].Amount;
            case SynthParameter.ModMatrix6_Enabled:           return s.modMatrix[5].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix6_Source:            return (float)s.modMatrix[5].Source;
            case SynthParameter.ModMatrix6_Destination:       return (float)s.modMatrix[5].Destination;
            case SynthParameter.ModMatrix6_Amount:            return s.modMatrix[5].Amount;
            case SynthParameter.ModMatrix7_Enabled:           return s.modMatrix[6].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix7_Source:            return (float)s.modMatrix[6].Source;
            case SynthParameter.ModMatrix7_Destination:       return (float)s.modMatrix[6].Destination;
            case SynthParameter.ModMatrix7_Amount:            return s.modMatrix[6].Amount;
            case SynthParameter.ModMatrix8_Enabled:           return s.modMatrix[7].Enabled ? 1f : 0f;
            case SynthParameter.ModMatrix8_Source:            return (float)s.modMatrix[7].Source;
            case SynthParameter.ModMatrix8_Destination:       return (float)s.modMatrix[7].Destination;
            case SynthParameter.ModMatrix8_Amount:            return s.modMatrix[7].Amount;
            case SynthParameter.Master_Limiter_Threshold_dB:   return s.Limiter_Threshold_dB;
            case SynthParameter.Master_Limiter_Attack_ms:      return s.Limiter_Attack_ms;
            case SynthParameter.Master_Limiter_Release_ms:     return s.Limiter_Release_ms;
            default:                                          return 0f;
        }
    }

    private void SetSynthValue(Synth s, SynthParameter param, float value)
    {
        switch (param)
        {
            case SynthParameter.Osc1_Sine_Mix:                s.oscillator1.Sine_Mix           = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Square_Mix:              s.oscillator1.Square_Mix         = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Sawtooth_Mix:            s.oscillator1.Sawtooth_Mix       = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Triangle_Mix:            s.oscillator1.Triangle_Mix       = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Ramp_Mix:                s.oscillator1.Ramp_Mix           = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Wavetable_Mix:           s.oscillator1.Wavetable_Mix      = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Harmonic_00:             s.oscillator1.Harmonics[0]       = value; break;
            case SynthParameter.Osc1_Harmonic_01:             s.oscillator1.Harmonics[1]       = value; break;
            case SynthParameter.Osc1_Harmonic_02:             s.oscillator1.Harmonics[2]       = value; break;
            case SynthParameter.Osc1_Harmonic_03:             s.oscillator1.Harmonics[3]       = value; break;
            case SynthParameter.Osc1_Harmonic_04:             s.oscillator1.Harmonics[4]       = value; break;
            case SynthParameter.Osc1_Harmonic_05:             s.oscillator1.Harmonics[5]       = value; break;
            case SynthParameter.Osc1_Harmonic_06:             s.oscillator1.Harmonics[6]       = value; break;
            case SynthParameter.Osc1_Harmonic_07:             s.oscillator1.Harmonics[7]       = value; break;
            case SynthParameter.Osc1_Harmonic_08:             s.oscillator1.Harmonics[8]       = value; break;
            case SynthParameter.Osc1_Harmonic_09:             s.oscillator1.Harmonics[9]       = value; break;
            case SynthParameter.Osc1_Harmonic_10:             s.oscillator1.Harmonics[10]      = value; break;
            case SynthParameter.Osc1_Harmonic_11:             s.oscillator1.Harmonics[11]      = value; break;
            case SynthParameter.Osc1_Harmonic_12:             s.oscillator1.Harmonics[12]      = value; break;
            case SynthParameter.Osc1_Harmonic_13:             s.oscillator1.Harmonics[13]      = value; break;
            case SynthParameter.Osc1_Harmonic_14:             s.oscillator1.Harmonics[14]      = value; break;
            case SynthParameter.Osc1_Harmonic_15:             s.oscillator1.Harmonics[15]      = value; break;
            case SynthParameter.Osc1_Frequency:               s.oscillator1.Frequency          = Mathf.Max(0f, value); break;
            case SynthParameter.Osc1_Amplitude:               s.oscillator1.Amplitude          = Mathf.Clamp01(value); break;
            case SynthParameter.Osc1_Offset:                  s.oscillator1.Offset             = value; break;
            case SynthParameter.Osc2_Sine_Mix:                s.oscillator2.Sine_Mix           = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Square_Mix:              s.oscillator2.Square_Mix         = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Sawtooth_Mix:            s.oscillator2.Sawtooth_Mix       = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Triangle_Mix:            s.oscillator2.Triangle_Mix       = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Ramp_Mix:                s.oscillator2.Ramp_Mix           = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Wavetable_Mix:           s.oscillator2.Wavetable_Mix      = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Harmonic_00:             s.oscillator2.Harmonics[0]       = value; break;
            case SynthParameter.Osc2_Harmonic_01:             s.oscillator2.Harmonics[1]       = value; break;
            case SynthParameter.Osc2_Harmonic_02:             s.oscillator2.Harmonics[2]       = value; break;
            case SynthParameter.Osc2_Harmonic_03:             s.oscillator2.Harmonics[3]       = value; break;
            case SynthParameter.Osc2_Harmonic_04:             s.oscillator2.Harmonics[4]       = value; break;
            case SynthParameter.Osc2_Harmonic_05:             s.oscillator2.Harmonics[5]       = value; break;
            case SynthParameter.Osc2_Harmonic_06:             s.oscillator2.Harmonics[6]       = value; break;
            case SynthParameter.Osc2_Harmonic_07:             s.oscillator2.Harmonics[7]       = value; break;
            case SynthParameter.Osc2_Harmonic_08:             s.oscillator2.Harmonics[8]       = value; break;
            case SynthParameter.Osc2_Harmonic_09:             s.oscillator2.Harmonics[9]       = value; break;
            case SynthParameter.Osc2_Harmonic_10:             s.oscillator2.Harmonics[10]      = value; break;
            case SynthParameter.Osc2_Harmonic_11:             s.oscillator2.Harmonics[11]      = value; break;
            case SynthParameter.Osc2_Harmonic_12:             s.oscillator2.Harmonics[12]      = value; break;
            case SynthParameter.Osc2_Harmonic_13:             s.oscillator2.Harmonics[13]      = value; break;
            case SynthParameter.Osc2_Harmonic_14:             s.oscillator2.Harmonics[14]      = value; break;
            case SynthParameter.Osc2_Harmonic_15:             s.oscillator2.Harmonics[15]      = value; break;
            case SynthParameter.Osc2_Frequency:               s.oscillator2.Frequency          = Mathf.Max(0f, value); break;
            case SynthParameter.Osc2_Amplitude:               s.oscillator2.Amplitude          = Mathf.Clamp01(value); break;
            case SynthParameter.Osc2_Offset:                  s.oscillator2.Offset             = value; break;
            case SynthParameter.AudioOp_PreOp_Multiply:       s.audioOperations.PreOp_Multiply = value; break;
            case SynthParameter.AudioOp_PreOp_Add:            s.audioOperations.PreOp_Add      = value; break;
            case SynthParameter.AudioOp_PostOp_Multiply:      s.audioOperations.PostOp_Multiply= value; break;
            case SynthParameter.AudioOp_PostOp_Add:           s.audioOperations.PostOp_Add     = value; break;
            case SynthParameter.Waveshaper_Enabled:           s.waveshaper.Enabled             = value > 0.5f; break;
            case SynthParameter.Waveshaper_Shape:             s.waveshaper.Shape               = (WaveshaperSettings.Function)Mathf.Clamp(Mathf.RoundToInt(value), 0, 3); break;
            case SynthParameter.Waveshaper_Drive:             s.waveshaper.Drive               = Mathf.Max(1f, value); break;
            case SynthParameter.Waveshaper_Mix:               s.waveshaper.Mix                 = Mathf.Clamp01(value); break;
            case SynthParameter.EQ1_Enabled:                  s.eqBands[0].Enabled             = value > 0.5f; break;
            case SynthParameter.EQ1_Frequency:                s.eqBands[0].Frequency           = Mathf.Max(20f, value); break;
            case SynthParameter.EQ1_Bandwidth:                s.eqBands[0].Bandwidth           = Mathf.Clamp(value, 0.1f, 10f); break;
            case SynthParameter.EQ1_Boost_dB:                 s.eqBands[0].Boost_dB            = Mathf.Clamp(value, -24f, 24f); break;
            case SynthParameter.EQ1_Frequency_Mod:            s.eqBands[0].Frequency_Mod_Amount= value; break;
            case SynthParameter.EQ1_Bandwidth_Mod:            s.eqBands[0].Bandwidth_Mod_Amount= value; break;
            case SynthParameter.EQ1_Boost_Mod:                s.eqBands[0].Boost_Mod_Amount    = value; break;
            case SynthParameter.EQ2_Enabled:                  s.eqBands[1].Enabled             = value > 0.5f; break;
            case SynthParameter.EQ2_Frequency:                s.eqBands[1].Frequency           = Mathf.Max(20f, value); break;
            case SynthParameter.EQ2_Bandwidth:                s.eqBands[1].Bandwidth           = Mathf.Clamp(value, 0.1f, 10f); break;
            case SynthParameter.EQ2_Boost_dB:                 s.eqBands[1].Boost_dB            = Mathf.Clamp(value, -24f, 24f); break;
            case SynthParameter.EQ2_Frequency_Mod:            s.eqBands[1].Frequency_Mod_Amount= value; break;
            case SynthParameter.EQ2_Bandwidth_Mod:            s.eqBands[1].Bandwidth_Mod_Amount= value; break;
            case SynthParameter.EQ2_Boost_Mod:                s.eqBands[1].Boost_Mod_Amount    = value; break;
            case SynthParameter.EQ3_Enabled:                  s.eqBands[2].Enabled             = value > 0.5f; break;
            case SynthParameter.EQ3_Frequency:                s.eqBands[2].Frequency           = Mathf.Max(20f, value); break;
            case SynthParameter.EQ3_Bandwidth:                s.eqBands[2].Bandwidth           = Mathf.Clamp(value, 0.1f, 10f); break;
            case SynthParameter.EQ3_Boost_dB:                 s.eqBands[2].Boost_dB            = Mathf.Clamp(value, -24f, 24f); break;
            case SynthParameter.EQ3_Frequency_Mod:            s.eqBands[2].Frequency_Mod_Amount= value; break;
            case SynthParameter.EQ3_Bandwidth_Mod:            s.eqBands[2].Bandwidth_Mod_Amount= value; break;
            case SynthParameter.EQ3_Boost_Mod:                s.eqBands[2].Boost_Mod_Amount    = value; break;
            case SynthParameter.Filter1_Enabled:              s.frequencyFilters[0].Enabled    = value > 0.5f; break;
            case SynthParameter.Filter1_Type:                 s.frequencyFilters[0].Type       = (FrequencyFilterSettings.FilterType)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.Filter1_Cutoff:               s.frequencyFilters[0].Cutoff     = Mathf.Clamp(value, 20f, 20000f); break;
            case SynthParameter.Filter1_Resonance:            s.frequencyFilters[0].Resonance  = Mathf.Clamp(value, 0.707f, 20f); break;
            case SynthParameter.Filter1_Cutoff_Mod:           s.frequencyFilters[0].Cutoff_Mod_Amount     = value; break;
            case SynthParameter.Filter1_Resonance_Mod:        s.frequencyFilters[0].Resonance_Mod_Amount = value; break;
            case SynthParameter.Filter2_Enabled:              s.frequencyFilters[1].Enabled    = value > 0.5f; break;
            case SynthParameter.Filter2_Type:                 s.frequencyFilters[1].Type       = (FrequencyFilterSettings.FilterType)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.Filter2_Cutoff:               s.frequencyFilters[1].Cutoff     = Mathf.Clamp(value, 20f, 20000f); break;
            case SynthParameter.Filter2_Resonance:            s.frequencyFilters[1].Resonance  = Mathf.Clamp(value, 0.707f, 20f); break;
            case SynthParameter.Filter2_Cutoff_Mod:           s.frequencyFilters[1].Cutoff_Mod_Amount     = value; break;
            case SynthParameter.Filter2_Resonance_Mod:        s.frequencyFilters[1].Resonance_Mod_Amount = value; break;
            case SynthParameter.Filter3_Enabled:              s.frequencyFilters[2].Enabled    = value > 0.5f; break;
            case SynthParameter.Filter3_Type:                 s.frequencyFilters[2].Type       = (FrequencyFilterSettings.FilterType)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.Filter3_Cutoff:               s.frequencyFilters[2].Cutoff     = Mathf.Clamp(value, 20f, 20000f); break;
            case SynthParameter.Filter3_Resonance:            s.frequencyFilters[2].Resonance  = Mathf.Clamp(value, 0.707f, 20f); break;
            case SynthParameter.Filter3_Cutoff_Mod:           s.frequencyFilters[2].Cutoff_Mod_Amount     = value; break;
            case SynthParameter.Filter3_Resonance_Mod:        s.frequencyFilters[2].Resonance_Mod_Amount = value; break;
            case SynthParameter.Filter4_Enabled:              s.frequencyFilters[3].Enabled    = value > 0.5f; break;
            case SynthParameter.Filter4_Type:                 s.frequencyFilters[3].Type       = (FrequencyFilterSettings.FilterType)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.Filter4_Cutoff:               s.frequencyFilters[3].Cutoff     = Mathf.Clamp(value, 20f, 20000f); break;
            case SynthParameter.Filter4_Resonance:            s.frequencyFilters[3].Resonance  = Mathf.Clamp(value, 0.707f, 20f); break;
            case SynthParameter.Filter4_Cutoff_Mod:           s.frequencyFilters[3].Cutoff_Mod_Amount     = value; break;
            case SynthParameter.Filter4_Resonance_Mod:        s.frequencyFilters[3].Resonance_Mod_Amount = value; break;
            case SynthParameter.CombFilter_Enabled:           s.combFilter.Enabled             = value > 0.5f; break;
            case SynthParameter.CombFilter_Delay_ms:          s.combFilter.Delay_ms            = Mathf.Clamp(value, 1f, 100f); break;
            case SynthParameter.CombFilter_Feedback:          s.combFilter.Feedback            = Mathf.Clamp(value, -0.99f, 0.99f); break;
            case SynthParameter.CombFilter_Mix:               s.combFilter.Mix                 = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Sine_Mix:                s.lfo1.Sine_Mix                  = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Square_Mix:              s.lfo1.Square_Mix                = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Sawtooth_Mix:            s.lfo1.Sawtooth_Mix              = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Triangle_Mix:            s.lfo1.Triangle_Mix              = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Ramp_Mix:                s.lfo1.Ramp_Mix                  = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Wavetable_Mix:           s.lfo1.Wavetable_Mix             = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Harmonic_00:             s.lfo1.Harmonics[0]              = value; break;
            case SynthParameter.LFO1_Harmonic_01:             s.lfo1.Harmonics[1]              = value; break;
            case SynthParameter.LFO1_Harmonic_02:             s.lfo1.Harmonics[2]              = value; break;
            case SynthParameter.LFO1_Harmonic_03:             s.lfo1.Harmonics[3]              = value; break;
            case SynthParameter.LFO1_Harmonic_04:             s.lfo1.Harmonics[4]              = value; break;
            case SynthParameter.LFO1_Harmonic_05:             s.lfo1.Harmonics[5]              = value; break;
            case SynthParameter.LFO1_Harmonic_06:             s.lfo1.Harmonics[6]              = value; break;
            case SynthParameter.LFO1_Harmonic_07:             s.lfo1.Harmonics[7]              = value; break;
            case SynthParameter.LFO1_Harmonic_08:             s.lfo1.Harmonics[8]              = value; break;
            case SynthParameter.LFO1_Harmonic_09:             s.lfo1.Harmonics[9]              = value; break;
            case SynthParameter.LFO1_Harmonic_10:             s.lfo1.Harmonics[10]             = value; break;
            case SynthParameter.LFO1_Harmonic_11:             s.lfo1.Harmonics[11]             = value; break;
            case SynthParameter.LFO1_Harmonic_12:             s.lfo1.Harmonics[12]             = value; break;
            case SynthParameter.LFO1_Harmonic_13:             s.lfo1.Harmonics[13]             = value; break;
            case SynthParameter.LFO1_Harmonic_14:             s.lfo1.Harmonics[14]             = value; break;
            case SynthParameter.LFO1_Harmonic_15:             s.lfo1.Harmonics[15]             = value; break;
            case SynthParameter.LFO1_Frequency:               s.lfo1.Frequency                 = Mathf.Max(0f, value); break;
            case SynthParameter.LFO1_Amplitude:               s.lfo1.Amplitude                 = Mathf.Clamp01(value); break;
            case SynthParameter.LFO1_Offset:                  s.lfo1.Offset                    = value; break;
            case SynthParameter.LFO2_Sine_Mix:                s.lfo2.Sine_Mix                  = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Square_Mix:              s.lfo2.Square_Mix                = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Sawtooth_Mix:            s.lfo2.Sawtooth_Mix              = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Triangle_Mix:            s.lfo2.Triangle_Mix              = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Ramp_Mix:                s.lfo2.Ramp_Mix                  = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Wavetable_Mix:           s.lfo2.Wavetable_Mix             = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Harmonic_00:             s.lfo2.Harmonics[0]              = value; break;
            case SynthParameter.LFO2_Harmonic_01:             s.lfo2.Harmonics[1]              = value; break;
            case SynthParameter.LFO2_Harmonic_02:             s.lfo2.Harmonics[2]              = value; break;
            case SynthParameter.LFO2_Harmonic_03:             s.lfo2.Harmonics[3]              = value; break;
            case SynthParameter.LFO2_Harmonic_04:             s.lfo2.Harmonics[4]              = value; break;
            case SynthParameter.LFO2_Harmonic_05:             s.lfo2.Harmonics[5]              = value; break;
            case SynthParameter.LFO2_Harmonic_06:             s.lfo2.Harmonics[6]              = value; break;
            case SynthParameter.LFO2_Harmonic_07:             s.lfo2.Harmonics[7]              = value; break;
            case SynthParameter.LFO2_Harmonic_08:             s.lfo2.Harmonics[8]              = value; break;
            case SynthParameter.LFO2_Harmonic_09:             s.lfo2.Harmonics[9]              = value; break;
            case SynthParameter.LFO2_Harmonic_10:             s.lfo2.Harmonics[10]             = value; break;
            case SynthParameter.LFO2_Harmonic_11:             s.lfo2.Harmonics[11]             = value; break;
            case SynthParameter.LFO2_Harmonic_12:             s.lfo2.Harmonics[12]             = value; break;
            case SynthParameter.LFO2_Harmonic_13:             s.lfo2.Harmonics[13]             = value; break;
            case SynthParameter.LFO2_Harmonic_14:             s.lfo2.Harmonics[14]             = value; break;
            case SynthParameter.LFO2_Harmonic_15:             s.lfo2.Harmonics[15]             = value; break;
            case SynthParameter.LFO2_Frequency:               s.lfo2.Frequency                 = Mathf.Max(0f, value); break;
            case SynthParameter.LFO2_Amplitude:               s.lfo2.Amplitude                 = Mathf.Clamp01(value); break;
            case SynthParameter.LFO2_Offset:                  s.lfo2.Offset                    = value; break;
            case SynthParameter.LFO_Op_PreOp_Multiply:        s.lfoOperations.PreOp_Multiply   = value; break;
            case SynthParameter.LFO_Op_PreOp_Add:             s.lfoOperations.PreOp_Add        = value; break;
            case SynthParameter.LFO_Op_PostOp_Multiply:       s.lfoOperations.PostOp_Multiply  = value; break;
            case SynthParameter.LFO_Op_PostOp_Add:            s.lfoOperations.PostOp_Add       = value; break;
            case SynthParameter.ModMatrix1_Enabled:           s.modMatrix[0].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix1_Source:            s.modMatrix[0].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix1_Destination:       s.modMatrix[0].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix1_Amount:            s.modMatrix[0].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix2_Enabled:           s.modMatrix[1].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix2_Source:            s.modMatrix[1].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix2_Destination:       s.modMatrix[1].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix2_Amount:            s.modMatrix[1].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix3_Enabled:           s.modMatrix[2].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix3_Source:            s.modMatrix[2].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix3_Destination:       s.modMatrix[2].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix3_Amount:            s.modMatrix[2].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix4_Enabled:           s.modMatrix[3].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix4_Source:            s.modMatrix[3].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix4_Destination:       s.modMatrix[3].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix4_Amount:            s.modMatrix[3].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix5_Enabled:           s.modMatrix[4].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix5_Source:            s.modMatrix[4].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix5_Destination:       s.modMatrix[4].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix5_Amount:            s.modMatrix[4].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix6_Enabled:           s.modMatrix[5].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix6_Source:            s.modMatrix[5].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix6_Destination:       s.modMatrix[5].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix6_Amount:            s.modMatrix[5].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix7_Enabled:           s.modMatrix[6].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix7_Source:            s.modMatrix[6].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix7_Destination:       s.modMatrix[6].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix7_Amount:            s.modMatrix[6].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.ModMatrix8_Enabled:           s.modMatrix[7].Enabled           = value > 0.5f; break;
            case SynthParameter.ModMatrix8_Source:            s.modMatrix[7].Source            = (ModSource)Mathf.Clamp(Mathf.RoundToInt(value), 0, 2); break;
            case SynthParameter.ModMatrix8_Destination:       s.modMatrix[7].Destination       = (ModDestination)Mathf.Clamp(Mathf.RoundToInt(value), 0, 31); break;
            case SynthParameter.ModMatrix8_Amount:            s.modMatrix[7].Amount            = Mathf.Clamp(value, -1f, 1f); break;
            case SynthParameter.Master_Limiter_Threshold_dB:   s.Limiter_Threshold_dB           = Mathf.Clamp(value, -20f, 0f); break;
            case SynthParameter.Master_Limiter_Attack_ms:      s.Limiter_Attack_ms              = Mathf.Clamp(value, 0.1f, 50f); break;
            case SynthParameter.Master_Limiter_Release_ms:     s.Limiter_Release_ms             = Mathf.Clamp(value, 5f, 500f); break;
        }
    }

    private float GetLooperValue(SynthNoteLooper looper, LooperParameter param)
    {
        switch (param)
        {
            case LooperParameter.AttackTime:     return looper.attackTime;
            case LooperParameter.DecayTime:      return looper.decayTime;
            case LooperParameter.SustainLevel:   return looper.sustainLevel;
            case LooperParameter.ReleaseTime:    return looper.releaseTime;
            case LooperParameter.NoteDuration:   return looper.noteDuration;
            case LooperParameter.WaitTime:       return looper.waitTime;
            default:                             return 0f;
        }
    }

    private void SetLooperValue(SynthNoteLooper looper, LooperParameter param, float value)
    {
        switch (param)
        {
            case LooperParameter.AttackTime:     looper.attackTime   = Mathf.Max(0f, value); break;
            case LooperParameter.DecayTime:      looper.decayTime    = Mathf.Max(0f, value); break;
            case LooperParameter.SustainLevel:   looper.sustainLevel = Mathf.Clamp01(value); break;
            case LooperParameter.ReleaseTime:    looper.releaseTime  = Mathf.Max(0f, value); break;
            case LooperParameter.NoteDuration:   looper.noteDuration = Mathf.Max(0f, value); break;
            case LooperParameter.WaitTime:       looper.waitTime     = Mathf.Max(0f, value); break;
        }
    }

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(PhoneticGenomeSynthController))]
public class PhoneticGenomeSynthControllerEditor : Editor
{
    private SerializedProperty rtmlCoreProp;
    private SerializedProperty genomeLoggerProp;
    private SerializedProperty synthProp;
    private SerializedProperty noteLooperProp;
    private SerializedProperty inputsProp;
    private SerializedProperty outputsProp;
    private SerializedProperty looperOutputsProp;
    private SerializedProperty recordKeyProp;
    private SerializedProperty trainKeyProp;
    private SerializedProperty predictKeyProp;

    void OnEnable()
    {
        rtmlCoreProp         = serializedObject.FindProperty("rtmlCore");
        genomeLoggerProp     = serializedObject.FindProperty("genomeLogger");
        synthProp            = serializedObject.FindProperty("synth");
        noteLooperProp       = serializedObject.FindProperty("noteLooper");
        inputsProp           = serializedObject.FindProperty("selectedGenomeInputs");
        outputsProp          = serializedObject.FindProperty("selectedSynthOutputs");
        looperOutputsProp    = serializedObject.FindProperty("selectedLooperOutputs");
        recordKeyProp        = serializedObject.FindProperty("recordKey");
        trainKeyProp         = serializedObject.FindProperty("trainKey");
        predictKeyProp       = serializedObject.FindProperty("predictKey");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var controller = (PhoneticGenomeSynthController)target;

        EditorGUILayout.LabelField("Core Components", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rtmlCoreProp);
        EditorGUILayout.PropertyField(genomeLoggerProp);
        EditorGUILayout.PropertyField(synthProp);
        EditorGUILayout.PropertyField(noteLooperProp);
        
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Input Parameters (Genome)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select which genome parameters will be used as input to the model. The order matters.", MessageType.Info);
        EditorGUILayout.PropertyField(inputsProp, true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add All Inputs"))
        {
            var allParams = System.Enum.GetValues(typeof(GenomeParameter))
                                 .Cast<GenomeParameter>()
                                 .Where(p => p != GenomeParameter.None)
                                 .ToList();
            controller.selectedGenomeInputs.Clear();
            controller.selectedGenomeInputs.AddRange(allParams);
            EditorUtility.SetDirty(controller);
        }
        if (GUILayout.Button("Clear All Inputs"))
        {
            controller.selectedGenomeInputs.Clear();
            EditorUtility.SetDirty(controller);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Current Input Count: " + controller.selectedGenomeInputs.Count);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Output Parameters (Synth)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select which synth parameters the model will control. The order matters.", MessageType.Info);
        EditorGUILayout.PropertyField(outputsProp, true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add All Outputs"))
        {
            var allParams = System.Enum.GetValues(typeof(SynthParameter))
                                 .Cast<SynthParameter>()
                                 .Where(p => p != SynthParameter.None)
                                 .ToList();
            controller.selectedSynthOutputs.Clear();
            controller.selectedSynthOutputs.AddRange(allParams);
            EditorUtility.SetDirty(controller);
        }
        if (GUILayout.Button("Clear All Outputs"))
        {
            controller.selectedSynthOutputs.Clear();
            EditorUtility.SetDirty(controller);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Current Output Count: " + controller.selectedSynthOutputs.Count);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Output Parameters (Note Looper)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select which note looper parameters the model will control. The order matters.", MessageType.Info);
        EditorGUILayout.PropertyField(looperOutputsProp, true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add All Looper Outputs"))
        {
            var allParams = System.Enum.GetValues(typeof(LooperParameter))
                                 .Cast<LooperParameter>()
                                 .Where(p => p != LooperParameter.None)
                                 .ToList();
            controller.selectedLooperOutputs.Clear();
            controller.selectedLooperOutputs.AddRange(allParams);
            EditorUtility.SetDirty(controller);
        }
        if (GUILayout.Button("Clear All Looper Outputs"))
        {
            controller.selectedLooperOutputs.Clear();
            EditorUtility.SetDirty(controller);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Current Looper Output Count: " + controller.selectedLooperOutputs.Count);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(recordKeyProp);
        EditorGUILayout.PropertyField(trainKeyProp);
        EditorGUILayout.PropertyField(predictKeyProp);

        if (serializedObject.ApplyModifiedProperties())
        {
            controller.UpdateModelDimensions();
        }
        
        if (Event.current.type == EventType.Layout)
        {
            controller.UpdateModelDimensions();
        }
    }
}
#endif
