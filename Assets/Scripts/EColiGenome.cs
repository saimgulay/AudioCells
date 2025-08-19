using UnityEngine;

[CreateAssetMenu(menuName = "EColi/EColi Genome")]
public class EColiGenome : ScriptableObject
{
    [Header("Basic Metabolism")]
    public float runSpeedFactor = 1f;
    public float tumbleSensitivity = 1f;
    public float metabolismRate = 1f;
    public float reproductionThreshold = 10f;
    public float optimalTemperature = 20f;
    public float temperatureSensitivity = 0.1f;
    public float optimalPH = 7f;
    public float pHSensitivity = 0.1f;
    [Range(0f, 1f)] public float toxinResistance = 0.1f;
    [Range(0f, 1f)] public float uvResistance = 0.05f;

    [Header("Feeding Efficiency")]
    public float nutrientEfficiencyA = 1f;
    public float nutrientEfficiencyB = 1f;
    public float nutrientEfficiencyC = 1f;
    public float nutrientEfficiencyD = 1f;

    [Header("Chemotaxis Strategy")]
    public int chemotaxisMemoryLength = 5;
    public float baseTumbleRate = 0.5f;
    public float tumbleSlopeSensitivity = 1f;
    public float tumbleAngleRange = 45f;
    [Range(0f, 1f)] public float explorationTendency = 0.2f;
    public float gradientTolerance = 0.01f;
    public float decisionNoise = 0.01f;

    [Header("Environmental Plasticity")]
    [Range(0f, 1f)] public float temperaturePlasticity = 0.1f;
    [Range(0f, 1f)] public float pHPlasticity = 0.1f;

    [Header("Survival & Rest")]
    public float starvationTolerance = 2f;
    public float deathDelayBias = 0.1f;
    [Range(0f, 1f)] public float restBehavior = 0.1f;

    [Header("Feeding Preference")]
    public float nutrientDiscrimination = 0.1f;
    [Range(0f, 1f)] public float deadCellPreference = 0.1f;

    [Header("Gene Transfer")]
    public float conjugationAggressiveness = 0.1f;
    [Range(0f, 1f)] public float geneticStability = 0.9f;

    [Header("Agent-Specific Parameters")]
    public float baseRunSpeed = 2f;
    public float tumbleDuration = 1f;

    [Header("Mutation Settings")]
    [Tooltip("Global multiplier for all mutationRanges")]
    public float generalMutationRate = 0.01f;
    [Tooltip("Chance that a dividing cell dies from lethal load")]
    [Range(0f, 1f)] public float lethalMutationRate = 0.005f;
    public MutationRates mutationRates;

    [Header("New Sensor/Movement Genes")]
    public float sensorRadius = 1f;

    [Header("New Behavior-Strategy Genes")]
    public float divisionDelay = 1f;
    public float selfPreservation = 0.1f;

    [Header("Survival & Social Genes")]
    public float dormancyThreshold = 1.5f;
    public float wakeUpEnergyCost = 1f;
    public float toxinProductionRate = 0f;
    public float toxinPotency = 0.5f;
    public float biofilmTendency = 0.1f;
    public float biofilmMatrixCost = 0.05f;
    public float quorumSensingThreshold = 5f;
    public float quorumToxinBoost = 2f;
    public float kinRecognitionFidelity = 0.5f;
    public float kinCooperationBonus = 1.5f;
    public float stressInducedMutabilityFactor = 1.0f;
    public float plasmidCompatibilityThreshold = 2.0f;
}

[System.Serializable]
public struct MutationRates
{
    [Header("Basic Metabolism")]
    public float runSpeedFactor;
    public float tumbleSensitivity;
    public float metabolismRate;
    public float reproductionThreshold;
    public float optimalTemperature;
    public float temperatureSensitivity;
    public float optimalPH;
    public float pHSensitivity;
    public float toxinResistance;
    [Range(0f, 20f)] public float uvResistance;

    [Header("Feeding Efficiency")]
    public float nutrientEfficiencyA;
    public float nutrientEfficiencyB;
    public float nutrientEfficiencyC;
    public float nutrientEfficiencyD;

    [Header("Chemotaxis Strategy")]
    public float chemotaxisMemoryLength;
    public float baseTumbleRate;
    public float tumbleSlopeSensitivity;
    public float tumbleAngleRange;
    [Range(0f, 1f)] public float explorationTendency;
    public float gradientTolerance;
    public float decisionNoise;

    [Header("Environmental Plasticity")]
    public float temperaturePlasticity;
    public float pHPlasticity;

    [Header("Survival & Rest")]
    public float starvationTolerance;
    public float deathDelayBias;
    public float restBehavior;

    [Header("Feeding Preference")]
    public float nutrientDiscrimination;
    [Range(0f, 1f)] public float deadCellPreference;

    [Header("Gene Transfer")]
    public float conjugationAggressiveness;
    [Range(0f, 1f)] public float geneticStability;

    [Header("Agent-Specific Parameters")]
    public float baseRunSpeed;
    public float tumbleDuration;

    [Header("New Sensor/Movement Genes")]
    public float sensorRadius;

    [Header("New Behavior-Strategy Genes")]
    public float divisionDelay;
    public float selfPreservation;

    [Header("Survival & Social Genes")]
    public float dormancyThreshold;
    public float wakeUpEnergyCost;
    public float toxinProductionRate;
    public float toxinPotency;
    public float biofilmTendency;
    public float biofilmMatrixCost;
    public float quorumSensingThreshold;
    public float quorumToxinBoost;
    public float kinRecognitionFidelity;
    public float kinCooperationBonus;
    public float stressInducedMutabilityFactor;
    public float plasmidCompatibilityThreshold;
}
