/*
 * MathHelpers.cs
 *
 * Static utility methods for vector maths.
 * Offers Dot, EuclideanDistance, ManhattanDistance and CosineSimilarity
 * for float[] arrays of equal length.
 */


using UnityEngine;

namespace RTMLToolKit.Util
{
    /// <summary>
    /// Common mathematical helper functions for RTML Tool Kit.
    /// </summary>
    public static class MathHelpers
    {
        /// <summary>
        /// Computes the dot product of two equal-length vectors.
        /// </summary>
        public static float Dot(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                Debug.LogWarning($"[MathHelpers] Dot: vector lengths differ ({a.Length} vs {b.Length}).");
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return sum;
        }

        /// <summary>
        /// Computes the Euclidean distance between two vectors.
        /// </summary>
        public static float EuclideanDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                Debug.LogWarning($"[MathHelpers] EuclideanDistance: vector lengths differ ({a.Length} vs {b.Length}).");
                return float.PositiveInfinity;
            }

            float sumSq = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float d = a[i] - b[i];
                sumSq += d * d;
            }
            return Mathf.Sqrt(sumSq);
        }

        /// <summary>
        /// Computes the Manhattan (L1) distance between two vectors.
        /// </summary>
        public static float ManhattanDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                Debug.LogWarning($"[MathHelpers] ManhattanDistance: vector lengths differ ({a.Length} vs {b.Length}).");
                return float.PositiveInfinity;
            }

            float sum = 0f;
            for (int i = 0; i < a.Length; i++)
                sum += Mathf.Abs(a[i] - b[i]);
            return sum;
        }

        /// <summary>
        /// Computes the Cosine similarity between two vectors.
        /// Returns a value in [-1,1].
        /// </summary>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                Debug.LogWarning($"[MathHelpers] CosineSimilarity: vector lengths differ ({a.Length} vs {b.Length}).");
                return 0f;
            }

            float dot = Dot(a, b);
            float magA = Mathf.Sqrt(Dot(a, a));
            float magB = Mathf.Sqrt(Dot(b, b));
            if (magA <= 0f || magB <= 0f)
                return 0f;
            return dot / (magA * magB);
        }
    }
}
