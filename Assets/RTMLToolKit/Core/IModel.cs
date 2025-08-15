
/*
 * IModel.cs
 *
 * Defines the interface for all RTML models.
 * Any ML class must implement:
 *   void Train(List<float[]> inputs, List<float[]> outputs);
 *   float[] Predict(float[] input);
 */

using System.Collections.Generic;

namespace RTMLToolKit
{
    /// <summary>
    /// Interface for all RTML models.
    /// Defines methods for training and prediction.
    /// </summary>
    public interface IModel
    {
        /// <summary>
        /// Train the model with matching input and output samples.
        /// </summary>
        void Train(List<float[]> inputs, List<float[]> outputs);

        /// <summary>
        /// Predict an output vector for the given input vector.
        /// </summary>
        float[] Predict(float[] input);
    }
}
