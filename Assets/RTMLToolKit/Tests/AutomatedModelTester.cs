/*
 * ExtendedRigorousModelTester.cs
 *
 * MonoBehaviour that performs comprehensive automated tests for each RTML model type,
 * including timing, error metrics, and time-dimension performance scaling,
 * reporting results to the Unity Console in British English.
 * Attach this to any GameObject in your scene and press Play; overall run may take several seconds.
 */

using UnityEngine;
using RTMLToolKit;
using RTMLToolKit.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;  // For Stopwatch

namespace RTMLToolKit.Tests
{
    public class ExtendedRigorousModelTester : MonoBehaviour
    {
        [Header("Regression & Classification Test Parameters")]
        [Tooltip("Number of training samples to generate for core tests.")]
        public int numTrainSamples = 1000;

        [Tooltip("Number of test samples to evaluate for core tests.")]
        public int numTestSamples = 200;

        [Tooltip("Standard deviation of Gaussian noise applied to outputs.")]
        public float noiseStdDev = 0.1f;

        [Header("Time-Dimension Performance Parameters")]
        [Tooltip("Various sample counts to benchmark performance.")]
        public int[] performanceSampleCounts = new int[] { 100, 500, 1000 };

        [Tooltip("Number of prediction calls per benchmark iteration.")]
        public int predictionCalls = 100;

        void Start()
        {
            int failures = 0;
            UnityEngine.Debug.Log("=== RTML Tool Kit Extended Rigorous Model Testing Commencing ===");

            if (RunRegressionTest())
                UnityEngine.Debug.Log("LinearRegression: Rigorous regression test passed successfully.");
            else
            {
                UnityEngine.Debug.LogError("LinearRegression: Rigorous regression test failed.");
                failures++;
            }

            if (RunKNNTest())
                UnityEngine.Debug.Log("KNNClassifier: Rigorous kNN test passed successfully.");
            else
            {
                UnityEngine.Debug.LogError("KNNClassifier: Rigorous kNN test failed.");
                failures++;
            }

            if (RunDTWTest())
                UnityEngine.Debug.Log("DTWRecognizer: Rigorous DTW test passed successfully.");
            else
            {
                UnityEngine.Debug.LogError("DTWRecognizer: Rigorous DTW test failed.");
                failures++;
            }

            UnityEngine.Debug.Log($"=== Core Testing Completed: {failures} failure(s), {3 - failures} success(es) ===");

            RunTimeDimensionTests();
        }

        private bool RunRegressionTest()
        {
            const int dim = 5;
            var rng = new System.Random(42);

            // Generate random linear mapping y = A * x + b
            float[,] A = new float[dim, dim];
            float[] b  = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                b[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                for (int j = 0; j < dim; j++)
                    A[i, j] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }

            var inputs  = new List<float[]>();
            var outputs = new List<float[]>();
            for (int n = 0; n < numTrainSamples; n++)
            {
                var x = new float[dim];
                var y = new float[dim];
                for (int i = 0; i < dim; i++)
                    x[i] = (float)(rng.NextDouble() * 10.0 - 5.0);
                for (int i = 0; i < dim; i++)
                {
                    float sum = b[i];
                    for (int j = 0; j < dim; j++)
                        sum += A[i, j] * x[j];
                    sum += (float)(Gaussian(rng) * noiseStdDev);
                    y[i] = sum;
                }
                inputs.Add(x);
                outputs.Add(y);
            }

            var model = new LinearRegression(dim, dim, learningRate: 0.01f, iterations: 2000);
            var sw = Stopwatch.StartNew();
            model.Train(inputs, outputs);
            sw.Stop();
            UnityEngine.Debug.Log($"[LinearRegression] Training took {sw.ElapsedMilliseconds} ms on {numTrainSamples} samples.");

            double mse = 0.0;
            sw.Restart();
            for (int n = 0; n < numTestSamples; n++)
            {
                int idx = rng.Next(numTrainSamples);
                var x   = inputs[idx];
                var truth = outputs[idx];
                var pred  = model.Predict(x);
                for (int i = 0; i < dim; i++)
                {
                    double err = pred[i] - truth[i];
                    mse += err * err;
                }
            }
            sw.Stop();
            mse /= (numTestSamples * dim);
            UnityEngine.Debug.Log($"[LinearRegression] Prediction on {numTestSamples} samples took {sw.ElapsedMilliseconds} ms; MSE = {mse:F4}.");

            float threshold = noiseStdDev * noiseStdDev * 1.2f;
            UnityEngine.Debug.Log($"[LinearRegression] Acceptable MSE threshold = {threshold:F4}.");
            return mse < threshold;
        }

