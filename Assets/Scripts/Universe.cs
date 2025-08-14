using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor; // Required for the in-editor labels
#endif

/// <summary>
/// Defines the simulation space and its eight environmental zones.
/// Spawns a distinct prefab at the centre of each zone,
/// and provides public methods to spawn various types of agent prefabs.
/// Maintains each group’s population by refilling when numbers fall below
/// a configurable fraction of the initial count, if enabled per-prefab.
/// Every spawned object becomes a child of the GameObject this script is attached to.
/// </summary>
public class Universe : MonoBehaviour
{
    public enum SpawnMode
    {
        Cluster, Uniform, Shell, Wall, Grid,
        OpposingClusters, Nebula, Archipelago, FlowingRiver, BoxSurface
    }

    [System.Serializable]
    public struct PrefabEntry
    {
        [Tooltip("The agent prefab to be spawned.")]
        public GameObject prefab;
        [Tooltip("How many agents to spawn at once.")]
        public int count;
        [Tooltip("The distribution mode used at spawn.")]
        public SpawnMode spawnMode;
        [Tooltip("If checked, this prefab group will be spawned when the simulation starts.")]
        public bool spawnOnStart;
        [Tooltip("Index of the environmental zone in which to spawn agents (0–7).")]
        public int zoneIndex;
        [Tooltip("Enable refill when the current population falls below a fraction of initial count.")]
        public bool enableRefillOnLowCount;
        [Tooltip("Refill when population falls below this fraction (0 to 1) of initial count.")]
        [Range(0f, 1f)]
        public float refillThreshold;
    }

    [System.Serializable]
    public struct EnvironmentZone
    {
        [Header("Zone Environmental Properties")]
        [Tooltip("Temperature in degrees Celsius.")]
        public float temperature;
        [Tooltip("pH level of the environment.")]
        public float pH;
        [Tooltip("UV light intensity (arbitrary units).")]
        public float uvLightIntensity;
        [Tooltip("Toxin field strength (arbitrary units).")]
        public float toxinFieldStrength;

        [HideInInspector]
        public Vector3 centre;
    }

    [Header("Universe Definition")]
    [Tooltip("Assign the cube that defines your universe bounds.")]
    public GameObject universeContainer;
    public Bounds Bounds { get; private set; }

    [Header("Environmental Zones")]
    [Tooltip("Define the properties for the eight zones of the universe.")]
    public EnvironmentZone[] zones = new EnvironmentZone[8];

    [Header("Zone Centre Prefabs")]
    [Tooltip("Prefabs to instantiate at each zone centre. Array length must match zones length.")]
    public GameObject[] zoneCentrePrefabs = new GameObject[8];

    [Header("Spawning Configuration")]
    [Tooltip("Configure the types of agent prefabs for the simulation.")]
    public PrefabEntry[] prefabsToSpawn = new PrefabEntry[4];

    [Header("Distribution Settings")]
    [Tooltip("The radius used for Cluster and Shell spawn modes.")]
    public float spawnRadius = 8f;
    [Tooltip("Number of islands for the Archipelago mode.")]
    public int archipelagoIslandCount = 5;
    [Tooltip("Amplitude for the Flowing River mode.")]
    public float riverAmplitude = 5f;
    [Tooltip("Frequency for the Flowing River mode.")]
    public float riverFrequency = 0.5f;

    void Awake()
    {
        var renderer = universeContainer.GetComponent<Renderer>();
        if (renderer != null)
            Bounds = renderer.bounds;
        else
            Debug.LogError("UniverseContainer must have a Renderer to define its bounds!");
    }

