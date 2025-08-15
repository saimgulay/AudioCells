/*
 * SphereControllerSliders.cs
 *
 * Reads 5 UI sliders each frame, feeds the values into RTMLCore,
 * then applies the 12-element output vector as RGBA to 3 sphere materials.
 * Also allows you to set each sphere's RGBA colour directly via the Inspector
 * when not in real-time ML run mode.
 * Press R to record (input→current colours), T to train, P to start/stop real-time prediction.
 */

using UnityEngine;
using UnityEngine.UI;
using RTMLToolKit;

public class SphereControllerSliders : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to your RTMLRunner's RTMLCore component.")]
    public RTMLCore rtmlCore;

    [Tooltip("Five UI sliders providing input features.")]
    public Slider[] sliders = new Slider[5];

    [Tooltip("Materials for the three spheres to control.")]
    public Material[] sphereMats = new Material[3];

    [Header("Inspector Sphere Colours")]
    [Tooltip("Direct RGBA colours for each sphere when not in run mode.")]
    public Color[] inspectorColours = new Color[3]
    {
        new Color(1,1,1,1),  // Sphere 1 initial white
        new Color(1,1,1,1),  // Sphere 2 initial white
        new Color(1,1,1,1)   // Sphere 3 initial white
    };

    [Header("Keyboard Shortcuts")]
    [Tooltip("Key to record current sliders→sphere mapping.")]
    public KeyCode recordKey = KeyCode.R;

    [Tooltip("Key to train the ML model on recorded samples.")]
    public KeyCode trainKey  = KeyCode.T;

    [Tooltip("Key to toggle real-time ML prediction.")]
    public KeyCode predictKey = KeyCode.P;

    void Start()
    {
        // Ensure RTMLCore is in direct-API mode and recording enabled
        // Make sure in the Inspector that RTMLCore.inputSize = 5 and outputSize = 12
        rtmlCore.enableRun    = false;
        rtmlCore.enableRecord = true;
    }

    void Update()
    {
        // If not in ML run mode, apply Inspector colours directly
        if (!rtmlCore.enableRun)
        {
            ApplyInspectorColours();
        }

        // 1) Record sample
        if (Input.GetKeyDown(recordKey))
        {
            float[] input  = ReadSliders();
            float[] output = ReadSphereColours();
            rtmlCore.RecordSample(input, output);
            Debug.Log("[SphereController] Recorded sample.");
        }

        // 2) Train model
        if (Input.GetKeyDown(trainKey))
        {
            rtmlCore.TrainModel();
            Debug.Log("[SphereController] Model trained.");
        }

        // 3) Toggle real-time prediction
        if (Input.GetKeyDown(predictKey))
        {
            rtmlCore.enableRun = !rtmlCore.enableRun;
            Debug.Log($"[SphereController] Real-time prediction = {rtmlCore.enableRun}");
        }

        // 4) If in run mode, predict + apply ML output
        if (rtmlCore.enableRun)
        {
            float[] input = ReadSliders();
            float[] pred  = rtmlCore.PredictSample(input);
            ApplyToSpheres(pred);
        }
    }

    // Apply the Inspector-defined colours to each sphere material
    private void ApplyInspectorColours()
    {
        for (int i = 0; i < sphereMats.Length && i < inspectorColours.Length; i++)
        {
            sphereMats[i].color = inspectorColours[i];
        }
    }

    // Read 5 slider values into a float[5]
    private float[] ReadSliders()
    {
        var arr = new float[sliders.Length];
        for (int i = 0; i < sliders.Length; i++)
            arr[i] = sliders[i].value;
        return arr;
    }

    // Read current sphere material colours (RGBA) into float[12]
    private float[] ReadSphereColours()
    {
        var arr = new float[sphereMats.Length * 4];
        for (int i = 0; i < sphereMats.Length; i++)
        {
            Color c = sphereMats[i].color;
            arr[i*4 + 0] = c.r;
            arr[i*4 + 1] = c.g;
            arr[i*4 + 2] = c.b;
            arr[i*4 + 3] = c.a;
        }
        return arr;
    }

    // Apply a float[N*4] to sphere materials as RGBA
    private void ApplyToSpheres(float[] data)
    {
        for (int i = 0; i < sphereMats.Length; i++)
        {
            int baseIdx = i * 4;
            if (baseIdx + 3 >= data.Length) break;
            var c = new Color(
                data[baseIdx + 0],
                data[baseIdx + 1],
                data[baseIdx + 2],
                data[baseIdx + 3]
            );
            sphereMats[i].color = c;
        }
    }
}
