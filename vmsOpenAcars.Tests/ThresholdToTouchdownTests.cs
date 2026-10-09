using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El tiempo desde el cruce del umbral hasta el contacto**, el dato que pidió un piloto.
    ///
    /// El caso base es el **vuelo 41 de la base local** (SKBG → SKCG, pista 01, toque a
    /// **1 736,4 ft**, pista de 7 841 ft), el mismo perfil real que usan `TouchdownCloseupGeometryTests`,
    /// `FlareChartLayoutTests`, `LandingCloseupFormTests` y `FlareAnalysisFormTests`. Sobre esos nudos
    /// reales —distancia al umbral y AGL— se monta la traza **fina** como la captura de verdad: una
    /// muestra cada **0,1 s**, que a los **150 kt** del tramo son **25,3 ft por muestra**. El contacto
    /// se marca en la distancia real de la toma (`flights.touchdown_dist_ft = 1 736,398 ft`), **no**
    /// en el cruce del umbral: marcarlo ahí daría un tiempo de cero, que es justo el invento que el
    /// helper tiene que rechazar.
    ///
    /// Aquí se fijan las cuatro cosas que no pueden volver a romperse:
    ///
    /// 1. **La interpolación**: el valor sale de interpolar el cruce entre las dos muestras que lo
    ///    rodean y el contacto en el intervalo del salto a tierra, no de coger la muestra más cercana.
    /// 2. **El dato cuadra** con `touchdown_dist_ft` y con la velocidad del tramo: 1 736,4 ft a 150 kt
    ///    ≈ 6,9 s, y el helper devuelve **6,8 s**.
    /// 3. **La traza de 2 s no se publica**: se señala (`CoarseTrack`), no se calcula como si fuera fina.
    /// 4. **Sin datos no hay número**: sin traza, sin cruce, sin transición a tierra o con una sola
    ///    muestra se devuelve un motivo, nunca un 0 ni una aproximación.
    /// </summary>
    [TestClass]
    public class ThresholdToTouchdownTests
    {
        /// <summary>Pies por segundo y nudo: `6076,12 ft/NM ÷ 3600 s`, la misma conversión que usa
        /// `TouchdownCloseupGeometry.FeetPerNm`.</summary>
        private const double FeetPerSecondPerKnot = 1.68781;

        /// <summary>La distancia de toma real del vuelo 41 (`flights.touchdown_dist_ft`).</summary>
        private const double Skcg41TouchdownFt = 1736.398;

        // ── Los casos base: el vuelo 41 ──────────────────────────────────────────

        /// <summary>
        /// **La traza fina del vuelo 41**, de 1 500 ft antes del umbral hasta la toma: los nudos
        /// reales de `approach_track` (los mismos que `FlareChartLayoutTests.Skcg41Flare`) y una
        /// muestra cada 0,1 s a los 150 kt del tramo. El contacto va donde la base dice que tocó.
        /// </summary>
        internal static List<FlareTrackPoint> Skcg41FineTrack()
        {
            // (dist_ft al umbral, agl_ft) reales del vuelo 41, de antes del umbral a después.
            var knots = new[]
            {
                (1653.8, 147.0), (1103.0, 131.0), (558.3, 119.0), (20.7, 102.0),
                (-499.3, 80.0), (-1020.6, 60.0), (-1541.7, 50.0),
            };

            const double gsKt = 150.0;
            double stepFt = gsKt * FeetPerSecondPerKnot * 0.1;   // 25,31715 ft por muestra
            var t0 = new DateTime(2026, 10, 7, 3, 8, 50, DateTimeKind.Utc);

            var samples = new List<FlareTrackPoint>();
            int index = 0;
            double distFt = 1500.0;
            while (true)
            {
                bool onGround = distFt <= -Skcg41TouchdownFt;
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = index,
                    TimestampUtc = t0.AddMilliseconds(100 * index),
                    DistFt       = distFt,
                    AglFt        = onGround ? 0.0 : AglAt(knots, distFt),
                    IasKt        = gsKt,
                    OnGround     = onGround,
                });
                if (onGround) break;
                index++;
                distFt -= stepFt;
            }
            return samples;
        }

        /// <summary>
        /// **La traza de 2 s del vuelo 42** (SKCG → MMTL, pista 12, `touchdown_dist_ft = 3 949,5 ft`),
        /// el vuelo del log `196 muestras (stopped:post-touchdown 2.1s)`. Cruza el umbral y toca
        /// tierra —los dos hitos están—, pero su cadencia es la de `approach_track`: por eso el
        /// helper tiene que señalar la traza, no calcular el número.
        /// </summary>
        private static List<FlareTrackPoint> Skcg42CoarseTrack()
        {
            const double touchdownFt = 3949.5;
            const double gsKt = 150.0;
            const double cadenceSec = 2.0;
            double stepFt = gsKt * FeetPerSecondPerKnot * cadenceSec;   // ≈ 506 ft por muestra
            var t0 = new DateTime(2026, 10, 8, 4, 35, 0, DateTimeKind.Utc);

            var samples = new List<FlareTrackPoint>();
            int index = 0;
            double distFt = 1500.0;
            while (true)
            {
                bool onGround = distFt <= -touchdownFt;
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = index,
                    TimestampUtc = t0.AddSeconds(cadenceSec * index),
                    DistFt       = distFt,
                    AglFt        = onGround ? 0.0 : 100.0,
                    IasKt        = gsKt,
                    OnGround     = onGround,
                });
                if (onGround) break;
                index++;
                distFt -= stepFt;
            }
            return samples;
        }

        /// <summary>AGL interpolado de los nudos reales, para que la traza sintética no lleve una
        /// altitud inventada. El helper no lo usa: aquí solo da realismo a la muestra.</summary>
        private static double AglAt((double DistFt, double AglFt)[] knots, double distFt)
        {
            if (distFt >= knots[0].DistFt) return knots[0].AglFt;
            for (int i = 1; i < knots.Length; i++)
            {
                if (distFt >= knots[i].DistFt)
                {
                    var a = knots[i - 1];
                    var b = knots[i];
                    double w = (a.DistFt - distFt) / (a.DistFt - b.DistFt);
                    return a.AglFt + (b.AglFt - a.AglFt) * w;
                }
            }
            return knots[knots.Length - 1].AglFt;
        }

        // ── 1. El dato, con la interpolación y cuadrando con la distancia ─────────

        /// <summary>
        /// **El vuelo 41: 6,8 s del umbral a la toma.**
        ///
        /// La comprobación que hace que el número valga es la de la derecha: el tiempo que sale tiene
        /// que cuadrar con la distancia de toma real (1 736,4 ft) y con la velocidad del tramo
        /// (150 kt), que dan 1 736,4 ÷ 253,17 ft/s = **6,86 s**. Y el valor devuelto es **6,8 s**,
        /// redondeado a la décima: la diferencia de 0,06 s es el medio intervalo del salto a tierra
        /// (el contacto se interpola en el punto medio), no un error de cálculo.
        /// </summary>
        [TestMethod]
        public void Vuelo41_LaTrazaFinaDa68Segundos_YCUADRAConLaDistanciaYLaVelocidad()
        {
            var result = ThresholdToTouchdown.Compute(Skcg41FineTrack());

            Assert.AreEqual(ThresholdToTouchdownStatus.Ok, result.Status);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(6.8, result.Seconds, 1e-9,
                "el tiempo umbral→toma del vuelo 41, redondeado a 0,1 s");
            Assert.AreEqual(129, result.SampleCount, "1 500 ft antes del umbral hasta la toma, a 0,1 s");
            Assert.AreEqual(0.1, result.MedianSampleIntervalSec, 1e-9,
                "la traza es fina: 10 Hz");

            // El cuadre: distancia real de toma / velocidad del tramo.
            double expectedSec = Skcg41TouchdownFt / (150.0 * FeetPerSecondPerKnot);
            Assert.AreEqual(6.8586, expectedSec, 5e-4);
            Assert.IsTrue(Math.Abs(result.Seconds - expectedSec) <= 0.2,
                $"el dato no cuadra con la distancia y la velocidad: {result.Seconds:F1} s contra {expectedSec:F2} s");

            // Y con la unidad dicha, tanto en el PIREP como en pantalla.
            Assert.AreEqual("THR-TD 6.8s", ThresholdToTouchdown.PirepSuffix(result));
            Assert.AreEqual("6.8 s", ThresholdToTouchdown.FormatSeconds(result.Seconds));
        }

        /// <summary>
        /// **Se interpola, no se coge la muestra más cercana.** El caso está montado para que la
        /// diferencia se **vea en el valor publicado**, no solo en el cálculo interno: el cruce cae a
        /// 0,04 s de la muestra anterior (fracción 0,4 del intervalo) y el contacto a 0,55 s, así que
        /// el tiempo real es 0,51 s → **0,5 s** redondeado, mientras que quedarse con la muestra
        /// anterior al umbral daría 0,55 s → **0,6 s**. La traza es de 0,1 s por muestra, como la
        /// captura de verdad.
        /// </summary>
        [TestMethod]
        public void ElCruceDelUmbral_SeInterpolaEntreLasDosMuestrasQueLoRodean()
        {
            var t0 = new DateTime(2026, 10, 7, 3, 9, 0, DateTimeKind.Utc);
            var samples = new List<FlareTrackPoint>();
            for (int i = 0; i <= 10; i++)
            {
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t0.AddMilliseconds(100 * i),
                    // +10 ft en la muestra 0 y −15 ft en la 1: el cero cae al 40 % del intervalo.
                    DistFt       = 10.0 - 25.0 * i,
                    AglFt        = 20.0 - i,
                    OnGround     = i >= 6,          // el salto a tierra, entre la 5 (0,5 s) y la 6 (0,6 s)
                });
            }

            var result = ThresholdToTouchdown.Compute(samples);
            Assert.AreEqual(ThresholdToTouchdownStatus.Ok, result.Status);

            // El cálculo a mano: cruce interpolado (0,04 s) y contacto en el punto medio (0,55 s).
            double crossingSec = 0.04;
            double contactSec  = 0.55;
            Assert.AreEqual(0.51, contactSec - crossingSec, 1e-9);
            Assert.AreEqual(0.5, result.Seconds, 1e-9, "0,51 s redondeado a la décima");

            // Y **no** es la muestra más cercana: la de antes del umbral daría 0,6 s.
            Assert.AreEqual(0.6, Math.Round(contactSec - 0.0, 1, MidpointRounding.AwayFromZero), 1e-9);
            Assert.AreNotEqual(0.6, result.Seconds,
                "el valor tiene que salir de interpolar los dos extremos, no de la muestra más cercana");

            // En el vuelo 41 la interpolación también manda, aunque ahí el redondeo a la décima
            // esconda la diferencia: se comprueba contra el mismo cálculo hecho a mano.
            var real = Skcg41FineTrack();
            int before = -1;
            for (int i = 0; i < real.Count - 1; i++)
                if (real[i].DistFt >= 0.0 && real[i + 1].DistFt < 0.0) { before = i; break; }
            Assert.IsTrue(before > 0, "la traza del vuelo 41 tiene que cruzar el umbral");

            double d0 = real[before].DistFt;
            double d1 = real[before + 1].DistFt;
            double crossingReal = (real[before].TimestampUtc - real[0].TimestampUtc).TotalSeconds
                                  + 0.1 * (d0 / (d0 - d1));
            int airborne = -1;
            for (int i = 1; i < real.Count; i++)
                if (!real[i - 1].OnGround && real[i].OnGround) { airborne = i - 1; break; }
            double contactReal = (real[airborne].TimestampUtc - real[0].TimestampUtc).TotalSeconds + 0.05;

            Assert.AreEqual(Math.Round(contactReal - crossingReal, 1, MidpointRounding.AwayFromZero),
                            ThresholdToTouchdown.Compute(real).Seconds, 1e-9,
                "y en el vuelo 41 el mismo cálculo a mano da el mismo resultado");
        }

        // ── 2. La traza de 2 s: se señala, no se publica ─────────────────────────

        /// <summary>
        /// **La traza de 2 s no se publica.** El vuelo 42 cruza el umbral y tiene su transición a
        /// tierra —los dos extremos están—, y aun así el helper no devuelve número: con 2 s por
        /// muestra, interpolar da ±1 s de error, y un dato con esa barra disfrazado de décimas es peor
        /// que no tenerlo. Se dice **por qué** (`CoarseTrack`) para que el llamante pueda callarse.
        /// </summary>
        [TestMethod]
        public void LaTrazaDe2s_NoSePublica_YSeSenalaElMotivo()
        {
            var coarse = Skcg42CoarseTrack();

            // El test no puede pasar «porque falte el cruce o el contacto»: los dos hitos están.
            Assert.IsTrue(coarse.Any(s => s.DistFt >= 0.0)
                          && coarse.Any(s => s.DistFt < 0.0),
                "la traza de 2 s del vuelo 42 cruza el umbral");
            Assert.IsTrue(Enumerable.Range(1, coarse.Count - 1)
                                    .Any(i => !coarse[i - 1].OnGround && coarse[i].OnGround),
                "y tiene su transición a tierra: lo que la descalifica es la cadencia");

            var result = ThresholdToTouchdown.Compute(coarse);

            Assert.AreEqual(ThresholdToTouchdownStatus.CoarseTrack, result.Status);
            Assert.IsFalse(result.HasValue, "con traza gruesa no hay valor que publicar");
            Assert.IsTrue(double.IsNaN(result.Seconds), "nada de un número a medias: NaN");
            Assert.AreEqual(2.0, result.MedianSampleIntervalSec, 1e-9);
            Assert.IsNull(ThresholdToTouchdown.PirepSuffix(result),
                "y al PIREP no viaja nada");
        }

        // ── 3. Degradar sin datos ────────────────────────────────────────────────

        /// <summary>Sin cruce identificable no hay tiempo: la traza se quedó antes del umbral.</summary>
        [TestMethod]
        public void SinCruceDelUmbral_NoDevuelveNumero()
        {
            var samples = new List<FlareTrackPoint>();
            var t0 = new DateTime(2026, 10, 7, 3, 8, 50, DateTimeKind.Utc);
            for (int i = 0; i < 60; i++)
            {
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t0.AddMilliseconds(100 * i),
                    // Siempre antes del umbral, y el último ya en tierra: el contacto existe, el
                    // cruce no.
                    DistFt       = 1200.0 - i * 17.0,
                    AglFt        = 100.0 - i,
                    OnGround     = i >= 55,
                });
            }
            Assert.IsTrue(samples.Last().DistFt > 0.0, "la traza de este caso no llega al umbral");

            var result = ThresholdToTouchdown.Compute(samples);

            Assert.AreEqual(ThresholdToTouchdownStatus.NoThresholdCrossing, result.Status);
            Assert.IsFalse(result.HasValue);
            Assert.IsTrue(double.IsNaN(result.Seconds));
        }

        /// <summary>Sin transición a tierra no hay contacto que fechar: el avión sigue en el aire.</summary>
        [TestMethod]
        public void SinTransicionATierra_NoDevuelveNumero()
        {
            var samples = new List<FlareTrackPoint>();
            var t0 = new DateTime(2026, 10, 7, 3, 8, 50, DateTimeKind.Utc);
            for (int i = 0; i < 120; i++)
            {
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t0.AddMilliseconds(100 * i),
                    DistFt       = 900.0 - i * 25.31715,     // cruza el umbral
                    AglFt        = 90.0 - i * 0.5,
                    OnGround     = false,
                });
            }
            Assert.IsTrue(samples.Any(s => s.DistFt < 0.0), "este caso sí cruza el umbral");

            var result = ThresholdToTouchdown.Compute(samples);

            Assert.AreEqual(ThresholdToTouchdownStatus.NoGroundTransition, result.Status);
            Assert.IsFalse(result.HasValue);
        }

        /// <summary>
        /// **Un solo punto no es una traza**: sin dos muestras no hay intervalo que interpolar. Es el
        /// caso de una captura que armó y se quedó en la primera muestra.
        /// </summary>
        [TestMethod]
        public void UnSoloPunto_NoDevuelveNumero()
        {
            var one = new List<FlareTrackPoint>
            {
                new FlareTrackPoint
                {
                    SeqNo = 0, TimestampUtc = new DateTime(2026, 10, 7, 3, 8, 50, DateTimeKind.Utc),
                    DistFt = 1400.0, AglFt = 140.0, OnGround = false,
                }
            };

            var result = ThresholdToTouchdown.Compute(one);

            Assert.AreEqual(ThresholdToTouchdownStatus.NoTrack, result.Status);
            Assert.IsFalse(result.HasValue);
            Assert.IsTrue(double.IsNaN(result.Seconds));
        }

        /// <summary>Sin traza —null, vacía, o con muestras sin instante— la respuesta es no tener dato.</summary>
        [TestMethod]
        public void SinTraza_NoDevuelveNumero()
        {
            foreach (var nothing in new IList<FlareTrackPoint>[]
                     {
                         null,
                         new List<FlareTrackPoint>(),
                         new List<FlareTrackPoint> { null, null },
                         // Muestras sin instante: no hay con qué interpolar.
                         new List<FlareTrackPoint>
                         {
                             new FlareTrackPoint { DistFt = 500.0, OnGround = false },
                             new FlareTrackPoint { DistFt = -500.0, OnGround = true },
                         },
                     })
            {
                var result = ThresholdToTouchdown.Compute(nothing);
                Assert.AreEqual(ThresholdToTouchdownStatus.NoTrack, result.Status);
                Assert.IsFalse(result.HasValue);
                Assert.IsNull(ThresholdToTouchdown.PirepSuffix(result));
            }
        }

        // ── 4. Las claves de idioma, simétricas ──────────────────────────────────

        /// <summary>
        /// **El texto del dato existe en los dos idiomas y son simétricos.** Se leen los dos `.json`
        /// del repositorio porque el proyecto de tests no despliega `Languages\`: la clave del valor
        /// tiene que llevar su hueco y su unidad, y la del rótulo del logbook tiene que existir, todo
        /// en español y en inglés.
        /// </summary>
        [TestMethod]
        public void LasClavesDelDato_EstanEnLosDosIdiomasYConSuUnidad()
        {
            var es = LoadLanguage("es.json");
            var en = LoadLanguage("en.json");
            if (es.Count == 0 || en.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            CollectionAssert.AreEquivalent(es.Keys.ToList(), en.Keys.ToList(),
                "es.json y en.json tienen que seguir siendo simétricos");

            foreach (var file in new[] { es, en })
            {
                Assert.IsTrue(file.ContainsKey("Landing_ThrToTd"), "falta «Landing_ThrToTd»");
                Assert.IsTrue(file.ContainsKey("Landing_ThrToTdHeader"), "falta «Landing_ThrToTdHeader»");
                StringAssert.Contains(file["Landing_ThrToTd"], "{0}",
                    "el valor necesita su hueco para el número");
                StringAssert.Contains(file["Landing_ThrToTd"], "THR-TD");
                StringAssert.Contains(file["Landing_ThrToTd"], "s",
                    "y la unidad de tiempo, que es parte del dato");
            }
        }

        /// <summary>Los archivos de idioma, con la misma búsqueda de rutas que usa `CloseupTrackSourceTests`.</summary>
        private static Dictionary<string, string> LoadLanguage(string fileName)
        {
            foreach (string rel in new[]
            {
                Path.Combine("Languages", fileName),
                Path.Combine(@"..\..\vmsOpenAcars\Languages", fileName),
                Path.Combine(@"..\..\..\vmsOpenAcars\bin\Debug\Languages", fileName),
                Path.Combine(@"..\vmsOpenAcars\bin\Debug\Languages", fileName),
            })
            {
                try
                {
                    if (!File.Exists(rel)) continue;

                    var dict = new Dictionary<string, string>();
                    foreach (Match m in Regex.Matches(File.ReadAllText(rel),
                                 "\"(?<k>[^\"]+)\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\""))
                        dict[m.Groups["k"].Value] = Regex.Unescape(m.Groups["v"].Value);

                    if (dict.Count > 0) return dict;
                }
                catch { }
            }
            return new Dictionary<string, string>();
        }
    }
}
