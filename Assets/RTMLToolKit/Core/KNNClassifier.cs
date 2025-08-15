/*
 * KNNClassifier.cs
 *
 * Implements IModel for k-Nearest Neighbours regression.
 * Construct with inputSize, outputSize and optional k (number of neighbours).
 * Use Train(inputs, outputs) to store samples, then Predict(input)
 * returns the averaged outputs of the k closest training vectors.
 */

using System.Collections.Generic;
using UnityEngine;

namespace RTMLToolKit
{
    public class KNNClassifier : IModel
    {
        // Model dimensions (public for persistence)
        public int inputSize;
        public int outputSize;

        // Number of neighbours to consider (public for persistence)
        public int k;

        // Training data (public for persistence)
        public List<float[]> trainInputs;
        public List<float[]> trainOutputs;

        /// <summary>
        /// Constructor: specify dimensions and number of neighbours.
        /// </summary>
        public KNNClassifier(int inputSize, int outputSize, int k = 1)
        {
            this.inputSize  = inputSize;
            this.outputSize = outputSize;
            this.k          = k;
            trainInputs     = new List<float[]>();
            trainOutputs    = new List<float[]>();
        }

        /// <summary>
        /// Store the provided input–output pairs for later prediction.
        /// </summary>
        public void Train(List<float[]> inputs, List<float[]> outputs)
        {
            if (inputs.Count != outputs.Count)
            {
                Logger.LogWarning($"[KNNClassifier] Number of inputs ({inputs.Count}) does not match number of outputs ({outputs.Count}).");
                return;
            }

            trainInputs.Clear();
            trainOutputs.Clear();

            for (int i = 0; i < inputs.Count; i++)
            {
                var x = inputs[i];
                var y = outputs[i];
                if (x.Length != inputSize || y.Length != outputSize)
                {
                    Logger.LogWarning($"[KNNClassifier] Sample dimension mismatch at index {i}.");
                    continue;
                }
                trainInputs.Add((float[])x.Clone());
                trainOutputs.Add((float[])y.Clone());
            }

            Logger.Log($"[KNNClassifier] Stored {trainInputs.Count} sample(s) (k = {k}).");
        }

        /// <summary>
        /// Predict the output for a given input by averaging the outputs of the k nearest neighbours.
        /// </summary>
        public float[] Predict(float[] input)
        {
            if (input.Length != inputSize)
            {
                Logger.LogWarning($"[KNNClassifier] Input length ({input.Length}) does not match expected ({inputSize}).");
                return new float[outputSize];
            }

            if (trainInputs.Count == 0)
            {
                Logger.LogWarning("[KNNClassifier] No training samples available for prediction.");
                return new float[outputSize];
            }

            // Compute L1 distances to all training samples
            var distances = new List<(float distance, int index)>(trainInputs.Count);
            for (int i = 0; i < trainInputs.Count; i++)
            {
                float sum = 0f;
                var xi = trainInputs[i];
                for (int j = 0; j < inputSize; j++)
                    sum += Mathf.Abs(xi[j] - input[j]);
                distances.Add((sum, i));
            }

            // Sort by distance
            distances.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Average the outputs of the k nearest neighbours
            int neighbours = Mathf.Min(k, distances.Count);
            var result = new float[outputSize];
            for (int n = 0; n < neighbours; n++)
            {
                var idx = distances[n].index;
                var yo  = trainOutputs[idx];
                for (int o = 0; o < outputSize; o++)
                    result[o] += yo[o];
            }
            for (int o = 0; o < outputSize; o++)
                result[o] /= neighbours;

            return result;
        }
    }
}
