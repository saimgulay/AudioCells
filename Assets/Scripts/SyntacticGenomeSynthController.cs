// SyntacticGenomeSynthController.cs
// Final, fully implemented and syntactically corrected version.
// Controls parameters of SynthMelodyLooper based on E. coli genome.

using UnityEngine;
using RTMLToolKit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

#region Parameter Enums

// Do not redefine GenomeParameter here—use the existing one in your project.

public enum MelodyParameter
{
    None,
    AttackTime,
    DecayTime,
    SustainLevel,
    ReleaseTime,
    Semitone0,
    Semitone1,
    Semitone2,
    Semitone3,
    BaseFrequency,
    InterNotePause,
    InterMelodyPause,
    Osc2Multiplier
}

#endregion

[ExecuteAlways]
public class SyntacticGenomeSynthController : MonoBehaviour
{
    [Header("RTML Settings")]
    [Tooltip("Reference to your RTMLRunner's RTMLCore component.")]
    public RTMLCore rtmlCore;

    [Header("Data Sources")]
    [Tooltip("Provides the current sampled E. coli genome.")]
    public EColiGenomeLogger genomeLogger;

    [Tooltip("The SynthMelodyLooper whose parameters will be driven.")]
    public SynthMelodyLooper melodyLooper;

    [Header("Parameter Selection")]
    [Tooltip("Choose which Genome parameters to use as inputs for the model.")]
    public List<GenomeParameter> selectedGenomeInputs = new List<GenomeParameter>();

    [Tooltip("Choose which melody parameters the model should learn to control.")]
    public List<MelodyParameter> selectedMelodyOutputs = new List<MelodyParameter>();

    [Header("Keyboard Shortcuts")]
    public KeyCode recordKey   = KeyCode.R;
    public KeyCode trainKey    = KeyCode.T;
    public KeyCode predictKey  = KeyCode.P;

    // Ensure RTMLCore dimensions stay in sync
    void OnValidate()
    {
        UpdateModelDimensions();
    }

