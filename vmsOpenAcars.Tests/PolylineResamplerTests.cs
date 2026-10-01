using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El recorte de la polilínea al tope de **500 puntos** que acepta NavData en
    /// `planned.polyline`. La regla no es «cortar»: es conservar el **primer** punto (dónde arrancó
    /// el rodaje) y el **último** (el punto de espera de la pista) y repartir el resto, porque los
    /// dos extremos son justo los que dan sentido a la observación. Un `Take(500)` habría tirado el
    /// punto de espera, que es contra el que se mide «no empeora».
    /// </summary>
    [TestClass]
    public class PolylineResamplerTests
    {
        private const int Max = PolylineResampler.MaxPolylinePoints;

        /// <summary>Polilínea sintética numerada: lat = índice/10000, para poder leer en el test qué
        /// índice salió y comprobar el muestreo sin ambigüedad.</summary>
        private static List<(double Lat, double Lon)> Make(int count)
        {
            var list = new List<(double Lat, double Lon)>();
            for (int i = 0; i < count; i++) list.Add((i / 10000.0, -i / 10000.0));
            return list;
        }

        private static int IndexOf((double Lat, double Lon) p) => (int)Math.Round(p.Lat * 10000.0);

        [TestMethod]
        public void ALongPolylineIsReducedToFiveHundred_WithBothEndsIntact()
        {
            var points  = Make(1200);
            var capped  = PolylineResampler.Cap(points);

            Assert.AreEqual(Max, capped.Count, "el tope son 500 puntos");
            Assert.AreEqual(points[0],    capped[0],        "el primero no se toca");
            Assert.AreEqual(points[1199], capped[Max - 1],  "el último no se toca: es el punto de espera");
        }

        [TestMethod]
        public void TheMiddleIsSampledUniformly_NotTruncated()
        {
            var points = Make(1200);
            var capped = PolylineResampler.Cap(points);

            // Si esto fuera un recorte por el principio, capped[1] sería points[1] y el índice 1199
            // no aparecería hasta el final.
            Assert.AreNotEqual(points[1], capped[1],
                "el segundo punto tiene que ser una muestra, no el siguiente del original");

            // La recta 0..1199 repartida en 499 tramos: el punto i cae en round(i × 1199 / 499).
            for (int i = 0; i < capped.Count; i++)
            {
                int expected = (int)Math.Round((double)i * 1199 / (Max - 1));
                Assert.AreEqual(expected, IndexOf(capped[i]), $"índice del punto muestreado {i}");
            }

            // Y estrictamente creciente y sin repetir: si el muestreo repitiera índices, la
            // polilínea tendría un punto de más y otro de menos.
            for (int i = 1; i < capped.Count; i++)
                Assert.IsTrue(IndexOf(capped[i]) > IndexOf(capped[i - 1]),
                    $"los índices tienen que crecer (posición {i})");
        }

        [TestMethod]
        public void AShortPolylineIsReturnedUntouched()
        {
            var points = Make(3);
            var capped = PolylineResampler.Cap(points);

            Assert.AreEqual(3, capped.Count, "3 puntos → los 3");
            CollectionAssert.AreEqual(points, capped, "y con los mismos valores y el mismo orden");
        }

        [TestMethod]
        public void ExactlyTheLimit_IsNotTouchedEither()
        {
            var points = Make(Max);
            var capped = PolylineResampler.Cap(points);
            Assert.AreEqual(Max, capped.Count);
            CollectionAssert.AreEqual(points, capped);
        }

        [TestMethod]
        public void DegenerateInputs_DoNotThrow()
        {
            Assert.AreEqual(0, PolylineResampler.Cap(null).Count);
            Assert.AreEqual(0, PolylineResampler.Cap(new List<(double, double)>()).Count);

            var one = PolylineResampler.Cap(Make(10), 1);
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual(0.0, one[0].Lat, 1e-9, "con un solo hueco se queda el primero");
        }
    }
}
