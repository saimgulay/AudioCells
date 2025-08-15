// Assets/Scripts/SequentialPrefabSpawner.cs
// Shows prefabs one-by-one. Press Return to destroy current and spawn the next.
// After the last prefab is destroyed, waits X seconds and loads the specified scene.
// British English comments and logs.

using UnityEngine;
using UnityEngine.SceneManagement;

public class SequentialPrefabSpawner : MonoBehaviour
{
    [Header("Prefabs to Spawn (in order)")]
    [Tooltip("Assign your prefabs in the order they should appear.")]
    public GameObject[] prefabs;

    [Header("Parent for Spawned Objects")]
    [Tooltip("New instances will be parented under this Transform (optional).")]
    public Transform parentTransform;

    [Header("Local Spawn Positions")]
    [Tooltip("Must match the length of 'prefabs'. Local positions relative to parent.")]
    public Vector3[] localSpawnPositions;

    [Header("Local Spawn Rotations (Euler)")]
    [Tooltip("Must match the length of 'prefabs'. Local Euler rotations relative to parent.")]
    public Vector3[] localSpawnRotations;

    [Header("Next Scene")]
    [Tooltip("Scene name to load after the last prefab is destroyed (must be added to Build Settings). Leave empty to do nothing.")]
    public string nextSceneName = "";

    [Tooltip("Delay in seconds after the last prefab is destroyed before loading the next scene.")]
    [Min(0f)] public float delayAfterLast = 2f;

    private int currentIndex = -1;
    private GameObject currentInstance;

    private bool sequenceCompleted = false;
    private bool loadTriggered = false;

    void Start()
    {
        if (!ValidateConfig())
            return;

        SpawnNext(); // spawn the first at scene start
    }

    void Update()
    {
        if (sequenceCompleted)
            return;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (currentInstance != null)
            {
                Destroy(currentInstance);
                currentInstance = null;
            }
            SpawnNext();
        }
    }

    bool ValidateConfig()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("Assign at least one prefab.");
            return false;
        }
        if (localSpawnPositions == null || localSpawnPositions.Length != prefabs.Length)
        {
            Debug.LogError("localSpawnPositions length must match prefabs length.");
            return false;
        }
        if (localSpawnRotations == null || localSpawnRotations.Length != prefabs.Length)
        {
            Debug.LogError("localSpawnRotations length must match prefabs length.");
            return false;
        }
        return true;
    }

    void SpawnNext()
    {
        currentIndex++;

        if (currentIndex < prefabs.Length)
        {
            GameObject prefabToSpawn = prefabs[currentIndex];

            // Instantiate as child if parent is set
            currentInstance = parentTransform != null
                ? Instantiate(prefabToSpawn, parentTransform)
                : Instantiate(prefabToSpawn);

            // Apply local transform relative to parent
            currentInstance.transform.localPosition = localSpawnPositions[currentIndex];
            currentInstance.transform.localRotation = Quaternion.Euler(localSpawnRotations[currentIndex]);
            currentInstance.transform.localScale   = Vector3.one;
        }
        else
        {
            // Sequence finished; last one was already destroyed on the final Return press.
            currentInstance = null;
            sequenceCompleted = true;
            Debug.Log("All prefabs have been shown and destroyed.");

            if (!loadTriggered && !string.IsNullOrEmpty(nextSceneName))
            {
                loadTriggered = true;
                StartCoroutine(LoadSceneAfterDelay());
            }
        }
    }

    System.Collections.IEnumerator LoadSceneAfterDelay()
    {
        if (delayAfterLast > 0f)
            yield return new WaitForSeconds(delayAfterLast);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("Next scene name is empty; staying on the current scene.");
        }
    }
}
