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

        /// <summary>
        /// **La traza del vuelo 41 con flaps y potencia**, que es lo que estrena esta ventana. Los dos
        /// datos son los de un 737 en final: el mando en el 82 % del recorrido (que en el 737 es la
        /// banda del `FLAPS 30`) y el N1 retardado de 58 % a 30 % unos 3 s antes del toque.
        ///
        /// Se apoya en `ThresholdToTouchdownTests.Skcg41FineTrack()`, que es la traza fina **real**
        /// —1 500 ft al umbral a 150 kt, toque a los 1 736,4 ft que dice la base—, en vez de montar una
        /// segunda geometría paralela que pudiera separarse de la primera.
        /// </summary>
        private static List<FlareTrackPoint> Skcg41WithFlapsAndPower()
        {
            var samples = ThresholdToTouchdownTests.Skcg41FineTrack();
            foreach (var s in samples)
            {
                s.FlapsPct = 82.0;                                    // banda del FLAPS 30 en el 737
                s.Eng1Pct  = s.SeqNo <= 28 ? 58.0 : 30.0;
                s.Eng2Pct  = s.SeqNo <= 28 ? 57.0 : 29.0;
            }
            return samples;
        }

        /// <summary>El registro del vuelo 41 con la familia del avión, que es lo que permite etiquetar
        /// los flaps en compuertas en vez de en porcentaje.</summary>
        private static FlightRecord Skcg41B737()
        {
            var r = Skcg41();
            r.AircraftIcao = "B737";
            return r;
        }

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

                // La pista sale del closeup —el mismo objeto— y se dibuja en las áreas **que tienen
                // escala vertical**: altitud y velocidad. La de pitch no tiene un solo punto (esta
                // traza no lo trae), así que no hay eje al que anclar la banda y **no se decora**: se
                // marca «sin datos». Anclarla en un eje automático dejaba puntos no finitos, que es
                // una de las vías por las que el motor se quedaba sin escala al pintar.
                Assert.AreEqual(ChartAxisSafety.NoDataMinimum, chart.ChartAreas["ptc"].AxisY.Minimum, 1e-9);
                Assert.AreEqual(ChartAxisSafety.NoDataMaximum, chart.ChartAreas["ptc"].AxisY.Maximum, 1e-9,
                    "el área de pitch no tiene datos: se queda con un marco fijo, nunca en automático");
                Assert.AreEqual(1, AnnotationsFor(chart, "ptc").Count,
                    "y por eso se marca como sin datos");

                var runway = chart.Series.Where(s => s.Name.StartsWith("RWY 7,841 ft")).ToList();
                Assert.AreEqual(2, runway.Count, "la banda de pista va en las áreas con datos");
                Assert.IsTrue(runway.All(s => s.Points.Count == 2));

                // El umbral y el toque, en las mismas dos. La marca del toque cae **dentro** del
                // encuadre (la captura del vuelo 41 llega a −1 650 ft y el encuadre enseña 1 700 ft);
                // si hubiera quedado fuera, `TouchdownCloseup` la prohibiría en vez de llevarla al
                // borde, que es el criterio que ya fija `TouchdownCloseupGeometryTests`.
                Assert.AreEqual(2, chart.Series.Count(s => s.Name.StartsWith("THR_")));
                Assert.AreEqual(2, chart.Series.Count(s => s.Name.StartsWith("TD_")));
                Assert.IsTrue(form.SummaryLabel.Text.Contains("TD 1,600 ft"), form.SummaryLabel.Text);

                // Y las bandas de la zona de toma, que son las que puntúa `TouchdownZonePolicy`, en
                // las áreas decoradas. La de pitch no lleva ninguna: no tiene eje al que anclarlas.
                foreach (var name in new[] { "alt", "spd" })
                    Assert.AreEqual(2, chart.ChartAreas[name].AxisX.StripLines.Count, name);
                Assert.AreEqual(0, chart.ChartAreas["ptc"].AxisX.StripLines.Count, "ptc");

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

                SaveChartPng(form.FlareChart, PngPath);

                Assert.IsTrue(File.Exists(PngPath), "el PNG se tiene que haber escrito");
                Assert.IsTrue(new FileInfo(PngPath).Length > 5000,
                    $"un PNG de un gráfico vacío pesa muy poco ({new FileInfo(PngPath).Length} B)");

                int colors = DistinctColors(PngPath);
                Assert.IsTrue(colors > 5,
                    $"el gráfico renderizado tiene que tener contenido, no un solo color ({colors} distintos)");
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

                SaveChartPng(chart, path);
                Assert.IsTrue(new FileInfo(path).Length > 3000,
                    $"el PNG del closeup pesa poco: el gráfico no se pintó ({new FileInfo(path).Length} B)");

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

                SaveChartPng(chart, path);

                Assert.IsTrue(File.Exists(path), "el PNG se tiene que haber escrito");
                Assert.IsTrue(new FileInfo(path).Length > 3000,
                    $"el PNG del closeup con traza fina pesa poco ({new FileInfo(path).Length} B)");

                int colors = DistinctColors(path);
                Assert.IsTrue(colors > 5,
                    $"el closeup renderizado tiene que tener contenido ({colors} colores)");

                // Y la Y no es la del perfil completo: la traza entera del vuelo 41 llega a 2 563 ft.
                var area = chart.ChartAreas["main"];
                Assert.IsTrue(area.AxisY.Maximum < 2563.0 / 5.0,
                    $"la Y tiene que describir el tramo ({area.AxisY.Maximum})");
                Assert.IsTrue(area.AxisY.Minimum <= 0.0, "y el cero del terreno tiene que estar dentro");
            }
        }

        // ── Flaps y potencia: dos áreas más y la marca del corte ──────────────────

        /// <summary>
        /// **Con flaps y N1 en la traza, el gráfico gana sus dos áreas y la marca del corte.** Son las
        /// dos preguntas del mantenedor —«el porcentaje de flaps sería útil» y «el porcentaje de N2, en
        /// qué momento se corta la potencia»— y aquí se comprueba que llegan a la pantalla, no solo al
        /// helper: las cinco áreas, las series en la suya y el punto del corte sobre la curva de N1.
        ///
        /// Los flaps van **etiquetados** (`≈FLAPS 30`) porque el vuelo trae su familia (`B737`), que es
        /// lo que permite traducir el porcentaje del mando a una compuerta en vez de enseñar el número.
        /// </summary>
        [TestMethod]
        public void ConFlapsYPotencia_ElGraficoGanaLasDosAreasYLaMarcaDelCorte()
        {
            var samples = Skcg41WithFlapsAndPower();

            using (var form = new FlareAnalysisForm(Skcg41B737(), samples, true))
            {
                form.PerformLayout();
                var chart = form.FlareChart;

                Assert.IsNotNull(chart);
                Assert.AreEqual(5, chart.ChartAreas.Count,
                    "altitud, velocidad, pitch, flaps y N1");
                Assert.IsNotNull(chart.ChartAreas["flp"], "el área de flaps");
                Assert.IsNotNull(chart.ChartAreas["n1"],  "el área de potencia");

                // El eje de flaps va de 0 a 100 **siempre**: la escala del mando es absoluta, y
                // autoescalarlo mentiría sobre cuánto flap llevaba el avión.
                Assert.AreEqual(0.0,   chart.ChartAreas["flp"].AxisY.Minimum, 1e-9);
                Assert.AreEqual(100.0, chart.ChartAreas["flp"].AxisY.Maximum, 1e-9);

                // El de N1 sí se aprieta a lo que se ve: 30–57 % con el hueco de la banda.
                Assert.IsTrue(chart.ChartAreas["n1"].AxisY.Minimum >= 20.0
                              && chart.ChartAreas["n1"].AxisY.Minimum <= 30.0,
                    $"el suelo de N1 tiene que describir el tramo ({chart.ChartAreas["n1"].AxisY.Minimum})");
                Assert.IsTrue(chart.ChartAreas["n1"].AxisY.Maximum >= 58.0
                              && chart.ChartAreas["n1"].AxisY.Maximum <= 65.0,
                    $"y el techo, la potencia de aproximación ({chart.ChartAreas["n1"].AxisY.Maximum})");

                // Las dos series de dato, cada una en su área.
                Assert.IsNotNull(chart.Series.FirstOrDefault(s => s.Name == "Flaps (%)" && s.ChartArea == "flp"));
                var n1 = chart.Series.FirstOrDefault(s => s.Name == "N1 (%)" && s.ChartArea == "n1");
                Assert.IsNotNull(n1);
                Assert.AreEqual(samples.Count, n1.Points.Count, "una muestra por punto");

                // Y la marca del corte: una **línea vertical** dentro del área de N1, en el instante que
                // decide `PowerCut`. El caso corta en la muestra 29, que está a +750 ft **antes** del
                // umbral: el piloto retardó el ralentí tres segundos antes de llegar a la pista.
                var cut = chart.ChartAreas["n1"].AxisX.StripLines
                               .Cast<StripLine>()
                               .FirstOrDefault(sl => sl.Text == "PWR CUT");
                Assert.IsNotNull(cut, "el corte de potencia tiene que marcarse sobre la traza de N1");
                Assert.IsTrue(cut.IntervalOffset > 0.0,
                    $"el corte del caso cae antes del umbral ({cut.IntervalOffset})");
                Assert.IsTrue(cut.IntervalOffset < 1000.0, "y no al principio de la captura");

                // Y el resumen del panel lo dice con palabras, del mismo helper que el resto.
                Assert.IsTrue(form.SummaryLabel.Text.Contains("FLAPS"), form.SummaryLabel.Text);
                Assert.IsTrue(form.SummaryLabel.Text.Contains("FLAPS 30"), form.SummaryLabel.Text);
                Assert.IsTrue(form.SummaryLabel.Text.Contains("PWR CUT") ||
                              form.SummaryLabel.Text.Contains("CORTE POT."), form.SummaryLabel.Text);
            }
        }

        /// <summary>
        /// **El gráfico de cinco áreas, renderizado a PNG.** Es la verificación que ya destapó el
        /// solape del rótulo «THR» y las bandas giradas: con tres áreas apiladas el reparto estaba
        /// medido, con cinco cada una es un 40 % más baja y el eje X de abajo puede quedarse sin sitio.
        /// Se vuelca a `%TEMP%` para poder abrirlo y mirarlo.
        /// </summary>
        [TestMethod]
        public void ElGraficoConFlapsYPotencia_SePuedeRenderizarAPng()
        {
            string path = Path.Combine(Path.GetTempPath(), "vmsopenacars_flare_chart_flaps_power.png");

            using (var form = new FlareAnalysisForm(Skcg41B737(), Skcg41WithFlapsAndPower(), true))
            {
                form.Size = new Size(1000, 760);
                form.CreateControl();
                form.PerformLayout();
                form.FlareChart.Width  = 960;
                form.FlareChart.Height = 660;

                SaveChartPng(form.FlareChart, path);

                Assert.IsTrue(File.Exists(path), "el PNG se tiene que haber escrito");
                Assert.IsTrue(new FileInfo(path).Length > 5000,
                    $"un PNG de un gráfico vacío pesa muy poco ({new FileInfo(path).Length} B)");

                int colors = DistinctColors(path);
                Assert.IsTrue(colors > 5,
                    $"el gráfico tiene que tener contenido ({colors} colores)");
            }
        }

        /// <summary>
        /// **Un gráfico con áreas sin datos no puede reventar al volcarse, y se ve que no hay datos.**
        ///
        /// Esta prueba era un diagnóstico suelto (`ZDiagnostico_ChartAreasVsPng`) que montaba áreas
        /// sin ninguna serie y las volcaba con `DrawToBitmap` —el camino `OnPrint → WmPrintClient` de
        /// la pila del popup—. Ahora mide lo que importa: con el blindaje de `ChartAxisSafety`, un
        /// gráfico con áreas vacías se guarda **sin lanzar**, cada área vacía se queda con un marco
        /// fijo y marcada «sin datos», y el PNG resultante tiene contenido.
        /// </summary>
        [TestMethod]
        public void UnGraficoConAreasSinDatos_SeGuardaSinLanzarYSeMarca()
        {
            string[] names = { "alt", "spd", "ptc" };
            string path = Path.Combine(Path.GetTempPath(), "vmsopenacars_chart_sin_datos.png");

            var chart = new Chart { BackColor = Color.FromArgb(20, 30, 42) };
            chart.Size = new Size(960, 660);
            double slice = 74.0 / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var a = new ChartArea(names[i])
                {
                    BackColor = Color.FromArgb(20, 30, 42),
                    Position  = new ElementPosition(9, (float)(2 + i * slice), 88, (float)(slice - 3)),
                };
                a.AxisX.IsReversed = true;
                chart.ChartAreas.Add(a);
            }
            for (int i = 1; i < names.Length; i++) chart.ChartAreas[i].AlignWithChartArea = "alt";

            // Solo la primera área tiene serie: `ptc` y `spd` se quedan sin un solo punto, que es el
            // estado en el que el motor no puede estimar el intervalo automático de sus ejes.
            var s = new Series("s") { ChartType = SeriesChartType.Line, ChartArea = names[0] };
            s.Points.AddXY(1500, 10); s.Points.AddXY(0, 50); s.Points.AddXY(-1500, 20);
            chart.Series.Add(s);

            using (chart)
            {
                int marked = ChartAxisSafety.EnsureExplicit(chart);

                // Las dos áreas sin serie quedan fijadas y marcadas; la que tiene datos, intacta.
                Assert.AreEqual(2, marked, "las dos áreas sin serie tienen que quedar marcadas");
                Assert.AreEqual(ChartAxisSafety.NoDataMinimum, chart.ChartAreas["ptc"].AxisY.Minimum, 1e-9);
                Assert.AreEqual(ChartAxisSafety.NoDataMaximum, chart.ChartAreas["ptc"].AxisY.Maximum, 1e-9);
                Assert.AreEqual(1, AnnotationsFor(chart, "ptc").Count);
                Assert.AreEqual(L._(ChartAxisSafety.NoDataKey),
                                ((TextAnnotation)AnnotationsFor(chart, "ptc")[0]).Text,
                                "el área sin datos tiene que decirlo");
                Assert.AreEqual(0, AnnotationsFor(chart, "alt").Count,
                                "el área con datos no se marca");

                SaveChartPng(chart, path);
                Assert.IsTrue(new FileInfo(path).Length > 1000,
                    $"el PNG del gráfico con áreas vacías no se escribió ({new FileInfo(path).Length} B)");
            }
        }

        // ── La cabecera de datos: lo que se comparte ─────────────────────────────

        /// <summary>
        /// **Un vuelo sin traza de flare y sin aeronave guardada no puede tumbar la ventana.** Es el
        /// caso real de los vuelos anteriores a `flare_track` (y de los que se grabaron antes de que
        /// existiera `flights.aircraft_icao`): no hay muestras —no se pinta gráfico— y no hay título,
        /// modelo ni familia, así que la cabecera **no tiene de dónde sacar la aeronave ni el peso**.
        ///
        /// Lo que se mide: que el formulario se construye y se reparte sin lanzar, que el bloque
        /// **no inventa** la línea de aeronave, y que el aviso de «no hay traza fina» sigue en su
        /// franja sin que el bloque lo tape.
        /// </summary>
        [TestMethod]
        public void SinTrazaNiAeronaveGuardada_LaVentanaNoLanzaYNoInventaLineas()
        {
            var record = Skcg41();       // sin AircraftIcao / AircraftTitle / AircraftModel
            Assert.IsNull(record.AircraftIcao);

            using (var form = new FlareAnalysisForm(record, new List<FlareTrackPoint>(), true))
            {
                form.CreateControl();
                form.PerformLayout();

                Assert.IsNull(form.FlareChart, "sin muestras no hay gráfico");
                Assert.IsNotNull(form.SummaryLabel);
                StringAssert.Contains(form.SummaryLabel.Text, "No flare samples");

                // La cabecera no puede afirmar una aeronave que no se guardó.
                StringAssert.Contains(form.SummaryLabel.Text, L._(LandingHeader.KeyFlight),
                    "el vuelo sí se sabe y se publica");
                Assert.IsFalse(form.SummaryLabel.Text.Contains(L._(LandingHeader.KeyAircraft)),
                    "sin dato no se escribe la línea de aeronave");
                Assert.IsFalse(form.SummaryLabel.Text.Contains(L._(LandingHeader.KeyWeight)),
                    "ni la del peso");

                // El reparto: el bloque crece hacia arriba y el contenido no lo invade.
                var contenido = form.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Dock == DockStyle.Fill);
                Assert.IsNotNull(contenido);
                Assert.IsTrue(contenido.Bottom <= form.SummaryLabel.Top,
                    $"el contenido no puede invadir el bloque (contenido {contenido.Bounds}, bloque {form.SummaryLabel.Bounds})");

                // Sin gráfico no hay nada que guardar: el botón se queda apagado.
                Assert.IsNotNull(form.SaveImageButton);
                Assert.IsFalse(form.SaveImageButton.Enabled);
            }
        }

        /// <summary>
        /// **Con datos, la cabecera del gráfico lleva la identidad del vuelo y el bloque crece.** La
        /// franja del formulario pasa de una línea a varias; aquí se comprueba que sigue sin comerse
        /// el gráfico (`Dock.Bottom` reserva su alto) y que el botón de guardar está disponible.
        /// </summary>
        [TestMethod]
        public void ConAeronave_LaCabeceraLlevaLaIdentidadYElBloqueNoTapaElGrafico()
        {
            using (var form = new FlareAnalysisForm(Skcg41B737(), Skcg41WithFlapsAndPower(), true))
            {
                form.CreateControl();
                form.PerformLayout();

                StringAssert.Contains(form.SummaryLabel.Text, "B737",
                    "la familia guardada con el vuelo sale en la cabecera");
                StringAssert.Contains(form.SummaryLabel.Text, "Boeing",
                    "y el fabricante, deducido del designador ICAO");

                var contenido = form.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Dock == DockStyle.Fill);
                Assert.IsTrue(contenido.Bottom <= form.SummaryLabel.Top,
                    $"el bloque no puede invadir el gráfico (contenido {contenido.Bounds}, bloque {form.SummaryLabel.Bounds})");

                Assert.IsTrue(form.SaveImageButton.Enabled, "con gráfico, el botón de guardar está activo");

                // **El ancho**: el bloque es monoespaciado y se corta, no se reparte. Se mide **línea a
                // línea** con el tipo de letra real del rótulo —`PreferredWidth` mide el texto como
                // una sola línea y aquí hay varias—.
                //
                // Se mide el **bloque de datos** (todas las líneas menos la última, que es la línea
                // técnica del gráfico: muestras, encuadre, bandas de la TDZ y THR-TD). Esa última ya
                // era más larga que la ventana **antes** de este cambio —la compone
                // `TouchdownCloseupGeometry.Summary`, que comparten los dos formularios— y aquí queda
                // documentada en vez de tapada con una aserción que no se sostiene.
                var lineas = form.SummaryLabel.Text.Split('\n');
                var bloque = lineas.Take(lineas.Length - 1).ToArray();
                var fuente = form.SummaryLabel.Font;

                foreach (string linea in bloque)
                {
                    int ancho = TextRenderer.MeasureText(linea, fuente).Width;
                    Assert.IsTrue(ancho <= form.ClientSize.Width,
                        $"una línea del bloque no cabe a lo ancho ({ancho} px de {form.ClientSize.Width} px): «{linea}»");
                }

                // El nombre propuesto lleva el vuelo y la pista, para poder archivarlo.
                StringAssert.Contains(form.DefaultImageName(), "9821");
                StringAssert.Contains(form.DefaultImageName(), "SKBG-SKCG");
                StringAssert.Contains(form.DefaultImageName(), "RWY01");
            }
        }

        /// <summary>
        /// **El botón de guardar escribe el PNG con `Chart.SaveImage`.** Se prueba el volcado sin el
        /// `SaveFileDialog` a propósito: una prueba **no puede abrir una ventana** delante del
        /// mantenedor. Y la ruta que usa es la del motor de gráficos, no `DrawToBitmap`, que es la
        /// pila de la excepción que ya apareció en pantalla.
        /// </summary>
        [TestMethod]
        public void ElBotonDeGuardar_VuelcaElPngSinPasarPorDrawToBitmap()
        {
            string path = Path.Combine(Path.GetTempPath(), "vmsopenacars_flare_header.png");

            using (var form = new FlareAnalysisForm(Skcg41B737(), Skcg41WithFlapsAndPower(), true))
            {
                form.CreateControl();
                form.PerformLayout();
                form.FlareChart.Width  = 960;
                form.FlareChart.Height = 620;

                try
                {
                    form.SaveChartImage(path);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"el volcado del gráfico falló: {ex.GetType().Name}: {ex.Message}");
                }

                Assert.IsTrue(File.Exists(path), "el PNG se tiene que haber escrito");
                Assert.IsTrue(new FileInfo(path).Length > 5000,
                    $"un PNG con contenido pesa más que esto ({new FileInfo(path).Length} B)");
                Assert.IsTrue(DistinctColors(path) > 5,
                    "y tiene que tener contenido, no un solo color");

                // Y el título del gráfico lleva la identidad que viaja en la imagen: sin ella, un PNG
                // compartido no diría de qué avión ni de qué día es. **En una sola línea**: las áreas
                // se colocan en porcentajes del control y un título de dos líneas se les echa encima
                // —lo destapó el PNG con `9821 · SKBG → SKCG …` cruzado por la traza de AGL—.
                var titulo = form.FlareChart.Titles.Cast<Title>().FirstOrDefault();
                Assert.IsNotNull(titulo);
                StringAssert.Contains(titulo.Text, "RWY");
                Assert.IsFalse(titulo.Text.Contains("\n"),
                    "el título tiene que caber en una línea o se solapa con la primera área");
            }
        }

        // ── Auxiliares del volcado ────────────────────────────────────────────────

        /// <summary>Las anotaciones que marcan un área como sin datos (van en el gráfico, sin recortar).</summary>
        private static List<Annotation> AnnotationsFor(Chart chart, string areaName)
            => chart.Annotations.Cast<Annotation>()
                    .Where(a => a.Name == ChartAxisSafety.NoDataAnnotationName(areaName)).ToList();

        /// <summary>
        /// **Vuelca el gráfico a PNG con `Chart.SaveImage`, que es el render propio del control.**
        ///
        /// No se usa `Control.DrawToBitmap`: ese camino manda `WM_PRINTCLIENT`, y el control lo atiende
        /// por `OnPrint → WmPrintClient`, que es exactamente la pila de la excepción que aparecía en
        /// pantalla al correr las pruebas. `SaveImage` pinta con el motor del propio gráfico y no pasa
        /// por el bucle de mensajes de Windows.
        ///
        /// Y todo va dentro de un `try/catch` que **falla el test con el mensaje**: una prueba no puede
        /// abrir una ventana de excepción delante del mantenedor.
        /// </summary>
        private static void SaveChartPng(Chart chart, string path)
        {
            try
            {
                chart.SaveImage(path, ChartImageFormat.Png);
            }
            catch (Exception ex)
            {
                Assert.Fail($"el render del gráfico a PNG falló: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Cuántos colores distintos tiene el PNG, muestreado cada 3 px.</summary>
        private static int DistinctColors(string path)
        {
            using (var bmp = new Bitmap(path))
            {
                var colors = new HashSet<int>();
                for (int y = 0; y < bmp.Height; y += 3)
                    for (int x = 0; x < bmp.Width; x += 3)
                        colors.Add(bmp.GetPixel(x, y).ToArgb());
                return colors.Count;
            }
        }

        private static string ControlText(Control root)        {
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
