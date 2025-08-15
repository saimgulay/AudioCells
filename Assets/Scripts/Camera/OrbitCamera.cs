using UnityEngine;

/// <summary>
/// Orbits the camera around a target object on its Y axis,
/// while always looking at the target.
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [Tooltip("Transform of the object to orbit around")]
    public Transform target;

    [Tooltip("Speed of orbit in degrees per second")]
    public float orbitSpeed = 30f;

    [Tooltip("Initial offset from the target (e.g. height and distance)")]
    public Vector3 offset = new Vector3(0f, 5f, -10f);

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("OrbitCamera: No target assigned!");
            enabled = false;
            return;
        }

        // Position camera at the initial offset relative to target
        transform.position = target.position + offset;
        // Ensure the camera is looking at the target
        transform.LookAt(target);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Orbit around the target's Y axis
        transform.RotateAround(
            target.position,         // point to rotate around
            Vector3.up,              // axis (Y axis)
            orbitSpeed * Time.deltaTime  // angle in degrees per frame
        );

        // After moving, always look at the target
        transform.LookAt(target);
    }
}
