/*
 * LinearRegression.cs
 *
 * Implements IModel for simple multivariate regression.
 * Construct with (inputSize, outputSize[, learningRate, iterations]).
 * Call Train(inputs, outputs) to fit, then Predict(input) to obtain a float[] result.
 */

using System.Collections.Generic;
using NUnit.Framework;
using RTMLToolKit;

namespace RTMLToolKit.Tests
{
    [TestFixture]
    public class LinearRegressionTest
    {
        [Test]
        public void SimpleLinearRegressionTest()
        {
            // y = 2x
            var model = new LinearRegression(inputSize: 1, outputSize: 1, learningRate: 0.1f, iterations: 200);
            var inputs = new List<float[]>
            {
                new[] { 1f },
                new[] { 2f },
                new[] { 3f }
            };
            var outputs = new List<float[]>
            {
                new[] { 2f },
                new[] { 4f },
                new[] { 6f }
            };

            model.Train(inputs, outputs);

            var prediction = model.Predict(new[] { 4f });
            // Expect approx 8 (within tolerance)
            Assert.That(prediction[0], Is.EqualTo(8f).Within(0.5f));
        }
    }
}
