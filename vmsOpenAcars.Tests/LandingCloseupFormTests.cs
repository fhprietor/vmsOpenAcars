using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Models;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El closeup del perfil vertical, en el formulario de verdad.**
    ///
    /// La guía avisa de la trampa de este proyecto: en un `TableLayoutPanel` dos controles en la
    /// misma celda **no dan error**, el último se pinta encima y el síntoma es un gráfico tapado.
    /// Igual de silencioso sería recortar la rejilla de los cuatro gráficos. Aquí se instancia
    /// `LandingAnalysisForm` **sin enseñarlo** (como `EcamDialogTests` y `SettingsFormLayoutTests`)
    /// y se mide el reparto y el cableado del closeup: no el dibujo en pantalla, que eso no se puede
    /// comprobar sin ojos.
    ///
    /// La traza es la **real** del vuelo 41 de la base local —SKBG → SKCG, pista 01, toque a
    /// 1 736 ft, pista de 7 841 ft—: las siete últimas muestras de `approach_track`, con su
    /// `dist_nm` y su AGL de verdad.
    /// </summary>
    [TestClass]
    public class LandingCloseupFormTests
    {
        private const double FeetPerNm = 6076.12;

        // ── El vuelo 41 de la base local (SKBG → SKCG, pista 01) ──────────────────
        private static FlightRecord SkcgFlight(double? runwayLengthFt = 7841.0, double touchdownFt = 1736.398)
            => new FlightRecord
            {
                Id              = 41,
                FlightNumber    = "9821",
                Origin          = "SKBG",
                Destination     = "SKCG",
                RunwayName      = "01",
                FlightDate      = new DateTime(2026, 10, 7, 3, 9, 22, DateTimeKind.Utc),
                LandingRateFpm  = -223,
                GForce          = 1.48,
                TouchdownDistFt = touchdownFt,
                CenterlineDevFt = 5.27,
                Score           = 82,
                RunwayLengthFt  = runwayLengthFt,
            };

        /// <summary>Las siete últimas muestras del vuelo 41 (`seq_no` 78–84), en pies al umbral y
        /// con su AGL: la curva real de los últimos 1 700 ft.</summary>
        private static List<ApproachTrackPoint> SkcgTrack()
        {
            var raw = new[]
            {
                // dist_ft (positivo = antes del umbral), agl_ft
                (1653.8, 147.0), (1103.0, 131.0), (558.3, 119.0), (20.7, 102.0),
                (-499.3, 80.0), (-1020.6, 60.0), (-1541.7, 50.0),
            };

            var track = new List<ApproachTrackPoint>();
            for (int i = 0; i < raw.Length; i++)
                track.Add(new ApproachTrackPoint
                {
                    FlightId = 41,
                    SeqNo    = 78 + i,
                    AltFt    = 76.0 + raw[i].Item2,
                    AglFt    = raw[i].Item2,
                    IasKt    = 143.0,
                    VsFpm    = -281.0,
                    DistNm   = raw[i].Item1 / FeetPerNm,
                    LateralFt = 5.0,
                });
            return track;
        }

        private static LandingAnalysisForm SingleFlightForm(double? runwayLengthFt = 7841.0,
                                                            double touchdownFt = 1736.398)
            => new LandingAnalysisForm(new List<(FlightRecord, List<ApproachTrackPoint>)>
            {
                (SkcgFlight(runwayLengthFt, touchdownFt), SkcgTrack())
            });

        // ── El reparto: la barra no puede comerse ni tapar los gráficos ───────────

        [TestMethod]
        public void LaBarraDelCloseup_NoSeSuperponeALaRejillaDeLosCuatroGraficos()
        {
            using (var form = SingleFlightForm())
            {
                form.PerformLayout();

                Rectangle grid = form.ChartGrid.Bounds;
                Rectangle bar  = form.CloseupBar.Bounds;

                Assert.IsTrue(bar.Bottom <= form.ClientSize.Height,
                    $"la barra del closeup tiene que caber en la ventana ({bar} en {form.ClientSize})");
                Assert.IsTrue(grid.Bottom <= bar.Top,
                    $"la barra no puede invadir la rejilla de los cuatro gráficos (rejilla {grid}, barra {bar})");
                Assert.IsTrue(grid.Height > 0 && grid.Width > 0, "los gráficos siguen teniendo área");

                // Y siguen siendo cuatro, en cuatro celdas distintas de la rejilla 2×2.
                var charts = form.ChartGrid.Controls.OfType<Chart>().ToList();
                Assert.AreEqual(4, charts.Count, "el closeup no puede añadir ni quitar gráficos");
                var cells = charts.Select(c => (Col: form.ChartGrid.GetColumn(c),
                                                Row: form.ChartGrid.GetRow(c))).ToList();
                Assert.AreEqual(4, cells.Distinct().Count(),
                    "cada gráfico tiene que seguir en su propia celda de la rejilla");
                Assert.IsTrue(form.ChartGrid.Controls.OfType<Chart>()
                                  .Any(c => (string)c.Tag == "VERTICAL"),
                              "falta el perfil vertical");
            }
        }

        [TestMethod]
        public void ElCloseup_VieneApagadoYNoSeOfreceEnComparacion()
        {
            using (var form = SingleFlightForm())
            {
                form.PerformLayout();
                Assert.IsNotNull(form.CloseupToggle, "el closeup tiene que poder activarse");
                Assert.IsFalse(form.CloseupToggle.Checked, "nace apagado: el gráfico de siempre no cambia");
                Assert.IsFalse(form.CloseupScaleSelect.Enabled, "la escala solo se toca con el closeup activo");
                Assert.AreEqual(2, form.CloseupScaleSelect.Items.Count, "dos escalas, como mínimo");
            }

            using (var form = new LandingAnalysisForm(
                new List<(FlightRecord, List<ApproachTrackPoint>)>
                {
                    (SkcgFlight(), SkcgTrack()),
                    (SkcgFlight(12467.0, 2924.0), SkcgTrack()),
                }))
            {
                form.PerformLayout();
                Assert.IsNull(form.CloseupBar,
                    "en comparación no hay closeup: es de una pista y un toque, no de tres vuelos");
            }
        }

        // ── El cableado: el rango y las marcas salen del helper ──────────────────

        [TestMethod]
        public void AlActivarElCloseup_ElPerfilVerticalPasaAPiesYEncuadraElTramoFinal()
        {
            using (var form = SingleFlightForm())
            {
                form.PerformLayout();
                var area = form.VerticalProfile.ChartAreas["main"];

                Assert.AreEqual(0.0, area.AxisX.Minimum, 1e-9, "sin closeup el eje arranca en el umbral");
                Assert.AreEqual("Distance to threshold (NM)", area.AxisX.Title);
                double ejeNormalNm = area.AxisX.Maximum;

                form.CloseupToggle.Checked = true;
                form.PerformLayout();

                Assert.AreEqual("Distance to threshold (ft)", area.AxisX.Title,
                    "en un encuadre de 9 500 ft las etiquetas en NM no dicen nada");
                Assert.AreEqual(-4500.0, area.AxisX.Minimum, 1e-9,
                    "se ven 4 500 ft pasados el umbral, donde está la pista");
                Assert.AreEqual(5000.0, area.AxisX.Maximum, 1e-9,
                    "y 5 000 ft antes del umbral: el encuadre lo fija la escala, no hasta dónde llega la traza");
                Assert.AreEqual(-50.0, area.AxisY.Minimum, 1e-9,
                    "el suelo baja bajo AGL 0 para que la barra de pista se vea entera");

                // La pista, con su largo de verdad, y el umbral y el toque marcados.
                Assert.IsNotNull(FindSeries(form, "RWY 7,841 ft"),
                    "la pista se dibuja con la longitud que publica NavData");
                Assert.IsNotNull(FindSeries(form, "THR"));
                var td = FindSeries(form, "TD");
                Assert.IsNotNull(td);
                Assert.AreEqual(-1736.398, td.Points[0].XValue, 1e-6,
                    "la marca del toque va en X negativa, pasada la pista del umbral");
                Assert.AreEqual("TD 1,736 ft from threshold", td.Points[1].Label);

                // Las dos bandas de la zona de toma, sombreadas sobre el eje X.
                var bands = area.AxisX.StripLines;
                Assert.AreEqual(2, bands.Count, "una banda para los 0 puntos y otra para los 3");
                Assert.AreEqual(-1500.0, bands[0].IntervalOffset, 1e-9);
                Assert.AreEqual(1500.0, bands[0].StripWidth, 1e-9);
                Assert.AreEqual(-3000.0, bands[1].IntervalOffset, 1e-9);
                Assert.AreEqual(1500.0, bands[1].StripWidth, 1e-9, "de 1 500 a 3 000 ft");

                Assert.IsTrue(form.CloseupSummaryLabel.Text.Contains("TD 1,736 ft from threshold"));
                Assert.IsTrue(form.CloseupSummaryLabel.Text.Contains("3 pts"));

                // La barra de pista arranca en el umbral (x = 0) y se mete hacia la derecha: solo
                // caben 4 500 ft de los 7 841, así que sale del encuadre, que es lo correcto.
                var runway = FindSeries(form, "RWY 7,841 ft");
                Assert.AreEqual(-4500.0, runway.Points[0].XValue, 1e-9);
                Assert.AreEqual(0.0, runway.Points[1].XValue, 1e-9);
                Assert.AreEqual(0.0, runway.Points[1].YValues[0], 1e-9, "la pista va en AGL 0");

                // Y el eje normal era el de siempre (0 … 0,3 NM con esta traza), no se ha perdido.
                Assert.IsTrue(ejeNormalNm > 0.0 && ejeNormalNm < 1.0,
                    $"el eje sin closeup va en NM (dio {ejeNormalNm})");
            }
        }

        [TestMethod]
        public void LaEscalaDeCerca_ReduceElEncuadreYAlApagarElCloseupSeVuelveAlDeSiempre()
        {
            using (var form = SingleFlightForm())
            {
                form.PerformLayout();
                var area = form.VerticalProfile.ChartAreas["main"];
                double ejeNormalNm = area.AxisX.Maximum;   // 0,3 NM: hasta donde llega esta traza

                form.CloseupToggle.Checked = true;
                form.CloseupScaleSelect.SelectedIndex = 1;

                Assert.AreEqual(-2500.0, area.AxisX.Minimum, 1e-9);
                Assert.AreEqual(2500.0, area.AxisX.Maximum, 1e-9,
                    "la escala de cerca solo deja ver los últimos 2 500 ft antes del umbral");

                form.CloseupToggle.Checked = false;

                Assert.AreEqual(0.0, area.AxisX.Minimum, 1e-9);
                Assert.AreEqual(ejeNormalNm, area.AxisX.Maximum, 1e-9,
                    "vuelve el eje en NM de siempre, tal cual estaba");
                Assert.AreEqual("Distance to threshold (NM)", area.AxisX.Title);
                Assert.IsNull(FindSeries(form, "THR"),
                    "apagado, el perfil vertical vuelve a ser el de siempre: sin marcas del closeup");
                Assert.IsNull(FindSeries(form, "RWY 7,841 ft"));
                Assert.AreEqual(0, area.AxisX.StripLines.Count);
                Assert.AreEqual(0.0, area.AxisY.Minimum, 1e-9);
                Assert.AreEqual("", form.CloseupSummaryLabel.Text);
            }
        }

        // ── Degradar sin datos, también en el formulario ─────────────────────────

        [TestMethod]
        public void SinLongitudDePista_NoSeDibujaLaPistaPeroSiElUmbralYElToque()
        {
            // Los vuelos guardados antes de que `flights` tuviera `runway_length_ft`.
            using (var form = SingleFlightForm(runwayLengthFt: null))
            {
                form.PerformLayout();
                form.CloseupToggle.Checked = true;

                Assert.IsFalse(AllSeries(form).Any(s => s.Name.StartsWith("RWY")),
                    "sin longitud no se dibuja una pista de largo inventado");
                Assert.IsNotNull(FindSeries(form, "THR"), "el umbral sí se marca");
                Assert.IsNotNull(FindSeries(form, "TD"),  "y el toque también");
                Assert.IsTrue(form.CloseupSummaryLabel.Text.StartsWith("RWY —"));
            }
        }

        [TestMethod]
        public void SinDistanciaDeToma_NoSeMarcaNingunPuntoDeToque()
        {
            // Los vuelos 38 y 40 de la base: `touchdown_dist_ft = 0`.
            using (var form = SingleFlightForm(touchdownFt: 0.0))
            {
                form.PerformLayout();
                form.CloseupToggle.Checked = true;

                Assert.IsNull(FindSeries(form, "TD"),
                    "sin distancia de toma no hay marca: un 0 no es un toque en el umbral");
                Assert.IsNotNull(FindSeries(form, "THR"));
                Assert.IsNotNull(FindSeries(form, "RWY 7,841 ft"));
                Assert.IsTrue(form.CloseupSummaryLabel.Text.Contains("TD —"));
            }
        }

        [TestMethod]
        public void ToqueMasAllaDelEncuadre_NoSeDibujaLaMarcaPeroSiSeDiceLaDistancia()
        {
            // Vuelo 11: SKCL → KJFK 22L, toque a 5 898 ft — la toma más larga del corpus. No cabe en
            // los 4 500 ft vistos, y llevarla al borde sería colocar el toque donde no está.
            using (var form = SingleFlightForm(runwayLengthFt: 10006.0, touchdownFt: 5898.0))
            {
                form.PerformLayout();
                form.CloseupToggle.Checked = true;

                Assert.IsNull(FindSeries(form, "TD"));
                Assert.IsNotNull(FindSeries(form, "THR"));
                Assert.IsTrue(form.CloseupSummaryLabel.Text.Contains("TD 5,898 ft from threshold"));
                Assert.IsTrue(form.CloseupSummaryLabel.Text.Contains("beyond this view"));
            }
        }

        // ── Auxiliares ────────────────────────────────────────────────────────────

        private static Series FindSeries(LandingAnalysisForm form, string name)
            => AllSeries(form).FirstOrDefault(s => s.Name == name);

        private static IEnumerable<Series> AllSeries(LandingAnalysisForm form)
            => form.VerticalProfile.Series.Cast<Series>();
    }
}
