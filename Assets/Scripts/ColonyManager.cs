// EvolutionManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class EvolutionManager : MonoBehaviour
{
    [Header("Prefabs & Layout")]
    public GameObject spherePrefab;
    public GameObject planePrefab;
    [Tooltip("Grid hücreleri arasındaki mesafe")]
    public float cellSpacing = 2f;
    [Tooltip("Kürenin zeminin biraz üstünde duracağı Y offset")]
    public float sphereYOffset = 0.5f;
    [Tooltip("Plane objesinin rotasyonu (Euler açıları)")]
    public Vector3 planeRotation = new Vector3(90f, 0f, 0f);

    [Header("Population")]
    public int populationSize = 20;
    public float evaluationTime = 5f;

    [Header("Genetic Algorithm")]
    [Range(1,20)] public int elitism = 4;
    [Range(0f,1f)] public float mutationRate = 0.1f;
    public float mutationAmount = 0.2f;

    [Header("Noise Param Ranges")]
    public float freqMin   = 0.5f, freqMax   = 3f;
    public float ampMin    = 0.1f, ampMax    = 1f;
    public float speedMin  = 0f,   speedMax  = 2f;
    public float offsetMin = 0f,   offsetMax = 2f * Mathf.PI;

    [Serializable]
    public class RegionSettingsWrapper
    {
        public SphereNoiseDeformer.RegionNoiseSettings[] settings;
    }

    class Individual
    {
        public GameObject sphere;
        public GameObject plane;
        public SphereNoiseDeformer.RegionNoiseSettings[] settings;
        public float fitness;
        public Rigidbody rb;
        public SphereNoiseDeformer deformer;
        public Vector3 gridPos;
    }

    List<Individual> population = new List<Individual>();
    int generation = 0;

    void Start()
    {
        SpawnInitialPopulation();
        StartCoroutine(EvaluateAndEvolve());
    }

    void SpawnInitialPopulation()
    {
        population.Clear();

        // Grid boyutunu hesapla
        int cols = Mathf.CeilToInt(Mathf.Sqrt(populationSize));
        int rows = Mathf.CeilToInt(populationSize / (float)cols);

        for (int i = 0; i < populationSize; i++)
        {
            int row = i / cols;
            int col = i % cols;
            Vector3 basePos = new Vector3(col * cellSpacing, 0f, row * cellSpacing);

            // Plane
            var plane = Instantiate(
                planePrefab,
                basePos,
                Quaternion.Euler(planeRotation),
                transform
            );

            // Sphere
            Vector3 spherePos = basePos + Vector3.up * sphereYOffset;
            var sphere = Instantiate(
                spherePrefab,
                spherePos,
                Quaternion.identity,
                transform
            );

            var ind = new Individual
            {
                plane      = plane,
                sphere     = sphere,
                gridPos    = basePos,
                settings   = RandomSettings(),
                fitness    = 0f,
                rb         = sphere.GetComponent<Rigidbody>(),
                deformer   = sphere.GetComponent<SphereNoiseDeformer>()
            };

            ind.deformer.ApplyRegionSettings(ind.settings);
            population.Add(ind);
        }
    }

    IEnumerator EvaluateAndEvolve()
    {
        while (true)
        {
            generation++;
            Debug.Log($"=== Generation {generation} ===");

            yield return new WaitForSeconds(evaluationTime);

            // Fitness hesapla
            foreach (var ind in population)
            {
                Vector3 delta = ind.sphere.transform.position -
                                (ind.gridPos + Vector3.up * sphereYOffset);
                ind.fitness = new Vector2(delta.x, delta.z).magnitude;
                Debug.Log($"  Fitness: {ind.fitness:F2}");
            }

            // Yeni nesil & kaydet
            CreateNextGeneration();

            // Reset ile grid pozisyonuna döndür
            ResetPopulation();
        }
    }

    void CreateNextGeneration()
    {
        population.Sort((a, b) => b.fitness.CompareTo(a.fitness));
        SaveBestSettings(population[0]);

        var elites = population.Take(elitism)
                               .Select(ind => ind.settings)
                               .ToList();

        var newSettings = new List<SphereNoiseDeformer.RegionNoiseSettings[]>(elites);
        while (newSettings.Count < populationSize)
        {
            var A = elites[UnityEngine.Random.Range(0, elites.Count)];
            var B = elites[UnityEngine.Random.Range(0, elites.Count)];
            var child = Crossover(A, B);
            Mutate(child);
            newSettings.Add(child);
        }

        for (int i = 0; i < populationSize; i++)
            population[i].settings = newSettings[i];
    }

    void ResetPopulation()
    {
        foreach (var ind in population)
        {
            // Plane'i pozisyon ve rotasyona set et
            ind.plane.transform.SetPositionAndRotation(
                ind.gridPos,
                Quaternion.Euler(planeRotation)
            );

            // Sphere'i grid pozisyonu + Y offset ile set et
            Vector3 spherePos = ind.gridPos + Vector3.up * sphereYOffset;
            ind.sphere.transform.SetPositionAndRotation(
                spherePos,
                Quaternion.identity
            );

            ind.rb.linearVelocity        = Vector3.zero;
            ind.rb.angularVelocity = Vector3.zero;
            ind.deformer.ApplyRegionSettings(ind.settings);
        }
    }

    void SaveBestSettings(Individual best)
    {
        var wrapper = new RegionSettingsWrapper { settings = best.settings };
        string json = JsonUtility.ToJson(wrapper, true);
        string path = Path.Combine(Application.persistentDataPath, "bestSettings.json");
        File.WriteAllText(path, json);
        Debug.Log($"[EvolutionManager] En iyi ayarlar kaydedildi: {path}");
    }

    SphereNoiseDeformer.RegionNoiseSettings[] RandomSettings()
    {
        int R = 8;
        var s = new SphereNoiseDeformer.RegionNoiseSettings[R];
        for (int i = 0; i < R; i++)
        {
            s[i].regionIndex    = i;
            s[i].noiseFrequency = UnityEngine.Random.Range(freqMin, freqMax);
            s[i].noiseAmplitude = UnityEngine.Random.Range(ampMin, ampMax);
            s[i].phaseSpeed     = UnityEngine.Random.Range(speedMin, speedMax);
            s[i].noiseOffset    = UnityEngine.Random.Range(offsetMin, offsetMax);
        }
        return s;
    }

    SphereNoiseDeformer.RegionNoiseSettings[] Crossover(
        SphereNoiseDeformer.RegionNoiseSettings[] A,
        SphereNoiseDeformer.RegionNoiseSettings[] B)
    {
        int R = A.Length;
        var C = new SphereNoiseDeformer.RegionNoiseSettings[R];
        int cp = UnityEngine.Random.Range(1, R);
        for (int i = 0; i < R; i++)
            C[i] = (i < cp) ? A[i] : B[i];
        return C;
    }

    void Mutate(SphereNoiseDeformer.RegionNoiseSettings[] S)
    {
        for (int i = 0; i < S.Length; i++)
        {
            if (UnityEngine.Random.value < mutationRate)
                S[i].noiseFrequency = Mathf.Clamp(
                    S[i].noiseFrequency + UnityEngine.Random.Range(-mutationAmount, mutationAmount),
                    freqMin, freqMax);
            if (UnityEngine.Random.value < mutationRate)
                S[i].noiseAmplitude = Mathf.Clamp(
                    S[i].noiseAmplitude + UnityEngine.Random.Range(-mutationAmount, mutationAmount),
                    ampMin, ampMax);
            if (UnityEngine.Random.value < mutationRate)
                S[i].phaseSpeed = Mathf.Clamp(
                    S[i].phaseSpeed + UnityEngine.Random.Range(-mutationAmount, mutationAmount),
                    speedMin, speedMax);
            if (UnityEngine.Random.value < mutationRate)
                S[i].noiseOffset = UnityEngine.Random.Range(offsetMin, offsetMax);
        }
    }
}
