/*
 * DTWRecognizer.cs
 *
 * Implements IModel as a placeholder DTW recogniser.
 * Construct with inputSize and outputSize.
 * Call Train(templates, outputs) to store reference frames,
 * then Predict(input) returns the output of the nearest template by Euclidean distance
 * only if its similarity score meets the similarityThreshold.
 */

using System.Collections.Generic;
using UnityEngine;

namespace RTMLToolKit
{
    /// <summary>
    /// Placeholder DTW recogniser using one-nearest-neighbour on single frames.
    /// Full sequence-based DTW support may be added later.
    /// </summary>
    public class DTWRecognizer : IModel
    {
        /// <summary>Number of features expected per input vector.</summary>
        public int inputSize;

        /// <summary>Number of dimensions in the output vector.</summary>
        public int outputSize;

        /// <summary>
        /// Required similarity score (0-1) for a match to be considered valid.
        /// Higher values require a closer match (i.e., smaller distance).
        /// </summary>
        public float similarityThreshold;

        /// <summary>Stored template inputs.</summary>
        public List<float[]> templates;

        /// <summary>Stored template outputs corresponding to each template.</summary>
        public List<float[]> templateOutputs;

        /// <summary>
        /// Constructor: specify the dimensions of input and output vectors.
        /// </summary>
        public DTWRecognizer(int inputSize, int outputSize)
        {
            this.inputSize = inputSize;
            this.outputSize = outputSize;
            this.similarityThreshold = 0.8f; // Default threshold
            this.templates = new List<float[]>();
            this.templateOutputs = new List<float[]>();
        }

        /// <summary>
        /// Stores each input–output pair as a template for one-nearest-neighbour matching.
        /// No longer rejects by vector length—accepts all pairs.
        /// </summary>
        public void Train(List<float[]> inputs, List<float[]> outputs)
        {
            if (inputs.Count != outputs.Count)
            {
                Logger.LogWarning("[DTWRecognizer] Input/output count mismatch.");
                return;
            }

            templates.Clear();
            templateOutputs.Clear();

            for (int i = 0; i < inputs.Count; i++)
            {
                templates.Add((float[])inputs[i].Clone());
                templateOutputs.Add((float[])outputs[i].Clone());
            }

            Logger.Log($"[DTWRecognizer] Stored {templates.Count} template frames.");
        }

        /// <summary>
        /// Predicts output for the given input by finding the nearest template.
        /// Returns the template's output only if the calculated similarity meets the threshold.
        /// Otherwise, returns a zero vector.
        /// </summary>
        public float[] Predict(float[] input)
        {
            if (templates.Count == 0)
            {
                Logger.LogWarning("[DTWRecognizer] No templates available to predict from.");
                return new float[outputSize];
            }

            float bestDistance = float.PositiveInfinity;
            int bestIndex = -1;

            // Find the template with the minimum Euclidean distance
            for (int i = 0; i < templates.Count; i++)
            {
                int len = Mathf.Min(input.Length, templates[i].Length);
                float sumSq = 0f;
                for (int j = 0; j < len; j++)
                {
                    float d = input[j] - templates[i][j];
                    sumSq += d * d;
                }
                float distance = Mathf.Sqrt(sumSq);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            // Convert distance to a similarity score (e.g., 1 is a perfect match, 0 is very dissimilar)
            float similarity = 1.0f / (1.0f + bestDistance);

            // Return the result only if the similarity is high enough
            if (bestIndex != -1 && similarity >= similarityThreshold)
            {
                return (float[])templateOutputs[bestIndex].Clone();
            }

            // If no match met the threshold, return a default/zero array
            return new float[outputSize];
        }
    }
}