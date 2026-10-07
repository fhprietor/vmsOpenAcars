using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El eje X del closeup, en pies enteros.**
    ///
    /// El encuadre lo fijan las dos escalas de `TouchdownCloseupGeometry` (**5 000 / 4 500** y
    /// **2 500 / 2 500**), y hasta este cambio el motor de gráficos repartía ese rango en cuatro
    /// marcas y **sacaba fracciones de pie**. Lo que se fija aquí: el paso «bonito» de cada escala,
    /// que los rótulos sean enteros con miles separados, que el umbral diga **THR** y que un encuadre
    /// con decimales **no se rotule a mano** en vez de mentir.
    /// </summary>
    [TestClass]
    public class CloseupAxisTests
    {
        // ── Las dos escalas reales del closeup ────────────────────────────────────

        /// <summary>
        /// Escala ancha (5 000 ft antes / 4 500 después): el rango son 9 500 ft, así que el paso de
        /// **2 500 ft** deja **4 rótulos** —`5.000 · 2.500 · THR · 2.500`— que entran enteros en los
        /// ~450 px del perfil vertical.
        ///
        /// Con 1 000 ft salían 10 marcas a ~40 px cada una y el **signo menos se recortaba**:
        /// `-3.000` se pintaba `3.000`, que miente sobre el lado del umbral. Se vio renderizando el
        /// closeup a PNG, y es lo que fija el tope de `MaxLabels` en 9.
        /// </summary>
        [TestMethod]
        public void EscalaAncha_PasoGruesoYRotulosEnteros()
        {
            double step = CloseupAxis.StepFor(5000.0, 4500.0);
            Assert.AreEqual(2500.0, step, 1e-9);
            // Dos marcas antes (2 500 y 5 000) + THR + una después (2 500) = 4.
            Assert.AreEqual(4, CloseupAxis.LabelCount(5000.0, 4500.0, step));
            Assert.IsTrue(CloseupAxis.LabelCount(5000.0, 4500.0, step) <= CloseupAxis.MaxLabels);

            var labels = CloseupAxis.Labels(5000.0, 4500.0);
            var texts  = labels.Select(l => l.Value).ToList();

            CollectionAssert.Contains(texts, "THR", "el umbral tiene que estar rotulado con su nombre");
            CollectionAssert.Contains(texts, "5.000");
            CollectionAssert.Contains(texts, "2.500");

            // Ningún rótulo con fracción: o es THR o son dígitos con los miles separados (el punto
            // **es** el separador, así que se quita antes de comprobar que el valor es entero).
            foreach (string t in texts)
            {
                if (t == CloseupAxis.ThresholdLabel) continue;
                string digits = t.Replace(".", "").Replace("-", "");
                Assert.IsTrue(digits.Length > 0 && digits.All(char.IsDigit),
                              $"el rótulo «{t}» no es un entero con miles separados");
            }
            Assert.AreEqual(0.0, labels.Single(l => l.Value == "THR").Key, 1e-9);
            // Y las marcas son múltiplos del paso, que es lo que las hace caer en la rejilla del eje.
            foreach (var l in labels.Where(l => l.Value != CloseupAxis.ThresholdLabel))
                Assert.AreEqual(0.0, Math.Abs(l.Key % step), 1e-6, $"«{l.Value}» no cae en la rejilla");
        }

        /// <summary>
        /// Escala de cerca (2 500 / 2 500): 5 000 ft de rango y paso de **1 000 ft**. Es el caso que el
        /// mantenedor veía con decimales (−2 250 · −750 · 750 · 2 250).
        /// </summary>
        [TestMethod]
        public void EscalaDeCerca_PasoDe1000FtSinNingunaFraccion()
        {
            double step = CloseupAxis.StepFor(2500.0, 2500.0);
            Assert.AreEqual(1000.0, step, 1e-9);
            Assert.AreEqual(5, CloseupAxis.LabelCount(2500.0, 2500.0, step));

            var labels = CloseupAxis.Labels(2500.0, 2500.0);
            foreach (var l in labels)
            {
                if (l.Value == CloseupAxis.ThresholdLabel) continue;
                // Todos los rótulos son múltiplos del paso: ningún −750 ni −2 250.
                Assert.AreEqual(0.0, Math.Abs(l.Key % step), 1e-6,
                    $"el rótulo {l.Value} no cae en una marca del paso {step}");
            }
            CollectionAssert.Contains(labels.Select(l => l.Value).ToList(), "2.000");
            CollectionAssert.Contains(labels.Select(l => l.Value).ToList(), "1.000");
        }

        // ── El formateo ───────────────────────────────────────────────────────────

        [TestMethod]
        public void LosMilesSeSeparanYNoHayDecimalesNunca()
        {
            Assert.AreEqual("1.500", CloseupAxis.Format(1500.0));
            Assert.AreEqual("500",   CloseupAxis.Format(500.0));
            Assert.AreEqual("0",     CloseupAxis.Format(0.0));
            Assert.AreEqual("-1.500", CloseupAxis.Format(-1500.0));
            // Un valor con fracción se redondea al pie entero: el eje se mide en pies.
            Assert.AreEqual("1.500", CloseupAxis.Format(1499.6));
        }

        // ── Degradar sin datos ────────────────────────────────────────────────────

        /// <summary>
        /// Un encuadre con fracciones (que las escalas del formulario no producen, pero que un
        /// encuadre nuevo podría) **no se rotula a mano**: mejor el eje automático que un rótulo que
        /// miente sobre dónde está el umbral.
        /// </summary>
        [TestMethod]
        public void UnEncuadreConFracciones_NoSeRotulaAManoYNoLanza()
        {
            var labels = CloseupAxis.Labels(5000.5, 4500.25);
            Assert.AreEqual(0, labels.Count, "sin bordes enteros no se inventan rótulos");

            // Y un encuadre vacío o imposible no revienta: en (0, 0) solo queda el rótulo del umbral
            // —el único punto que sigue existiendo—, y un encuadre con los dos bordes negativos no
            // tiene ningún punto del eje dentro, así que no hay rótulos que poner.
            Assert.AreEqual(1, CloseupAxis.Labels(0.0, 0.0).Count);
            Assert.AreEqual(CloseupAxis.ThresholdLabel, CloseupAxis.Labels(0.0, 0.0)[0].Value);
            Assert.AreEqual(1, CloseupAxis.Labels(-5.0, -5.0).Count,
                "aunque los bordes sean negativos, el umbral (x = 0) sigue siendo un punto del eje");
            Assert.AreEqual(0, CloseupAxis.Labels(double.NaN, double.NaN).Count);
            double b = 0.0, a = -3.0;
            CloseupAxis.RoundFrameOut(ref b, ref a);
            Assert.AreEqual(0.0, b, 1e-9);
            Assert.AreEqual(0.0, a, 1e-9);
        }

        // ── El encuadre elástico del gráfico del flare ─────────────────────────────

        /// <summary>
        /// El gráfico del flare no tiene una escala fija: su encuadre sale de la traza. Este test fija
        /// que el redondeo sea **hacia fuera** —nunca puede recortar lo capturado— y a la centena, que
        /// es el redondeo más fino que deja los rótulos en pies enteros.
        /// </summary>
        [TestMethod]
        public void ElEncuadreElastico_SeRedondeaHaciaFueraYLoCapturadoSigueDentro()
        {
            double before = 1641.0, after = 1650.0;
            CloseupAxis.RoundFrameOut(ref before, ref after);

            Assert.IsTrue(before >= 1641.0, "redondear nunca puede recortar la traza");
            Assert.IsTrue(after  >= 1650.0);
            Assert.AreEqual(1700.0, before, 1e-9, "1641 sube a la siguiente centena");
            Assert.AreEqual(1700.0, after, 1e-9, "y 1650 también: redondear a la baja recortaría muestras");
            Assert.AreNotEqual(0, CloseupAxis.Labels(before, after).Count,
                $"el encuadre {before}/{after} tiene que poder rotularse");
            Assert.IsTrue(CloseupAxis.LabelCount(before, after, CloseupAxis.StepFor(before, after))
                          <= CloseupAxis.MaxLabels);
        }

        /// <summary>
        /// Los encuadres del closeup (5 000 / 4 500 y 2 500 / 2 500) **no se mueven**: el arreglo del
        /// eje no puede cambiar el encuadre que fijan `TouchdownCloseupGeometry` y sus tests, y el
        /// redondeo a la centena los deja igual porque ya son múltiplos de 100.
        /// </summary>
        [TestMethod]
        public void LosBordesDelCloseup_NoSeMueven()
        {
            foreach (var (before, after) in new[]
                     {
                         (5000.0, 4500.0), (2500.0, 2500.0), (5000.0, 0.0), (2500.0, 0.0),
                     })
            {
                double b = before, a = after;
                CloseupAxis.RoundFrameOut(ref b, ref a);
                Assert.AreEqual(before, b, 1e-9, "el closeup no cambia de encuadre");
                Assert.AreEqual(after,  a, 1e-9, "el closeup no cambia de encuadre");
            }
        }
    }
}
