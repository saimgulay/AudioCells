using UnityEngine;
using RTMLToolKit;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The main script logic - THIS PART HAS NOT BEEN CHANGED.
public class SphereControllerVoice : MonoBehaviour
{
    [Header("RTML Settings")]
    [Tooltip("A reference to your RTMLCore component.")]
    public RTMLCore rtmlCore;

    [Header("Sphere")]
    [Tooltip("The material of the sphere to colour via the ML model's output.")]
    public Material sphereMat;

    [Header("Colour Settings")]
    [Tooltip("Choose a colour to apply to the sphere when real-time ML prediction is disabled.")]
    public Color inspectorColour = Color.white;
    
    [Tooltip("Choose the colour to apply when silence is detected (i.e., when audio is below the noise threshold).")]
    public Color silenceColour = Color.black;

    [Header("Audio Settings")]
    // This field will be replaced by a dropdown in the custom editor.
    public int micDeviceIndex = 0;

    [HideInInspector]
    public string micDevice = "";

    [Tooltip("The playback/monitor volume (0–1).")]
    [Range(0f, 1f)]
    public float micVolume = 1f;

    [Tooltip("The number of FFT samples for the ML input. Must be a power of two (e.g., 256, 512).")]
    public int audioInputSize = 256;

    [Tooltip("Audio energy below this level will be treated as silence, ignoring the ML model. Adjust for your mic and environment.")]
    [Range(0f, 1f)]
    public float noiseThreshold = 0.05f;

    [Header("Keyboard Shortcuts")]
    public KeyCode startRecordKey = KeyCode.R;
    public KeyCode stopRecordKey  = KeyCode.S;
    public KeyCode trainKey       = KeyCode.T;
    public KeyCode predictKey     = KeyCode.P;

    private AudioSource audioSource;
    private AudioClip   audioClip;
    private bool        isRecordingSample = false;

    // The rest of the main script remains completely unchanged.
    void Start()
    {
        var devices = Microphone.devices;
        if (devices.Length > 0)
        {
            micDeviceIndex = Mathf.Clamp(micDeviceIndex, 0, devices.Length - 1);
            micDevice = devices[micDeviceIndex];
        }
        else
        {
            Debug.LogWarning("[SphereControllerVoice] No microphone devices detected.");
            micDevice = "";
        }

        if (!Mathf.IsPowerOfTwo(audioInputSize))
        {
            Debug.LogError($"[SphereControllerVoice] 'Audio Input Size' ({audioInputSize}) must be a power of two for FFT. Defaulting to 256.");
            audioInputSize = 256;
        }

        rtmlCore.enableRun    = false;
        rtmlCore.enableRecord = true;
        rtmlCore.inputSize    = audioInputSize;
        rtmlCore.outputSize   = 4;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop        = true;
        audioSource.volume      = micVolume;
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (!rtmlCore.enableRun)
        {
            sphereMat.color = inspectorColour;
        }

        if (Input.GetKeyDown(startRecordKey) && !isRecordingSample)
            StartSampleRecording();

        if (Input.GetKeyDown(stopRecordKey) && isRecordingSample)
            StopAndRecordSample();

        if (Input.GetKeyDown(trainKey))
        {
            rtmlCore.TrainModel();
        }

        if (Input.GetKeyDown(predictKey))
        {
            rtmlCore.enableRun = !rtmlCore.enableRun;
            Debug.Log($"[SphereControllerVoice] Real-time prediction toggled to: {rtmlCore.enableRun}");

            if (rtmlCore.enableRun && !Microphone.IsRecording(micDevice))
            {
                StartMicrophone();
            }
            else if (!rtmlCore.enableRun && !isRecordingSample)
            {
                StopMicrophone();
            }
        }
        
        if (rtmlCore.enableRun && audioSource.isPlaying)
        {
            float[] spectrumData = new float[audioInputSize];
            audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
            
            float rawMagnitude = GetSpectrumMagnitude(spectrumData);
            
            if (rawMagnitude < noiseThreshold)
            {
                sphereMat.color = silenceColour; 
            }
            else
            {
                NormaliseSpectrum(spectrumData);
                
                float[] prediction = rtmlCore.PredictSample(spectrumData);
                ApplyToSphere(prediction);
            }
        }
    }

