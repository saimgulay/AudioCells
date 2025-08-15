using UnityEngine;

/// <summary>
/// Simulates a simple, free-floating (planktonic) algae agent.
/// It performs photosynthesis using UV light to gain energy, reproduces,
/// and turns into a nutrient source upon death, creating the base of the food chain.
/// This script does not use a Rigidbody.
/// </summary>
public class Algae_Plankton : MonoBehaviour
{
    [Header("Life Cycle Parameters")]
    [Tooltip("The initial energy the algae starts with.")]
    public float initialEnergy = 5f;
    [Tooltip("The energy level required for the algae to divide.")]
    public float reproductionThreshold = 10f;
    [Tooltip("The constant energy cost for staying alive.")]
    public float metabolismRate = 0.1f;
    [Tooltip("The maximum age in seconds before the algae dies.")]
    public float maxAge = 500f;
    [Tooltip("How efficiently the algae converts UV light into energy.")]
    public float photosynthesisEfficiency = 0.5f;

    [Header("Death Settings")]
    [Tooltip("The nutrient prefab that spawns when this algae dies.")]
    public GameObject nutrientPrefabOnDeath;

    [Header("Movement")]
    [Tooltip("How fast the algae drifts randomly. Set to 0 for no movement.")]
    public float driftSpeed = 0.05f;

    // --- Internal State ---
    private float energy;
    private float age;
    private Vector3 driftDirection;
    private float driftChangeTimer;
    private Universe universeRef;

    // --- Static Counters for this Agent Type ---
    public static int aliveCount;
    public static int totalCreated;

    #region Unity Messages

    void OnEnable()
    {
        aliveCount++;
        totalCreated++;
    }

    void OnDisable()
    {
        aliveCount--;
    }

    void Start()
    {
        energy = initialEnergy;
        age = 0f;
        universeRef = FindObjectOfType<Universe>();

        // Set an initial random drift direction
        ChangeDriftDirection();
    }

    void Update()
    {
        float timeScale = (EnvironmentManager.Instance != null) ? EnvironmentManager.Instance.timeScale : 1f;
        float dt = Time.deltaTime * timeScale;

        // 1. Gain Energy via Photosynthesis
        if (EnvironmentManager.Instance != null)
        {
            var localEnvironment = EnvironmentManager.Instance.GetEnvironmentAtPoint(transform.position);
            float energyGained = localEnvironment.uvLightIntensity * photosynthesisEfficiency * dt;
            energy += energyGained;
        }

        // 2. Lose Energy via Metabolism
        energy -= metabolismRate * dt;

        // 3. Age and check for death
        age += dt;
        if (energy <= 0 || age >= maxAge)
        {
            Die();
            return; // Stop further execution for this frame
        }

        // 4. Check for reproduction
        if (energy >= reproductionThreshold)
        {
            Divide();
        }
        
        // 5. Handle Movement
        Drift(dt);
        if (universeRef != null)
        {
            KeepInBounds();
        }
    }
    
    #endregion

    #region Core Logic

    /// <summary>
    /// The process of dividing into two identical algae cells.
    /// </summary>
    private void Divide()
    {
        // Halve the energy and share it with the child.
        energy *= 0.5f;

        // Create the new algae at a slight offset.
        Vector3 offset = Random.insideUnitSphere * 0.5f;
        Instantiate(gameObject, transform.position + offset, Quaternion.identity, transform.parent);
    }

    /// <summary>
    /// The process of dying and leaving behind a nutrient source.
    /// </summary>
    private void Die()
    {
        // If a nutrient prefab is assigned, spawn it at our position.
        if (nutrientPrefabOnDeath != null)
        {
            Instantiate(nutrientPrefabOnDeath, transform.position, Quaternion.identity, transform.parent);
        }

        // Destroy the algae object.
        Destroy(gameObject);
    }

    /// <summary>
    /// Simulates passive drifting in the water.
    /// </summary>
    private void Drift(float dt)
    {
        // Periodically change the random drift direction to simulate shifting currents.
        driftChangeTimer += dt;
        if (driftChangeTimer > 5.0f) // Change direction every 5 seconds
        {
            ChangeDriftDirection();
            driftChangeTimer = 0f;
        }

        transform.position += driftDirection * driftSpeed * dt;
    }
    
    /// <summary>
    /// Selects a new random direction for drifting.
    /// </summary>
    private void ChangeDriftDirection()
    {
        driftDirection = Random.onUnitSphere;
    }
    
    /// <summary>
    /// Ensures the algae stays within the universe bounds.
    /// </summary>
    private void KeepInBounds()
    {
        // A simple version that just clamps the position.
        // This is less bouncy than the E. coli version, fitting for passive plankton.
        transform.position = universeRef.Bounds.ClosestPoint(transform.position);
    }

    #endregion
}
