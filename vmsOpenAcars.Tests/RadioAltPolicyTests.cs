using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La regla del radioaltímetro** (`RadioAltPolicy`): el cero del addon y la altura de ruedas.
    ///
    /// Los números son los de las trazas reales del hilo del AGL (18/11/2026), no geometría
    /// inventada: el **Fenix A320** de `Og9zwbrrmjob0MXv` da **12,15 ft** en tierra, el **ToLiss
    /// A320** de `9o45JZ5eAVZ11qAe` da **0,09–0,67 ft**. El mismo tipo, dos ceros: por eso el cero
    /// viaja con el vuelo y no se deriva del avión.
    /// </summary>
    [TestClass]
    public class RadioAltPolicyTests
    {
        /// <summary>Las 14 muestras en tierra del ToLiss A320 de `9o45JZ5eAVZ11qAe`.</summary>
        private static readonly double[] ToLissGround =
        {
            0.09, 0.18, 0.20, 0.24, 0.27, 0.36, 0.37,
            0.41, 0.48, 0.53, 0.59, 0.60, 0.63, 0.67,
        };

        /// <summary>Las 9 muestras en tierra del Fenix A320 de `Og9zwbrrmjob0MXv`.</summary>
        private static readonly double[] FenixGround =
        {
            11.7831, 12.0159, 12.0159, 12.1027, 12.1513,
            12.2657, 12.2698, 12.3000, 12.3342,
        };

        [TestMethod]
        public void ElCeroDelFenixEs12Coma15_YElDelToLissEs0Coma41()
        {
            Assert.AreEqual(12.1513, RadioAltPolicy.ZeroFrom(FenixGround).Value, 1e-4,
                            "el Fenix A320 mide 12,15 ft en tierra");

            Assert.AreEqual(0.41, RadioAltPolicy.ZeroFrom(ToLissGround).Value, 1e-9,
                            "el ToLiss A320 mide 0,41 ft en tierra: el mismo tipo, otro addon");
        }

        [TestMethod]
        public void LaAlturaDeRuedasEsLaIndicacionMenosElCero()
        {
            // En el umbral de MPTO 03R, el Fenix marcaba 37,37 ft con el cero en 12,15.
            Assert.AreEqual(25.22, RadioAltPolicy.WheelHeight(37.37, 12.15).Value, 1e-9);

            // En el contacto, la indicación baja de 9,3 con el cero en 9,46: la resta es negativa
            // por el ruido y las ruedas no están bajo tierra. El max(0, …) lo corta.
            Assert.AreEqual(0.0, RadioAltPolicy.WheelHeight(9.3, 9.46).Value, 1e-9);
        }

        [TestMethod]
        public void SinRadarOSinCero_NoHayAlturaDeRuedas()
        {
            Assert.IsNull(RadioAltPolicy.WheelHeight(null, 12.15),
                          "sin indicación no se inventa una altura");
            Assert.IsNull(RadioAltPolicy.WheelHeight(37.37, null),
                          "sin cero no se publica la indicación cruda disfrazada");
            Assert.IsNull(RadioAltPolicy.WheelHeight(0.0, 12.15),
                          "un radar de 0 es el centinela de «no lo publica», no un avión en el suelo");
        }

        [TestMethod]
        public void SinMuestrasEnTierra_ElCeroEsNull()
        {
            Assert.IsNull(RadioAltPolicy.ZeroFrom(null));
            Assert.IsNull(RadioAltPolicy.ZeroFrom(new List<double>()),
                          "un vuelo retomado en el aire no tiene cero: null, no 0");
            Assert.IsNull(RadioAltPolicy.ZeroFrom(new[] { 0.0, 0.0 }),
                          "el 0 de «no lo publica» no es un cero físico");
        }
    }
}
