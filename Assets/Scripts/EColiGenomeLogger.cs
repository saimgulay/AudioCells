// Assets/Scripts/GenomeSonification/EColiGenomeLogger.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Live logger for E. coli genomes and population stats.
/// - Optionally locks onto the first available agent for stable live editing.
/// - Otherwise samples at intervals, synchronised with optional audio notifiers.
/// - Displays selected cell’s genome highlights + population summary.
/// - Shows the selected cell's **zone** (1–8 for display) and that zone’s
///   environmental properties (Temperature, pH, UV, Toxin).
/// - When not locked, cycles zones in order (0→7, then wraps). If the current
///   zone has no living cells, it skips forward until it finds one.
/// - Bootstrap-safe: notifier eşleşmeleri olsa bile ilk hücreyi seçmek için
///   beklemez; en az bir hücre seçildikten sonra notifier senkron kapısı devreye girer.
/// </summary>
[ExecuteAlways]
public class EColiGenomeLogger : MonoBehaviour
{
    [Tooltip("How often (in seconds) to sample a random living E. coli. Only used if locking is disabled.")]
    public float updateInterval = 1f;

    [Tooltip("If checked, the logger will find the first available agent and lock onto it; if unchecked, it will sample a random agent at each interval.")]
    public bool lockOntoFirstAgent = true;

    [Header("Audio Notifiers")]
    [Tooltip("Optional: a SynthMelodyLooper to wait for melody completion before sampling the next agent.")]
    public SynthMelodyLooper melodyLooper;

    [Tooltip("Optional: a SynthChordPlayer to wait for full chord sequence completion before sampling the next agent.")]
    public SynthChordPlayer chordPlayer;

    [Header("Log Display")]
    [Tooltip("Drag the TextMeshPro UGUI component for your 'Logs' object here.")]
    public TMP_Text logText;

    /// <summary>The genome currently being sampled (null if none).</summary>
    [HideInInspector] public EColiGenome currentGenome;

    /// <summary>Fired whenever currentGenome changes (oldAgent, newAgent).</summary>
    public event System.Action<EColiAgent, EColiAgent> OnSampledAgentChanged;

    private float _timer;
    private EColiAgent _lastAgent;
    private bool _canSample = true;

    // Universe reference for zone/environment look-ups.
    private Universe _universeRef;

    // Sequential zone cycling: next zone to target (zero-based). Defaults to 0..7.
    private int _nextZoneIndex = 0;

    // Optional: record last sampled zone for UI/debug.
    private int _lastZoneIndex = -1;

    void OnEnable()
    {
        if (melodyLooper != null)
            melodyLooper.OnMelodyFinished += HandleNotifierFinished;
        if (chordPlayer != null)
            chordPlayer.OnSequenceFinished += HandleNotifierFinished;
    }

    void OnDisable()
    {
        if (melodyLooper != null)
            melodyLooper.OnMelodyFinished -= HandleNotifierFinished;
        if (chordPlayer != null)
            chordPlayer.OnSequenceFinished -= HandleNotifierFinished;
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        if (logText == null)
            Debug.LogError("'logText' reference is not set in the Inspector for EColiGenomeLogger!");

        _universeRef = FindObjectOfType<Universe>();
        _nextZoneIndex = 0; // start from zone 0

        // Immediate first sample attempt (spawn henüz gelmediyse sonuçsuz olabilir)
        SampleOneAgentImmediately();

        UpdateLogDisplay();
        _timer = updateInterval;

        // Notifier varsa normalde beklerdik; ancak bootstrap kuralı gereği
        // currentGenome null ise beklemeyeceğiz. Yine de bayrak dursun.
        if (melodyLooper != null || chordPlayer != null)
            _canSample = false;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (_universeRef == null) _universeRef = FindObjectOfType<Universe>();

        // If locked on the first agent, never switch — but keep display fresh
        if (lockOntoFirstAgent && currentGenome != null)
        {
            UpdateLogDisplay();
            return;
        }

        // —— Bootstrap-safe notifier gate ——
        // Notifier’a bağlı beklemeyi SADECE en az bir hücre seçtikten sonra uygula.
        // İlk hücreyi almak için bekleme yapma; böylece deadlock kırılır.
        bool notifierPresent = (melodyLooper != null || chordPlayer != null);
        if (notifierPresent && !_canSample && currentGenome != null)
        {
            UpdateLogDisplay();
            return;
        }

        _timer += Time.deltaTime;
        if (_timer < updateInterval)
        {
            UpdateLogDisplay();
            return;
        }
        _timer = 0f;

        // Gather living agents
        var alive = EColiAgent.Agents
            .Where(a => a != null && a.state != EColiAgent.State.Dead)
            .ToList();

        if (alive.Count == 0)
        {
            if (!lockOntoFirstAgent && currentGenome != null)
                SwapAgent(null);
            UpdateLogDisplay();
            return;
        }

        // Pick next agent by cycling zones 0..7 (or Universe.zones.Length-1), skipping empty zones.
        var next = lockOntoFirstAgent ? alive[0] : ChooseRandomSequentialByZone(alive);
        SwapAgent(next);

        // Notifier senkronunu sadece bir hücre seçilmişken aktif et
        if (notifierPresent && currentGenome != null)
            _canSample = false;
    }

