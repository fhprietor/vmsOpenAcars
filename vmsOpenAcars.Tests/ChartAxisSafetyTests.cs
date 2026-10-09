using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El blindaje del pintado de los gráficos.**
    ///
    /// La causa raíz está en el comentario de <see cref="ChartAxisSafety"/>: el motor de gráficos
    /// estima el rango y el paso de cada eje al pintar, y con un área **sin un solo punto** se queda
    /// sin escala de la que sacar el intervalo automático y lanza
    /// `InvalidOperationException: Axis Object - Auto interval does not have proper value.` El camino
    /// de la pila es `OnPrint → WmPrintClient`, que es el dibujado **a un mapa de bits**
    /// (`Control.DrawToBitmap`), no el pintado en pantalla.
    ///
    /// Aquí se prueba la decisión (puro) y los dos formularios con las trazas reales del vuelo 41:
    /// **sin traza de flare** —el caso de todos los vuelos guardados antes de `flare_track`— y **sin
    /// traza de aproximación**, que deja los cuatro gráficos del análisis vacíos.
    /// </summary>
    [TestClass]
    public class ChartAxisSafetyTests
    {
        // ── La decisión, pura ─────────────────────────────────────────────────────

        [TestMethod]
        public void UnRangoConRecorridoEsUsable_YUnoDegeneradoNo()
        {
            Assert.IsTrue(ChartAxisSafety.IsUsable(0.0, 100.0));
            Assert.IsTrue(ChartAxisSafety.IsUsable(-50.0, 0.0));

            // `mínimo == máximo` es el estado sin intervalo posible: la serie es plana y el eje se
            // fijó a mano sobre ese único valor.
            Assert.IsFalse(ChartAxisSafety.IsUsable(82.0, 82.0));
            Assert.IsFalse(ChartAxisSafety.IsUsable(10.0, 0.0), "un rango invertido no tiene recorrido");
            // Y `NaN` es «automático», que es justo lo que hay que resolver cuando no hay datos.
            Assert.IsFalse(ChartAxisSafety.IsUsable(double.NaN, 100.0));
            Assert.IsFalse(ChartAxisSafety.IsUsable(0.0, double.NaN));
            Assert.IsFalse(ChartAxisSafety.IsUsable(double.NegativeInfinity, 100.0));
        }

        [TestMethod]
        public void SinValores_ElMarcoEsElDeRespaldo()
        {
            var r = ChartAxisSafety.RangeFrom(new List<double>(), 0.0, 1.0);
            Assert.AreEqual(0.0, r.Minimum, 1e-9);
            Assert.AreEqual(1.0, r.Maximum, 1e-9);
        }

        [TestMethod]
        public void ConValores_ElMarcoEsElRecorridoConUnCincoPorCientoDeAire()
        {
            var r = ChartAxisSafety.RangeFrom(new List<double> { 100.0, 150.0, 120.0 }, 0.0, 1.0);
            // 100 … 150 con 5 % de 50 = 2,5 por lado.
            Assert.AreEqual(97.5, r.Minimum, 1e-9);
            Assert.AreEqual(152.5, r.Maximum, 1e-9);
        }

        [TestMethod]
        public void ConUnValorConstante_ElMarcoSeAbreMedioPuntoPorLado()
        {
            // Es el caso que deja el rango sin recorrido: una traza plana (los flaps quietos en el 82 %).
            var r = ChartAxisSafety.RangeFrom(new List<double> { 82.0, 82.0, 82.0 }, 0.0, 1.0);
            Assert.AreEqual(81.5, r.Minimum, 1e-9);
            Assert.AreEqual(82.5, r.Maximum, 1e-9);
            Assert.IsTrue(ChartAxisSafety.IsUsable(r.Minimum, r.Maximum));
        }

        [TestMethod]
        public void LosValoresNoFinitos_NoEntranEnElMarco()
        {
            var r = ChartAxisSafety.RangeFrom(new List<double> { double.NaN, 10.0, double.PositiveInfinity }, 0.0, 1.0);
            Assert.AreEqual(9.5, r.Minimum, 1e-9);
            Assert.AreEqual(10.5, r.Maximum, 1e-9);
        }

        // ── El repaso sobre un gráfico ────────────────────────────────────────────

        private static Chart AreaWithSeries(string areaName, bool pinY, params double[] ys)
        {
            var chart = new Chart { Size = new Size(400, 300) };
            var area = new ChartArea(areaName);
            if (pinY) { area.AxisY.Minimum = 0.0; area.AxisY.Maximum = 100.0; }
            chart.ChartAreas.Add(area);

            if (ys != null && ys.Length > 0)
            {
                var s = new Series("s") { ChartType = SeriesChartType.Line, ChartArea = areaName };
                for (int i = 0; i < ys.Length; i++) s.Points.AddXY(i * 10.0, ys[i]);
                chart.Series.Add(s);
            }
            return chart;
        }

        /// <summary>Las anotaciones que marcan un área como sin datos (van en el gráfico, sin recortar).</summary>
        private static List<Annotation> AnnotationsFor(Chart chart, string areaName)
            => chart.Annotations.Cast<Annotation>()
                    .Where(a => a.Name == ChartAxisSafety.NoDataAnnotationName(areaName)).ToList();

        [TestMethod]
        public void UnAreaSinPuntos_QuedaFijadaYMarcada()
        {
            using (var chart = AreaWithSeries("ptc", pinY: false))
            {
                int marked = ChartAxisSafety.EnsureExplicit(chart);

                Assert.AreEqual(1, marked);
                Assert.AreEqual(ChartAxisSafety.NoDataMinimum, chart.ChartAreas["ptc"].AxisY.Minimum, 1e-9);
                Assert.AreEqual(ChartAxisSafety.NoDataMaximum, chart.ChartAreas["ptc"].AxisY.Maximum, 1e-9);
                Assert.IsTrue(ChartAxisSafety.IsUsable(chart.ChartAreas["ptc"].AxisY.Minimum,
                                                       chart.ChartAreas["ptc"].AxisY.Maximum));

                var marcas = AnnotationsFor(chart, "ptc");
                Assert.AreEqual(1, marcas.Count);
                var marcar = marcas[0] as TextAnnotation;
                Assert.IsNotNull(marcar, "la marca de «sin datos» es un texto");
                Assert.AreEqual(L._(ChartAxisSafety.NoDataKey), marcar.Text);
            }
        }

        [TestMethod]
        public void UnAreaConDatos_YElEjeEnAutomatico_NoSeToca()
        {
            using (var chart = AreaWithSeries("alt", pinY: false, 10.0, 50.0, 20.0))
            {
                int marked = ChartAxisSafety.EnsureExplicit(chart);

                Assert.AreEqual(0, marked, "con datos no se marca nada");
                // La Y se queda automática: la decide el motor, que aquí sí tiene de dónde. Fijarla
                // cambiaría la Y autoescalada del closeup y las escalas de LATERAL y VS, que ya están
                // decididas y probadas.
                Assert.IsTrue(double.IsNaN(chart.ChartAreas["alt"].AxisY.Maximum));
                Assert.AreEqual(0, AnnotationsFor(chart, "alt").Count);
            }
        }

        [TestMethod]
        public void UnEjeExplicitoDegenerado_SeAbreConLosDatos()
        {
            using (var chart = AreaWithSeries("alt", pinY: true, 10.0, 50.0, 20.0))
            {
                // El caso que no tiene intervalo: los dos extremos en el mismo valor.
                chart.ChartAreas["alt"].AxisY.Minimum = 30.0;
                chart.ChartAreas["alt"].AxisY.Maximum = 30.0;

                ChartAxisSafety.EnsureExplicit(chart);

                Assert.IsTrue(ChartAxisSafety.IsUsable(chart.ChartAreas["alt"].AxisY.Minimum,
                                                       chart.ChartAreas["alt"].AxisY.Maximum),
                              "un rango sin recorrido no puede quedarse así");
                // El recorrido de los datos es 10 … 50, con un 5 % de 40 = 2 de aire por lado.
                Assert.AreEqual(8.0, chart.ChartAreas["alt"].AxisY.Minimum, 1e-9);
                Assert.AreEqual(52.0, chart.ChartAreas["alt"].AxisY.Maximum, 1e-9);
            }
        }

        [TestMethod]
        public void LosPuntosNoFinitos_SeQuitanAntesDeContar()
        {
            using (var chart = AreaWithSeries("alt", pinY: false, 10.0, 50.0))
            {
                var s = chart.Series["s"];
                // Es lo que arrastraba el closeup: una marca anclada en el mínimo del eje, que en un
                // área sin datos es `NaN`. Se escriben a mano porque `AddXY` con un no finito no
                // garantiza que el valor llegue tal cual al punto.
                s.Points.AddXY(500.0, 12.0);
                s.Points[2].YValues[0] = double.NaN;
                s.Points.AddXY(600.0, 13.0);
                s.Points[3].XValue = double.NaN;

                Assert.AreEqual(4, s.Points.Count, "los cuatro puntos están, dos de ellos envenenados");

                ChartAxisSafety.EnsureExplicit(chart);

                Assert.AreEqual(2, s.Points.Count, "los puntos no finitos no son datos");
            }
        }

        // ── Los formularios: sin traza de flare y sin traza de aproximación ───────

        private static FlightRecord Skcg41() => new FlightRecord
        {
            Id              = 41,
            FlightNumber    = "9821",
            Origin          = "SKBG",
            Destination     = "SKCG",
            RunwayName      = "01",
            FlightDate      = new DateTime(2026, 10, 7, 3, 9, 22, DateTimeKind.Utc),
            LandingRateFpm  = -223,
            GForce          = 1.48,
            TouchdownDistFt = 1736.398,
            CenterlineDevFt = 5.27,
            Score           = 82,
            RunwayLengthFt  = 7841.0,
        };

        /// <summary>
        /// **Un vuelo sin traza de flare se abre y no lanza nada.** Es el caso real de todos los vuelos
        /// guardados antes de que existiera `flare_track`: la ventana del flare no pinta gráfico y lo
        /// dice, en vez de quedarse con un área sin datos.
        /// </summary>
        [TestMethod]
        public void UnVueloSinTrazaDeFlare_SeAbreSinLanzar()
        {
            using (var viejo = new FlareAnalysisForm(Skcg41(), new List<FlareTrackPoint>(), false))
            {
                viejo.Size = new Size(1000, 760);
                viejo.CreateControl();
                viejo.PerformLayout();

                Assert.IsNull(viejo.FlareChart, "sin traza fina no hay gráfico que pintar");
            }

            using (var armada = new FlareAnalysisForm(Skcg41(), new List<FlareTrackPoint>(), true))
            {
                armada.Size = new Size(1000, 760);
                armada.CreateControl();
                armada.PerformLayout();

                Assert.IsNull(armada.FlareChart, "una captura armada y sin muestras tampoco pinta gráfico");
            }
        }

        /// <summary>
        /// **El gráfico del flare, con una traza real, se vuelca a PNG sin lanzar y con las áreas sin
        /// datos marcadas.** La traza del vuelo 41 no trae pitch: esa área se queda sin serie y es
        /// exactamente la que dejaba el eje en automático sobre un área vacía.
        /// </summary>
        [TestMethod]
        public void ElGraficoDelFlare_SeVuelcaSinLanzarYSusAreasSinDatosSeMarcan()
        {
            string path = Path.Combine(Path.GetTempPath(), "vmsopenacars_chart_axis_safety.png");

            using (var form = new FlareAnalysisForm(Skcg41(), FlareChartLayoutTests.Skcg41Flare(), true))
            {
                form.Size = new Size(1000, 760);
                form.CreateControl();
                form.PerformLayout();
                form.FlareChart.Width  = 960;
                form.FlareChart.Height = 660;

                Assert.IsNull(form.FlareChart.Series.FirstOrDefault(s => s.Name == "Pitch (°)"),
                    "la traza del vuelo 41 no trae pitch: no se inventa la serie");
                Assert.AreEqual(1, AnnotationsFor(form.FlareChart, "ptc").Count,
                    "y el área de pitch queda marcada como sin datos");

                try
                {
                    form.FlareChart.SaveImage(path, ChartImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"el volcado del gráfico del flare falló: {ex.GetType().Name}: {ex.Message}");
                }

                Assert.IsTrue(new FileInfo(path).Length > 5000,
                    $"el PNG del gráfico del flare no se escribió ({new FileInfo(path).Length} B)");
            }
        }

        /// <summary>
        /// **Un vuelo sin traza de aproximación abre el análisis y sus cuatro gráficos se vuelcan sin
        /// lanzar.** Es el camino de salida de `PopulateCharts` (`maxDist <= 0`): no hay una sola
        /// distancia, así que no hay series, y sin el blindaje los cuatro ejes se quedan en automático
        /// sobre un área vacía.
        /// </summary>
        [TestMethod]
        public void UnVueloSinTrazaDeAproximacion_SusCuatroGraficosSeVuelcanSinLanzar()
        {
            // El vuelo 38 de la base local: sin traza de aproximación guardada.
            var sinTraza = Skcg41();
            sinTraza.Id = 38;

            using (var form = new LandingAnalysisForm(
                new List<(FlightRecord, List<ApproachTrackPoint>)>
                {
                    (sinTraza, new List<ApproachTrackPoint>())
                }))
            {
                form.Size = new Size(1000, 740);
                form.CreateControl();
                form.PerformLayout();

                var charts = form.ChartGrid.Controls.OfType<Chart>().ToList();
                Assert.AreEqual(4, charts.Count, "siguen siendo los cuatro gráficos");

                foreach (var chart in charts)
                {
                    string path = Path.Combine(Path.GetTempPath(),
                        $"vmsopenacars_landing_sintraza_{(string)chart.Tag}.png");
                    chart.Width = 490; chart.Height = 330;

                    try
                    {
                        chart.SaveImage(path, ChartImageFormat.Png);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"el volcado de {(string)chart.Tag} falló: {ex.GetType().Name}: {ex.Message}");
                    }

                    Assert.IsTrue(new FileInfo(path).Length > 500,
                        $"el PNG de {(string)chart.Tag} no se escribió ({new FileInfo(path).Length} B)");
                    Assert.AreEqual(1, AnnotationsFor(chart, "main").Count,
                        $"{(string)chart.Tag} no tiene datos: tiene que decirlo");
                }
            }
        }
    }
}
