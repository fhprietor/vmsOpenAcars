using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Qué traza pinta el closeup y con qué rótulo se declara.**
    ///
    /// Aquí se fijan las tres cosas que no pueden volver a romperse:
    ///
    /// 1. **El signo de `dist_ft`**: la traza fina es positiva **antes** del umbral y negativa
    ///    después, y ese **es** el convenio del eje X del closeup. La traza ya se tropezó con esto
    ///    —la distancia de toma, que va en positivo, se confundía con la X del gráfico y el punto de
    ///    toque salía del lado equivocado—, así que el test compara las dos convenciones y exige que
    ///    apunten al **mismo** sitio.
    /// 2. **La decisión de fuente**: con traza fina manda la de 10 Hz; sin ella, la de 2 s, que es lo
    ///    que había. Y la altitud se corta en el toque, como en la ventana del flare.
    /// 3. **El rótulo**: las dos resoluciones se dicen con claves distintas, presentes y simétricas en
    ///    `es.json` y `en.json`, y el texto de cada una nombra su resolución (10 Hz frente a 2 s).
    ///
    /// Los perfiles son los del **vuelo 41 de la base local** (SKBG → SKCG 01, toque a 1 736 ft,
    /// pista de 7 841 ft), los mismos que usan `FlareChartLayoutTests` y `LandingCloseupFormTests`.
    /// </summary>
    [TestClass]
    public class CloseupTrackSourceTests
    {
        private const double Skcg01RunwayFt = 7841.0;

        // ── El signo: la traza fina y la marca del toque tienen que caer en la misma X ──

        /// <summary>
        /// **El test del signo.** El perfil es el del vuelo 41 (los mismos AGL reales de
        /// `approach_track`, muestreados a 10 Hz) y el toque está donde la base dice:
        /// `touchdown_dist_ft = 1 736 ft` pasada la pista, que en la traza es `dist_ft = −1 736`.
        ///
        /// Las dos convenciones apuntan al **mismo** sitio y el test lo exige: la X de la curva es
        /// `dist_ft` **literal** (positiva antes del umbral, negativa después) y la marca del toque
        /// es `−touchdown_dist_ft`. Si alguien le diera la vuelta a una sola de las dos —que es el
        /// fallo en el que ya se tropezó: la distancia de toma se confundía con la X del gráfico—, la
        /// curva y la marca se irían a lados distintos del umbral y esto cae.
        /// </summary>
        [TestMethod]
        public void LaTrazaFinaConservaElSigno_YElToqueCaeEnLaMismaXQueLaMarca()
        {
            var samples = new List<FlareTrackPoint>();
            // (dist_ft, agl_ft) del vuelo 41, y la toma a los 1 736 ft de `touchdown_dist_ft`.
            foreach (var (distFt, aglFt) in new[]
                     {
                         (1653.8, 147.0), (1103.0, 131.0), (558.3, 119.0), (20.7, 102.0),
                         (-499.3, 80.0), (-1020.6, 60.0), (-1541.7, 40.0), (-1736.0, 0.0),
                     })
            {
                samples.Add(new FlareTrackPoint
                {
                    DistFt   = distFt,
                    AglFt    = aglFt,
                    OnGround = distFt <= -1736.0,
                });
            }

            var points = CloseupTrackSource.FlarePoints(samples);

            // La curva llega más allá del umbral con X **negativa**: la traza no se da la vuelta.
            Assert.AreEqual(1653.8, points[0].XFt, 1e-9, "antes del umbral la X es positiva");
            Assert.AreEqual(-1541.7, points[points.Count - 1].XFt, 1e-9,
                "pasado el umbral la X es negativa: el mismo signo que `dist_ft`");
            Assert.IsTrue(points.Any(p => p.XFt < 0.0), "la traza capturada pasado el umbral se pinta");

            // Y la X es **literalmente** `dist_ft`: no hay conversión escondida en la traza.
            foreach (var p in points)
                Assert.IsTrue(samples.Any(s => !s.OnGround && s.AglFt.HasValue
                                               && Math.Abs(s.DistFt - p.XFt) < 1e-9),
                              $"la X {p.XFt} no sale de ningún `dist_ft` de la traza");

            // La marca del toque, que viene en POSITIVO (1 736 ft pasada la pista), cae en la misma
            // X negativa que la última muestra de la captura: la del toque.
            var onGround = samples.First(s => s.OnGround);
            Assert.AreEqual(-1736.0, onGround.DistFt, 1e-9);

            var cu = TouchdownCloseupGeometry.Compute(
                1736.0, Skcg01RunwayFt,
                TouchdownCloseupGeometry.WideBeforeFt, TouchdownCloseupGeometry.WideAfterFt);
            Assert.AreEqual(-1736.0, cu.TouchdownX, 1e-9);
            Assert.AreEqual(onGround.DistFt, cu.TouchdownX, 1e-9,
                "la traza y la marca del toque tienen que estar en la misma X del gráfico");
        }

        /// <summary>
        /// **La X no se toca, la altitud sí.** En tierra el AGL vale 0 por definición: seguir
        /// pintándolo dibujaba la caída vertical de la última altura y una raya pegada a la banda de
        /// pista durante toda la frenada. Se corta en el toque —el mismo corte que hace la ventana del
        /// flare— y el punto de toque no se pierde, porque la marca sale de `touchdown_dist_ft`.
        /// </summary>
        [TestMethod]
        public void LaAltitudSeCortaEnElToque_PeroLaXMuestreadaSigue()
        {
            var samples = new List<FlareTrackPoint>
            {
                new FlareTrackPoint { DistFt =  900.0, AglFt = 80.0, OnGround = false },
                new FlareTrackPoint { DistFt =  100.0, AglFt = 20.0, OnGround = false },
                new FlareTrackPoint { DistFt = -200.0, AglFt =  0.0, OnGround = true  },
                new FlareTrackPoint { DistFt = -700.0, AglFt =  0.0, OnGround = true  },
            };

            var points = CloseupTrackSource.FlarePoints(samples);

            Assert.AreEqual(2, points.Count, "las dos muestras en tierra no se pintan");
            Assert.AreEqual(900.0,  points[0].XFt, 1e-9);
            Assert.AreEqual(100.0,  points[1].XFt, 1e-9);
            Assert.IsFalse(points.Any(p => p.AltFt == 0.0), "no queda la raya a AGL 0 sobre la pista");
        }

        /// <summary>
        /// **Sin AGL no hay punto, y el radioaltímetro no lo sustituye aquí**: el eje del closeup es
        /// AGL y la ventana del flare es la que pinta el radioaltímetro. Mezclar los dos en la misma
        /// línea dibujaría saltos allí donde una muestra tiene uno y la otra no.
        /// </summary>
        [TestMethod]
        public void SinAglNoHayPunto_YElRadioaltimetroNoSustituyeAlAglEnElCloseup()
        {
            var samples = new List<FlareTrackPoint>
            {
                new FlareTrackPoint { DistFt = 500.0, AglFt = 60.0 },
                new FlareTrackPoint { DistFt = 400.0, AglFt = null, RadarAltFt = 55.0 },
                new FlareTrackPoint { DistFt = double.NaN, AglFt = 40.0 },
                null,
            };

            var points = CloseupTrackSource.FlarePoints(samples);

            Assert.AreEqual(1, points.Count);
            Assert.AreEqual(500.0, points[0].XFt, 1e-9);
            Assert.AreEqual(60.0,  points[0].AltFt, 1e-9);

            Assert.AreEqual(0, CloseupTrackSource.FlarePoints(null).Count);
        }

        // ── La decisión de fuente ────────────────────────────────────────────────

        /// <summary>
        /// **La traza fina manda cuando existe**, y sin ella todo queda exactamente como estaba: la
        /// de 2 s. Los vuelos anteriores a `flare_track` no notan este cambio.
        /// </summary>
        [TestMethod]
        public void ConTrazaFinaMandaElFlare_YSinEllaSeQuedaLaDeDosSegundos()
        {
            Assert.AreEqual(CloseupTraceSource.Flare,    CloseupTrackSource.Pick(1));
            Assert.AreEqual(CloseupTraceSource.Flare,    CloseupTrackSource.Pick(400));
            Assert.AreEqual(CloseupTraceSource.Approach, CloseupTrackSource.Pick(0));
            Assert.AreEqual(CloseupTraceSource.Approach, CloseupTrackSource.Pick(-3));

            Assert.AreEqual(CloseupTrackSource.FlareLabelKey,    CloseupTrackSource.LabelKey(CloseupTraceSource.Flare));
            Assert.AreEqual(CloseupTrackSource.ApproachLabelKey, CloseupTrackSource.LabelKey(CloseupTraceSource.Approach));
            Assert.AreNotEqual(CloseupTrackSource.FlareLabelKey, CloseupTrackSource.ApproachLabelKey,
                "las dos resoluciones no pueden rotularse igual");
        }

        /// <summary>
        /// La traza de 2 s se lleva a pies con **la misma** conversión que el encuadre y el eje X
        /// (`TouchdownCloseupGeometry.FeetPerNm`), para que las dos trazas caigan en la misma columna
        /// del gráfico y no haya un segundo factor de conversión escondido.
        /// </summary>
        [TestMethod]
        public void LaTrazaDe2s_SeLlevaAPiesConLaConversionDelEncuadre()
        {
            var track = FlareChartLayoutTests.TrackFromFlare();

            var points = CloseupTrackSource.ApproachPoints(track);

            Assert.AreEqual(track.Count, points.Count);
            for (int i = 0; i < track.Count; i++)
                Assert.AreEqual(track[i].DistNm * TouchdownCloseupGeometry.FeetPerNm, points[i].XFt, 1e-6);

            // Con los tres puntos reales del vuelo 41: +558,3 / +20,7 / −499,3 ft al umbral.
            Assert.AreEqual(558.3, points[0].XFt, 0.1);
            Assert.IsTrue(points[points.Count - 1].XFt < 0.0, "y el signo es el mismo del closeup");

            Assert.AreEqual(0, CloseupTrackSource.ApproachPoints(null).Count);
        }

        // ── El rótulo: los dos idiomas, simétricos y sin confundir resoluciones ──

        /// <summary>
        /// **Las claves del rótulo existen en los dos idiomas, dicen lo mismo y no se confunden.**
        ///
        /// Se leen los dos `.json` del repositorio (como `RaasReplayTests` con `es.json`, porque el
        /// proyecto de tests no despliega `Languages\`): las dos claves tienen que estar en los dos,
        /// con texto, y el texto de cada una tiene que **nombrar su resolución** —`10 Hz` para el
        /// flare y `2 s` para la de aproximación— en ambos idiomas. Sin eso, el gráfico podría estar
        /// vendiendo 0,1 s como si fueran 2 s (o al revés) en cualquiera de los dos idiomas.
        /// </summary>
        [TestMethod]
        public void LasDosClavesDelRotulo_EstanEnLosDosIdiomasYDistinguenLaResolucion()
        {
            var es = LoadLanguage("es.json");
            var en = LoadLanguage("en.json");
            if (es.Count == 0 || en.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            // Simetría: los dos idiomas tienen el mismo juego de claves.
            CollectionAssert.AreEquivalent(es.Keys.ToList(), en.Keys.ToList(),
                "es.json y en.json tienen que seguir siendo simétricos");

            foreach (var file in new[] { es, en })
            {
                Assert.IsTrue(file.ContainsKey(CloseupTrackSource.FlareLabelKey),
                    $"falta «{CloseupTrackSource.FlareLabelKey}»");
                Assert.IsTrue(file.ContainsKey(CloseupTrackSource.ApproachLabelKey),
                    $"falta «{CloseupTrackSource.ApproachLabelKey}»");

                string flare    = file[CloseupTrackSource.FlareLabelKey];
                string approach = file[CloseupTrackSource.ApproachLabelKey];

                Assert.IsFalse(string.IsNullOrWhiteSpace(flare));
                Assert.IsFalse(string.IsNullOrWhiteSpace(approach));
                Assert.AreNotEqual(flare, approach, "el rótulo tiene que decir cuál de las dos se pinta");
                StringAssert.Contains(flare, "10 Hz",
                    "el rótulo de la traza fina tiene que decir que es de 10 Hz");
                StringAssert.Contains(approach, "2 s",
                    "el rótulo de la traza de aproximación tiene que decir que es de 2 s");
            }
        }

        /// <summary>Los archivos de idioma, con la misma búsqueda de rutas que usa `RaasReplayTests`.</summary>
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

                    // Se lee con regex y no con Newtonsoft para no añadir esa dependencia al proyecto
                    // de tests: el archivo de idioma es plano, clave -> texto.
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
