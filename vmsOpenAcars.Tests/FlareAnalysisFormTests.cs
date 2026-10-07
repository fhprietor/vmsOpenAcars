using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
    /// **El gráfico del flare, en el formulario de verdad y renderizado a PNG.**
    ///
    /// Hace falta porque hay cosas que no se ven compilando: si un rótulo tapa el punto de toque, si
    /// la banda de pista se sale del encuadre o si un eje se queda sin marcas. La única forma de
    /// mirarlo sin un piloto aterrizando delante es **renderizar el control a un mapa de bits fuera
    /// de pantalla** y volcarlo a `%TEMP%` — es lo que destapó el solape del rótulo «THR» con las
    /// etiquetas del eje X en el closeup.
    ///
    /// El caso es el **vuelo 41 de la base local** (SKBG → SKCG, pista 01, toque a 1 736 ft, pista de
    /// 7 841 ft), el mismo perfil real que usan `TouchdownCloseupGeometryTests` y
    /// `FlareChartLayoutTests`.
    /// </summary>
    [TestClass]
    public class FlareAnalysisFormTests
    {
        /// <summary>El PNG que se deja en `%TEMP%` para poder mirar el gráfico sin el simulador.</summary>
        private static string PngPath =>
            Path.Combine(Path.GetTempPath(), "vmsopenacars_flare_chart.png");

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

        // ── El reparto: el aviso no puede tapar el gráfico ───────────────────────

        /// <summary>
        /// El aviso de «no hay traza fina» vive en su **propia franja** (`Dock.Fill`) y el resumen en
        /// la suya (`Dock.Bottom`): en un `Panel` dos controles con el mismo `Dock` sí se apilan, pero
        /// uno en `Fill` y otro en `Bottom` no comparten celda. Es la trampa que la guía documenta
        /// para `TableLayoutPanel` y que aquí se mide, no se supone.
        /// </summary>
        [TestMethod]
        public void SinTrazaFina_ElAvisoNoSeSuperponeAlResumen()
        {
            using (var form = new FlareAnalysisForm(Skcg41(), new List<FlareTrackPoint>(), false))
            {
                form.PerformLayout();

                Assert.IsNull(form.FlareChart,
                    "sin muestras no hay gráfico que pintar: se enseña el aviso");
                Assert.IsNotNull(form.SummaryLabel);
                Assert.IsTrue(form.SummaryLabel.Text.Contains("No flare track"),
                    $"el resumen tiene que decir que no hay traza ({form.SummaryLabel.Text})");

                var contenido = form.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Dock == DockStyle.Fill);
                Assert.IsNotNull(contenido, "tiene que haber una franja de contenido");
                Assert.IsTrue(contenido.Bottom <= form.SummaryLabel.Top,
                    $"el aviso no puede invadir el resumen (contenido {contenido.Bounds}, resumen {form.SummaryLabel.Bounds})");
            }
        }

        /// <summary>
        /// Un vuelo **anterior a `flare_track`** se distingue de una captura que se armó y no dejó
        /// muestras: el mensaje cambia. Y en los dos casos se dice explícitamente que **no** se pinta
        /// la traza de 2 s, para que nadie crea que el gráfico está vacío por un fallo.
        /// </summary>
        [TestMethod]
        public void LosDosMotivosDeNoTenerDatos_SeDistinguen()
        {
            using (var viejo = new FlareAnalysisForm(Skcg41(), new List<FlareTrackPoint>(), false))
            {
                viejo.PerformLayout();
                Assert.IsTrue(viejo.SummaryLabel.Text.Contains("No flare track"));
                StringAssert.Contains(ControlText(viejo), "approach track");
            }

            using (var armada = new FlareAnalysisForm(Skcg41(), new List<FlareTrackPoint>(), true))
            {
                armada.PerformLayout();
                Assert.IsTrue(armada.SummaryLabel.Text.Contains("No flare samples"));
            }
        }

        // ── Con datos: el encuadre, las marcas y el eje rotulado ──────────────────

        [TestMethod]
        public void ConTrazaFina_ElGraficoLlevaElEncuadreDelHelperYLasMarcasDelCloseup()
        {
            using (var form = new FlareAnalysisForm(Skcg41(), FlareChartLayoutTests.Skcg41Flare(), true))
            {
                form.PerformLayout();

                var chart = form.FlareChart;
                Assert.IsNotNull(chart, "con muestras el gráfico tiene que existir");
                Assert.AreEqual(3, chart.ChartAreas.Count,
                    "altitud, velocidad y pitch: tres áreas con el mismo eje X");

                var layout = FlareChartLayout.Build(
                    FlareChartLayoutTests.Skcg41Flare(), 7841.0, true);

                foreach (var area in chart.ChartAreas)
                {
                    Assert.AreEqual(layout.BeforeFt, area.AxisX.Maximum, 1e-9,
                        $"el encuadre lo decide el helper, no el gráfico ({area.Name})");
                    Assert.AreEqual(-layout.AfterFt, area.AxisX.Minimum, 1e-9);
                }

                // La pista sale del closeup —el mismo objeto— y se dibuja en las tres áreas.
                var runway = chart.Series.Where(s => s.Name.StartsWith("RWY 7,841 ft")).ToList();
                Assert.AreEqual(3, runway.Count, "la banda de pista va en las tres áreas");
                Assert.IsTrue(runway.All(s => s.Points.Count == 2));

                // El umbral y el toque, en las tres áreas. La marca del toque cae **dentro** del
                // encuadre (la captura del vuelo 41 llega a −1 650 ft y el encuadre enseña 1 700 ft);
                // si hubiera quedado fuera, `TouchdownCloseup` la prohibiría en vez de llevarla al
                // borde, que es el criterio que ya fija `TouchdownCloseupGeometryTests`.
                Assert.AreEqual(3, chart.Series.Count(s => s.Name.StartsWith("THR_")));
                Assert.AreEqual(3, chart.Series.Count(s => s.Name.StartsWith("TD_")));
                Assert.IsTrue(form.SummaryLabel.Text.Contains("TD 1,600 ft"), form.SummaryLabel.Text);

                // Y las bandas de la zona de toma, que son las que puntúa `TouchdownZonePolicy`.
                foreach (var area in chart.ChartAreas)
                    Assert.AreEqual(2, area.AxisX.StripLines.Count, area.Name);

                // Las series de datos, cada una en su área.
                Assert.IsNotNull(chart.Series.FirstOrDefault(s => s.Name == "AGL (ft)" && s.ChartArea == "alt"));
                Assert.IsNotNull(chart.Series.FirstOrDefault(s => s.Name == "IAS (kt)" && s.ChartArea == "spd"));
                // Sin pitch en la traza (approach_track no lo guarda) no se inventa una serie vacía.
                Assert.IsNull(chart.Series.FirstOrDefault(s => s.Name == "Pitch (°)"));
            }
        }

        /// <summary>
        /// **El eje X queda en pies enteros**: el paso es el «bonito» de `CloseupAxis`, hay rótulos
        /// personalizados y **no queda ninguno automático con decimales**. Es la queja del mantenedor
        /// («el eje X del closeup, la distancia al umbral, tiene decimales») comprobada donde se
        /// decide, no solo en el helper.
        /// </summary>
        [TestMethod]
        public void ElEjeXDelGrafico_QuedaEnPiesEnterosConTHR()
        {
            using (var form = new FlareAnalysisForm(Skcg41(), FlareChartLayoutTests.Skcg41Flare(), true))
            {
                form.PerformLayout();
                var layout = FlareChartLayout.Build(
                    FlareChartLayoutTests.Skcg41Flare(), 7841.0, true);

                foreach (var area in form.FlareChart.ChartAreas)
                {
                    var axis = area.AxisX;
                    var labels = axis.CustomLabels;
                    Assert.IsTrue(labels.Count > 0,
                        $"el eje X de {area.Name} tiene que llevar rótulos en pies enteros");
                    Assert.AreEqual(CloseupAxis.StepFor(layout.BeforeFt, layout.AfterFt), axis.Interval, 1e-9,
                        "el paso lo decide `CloseupAxis`, no el motor de gráficos");

                    var textos = labels.Cast<CustomLabel>().Select(c => c.Text).ToList();
                    CollectionAssert.Contains(textos, CloseupAxis.ThresholdLabel, area.Name);
                    foreach (string t in textos)
                    {
                        if (t == CloseupAxis.ThresholdLabel) continue;
                        string digits = t.Replace(".", "").Replace("-", "");
                        Assert.IsTrue(digits.Length > 0 && digits.All(char.IsDigit),
                            $"el rótulo «{t}» de {area.Name} no es un pie entero");
                    }
                }
            }
        }

        // ── El PNG: mirarlo sin piloto delante ───────────────────────────────────

        /// <summary>
        /// **Renderiza el gráfico a un PNG en `%TEMP%`** y comprueba que la imagen tiene contenido de
        /// verdad (más de un color y más de un puñado de píxeles distintos). Una imagen en negro o de
        /// un solo color es lo que sale cuando el control no se ha pintado, y es justo el síntoma que
        /// no se ve en una aserción de propiedades.
        ///
        /// Deja además el archivo para poder abrirlo a ojo: es la única forma de comprobar sin un
        /// piloto aterrizando delante si los rótulos se solapan.
        /// </summary>
        [TestMethod]
        public void ElGraficoSePuedeRenderizarAPngFueraDePantalla()
        {
            using (var form = new FlareAnalysisForm(Skcg41(), FlareChartLayoutTests.Skcg41Flare(), true))
            {
                form.Size = new Size(1000, 760);
                form.CreateControl();
                form.PerformLayout();
                form.FlareChart.Width  = 960;
                form.FlareChart.Height = 660;

                using (var bmp = new Bitmap(960, 660))
                {
                    form.FlareChart.DrawToBitmap(bmp, new Rectangle(0, 0, 960, 660));
                    bmp.Save(PngPath, ImageFormat.Png);

                    Assert.IsTrue(File.Exists(PngPath), "el PNG se tiene que haber escrito");
                    Assert.IsTrue(new FileInfo(PngPath).Length > 5000,
                        $"un PNG de un gráfico vacío pesa muy poco ({new FileInfo(PngPath).Length} B)");

                    var colors = new HashSet<int>();
                    for (int y = 0; y < bmp.Height; y += 3)
                        for (int x = 0; x < bmp.Width; x += 3)
                            colors.Add(bmp.GetPixel(x, y).ToArgb());

                    Assert.IsTrue(colors.Count > 5,
                        $"el gráfico renderizado tiene que tener contenido, no un solo color ({colors.Count} distintos)");
                }
            }
        }

        /// <summary>
        /// **El eje X del closeup del perfil vertical, renderizado a PNG.** Es el arreglo que pidió el
        /// mantenedor («el eje X del closeup, la distancia al umbral, tiene decimales») y aquí queda la
        /// prueba **en la imagen**, no solo en el helper: se activa el closeup en el formulario de
        /// análisis y se vuelca el perfil vertical a `%TEMP%`, para poder abrirlo y ver que las marcas
        /// son `5.000 · 4.000 · … · THR · … · 4.000` y no `−2 250 · −750 · 750`.
        /// </summary>
        [TestMethod]
        public void ElCloseupSePuedeRenderizarAPngConElEjeEnPiesEnteros()
        {
            string path = Path.Combine(Path.GetTempPath(), "vmsopenacars_closeup_axis.png");

            using (var form = new LandingAnalysisForm(
                new List<(FlightRecord, List<ApproachTrackPoint>)>
                {
                    // Con traza de verdad: `PopulateCharts` sale antes de rotular si no hay ninguna
                    // distancia (`maxDist <= 0`), así que un vuelo sin traza no probaría nada del eje.
                    (Skcg41(), FlareChartLayoutTests.TrackFromFlare())
                }))
            {
                form.Size = new Size(1000, 740);
                form.CreateControl();
                form.CloseupToggle.Checked = true;      // el closeup dispara el eje en pies
                form.PerformLayout();

                var chart = form.VerticalProfile;
                // A un ancho realista: el perfil vertical ocupa la mitad de un formulario de 1 000 px.
                chart.Width  = 490;
                chart.Height = 330;

                using (var bmp = new Bitmap(490, 330))
                {
                    chart.DrawToBitmap(bmp, new Rectangle(0, 0, 490, 330));
                    bmp.Save(path, ImageFormat.Png);
                    Assert.IsTrue(new FileInfo(path).Length > 3000,
                        $"el PNG del closeup pesa poco: el gráfico no se pintó ({new FileInfo(path).Length} B)");
                }

                // Y las etiquetas del eje son las del helper, todas enteras.
                var textos = chart.ChartAreas["main"].AxisX.CustomLabels
                                  .Cast<CustomLabel>().Select(c => c.Text).ToList();
                Assert.IsTrue(textos.Count > 0, "el closeup tiene que rotular el eje");
                CollectionAssert.Contains(textos, CloseupAxis.ThresholdLabel);
                foreach (string t in textos)
                {
                    if (t == CloseupAxis.ThresholdLabel) continue;
                    string digits = t.Replace(".", "").Replace("-", "");
                    Assert.IsTrue(digits.Length > 0 && digits.All(char.IsDigit),
                                  $"el rótulo «{t}» del closeup no es un pie entero");
                }
            }
        }

        /// <summary>
        /// **El closeup con la Y autoescalada y la traza fina, renderizado a PNG.** Es lo que pide la
        /// verificación por imagen que ya destapó el rótulo «THR» solapado y las bandas en vertical:
        /// se vuelca el perfil vertical a `%TEMP%` en la escala fina (±1 000 ft), que solo tiene
        /// sentido con las 40 muestras de la traza del flare, para poder abrirlo y ver que la curva
        /// ocupa el gráfico en vez de quedarse aplastada contra el suelo.
        /// </summary>
        [TestMethod]
        public void ElCloseupConYAutoescaladaYTrazaFina_SePuedeRenderizarAPng()
        {
            string path = Path.Combine(Path.GetTempPath(), "vmsopenacars_closeup_yfit_flare.png");

            using (var form = new LandingAnalysisForm(
                new List<(FlightRecord, List<ApproachTrackPoint>)>
                {
                    (Skcg41(), FlareChartLayoutTests.TrackFromFlare())
                },
                id => FlareChartLayoutTests.Skcg41Flare(),
                id => true))
            {
                form.Size = new Size(1000, 740);
                form.CreateControl();
                form.CloseupToggle.Checked = true;
                form.CloseupScaleSelect.SelectedIndex = 2;   // Last 1 000 ft
                form.PerformLayout();

                var chart = form.VerticalProfile;
                chart.Width  = 490;
                chart.Height = 330;

                using (var bmp = new Bitmap(490, 330))
                {
                    chart.DrawToBitmap(bmp, new Rectangle(0, 0, 490, 330));
                    bmp.Save(path, ImageFormat.Png);

                    Assert.IsTrue(File.Exists(path), "el PNG se tiene que haber escrito");
                    Assert.IsTrue(new FileInfo(path).Length > 3000,
                        $"el PNG del closeup con traza fina pesa poco ({new FileInfo(path).Length} B)");

                    var colors = new HashSet<int>();
                    for (int y = 0; y < bmp.Height; y += 3)
                        for (int x = 0; x < bmp.Width; x += 3)
                            colors.Add(bmp.GetPixel(x, y).ToArgb());
                    Assert.IsTrue(colors.Count > 5,
                        $"el closeup renderizado tiene que tener contenido ({colors.Count} colores)");
                }

                // Y la Y no es la del perfil completo: la traza entera del vuelo 41 llega a 2 563 ft.
                var area = chart.ChartAreas["main"];
                Assert.IsTrue(area.AxisY.Maximum < 2563.0 / 5.0,
                    $"la Y tiene que describir el tramo ({area.AxisY.Maximum})");
                Assert.IsTrue(area.AxisY.Minimum <= 0.0, "y el cero del terreno tiene que estar dentro");
            }
        }

        private static string ControlText(Control root)
        {
            var sb = new System.Text.StringBuilder();
            void Walk(Control c)
            {
                if (c is Label l) sb.AppendLine(l.Text);
                foreach (Control child in c.Controls) Walk(child);
            }
            Walk(root);
            return sb.ToString();
        }
    }
}
