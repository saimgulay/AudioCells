// SphereNoiseDeformer.cs
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(MeshCollider))]
[ExecuteAlways]
public class SphereNoiseDeformer : MonoBehaviour
{
    [System.Serializable]
    public struct RegionNoiseSettings
    {
        [Tooltip("0=(-,-,-),1=(+,-,-),…7=(+,+,+)")]
        public int    regionIndex;
        [Tooltip("Gürültünün detay yoğunluğu")]
        public float  noiseFrequency;
        [Tooltip("Gürültü genliği")]
        public float  noiseAmplitude;
        [Tooltip("Animasyon hızı (phase çarpanı)")]
        public float  phaseSpeed;
        [Tooltip("Phase’e ek ofset (statik kaydırma)")]
        public float  noiseOffset;
    }

    [Header("Per-Octant Noise Settings (8 regions)")]
    [Tooltip("Index = (x>=0?1:0)+(y>=0?2:0)+(z>=0?4:0)")]
    public RegionNoiseSettings[] regionSettings = new RegionNoiseSettings[8];

    [Header("Object Scale")]
    public Vector3 objectScale = Vector3.one;

    [Header("Update Options")]
    public bool updateContinuously = true;

    Mesh         mesh;
    Vector3[]    baseVertices;
    Vector3[]    displacedVertices;
    MeshCollider meshCollider;
    Rigidbody    rb;
    MeshFilter   meshFilter;

    void OnEnable()
    {
        Initialize();
        if (!Application.isPlaying && updateContinuously)
            DeformMesh();
    }

    void Initialize()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (mesh == null && meshFilter.sharedMesh != null)
        {
            mesh = Instantiate(meshFilter.sharedMesh);
            mesh.name = meshFilter.sharedMesh.name + "_Deformed";
            meshFilter.sharedMesh = mesh;
            baseVertices     = mesh.vertices;
            displacedVertices = new Vector3[baseVertices.Length];
        }

        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh       = mesh;
        meshCollider.convex           = true;

        if (rb == null)
            rb = GetComponent<Rigidbody>();
        rb.interpolation             = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode    = CollisionDetectionMode.ContinuousDynamic;
    }

    void Update()
    {
        if (updateContinuously)
        {
            Initialize();
            DeformMesh();
        }
    }

    void DeformMesh()
    {
        if (mesh == null || baseVertices == null) return;

        float t = Time.time;
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 scaled = Vector3.Scale(baseVertices[i], objectScale);
            int ix = scaled.x >= 0f ? 1 : 0;
            int iy = scaled.y >= 0f ? 2 : 0;
            int iz = scaled.z >= 0f ? 4 : 0;
            int region = ix + iy + iz;

            var rs = regionSettings[region];
            float phase = t * rs.phaseSpeed + rs.noiseOffset;

            float n1 = Mathf.PerlinNoise((scaled.x + phase) * rs.noiseFrequency,
                                         (scaled.y + phase) * rs.noiseFrequency);
            float n2 = Mathf.PerlinNoise((scaled.y + phase) * rs.noiseFrequency,
                                         (scaled.z + phase) * rs.noiseFrequency);
            float n3 = Mathf.PerlinNoise((scaled.z + phase) * rs.noiseFrequency,
                                         (scaled.x + phase) * rs.noiseFrequency);
            float noiseValue = (n1 + n2 + n3) / 3f;

            Vector3 normal = baseVertices[i].normalized;
            displacedVertices[i] = scaled + normal * (noiseValue * rs.noiseAmplitude);
        }

        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshCollider.sharedMesh = mesh;
    }

    /// <summary>
    /// Dışarıdan tüm region ayarlarını topluca set etmek için.
    /// </summary>
    public void ApplyRegionSettings(RegionNoiseSettings[] settings)
    {
        if (settings == null || settings.Length != regionSettings.Length)
        {
            Debug.LogError($"RegionSettings.Length must be {regionSettings.Length}");
            return;
        }
        regionSettings = settings;
    }
}