    private void HandleNotifierFinished() => _canSample = true;

    private void SampleOneAgentImmediately()
    {
        var alive = EColiAgent.Agents
            .Where(a => a != null && a.state != EColiAgent.State.Dead)
            .ToList();

        if (alive.Count == 0) return;

        var first = lockOntoFirstAgent ? alive[0] : ChooseRandomSequentialByZone(alive);
        SwapAgent(first);
    }

    /// <summary>
    /// Sequential zone selector: attempts to select from _nextZoneIndex, and if no
    /// living cells exist there, advances until a zone with living cells is found.
    /// Picks a random agent from that zone. Advances _nextZoneIndex to the zone
    /// after the selected one (wraps around).
    /// Falls back to any living agent if zones are undefined.
    /// </summary>
    private EColiAgent ChooseRandomSequentialByZone(List<EColiAgent> alive)
    {
        if (_universeRef == null || _universeRef.zones == null || _universeRef.zones.Length == 0)
        {
            // No zone data; just pick random.
            return alive[Random.Range(0, alive.Count)];
        }

        int zones = _universeRef.zones.Length; // expected 8, but generalised
        // Try each zone at most once per sampling to avoid infinite loops.
        for (int k = 0; k < zones; k++)
        {
            int targetZ = (_nextZoneIndex + k) % zones;

            // Filter all living agents that belong to targetZ
            var inZone = alive.Where(a => GetZoneIndexZeroBased(a) == targetZ).ToList();
            if (inZone.Count > 0)
            {
                // Random cell within the target zone
                var chosen = inZone[Random.Range(0, inZone.Count)];

                // Next time, move to the zone after the one we actually picked
                _nextZoneIndex = (targetZ + 1) % zones;
                return chosen;
            }
            // otherwise skip to next zone in the loop
        }

        // Fallback: any living agent and still advance the pointer.
        var fallback = alive[Random.Range(0, alive.Count)];
        _nextZoneIndex = (_nextZoneIndex + 1) % Mathf.Max(1, _universeRef.zones.Length);
        return fallback;
    }

    /// <summary>
    /// Returns the agent's zone index (zero-based). Uses SpawnerTracker if present,
    /// otherwise falls back to nearest zone centre. Returns -1 if unavailable.
    /// </summary>
    private int GetZoneIndexZeroBased(EColiAgent agent)
    {
        if (agent == null) return -1;
        if (_universeRef == null || _universeRef.zones == null || _universeRef.zones.Length == 0)
            return -1;

        var tracker = agent.GetComponent<SpawnerTracker>();
        if (tracker != null && tracker.zoneIndex >= 0 && tracker.zoneIndex < _universeRef.zones.Length)
            return tracker.zoneIndex;

        float best = float.MaxValue; int bestIdx = -1;
        for (int i = 0; i < _universeRef.zones.Length; i++)
        {
            float d2 = (agent.transform.position - _universeRef.zones[i].centre).sqrMagnitude;
            if (d2 < best) { best = d2; bestIdx = i; }
        }
        return bestIdx;
    }

    private void SwapAgent(EColiAgent next)
    {
        if (next == _lastAgent) { UpdateLogDisplay(); return; }

        var old = _lastAgent;
        _lastAgent = next;
        currentGenome = next != null ? next.genome : null;

        // Update last zone index for UI/debug (not used for selection any more)
        _lastZoneIndex = GetZoneIndexZeroBased(next);

        OnSampledAgentChanged?.Invoke(old, next);
        UpdateLogDisplay();
    }

