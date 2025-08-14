/*
 * RTMLCore.cs
 *
 * Core controller for RTML Tool Kit. Manages data buffering,
 * model lifecycle, optional OSC input/output, and model persistence.
 * Configure every parameter in the Inspector.
 * Use direct API for in-Unity usage, or enable OSC for external control.
 * Supports saving and loading of trained models to Assets/RTMLToolKit/SavedModels.
 *
 * Includes a custom Inspector with "Save Model", "Load Model",
 * "Delete Last Sample" and "Delete All Samples" buttons.
 */

using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RTMLToolKit
{
    [Serializable]
    class LinearRegressionData
    {
        public int inputSize;
        public int outputSize;
        public float learningRate;
        public int iterations;
        public float[] bias;
        public float[] weights;    // flattened row-major: length == outputSize * inputSize
    }

    [Serializable]
    class KNNData
    {
        public int inputSize;
        public int outputSize;
        public int k;
        public List<List<float>> trainInputs;
        public List<List<float>> trainOutputs;
    }

    [Serializable]
    class DTWData
    {
        public int inputSize;
        public int outputSize;
        public float similarityThreshold; // EKLENDI: Eşleşme eşiği
        public List<List<float>> templates;
        public List<List<float>> templateOutputs;
    }

    /// <summary>
    /// Central engine for RTML Tool Kit, providing supervised learning model training,
    /// prediction, optional OSC input/output and model persistence.
    /// </summary>
    public class RTMLCore : MonoBehaviour
    {
        //--------------- Inspector Parameters ---------------//

        [Header("Data Dimensions")]
        [Tooltip("Number of input features expected per sample.")]
        public int inputSize = 1;

        [Tooltip("Number of output dimensions.")]
        public int outputSize = 1;

        [Header("Model Settings")]
        [Tooltip("Type of model to use: LinearRegression, KNN, or DTW.")]
        public ModelType modelType = ModelType.LinearRegression;

        [Header("DTW Settings")] // EKLENDI: DTW’ye özel ayarlar bölümü
        [Tooltip("Similarity threshold for DTW matching. A lower distance means a better match. If the best match distance is above this threshold, it is rejected. Higher values make matching easier.")]
        public float dtwSimilarityThreshold = 100.0f;

        [Header("Modes")]
        [Tooltip("Enable recording of incoming samples.")]
        public bool enableRecord = false;

        [Tooltip("Enable training when toggled.")]
        public bool enableTrain = false;

        [Tooltip("Enable real-time prediction on incoming data.")]
        public bool enableRun = true;

        [Header("OSC Integration")]
        [Tooltip("Toggle use of OSC for external control and data I/O.")]
        public bool useOsc = false;

        [Tooltip("Local port on which to listen for OSC input.")]
        public int oscInPort = 6448;

        [Tooltip("OSC address for incoming feature vectors.")]
        public string oscInputAddress = "/rtml/inputs";

        [Tooltip("Remote IP address for sending OSC output.")]
        public string oscOutIP = "127.0.0.1";

        [Tooltip("Remote port for sending OSC output.")]
        public int oscOutPort = 12000;

        [Tooltip("OSC address to which predictions will be sent.")]
        public string oscOutputAddress = "/rtml/outputs";

        [Header("Persistence Settings")]
        [Tooltip("Filename (without extension) for saving/loading the model JSON.")]
        public string modelFileName = "defaultModel";

        [Header("Startup Options")]
        [Tooltip("If enabled, the model specified in 'modelFileName' will be loaded and set to run on Start.")]
        public bool loadAndRunOnStart = false;

        //--------------- Internal References ---------------//

        private SampleBuffer sampleBuffer;
        internal IModel model;
        private OSCReceiver oscReceiver;
        private OSCSender oscSender;

        //--------------- Unity Lifecycle ---------------//

        void Awake()
        {
            sampleBuffer = new SampleBuffer(inputSize, outputSize);
            InitialiseModel();

            if (useOsc)
                SetUpOsc();
        }

        void Start()
        {
            if (loadAndRunOnStart)
            {
                LoadModel(modelFileName);
                enableRun = true;
                Logger.Log($"[RTMLCore] Model '{modelFileName}' loaded and running on start.");
            }
        }

        void Update()
        {
            if (enableTrain)
            {
                TrainModel();
                enableTrain = false;
            }
        }

        void OnDestroy()
        {
            if (useOsc)
            {
                oscReceiver.Close();
                oscSender.Close();
            }
        }

        //--------------- Model Initialization ---------------//

        private void InitialiseModel()
        {
            switch (modelType)
            {
                case ModelType.LinearRegression:
                    model = new LinearRegression(inputSize, outputSize);
                    break;
                case ModelType.KNN:
                    model = new KNNClassifier(inputSize, outputSize);
                    break;
                case ModelType.DTW:
                    var dtw = new DTWRecognizer(inputSize, outputSize);
                    dtw.similarityThreshold = dtwSimilarityThreshold;
                    model = dtw;
                    break;
                default:
                    model = new LinearRegression(inputSize, outputSize);
                    break;
            }
            Logger.Log($"[RTMLCore] Model initialised: {modelType}");
        }

        //--------------- OSC Setup ---------------//

        private void SetUpOsc()
        {
            oscReceiver = gameObject.GetComponent<OSCReceiver>() ?? gameObject.AddComponent<OSCReceiver>();
            oscSender   = gameObject.GetComponent<OSCSender>()   ?? gameObject.AddComponent<OSCSender>();

            oscReceiver.Initialize(oscInPort);
            oscReceiver.Bind(oscInputAddress, DirectOnData);
            oscReceiver.Bind("/rtml/control/record", (addr, val) => enableRecord = val > 0.5f);
            oscReceiver.Bind("/rtml/control/train",  (addr, val) => { if (val > 0.5f) TrainModel(); });
            oscReceiver.Bind("/rtml/control/run",    (addr, val) => enableRun    = val > 0.5f);

            oscSender.Initialize(oscOutIP, oscOutPort);

            Logger.Log($"[RTMLCore] OSC enabled on port {oscInPort}");
        }

        //--------------- Persistence Methods ---------------//

        /// <summary>
        /// Save the current model to Assets/RTMLToolKit/SavedModels/{name}.json.
        /// Always writes both bias and weights fields, even if zero.
        /// </summary>
        public void SaveModel(string name)
        {
            string directory = Path.Combine(Application.dataPath, "RTMLToolKit/SavedModels");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".json");

            switch (modelType)
            {
                case ModelType.LinearRegression:
                    SaveLinearRegression(path);
                    break;
                case ModelType.KNN:
                    SaveKNN(path);
                    break;
                case ModelType.DTW:
                    SaveDTW(path);
                    break;
            }

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        private void SaveLinearRegression(string path)
        {
            var lr = model as LinearRegression;
            var data = new LinearRegressionData
            {
                inputSize    = inputSize,
                outputSize   = outputSize,
                learningRate = lr.learningRate,
                iterations   = lr.iterations,
                bias         = (float[])lr.bias.Clone(),
                weights      = new float[outputSize * inputSize]
            };

            for (int i = 0; i < outputSize; i++)
                for (int j = 0; j < inputSize; j++)
                    data.weights[i * inputSize + j] = lr.weights[i, j];

            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log($"[RTMLCore] LinearRegression model saved to {path}");
        }

        private void SaveKNN(string path)
        {
            var knn = model as KNNClassifier;
            var data = new KNNData
            {
                inputSize    = inputSize,
                outputSize   = outputSize,
                k            = knn.k,
                trainInputs  = new List<List<float>>(),
                trainOutputs = new List<List<float>>()
            };
            foreach (var inp in knn.trainInputs)  data.trainInputs.Add(new List<float>(inp));
            foreach (var outp in knn.trainOutputs) data.trainOutputs.Add(new List<float>(outp));

            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log($"[RTMLCore] KNNClassifier model saved to {path}");
        }

        private void SaveDTW(string path)
        {
            var dtw = model as DTWRecognizer;
            var data = new DTWData
            {
                inputSize           = inputSize,
                outputSize          = outputSize,
                similarityThreshold = dtw.similarityThreshold,
                templates           = new List<List<float>>(),
                templateOutputs     = new List<List<float>>()
            };
            foreach (var t in dtw.templates)       data.templates.Add(new List<float>(t));
            foreach (var o in dtw.templateOutputs) data.templateOutputs.Add(new List<float>(o));

            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log($"[RTMLCore] DTWRecognizer model saved to {path}");
        }

        /// <summary>
        /// Load model parameters from Assets/RTMLToolKit/SavedModels/{name}.json.
        /// Bias and weights are both optional: missing weights default to zeros.
        /// </summary>
        public void LoadModel(string name)
        {
            string path = Path.Combine(Application.dataPath, "RTMLToolKit/SavedModels", name + ".json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[RTMLCore] No model file at {path}");
                return;
            }

            switch (modelType)
            {
                case ModelType.LinearRegression: LoadLinearRegression(path); break;
                case ModelType.KNN:             LoadKNN(path);             break;
                case ModelType.DTW:             LoadDTW(path);             break;
            }
        }

        private void LoadLinearRegression(string path)
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<LinearRegressionData>(json);
            if (data == null || data.bias == null)
            {
                Debug.LogError($"[RTMLCore] Failed to load LinearRegression model from {path} (schema mismatch).");
                return;
            }

            var lr = new LinearRegression(data.inputSize, data.outputSize, data.learningRate, data.iterations);

            if (data.bias.Length == data.outputSize)
                for (int i = 0; i < data.outputSize; i++)
                    lr.bias[i] = data.bias[i];
            else
                Debug.LogWarning($"[RTMLCore] Loaded bias length ({data.bias.Length}) != expected ({data.outputSize}), skipping bias.");

            if (data.weights != null && data.weights.Length == data.outputSize * data.inputSize)
                for (int i = 0; i < data.outputSize; i++)
                    for (int j = 0; j < data.inputSize; j++)
                        lr.weights[i, j] = data.weights[i * data.inputSize + j];
            else
                Debug.LogWarning($"[RTMLCore] Weights length mismatch or missing; using zero weights.");

            model = lr;
            Debug.Log($"[RTMLCore] LinearRegression model loaded from {path}");
        }

        private void LoadKNN(string path)
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<KNNData>(json);
            if (data == null || data.trainInputs == null || data.trainOutputs == null)
            {
                Debug.LogError($"[RTMLCore] Failed to load KNN model from {path} (schema mismatch).");
                return;
            }

            var knn = new KNNClassifier(data.inputSize, data.outputSize, data.k)
            {
                trainInputs  = new List<float[]>(data.trainInputs.Count),
                trainOutputs = new List<float[]>(data.trainOutputs.Count)
            };
            for (int i = 0; i < data.trainInputs.Count; i++)
            {
                knn.trainInputs.Add(data.trainInputs[i].ToArray());
                knn.trainOutputs.Add(data.trainOutputs[i].ToArray());
            }

            model = knn;
            Debug.Log($"[RTMLCore] KNNClassifier model loaded from {path}");
        }

        private void LoadDTW(string path)
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<DTWData>(json);
            if (data == null || data.templates == null || data.templateOutputs == null)
            {
                Debug.LogError($"[RTMLCore] Failed to load DTW model from {path} (schema mismatch).");
                return;
            }

            var dtw = new DTWRecognizer(data.inputSize, data.outputSize);

            dtw.similarityThreshold     = data.similarityThreshold;
            this.dtwSimilarityThreshold = data.similarityThreshold;

            foreach (var t in data.templates)       dtw.templates.Add(t.ToArray());
            foreach (var o in data.templateOutputs) dtw.templateOutputs.Add(o.ToArray());

            model = dtw;
            Debug.Log($"[RTMLCore] DTWRecognizer model loaded from {path}");
        }

        //--------------- Direct API Methods ---------------//

        public void RecordSample(float[] input, float[] output)
        {
            if (!enableRecord) return;
            sampleBuffer.AddSample(input, output);
        }

        public void TrainModel()
        {
            var inputs  = sampleBuffer.GetInputs();
            var outputs = sampleBuffer.GetOutputs();
            if (inputs.Count == 0)
            { Logger.LogWarning("[RTMLCore] No samples to train."); return; }
            model.Train(inputs, outputs);
            Logger.Log($"[RTMLCore] Trained on {inputs.Count} samples.");
            sampleBuffer.Clear();
        }

        public float[] PredictSample(float[] input)
        {
            if (!enableRun) return new float[outputSize];
            return model.Predict(input);
        }

        //--------------- Internal Data Handler ---------------//

        private void DirectOnData(float[] data)
        {
            if (data.Length != inputSize) return;
            if (enableRecord) sampleBuffer.AddSample(data, new float[outputSize]);
            if (enableRun)
            {
                var pred = model.Predict(data);
                oscSender.Send(oscOutputAddress, pred);
            }
        }

        //--------------- Sample Deletion Methods ---------------//

        /// <summary>Delete the most recently recorded sample.</summary>
        public void DeleteLastSample()
        {
            var inputs  = sampleBuffer.GetInputs();
            var outputs = sampleBuffer.GetOutputs();
            if (inputs.Count > 0)
            {
                inputs.RemoveAt(inputs.Count - 1);
                outputs.RemoveAt(outputs.Count - 1);
                Logger.Log($"[RTMLCore] Deleted last recorded sample. {inputs.Count} remain.");
            }
            else
            {
                Logger.LogWarning("[RTMLCore] No samples to delete.");
            }
        }

        /// <summary>Delete all recorded samples.</summary>
        public void DeleteAllSamples()
        {
            sampleBuffer.Clear();
            Logger.Log("[RTMLCore] All recorded samples deleted.");
        }
    }

    /// <summary>
    /// Supported model types for RTML Tool Kit.
    /// </summary>
    public enum ModelType
    {
        LinearRegression,
        KNN,
        DTW
    }
}

#if UNITY_EDITOR
// Custom Inspector for RTMLCore: adds Save/Load and Delete Sample buttons
namespace RTMLToolKit
{
    [CustomEditor(typeof(RTMLCore))]
    public class RTMLCoreEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RTMLCore core = (RTMLCore)target;

            // Ensure DTW threshold in model stays in sync with Inspector
            if (core.modelType == ModelType.DTW && core.model is DTWRecognizer dtwModel)
            {
                if (dtwModel.similarityThreshold != core.dtwSimilarityThreshold)
                    dtwModel.similarityThreshold = core.dtwSimilarityThreshold;
            }

            GUILayout.Space(8);

            // Save / Load
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Model")) core.SaveModel(core.modelFileName);
            if (GUILayout.Button("Load Model")) core.LoadModel(core.modelFileName);
            GUILayout.EndHorizontal();

            // Delete samples
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Delete Last Sample")) core.DeleteLastSample();
            if (GUILayout.Button("Delete All Samples")) core.DeleteAllSamples();
            GUILayout.EndHorizontal();
        }
    }
}
#endif
