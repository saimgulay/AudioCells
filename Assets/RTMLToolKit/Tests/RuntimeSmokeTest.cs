using System.Collections.Generic;
using NUnit.Framework;
using RTMLToolKit;

namespace RTMLToolKit.Tests
{
    [TestFixture]
    public class RuntimeSmokeTest
    {
        [Test]
        public void ModelInstantiationAndPredictLength()
        {
            // LinearRegression
            IModel lr = new LinearRegression(inputSize: 2, outputSize: 3);
            var outLR = lr.Predict(new float[] { 0f, 0f });
            Assert.AreEqual(3, outLR.Length);

            // kNN
            IModel knn = new KNNClassifier(inputSize: 2, outputSize: 2);
            var outKNN = knn.Predict(new float[] { 0f, 0f });
            Assert.AreEqual(2, outKNN.Length);

            // DTW
            IModel dtw = new DTWRecognizer(inputSize: 2, outputSize: 1);
            var outDTW = dtw.Predict(new float[] { 0f, 0f });
            Assert.AreEqual(1, outDTW.Length);
        }

        [Test]
        public void SampleBufferFunctionality()
        {
            var buffer = new SampleBuffer(inputSize: 2, outputSize: 1);
            Assert.AreEqual(0, buffer.Count);

            buffer.AddSample(new[] { 1f, 2f }, new[] { 3f });
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.GetInputs().Count);
            Assert.AreEqual(1, buffer.GetOutputs().Count);

            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
        }
    }
}