    void Start()
    {
        CalculateZoneCentres();

        // Spawn centre markers for each zone
        if (zoneCentrePrefabs != null && zoneCentrePrefabs.Length == zones.Length)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                var prefab = zoneCentrePrefabs[i];
                if (prefab != null)
                    Instantiate(prefab, zones[i].centre, Quaternion.identity, transform);
            }
        }

        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.Initialize(Bounds, zones);
        }
        else
        {
            Debug.LogError("EnvironmentManager instance not found!");
        }

        // Initial spawn
        for (int i = 0; i < prefabsToSpawn.Length; i++)
        {
            var entry = prefabsToSpawn[i];
            if (entry.prefab != null && entry.spawnOnStart)
                Spawn(entry, i);
        }
    }

    void Update()
    {
        // Refill logic: for each prefab group, if refill enabled and population falls below threshold * initial count, spawn a full batch
        for (int i = 0; i < prefabsToSpawn.Length; i++)
        {
            var entry = prefabsToSpawn[i];
            if (!entry.enableRefillOnLowCount || entry.prefab == null || entry.count <= 0) continue;
            int zone = entry.zoneIndex;
            if (zone < 0 || zone >= zones.Length) continue;

            int currentCount = 0;
            foreach (Transform child in transform)
            {
                var tracker = child.GetComponent<SpawnerTracker>();
                if (tracker != null && tracker.entryIndex == i && tracker.zoneIndex == zone)
                    currentCount++;
            }

            if (currentCount <= entry.count * entry.refillThreshold)
                Spawn(entry, i);
        }
    }

    #region Public Spawning Functions (for UI Buttons)

    public void SpawnPrefab1() { if (prefabsToSpawn.Length > 0) Spawn(prefabsToSpawn[0], 0); }
    public void SpawnPrefab2() { if (prefabsToSpawn.Length > 1) Spawn(prefabsToSpawn[1], 1); }
    public void SpawnPrefab3() { if (prefabsToSpawn.Length > 2) Spawn(prefabsToSpawn[2], 2); }
    public void SpawnPrefab4() { if (prefabsToSpawn.Length > 3) Spawn(prefabsToSpawn[3], 3); }

    #endregion

    private void Spawn(PrefabEntry entry, int entryIndex)
    {
        if (entry.prefab == null || entry.count <= 0) return;

        // Determine a random cluster centre within the zone for cluster-like modes
        Vector3? specificCentre = null;
        if (entry.zoneIndex >= 0 && entry.zoneIndex < zones.Length)
        {
            switch (entry.spawnMode)
            {
                case SpawnMode.Cluster:
                case SpawnMode.Shell:
                case SpawnMode.OpposingClusters:
                case SpawnMode.Nebula:
                case SpawnMode.Archipelago:
                    specificCentre = GetRandomPointInZone(entry.zoneIndex, spawnRadius);
                    break;
                default:
                    specificCentre = null;
                    break;
            }
        }

        switch (entry.spawnMode)
        {
            case SpawnMode.Cluster:
                SpawnCluster(entry.prefab, entry.count, specificCentre, entryIndex, entry.zoneIndex);
                break;
            case SpawnMode.Uniform:
                SpawnUniform(entry.prefab, entry.count, entry.zoneIndex, entryIndex);
                break;
            case SpawnMode.Shell:
                SpawnShell(entry.prefab, entry.count, specificCentre, entryIndex, entry.zoneIndex);
                break;
            case SpawnMode.Wall:
                SpawnWall(entry.prefab, entry.count, entry.zoneIndex, entryIndex);
                break;
            case SpawnMode.Grid:
                SpawnGrid(entry.prefab, entry.count, entry.zoneIndex, entryIndex);
                break;
            case SpawnMode.OpposingClusters:
                SpawnOpposingClusters(entry.prefab, entry.count, specificCentre, entryIndex, entry.zoneIndex);
                break;
            case SpawnMode.Nebula:
                SpawnNebula(entry.prefab, entry.count, specificCentre, entryIndex, entry.zoneIndex);
                break;
            case SpawnMode.Archipelago:
                SpawnArchipelago(entry.prefab, entry.count, specificCentre, entryIndex, entry.zoneIndex);
                break;
            case SpawnMode.FlowingRiver:
                SpawnFlowingRiver(entry.prefab, entry.count, entry.zoneIndex, entryIndex);
                break;
            case SpawnMode.BoxSurface:
                SpawnBoxSurface(entry.prefab, entry.count, entry.zoneIndex, entryIndex);
                break;
        }
    }

    private void CalculateZoneCentres()
    {
        if (Bounds.size == Vector3.zero) return;
        Vector3 size = Bounds.size;
        Vector3 quarter = size / 4f;
        Vector3 centrePoint = Bounds.center;

        zones[0].centre = centrePoint + new Vector3(-quarter.x, -quarter.y, -quarter.z);
        zones[1].centre = centrePoint + new Vector3( quarter.x, -quarter.y, -quarter.z);
        zones[2].centre = centrePoint + new Vector3(-quarter.x,  quarter.y, -quarter.z);
        zones[3].centre = centrePoint + new Vector3( quarter.x,  quarter.y, -quarter.z);
        zones[4].centre = centrePoint + new Vector3(-quarter.x, -quarter.y,  quarter.z);
        zones[5].centre = centrePoint + new Vector3( quarter.x, -quarter.y,  quarter.z);
        zones[6].centre = centrePoint + new Vector3(-quarter.x,  quarter.y,  quarter.z);
        zones[7].centre = centrePoint + new Vector3( quarter.x,  quarter.y,  quarter.z);
    }

    #region Spawning Methods

    private void SpawnCluster(GameObject prefab, int count, Vector3? specificCentre, int entryIndex, int zoneIndex)
    {
        Vector3 spawnPoint = specificCentre ?? GetRandomPointInBounds(spawnRadius);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = spawnPoint + Random.insideUnitSphere * spawnRadius;
            InstantiateAgent(prefab, pos, entryIndex, zoneIndex);
        }
    }

    private void SpawnUniform(GameObject prefab, int count, int zoneIndex, int entryIndex)
    {
        if (zoneIndex >= 0 && zoneIndex < zones.Length)
        {
            for (int i = 0; i < count; i++)
                InstantiateAgent(prefab, GetRandomPointInZone(zoneIndex, 0f), entryIndex, zoneIndex);
        }
        else
        {
            for (int i = 0; i < count; i++)
                InstantiateAgent(prefab, GetRandomPointInBounds(0f), entryIndex, -1);
        }
    }

    private void SpawnShell(GameObject prefab, int count, Vector3? specificCentre, int entryIndex, int zoneIndex)
    {
        Vector3 spawnPoint = specificCentre ?? GetRandomPointInBounds(spawnRadius);
        for (int i = 0; i < count; i++)
            InstantiateAgent(prefab, spawnPoint + Random.onUnitSphere * spawnRadius, entryIndex, zoneIndex);
    }

    private void SpawnWall(GameObject prefab, int count, int zoneIndex, int entryIndex)
    {
        Bounds targetBounds = (zoneIndex >= 0 && zoneIndex < zones.Length)
            ? new Bounds(zones[zoneIndex].centre, Bounds.size / 2f)
            : Bounds;

        int axis = Random.Range(0, 3);
        float coord = Random.Range(targetBounds.min[axis], targetBounds.max[axis]);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(
                axis == 0 ? coord : Random.Range(targetBounds.min.x, targetBounds.max.x),
                axis == 1 ? coord : Random.Range(targetBounds.min.y, targetBounds.max.y),
                axis == 2 ? coord : Random.Range(targetBounds.min.z, targetBounds.max.z)
            );
            InstantiateAgent(prefab, pos, entryIndex, zoneIndex);
        }
    }

    private void SpawnGrid(GameObject prefab, int count, int zoneIndex, int entryIndex)
    {
        Bounds targetBounds = (zoneIndex >= 0 && zoneIndex < zones.Length)
            ? new Bounds(zones[zoneIndex].centre, Bounds.size / 2f)
            : Bounds;

        int perSide = Mathf.CeilToInt(Mathf.Pow(count, 1f / 3f));
        if (perSide <= 1)
        {
            SpawnCluster(prefab, count, targetBounds.center, entryIndex, zoneIndex);
            return;
        }
        Vector3 step = targetBounds.size / (perSide - 1);
        int spawned = 0;

        for (int x = 0; x < perSide; x++)
            for (int y = 0; y < perSide; y++)
                for (int z = 0; z < perSide; z++)
                {
                    if (spawned++ >= count) return;
                    Vector3 pos = targetBounds.min + new Vector3(x * step.x, y * step.y, z * step.z);
                    InstantiateAgent(prefab, pos, entryIndex, zoneIndex);
                }
    }

    private void SpawnOpposingClusters(GameObject prefab, int count, Vector3? specificCentre, int entryIndex, int zoneIndex)
    {
        Vector3 centrePoint = specificCentre ?? Bounds.center;
        int half = count / 2;
        Vector3 ext = Bounds.extents * 0.75f;
        Vector3 offset = Random.onUnitSphere * ext.magnitude;
        Vector3 c1 = centrePoint - offset;
        Vector3 c2 = centrePoint + offset;
        SpawnCluster(prefab, half, c1, entryIndex, zoneIndex);
        SpawnCluster(prefab, count - half, c2, entryIndex, zoneIndex);
    }

    private void SpawnNebula(GameObject prefab, int count, Vector3? specificCentre, int entryIndex, int zoneIndex)
    {
        Vector3 nebulaCentre = specificCentre ?? GetRandomPointInBounds(spawnRadius);
        for (int i = 0; i < count; i++)
        {
            Vector3 jitter = Random.value * Random.insideUnitSphere;
            InstantiateAgent(prefab, nebulaCentre + jitter, entryIndex, zoneIndex);
        }
    }

    private void SpawnArchipelago(GameObject prefab, int count, Vector3? specificCentre, int entryIndex, int zoneIndex)
    {
        if (archipelagoIslandCount <= 0) return;
        int perIsland = Mathf.Max(1, count / archipelagoIslandCount);
        for (int i = 0; i < archipelagoIslandCount; i++)
        {
            int thisCount = (i == archipelagoIslandCount - 1)
                ? (count - perIsland * i)
                : perIsland;
            SpawnCluster(prefab, thisCount, specificCentre, entryIndex, zoneIndex);
        }
    }

    private void SpawnFlowingRiver(GameObject prefab, int count, int zoneIndex, int entryIndex)
    {
        Bounds targetBounds = (zoneIndex >= 0 && zoneIndex < zones.Length)
            ? new Bounds(zones[zoneIndex].centre, Bounds.size / 2f)
            : Bounds;

        int axis = Random.Range(0, 3);
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0f;
            Vector3 p = targetBounds.min;
            p[axis] += t * targetBounds.size[axis];
            p[(axis + 1) % 3] = targetBounds.center[(axis + 1) % 3] + Mathf.Sin(t * Mathf.PI * riverFrequency) * riverAmplitude;
            p[(axis + 2) % 3] = targetBounds.center[(axis + 2) % 3] + Mathf.Cos(t * Mathf.PI * riverFrequency) * riverAmplitude;
            InstantiateAgent(prefab, p + Random.insideUnitSphere * 0.5f, entryIndex, zoneIndex);
        }
    }

    private void SpawnBoxSurface(GameObject prefab, int count, int zoneIndex, int entryIndex)
    {
        Bounds targetBounds = (zoneIndex >= 0 && zoneIndex < zones.Length)
            ? new Bounds(zones[zoneIndex].centre, Bounds.size / 2f)
            : Bounds;

        for (int i = 0; i < count; i++)
        {
            int face = Random.Range(0, 6);
            float u = Random.value, v = Random.value;
            Vector3 pos = Vector3.zero;
            switch (face)
            {
                case 0: pos = new Vector3(targetBounds.min.x, Lerp(targetBounds.min.y, targetBounds.max.y, u), Lerp(targetBounds.min.z, targetBounds.max.z, v)); break;
                case 1: pos = new Vector3(targetBounds.max.x, Lerp(targetBounds.min.y, targetBounds.max.y, u), Lerp(targetBounds.min.z, targetBounds.max.z, v)); break;
                case 2: pos = new Vector3(Lerp(targetBounds.min.x, targetBounds.max.x, u), targetBounds.min.y, Lerp(targetBounds.min.z, targetBounds.max.z, v)); break;
                case 3: pos = new Vector3(Lerp(targetBounds.min.x, targetBounds.max.x, u), targetBounds.max.y, Lerp(targetBounds.min.z, targetBounds.max.z, v)); break;
                case 4: pos = new Vector3(Lerp(targetBounds.min.x, targetBounds.max.x, u), Lerp(targetBounds.min.y, targetBounds.max.y, v), targetBounds.min.z); break;
                case 5: pos = new Vector3(Lerp(targetBounds.min.x, targetBounds.max.x, u), Lerp(targetBounds.min.y, targetBounds.max.y, v), targetBounds.max.z); break;
            }
            InstantiateAgent(prefab, pos, entryIndex, zoneIndex);
        }
    }

    #endregion

    #region Helpers

    private void InstantiateAgent(GameObject prefab, Vector3 pos, int entryIndex, int zoneIndex)
    {
        Vector3 finalPos = Bounds.ClosestPoint(pos);
        var go = Instantiate(prefab, finalPos, Random.rotation, transform);
        var tracker = go.AddComponent<SpawnerTracker>();
        tracker.entryIndex = entryIndex;
        tracker.zoneIndex = zoneIndex;
        NutrientField.Instance?.InjectSource(finalPos, 1f);
    }

    private Vector3 GetRandomPointInBounds(float margin)
    {
        return new Vector3(
            Random.Range(Bounds.min.x + margin, Bounds.max.x - margin),
            Random.Range(Bounds.min.y + margin, Bounds.max.y - margin),
            Random.Range(Bounds.min.z + margin, Bounds.max.z - margin)
        );
    }

    /// <summary>
    /// Returns a random point within the specified zone's bounds, respecting a margin.
    /// </summary>
    private Vector3 GetRandomPointInZone(int zoneIndex, float margin)
    {
        Bounds zoneBounds = new Bounds(zones[zoneIndex].centre, Bounds.size / 2f);
        return new Vector3(
            Random.Range(zoneBounds.min.x + margin, zoneBounds.max.x - margin),
            Random.Range(zoneBounds.min.y + margin, zoneBounds.max.y - margin),
            Random.Range(zoneBounds.min.z + margin, zoneBounds.max.z - margin)
        );
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    #endregion

    #region Editor Visualisation

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        if (Bounds.size == Vector3.zero && universeContainer != null)
        {
            var renderer = universeContainer.GetComponent<Renderer>();
            if (renderer != null)
                Bounds = renderer.bounds;
        }

        CalculateZoneCentres();
        if (zones == null || zones.Length != 8) return;

        Gizmos.color = Color.yellow;
        DrawInterpolationCube();

        for (int i = 0; i < 8; i++)
        {
#if UNITY_EDITOR
            Handles.Label(zones[i].centre,
                $"Zone {i}\nTemp:{zones[i].temperature:F1}° pH:{zones[i].pH:F1}\nUV:{zones[i].uvLightIntensity:F2} Tox:{zones[i].toxinFieldStrength:F2}");
#endif
        }
    }

    private void DrawInterpolationCube()
    {
        Gizmos.DrawLine(zones[0].centre, zones[1].centre);
        Gizmos.DrawLine(zones[1].centre, zones[5].centre);
        Gizmos.DrawLine(zones[5].centre, zones[4].centre);
        Gizmos.DrawLine(zones[4].centre, zones[0].centre);
        Gizmos.DrawLine(zones[2].centre, zones[3].centre);
        Gizmos.DrawLine(zones[3].centre, zones[7].centre);
        Gizmos.DrawLine(zones[7].centre, zones[6].centre);
        Gizmos.DrawLine(zones[6].centre, zones[2].centre);
        Gizmos.DrawLine(zones[0].centre, zones[2].centre);
        Gizmos.DrawLine(zones[1].centre, zones[3].centre);
        Gizmos.DrawLine(zones[4].centre, zones[6].centre);
        Gizmos.DrawLine(zones[5].centre, zones[7].centre);
    }

    #endregion
}

/// <summary>
/// Tracks which PrefabEntry and zone each instantiated agent belongs to,
/// so the Universe can monitor and refill populations as needed.
/// </summary>
public class SpawnerTracker : MonoBehaviour
{
    public int entryIndex;
    public int zoneIndex;
}
