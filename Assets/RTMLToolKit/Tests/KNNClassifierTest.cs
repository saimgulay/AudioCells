using System.Collections.Generic;
using NUnit.Framework;
using RTMLToolKit;

namespace RTMLToolKit.Tests
{
    [TestFixture]
    public class KNNClassifierTest
    {
        [Test]
        public void KNNBasicTest()
        {
            var model = new KNNClassifier(inputSize: 1, outputSize: 1, k: 3);
            var inputs = new List<float[]> { new[] { 0f }, new[] { 10f }, new[] { 20f } };
            var outputs = new List<float[]> { new[] { 0f }, new[] { 10f }, new[] { 20f } };

            model.Train(inputs, outputs);

            var prediction = model.Predict(new[] { 5f });
            Assert.That(prediction[0], Is.EqualTo(10f).Within(0.01f));
        }
    }
}
