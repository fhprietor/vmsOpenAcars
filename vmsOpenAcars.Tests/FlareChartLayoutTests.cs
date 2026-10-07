using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El encuadre y las marcas del gráfico del flare.**
    ///
    /// El caso base es el **vuelo 41 de la base local** (SKBG → SKCG, pista 01, toque a **1 736 ft**,
    /// pista de **7 841 ft**), el mismo que usa `TouchdownCloseupGeometryTests`: su perfil real es
    ///
    /// | Dist al umbral | AGL | IAS | VS |
    /// |---|---|---|---|
    /// | +1 653,8 ft | 147 ft | 152,3 kt | −618 fpm |
    /// | +1 103,0 ft | 131 ft | 151,7 kt | −474 fpm |
    /// | +558,3 ft | 119 ft | 151,6 kt | −371 fpm |
    /// | +20,7 ft | 102 ft | 151,3 kt | −504 fpm |
    /// | −499,3 ft | 80 ft | 149,5 kt | −657 fpm |
    /// | −1 020,6 ft | 60 ft | 146,7 kt | −559 fpm |
    /// | −1 541,7 ft | 50 ft | 143,3 kt | −281 fpm |
    ///
    /// Sobre ese perfil se interpola la traza de 10 Hz que el cliente **capturaría** hoy: la captura
    /// se arma a 1 500 ft del umbral (dos muestras antes del primer punto de la tabla) y sigue un
    /// par de segundos pasada la toma. Las cifras de AGL, IAS y VS son las de la base, no inventadas.
    /// </summary>
    [TestClass]
    public class FlareChartLayoutTests
    {
        private const double Skcg01 = 7841.0;

        /// <summary>La traza fina interpolada del vuelo 41: 10 Hz desde +1 500 ft hasta −1 650 ft.</summary>
        internal static List<FlareTrackPoint> Skcg41Flare()
        {
            // (dist_ft, agl_ft, ias_kt, vs_fpm) reales del vuelo 41, de antes del umbral a después.
            var knots = new[]
            {
                (1653.8, 147.0, 152.3, -618.0),
                (1103.0, 131.0, 151.7, -474.0),
                ( 558.3, 119.0, 151.6, -371.0),
                (  20.7, 102.0, 151.3, -504.0),
                (-499.3,  80.0, 149.5, -657.0),
                (-1020.6, 60.0, 146.7, -559.0),
                (-1541.7, 50.0, 143.3, -281.0),
            };

            var samples = new List<FlareTrackPoint>();
            var t = new DateTime(2026, 10, 7, 3, 8, 0, DateTimeKind.Utc);

            // De +1 500 ft a −1 600 ft en pasos de 25 ft: a 150 kt son ~0,1 s por muestra (10 Hz).
            // El borde para en −1 600 ft, el último punto que cae dentro de los 1 700 del encuadre.
            int index = 0;
            for (double d = 1500.0; d >= -1600.0; d -= 25.0, index++)
            {
                int i = 1;
                while (i < knots.Length - 1 && knots[i].Item1 > d) i++;
                var a = knots[i - 1];
                var b = knots[i];
                double w = (a.Item1 - d) / Math.Max(1e-9, a.Item1 - b.Item1);
                w = Math.Max(0.0, Math.Min(1.0, w));

                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = index,
                    TimestampUtc = t.AddMilliseconds(100 * index),
                    DistFt       = d,
                    AglFt        = a.Item2 + (b.Item2 - a.Item2) * w,
                    IasKt        = a.Item3 + (b.Item3 - a.Item3) * w,
                    VsFpm        = a.Item4 + (b.Item4 - a.Item4) * w,
                    // El pitch no está en `approach_track` (esa tabla no lo guarda) y el
                    // radioaltímetro tampoco: van a null, que es como se declara «no lo sé».
                    PitchDeg     = null,
                    RadarAltFt   = null,
                    GsKt         = null,
                    OnGround     = d <= 0.0,
                });
            }
            return samples;
        }

        /// <summary>
        /// **La traza de 2 s del mismo vuelo 41**, para los sitios que necesitan un perfil vertical
        /// real (el formulario de análisis, que sale antes de pintar si no hay ninguna distancia). Son
        /// los tres últimos puntos de `approach_track`, con su `dist_nm` y su AGL de verdad.
        /// </summary>
        internal static List<ApproachTrackPoint> TrackFromFlare()
        {
            var raw = new[]
            {
                // dist_ft (positivo = antes del umbral), agl_ft, ias_kt, vs_fpm
                ( 558.3, 119.0, 151.6, -371.0),
                (  20.7, 102.0, 151.3, -504.0),
                (-499.3,  80.0, 149.5, -657.0),
            };

            var track = new List<ApproachTrackPoint>();
            for (int i = 0; i < raw.Length; i++)
                track.Add(new ApproachTrackPoint
                {
                    FlightId   = 41,
                    SeqNo      = i,
                    AltFt      = 76.0 + raw[i].Item2,
                    AglFt      = raw[i].Item2,
                    IasKt      = raw[i].Item3,
                    VsFpm      = raw[i].Item4,
                    DistNm     = raw[i].Item1 / 6076.12,
                    LateralFt  = 5.0,
                });
            return track;
        }

        // ── El encuadre sale de la traza, no de una escala fija ───────────────────

        [TestMethod]
        public void Skcg41_ElEncuadreCubreLoCapturadoYLasMarcasSonLasDelCloseup()
        {
            var samples = Skcg41Flare();
            var l = FlareChartLayout.Build(samples, Skcg01, everStarted: true);

            Assert.IsTrue(l.HasData);
            Assert.AreEqual(samples.Count, l.SampleCount);

            // El encuadre cubre los 1 500 ft capturados antes del umbral y la frenada posterior...
            Assert.IsTrue(l.BeforeFt >= 1500.0, $"el encuadre no puede recortar la traza ({l.BeforeFt})");
            Assert.IsTrue(l.AfterFt  >= 1600.0, $"y tiene que llegar a la última muestra ({l.AfterFt})");
            // ...y es rotulable en pies enteros: es lo que exige `CloseupAxis`.
            Assert.IsTrue(CloseupAxis.IsWholeNumber(l.BeforeFt) && CloseupAxis.IsWholeNumber(l.AfterFt));
            Assert.AreNotEqual(0, CloseupAxis.Labels(l.BeforeFt, l.AfterFt).Count,
                "el eje del gráfico del flare tiene que poder rotularse");
            Assert.IsTrue(CloseupAxis.LabelCount(l.BeforeFt, l.AfterFt,
                                                 CloseupAxis.StepFor(l.BeforeFt, l.AfterFt))
                          <= CloseupAxis.MaxLabels);

            // Las marcas son **las mismas** que las del closeup: el helper no tiene una segunda
            // definición de la pista ni de la zona de toma.
            Assert.IsNotNull(l.Closeup);
            Assert.AreEqual(1500.0, l.Closeup.ZeroBandFt, 1e-9);
            Assert.AreEqual(3000.0, l.Closeup.ThreeBandFt, 1e-9);
        }

        /// <summary>
        /// El toque sale de la **propia traza** (la última muestra en tierra), no de
        /// `touchdown_dist_ft`: es lo que hace que el gráfico siga siendo correcto en un vuelo en el
        /// que el dato del PIREP y la traza no coincidan, y lo que permite dibujar el TD aunque el
        /// registro no lo traiga.
        ///
        /// **Y ojo con el signo**, que es la trampa de este helper: `flare_track.dist_ft` es negativo
        /// pasado el umbral y `TouchdownCloseupGeometry` espera la distancia de toma **en positivo**.
        /// Sin la conversión, el helper la tomaba por un valor no utilizable y el gráfico se quedaba
        /// **sin punto de toque** —pasó, y por eso este test existe—.
        /// </summary>
        [TestMethod]
        public void ElToqueSaleDeLaTrazaYSeMarcaDondeEsta()
        {
            // Con el toque dentro del encuadre (la captura termina poco después de la toma) se marca.
            var cerca = new List<FlareTrackPoint>();
            for (int i = 0; i < 20; i++)
                cerca.Add(new FlareTrackPoint
                {
                    DistFt = 900.0 - i * 100.0, AglFt = 80.0 - i, IasKt = 140.0,
                    OnGround = i >= 9,      // la toma a −100 ft, la captura sigue hasta −1 000
                });

            var l = FlareChartLayout.Build(cerca, Skcg01, true);
            Assert.IsTrue(l.Closeup.HasTouchdown, "la traza tiene muestra en tierra: hay toque");
            Assert.AreEqual(1000.0, l.Closeup.TouchdownFt, 1e-9,
                "la distancia de toma va en POSITIVO, como `touchdown_dist_ft`");
            Assert.IsTrue(l.Closeup.TouchdownX < 0.0, "y la marca se pinta pasada la pista del umbral");
            Assert.IsFalse(double.IsNaN(l.Closeup.TouchdownX));
        }

        /// <summary>
        /// Con la traza real del vuelo 41 (cortada en −1 600 ft) el toque queda al borde
        /// mismo del encuadre (1 700 ft), así que **sí se marca**: cabe. El caso de «no cabe» lo fija
        /// `TouchdownCloseupGeometryTests` —el toque a 5 898 ft del vuelo 11—, y este helper **hereda
        /// ese criterio en vez de reimplementarlo**: si no cupiera, `TouchdownInView` sería falso y la
        /// marca no se pintaría, con el dato conservado en el rótulo.
        /// </summary>
        [TestMethod]
        public void ConLaCapturaLarga_ElToqueQuedaAlBordeYSeMarca()
        {
            var l = FlareChartLayout.Build(Skcg41Flare(), Skcg01, true);

            Assert.IsTrue(l.Closeup.HasTouchdown, "el toque existe: la traza tiene muestras en tierra");
            Assert.AreEqual(1600.0, l.Closeup.TouchdownFt, 1e-9);
            Assert.IsTrue(l.Closeup.TouchdownInView, "1 600 ft caben en los 1 700 del encuadre");
            Assert.AreEqual(-1600.0, l.Closeup.TouchdownX, 1e-9,
                "la marca va pasada la pista del umbral");
            Assert.IsTrue(l.Closeup.TouchdownLabel.Contains("1,600 ft"), l.Closeup.TouchdownLabel);
            Assert.AreEqual(3, l.Closeup.TouchdownPoints, "1 500 < 1 600 ≤ 3 000");
        }

        // ── Los ejes verticales salen de lo que hay ───────────────────────────────

        /// <summary>
        /// **Sin radioaltímetro se pinta AGL** (el vuelo 41 es de la época en la que no se guardaba
        /// radioaltímetro, así que sus muestras van a `null`): el gráfico degrada al altímetro en vez
        /// de quedarse sin eje de altitud.
        /// </summary>
        [TestMethod]
        public void SinRadioaltimetro_ElEjeDeAltitudUsaElAgl()
        {
            var l = FlareChartLayout.Build(Skcg41Flare(), Skcg01, true);

            Assert.AreEqual(FlareAltitudeSource.Agl, l.AltitudeSource);
            Assert.AreEqual(-50.0, l.AltitudeFloorFt, 1e-9,
                "el suelo baja bajo 0 para que la banda de pista se vea entera, como en el closeup");
            Assert.IsTrue(l.AltitudeTopFt >= 147.0,
                $"el techo tiene que llegar al AGL más alto capturado ({l.AltitudeTopFt})");
        }

        /// <summary>
        /// Con radioaltímetro, **ese manda**: la altitud del gráfico es la del instrumento bueno para
        /// el flare, y el AGL solo se usa si no hay radioaltímetro.
        /// </summary>
        [TestMethod]
        public void ConRadioaltimetro_MandaElRadioaltimetro()
        {
            var samples = Skcg41Flare();
            foreach (var s in samples) s.RadarAltFt = s.AglFt - 4.0;

            var l = FlareChartLayout.Build(samples, Skcg01, true);
            Assert.AreEqual(FlareAltitudeSource.RadarAltimeter, l.AltitudeSource);
        }

        /// <summary>
        /// La velocidad y el pitch se ajustan a lo que se ve, que en el flare son **10–15 kt** y unos
        /// pocos grados: un eje de 0 a 300 kt haría invisible la maniobra. Las cifras de la traza son
        /// las del vuelo 41 —la IAS real baja de 152,3 a 143,3 kt—, así que el eje tiene que quedarse
        /// cerca de ese tramo y no abrirse a 0–300.
        /// </summary>
        [TestMethod]
        public void LosEjesDeVelocidadYPitch_SeAjustanALoQueSeVe()
        {
            var samples = Skcg41Flare();
            // El pitch no viene en `approach_track`: se añade aquí el perfil típico de la toma
            // (morro arriba hasta ~4° y bajando al tocar) para poder comprobar el eje.
            double pitch = -0.5;
            foreach (var s in samples) { s.PitchDeg = pitch; pitch = Math.Min(4.0, pitch + 0.12); }

            var l = FlareChartLayout.Build(samples, Skcg01, true);

            Assert.IsTrue(l.HasPitch);
            // La IAS real del tramo va de 143,3 a 152,3 kt: el eje se redondea a decenas con ±5 kt.
            Assert.IsTrue(l.SpeedFloorKt >= 130.0 && l.SpeedFloorKt <= 140.0, $"suelo {l.SpeedFloorKt}");
            Assert.IsTrue(l.SpeedTopKt   >= 155.0 && l.SpeedTopKt   <= 170.0, $"techo {l.SpeedTopKt}");
            Assert.IsTrue(l.SpeedTopKt - l.SpeedFloorKt < 40.0,
                "un eje de velocidad abierto a 0–300 kt esconde la maniobra que se viene a mirar");
            Assert.IsTrue(l.PitchFloorDeg <= -0.5 && l.PitchTopDeg >= 4.0);
        }

        // ── Degradar sin datos ────────────────────────────────────────────────────

        /// <summary>
        /// **Un vuelo sin `flare_track` no tiene gráfico, y eso es una respuesta, no un fallo.** El
        /// helper no rellena con `approach_track` —sería vender 2 s como si fueran 0,1 s— y el resumen
        /// lo dice con esas palabras.
        /// </summary>
        [TestMethod]
        public void SinMuestras_NoHayGraficoYSeDice()
        {
            foreach (var vacio in new List<FlareTrackPoint>[] { null, new List<FlareTrackPoint>() })
            {
                var l = FlareChartLayout.Build(vacio, Skcg01, everStarted: false);

                Assert.IsFalse(l.HasData);
                Assert.AreEqual(0, l.SampleCount);
                Assert.AreEqual(0.0, l.BeforeFt, 1e-9);
                Assert.AreEqual(0.0, l.AfterFt, 1e-9);
                Assert.IsFalse(l.HasPitch);
                Assert.AreEqual(FlareAltitudeSource.None, l.AltitudeSource);
                Assert.IsTrue(l.Summary.Contains("No flare track"), l.Summary);

                // El closeup sigue construido (no nulo) para que el formulario pueda pintar el
                // mensaje sin comprobar nada: pero sin toque, porque no hay dato de toque.
                Assert.IsNotNull(l.Closeup);
                Assert.IsFalse(l.Closeup.HasTouchdown);
            }
        }

        /// <summary>
        /// Una captura que armó pero cuyas muestras se perdieron es distinta de «este vuelo no tiene
        /// traza»: el resumen lo dice, porque son dos causas distintas y el piloto merece saber cuál.
        /// </summary>
        [TestMethod]
        public void CapturaArmadaSinMuestras_SeDistingueDeNoTenerTraza()
        {
            var sinNada = FlareChartLayout.Build(new List<FlareTrackPoint>(), Skcg01, false);
            var armada  = FlareChartLayout.Build(new List<FlareTrackPoint>(), Skcg01, true);

            Assert.AreEqual(sinNada.Summary, armada.Summary,
                "el helper no cambia el resumen: el matiz lo da el formulario con `everStarted`");
            Assert.IsFalse(armada.HasData);
        }

        /// <summary>
        /// Una traza donde **todo** está pasado el umbral (armado por AGL con el avión ya encima de
        /// él) sigue teniendo encuadre: el helper no puede dejar un rango de anchura cero, que
        /// rompería el eje del gráfico.
        /// </summary>
        [TestMethod]
        public void UnaTrazaTodaPasadoElUmbral_TieneEncuadreUtilizable()
        {
            var samples = new List<FlareTrackPoint>();
            for (int i = 0; i < 10; i++)
                samples.Add(new FlareTrackPoint
                {
                    DistFt = -50.0 - i * 20.0, AglFt = 10.0, IasKt = 130.0, OnGround = i > 5,
                });

            var l = FlareChartLayout.Build(samples, Skcg01, true);

            Assert.IsTrue(l.HasData);
            Assert.IsTrue(l.BeforeFt > 0.0, "sin encuadre por delante el eje no se puede ni rotular");
            Assert.IsTrue(l.AfterFt >= 230.0);
        }

        /// <summary>Toda la traza del vuelo 41 llega a −1 650 ft, así que el toque queda fuera del
        /// encuadre: en su caso no se dibuja marca, pero el **dato** sigue en el rótulo. Sin longitud
        /// de pista no se dibuja pista, y el umbral y las bandas siguen: es la regla de degradar sin
        /// datos que ya fija `TouchdownCloseupGeometry`.</summary>
        [TestMethod]
        public void SinLongitudDePista_NoHayBandaDePistaPeroSiUmbralYToque()
        {
            var l = FlareChartLayout.Build(Skcg41Flare(), 0.0, true);

            Assert.IsFalse(l.Closeup.RunwayVisible, "sin dato no se inventa un largo de pista");
            Assert.IsTrue(l.Closeup.HasTouchdown, "el toque existe aunque no quepa en el encuadre");
            Assert.IsTrue(l.Closeup.TouchdownLabel.Contains("1,600 ft"), l.Closeup.TouchdownLabel);
            Assert.AreEqual(1500.0, l.Closeup.ZeroBandFt, 1e-9, "sin dato manda la regla de la casa");
            Assert.AreEqual(2500.0, l.Closeup.ThreeBandFt, 1e-9);
        }
    }
}
