using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// E. coli agent whose genotype (including uvResistance and toxinResistance)
/// remains constant throughout its lifetime unless explicitly altered by
/// mutation, conjugation, transduction or reproduction.
/// All other behaviours—movement, feeding, ageing, costs—read from genome
/// but never write to it.
/// Every newly spawned object (daughter cells and phages) is parented under
/// the Universe GameObject that manages the simulation.
/// Each daughter cell is instantiated with a fixed “birth” material,
/// regardless of any material changes that occur later at runtime.
/// Colony zoning: an agent spawned in a given zone (0–7) treats that zone
/// as its entire world; all of its descendants inherit that same zone.
/// </summary>
public class EColiAgent : MonoBehaviour
{
    public enum State { Run, Tumble, Dead, Dormant }

    [System.Serializable]
    public struct NutrientType
    {
        [Tooltip("The tag of the nutrient GameObject.")]
        public string tag;
        [Tooltip("The base energy this nutrient provides before efficiency multipliers.")]
        public float baseEnergyValue;
        [Tooltip("The absolute maximum energy that can be gained from this nutrient.")]
        public float maxEnergyYield;
    }

    [Header("Genome Template")]
    [Tooltip("ScriptableObject template for this agent's genome.")]
    public EColiGenome genomeAsset;
    [HideInInspector] public EColiGenome genome;  // runtime clone

    [Header("Appearance")]
    [Tooltip("Material to assign to every newly born cell at instantiation.")]
    public Material birthMaterial;

    [Header("Genetic Transfer Rates")]
    public float conjugationRate = 0.05f;
    public float conjugationRadius = 1f;
    public LayerMask agentLayerMask;
    public float transformationRate = 0.02f;
    public LayerMask deadCellLayerMask;
    public float transductionRate = 0.01f;
    public GameObject phagePrefab;

    [Header("Phage Behaviour")]
    public float phageLifetime = 5f;

    [Header("Feeding")]
    [Tooltip("Layers that agents consider to be food.")]
    public LayerMask nutrientLayerMask;
    [Tooltip("Configure the 4 nutrient types and their energy values.")]
    public NutrientType[] nutrientTypes = new NutrientType[4];

    [Header("Initial & Growth")]
    public float initialEnergy = 10f;
    public GameObject agentPrefab;
    public float divisionOffset = 0.5f;
    public float deadLifetime = 30f;

    [Header("Costs")]
    public float speedCostMultiplier = 0.1f;
    public float sensorCostMultiplier = 1f;

    [Header("Population Stress")]
    public int maxPopulation = 300;

    [Header("Aging & Senescence")]
    public float ageThreshold = 200f;
    public float maxAge = 300f;
    public int maxReplications = 20;

    // --- Internal State ---
    public State state = State.Run;
    private float energy, age;
    private int replicationCount;
    public int generation;
    private float[] concMemory;
    private int memoryIndex;
    private bool isQuorumActive = false;
    private bool isInBiofilm = false;

    // --- Static Tracking ---
    private static List<EColiAgent> allAgents = new List<EColiAgent>();
    public static int aliveCount, deadCount, dormantCount, totalCreated, cumulativeDeaths, maxGenerationReached;
    public static IReadOnlyList<EColiAgent> Agents => allAgents;

    // --- References & Buffers ---
    private Universe universeRef;
    private Bounds movementBounds;     // << zone-limited bounds for this lineage
    private int zoneIndex = -1;        // << inherited zone index for this lineage
    private const int MAX_OVERLAP = 32;
    private Collider[] overlapBuffer = new Collider[MAX_OVERLAP];
    private float geneTransferTimer;
    private const float GENE_INTERVAL = 0.5f;
    private float tumbleTimer;
    private Quaternion initialRotation, targetRotation;
    private Vector3 currentRunDirection;

    #region Unity Messages

    void OnEnable()
    {
        allAgents.Add(this);
        if (state == State.Dormant) dormantCount++; else aliveCount++;
        totalCreated++;
        if (generation > maxGenerationReached) maxGenerationReached = generation;
    }

    void OnDisable()
    {
        if (state == State.Dormant) dormantCount = Mathf.Max(0, dormantCount - 1);
        else if (state != State.Dead) aliveCount = Mathf.Max(0, aliveCount - 1);
        allAgents.Remove(this);
    }

