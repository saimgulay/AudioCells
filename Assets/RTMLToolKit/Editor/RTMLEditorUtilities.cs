#if UNITY_EDITOR
/*
 * RTMLEditorUtilities.cs
 *
 * Editor-only utilities for RTML Tool Kit.
 * Adds menu items under GameObject → RTML Tool Kit:
 *   - Create RTML Runner: spawns a preconfigured GameObject with OSCReceiver, OSCSender and RTMLCore.
 *   - Documentation: opens your GitHub README in a browser.
 */
#endif


using System.Collections.Generic;

namespace RTMLToolKit
{
    /// <summary>
    /// Buffers input-output samples for training.
    /// </summary>
    public class SampleBuffer
    {
        private int inputSize;
        private int outputSize;

        private List<float[]> inputs;
        private List<float[]> outputs;

        /// <summary>
        /// Constructor: initialise buffer with fixed dimensions.
        /// </summary>
        public SampleBuffer(int inputSize, int outputSize)
        {
            this.inputSize = inputSize;
            this.outputSize = outputSize;
            inputs = new List<float[]>();
            outputs = new List<float[]>();
        }

        /// <summary>
        /// Add a single sample (input-output pair) to the buffer.
        /// </summary>
        public void AddSample(float[] input, float[] output)
        {
            if (input.Length != inputSize || output.Length != outputSize)
            {
                Logger.LogWarning($"[SampleBuffer] Sample dimensions mismatch. Expected in:{inputSize}, out:{outputSize}.");
                return;
            }
            inputs.Add((float[])input.Clone());
            outputs.Add((float[])output.Clone());
        }

        /// <summary>
        /// Retrieve all buffered input vectors.
        /// </summary>
        public List<float[]> GetInputs()
        {
            return new List<float[]>(inputs);
        }

        /// <summary>
        /// Retrieve all buffered output vectors.
        /// </summary>
        public List<float[]> GetOutputs()
        {
            return new List<float[]>(outputs);
        }

        /// <summary>
        /// Clears the buffer.
        /// </summary>
        public void Clear()
        {
            inputs.Clear();
            outputs.Clear();
        }

        /// <summary>
        /// Number of samples currently buffered.
        /// </summary>
        public int Count => inputs.Count;
    }
}
