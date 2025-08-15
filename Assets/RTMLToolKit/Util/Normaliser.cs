/*
 * Normaliser.cs
 *
 * Provides Min-Max or Z-Score normalisation.
 * Instantiate with chosen Method, call Fit(samples) to compute parameters,
 * then Transform(sample) or FitTransform(samples) to obtain normalised data.
 */


using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTMLToolKit.Util
{
    /// <summary>
    /// Provides min–max scaling or z-score normalisation for feature vectors.
    /// Instantiate, call Fit(...) on training data, then Transform(...) on each sample.
    /// </summary>
    public class Normaliser
    {
        public enum Method
        {
            MinMax,    // Scale each feature to [0,1]
            ZScore     // Standardise to zero mean and unit variance
        }

        private Method method;
        private float[] means;
        private float[] stdDevs;
        private float[] mins;
        private float[] maxs;
        private bool isFitted = false;

        /// <summary>
        /// Constructor: choose scaling method.
        /// </summary>
        public Normaliser(Method method = Method.MinMax)
        {
            this.method = method;
        }

        /// <summary>
        /// Compute parameters from a list of samples.
        /// </summary>
        public void Fit(List<float[]> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                Debug.LogWarning("[Normaliser] No samples provided for Fit().");
                return;
            }

            int dim = samples[0].Length;
            means   = new float[dim];
            stdDevs = new float[dim];
            mins    = new float[dim];
            maxs    = new float[dim];

            // Initialise mins and maxs
            for (int i = 0; i < dim; i++)
            {
                mins[i] = float.PositiveInfinity;
                maxs[i] = float.NegativeInfinity;
            }

            // Compute sums, mins and maxs
            for (int s = 0; s < samples.Count; s++)
            {
                var vec = samples[s];
                for (int i = 0; i < dim; i++)
                {
                    float v = vec[i];
                    means[i] += v;
                    mins[i]   = Mathf.Min(mins[i], v);
                    maxs[i]   = Mathf.Max(maxs[i], v);
                }
            }

            // Finalise mean
            for (int i = 0; i < dim; i++)
                means[i] /= samples.Count;

            if (method == Method.ZScore)
            {
                // Compute variance
                for (int s = 0; s < samples.Count; s++)
                {
                    var vec = samples[s];
                    for (int i = 0; i < dim; i++)
                    {
                        float d = vec[i] - means[i];
                        stdDevs[i] += d * d;
                    }
                }
                // Finalise standard deviation
                for (int i = 0; i < dim; i++)
                    stdDevs[i] = Mathf.Sqrt(stdDevs[i] / samples.Count);
            }

            isFitted = true;
            Debug.Log($"[Normaliser] Fitted {method} on {samples.Count} samples, dimension {dim}.");
        }

        /// <summary>
        /// Transform a single sample using previously computed parameters.
        /// </summary>
        public float[] Transform(float[] sample)
        {
            if (!isFitted)
            {
                Debug.LogWarning("[Normaliser] Transform() called before Fit(). Returning input unchanged.");
                return (float[])sample.Clone();
            }

            int dim = sample.Length;
            var result = new float[dim];

            if (method == Method.MinMax)
            {
                for (int i = 0; i < dim; i++)
                {
                    float range = maxs[i] - mins[i];
                    result[i] = (range > 0f)
                        ? (sample[i] - mins[i]) / range
                        : 0f;
                }
            }
            else // ZScore
            {
                for (int i = 0; i < dim; i++)
                {
                    float sd = stdDevs[i];
                    result[i] = (sd > 0f)
                        ? (sample[i] - means[i]) / sd
                        : 0f;
                }
            }

            return result;
        }

        /// <summary>
        /// Convenience: fit then transform all samples.
        /// </summary>
        public List<float[]> FitTransform(List<float[]> samples)
        {
            Fit(samples);
            var transformed = new List<float[]>(samples.Count);
            foreach (var s in samples)
                transformed.Add(Transform(s));
            return transformed;
        }
    }
}
