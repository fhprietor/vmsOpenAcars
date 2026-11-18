using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El rótulo del G y el scoring son la misma tabla** (`Helpers/GForceBands`).
    ///
    /// El caso es el PIREP real **`ez9OqzRnYxbnGLR8`** (B738 PMDG, MSFS 2024, SKBO→SKBG, −236 fpm):
    /// el contacto de **1,72 g** se pintó en el registro como `Fuerza G: 1.72g (Normal)` —el rótulo
    /// usaba `< 1.8`— y a la vez el scoring cobró **los 15 puntos de la banda máxima** (`> 1.7`).
    /// El mismo valor no puede ser «Normal» y estar en la banda que más resta; el umbral bueno, el
    /// que confirmó el mantenedor, es **1,7**.
    ///
    /// Un avión de este mismo vuelo trae el segundo valor: **1,65 g** —el contacto del vuelo de
    /// ida, mismo modelo y misma pista—, que sí es de la banda media y cobra 7. Los dos van fijados
    /// aquí con su deducción **y** su rótulo, y el barrido del final comprueba la propiedad que hace
    /// falta de verdad: que no exista ningún valor para el que el rótulo y la deducción digan cosas
    /// distintas.
    /// </summary>
    [TestClass]
    public class GForceBandsTests
    {
        // ── Las dos cifras reales del vuelo ───────────────────────────────────────

        /// <summary>
        /// **1,72 g**: el valor del defecto. Banda máxima (15 pts) y rótulo «Dura», nunca «Normal».
        /// </summary>
        [TestMethod]
        public void ElCasoReal_1_72g_EsBandaMaxima_NoNormal()
        {
            Assert.AreEqual(GForceBand.Hard, GForceBands.Classify(1.72));
            Assert.AreEqual(15, GForceBands.Deduction(1.72), "1,72 g paga los 15 puntos de la banda máxima");
            Assert.AreEqual("Score_Hard", GForceBands.LabelKey(1.72));
            Assert.AreNotEqual("Score_Normal", GForceBands.LabelKey(1.72),
                "lo que se pintaba «Normal» en ez9OqzRnYxbnGLR8");
        }

        /// <summary>
        /// **1,65 g**: dentro de la banda media. 7 puntos y rótulo «Normal», que es donde 1,72 g
        /// ya no está.
        /// </summary>
        [TestMethod]
        public void ElOtroContactoReal_1_65g_EsBandaMedia()
        {
            Assert.AreEqual(GForceBand.Firm, GForceBands.Classify(1.65));
            Assert.AreEqual(7, GForceBands.Deduction(1.65));
            Assert.AreEqual("Score_Normal", GForceBands.LabelKey(1.65));
        }

        // ── Las fronteras del scoring, sin mover ─────────────────────────────────

        [DataTestMethod]
        [DataRow(1.50, 0,  0,  "Score_Perfect", "el borde de arriba de «sin deducción»")]
        [DataRow(1.51, 7,  1,  "Score_Normal",  "1 kt de g por encima ya es banda media")]
        [DataRow(1.70, 7,  1,  "Score_Normal",  "el 1,7 que confirmó el mantenedor es el borde")]
        [DataRow(1.71, 15, 2,  "Score_Hard",    "un pelo por encima de 1,7 ya es la banda máxima")]
        [DataRow(2.50, 15, 2,  "Score_Hard",    "2,5 g sigue en la banda máxima")]
        [DataRow(2.51, 15, 3,  "Score_Crash",   "por encima de 2,5 solo cambia el rótulo")]
        public void LasFronteras_NoSeMueven(double g, int deduction, int bandIndex,
                                            string labelKey, string because)
        {
            Assert.AreEqual(deduction, GForceBands.Deduction(g), because);
            Assert.AreEqual(labelKey,  GForceBands.LabelKey(g),  because);
            Assert.AreEqual(bandIndex, (int)GForceBands.Classify(g), because);
        }

        // ── La propiedad: rótulo y deducción no pueden divergir ──────────────────

        /// <summary>
        /// Barrido de 0,90 a 3,50 g en pasos de 0,01: cada valor cae en **una** banda, y las parejas
        /// «deducción + rótulo» son siempre coherentes (0 ↔ Perfect, 7 ↔ Normal, 15 ↔ Hard/Crash).
        /// Es el test que impide que vuelva a haber dos tablas: si alguien añadiera un umbral al
        /// rótulo sin tocar el scoring, aquí saldría una pareja imposible.
        /// </summary>
        [TestMethod]
        public void NingunValorPuedeTenerRotuloDeUnaBandaYDeduccionDeOtra()
        {
            var permitted = new Dictionary<int, HashSet<string>>
            {
                { 0,  new HashSet<string> { "Score_Perfect" } },
                { 7,  new HashSet<string> { "Score_Normal" } },
                { 15, new HashSet<string> { "Score_Hard", "Score_Crash" } },
            };

            for (int i = 90; i <= 350; i++)
            {
                double g = i / 100.0;
                int deduction = GForceBands.Deduction(g);
                string label  = GForceBands.LabelKey(g);

                Assert.IsTrue(permitted.ContainsKey(deduction),
                    $"{g:0.00} g: deducción {deduction} fuera del baremo 0/7/15");
                Assert.IsTrue(permitted[deduction].Contains(label),
                    $"{g:0.00} g: la banda paga {deduction} pero se rotula «{label}»");
            }
        }

        // ── Y el scoring publicado usa ESA tabla, no una copia ───────────────────

        /// <summary>
        /// Integración: `ScoringService` cobra lo que dice el helper para los dos valores reales. Si
        /// alguien volviera a escribir los umbrales dentro del servicio, este test lo delata.
        /// </summary>
        [DataTestMethod]
        [DataRow(1.72, 15, "el defecto: 1,72 g cobraba ya los 15 puntos")]
        [DataRow(1.65, 7,  "1,65 g es banda media")]
        public void ElScoringCobraLaDeduccionDeLaBanda(double g, int expected, string because)
        {
            var data = new FlightScoreData
            {
                LandingDataCaptured          = true,
                LandingRate                  = -236,   // el mismo toque de ez9OqzRnYxbnGLR8
                LandingGForce                = g,
                LandingBank                  = 0.5,
                LandingPitch                 = 4.0,
                EngineType                   = ScoredEngineType.Turboprop,
                SingleEngineTaxi             = true,
            };

            var result = new ScoringService().Calculate(data);
            int deduction = 0;
            foreach (var d in result.Deductions)
                if (d.Criterion == "G-Force") deduction += d.PointsDeducted;

            Assert.AreEqual(expected, deduction, because);
            Assert.AreEqual(GForceBands.Deduction(g), deduction,
                "el scoring tiene que cobrar exactamente lo que declara la banda");
        }
    }
}
