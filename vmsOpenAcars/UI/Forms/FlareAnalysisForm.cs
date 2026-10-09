using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.UI.Forms
{
    /// <summary>
    /// **El gráfico del flare**: la maniobra de toma en detalle, con la **altitud** (y el
    /// radioaltímetro cuando existe), la **velocidad**, el **pitch**, los **flaps** y el **N1** frente
    /// a la **distancia al umbral**, más las marcas de pista que ya usa el closeup del perfil vertical.
    ///
    /// **Vive en su propia ventana, no dentro de `LandingAnalysisForm`.** No es una preferencia
    /// estética: la guía del proyecto avisa de que en un `TableLayoutPanel` dos controles en la misma
    /// celda **no dan error** —el último se pinta encima— y hay tests que miden ese reparto
    /// (`LandingCloseupFormTests`). Meter más gráficos en la rejilla 2×2 del análisis habría exigido
    /// repartirla y habría puesto en riesgo los cuatro gráficos ya probados. Aparte, la ventana se
    /// abre desde el botón **FLARE** de la barra del closeup y se puede mirar a la vez que el
    /// análisis, cada una con su zoom.
    ///
    /// **Las áreas de flaps y potencia solo aparecen si la traza trae el dato.** Las tres primeras
    /// —altitud, velocidad y pitch— están siempre, como antes; añadir una fila vacía cuando el avión
    /// no publica el offset sería enseñar un gráfico plano que no dice nada.
    ///
    /// **Toda la geometría la decide `Helpers/FlareChartLayout`** (puro, con test), que a su vez
    /// delega la pista, el umbral, las bandas de la zona de toma y el punto de toque en
    /// `TouchdownCloseupGeometry` — el mismo objeto que pinta el closeup. Aquí solo se traduce a
    /// series y ejes: no hay una segunda definición de la zona de toma ni del largo de pista. Y las
    /// compuertas de flaps y el corte de potencia salen de `FlapTrackSummary` y `PowerCut`, que son
    /// los mismos que los publican en la línea del aterrizaje y en el PIREP.
    ///
    /// **Sin `flare_track` no se inventa nada**: si el vuelo es anterior a esta traza (o la captura
    /// no llegó a armarse), el formulario lo dice y **no** rellena con `approach_track`, que sería
    /// vender un muestreo de 2 s como si fuera de 0,1 s.
    /// </summary>
    public class FlareAnalysisForm : Form
    {
        private readonly FlightRecord           _record;
        private readonly IList<FlareTrackPoint> _samples;
        private readonly bool                   _flareTrackExists;

        private Chart _chart;
        private Label _lblSummary;

        private static readonly Color AltitudeColor = Color.FromArgb( 30, 144, 255);   // DodgerBlue
        private static readonly Color RadarColor    = Color.FromArgb(140, 210, 255);   // radioaltímetro
        private static readonly Color SpeedColor    = Color.FromArgb(255, 215,   0);   // Gold
        private static readonly Color PitchColor    = Color.FromArgb( 50, 205,  50);   // LimeGreen
        private static readonly Color FlapColor     = Color.FromArgb(255, 150,  40);   // naranja
        private static readonly Color PowerColor    = Color.FromArgb(210, 130, 255);   // violeta
        private static readonly Color PowerCutColor = Color.FromArgb(255,  80,  80);   // rojo

        /// <summary>Suelo del eje de altitud: unos pies por debajo de 0 para que la banda de pista
        /// —que va **en** el suelo— no quede cortada por el borde inferior, como en el closeup.</summary>
        private const double FloorFt = -50.0;

        /// <summary>
        /// <paramref name="captureStarted"/> distingue los dos motivos de «no hay nada que pintar»:
        /// **hubo captura pero salió vacía** (armada y sin muestras guardadas, que no debería pasar)
        /// frente a **este vuelo no tiene traza fina** —los anteriores a `flare_track`, o aquellos en
        /// los que la pista del umbral nunca se resolvió y la captura jamás se armó—. El mensaje lo
        /// dice con esas palabras y **nunca** se rellena con `approach_track`.
        /// </summary>
        public FlareAnalysisForm(FlightRecord record, IList<FlareTrackPoint> samples, bool captureStarted)
        {
            _record            = record;
            _samples           = samples ?? new List<FlareTrackPoint>();
            _flareTrackExists  = captureStarted;
            BuildUI();
        }
        /// <summary>Punto de medida para los tests: el gráfico, sin necesidad de enseñar la ventana.</summary>
        internal Chart FlareChart => _chart;

        /// <summary>Punto de medida para los tests: el rótulo de resumen (o el aviso de que no hay datos).</summary>
        internal Label SummaryLabel => _lblSummary;

        // ── Layout ────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            Text            = $"Flare — {_record.FlightNumber} {_record.DisplayRoute} · RWY {_record.RunwayName}";
            Size            = new Size(1000, 760);
            MinimumSize     = new Size(760, 520);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor       = Color.FromArgb(18, 26, 36);
            Padding         = new Padding(2);

            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            };

            _lblSummary = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 28,
                Font      = new Font("Consolas", 9),
                ForeColor = Color.FromArgb(170, 215, 185),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0),
                BackColor = Color.FromArgb(15, 22, 32)
            };
            _lblSummary.Text = HasSamples ? "" : MissingDataMessage().Replace("\r\n", " ");

            // El aviso de «no hay traza fina» va en **su propia franja** (`Dock.Fill`), nunca encima
            // del gráfico: dos controles en la misma celda se pintan uno sobre otro sin dar error.
            if (HasSamples)
            {
                _chart = BuildChart();
                Controls.Add(_chart);
            }
            else
            {
                var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 26, 36) };
                pnl.Controls.Add(new Label
                {
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font      = new Font("Consolas", 11, FontStyle.Bold),
                    ForeColor = Color.FromArgb(200, 170, 90),
                    Text      = MissingDataMessage()
                });
                Controls.Add(pnl);
            }

            Controls.Add(_lblSummary);
            Controls.Add(BuildTitleBar());
        }

        private bool HasSamples => _samples != null && _samples.Count > 0;

        private string MissingDataMessage()
        {
            return _flareTrackExists
                ? "No flare samples captured for this landing."
                : "No flare track for this landing.\r\n\r\n" +
                  "This flight was recorded before the flare track existed (or the runway\r\n" +
                  "threshold was never resolved). The 2 s approach track is NOT plotted here:\r\n" +
                  "doing so would claim a resolution this graph does not have.";
        }

        private Panel BuildTitleBar()
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(28, 40, 52) };

            pnl.Controls.Add(new Label
            {
                Text      = $"FLARE ANALYSIS  ·  {_record.FlightNumber}  {_record.DisplayRoute}  ·  RWY {_record.RunwayName}",
                Font      = new Font("Consolas", 11, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location  = new Point(10, 8),
                AutoSize  = true
            });

            var btnX = new Button
            {
                Text      = "✕",
                Font      = new Font("Arial", 12, FontStyle.Bold),
                Size      = new Size(30, 25),
                Location  = new Point(ClientSize.Width - 35, 5),
                BackColor = Color.FromArgb(150, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnX.FlatAppearance.BorderSize = 0;
            btnX.Click += (s, e) => Close();
            pnl.Controls.Add(btnX);
            Resize += (s, e) => btnX.Location = new Point(ClientSize.Width - 35, 5);

            bool dragging = false; Point dragStart = Point.Empty;
            pnl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = new Point(e.X, e.Y); } };
            pnl.MouseMove += (s, e) => { if (dragging) { var p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            pnl.MouseUp   += (s, e) => dragging = false;

            return pnl;
        }

        // ── El gráfico ────────────────────────────────────────────────────────────

        /// <summary>
        /// **Áreas apiladas con el MISMO eje X**, no varios gráficos: así el flare se lee en vertical
        /// y la distancia al umbral es la misma columna en todas. `AlignWithChartArea` es lo que
        /// garantiza que sea **el mismo** eje y no varios parecidos.
        ///
        /// El reparto es proporcional al número de áreas, con el mismo hueco entre ellas que antes:
        /// con tres, las bandas caen donde caían (2–23, 27–48, 52–73).
        /// </summary>
        private Chart BuildChart()
        {
            var layout = FlareChartLayout.Build(_samples, _record.RunwayLengthFt ?? 0.0,
                                                _flareTrackExists, _record.AircraftIcao);

            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 30, 42) };

            // Las tres de siempre, siempre; flaps y potencia solo si la traza los trae.
            var areas = new List<(string Name, string Title)>
            {
                ("alt", "Altitude (ft)"),
                ("spd", "IAS (kt)"),
                ("ptc", "Pitch (°)"),
            };
            if (layout.HasFlaps) areas.Add(("flp", "Flaps (%)"));
            if (layout.HasPower) areas.Add(("n1",  "N1 (%)"));

            // El alto útil va de 2 a 76: lo que queda por debajo es la leyenda y el eje X rotulado,
            // que va dentro del área de abajo. Antes eran tres bandas de 21 puntos con 4 de hueco.
            double slice = 74.0 / areas.Count;
            for (int i = 0; i < areas.Count; i++)
            {
                double top    = 2.0 + i * slice;
                double bottom = top + slice - 3.0;
                chart.ChartAreas.Add(MakeArea(areas[i].Name, top, bottom, areas[i].Title));
            }

            // Todas se alinean con la primera: así el umbral cae en la misma columna en todas. El
            // título y las etiquetas del X los lleva **una sola** banda, y cuál es se decide al final,
            // cuando ya se sabe cuáles tienen datos (ver `RelabelBottomArea`).
            foreach (var area in chart.ChartAreas)
                if (area.Name != "alt") area.AlignWithChartArea = "alt";

            chart.Titles.Add(new Title("Flare — altitude · speed · pitch · flaps · N1 vs distance to threshold")
            {
                Font      = new Font("Consolas", 10, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Docking   = Docking.Top,
                Alignment = ContentAlignment.TopLeft
            });

            chart.Legends.Add(new Legend
            {
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font      = new Font("Consolas", 8),
                Docking   = Docking.Bottom,
            });

            _lblSummary.Text = layout.Summary + ThresholdToTouchdownNote() + FlapsNote(layout) + PowerNote(layout);

            // ── Los ejes, ANTES de las marcas: las bandas de la pista y las líneas del umbral y del
            //    toque se anclan en los mínimos de cada eje, que se fijan aquí.
            ConfigureAxes(chart, layout);

            // Las decoraciones del closeup (bandas de la zona de toma, pista, umbral y toque) van solo
            // en las tres áreas de siempre. En las de flaps y N1 repetirían la misma columna del eje X
            // —el umbral cae en el mismo sitio en todas— a cambio de **seis series y cuatro bandas**
            // más que el motor de gráficos tiene que componer en cada repintado: medido, con ellas el
            // volcado a PNG de cinco áreas pasaba de 1 s a más de un minuto y salía en blanco.
            foreach (var area in chart.ChartAreas)
                if (area.Name == "alt" || area.Name == "spd" || area.Name == "ptc")
                    AddCloseupDecorations(chart, area, layout);

            // ── Las series ────────────────────────────────────────────────────────
            // La altitud: el **radioaltímetro manda** cuando existe (es el instrumento del flare) y
            // el AGL se pinta detrás, más apagado, para poder comparar los dos. Sin radioaltímetro,
            // el AGL es la única altitud y va con su color de siempre.
            bool radar = layout.AltitudeSource == FlareAltitudeSource.RadarAltimeter;
            var altSeries = NewSeries(radar ? "Radar altimeter (ft)" : "AGL (ft)", radar ? RadarColor : AltitudeColor, "alt");

            Series aglSeries = null;
            if (radar)
            {
                aglSeries = NewSeries("AGL (ft)", AltitudeColor, "alt");
                chart.Series.Add(aglSeries);
            }
            chart.Series.Add(altSeries);

            var speedSeries = NewSeries("IAS (kt)", SpeedColor, "spd");
            chart.Series.Add(speedSeries);

            Series pitchSeries = null;
            if (layout.HasPitch)
            {
                pitchSeries = NewSeries("Pitch (°)", PitchColor, "ptc");
                chart.Series.Add(pitchSeries);
            }

            var flapSeries = layout.HasFlaps ? NewSeries("Flaps (%)", FlapColor, "flp") : null;
            if (flapSeries != null) chart.Series.Add(flapSeries);

            var powerSeries = layout.HasPower ? NewSeries("N1 (%)", PowerColor, "n1") : null;
            if (powerSeries != null) chart.Series.Add(powerSeries);

            foreach (var s in _samples)
            {
                if (s == null) continue;
                double x = s.DistFt;

                // **La altitud se corta en el toque.** En tierra `CurrentAGL` devuelve 0 por
                // definición, así que seguir pintándola dibujaba una caída vertical de 50 ft a 0 y
                // una raya pegada al suelo durante toda la frenada —visto en el PNG—: no es la
                // maniobra, es el convenio del dato. La IAS y el pitch sí siguen en tierra, que ahí
                // sí dicen algo (rueda y morro bajando).
                if (!s.OnGround)
                {
                    if (radar)
                    {
                        if (s.RadarAltFt.HasValue) altSeries.Points.AddXY(x, s.RadarAltFt.Value);
                        if (s.AglFt.HasValue)      aglSeries.Points.AddXY(x, s.AglFt.Value);
                    }
                    else if (s.AglFt.HasValue)
                    {
                        altSeries.Points.AddXY(x, s.AglFt.Value);
                    }
                }

                if (s.IasKt.HasValue)    speedSeries.Points.AddXY(x, s.IasKt.Value);
                if (pitchSeries != null && s.PitchDeg.HasValue) pitchSeries.Points.AddXY(x, s.PitchDeg.Value);

                // Los flaps y el N1 sí siguen en tierra: en la frenada el mando se retrae y el motor
                // se va a ralentí, y eso es parte de lo que se viene a mirar.
                if (flapSeries != null && s.FlapsPct.HasValue) flapSeries.Points.AddXY(x, s.FlapsPct.Value);
                if (powerSeries != null)
                {
                    double? p = MeanPower(s);
                    if (p.HasValue) powerSeries.Points.AddXY(x, p.Value);
                }
            }

            // ── La marca del corte de potencia ────────────────────────────────────
            // Una **línea vertical** en el instante que decide `PowerCut`, dentro del área de N1: cae
            // sobre la curva porque la curva es la que manda en ese eje, y el valor exacto está en el
            // resumen del panel y en la leyenda. Se dibuja como `StripLine` y **no** como una serie de
            // puntos: una serie `Point` con marcador en este motor de gráficos entra en el
            // posicionamiento de etiquetas y el repintado se dispara —medido: el volcado a PNG pasaba
            // de 1 s a más de un minuto y la imagen salía en blanco—.
            if (layout.HasPower && layout.Power != null && layout.Power.HasValue)
            {
                chart.ChartAreas["n1"].AxisX.StripLines.Add(new StripLine
                {
                    Interval           = 0,
                    IntervalOffset     = layout.Power.CutDistFt,
                    IntervalOffsetType = DateTimeIntervalType.Number,
                    StripWidth         = 0,
                    StripWidthType     = DateTimeIntervalType.Number,
                    BorderColor        = PowerCutColor,
                    BorderWidth        = 2,
                    Text               = "PWR CUT",
                    // El rótulo va horizontal y no girado: con cinco áreas apiladas el motor lo pone en
                    // vertical cuando no cabe a lo ancho, y en vertical se sale del área y se solapa
                    // con la de al lado —es lo que ya destapó el PNG con las bandas de la TDZ—.
                    TextOrientation    = TextOrientation.Horizontal,
                    TextLineAlignment  = StringAlignment.Near,
                    Font               = new Font("Consolas", 8, FontStyle.Bold),
                    ForeColor          = PowerCutColor,
                });
            }

            // ── El blindaje del pintado ───────────────────────────────────────────
            // Último paso, con todas las series y las bandas ya puestas: deja cada área en un estado
            // explícito. El área de pitch se crea **siempre** (las tres de arriba son fijas) y sin
            // `PitchDeg` en la traza se queda sin ninguna serie — que es el estado en el que el motor
            // de gráficos no puede estimar el intervalo automático y lanza al pintar (ver
            // `ChartAxisSafety`). Aquí pasa a estar marcada como «sin datos».
            ChartAxisSafety.EnsureExplicit(chart);

            // Y los rótulos del eje X, en la última banda que **tiene datos**.
            RelabelBottomArea(chart);

            return chart;
        }

        /// <summary>
        /// **El eje X se rotula en la banda de abajo que tiene datos.** La última área del apilado es
        /// la de pitch, y cuando la traza no lo trae —`approach_track` no lo guarda, y esta traza
        /// hereda de ahí— esa banda se queda sin serie: sus etiquetas **no se pintan** (visto en el
        /// PNG) y el gráfico se quedaba sin la escala de distancia al umbral, que es la que se lee.
        /// Se rotula la última con datos, que en el caso corriente es la de velocidad.
        /// </summary>
        private static void RelabelBottomArea(Chart chart)
        {
            ChartArea target = null;
            foreach (var area in chart.ChartAreas)
            {
                area.AxisX.LabelStyle.Enabled    = false;
                area.AxisX.MajorTickMark.Enabled = false;
                area.AxisX.Title                 = "";
                // El recorrido va de arriba abajo, así que la última que cumpla se queda con el rótulo.
                if (ChartAxisSafety.AreaHasData(chart, area)) target = area;
            }

            if (target == null) return;

            target.AxisX.LabelStyle.Enabled      = true;
            target.AxisX.MajorTickMark.Enabled   = true;
            target.AxisX.LabelStyle.IsEndLabelVisible = true;
            target.AxisX.Title                   = "Distance to threshold (ft)";
        }

        /// <summary>Un área del apilado. El X se configura en <see cref="ConfigureAxes"/>.</summary>
        private static ChartArea MakeArea(string name, double topPct, double bottomPct, string yTitle)
        {
            var area = new ChartArea(name)
            {
                BackColor = Color.FromArgb(20, 30, 42),
                Position  = new ElementPosition(9, (float)topPct, 88, (float)(bottomPct - topPct)),
            };
            StyleAxis(area.AxisX, "", Color.FromArgb(150, 170, 190));
            StyleAxis(area.AxisY, yTitle, Color.FromArgb(150, 170, 190));
            // El eje X va invertido, como en todo el análisis: la izquierda es «antes del umbral».
            area.AxisX.IsReversed = true;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(40, 55, 70);
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(40, 55, 70);
            area.AxisX.LineColor = Color.FromArgb(80, 100, 120);
            area.AxisY.LineColor = Color.FromArgb(80, 100, 120);
            area.AxisY.IsStartedFromZero = false;
            // El eje X es el mismo en todas: se rotula una sola vez, en la de abajo. Las etiquetas van
            // en el área, así que la última necesita sitio —por eso el reparto reserva el 24 % de abajo—.
            area.AxisX.LabelStyle.Enabled     = false;
            area.AxisX.MajorTickMark.Enabled  = false;
            return area;
        }

        /// <summary>
        /// Los rangos y los rótulos de los ejes. El X se rotula en **pies enteros** con
        /// `Helpers/CloseupAxis` (paso «bonito», miles con separador y **THR** clavado en 0); el Y de
        /// cada área sale de lo que hay en la traza, no de una escala fija.
        /// </summary>
        private static void ConfigureAxes(Chart chart, FlareChartLayout layout)
        {
            double xMin = -layout.AfterFt;
            double xMax = layout.BeforeFt;

            foreach (var area in chart.ChartAreas)
            {
                area.AxisX.Minimum = xMin;
                area.AxisX.Maximum = xMax;
                // El eje X en pies enteros lo decide `Helpers/CloseupAxis`: el mismo helper que usa el
                // closeup del perfil vertical, así los dos gráficos numeran igual.
                CloseupAxis.ApplyTo(area.AxisX, layout.BeforeFt, layout.AfterFt);
                // El X no se rotula aquí: qué banda lleva los rótulos se decide **después** de las
                // series, cuando ya se sabe cuál es la última con datos. Ver `RelabelBottomArea`.
                area.AxisX.LabelStyle.Enabled    = false;
                area.AxisX.MajorTickMark.Enabled = false;
            }

            // Etiquetas **horizontales** y sin giro: con el rótulo `1.500` y el paso del eje, el motor
            // las inclinaba 45° y las fusionaba en una maraña ilegible (visto en el PNG). El ángulo se
            // fija a 0: si no caben, es que el paso elegido no vale, y de eso ya se encarga la cuenta
            // de rótulos de `CloseupAxis.MaxLabels`.
            foreach (var area in chart.ChartAreas)
            {
                area.AxisX.LabelStyle.Angle = 0;
                area.AxisX.LabelStyle.IsEndLabelVisible = true;
            }

            chart.ChartAreas["alt"].AxisY.Minimum = FloorFt;
            chart.ChartAreas["alt"].AxisY.Maximum = layout.AltitudeTopFt;

            // El eje de velocidad se aprieta a lo que se ve: en el flare la IAS se mueve 10–15 kt.
            // **Solo si hay IAS**: sin dato, el eje se queda en automático —fijarlo en un rango
            // inventado sería dibujar una escala que la traza no sostiene— y, sobre todo, **nunca** se
            // deja con el mínimo igual al máximo, que es lo que impide al motor pintar el área.
            if (layout.HasSpeed)
            {
                chart.ChartAreas["spd"].AxisY.Minimum = layout.SpeedFloorKt;
                chart.ChartAreas["spd"].AxisY.Maximum = layout.SpeedTopKt;
            }

            if (layout.HasPitch)
            {
                chart.ChartAreas["ptc"].AxisY.Minimum = layout.PitchFloorDeg;
                chart.ChartAreas["ptc"].AxisY.Maximum = layout.PitchTopDeg;
            }

            // El de flaps va de 0 a 100 siempre (la escala del mando es absoluta); el de N1 se aprieta
            // a lo que se ve, que es lo que deja leer la caída.
            if (layout.HasFlaps)
            {
                chart.ChartAreas["flp"].AxisY.Minimum = layout.FlapFloorPct;
                chart.ChartAreas["flp"].AxisY.Maximum = layout.FlapTopPct;
            }
            if (layout.HasPower)
            {
                chart.ChartAreas["n1"].AxisY.Minimum = layout.PowerFloorPct;
                chart.ChartAreas["n1"].AxisY.Maximum = layout.PowerTopPct;
            }
        }

        // ── Las marcas heredadas del closeup ──────────────────────────────────────

        /// <summary>
        /// Las cuatro marcas del closeup, en una sola área: las bandas de la zona de toma (lo que se
        /// puntúa), la pista, el umbral y el toque. Nada se coloca aquí — si
        /// <see cref="TouchdownCloseup"/> dice que el toque no cabe, no se dibuja.
        /// </summary>
        private static void AddCloseupDecorations(Chart chart, ChartArea area, FlareChartLayout layout)
        {
            var cu = layout.Closeup;
            if (cu == null) return;

            // **Sin eje Y no hay dónde anclar nada.** En un área sin datos —el pitch que esta traza no
            // trae— el eje Y se queda automático (`NaN`) y las marcas verticales nacían con las dos
            // coordenadas no finitas: puntos envenenados que ensuciaban la estimación del motor (y que
            // ahora limpia `ChartAxisSafety`). Se salta el área entera y el área se marca «sin datos».
            if (!ChartAxisSafety.IsUsable(area.AxisY.Minimum, area.AxisY.Maximum)) return;

            double yTop = area.AxisY.Maximum;

            if (cu.ZeroBandFt > 0.0)
                area.AxisX.StripLines.Add(MakeBand(-cu.ZeroBandFt, cu.ZeroBandFt, "0 pts",
                    Color.FromArgb(42, 70, 160, 90), Color.FromArgb(130, 70, 200, 120)));

            if (cu.ThreeBandFt > cu.ZeroBandFt)
                area.AxisX.StripLines.Add(MakeBand(-cu.ThreeBandFt, cu.ThreeBandFt - cu.ZeroBandFt, "3 pts",
                    Color.FromArgb(34, 190, 150, 40), Color.FromArgb(120, 215, 175, 60)));

            // La pista: banda horizontal en el suelo del área. En la de altitud el suelo es AGL 0 y
            // en las demás es su propio mínimo, así que la banda sigue existiendo como referencia del
            // eje X sin inventar una altitud en un eje que no es de altitud.
            if (cu.RunwayVisible)
            {
                var runway = new Series(cu.RunwayLabel + "_" + area.Name)
                {
                    ChartType         = SeriesChartType.Line,
                    Color             = Color.FromArgb(200, 206, 216),
                    BorderWidth       = 6,
                    IsVisibleInLegend = false,
                    ChartArea         = area.Name,
                };
                runway.Points.AddXY(-cu.RunwayVisibleFt, area.AxisY.Minimum);
                runway.Points.AddXY(0.0, area.AxisY.Minimum);
                chart.Series.Add(runway);
            }

            // El umbral y el toque, como **líneas** verticales sin rótulo.
            //
            // En el closeup llevan texto (`THR` / `TD 1 736 ft from threshold`), pero en este gráfico
            // no: el motor de gráficos gira el texto a 90° cuando no cabe, y con las áreas apiladas
            // quedaba ilegible y encima del eje de altitud —visto en el PNG—. El dato no se pierde:
            // está en la línea de resumen de abajo y en la leyenda, y las bandas de la zona de toma
            // siguen etiquetadas sobre el eje X.
            AddVerticalMark(chart, area, "THR", 0.0, area.AxisY.Minimum, yTop,
                            Color.FromArgb(255, 200, 60));

            if (cu.TouchdownInView)
                AddVerticalMark(chart, area, "TD", cu.TouchdownX, area.AxisY.Minimum, yTop,
                                Color.FromArgb(255, 80, 80));
        }

        private static StripLine MakeBand(double offsetFt, double widthFt, string text,
                                         Color back, Color border)
        {
            return new StripLine
            {
                Interval           = 0,
                IntervalOffset     = offsetFt,
                IntervalOffsetType = DateTimeIntervalType.Number,
                StripWidth         = widthFt,
                StripWidthType     = DateTimeIntervalType.Number,
                BackColor          = back,
                BorderColor        = border,
                BorderWidth        = 1,
                Text               = text,
                // El rótulo se centra **en la banda**, no en el borde: sin alineación explícita el
                // motor lo pegaba al borde derecho del área (visto en el PNG) en vez de dejarlo sobre
                // el tramo que describe.
                TextAlignment      = StringAlignment.Center,
                TextLineAlignment  = StringAlignment.Center,
                // El texto de una banda se pinta **girado** por defecto cuando no cabe a lo ancho, y
                // con dos bandas estrechas (1 500 y 3 000 ft) quedaba en vertical e ilegible —lo
                // destapó el PNG—. Horizontal y centrado en la banda.
                TextOrientation    = TextOrientation.Horizontal,
                Font               = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor          = Color.FromArgb(210, 225, 240, 250)
            };
        }

        /// <summary>
        /// Una marca vertical (el umbral o el punto de toque): dos puntos por encima del mínimo del
        /// área. Sin rótulo a propósito — ver el porqué en <see cref="AddCloseupDecorations"/>—, y por
        /// eso son **dos** puntos y no tres: no hay texto que anclar en un punto intermedio.
        /// </summary>
        private static void AddVerticalMark(Chart chart, ChartArea area, string name, double x,
                                            double yBottom, double yTop, Color color)
        {
            var s = new Series(name + "_" + area.Name)
            {
                ChartType         = SeriesChartType.Line,
                Color             = color,
                BorderWidth       = 1,
                BorderDashStyle   = ChartDashStyle.Dash,
                IsVisibleInLegend = false,
                ChartArea         = area.Name,
            };
            s.SmartLabelStyle.Enabled = false;
            s.Points.AddXY(x, yBottom);
            s.Points.AddXY(x, yTop);

            chart.Series.Add(s);
        }

        /// <summary>`null` no es un número: solo lo finito entra en la media del eje.</summary>
        private static bool Finite(double? value)
            => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);

        /// <summary>
        /// La potencia de una muestra: la **media de los motores que publican dato**, el mismo cálculo
        /// que usa `PowerCut` y que el eje del gráfico. Si aquí se pintara una sola serie, la marca del
        /// corte caería fuera de la curva.
        /// </summary>
        private static double? MeanPower(FlareTrackPoint s)
        {
            double sum = 0.0; int n = 0;
            if (Finite(s.Eng1Pct)) { sum += s.Eng1Pct.Value; n++; }
            if (Finite(s.Eng2Pct)) { sum += s.Eng2Pct.Value; n++; }
            return n == 0 ? (double?)null : sum / n;
        }

        /// <summary>
        /// El tiempo umbral→toma de esta captura, para el resumen del panel. Cadena vacía sin dato:
        /// la traza de 2 s no se publica (`CoarseTrack`) y esta ventana **solo** recibe traza fina, así
        /// que el dato sale del mismo helper que el resto del aterrizaje.
        /// </summary>
        private string ThresholdToTouchdownNote()
        {
            var result = ThresholdToTouchdown.Compute(_samples);
            return result.HasValue ? "  ·  " + L._("Landing_ThrToTd", result.Seconds) : "";
        }

        /// <summary>
        /// **Los flaps del aterrizaje**, en el resumen: el ajuste en el umbral, el de la toma y, si
        /// cambió, el aviso —que es lo que explica un flotado largo—. Todo el texto sale de
        /// `FlapTrackSummary`, que es el mismo helper que lo publica en el logbook; con la familia
        /// desconocida enseña el **porcentaje**, nunca una compuerta inventada.
        /// </summary>
        private string FlapsNote(FlareChartLayout layout)
        {
            var flaps = layout.Flaps;
            if (flaps == null || !flaps.HasTrack || flaps.AtThreshold == null) return "";

            string text = "  ·  " + L._("Landing_FlapsHeader") + " " + flaps.AtThreshold.Text;

            if (flaps.AtTouchdown != null && flaps.Changed)
                text += "  " + L._("Landing_FlapsChanged", flaps.AtThreshold.ShortText,
                                    flaps.AtTouchdown.ShortText);

            return text;
        }

        /// <summary>
        /// **El corte de potencia**, en el resumen: los segundos antes de la toma, o el motivo por el
        /// que no se publica. Sin dato no se enseña un cero.
        /// </summary>
        private string PowerNote(FlareChartLayout layout)
        {
            var power = layout.Power;
            if (power == null || !power.HasValue) return "";

            return "  ·  " + L._(power.EngineCount == 2 ? "Landing_PowerCut" : "Landing_PowerCutEngine1",
                                 power.SecondsBeforeTouchdown);
        }

        private static Series NewSeries(string name, Color color, string areaName)
        {
            return new Series(name)
            {
                ChartType         = SeriesChartType.Line,
                Color             = color,
                BorderWidth       = 2,
                IsVisibleInLegend = true,
                ChartArea         = areaName,
                XValueType        = ChartValueType.Double,
            };
        }

        private static void StyleAxis(Axis axis, string title, Color labelColor)
        {
            axis.Title                   = title;
            axis.TitleFont               = new Font("Consolas", 8);
            axis.TitleForeColor          = labelColor;
            axis.LabelStyle.Font         = new Font("Consolas", 8);
            axis.LabelStyle.ForeColor    = labelColor;
            axis.MajorTickMark.LineColor = labelColor;
        }
    }
}