    private float GetSpectrumMagnitude(float[] spectrum)
    {
        float magnitude = 0f;
        for (int i = 0; i < spectrum.Length; i++)
        {
            magnitude += spectrum[i] * spectrum[i];
        }
        return Mathf.Sqrt(magnitude);
    }

    private void NormaliseSpectrum(float[] spectrum)
    {
        float magnitude = GetSpectrumMagnitude(spectrum);
        if (magnitude < 1e-6) return;

        for (int i = 0; i < spectrum.Length; i++)
        {
            spectrum[i] /= magnitude;
        }
    }
    
    private void StopAndRecordSample()
    {
        isRecordingSample = false;

        float[] spectrumData = new float[audioInputSize];
        audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
        
        NormaliseSpectrum(spectrumData);

        Color currentColour = sphereMat.color;
        float[] output = new float[4] { currentColour.r, currentColour.g, currentColour.b, currentColour.a };

        rtmlCore.RecordSample(spectrumData, output);
        Debug.Log("[SphereControllerVoice] Normalised speech sample recorded.");

        if (!rtmlCore.enableRun)
        {
            StopMicrophone();
        }
    }
    
    private void StartSampleRecording()
    {
        if (string.IsNullOrEmpty(micDevice)) return;
        if (Microphone.IsRecording(micDevice)) StopMicrophone(); 

        StartMicrophone();
        isRecordingSample = true;
        Debug.Log($"[SphereControllerVoice] Sample recording started on '{micDevice}'.");
    }

    private void StartMicrophone()
    {
        if (string.IsNullOrEmpty(micDevice))
        {
            Debug.LogError("[SphereControllerVoice] Cannot start: no microphone selected.");
            return;
        }

        audioClip = Microphone.Start(micDevice, true, 1, 44100);
        audioSource.clip = audioClip;
        while (Microphone.GetPosition(micDevice) <= 0) { }
        audioSource.Play();
    }

    private void StopMicrophone()
    {
        if (Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
            audioSource.Stop();
        }
    }

    private void ApplyToSphere(float[] data)
    {
        if (data == null || data.Length < 4) return;
        Color predictedColour = new Color(data[0], data[1], data[2], data[3]);
        sphereMat.color = predictedColour;
    }
}


#if UNITY_EDITOR
// THIS IS THE CORRECTED EDITOR SCRIPT
// It completely replaces the default inspector to remove the integer field.
[CustomEditor(typeof(SphereControllerVoice))]
public class SphereControllerVoiceEditor : Editor 
{ 
    public override void OnInspectorGUI()
    {
        // Get a reference to the script being inspected.
        SphereControllerVoice controller = (SphereControllerVoice)target;
        
        // Use serializedObject for robust handling of properties.
        serializedObject.Update();

        // Manually draw all fields, recreating the headers.
        EditorGUILayout.LabelField("RTML Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rtmlCore"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sphere", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sphereMat"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colour Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("inspectorColour"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("silenceColour"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);

        // --- START OF THE DROPDOWN REPLACEMENT ---
        string[] devices = Microphone.devices;
        if (devices.Length > 0)
        {
            int currentIndex = controller.micDeviceIndex;
            if (currentIndex < 0 || currentIndex >= devices.Length)
            {
                currentIndex = 0;
            }
            
            // Draw the dropdown list INSTEAD OF the integer field.
            int newIndex = EditorGUILayout.Popup(new GUIContent("Mic Device", "Select a microphone from the list of available devices."), currentIndex, devices);
            if (newIndex != currentIndex)
            {
                controller.micDeviceIndex = newIndex;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No microphone devices found on this system.", MessageType.Warning);
        }
        // --- END OF THE DROPDOWN REPLACEMENT ---

        EditorGUILayout.PropertyField(serializedObject.FindProperty("micVolume"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("audioInputSize"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseThreshold"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startRecordKey"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stopRecordKey"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("trainKey"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("predictKey"));

        // Apply all changes made to the serializedObject.
        serializedObject.ApplyModifiedProperties();
    }
}
#endif