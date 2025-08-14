using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;

/// <summary>
/// Manages evolutionary logging.
/// FIX: Now uses a Coroutine to capture generation data, spreading the workload
/// over multiple frames to prevent performance spikes and audio glitches.
/// </summary>
public class EvolutionaryLogManager : MonoBehaviour
{
    // Holds Min/Avg/Max for a parameter
    public struct StatSnapshot
    {
        public float min;
        public float avg;
        public float max;

        public override string ToString()
        {
            return $"Min: {min:F3}, Avg: {avg:F3}, Max: {max:F3}";
        }
    }

    // Holds all data for one generation
    public class GenerationLogData
    {
        public int generationNumber;
        public int aliveCount;
        public int dormantCount;
        public int deadCount;
        public int totalBirths;
        public Dictionary<string, StatSnapshot> parameterStats;

        public GenerationLogData(int genNum)
        {
            generationNumber = genNum;
            parameterStats = new Dictionary<string, StatSnapshot>();
        }
    }

    [Header("Logging Settings")]
    [Tooltip("If checked, a simple status log will be printed every few seconds.")]
    public bool enablePeriodicStatusUpdates = true;
    [Tooltip("How often (in seconds) to print the simple status update.")]
    public float statusUpdateInterval = 5.0f;
    [Tooltip("How many agents to process per frame while logging to avoid spikes.")]
    public int agentsPerFrame = 20;

    private Dictionary<int, GenerationLogData> historicalLogs = new Dictionary<int, GenerationLogData>();
    private int lastLoggedGeneration = -1;
    private float statusLogTimer;
    private FieldInfo[] evolvableFields;
    private bool isLogging = false; // Aynı anda birden fazla log işlemini engeller

    void Start()
    {
        evolvableFields = typeof(EColiGenome)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(float))
            .ToArray();
        lastLoggedGeneration = -1;
    }

    void Update()
    {
        // Yeni bir jenerasyona ulaşıldığında ve zaten loglama yapılmıyorsa, loglamayı başlat
        if (EColiAgent.maxGenerationReached > lastLoggedGeneration && !isLogging)
        {
            // DÜZELTME: Ağır işlemi doğrudan çağırmak yerine Coroutine'i başlat
            StartCoroutine(CaptureAndLogGenerationDataCoroutine(lastLoggedGeneration));
            lastLoggedGeneration = EColiAgent.maxGenerationReached;
        }

        if (enablePeriodicStatusUpdates)
        {
            statusLogTimer += Time.deltaTime;
            if (statusLogTimer >= statusUpdateInterval)
            {
                statusLogTimer = 0f;
                LogCurrentStatus();
            }
        }
    }

    /// <summary>
    /// Prints a real-time status update.
    /// </summary>
    private void LogCurrentStatus()
    {
        string status = $"<color=gray>[STATUS] Gen: {EColiAgent.maxGenerationReached} | Alive: {EColiAgent.aliveCount} | Rest: {EColiAgent.dormantCount} | Dead: {EColiAgent.deadCount} | Total Births: {EColiAgent.totalCreated}</color>";
        Debug.Log(status);
    }

    /// <summary>
    /// DÜZELTME: Bu Coroutine, log verisini toplama işini karelere yayar.
    /// </summary>
    private IEnumerator CaptureAndLogGenerationDataCoroutine(int generationNumber)
    {
        if (generationNumber < 0)
        {
            yield break;
        }

        isLogging = true;

        var allAgents = EColiAgent.Agents.ToList();
        var agentsForAnalysis = allAgents.Where(a => a.state != EColiAgent.State.Dead).ToList();

        int calculatedAlive = agentsForAnalysis.Count;
        int calculatedDormant = allAgents.Count(a => a.state == EColiAgent.State.Dormant);
        int calculatedDead = allAgents.Count - calculatedAlive;

        var logData = new GenerationLogData(generationNumber)
        {
            aliveCount = calculatedAlive,
            dormantCount = calculatedDormant,
            deadCount = calculatedDead,
            totalBirths = EColiAgent.totalCreated
        };

        if (agentsForAnalysis.Count == 0)
        {
            Debug.LogWarning($"Generation {generationNumber} ended with no survivors.");
        }
        else
        {
            var tempStats = new Dictionary<string, (float sum, float min, float max)>();
            foreach (var field in evolvableFields)
            {
                tempStats[field.Name] = (0f, float.MaxValue, float.MinValue);
            }

            int processedCount = 0;
            // DÜZELTME: Tüm ajanları tek karede işlemek yerine, karelere böl.
            foreach (var agent in agentsForAnalysis)
            {
                var genome = agent.genome;
                foreach (var field in evolvableFields)
                {
                    string name = field.Name;
                    float value = (float)field.GetValue(genome); // Bu yavaş bir işlemdir
                    var current = tempStats[name];
                    tempStats[name] = (current.sum + value, Mathf.Min(current.min, value), Mathf.Max(current.max, value));
                }

                processedCount++;
                // Belirlenen sayıda ajanı işledikten sonra bir sonraki kareye geç.
                if (processedCount % agentsPerFrame == 0)
                {
                    yield return null;
                }
            }

            // Son hesaplamaları yap
            foreach (var field in evolvableFields)
            {
                var (sum, min, max) = tempStats[field.Name];
                logData.parameterStats[field.Name] = new StatSnapshot { min = min, avg = sum / agentsForAnalysis.Count, max = max };
            }
        }

        historicalLogs[generationNumber] = logData;
        PrintDetailedLogToConsole(logData);

        isLogging = false;
    }

    /// <summary>
    /// Formats and prints a generation's snapshot to console.
    /// </summary>
    private void PrintDetailedLogToConsole(GenerationLogData logData)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<color=green>===== GENERATION {logData.generationNumber} FINAL REPORT =====</color>");
        sb.AppendLine($"Counts -> Alive: {logData.aliveCount} | Rest: {logData.dormantCount} | Dead: {logData.deadCount} | Total Births: {logData.totalBirths}");
        
        if (logData.parameterStats.Any())
        {
            sb.AppendLine("--------- Evolvable Parameters (Min | Avg | Max) ---------");
            foreach (var entry in logData.parameterStats.OrderBy(e => e.Key))
            {
                sb.AppendLine($"{entry.Key,-30} | {entry.Value}");
            }
        }
        else
        {
            sb.AppendLine("--------- No survivors to analyze. ---------");
        }
        
        sb.AppendLine("==========================================================");
        Debug.Log(sb.ToString());
    }
}