    void Start()
    {
        // Clone genome so the SO asset is never mutated directly
        if (genome == null)
        {
            if (genomeAsset == null)
            {
                Debug.LogError($"EColiAgent '{name}' missing genomeAsset.", this);
                enabled = false;
                return;
            }
            genome = Instantiate(genomeAsset);
        }

        universeRef = FindObjectOfType<Universe>();
        if (universeRef == null)
            Debug.LogError("No Universe instance found – agents won't be parented correctly.", this);

        // Resolve our zone from SpawnerTracker (set by Universe at spawn) or keep -1
        ResolveZoneFromTracker();

        // Compute movement bounds for this lineage (zone or whole universe)
        RefreshMovementBounds();

        energy = initialEnergy;
        age = 0f;
        replicationCount = 0;

        int mem = Mathf.Max(1, genome.chemotaxisMemoryLength);
        concMemory = new float[mem];
        memoryIndex = 0;
        float c0 = NutrientField.Instance != null
            ? NutrientField.Instance.GetConcentration(transform.position)
            : 0f;
        for (int i = 0; i < mem; i++) concMemory[i] = c0;

        transform.forward = Random.onUnitSphere;
        currentRunDirection = transform.forward;

        // Ensure initial material is the birthMaterial
        if (birthMaterial != null)
            ApplyBirthMaterial(this.gameObject);
    }

    void Update()
    {
        if (state == State.Dead) return;
        if (state == State.Dormant)
        {
            HandleDormancy();
            return;
        }

        float dt = Time.deltaTime * (EnvironmentManager.Instance?.timeScale ?? 1f);
        age += dt;

        if (CheckAgingDeath(dt)) return;

        geneTransferTimer += dt;
        if (geneTransferTimer >= GENE_INTERVAL)
        {
            TryGeneTransfers();
            geneTransferTimer = 0f;
        }

        UpdateSocialBehaviours(dt);
        if (state == State.Dormant) return;

        TryFeedNutrients();
        UpdateCosts(dt);

        if (energy <= 0f)
        {
            BecomeDeadCell();
            return;
        }

        float dormThresh = genome.dormancyThreshold * (1f - genome.restBehavior + 0.1f);
        if (energy < dormThresh)
        {
            EnterDormancy();
            return;
        }

        if (energy < genome.selfPreservation * genome.reproductionThreshold)
        {
            Tumble(dt);
            KeepInZone();
            return;
        }

        if (state == State.Run) Run(dt);
        else Tumble(dt);

        KeepInZone();
        TryDivide();
    }

    #endregion

    #region Zone Handling

    private void ResolveZoneFromTracker()
    {
        var tracker = GetComponent<SpawnerTracker>();
        if (tracker != null) zoneIndex = tracker.zoneIndex;
    }

    private void RefreshMovementBounds()
    {
        if (universeRef == null)
        {
            movementBounds = new Bounds(transform.position, Vector3.one * 1000f);
            return;
        }

        if (zoneIndex >= 0 && zoneIndex < universeRef.zones.Length)
        {
            // Per your Universe logic: each zone is a box of half the universe size, centred on zone centre
            movementBounds = new Bounds(universeRef.zones[zoneIndex].centre, universeRef.Bounds.size / 2f);
        }
        else
        {
            movementBounds = universeRef.Bounds;
        }
    }

    #endregion

    #region State Management

    private static void RecordStateChange(State oldState, State newState)
    {
        if (oldState == State.Dormant) dormantCount = Mathf.Max(0, dormantCount - 1);
        else if (oldState == State.Run || oldState == State.Tumble) aliveCount = Mathf.Max(0, aliveCount - 1);

        if (newState == State.Dormant) dormantCount++;
        else if (newState == State.Run || newState == State.Tumble) aliveCount++;
        else if (newState == State.Dead) deadCount++;
    }

    void BecomeDeadCell()
    {
        if (state == State.Dead) return;
        RecordStateChange(state, State.Dead);
        state = State.Dead;
        cumulativeDeaths++;

        var dc = gameObject.AddComponent<DeadCell>();
        dc.genome = genome;
        gameObject.tag = "DeadCell";
        gameObject.layer = LayerMask.NameToLayer("DeadCell");
        Destroy(gameObject, deadLifetime);
    }

