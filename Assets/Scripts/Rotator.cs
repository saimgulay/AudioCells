using UnityEngine;

/// <summary>
/// Rotates the attached GameObject around its local Y axis at a configurable speed.
/// </summary>
public class Rotator : MonoBehaviour
{
    [Header("Rotation Speed")]
    [Tooltip("Degrees per second to rotate around the Y axis.")]
    public float rotationSpeed = 45f;

    void Update()
    {
        // Rotate around local Y axis at rotationSpeed degrees per second
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
