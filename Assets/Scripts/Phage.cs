// Assets/Scripts/Phage.cs
using UnityEngine;

/// <summary>
/// Represents a phage (virus) that can transfer genetic material from one EColiAgent to another.
/// Now uses the EColiGenome ScriptableObject for all genome fields.
/// </summary>
public class Phage : MonoBehaviour
{
    [HideInInspector] public EColiGenome storedGenome;
    [HideInInspector] public float lifetime = 5f;
    [HideInInspector] public EColiAgent creator;

    private float lifeTimer;

    void Update()
    {
        // scale time if EnvironmentManager exists
        float timeScale = EnvironmentManager.Instance != null
            ? EnvironmentManager.Instance.timeScale
            : 1f;
        lifeTimer += Time.deltaTime * timeScale;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponent<EColiAgent>();
        if (agent == null ||
            agent == creator ||
            agent.state == EColiAgent.State.Dead ||
            agent.state == EColiAgent.State.Dormant)
        {
            return;
        }

        // 50% chance to copy each gene from storedGenome into the agent
        if (Random.value < 0.5f) agent.genome.runSpeedFactor           = storedGenome.runSpeedFactor;
        if (Random.value < 0.5f) agent.genome.tumbleSensitivity       = storedGenome.tumbleSensitivity;
        if (Random.value < 0.5f) agent.genome.metabolismRate          = storedGenome.metabolismRate;
        if (Random.value < 0.5f) agent.genome.reproductionThreshold   = storedGenome.reproductionThreshold;
        if (Random.value < 0.5f) agent.genome.optimalTemperature      = storedGenome.optimalTemperature;
        if (Random.value < 0.5f) agent.genome.temperatureSensitivity = storedGenome.temperatureSensitivity;
        if (Random.value < 0.5f) agent.genome.optimalPH               = storedGenome.optimalPH;
        if (Random.value < 0.5f) agent.genome.pHSensitivity           = storedGenome.pHSensitivity;
        if (Random.value < 0.5f) agent.genome.toxinResistance         = storedGenome.toxinResistance;
        if (Random.value < 0.5f) agent.genome.uvResistance            = storedGenome.uvResistance;

        // Feeding Efficiency
        if (Random.value < 0.5f) agent.genome.nutrientEfficiencyA = storedGenome.nutrientEfficiencyA;
        if (Random.value < 0.5f) agent.genome.nutrientEfficiencyB = storedGenome.nutrientEfficiencyB;
        if (Random.value < 0.5f) agent.genome.nutrientEfficiencyC = storedGenome.nutrientEfficiencyC;
        if (Random.value < 0.5f) agent.genome.nutrientEfficiencyD = storedGenome.nutrientEfficiencyD;

        // Social & Survival Genes
        if (Random.value < 0.5f) agent.genome.dormancyThreshold           = storedGenome.dormancyThreshold;
        if (Random.value < 0.5f) agent.genome.wakeUpEnergyCost           = storedGenome.wakeUpEnergyCost;
        if (Random.value < 0.5f) agent.genome.toxinProductionRate        = storedGenome.toxinProductionRate;
        if (Random.value < 0.5f) agent.genome.toxinPotency               = storedGenome.toxinPotency;
        if (Random.value < 0.5f) agent.genome.biofilmTendency            = storedGenome.biofilmTendency;
        if (Random.value < 0.5f) agent.genome.biofilmMatrixCost          = storedGenome.biofilmMatrixCost;
        if (Random.value < 0.5f) agent.genome.quorumSensingThreshold     = storedGenome.quorumSensingThreshold;
        if (Random.value < 0.5f) agent.genome.quorumToxinBoost           = storedGenome.quorumToxinBoost;
        if (Random.value < 0.5f) agent.genome.kinRecognitionFidelity     = storedGenome.kinRecognitionFidelity;
        if (Random.value < 0.5f) agent.genome.kinCooperationBonus        = storedGenome.kinCooperationBonus;
        if (Random.value < 0.5f) agent.genome.stressInducedMutabilityFactor = storedGenome.stressInducedMutabilityFactor;
        if (Random.value < 0.5f) agent.genome.plasmidCompatibilityThreshold = storedGenome.plasmidCompatibilityThreshold;

        // Re-assign the genome reference (not strictly necessary since we're modifying the same object):
        // agent.genome = agent.genome;

        Destroy(gameObject);
    }
}