    void EnterDormancy()
    {
        RecordStateChange(state, State.Dormant);
        state = State.Dormant;
    }

    #endregion

    #region Survival & Social

    void HandleDormancy()
    {
        float dormantMetabolism = genome.metabolismRate * 0.05f * Mathf.Clamp01(1f - genome.starvationTolerance);
        energy -= dormantMetabolism * Time.deltaTime;
        if (energy <= 0f) { BecomeDeadCell(); return; }

        float surrounding = NutrientField.Instance?.GetConcentration(transform.position) ?? 0f;
        float wakeThresh = 0.4f + 0.5f * (1f - genome.restBehavior);
        if (surrounding > wakeThresh && energy > genome.wakeUpEnergyCost)
        {
            energy -= genome.wakeUpEnergyCost;
            RecordStateChange(State.Dormant, State.Run);
            state = State.Run;
        }
    }

    void UpdateSocialBehaviours(float dt)
    {
        int neighbours = Physics.OverlapSphereNonAlloc(
            transform.position, genome.sensorRadius,
            overlapBuffer, agentLayerMask);

        isQuorumActive = (neighbours - 1) >= genome.quorumSensingThreshold;
        if (!isInBiofilm && genome.biofilmTendency > 0.5f && isQuorumActive)
        {
            isInBiofilm = true;
            EnterDormancy();
            return;
        }
        if (isInBiofilm) energy -= genome.biofilmMatrixCost * dt;
        if (genome.toxinProductionRate > 0f) ProduceToxins(dt, neighbours);
    }

    void ProduceToxins(float dt, int count)
    {
        float cost = genome.toxinProductionRate * dt;
        energy -= cost;
        float potency = genome.toxinPotency * (isQuorumActive ? genome.quorumToxinBoost : 1f);
        if (potency <= 0f) return;

        for (int i = 0; i < count; i++)
        {
            var other = overlapBuffer[i].GetComponent<EColiAgent>();
            if (other == null || other == this || other.state != State.Run) continue;
            float dist = CalculateGeneticDistance(genome, other.genome);

            if (dist < genome.kinRecognitionFidelity)
            {
                float share = potency * genome.kinCooperationBonus * dt;
                if (energy > share && other.energy < other.genome.reproductionThreshold)
                {
                    energy -= share;
                    other.ModifyEnergy(share);
                }
            }
            else
            {
                float dmg = potency * (1f - genome.toxinResistance) * dt;
                other.ModifyEnergy(-dmg);
            }
        }
    }

    #endregion

    #region Core Logic

    bool CheckAgingDeath(float dt)
    {
        bool past = age > ageThreshold;
        if (age >= maxAge ||
            (past && Random.value < (Mathf.Clamp01((age - ageThreshold)/(maxAge - ageThreshold)) - genome.deathDelayBias)*dt))
        {
            BecomeDeadCell();
            return true;
        }
        return false;
    }

    void UpdateCosts(float dt)
    {
        float totalEnvCost = 0f;
        if (EnvironmentManager.Instance != null)
        {
            var env = EnvironmentManager.Instance.GetEnvironmentAtPoint(transform.position);

            float tempGap = Mathf.Abs(env.temperature - genome.optimalTemperature);
            float pHGap   = Mathf.Abs(env.pH          - genome.optimalPH);
            float tempCost  = tempGap * genome.temperatureSensitivity;
            float pHCost    = pHGap   * genome.pHSensitivity;
            float uvCost    = env.uvLightIntensity   * (1f - genome.uvResistance);
            float toxinCost = env.toxinFieldStrength * (1f - genome.toxinResistance);

            totalEnvCost = tempCost + pHCost + uvCost + toxinCost;
            if (isInBiofilm) totalEnvCost *= (1f - genome.biofilmTendency * 0.5f);
        }

        float speedCost  = speedCostMultiplier  * (genome.baseRunSpeed * genome.runSpeedFactor);
        float sensorCost = sensorCostMultiplier * (1f / genome.tumbleSensitivity);

        float totalMetCost = genome.metabolismRate + speedCost + sensorCost;
        if (energy < genome.dormancyThreshold)
            totalMetCost *= Mathf.Clamp01(1f - genome.starvationTolerance);

        energy -= (totalMetCost + totalEnvCost) * dt;
    }