    public void UpdateModelDimensions()
    {
        if (rtmlCore != null)
        {
            rtmlCore.inputSize  = selectedGenomeInputs.Count;
            rtmlCore.outputSize = selectedMelodyOutputs.Count;
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
        if (rtmlCore == null || genomeLogger == null || melodyLooper == null) return;

        if (Input.GetKeyDown(recordKey))   HandleRecord();
        if (Input.GetKeyDown(trainKey))    HandleTrain();
        if (Input.GetKeyDown(predictKey))  HandlePredictToggle();

        if (rtmlCore.enableRun) RunPrediction();
    }

    #region Core ML Methods

    private void HandleRecord()
    {
        // Guard against stale dimensions
        UpdateModelDimensions();

        var genomeVec = BuildInputVector();
        var outputVec = BuildOutputVector();

        Debug.Log($"[GenomeSynth] Recording with in:{genomeVec.Length}, out:{outputVec.Length} (core expecting in:{rtmlCore.inputSize}, out:{rtmlCore.outputSize})");

        if (genomeVec.Length == 0 || outputVec.Length == 0)
        {
            Debug.LogWarning("[GenomeSynth] Cannot record sample. Input or output lists are empty.");
            return;
        }

        rtmlCore.RecordSample(genomeVec, outputVec);
        Debug.Log($"[GenomeSynth] Recorded sample. (Inputs: {genomeVec.Length}, Outputs: {outputVec.Length})");
    }

    private void HandleTrain()
    {
        rtmlCore.TrainModel();
        Debug.Log("[GenomeSynth] Model training started.");
    }

    private void HandlePredictToggle()
    {
        rtmlCore.enableRun = !rtmlCore.enableRun;
        Debug.Log($"[GenomeSynth] Live control toggled: {rtmlCore.enableRun}");
    }

    private void RunPrediction()
    {
        var genomeVec = BuildInputVector();
        if (genomeVec.Length == 0) return;

        float[] pred = rtmlCore.PredictSample(genomeVec);

        if (pred != null && pred.Length > 0 && float.IsNaN(pred[0]))
        {
            Debug.LogError("[GenomeSynth] Prediction NaN! Check normalisation and learning rate.");
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
            v[i] = GetGenomeValue(g, selectedGenomeInputs[i]);
        return v;
    }

    private float[] BuildOutputVector()
    {
        if (melodyLooper == null) return new float[selectedMelodyOutputs.Count];

        float[] v = new float[selectedMelodyOutputs.Count];
        for (int i = 0; i < selectedMelodyOutputs.Count; i++)
            v[i] = GetMelodyValue(melodyLooper, selectedMelodyOutputs[i]);
        return v;
    }

    private void ApplyOutputVector(float[] data)
    {
        if (data == null || data.Length != selectedMelodyOutputs.Count) return;

        for (int i = 0; i < selectedMelodyOutputs.Count; i++)
            SetMelodyValue(melodyLooper, selectedMelodyOutputs[i], data[i]);

        melodyLooper.ApplyTetrachord();
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
            case GenomeParameter.RunSpeedFactor:       return Normalise(g.runSpeedFactor, 0f, 5f);
            case GenomeParameter.TumbleSensitivity:    return Normalise(g.tumbleSensitivity, 0f, 5f);
            case GenomeParameter.MetabolismRate:       return Normalise(g.metabolismRate, 0f, 5f);
            case GenomeParameter.ReproductionThreshold:return Normalise(g.reproductionThreshold, 5f, 20f);
            case GenomeParameter.OptimalTemperature:   return Normalise(g.optimalTemperature, 0f, 40f);
            case GenomeParameter.TemperatureSensitivity:return Normalise(g.temperatureSensitivity, 0f, 1f);
            case GenomeParameter.OptimalPH:            return Normalise(g.optimalPH, 0f, 14f);
            case GenomeParameter.PHSensitivity:        return Normalise(g.pHSensitivity, 0f, 1f);
            case GenomeParameter.ToxinResistance:      return Normalise(g.toxinResistance, 0f, 1f);
            case GenomeParameter.UVResistance:         return g.uvResistance;
            case GenomeParameter.NutrientEfficiencyA:  return Normalise(g.nutrientEfficiencyA, 0f, 2f);
            case GenomeParameter.NutrientEfficiencyB:  return Normalise(g.nutrientEfficiencyB, 0f, 2f);
            case GenomeParameter.NutrientEfficiencyC:  return Normalise(g.nutrientEfficiencyC, 0f, 2f);
            case GenomeParameter.NutrientEfficiencyD:  return Normalise(g.nutrientEfficiencyD, 0f, 2f);
            case GenomeParameter.ChemotaxisMemoryLength:return Normalise(g.chemotaxisMemoryLength, 1f, 20f);
            case GenomeParameter.BaseTumbleRate:       return Normalise(g.baseTumbleRate, 0f, 1f);
            case GenomeParameter.TumbleSlopeSensitivity:return Normalise(g.tumbleSlopeSensitivity, 0f, 5f);
            case GenomeParameter.TumbleAngleRange:     return Normalise(g.tumbleAngleRange, 10f, 180f);
            case GenomeParameter.ExplorationTendency:  return g.explorationTendency;
            case GenomeParameter.GradientTolerance:    return Normalise(g.gradientTolerance, 0f, 0.1f);
            case GenomeParameter.DecisionNoise:        return Normalise(g.decisionNoise, 0f, 0.1f);
            case GenomeParameter.TemperaturePlasticity:return g.temperaturePlasticity;
            case GenomeParameter.PHPlasticity:         return g.pHPlasticity;
            case GenomeParameter.StarvationTolerance:  return Normalise(g.starvationTolerance, 0f, 10f);
            case GenomeParameter.DeathDelayBias:       return Normalise(g.deathDelayBias, 0f, 1f);
            case GenomeParameter.RestBehaviour:        return g.restBehavior;
            case GenomeParameter.NutrientDiscrimination:return Normalise(g.nutrientDiscrimination, 0f, 1f);
            case GenomeParameter.DeadCellPreference:   return g.deadCellPreference;
            case GenomeParameter.ConjugationAggressiveness:return Normalise(g.conjugationAggressiveness, 0f, 1f);
            case GenomeParameter.GeneticStability:     return g.geneticStability;
            case GenomeParameter.BaseRunSpeed:         return Normalise(g.baseRunSpeed, 0f, 10f);
            case GenomeParameter.TumbleDuration:       return Normalise(g.tumbleDuration, 0f, 5f);
            case GenomeParameter.SensorRadius:         return Normalise(g.sensorRadius, 0f, 10f);
            case GenomeParameter.DivisionDelay:        return Normalise(g.divisionDelay, 0f, 5f);
            case GenomeParameter.SelfPreservation:     return Normalise(g.selfPreservation, 0f, 1f);
            case GenomeParameter.DormancyThreshold:    return Normalise(g.dormancyThreshold, 0f, 5f);
            case GenomeParameter.WakeUpEnergyCost:     return Normalise(g.wakeUpEnergyCost, 0f, 5f);
            case GenomeParameter.ToxinProductionRate:  return Normalise(g.toxinProductionRate, 0f, 1f);
            case GenomeParameter.ToxinPotency:         return Normalise(g.toxinPotency, 0f, 1f);
            case GenomeParameter.BiofilmTendency:      return Normalise(g.biofilmTendency, 0f, 1f);
            case GenomeParameter.BiofilmMatrixCost:    return Normalise(g.biofilmMatrixCost, 0f, 0.2f);
            case GenomeParameter.QuorumSensingThreshold:return Normalise(g.quorumSensingThreshold, 0f, 20f);
            case GenomeParameter.QuorumToxinBoost:     return Normalise(g.quorumToxinBoost,    1f, 5f);
            case GenomeParameter.KinRecognitionFidelity:return Normalise(g.kinRecognitionFidelity,0f,1f);
            case GenomeParameter.KinCooperationBonus:  return Normalise(g.kinCooperationBonus,  1f, 5f);
            case GenomeParameter.StressInducedMutabilityFactor:return Normalise(g.stressInducedMutabilityFactor,1f,5f);
            case GenomeParameter.PlasmidCompatibilityThreshold:return Normalise(g.plasmidCompatibilityThreshold,0f,5f);
            default: return 0f;
        }
    }

    private float GetMelodyValue(SynthMelodyLooper looper, MelodyParameter param)
    {
        switch (param)
        {
            case MelodyParameter.AttackTime:       return looper.attackTime;
            case MelodyParameter.DecayTime:        return looper.decayTime;
            case MelodyParameter.SustainLevel:     return looper.sustainLevel;
            case MelodyParameter.ReleaseTime:      return looper.releaseTime;
            case MelodyParameter.Semitone0:        return looper.semitone0;
            case MelodyParameter.Semitone1:        return looper.semitone1;
            case MelodyParameter.Semitone2:        return looper.semitone2;
            case MelodyParameter.Semitone3:        return looper.semitone3;
            case MelodyParameter.BaseFrequency:    return looper.baseFrequency;
            case MelodyParameter.InterNotePause:   return looper.interNotePause;
            case MelodyParameter.InterMelodyPause: return looper.interMelodyPause;
            case MelodyParameter.Osc2Multiplier:   return looper.osc2Multiplier;
            default: return 0f;
        }
    }

    private void SetMelodyValue(SynthMelodyLooper looper, MelodyParameter param, float value)
    {
        switch (param)
        {
            case MelodyParameter.AttackTime:
                looper.attackTime = Mathf.Max(0f, value);
                break;
            case MelodyParameter.DecayTime:
                looper.decayTime = Mathf.Max(0f, value);
                break;
            case MelodyParameter.SustainLevel:
                looper.sustainLevel = Mathf.Clamp01(value);
                break;
            case MelodyParameter.ReleaseTime:
                looper.releaseTime = Mathf.Max(0f, value);
                break;
            case MelodyParameter.Semitone0:
                looper.semitone0 = Mathf.Clamp(Mathf.RoundToInt(value), 0, 11);
                break;
            case MelodyParameter.Semitone1:
                looper.semitone1 = Mathf.Clamp(Mathf.RoundToInt(value), 0, 11);
                break;
            case MelodyParameter.Semitone2:
                looper.semitone2 = Mathf.Clamp(Mathf.RoundToInt(value), 0, 11);
                break;
            case MelodyParameter.Semitone3:
                looper.semitone3 = Mathf.Clamp(Mathf.RoundToInt(value), 0, 11);
                break;
            case MelodyParameter.BaseFrequency:
                looper.baseFrequency = Mathf.Max(0f, value);
                break;
            case MelodyParameter.InterNotePause:
                looper.interNotePause = Mathf.Max(0f, value);
                break;
            case MelodyParameter.InterMelodyPause:
                looper.interMelodyPause = Mathf.Max(0f, value);
                break;
            case MelodyParameter.Osc2Multiplier:
                looper.osc2Multiplier = Mathf.Max(0f, value);
                break;
        }
    }

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(SyntacticGenomeSynthController))]
public class SyntacticGenomeSynthControllerEditor : Editor
{
    private SerializedProperty rtmlCoreProp;
    private SerializedProperty genomeLoggerProp;
    private SerializedProperty melodyLooperProp;
    private SerializedProperty inputsProp;
    private SerializedProperty melodyOutputsProp;
    private SerializedProperty recordKeyProp;
    private SerializedProperty trainKeyProp;
    private SerializedProperty predictKeyProp;

