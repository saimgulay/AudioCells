using UnityEngine;
using System.Linq;

/// <summary>
/// Calculates an “alien score” for the E. coli cell currently shown by the genome logger,
/// by comparing its genotype (ideal tolerances) to its actual environment.
/// A perfectly adapted cell scores 0; a completely out‐of‐tolerance cell scores 1.
/// 
/// Now uses explicit biological tolerance parameters:
///   - temperatureTolerance: the °C deviation that yields alienDev = 1
///   - pHTolerance: the pH units deviation that yields alienDev = 1
///   - uvTolerance: the UV intensity deviation that yields alienDev = 1
///   - toxinTolerance: the toxin strength deviation that yields alienDev = 1
/// 
/// Dev = |actual – ideal| / tolerance
/// Clamped to [0,1], then averaged.
/// </summary>
[ExecuteAlways]
public class AlienScoreCalculator : MonoBehaviour
{
    [Header("Training Mode")]
    [Tooltip("When checked, alienScore is editable manually and dynamic calc is skipped.")]
    public bool trainingMode = false;

    [Header("References")]
    public EColiGenomeLogger genomeLogger;
    public EnvironmentManager environmentManager;
    public MIDIManager midiManager;

    [Header("Biological Tolerances (deviation → score=1)")]
    [Tooltip("°C deviation from optimal that yields full alien score")]
    public float temperatureTolerance = 17f;  // e.g. E. coli grows ~20–55°C (≈37±17)
    [Tooltip("pH units deviation from optimal that yields full alien score")]
    public float pHTolerance = 2f;            // e.g. pH 5–9 around optimum 7
    [Tooltip("UV intensity deviation (0–?) that yields full alien score")]
    public float uvTolerance = 1f;            // adjust to your UV scale
    [Tooltip("Toxin strength deviation that yields full alien score")]
    public float toxinTolerance = 1f;         // adjust to your toxin scale

    [Header("Result")]
    [Range(0f, 1f)]
    public float alienScore = 0f;

    void Update()
    {
        if (trainingMode) return;
        if (genomeLogger == null || environmentManager == null || midiManager == null) return;

        var genome = genomeLogger.currentGenome;
        if (genome == null)
        {
            alienScore = 0f;
            return;
        }

        var agent = EColiAgent.Agents.FirstOrDefault(a => a.genome == genome);
        if (agent == null)
        {
            alienScore = 0f;
            return;
        }

        var sample = environmentManager.GetEnvironmentAtPoint(agent.transform.position);

        // 1) Temperature dev
        float tempDev = Mathf.Abs(sample.temperature - genome.optimalTemperature)
                      / Mathf.Max(0.0001f, temperatureTolerance);

        // 2) pH dev
        float phDev = Mathf.Abs(sample.pH - genome.optimalPH)
                    / Mathf.Max(0.0001f, pHTolerance);

        // 3) UV dev (ideal = uvResistance * maxUV)
        Vector2 uvRange = midiManager.uvRange;
        float idealUV = genome.uvResistance * uvRange.y;
        float uvDev   = Mathf.Abs(sample.uvLightIntensity - idealUV)
                      / Mathf.Max(0.0001f, uvTolerance);

        // 4) Toxin dev (ideal = toxinResistance * maxToxin)
        Vector2 toxRange = midiManager.toxinRange;
        float idealToxin = genome.toxinResistance * toxRange.y;
        float toxDev     = Mathf.Abs(sample.toxinFieldStrength - idealToxin)
                         / Mathf.Max(0.0001f, toxinTolerance);

        // Clamp and average
        tempDev = Mathf.Clamp01(tempDev);
        phDev   = Mathf.Clamp01(phDev);
        uvDev   = Mathf.Clamp01(uvDev);
        toxDev  = Mathf.Clamp01(toxDev);

        alienScore = (tempDev + phDev + uvDev + toxDev) * 0.25f;
    }
}
