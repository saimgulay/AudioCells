using System.Collections.Generic;
using UnityEngine;

namespace RTMLToolKit
{
    /// <summary>
    /// Simple multivariate linear regression model.
    /// Implements the IModel interface: Train and Predict methods.
    /// </summary>
    public class LinearRegression : IModel
    {
        // Model dimensions (public for persistence)
        public int inputSize;
        public int outputSize;

        // Model parameters (public for persistence)
        public float[,] weights;
        public float[] bias;

        // Hyperparameters (public for persistence)
        public float learningRate;
        public int iterations;

        /// <summary>
        /// Constructor: takes input/output dimensions and optional hyperparameters.
        /// </summary>
        public LinearRegression(int inputSize, int outputSize, float learningRate = 0.01f, int iterations = 1000)
        {
            this.inputSize = inputSize;
            this.outputSize = outputSize;
            this.learningRate = learningRate;
            this.iterations = iterations;

            weights = new float[outputSize, inputSize];
            bias = new float[outputSize];
            // Weights and bias are initialised to zero
        }

        /// <summary>
        /// Train the model using simple batch gradient descent.
        /// </summary>
        public void Train(List<float[]> inputs, List<float[]> outputs)
        {
            if (inputs.Count != outputs.Count)
            {
                Logger.LogWarning("[LinearRegression] Number of inputs and outputs does not match.");
                return;
            }

            int sampleCount = inputs.Count;
            for (int epoch = 0; epoch < iterations; epoch++)
            {
                for (int s = 0; s < sampleCount; s++)
                {
                    float[] x = inputs[s];
                    float[] y = outputs[s];
                    float[] yPred = Predict(x);

                    for (int i = 0; i < outputSize; i++)
                    {
                        float error = y[i] - yPred[i];
                        bias[i] += learningRate * error;

                        for (int j = 0; j < inputSize; j++)
                        {
                            weights[i, j] += learningRate * error * x[j];
                        }
                    }
                }
            }

            Logger.Log($"[LinearRegression] Model trained on {sampleCount} samples for {iterations} iterations.");
        }

        /// <summary>
        /// Predict output for a given input vector.
        /// </summary>
        public float[] Predict(float[] input)
        {
            if (input.Length != inputSize)
                Logger.LogWarning($"[LinearRegression] Input vector length ({input.Length}) does not match expected ({inputSize}).");

            float[] result = new float[outputSize];
            for (int i = 0; i < outputSize; i++)
            {
                float sum = bias[i];
                for (int j = 0; j < inputSize; j++)
                {
                    sum += weights[i, j] * input[j];
                }
                result[i] = sum;
            }
            return result;
        }
    }
}
