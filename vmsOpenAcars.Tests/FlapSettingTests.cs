using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La compuerta de flaps que corresponde al porcentaje del mando**, o el porcentaje a secas
    /// cuando la etiqueta no se puede defender.
    ///
    /// El dato que se guarda está documentado y no se inventa aquí: `flare_track.flaps_pct` es
    /// `0x0BDC / 16383 × 100` —`Services/FsuipcService.cs`, offset declarado en la línea 157,
    /// convertido a porcentaje en la 830—, o sea **el recorrido del mando**, no un detente. Las
    /// bandas de esta clase son **los mismos umbrales** que `FsuipcService.DecodeFlapsByFamily`
    /// (líneas 878-959) usa para el rótulo del panel de vuelo, así que lo que se fija aquí es que la
    /// tabla no se desincronice de la del panel.
    ///
    /// **No hay traza real que valide la lectura en vuelo**: `flare_track` está **vacía (0 filas)** en
    /// la base local, así que no se puede comprobar contra un aterrizaje de verdad qué porcentaje
    /// publica un addon en cada compuerta. Estos tests fijan la **conversión** contra los umbrales del
    /// offset y contra el marcado que el cliente ya pinta; no son una validación de vuelo.
    /// </summary>
    [TestClass]
    public class FlapSettingTests
    {
        /// <summary>El porcentaje que corresponde a un valor raw del offset, redondeado como se guarda.</summary>
        private static double PctFromRaw(double raw) => raw / 16383.0 * 100.0;

        // ── Family recognition: Airbus goes CONF, Boeing goes detents ─────────────

        [TestMethod]
        public void Airbus_SeEtiquetaConLaEscalaConf()
        {
            // Centros de banda del A320, en la escala del offset (`DecodeFlapsByFamily` líneas 882-889).
            var casos = new[]
            {
                (Raw:   200.0, Label: "CONF 0"),      // [0, 400)      → 1,2 %
                (Raw:  2450.0, Label: "CONF 1+F"),    // [400, 4500)   → 15,0 %
                (Raw:  6550.0, Label: "CONF 2"),      // [4500, 8600)  → 40,0 %
                (Raw: 10650.0, Label: "CONF 3"),      // [8600, 12700) → 65,0 %
                (Raw: 16383.0, Label: "CONF FULL"),   // [12700, …)    → 100 %
            };

            foreach (var c in casos)
            {
                var s = FlapSetting.Interpret(PctFromRaw(c.Raw), "A320");

                Assert.IsTrue(s.HasValue);
                Assert.AreEqual(FlapLabelSource.AirbusConf, s.Source, $"{c.Raw} raw");
                Assert.AreEqual(c.Label, s.Label, $"{c.Raw} raw");
            }
        }

        [TestMethod]
        public void Boeing737_SeEtiquetaConLosDetentesDelMarcado()
        {
            // Los siete del 737 que cubren este test, con el umbral de `DecodeFlapsByFamily`
            // (líneas 893-904) y el centro de cada banda.
            var casos = new[]
            {
                (Raw:   100.0, Label: "FLAPS UP"),    // [0, 200)
                (Raw:  1224.0, Label: "FLAPS 1"),     // [200, 2248)
                (Raw:  3272.0, Label: "FLAPS 2"),     // [2248, 4296)
                (Raw:  5320.0, Label: "FLAPS 5"),     // [4296, 6344)
                (Raw:  7368.0, Label: "FLAPS 10"),    // [6344, 8392)
                (Raw:  9316.0, Label: "FLAPS 15"),    // [8392, 10240)
                (Raw: 11264.0, Label: "FLAPS 25"),    // [10240, 12288)
                (Raw: 13312.0, Label: "FLAPS 30"),    // [12288, 14336)
                (Raw: 15360.0, Label: "FLAPS 40"),    // [14336, …)
            };

            foreach (var c in casos)
            {
                var s = FlapSetting.Interpret(PctFromRaw(c.Raw), "B737");

                Assert.AreEqual(FlapLabelSource.BoeingDetent, s.Source, $"{c.Raw} raw");
                Assert.AreEqual(c.Label, s.Label, $"{c.Raw} raw");
            }
        }

        /// <summary>
        /// El 777 y el 747 **no tienen los mismos detentes que el 737** —el 777 va `1/5/15/20/25/30`
        /// y el 747 `1/5/10/20/25/30`—, así que se comprueba que cada familia usa la suya y no una
        /// tabla común. Es justo el error que haría que un 777 dijera `FLAPS 10`, que no existe.
        /// </summary>
        [TestMethod]
        public void B777YB747_UsanSuPropioMarcado_NoElDel737()
        {
            // El tramo raw [5460, 8190) —centro 6825— es la compuerta que el 777 llama 15 y el 747
            // llama 10: con una tabla común, uno de los dos diría una compuerta que el avión no tiene.
            Assert.AreEqual("FLAPS 15", FlapSetting.Interpret(PctFromRaw(6825.0), "B777").Label);
            Assert.AreEqual("FLAPS 10", FlapSetting.Interpret(PctFromRaw(6825.0), "B747").Label);

            // Y el 40 solo existe en el 737: en el 777 el fondo de escala es el 30.
            Assert.AreEqual("FLAPS 40", FlapSetting.Interpret(PctFromRaw(15360.0), "B737").Label);
            Assert.AreEqual("FLAPS 30", FlapSetting.Interpret(PctFromRaw(15360.0), "B777").Label);
        }

        /// <summary>
        /// **La familia desconocida se queda en el porcentaje.** El 787 y el 767 los mete
        /// `DecodeFlapsByFamily` en el mismo cajón que el 777 para el rótulo de vuelo, pero esa
        /// licencia no se hereda: no hay un marcado de 787 escrito en ninguna parte del proyecto.
        /// </summary>
        [TestMethod]
        public void FamiliaDesconocida_SoloPorcentaje()
        {
            foreach (string familia in new[] { "B787", "B767", "B757", "E190", "C172", "AT76",
                                               "BCS3", "????", "", null, "A" })
            {
                var s = FlapSetting.Interpret(91.0, familia);

                Assert.IsTrue(s.HasValue, $"«{familia}» tiene porcentaje");
                Assert.IsNull(s.Label, $"«{familia}» no puede llevar etiqueta inventada");
                Assert.AreEqual(FlapLabelSource.PercentageOnly, s.Source, $"«{familia}»");
                Assert.AreEqual("91%", s.Text, $"«{familia}»");
                Assert.AreEqual("FLAPS 91%", s.PirepText, $"«{familia}»");
                Assert.IsFalse(s.IsApproximate, "sin etiqueta no hay nada que marcar como aproximado");
            }
        }

        // ── La tolerancia: en tránsito no se etiqueta ─────────────────────────────

        /// <summary>
        /// **El valor pegado a la frontera entre dos compuertas no se etiqueta**: ahí el mando está en
        /// tránsito y decir «≈FLAPS 25» cuando va camino del 30 es peor que decir el porcentaje. La
        /// franja de duda es el 10 % de la anchura de la banda por cada lado (`BandMargin`), y este
        /// test la fija en el borde exacto: justo en el umbral no se etiqueta, y el 80 % central sí.
        /// </summary>
        [TestMethod]
        public void EnLaFronteraEntreDetentes_NoSeEtiqueta()
        {
            // Frontera 12288 del 737 (= 75,0 %): el 25 acaba ahí y el 30 empieza.
            var enLaFrontera = FlapSetting.Interpret(PctFromRaw(12288.0), "B737");
            Assert.IsNull(enLaFrontera.Label, "en la frontera exacta no se decide compuerta");
            Assert.AreEqual(FlapLabelSource.PercentageOnly, enLaFrontera.Source);

            // Un punto porcentual antes de la frontera sigue siendo 25 (la franja de duda del 25
            // empieza en 12288 − 204,8 = 12083).
            var porDentro = FlapSetting.Interpret(PctFromRaw(12000.0), "B737");
            Assert.AreEqual("FLAPS 25", porDentro.Label);
        }

        /// <summary>
        /// Los extremos del recorrido **no llevan franja de duda**: 0 % y 100 % son posiciones físicas
        /// del mando, no fronteras difusas entre dos compuertas. Sin esto, `FLAPS UP` no se etiquetaría
        /// nunca en el 737 —su banda ocupa 200 de 16383— y el piloto vería `0%` con el mando arriba.
        /// </summary>
        [TestMethod]
        public void LosExtremosDelRecorrido_SiSeEtiquetan()
        {
            Assert.AreEqual("FLAPS UP", FlapSetting.Interpret(0.0, "B737").Label);
            Assert.AreEqual("FLAPS 40", FlapSetting.Interpret(100.0, "B737").Label);
            Assert.AreEqual("CONF 0",   FlapSetting.Interpret(0.0, "A320").Label);
            Assert.AreEqual("CONF FULL", FlapSetting.Interpret(100.0, "A320").Label);
        }

        // ── La etiqueta es una traducción, y se ve ────────────────────────────────

        [TestMethod]
        public void LaEtiquetaSeVeAproximada_YElPorcentajeVaDetras()
        {
            var s = FlapSetting.Interpret(PctFromRaw(13312.0), "B737");   // ≈81 %

            Assert.IsTrue(s.IsApproximate,
                "la compuerta sale de traducir un porcentaje: no es la lectura del detente");
            StringAssert.StartsWith(s.ShortText, "≈");
            Assert.AreEqual("≈FLAPS 30 (81%)", s.Text);

            // En el `notes` del PIREP el matiz va en ASCII: el «≈» no tiene por qué sobrevivir a la
            // base de datos de la aerolínea.
            Assert.AreEqual("~FLAPS 30", s.PirepText);
            Assert.IsTrue(EsAscii(s.PirepText), s.PirepText);
        }

        [TestMethod]
        public void ElPorcentajeSinEtiqueta_EsElTextoYViajaIgual()
        {
            var s = FlapSetting.Interpret(91.4, "B787");

            Assert.AreEqual("91%", s.Text);
            Assert.AreEqual("91%", s.ShortText);
            // En el `notes` va **autodescriptivo**: junto a la meteo, un `91%` suelto no dice de qué es.
            Assert.AreEqual("FLAPS 91%", s.PirepText);
        }

        // ── Degradar sin datos ────────────────────────────────────────────────────

        [TestMethod]
        public void SinDato_NoHayNiPorcentajeNiEtiqueta()
        {
            foreach (double? nada in new double?[] { null, double.NaN, double.PositiveInfinity,
                                                     double.NegativeInfinity })
            {
                var s = FlapSetting.Interpret(nada, "B737");

                Assert.IsFalse(s.HasValue, $"{nada}");
                Assert.IsNull(s.Label);
                Assert.IsNull(s.PirepText, "sin dato no se concatena nada en el PIREP");
                Assert.AreEqual("—", s.Text);
                Assert.AreEqual("—", s.ShortText);
                Assert.AreEqual(FlapLabelSource.None, s.Source);
            }
        }

        /// <summary>
        /// Fuera de 0–100 no hay posición que traducir: el offset va de 0 a 16383 y un valor fuera de
        /// ese rango es un dato corrupto, no una compuerta. Se enseña el número y ninguna etiqueta.
        /// </summary>
        [TestMethod]
        public void FueraDeRango_SoloPorcentaje()
        {
            foreach (double raro in new[] { -0.5, 100.5, 250.0 })
            {
                var s = FlapSetting.Interpret(raro, "B737");
                Assert.IsNull(s.Label, $"{raro}");
                Assert.AreEqual(FlapLabelSource.PercentageOnly, s.Source, $"{raro}");
            }
        }

        // ── El detente real (`0x0BFC`) manda, y no es aproximado ──────────────────

        /// <summary>
        /// **Con el detente real la etiqueta no lleva «≈»**: `flare_track.flaps_index` es la compuerta
        /// que el simulador publica en `0x0BFC`, no una traducción del recorrido del mando. El índice
        /// se mapea posicionalmente a la tabla de su familia (0 = primera banda = `UP`/`CONF 0`), que
        /// es el orden del marcado, y las palabras salen de esa misma tabla para que no haya dos.
        /// </summary>
        [TestMethod]
        public void ConDetenteReal_LaEtiquetaNoEsAproximada()
        {
            // 737, notch 7 = FLAPS 30 (marcado 0=UP,1,2,5,10,15,25,30,40). El mismo 81 % que el test
            // `LaEtiquetaSeVeAproximada`, pero con el detente leído: el «≈» sobra.
            var s = FlapSetting.Interpret(81.0, "B737", 7);

            Assert.AreEqual("FLAPS 30", s.Label);
            Assert.IsFalse(s.IsApproximate, "el detente es una lectura, no una traducción");
            Assert.AreEqual(FlapLabelSource.BoeingDetent, s.Source);
            Assert.AreEqual("FLAPS 30", s.ShortText, "sin detente la misma compuerta iría con «≈»");
            Assert.AreEqual("FLAPS 30 (81%)", s.Text);
            Assert.AreEqual("FLAPS 30", s.PirepText, "en el PIREP no lleva el «~» de aproximado");
            Assert.IsTrue(EsAscii(s.PirepText), s.PirepText);

            // Airbus por su escala CONF (0=CONF 0 … 4=CONF FULL).
            var airbus = FlapSetting.Interpret(65.0, "A320", 3);
            Assert.AreEqual("CONF 3", airbus.Label);
            Assert.IsFalse(airbus.IsApproximate);
            Assert.AreEqual(FlapLabelSource.AirbusConf, airbus.Source);

            // Y el índice usa la tabla de **su** familia: el notch 3 es el 15 en el 777 y el 10 en el
            // 747, que es justo lo que una tabla común diría mal.
            Assert.AreEqual("FLAPS 15", FlapSetting.Interpret(40.0, "B777", 3).Label);
            Assert.AreEqual("FLAPS 10", FlapSetting.Interpret(40.0, "B747", 3).Label);
        }

        /// <summary>
        /// **Sin detente la etiqueta vuelve a ser la traducción, con «≈»**: es el caso de los vuelos
        /// anteriores a `flaps_index` y el de los aviones que no publican `0x0BFC`. El mismo porcentaje
        /// da la misma compuerta, pero el «≈» dice que salió de una banda y no de una lectura.
        /// </summary>
        [TestMethod]
        public void SinDetente_ConPorcentajeEnBanda_LlevaElAproximado()
        {
            var sinDetente = FlapSetting.Interpret(PctFromRaw(13312.0), "B737");

            Assert.AreEqual("FLAPS 30", sinDetente.Label);
            Assert.IsTrue(sinDetente.IsApproximate, "sale de una banda, no del notch");
            Assert.AreEqual("≈FLAPS 30", sinDetente.ShortText);
            Assert.AreEqual("~FLAPS 30", sinDetente.PirepText);
        }

        /// <summary>
        /// **Un detente que la tabla no cubre no se traduce por banda.** El `0x0BFC` ya afirmó una
        /// compuerta que no sabemos leer (índice fuera de la tabla de su familia, o familia sin tabla),
        /// y traducir el porcentaje encima podría dar una compuerta distinta de la que está puesta: se
        /// enseña el porcentaje, sin etiqueta.
        /// </summary>
        [TestMethod]
        public void DetenteFueraDeLaTabla_SoloPorcentaje()
        {
            // El 737 tiene 9 compuertas (índices 0–8): el 9 no existe en esa tabla.
            var fuera = FlapSetting.Interpret(81.0, "B737", 9);
            Assert.IsNull(fuera.Label);
            Assert.AreEqual(FlapLabelSource.PercentageOnly, fuera.Source);
            Assert.AreEqual("81%", fuera.Text);
            Assert.AreEqual("FLAPS 81%", fuera.PirepText);
            Assert.IsFalse(fuera.IsApproximate, "sin etiqueta no hay nada que marcar");

            // Familia desconocida: no hay tabla con la que mapear el índice, así que tampoco se
            // etiqueta (ni por banda: el 787 no tiene marcado en el proyecto).
            var sinFamilia = FlapSetting.Interpret(81.0, "B787", 3);
            Assert.IsNull(sinFamilia.Label);
            Assert.AreEqual("81%", sinFamilia.Text);
        }

        /// <summary>
        /// **Qué detente se puede persistir y cuál no** (`FlapSetting.DetentOrNull`). El byte de
        /// `0x0BFC` tiene un 0 que es a la vez «flaps arriba» y «el addon no escribe el offset»; se
        /// desambigua cruzando el notch con el porcentaje del mando (`0x0BDC`). Lo que no se puede
        /// sostener va `null`, nunca 0.
        /// </summary>
        [TestMethod]
        public void ElDetenteSeGuardaSoloCuandoElAvionLoPublica()
        {
            // Publicado: notch 7 con el mando desplegado.
            Assert.AreEqual(7, FlapSetting.DetentOrNull(7, 81.0));

            // Flaps arriba de verdad: notch 0 con el mando a 0. Es una lectura, no un hueco.
            Assert.AreEqual(0, FlapSetting.DetentOrNull(0, 0.0));
            Assert.AreEqual(0, FlapSetting.DetentOrNull(0, 0.5));

            // El addon no escribe 0x0BFC: devuelve 0 pero el mando está claramente desplegado.
            Assert.IsNull(FlapSetting.DetentOrNull(0, FlapsUp + 0.5), "0 con el mando abajo no es «arriba»");
            Assert.IsNull(FlapSetting.DetentOrNull(0, 40.0));

            // Sin porcentaje con el que contrastar, y sin dato no se inventa nada.
            Assert.IsNull(FlapSetting.DetentOrNull(0, null));
            Assert.IsNull(FlapSetting.DetentOrNull(5, null));

            // Un byte que no puede ser un notch (los marcados del proyecto no pasan de 9) y un
            // porcentaje corrupto tampoco se guardan.
            Assert.IsNull(FlapSetting.DetentOrNull(200, 40.0));
            Assert.IsNull(FlapSetting.DetentOrNull(16, 40.0));
            Assert.IsNull(FlapSetting.DetentOrNull(3, 120.0));
        }

        /// <summary>El punto porcentual a partir del cual los flaps ya no están arriba.</summary>
        private const double FlapsUp = FlapSetting.FlapsUpPercent;

        private static bool EsAscii(string text)
        {
            foreach (char c in text ?? "") if (c > 127) return false;
            return true;
        }
    }
}
