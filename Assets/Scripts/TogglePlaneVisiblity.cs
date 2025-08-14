using UnityEngine;

public class ToggleSelfRenderer : MonoBehaviour
{
    private MeshRenderer rendererComponent;
    private bool isVisible = false; // Başlangıçta görünmez

    void Start()
    {
        rendererComponent = GetComponent<MeshRenderer>();
        if (rendererComponent != null)
        {
            rendererComponent.enabled = isVisible;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && rendererComponent != null)
        {
            isVisible = !isVisible;
            rendererComponent.enabled = isVisible;
        }
    }
}
