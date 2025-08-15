// Assets/Scripts/GenomeSonification/SemanticGenomeSynthController.cs

using UnityEngine;
using RTMLToolKit;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GenomeSonification
{
    [ExecuteAlways]
    public class SemanticGenomeSynthController : MonoBehaviour
    {
        [Header("RTML Settings")]
        public RTMLCore rtmlCore;

        [Header("Genome Logger")]
        [Tooltip("Which E. coli genome is currently being sampled.")]
        public EColiGenomeLogger genomeLogger;

        [Header("Alien Score Input")]
        public AlienScoreCalculator alienScoreCalculator;

        [Header("Synth Chord Player")]
        public SynthChordPlayer synthChordPlayer;

        [Header("LABEL (0–4 for Training)")]
        [Range(0, 4)]
        public int labelValue = 0;

        [Header("Keys")]
        public KeyCode recordKey  = KeyCode.R;
        public KeyCode trainKey   = KeyCode.T;
        public KeyCode predictKey = KeyCode.P;

        [Header("Prediction Result")]
        [Range(0, 4)]
        public int predictedValue = 0;

        void Awake()
        {
            if (rtmlCore != null)
            {
                rtmlCore.enableRun    = false;
                rtmlCore.enableRecord = true;
                rtmlCore.inputSize    = 1;
                rtmlCore.outputSize   = 1;
            }
        }

        void OnEnable()
        {
            if (synthChordPlayer != null)
                StartCoroutine(CheckAndApplyPresetEachRest());
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        void Update()
        {
            if (rtmlCore == null || alienScoreCalculator == null || genomeLogger == null)
                return;

            if (Input.GetKeyDown(recordKey))  RecordSample();
            if (Input.GetKeyDown(trainKey))   TrainModel();
            if (Input.GetKeyDown(predictKey)) TogglePredict();
        }

        private IEnumerator CheckAndApplyPresetEachRest()
        {
            // At each rest, see if we should predict & play—or mute.
            while (true)
            {
                yield return new WaitForSeconds(synthChordPlayer.restDuration);

                // If run is off or no genome locked yet → mute
                if (!rtmlCore.enableRun || genomeLogger.currentGenome == null)
                {
                    synthChordPlayer.MuteLoop();  // stops playing until unmuted
                    continue;
                }

                // We have a genome & live predict on → do prediction + unmute
                float[] input = { alienScoreCalculator.alienScore };
                var pred = rtmlCore.PredictSample(input);
                if (pred != null && pred.Length == 1)
                {
                    int idx = Mathf.Clamp(
                        Mathf.RoundToInt(pred[0]),
                        0,
                        synthChordPlayer.presets.Length - 1
                    );
                    predictedValue = idx;

                    // apply new preset (but don't restart the loop)
                    synthChordPlayer.SetPresetIndexWithoutRestart(idx);
                    synthChordPlayer.UnmuteLoop();
                }
            }
        }

        void RecordSample()
        {
            rtmlCore.RecordSample(
                new float[] { alienScoreCalculator.alienScore },
                new float[] { labelValue }
            );
            Debug.Log($"[GenomeSynth] Recorded In[{alienScoreCalculator.alienScore:F3}] → Out[{labelValue}]");
        }

        void TrainModel()
        {
            rtmlCore.TrainModel();
            Debug.Log("[GenomeSynth] Model training started.");
        }

        void TogglePredict()
        {
            rtmlCore.enableRun = !rtmlCore.enableRun;
            Debug.Log($"[GenomeSynth] Live predict: {rtmlCore.enableRun}");
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(SemanticGenomeSynthController))]
    public class SemanticGenomeSynthControllerEditor : Editor
    {
        SerializedProperty rtmlCoreProp, genomeLoggerProp, alienScoreProp, synthChordProp;
        SerializedProperty labelProp, recordKeyProp, trainKeyProp, predictKeyProp, predictedProp;

        void OnEnable()
        {
            rtmlCoreProp       = serializedObject.FindProperty("rtmlCore");
            genomeLoggerProp   = serializedObject.FindProperty("genomeLogger");
            alienScoreProp     = serializedObject.FindProperty("alienScoreCalculator");
            synthChordProp     = serializedObject.FindProperty("synthChordPlayer");
            labelProp          = serializedObject.FindProperty("labelValue");
            recordKeyProp      = serializedObject.FindProperty("recordKey");
            trainKeyProp       = serializedObject.FindProperty("trainKey");
            predictKeyProp     = serializedObject.FindProperty("predictKey");
            predictedProp      = serializedObject.FindProperty("predictedValue");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var ctrl = (SemanticGenomeSynthController)target;

            EditorGUILayout.LabelField("Core Components", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(rtmlCoreProp);
            EditorGUILayout.PropertyField(genomeLoggerProp);
            EditorGUILayout.PropertyField(alienScoreProp);
            EditorGUILayout.PropertyField(synthChordProp);
            if (ctrl.synthChordPlayer == null)
                EditorGUILayout.HelpBox("Please assign a SynthChordPlayer component.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Training Label (0–4)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(labelProp);
            if (ctrl.labelValue < 0 || ctrl.labelValue > 4)
                EditorGUILayout.HelpBox("Label must be between 0 and 4 inclusive!", MessageType.Error);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Keys", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(recordKeyProp);
            EditorGUILayout.PropertyField(trainKeyProp);
            EditorGUILayout.PropertyField(predictKeyProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prediction Result", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(predictedProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
    #endif
}
