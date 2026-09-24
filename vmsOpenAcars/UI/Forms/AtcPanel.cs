using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    /// <summary>
    /// Panel lateral persistente con todas las posiciones ATC activas en IVAO y su ATIS
    /// completo.
    ///
    /// Complementa las formas geográficas del mapa: aquellas indican *dónde* está cada
    /// posición a 20 NM, este panel indica *qué* hay activo y con qué frecuencia, y es el
    /// único sitio donde el texto completo del ATIS es legible sin pasar el ratón por
    /// encima de cada marcador.
    ///
    /// Es de solo lectura y no guarda estado propio: se repuebla con cada poll de IVAO.
    /// </summary>
    internal sealed class AtcPanel : Panel
    {
        private readonly RichTextBox _content;
        private readonly Label       _lblHeader;
        private readonly Label       _lblCount;

        private static readonly Color BgColor      = Color.FromArgb(18, 24, 30);
        private static readonly Color HeaderColor  = Color.FromArgb(0, 180, 255);
        private static readonly Color DimColor     = Color.FromArgb(130, 150, 165);
        private static readonly Color LocalColor   = Color.FromArgb(255, 210, 90);
        private static readonly Color AreaColor    = Color.FromArgb(140, 220, 255);
        private static readonly Color AtisColor    = Color.FromArgb(180, 230, 185);

        internal const int PanelWidth = 320;

        internal AtcPanel()
        {
            Dock      = DockStyle.Right;
            Width     = PanelWidth;
            BackColor = BgColor;
            Visible   = false;

            _lblHeader = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 24,
                Text      = "  ATC / ATIS",
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Consolas", 9, FontStyle.Bold),
                ForeColor = HeaderColor,
                BackColor = Color.FromArgb(12, 18, 24),
            };

            _lblCount = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 20,
                Text      = "  sin datos",
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Consolas", 7.5f),
                ForeColor = DimColor,
                BackColor = BgColor,
            };

            _content = new RichTextBox
            {
                Dock       = DockStyle.Fill,
                ReadOnly   = true,
                BorderStyle = BorderStyle.None,
                BackColor  = BgColor,
                ForeColor  = Color.White,
                Font       = new Font("Consolas", 8.5f),
                WordWrap   = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
            };

            // Orden de docking: Fill antes que Top para que los encabezados queden arriba.
            Controls.Add(_content);
            Controls.Add(_lblCount);
            Controls.Add(_lblHeader);
        }

        /// <summary>
        /// Repuebla el panel. Thread-safe: el poll de IVAO entrega desde el thread-pool.
        /// </summary>
        internal void SetStations(IList<IvaoAtcStation> stations)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetStations(stations))); return; }

            _content.Clear();

            if (stations == null || stations.Count == 0)
            {
                _lblCount.Text = "  sin posiciones activas";
                _content.SelectionColor = DimColor;
                _content.AppendText(
                    "\n  No hay posiciones ATC activas en IVAO para la ruta.\n\n" +
                    "  Las posiciones aparecen aquí cuando el controlador\n" +
                    "  se conecta a la red y su aeropuerto entra en el\n" +
                    "  radio de la ruta activa.\n");
                return;
            }

            _lblCount.Text = $"  {stations.Count} posición(es) — {DateTime.Now:HH:mm:ss}";

            // Orden: por aeropuerto, y dentro de cada uno las dependencias locales primero
            // (más útiles en rodaje y despegue) y luego las de área. La regla vive en
            // Helpers.AtcStationOrder para poder probarla sin WinForms.
            var ordered = AtcStationOrder.Sort(stations);

            string currentIcao = null;
            foreach (var st in ordered)
            {
                if (!string.Equals(currentIcao, st.Icao, StringComparison.OrdinalIgnoreCase))
                {
                    currentIcao = st.Icao;
                    _content.SelectionColor = HeaderColor;
                    _content.SelectionFont  = new Font("Consolas", 9f, FontStyle.Bold);
                    _content.AppendText($"\n  {currentIcao ?? "----"}\n");
                }

                bool isAtis = string.Equals(st.Position, "ATIS", StringComparison.OrdinalIgnoreCase);
                _content.SelectionFont = new Font("Consolas", 8.5f, isAtis ? FontStyle.Regular : FontStyle.Bold);
                _content.SelectionColor = isAtis ? DimColor : PositionColor(st.Position);

                string freq = st.Frequency > 0 ? $"{st.Frequency:F3}" : "  ---  ";
                _content.AppendText($"  {st.Position,-4} {freq}\n");
                _content.SelectionFont = new Font("Consolas", 8f, FontStyle.Regular);
                _content.SelectionColor = DimColor;
                _content.AppendText($"       {st.Callsign}\n");

                // ATIS completo: cada línea del informe en su propio renglón, sangrado.
                if (!string.IsNullOrWhiteSpace(st.AtisText))
                {
                    _content.SelectionColor = AtisColor;
                    foreach (var line in SplitAtis(st.AtisText))
                        _content.AppendText($"       {line}\n");
                }
            }

            _content.SelectionStart = 0;
            _content.ScrollToCaret();
        }

        /// <summary>
        /// Parte el texto del ATIS en líneas legibles. Los informes suelen venir con
        /// varios elementos separados por saltos de línea; los que llegan en una sola
        /// línea se dejan tal cual para no inventar una segmentación.
        /// </summary>
        private static IEnumerable<string> SplitAtis(string atis)
        {
            var parts = atis
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);

            return parts;
        }

        private static Color PositionColor(string position)
        {
            switch ((position ?? "").ToUpperInvariant())
            {
                case "TWR": return Color.FromArgb(255, 120, 120);
                case "GND": return Color.FromArgb(255, 210, 90);
                case "DEL": return Color.FromArgb(255, 160, 60);
                default:    return AreaColor;
            }
        }
    }
}