        private bool RunKNNTest()
        {
            const int dim = 5;
            var rng = new System.Random(99);

            var trainX = new List<float[]>();
            var trainY = new List<float[]>();
            for (int n = 0; n < numTrainSamples; n++)
            {
                var x = new float[dim];
                var y = new float[dim];
                for (int i = 0; i < dim; i++)
                {
                    x[i] = (float)(rng.NextDouble() * 10.0);
                    y[i] = x[i] * 1.5f;
                }
                trainX.Add(x);
                trainY.Add(y);
            }

            var model = new KNNClassifier(dim, dim, k: 5);
            var sw = Stopwatch.StartNew();
            model.Train(trainX, trainY);
            sw.Stop();
            UnityEngine.Debug.Log($"[KNNClassifier] Training stored {numTrainSamples} samples in {sw.ElapsedMilliseconds} ms.");

            sw.Restart();
            double mse = 0.0;
            for (int n = 0; n < numTestSamples; n++)
            {
                int idx = rng.Next(numTrainSamples);
                var x     = trainX[idx];
                var truth = trainY[idx];
                var pred  = model.Predict(x);
                for (int i = 0; i < dim; i++)
                {
                    double err = pred[i] - truth[i];
                    mse += err * err;
                }
            }
            sw.Stop();
            mse /= (numTestSamples * dim);
            UnityEngine.Debug.Log($"[KNNClassifier] Prediction on {numTestSamples} samples took {sw.ElapsedMilliseconds} ms; MSE = {mse:F4}.");

            return mse < 1.0;
        }

        private bool RunDTWTest()
        {
            const int dim = 5;
            var rng = new System.Random(123);

            var templates = new List<float[]>();
            var labels    = new List<float[]>();
            for (int t = 0; t < numTrainSamples; t++)
            {
                var seq = new float[dim];
                for (int i = 0; i < dim; i++)
                    seq[i] = i + t * 0.01f;
                templates.Add(seq);
                labels.Add(new float[] { t });
            }

            var model = new DTWRecognizer(dim, 1);
            var sw = Stopwatch.StartNew();
            model.Train(templates, labels);
            sw.Stop();
            UnityEngine.Debug.Log($"[DTWRecognizer] Stored {numTrainSamples} templates in {sw.ElapsedMilliseconds} ms.");

            sw.Restart();
            int correct = 0;
            for (int n = 0; n < numTestSamples; n++)
            {
                int idx = rng.Next(numTrainSamples);
                var seq = templates[idx];
                var pred = model.Predict(seq);
                if (Math.Abs(pred[0] - idx) < 0.1f) correct++;
            }
            sw.Stop();
            float accuracy = (float)correct / numTestSamples;
            UnityEngine.Debug.Log($"[DTWRecognizer] Prediction on {numTestSamples} samples took {sw.ElapsedMilliseconds} ms; accuracy = {accuracy:F2}.");

            return accuracy > 0.9f;
        }

        private void RunTimeDimensionTests()
        {
            UnityEngine.Debug.Log("=== Time-Dimension Performance Tests Commencing ===");
            const int dim = 5;
            var rng = new System.Random(555);

            foreach (int count in performanceSampleCounts)
            {
                // Prepare data for performance tests
                var xSamples = new List<float[]>();
                for (int i = 0; i < count; i++)
                {
                    var x = new float[dim];
                    for (int j = 0; j < dim; j++)
                        x[j] = (float)(rng.NextDouble() * 10.0);
                    xSamples.Add(x);
                }

                // LinearRegression performance
                var lrModel = new LinearRegression(dim, dim);
                var sw = Stopwatch.StartNew();
                lrModel.Train(xSamples, xSamples);
                sw.Stop();
                long lrTrainMs = sw.ElapsedMilliseconds;

                sw.Restart();
                for (int k = 0; k < predictionCalls; k++)
                    lrModel.Predict(xSamples[k % count]);
                sw.Stop();
                long lrPredMs = sw.ElapsedMilliseconds;

                UnityEngine.Debug.Log($"[Performance][LinearRegression] Samples={count}: Train={lrTrainMs} ms, Predict({predictionCalls})={lrPredMs} ms");

                // kNN performance
                var knnModel = new KNNClassifier(dim, dim, k: 5);
                sw.Restart();
                knnModel.Train(xSamples, xSamples);
                sw.Stop();
                long knnTrainMs = sw.ElapsedMilliseconds;

                sw.Restart();
                for (int k = 0; k < predictionCalls; k++)
                    knnModel.Predict(xSamples[k % count]);
                sw.Stop();
                long knnPredMs = sw.ElapsedMilliseconds;

                UnityEngine.Debug.Log($"[Performance][KNNClassifier] Samples={count}: Train={knnTrainMs} ms, Predict({predictionCalls})={knnPredMs} ms");

                // DTWRecognizer performance
                var dtwModel = new DTWRecognizer(dim, 1);
                sw.Restart();
                dtwModel.Train(xSamples, xSamples);
                sw.Stop();
                long dtwTrainMs = sw.ElapsedMilliseconds;

                sw.Restart();
                for (int k = 0; k < predictionCalls; k++)
                    dtwModel.Predict(xSamples[k % count]);
                sw.Stop();
                long dtwPredMs = sw.ElapsedMilliseconds;

                UnityEngine.Debug.Log($"[Performance][DTWRecognizer] Samples={count}: Train={dtwTrainMs} ms, Predict({predictionCalls})={dtwPredMs} ms");
            }

            UnityEngine.Debug.Log("=== Time-Dimension Performance Tests Completed ===");
        }

        // Box–Muller transform for Gaussian noise
        private double Gaussian(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