    void Run(float dt)
    {
        float curSpeed = genome.baseRunSpeed * genome.runSpeedFactor;
        if (isInBiofilm) curSpeed *= (1f - genome.biofilmTendency);
        transform.position += currentRunDirection * curSpeed * dt;

        float raw = NutrientField.Instance?.GetConcentration(transform.position) ?? 0f;
        raw += Random.Range(-genome.decisionNoise, genome.decisionNoise);
        if (raw < genome.nutrientDiscrimination) raw = 0f;

        concMemory[memoryIndex] = raw;
        int newest = (memoryIndex - 1 + concMemory.Length) % concMemory.Length;
        int oldest = memoryIndex;
        float slope = (concMemory[newest] - concMemory[oldest]) / concMemory.Length;
        if (Mathf.Abs(slope) < genome.gradientTolerance) slope = 0f;

        float tumbleRate = Mathf.Clamp(
            genome.baseTumbleRate - slope * genome.tumbleSlopeSensitivity,
            0.01f, 10f);

        if (Random.value < genome.explorationTendency * dt) StartTumble(true);
        else if (Random.value < tumbleRate * dt) StartTumble(false);

        memoryIndex = (memoryIndex + 1) % concMemory.Length;
    }

    void StartTumble(bool exploratory)
    {
        RecordStateChange(State.Run, State.Tumble);
        state = State.Tumble;
        tumbleTimer = 0f;
        initialRotation = transform.rotation;

        if (exploratory)
            targetRotation = Quaternion.LookRotation(Random.onUnitSphere, Vector3.up);
        else
        {
            float angle = Random.Range(-genome.tumbleAngleRange, genome.tumbleAngleRange);
            Vector3 newDir = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
            targetRotation = Quaternion.LookRotation(newDir, Vector3.up);
        }
    }