    void OnEnable()
    {
        rtmlCoreProp        = serializedObject.FindProperty("rtmlCore");
        genomeLoggerProp    = serializedObject.FindProperty("genomeLogger");
        melodyLooperProp    = serializedObject.FindProperty("melodyLooper");
        inputsProp          = serializedObject.FindProperty("selectedGenomeInputs");
        melodyOutputsProp   = serializedObject.FindProperty("selectedMelodyOutputs");
        recordKeyProp       = serializedObject.FindProperty("recordKey");
        trainKeyProp        = serializedObject.FindProperty("trainKey");
        predictKeyProp      = serializedObject.FindProperty("predictKey");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var controller = (SyntacticGenomeSynthController)target;

        EditorGUILayout.LabelField("Core Components", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rtmlCoreProp);
        EditorGUILayout.PropertyField(genomeLoggerProp);
        EditorGUILayout.PropertyField(melodyLooperProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Input Parameters (Genome)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select which genome parameters will be used as input. The order matters.", MessageType.Info);
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
        EditorGUILayout.LabelField("Output Parameters (Melody)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select which melody parameters will be controlled. The order matters.", MessageType.Info);
        EditorGUILayout.PropertyField(melodyOutputsProp, true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add All Outputs"))
        {
            var allParams = System.Enum.GetValues(typeof(MelodyParameter))
                                       .Cast<MelodyParameter>()
                                       .Where(p => p != MelodyParameter.None)
                                       .ToList();
            controller.selectedMelodyOutputs.Clear();
            controller.selectedMelodyOutputs.AddRange(allParams);
            EditorUtility.SetDirty(controller);
        }
        if (GUILayout.Button("Clear All Outputs"))
        {
            controller.selectedMelodyOutputs.Clear();
            EditorUtility.SetDirty(controller);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Current Output Count: " + controller.selectedMelodyOutputs.Count);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(recordKeyProp);
        EditorGUILayout.PropertyField(trainKeyProp);
        EditorGUILayout.PropertyField(predictKeyProp);

        if (serializedObject.ApplyModifiedProperties())
            controller.UpdateModelDimensions();

        if (Event.current.type == EventType.Layout)
            controller.UpdateModelDimensions();
    }
}
#endif