    /// <summary>
    /// Updates on-screen text with selected genome, zone details, and population summary.
    /// </summary>
    private void UpdateLogDisplay()
    {
        if (logText == null) return;

        // Selected cell block
        string selectedInfo;
        if (_lastAgent != null && currentGenome != null)
        {
            selectedInfo =
                "Selected Cell:\n" +
                $"Generation: {_lastAgent.generation}\n" +
                $"Optimal Temperature: {currentGenome.optimalTemperature:F1} °C\n" +
                $"Optimal pH: {currentGenome.optimalPH:F2}\n" +
                $"UV Resistance: {currentGenome.uvResistance:F2}\n" +
                $"Toxin Resistance: {currentGenome.toxinResistance:F2}\n";
        }
        else
        {
            selectedInfo =
                "Selected Cell:\n" +
                "Generation: N/A\n" +
                "Optimal Temperature: N/A\n" +
                "Optimal pH: N/A\n" +
                "UV Resistance: N/A\n" +
                "Toxin Resistance: N/A\n";
        }

        // Zone block (display zone as 1–8; index stays zero-based internally)
        string zoneInfo = BuildZoneInfo(_lastAgent);

        // Population block
        int totalGenerations = EColiAgent.maxGenerationReached + 1;
        int currentPopulation = EColiAgent.aliveCount;
        int totalBirths = EColiAgent.totalCreated;

        string popInfo =
            $"\nTotal Generations: {totalGenerations}\n" +
            $"Current Population: {currentPopulation}\n" +
            $"Total Births: {totalBirths}";

        logText.text = selectedInfo + zoneInfo + popInfo;
    }

    /// <summary>
    /// Builds zone text for the agent. Prefers SpawnerTracker’s zone (zero-based),
    /// otherwise falls back to nearest zone centre. Displays zone number as 1–8.
    /// </summary>
    private string BuildZoneInfo(EColiAgent agent)
    {
        if (agent == null)
            return "\nZone: N/A\nTemperature: N/A\npH: N/A\nUV: N/A\nToxin: N/A\n";

        if (_universeRef == null || _universeRef.zones == null || _universeRef.zones.Length == 0)
            return "\nZone: N/A (Universe not found)\nTemperature: N/A\npH: N/A\nUV: N/A\nToxin: N/A\n";

        int idxZB = -1; // zero-based
        var tracker = agent.GetComponent<SpawnerTracker>();
        if (tracker != null && tracker.zoneIndex >= 0 && tracker.zoneIndex < _universeRef.zones.Length)
        {
            idxZB = tracker.zoneIndex;
        }
        else
        {
            // Fallback: nearest zone centre
            float best = float.MaxValue; int bestIdx = -1;
            for (int i = 0; i < _universeRef.zones.Length; i++)
            {
                float d2 = (agent.transform.position - _universeRef.zones[i].centre).sqrMagnitude;
                if (d2 < best) { best = d2; bestIdx = i; }
            }
            idxZB = bestIdx;
        }

        if (idxZB < 0 || idxZB >= _universeRef.zones.Length)
            return "\nZone: N/A\nTemperature: N/A\npH: N/A\nUV: N/A\nToxin: N/A\n";

        var z = _universeRef.zones[idxZB];
        int displayOneBased = idxZB + 1;

        return
            $"\nZone: {displayOneBased}\n" +
            $"Temperature: {z.temperature:F1} °C\n" +
            $"pH: {z.pH:F2}\n" +
            $"UV: {z.uvLightIntensity:F2}\n" +
            $"Toxin: {z.toxinFieldStrength:F2}\n";
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(EColiGenomeLogger))]
    public class EColiGenomeLoggerEditor : Editor
    {
        SerializedProperty _intervalProp, _lockProp, _melodyProp, _chordProp, _logTextProp;
        EColiGenomeLogger _logger;

        void OnEnable()
        {
            _logger       = (EColiGenomeLogger)target;
            _lockProp     = serializedObject.FindProperty(nameof(_logger.lockOntoFirstAgent));
            _intervalProp = serializedObject.FindProperty(nameof(_logger.updateInterval));
            _melodyProp   = serializedObject.FindProperty(nameof(_logger.melodyLooper));
            _chordProp    = serializedObject.FindProperty(nameof(_logger.chordPlayer));
            _logTextProp  = serializedObject.FindProperty(nameof(_logger.logText));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_lockProp);
            if (!_logger.lockOntoFirstAgent)
                EditorGUILayout.PropertyField(_intervalProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("▶ Audio Notifiers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_melodyProp);
            EditorGUILayout.PropertyField(_chordProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("▶ Log Display", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_logTextProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("▶ Current Sampled Genome (live)", EditorStyles.boldLabel);

            if (Application.isPlaying && _logger.currentGenome != null)
            {
                var genomeSO = new SerializedObject(_logger.currentGenome);
                genomeSO.Update();
                var it = genomeSO.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    EditorGUILayout.PropertyField(it, true);
                    enter = false;
                }
                genomeSO.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox("No genome sampled yet (or not in Play Mode).", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