    void Tumble(float dt)
    {
        tumbleTimer += dt;
        float t = tumbleTimer / genome.tumbleDuration;
        transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, Mathf.Clamp01(t));
        if (t >= 1f)
        {
            RecordStateChange(State.Tumble, State.Run);
            state = State.Run;
            currentRunDirection = transform.forward;
        }
    }

    // Clamp & bounce within our lineage's zone
    void KeepInZone()
    {
        if (universeRef == null) return;

        Vector3 p = transform.position;
        Vector3 f = currentRunDirection;
        bool bounced = false;

        if (p.x < movementBounds.min.x) { p.x = movementBounds.min.x; f.x = -f.x; bounced = true; }
        else if (p.x > movementBounds.max.x) { p.x = movementBounds.max.x; f.x = -f.x; bounced = true; }
        if (p.y < movementBounds.min.y) { p.y = movementBounds.min.y; f.y = -f.y; bounced = true; }
        else if (p.y > movementBounds.max.y) { p.y = movementBounds.max.y; f.y = -f.y; bounced = true; }
        if (p.z < movementBounds.min.z) { p.z = movementBounds.min.z; f.z = -f.z; bounced = true; }
        else if (p.z > movementBounds.max.z) { p.z = movementBounds.max.z; f.z = -f.z; bounced = true; }

        transform.position = p;
        if (bounced)
        {
            currentRunDirection = f.normalized;
            transform.rotation  = Quaternion.LookRotation(currentRunDirection, Vector3.up);
        }
    }

    void TryDivide()
    {
        if (replicationCount >= maxReplications) return;
        float popFactor = 1f - Mathf.Clamp01((float)aliveCount / maxPopulation);
        float dynThresh = genome.reproductionThreshold / Mathf.Max(popFactor, 0.1f);
        if (age >= genome.divisionDelay && energy >= dynThresh) Divide();
    }

    #endregion

    #region Feeding & Gene Transfer

    public void ModifyEnergy(float amount) => energy += amount;

    void TryFeedNutrients()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, genome.sensorRadius, overlapBuffer, nutrientLayerMask);
        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col.gameObject == gameObject) continue;

            if (col.CompareTag("DeadCell"))
            {
                if (Random.value < genome.deadCellPreference)
                {
                    energy += genome.reproductionThreshold * 0.5f;
                    Destroy(col.gameObject);
                    return;
                }
            }

            for (int j = 0; j < nutrientTypes.Length; j++)
            {
                if (col.CompareTag(nutrientTypes[j].tag))
                {
                    float e = nutrientTypes[j].baseEnergyValue * GetEfficiency(j);
                    e = Mathf.Min(e, nutrientTypes[j].maxEnergyYield);
                    energy += e;
                    Destroy(col.gameObject);
                    return;
                }
            }
        }
    }

    float GetEfficiency(int i)
    {
        switch (i)
        {
            case 0: return genome.nutrientEfficiencyA;
            case 1: return genome.nutrientEfficiencyB;
            case 2: return genome.nutrientEfficiencyC;
            case 3: return genome.nutrientEfficiencyD;
            default: return 1f;
        }
    }

    void TryGeneTransfers()
    {
        int m = Random.Range(0,3);
        if (m==0) TryConjugation();
        else if (m==1) TryTransformation();
        else TryTransduction();
    }

    void TryConjugation()
    {
        if (Random.value > conjugationRate * genome.conjugationAggressiveness * GENE_INTERVAL) return;
        if (Random.value < genome.geneticStability) return;

        int hits = Physics.OverlapSphereNonAlloc(transform.position,
                        conjugationRadius, overlapBuffer, agentLayerMask);
        for(int i=0;i<hits;i++)
        {
            var other = overlapBuffer[i].GetComponent<EColiAgent>();
            if (other==null||other==this||other.state!=State.Run) continue;
            float dist = CalculateGeneticDistance(genome, other.genome);
            if (dist>genome.plasmidCompatibilityThreshold) continue;
            MixGenomes(genome, other.genome);
            break;
        }
    }

    void TryTransformation()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position,
                        genome.sensorRadius, overlapBuffer, deadCellLayerMask);
        for(int i=0;i<hits;i++)
        {
            var dc = overlapBuffer[i].GetComponent<DeadCell>();
            if(dc!=null && Random.value>genome.geneticStability
               && Random.value<transformationRate)
            {
                MixGenomes(genome, dc.genome);
                Destroy(dc.gameObject);
                break;
            }
        }
    }

    void TryTransduction()
    {
        if (phagePrefab==null || universeRef==null) return;
        if (Random.value<genome.geneticStability) return;
        if (Random.value>transductionRate*GENE_INTERVAL) return;

        var p = Instantiate(phagePrefab,
                            transform.position,
                            Quaternion.identity,
                            universeRef.transform);
        if (p.GetComponent<Phage>() is Phage comp)
        {
            comp.creator      = this;
            comp.storedGenome = genome;
            comp.lifetime     = phageLifetime;
        }
        else Destroy(p);
    }

    #endregion

    #region Division & Mutation

    void Divide()
    {
        if (Random.value < genome.lethalMutationRate) return;
        energy *= 0.5f;
        replicationCount++;

        var env = EnvironmentManager.Instance != null
            ? EnvironmentManager.Instance.GetEnvironmentAtPoint(transform.position)
            : default;

        float eStress = Mathf.Clamp01((genome.reproductionThreshold - energy) / genome.reproductionThreshold);
        float uvStress = env.uvLightIntensity * 0.5f;
        float stress = eStress + uvStress;

        float mRate = genome.generalMutationRate * (1f + stress * genome.stressInducedMutabilityFactor);

        EColiGenome childGenome = Instantiate(genome);
        EvolveMutationRates(childGenome, mRate);
        MutateGenome(childGenome, mRate);
        InstantiateChild(childGenome);
    }

    void InstantiateChild(EColiGenome childGenome)
    {
        Vector3 off = Random.onUnitSphere * divisionOffset;
        Quaternion yaw = Quaternion.Euler(0f, Random.Range(-15f,15f),0f);
        var prefab = agentPrefab != null ? agentPrefab : gameObject;

        var go = Instantiate(prefab,
                             transform.position + off,
                             transform.rotation * yaw,
                             universeRef != null ? universeRef.transform : transform.parent);

        // Ensure the child carries the same zone/group via SpawnerTracker
        var childTracker = go.GetComponent<SpawnerTracker>() ?? go.AddComponent<SpawnerTracker>();
        var myTracker    = GetComponent<SpawnerTracker>();
        if (myTracker != null)
        {
            childTracker.zoneIndex  = myTracker.zoneIndex;
            childTracker.entryIndex = myTracker.entryIndex;
        }
        else
        {
            childTracker.zoneIndex  = zoneIndex;
            childTracker.entryIndex = -1;
        }

        // Assign genome & state
        if (go.GetComponent<EColiAgent>() is EColiAgent agent)
        {
            agent.genome      = childGenome;
            agent.genomeAsset = genomeAsset;
            agent.energy      = energy;
            agent.state       = State.Run;
            agent.generation  = generation + 1;

            // Pre-set the inherited zone so bounds are correct from frame 0
            agent.zoneIndex = childTracker.zoneIndex;
            agent.universeRef = this.universeRef;
            agent.RefreshMovementBounds();

            // initialise chemotaxis memory
            int mem = Mathf.Max(1, agent.genome.chemotaxisMemoryLength);
            agent.concMemory  = new float[mem];
            float c1 = NutrientField.Instance != null
                       ? NutrientField.Instance.GetConcentration(agent.transform.position)
                       : 0f;
            for (int i = 0; i < mem; i++)
                agent.concMemory[i] = c1;
        }

        // Apply the birth material to daughter cell
        if (birthMaterial != null)
            ApplyBirthMaterial(go);
    }

    /// <summary>
    /// Recursively sets the birthMaterial on all Renderers
    /// of the given GameObject and its children.
    /// </summary>
    private void ApplyBirthMaterial(GameObject obj)
    {
        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
        {
            rend.material = birthMaterial;
        }
    }

    void EvolveMutationRates(EColiGenome child, float metaRate)
    {
        var mr = child.mutationRates;
        float scale = metaRate * 0.1f;

        mr.nutrientEfficiencyA += Random.Range(-scale, scale);
        mr.nutrientEfficiencyB += Random.Range(-scale, scale);
        mr.nutrientEfficiencyC += Random.Range(-scale, scale);
        mr.nutrientEfficiencyD += Random.Range(-scale, scale);
        mr.dormancyThreshold   += Random.Range(-scale, scale);
        mr.wakeUpEnergyCost    += Random.Range(-scale, scale);
        mr.toxinProductionRate += Random.Range(-scale, scale);
        mr.toxinPotency        += Random.Range(-scale, scale);
        mr.biofilmTendency     += Random.Range(-scale, scale);
        mr.biofilmMatrixCost   += Random.Range(-scale, scale);
        mr.quorumSensingThreshold += Random.Range(-scale, scale);
        mr.quorumToxinBoost    += Random.Range(-scale, scale);
        mr.kinRecognitionFidelity += Random.Range(-scale, scale);
        mr.kinCooperationBonus += Random.Range(-scale, scale);
        mr.stressInducedMutabilityFactor += Random.Range(-scale, scale);
        mr.plasmidCompatibilityThreshold += Random.Range(-scale, scale);
        mr.uvResistance         += Random.Range(-scale, scale);

        child.mutationRates = mr;
    }

    void MutateGenome(EColiGenome child, float rate)
    {
        var mr = child.mutationRates;

        child.nutrientEfficiencyA += Random.Range(-rate*mr.nutrientEfficiencyA,   rate*mr.nutrientEfficiencyA);
        child.nutrientEfficiencyB += Random.Range(-rate*mr.nutrientEfficiencyB,   rate*mr.nutrientEfficiencyB);
        child.nutrientEfficiencyC += Random.Range(-rate*mr.nutrientEfficiencyC,   rate*mr.nutrientEfficiencyC);
        child.nutrientEfficiencyD += Random.Range(-rate*mr.nutrientEfficiencyD,   rate*mr.nutrientEfficiencyD);
        child.runSpeedFactor      += Random.Range(-rate*mr.runSpeedFactor,       rate*mr.runSpeedFactor);
        child.tumbleSensitivity   += Random.Range(-rate*mr.tumbleSensitivity,    rate*mr.tumbleSensitivity);
        child.dormancyThreshold   += Random.Range(-rate*mr.dormancyThreshold,    rate*mr.dormancyThreshold);
        child.wakeUpEnergyCost    += Random.Range(-rate*mr.wakeUpEnergyCost,     rate*mr.wakeUpEnergyCost);
        child.toxinProductionRate += Random.Range(-rate*mr.toxinProductionRate,  rate*mr.toxinProductionRate);
        child.toxinPotency        += Random.Range(-rate*mr.toxinPotency,         rate*mr.toxinPotency);

        child.biofilmTendency     += Random.Range(-rate*mr.biofilmTendency,      rate*mr.biofilmTendency);
        child.biofilmTendency      = Mathf.Clamp01(child.biofilmTendency);
        child.uvResistance        = Mathf.Clamp01(child.uvResistance);

        child.nutrientEfficiencyA = Mathf.Max(0f, child.nutrientEfficiencyA);
        child.nutrientEfficiencyB = Mathf.Max(0f, child.nutrientEfficiencyB);
        child.nutrientEfficiencyC = Mathf.Max(0f, child.nutrientEfficiencyC);
        child.nutrientEfficiencyD = Mathf.Max(0f, child.nutrientEfficiencyD);
    }

    #endregion

    #region Helpers

    public static float CalculateGeneticDistance(EColiGenome g1, EColiGenome g2)
    {
        float d = 0f;
        d += Mathf.Abs(g1.runSpeedFactor   - g2.runSpeedFactor);
        d += Mathf.Abs(g1.optimalTemperature - g2.optimalTemperature) * 0.1f;
        d += Mathf.Abs(g1.toxinResistance  - g2.toxinResistance);
        d += Mathf.Abs(g1.biofilmTendency  - g2.biofilmTendency);
        d += Mathf.Abs(g1.uvResistance     - g2.uvResistance);
        d += Mathf.Abs(g1.nutrientEfficiencyA - g2.nutrientEfficiencyA);
        d += Mathf.Abs(g1.nutrientEfficiencyB - g2.nutrientEfficiencyB);
        d += Mathf.Abs(g1.nutrientEfficiencyC - g2.nutrientEfficiencyC);
        d += Mathf.Abs(g1.nutrientEfficiencyD - g2.nutrientEfficiencyD);
        return d;
    }

    public static void MixGenomes(EColiGenome receiver, EColiGenome donor)
    {
        receiver.runSpeedFactor    = (receiver.runSpeedFactor    + donor.runSpeedFactor)    * 0.5f;
        receiver.toxinResistance   = (receiver.toxinResistance   + donor.toxinResistance)   * 0.5f;
        receiver.nutrientEfficiencyA = (receiver.nutrientEfficiencyA + donor.nutrientEfficiencyA) * 0.5f;
        receiver.nutrientEfficiencyB = (receiver.nutrientEfficiencyB + donor.nutrientEfficiencyB) * 0.5f;
        receiver.nutrientEfficiencyC = (receiver.nutrientEfficiencyC + donor.nutrientEfficiencyC) * 0.5f;
        receiver.nutrientEfficiencyD = (receiver.nutrientEfficiencyD + donor.nutrientEfficiencyD) * 0.5f;
        receiver.dormancyThreshold   = (receiver.dormancyThreshold   + donor.dormancyThreshold)   * 0.5f;
        receiver.toxinProductionRate = (receiver.toxinProductionRate + donor.toxinProductionRate) * 0.5f;
        receiver.toxinPotency        = (receiver.toxinPotency        + donor.toxinPotency)        * 0.5f;
        receiver.biofilmTendency     = (receiver.biofilmTendency     + donor.biofilmTendency)     * 0.5f;
        receiver.kinRecognitionFidelity = (receiver.kinRecognitionFidelity + donor.kinRecognitionFidelity) * 0.5f;
        receiver.plasmidCompatibilityThreshold =
            (receiver.plasmidCompatibilityThreshold + donor.plasmidCompatibilityThreshold) * 0.5f;
        receiver.uvResistance        = (receiver.uvResistance        + donor.uvResistance)        * 0.5f;
    }

    #endregion
}
