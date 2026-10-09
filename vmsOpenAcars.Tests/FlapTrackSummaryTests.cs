using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Los flaps de un aterrizaje leídos de la traza fina**: qué ajuste había en el umbral, qué
    /// ajuste había en la toma y si cambió por el camino.
    ///
    /// La traza que usan estos tests es **sintética y deliberadamente simple**: `flare_track` está
    /// **vacía (0 filas)** en la base local, así que no hay un aterrizaje real con flaps guardados
    /// contra el que validar. Lo que se fija aquí es la **lectura** de la traza (dónde está el umbral,
    /// dónde la toma, cuándo un cambio cuenta), no un perfil de vuelo concreto.
    /// </summary>
    [TestClass]
    public class FlapTrackSummaryTests
    {
        /// <summary>
        /// Una traza recta: del umbral hacia atrás con `flaps` constante, y el toque en `touchdownAt`.
        /// `dist` va de `from` a `to` en pasos de 25 ft, que a 150 kt son ~0,1 s (10 Hz).
        /// </summary>
        private static List<FlareTrackPoint> Track(double from, double to, double flaps,
                                                   double? touchdownFrom = null)
        {
            var list = new List<FlareTrackPoint>();
            var t = new DateTime(2026, 10, 7, 3, 8, 0, DateTimeKind.Utc);
            int i = 0;
            for (double d = from; d >= to; d -= 25.0, i++)
            {
                list.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t.AddMilliseconds(100 * i),
                    DistFt       = d,
                    AglFt        = 60.0,
                    FlapsPct     = flaps,
                    Eng1Pct      = 58.0,
                    Eng2Pct      = 57.5,
                    OnGround     = touchdownFrom.HasValue && d <= touchdownFrom.Value,
                });
            }
            return list;
        }

        // ── El ajuste del umbral y el de la toma ──────────────────────────────────

        [TestMethod]
        public void ElAjusteDeUmbralYDeToma_SalenDeLaTraza()
        {
            // El umbral en 0 (positivo antes, negativo después) y la toma a −1 700 ft, que es donde
            // toca el vuelo 41. A 150 kt son unos 6,8 s, el THR-TD que ya publica el cliente.
            var s = FlapTrackSummary.Compute(Track(1500.0, -1750.0, 81.25, touchdownFrom: -1700.0), "B737");

            Assert.IsTrue(s.HasTrack);
            Assert.AreEqual(131, s.SampleCount);
            Assert.IsNotNull(s.AtThreshold);
            Assert.IsNotNull(s.AtTouchdown);

            // Los dos son la misma compuerta del 737 —81 % del mando cae en la banda del 30—, así que
            // no hubo cambio.
            Assert.AreEqual("FLAPS 30", s.AtThreshold.Label);
            Assert.AreEqual("FLAPS 30", s.AtTouchdown.Label);
            Assert.IsFalse(s.Changed, "con el mismo ajuste no hay cambio que contar");
        }

        /// <summary>
        /// **Un cambio de flaps en corta final es lo que explica un flotado largo**, así que el
        /// resumen tiene que verlo: aquí el mando baja del 30 al 25 a mitad de la traza, antes de
        /// tocar, y el ajuste del umbral y el de la toma difieren.
        /// </summary>
        [TestMethod]
        public void UnCambioAntesDeLaToma_SeVeEnElResumen()
        {
            var samples = Track(1500.0, -1750.0, 81.25, touchdownFrom: -1700.0);
            // Del 30 al 25 (banda [10240, 12288) → centro 11264 raw ≈ 68,75 %) a partir del umbral.
            foreach (var s in samples)
                if (s.DistFt < 0.0) s.FlapsPct = 68.75;

            var summary = FlapTrackSummary.Compute(samples, "B737");

            Assert.AreEqual("FLAPS 30", summary.AtThreshold.Label);
            Assert.AreEqual("FLAPS 25", summary.AtTouchdown.Label);
            Assert.IsTrue(summary.Changed,
                "bajar el último punto de flap en corta final cambia el asiento del avión");
            Assert.IsTrue(summary.ChangedInCapture);
        }

        // ── Dónde se coge el valor: la muestra, no una interpolación ──────────────

        /// <summary>
        /// El ajuste se coge de la **muestra más cercana**, no interpolando: lo que se busca es una
        /// compuerta puesta, y promediar dos daría una tercera que el avión no tenía. Se comprueba con
        /// la muestra **en el umbral exacto** (`dist = 0`): su ajuste es el del umbral, no el de la
        /// primera muestra pasada.
        /// </summary>
        [TestMethod]
        public void ElUmbralSeCogeDeLaUltimaMuestraAntesDeEl()
        {
            var samples = new List<FlareTrackPoint>
            {
                new FlareTrackPoint { DistFt =  100.0, FlapsPct = 81.25, OnGround = false },
                new FlareTrackPoint { DistFt =   25.0, FlapsPct = 81.25, OnGround = false },
                new FlareTrackPoint { DistFt =    0.0, FlapsPct = 81.25, OnGround = false },
                new FlareTrackPoint { DistFt =  -25.0, FlapsPct = 68.75, OnGround = false },  // ya retraído
                new FlareTrackPoint { DistFt = -100.0, FlapsPct = 68.75, OnGround = true  },
            };

            var s = FlapTrackSummary.Compute(samples, "B737");

            Assert.AreEqual("FLAPS 30", s.AtThreshold.Label,
                "el ajuste del umbral es el de la última muestra todavía delante de él");
            Assert.AreEqual("FLAPS 25", s.AtTouchdown.Label,
                "y el de la toma, el de la última muestra EN EL AIRE");
        }

        // ── Degradar sin datos ────────────────────────────────────────────────────

        [TestMethod]
        public void SinMuestrasDeFlaps_NoHayResumen()
        {
            foreach (var nada in new[] { null, new List<FlareTrackPoint>() })
            {
                var s = FlapTrackSummary.Compute(nada, "B737");

                Assert.IsFalse(s.HasTrack, "sin `flaps_pct` no hay nada que decir");
                Assert.AreEqual(0, s.SampleCount);
                Assert.IsNull(s.AtThreshold);
                Assert.IsNull(s.AtTouchdown);
                Assert.IsFalse(s.Changed);
                Assert.IsTrue(double.IsNaN(s.MinPercent) && double.IsNaN(s.MaxPercent));
            }
        }

        /// <summary>
        /// **Sin transición aire→tierra no se inventa el ajuste de la toma.** Es el caso real de la
        /// primera lectura de la línea del aterrizaje: el ciclo que detecta el toque corre antes de
        /// que la captura guarde su muestra en tierra, así que la traza acaba en el aire. El umbral sí
        /// se puede decir; el de la toma, no.
        /// </summary>
        [TestMethod]
        public void SinToqueEnLaTraza_NoSeInventaElAjusteDeLaToma()
        {
            var s = FlapTrackSummary.Compute(Track(1500.0, -100.0, 81.25), "B737");   // todo en el aire

            Assert.IsTrue(s.HasTrack);
            Assert.IsNotNull(s.AtThreshold);
            Assert.IsNull(s.AtTouchdown, "sin `on_ground` no hay ajuste de toma que enseñar");
            Assert.IsFalse(s.Changed);
        }

        [TestMethod]
        public void MuestrasSinFlaps_SeSaltanSinContar()
        {
            var samples = Track(1500.0, -1750.0, 81.25, touchdownFrom: -1700.0);
            samples[0].FlapsPct = null;
            samples[1].FlapsPct = double.NaN;
            samples[2].FlapsPct = double.PositiveInfinity;

            var s = FlapTrackSummary.Compute(samples, "B737");

            Assert.AreEqual(128, s.SampleCount, "las tres no finitas no cuentan");
            Assert.IsTrue(s.HasTrack);
        }

        // ── El cambio, con la histéresis que ya usa el detector de vuelo ──────────

        /// <summary>
        /// El umbral de cambio es **1 punto**, la misma histéresis que el detector de cambios de flaps
        /// en vuelo (`FsuipcService.DetectFlapsChange`, 1 %): la traza fina dice «cambió» exactamente
        /// cuando el log de vuelo lo dijo. Un movimiento menor no cuenta.
        /// </summary>
        [TestMethod]
        public void UnMovimientoMenorQueLaHisteresis_NoCuentaComoCambio()
        {
            var samples = Track(1500.0, -1750.0, 81.25, touchdownFrom: -1700.0);
            foreach (var sample in samples) if (sample.DistFt < 0.0) sample.FlapsPct = 80.75;   // medio punto

            var s = FlapTrackSummary.Compute(samples, "B737");

            Assert.IsFalse(s.Changed, "medio punto no es un cambio de compuerta");
            Assert.IsFalse(s.ChangedInCapture);
        }

        [TestMethod]
        public void ElRecorridoDelMando_SeMideEnLaCaptura()
        {
            var samples = Track(1500.0, -1750.0, 81.25, touchdownFrom: -1700.0);
            foreach (var sample in samples) if (sample.DistFt < -500.0) sample.FlapsPct = 100.0;

            var s = FlapTrackSummary.Compute(samples, "B737");

            Assert.AreEqual(81.25, s.MinPercent, 1e-9);
            Assert.AreEqual(100.0, s.MaxPercent, 1e-9);
            Assert.IsTrue(s.ChangedInCapture);
        }

        /// <summary>
        /// Con la familia desconocida el resumen **no se pierde**: sigue habiendo porcentaje de umbral
        /// y de toma, y es la pantalla la que enseña el número. No se inventa una compuerta.
        /// </summary>
        [TestMethod]
        public void FamiliaDesconocida_DaPorcentajesSinEtiqueta()
        {
            var s = FlapTrackSummary.Compute(Track(1500.0, -1750.0, 81.25, touchdownFrom: -1700.0), null);

            Assert.IsTrue(s.HasTrack);
            Assert.IsNotNull(s.AtThreshold);
            Assert.IsNull(s.AtThreshold.Label);
            Assert.AreEqual("81%", s.AtThreshold.Text);
            Assert.IsNotNull(s.AtTouchdown);
        }
    }
}
