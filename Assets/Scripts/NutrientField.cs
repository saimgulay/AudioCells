// NutrientField.cs
using UnityEngine;

/// <summary>
/// Maintains a 3D grid of nutrient concentration within the Universe bounds,
/// applies simple diffusion each frame, and allows agents to Consume or external
/// sources to Inject nutrients at world positions.
/// </summary>
public class NutrientField : MonoBehaviour
{
    public static NutrientField Instance { get; private set; }

    [Header("Grid Settings")]
    public Bounds worldBounds;      // set by Universe
    public int resolution = 32;     // grid size per axis
    [Range(0f, 1f)]
    public float diffusionRate = 0.1f;

    float[,,] grid;
    Vector3 cellSize;

    void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;

        grid = new float[resolution, resolution, resolution];
        cellSize = worldBounds.size / resolution;

        // Initialize to zero concentration
        for (int x = 0; x < resolution; x++)
            for (int y = 0; y < resolution; y++)
                for (int z = 0; z < resolution; z++)
                    grid[x, y, z] = 0f;
    }

    void Update()
    {
        Diffuse();
    }

    void Diffuse()
    {
        var tmp = new float[resolution, resolution, resolution];

        for (int x = 1; x < resolution - 1; x++)
        for (int y = 1; y < resolution - 1; y++)
        for (int z = 1; z < resolution - 1; z++)
        {
            float sum =
                grid[x + 1, y, z] + grid[x - 1, y, z] +
                grid[x, y + 1, z] + grid[x, y - 1, z] +
                grid[x, y, z + 1] + grid[x, y, z - 1];
            tmp[x, y, z] = Mathf.Lerp(grid[x, y, z], sum / 6f, diffusionRate * Time.deltaTime);
        }

        for (int x = 1; x < resolution - 1; x++)
        for (int y = 1; y < resolution - 1; y++)
        for (int z = 1; z < resolution - 1; z++)
            grid[x, y, z] = tmp[x, y, z];
    }

    public float GetConcentration(Vector3 worldPos)
    {
        var idx = WorldToGrid(worldPos);
        return grid[idx.x, idx.y, idx.z];
    }

    public void Consume(Vector3 worldPos, float amount)
    {
        var idx = WorldToGrid(worldPos);
        grid[idx.x, idx.y, idx.z] = Mathf.Max(0f, grid[idx.x, idx.y, idx.z] - amount);
    }

    public void InjectSource(Vector3 worldPos, float amount)
    {
        var idx = WorldToGrid(worldPos);
        grid[idx.x, idx.y, idx.z] += amount;
    }

    Vector3Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - worldBounds.min;
        int ix = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize.x), 0, resolution - 1);
        int iy = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize.y), 0, resolution - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt(local.z / cellSize.z), 0, resolution - 1);
        return new Vector3Int(ix, iy, iz);
    }
}
