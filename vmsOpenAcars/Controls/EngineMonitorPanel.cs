using System;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Controls
{
    public class EngineMonitorPanel : Panel
    {
        private Label[] _engLabels;
        private Label[] _n1Labels;

        private int _engineCount = 2;

        public int EngineCount
        {
            get => _engineCount;
            set
            {
                _engineCount = Math.Max(1, Math.Min(4, value));
                RebuildLayout();
            }
        }

        private FsuipcService.AircraftCategory _lastCategory = FsuipcService.AircraftCategory.Unknown;

        /// <summary>
        /// Actualiza los valores del panel adaptando el layout y las etiquetas
        /// según la categoría de planta motriz detectada en tiempo de vuelo.
        /// Llamar en cada ciclo de UI desde MainForm.
        /// </summary>
        public void UpdateEngines(RawTelemetryData data)
        {
            if (data == null) return;

            // Reconstruir layout si la categoría cambió
            if (data.EngineCategory != _lastCategory)
            {
                _lastCategory = data.EngineCategory;
                RebuildLayout();
            }

            for (int i = 0; i < _engineCount; i++)
            {
                bool isEng1 = (i == 0);
                switch (data.EngineCategory)
                {
                    case FsuipcService.AircraftCategory.Piston:
                        SetPistonRow(i,
                            rpm: isEng1 ? data.Rpm_1 : data.Rpm_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;

                    case FsuipcService.AircraftCategory.Turboprop:
                        SetTurbopropRow(i,
                            torqPct: isEng1 ? data.TorquePct_1 : data.TorquePct_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;

                    default: // Jet / Unknown
                        SetJetRow(i,
                            n1: isEng1 ? data.N1_1 : data.N1_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;
                }
            }
        }

        // ── Mantener SetEngineParameters para compatibilidad con código existente ──
        [Obsolete("Use UpdateEngines(RawTelemetryData) instead.")]
        public void SetEngineParameters(int engine, float n1)
        {
            if (engine >= _engineCount) return;
            if (_n1Labels != null && engine < _n1Labels.Length) _n1Labels[engine].Text = $"N1: {n1:F0}%";
        }

        private void SetJetRow(int eng, float n1, bool running)
        {
            var c = running ? Color.LimeGreen : Color.Gray;
            if (_n1Labels != null && eng < _n1Labels.Length)
                _n1Labels[eng].Text = $"N1: {n1:F1}%";
            _n1Labels[eng].ForeColor = c;
            if (_engLabels != null && eng < _engLabels.Length)
                _engLabels[eng].ForeColor = running ? Color.Cyan : Color.DimGray;
        }

        private void SetTurbopropRow(int eng, float torqPct, bool running)
        {
            var c = running ? Color.Cyan : Color.Gray;
            var ct = running ? Color.Orange : Color.Gray;
            var cf = running ? Color.Yellow : Color.Gray;
            if (_n1Labels != null && eng < _n1Labels.Length) { _n1Labels[eng].Text = $"TRQ: {torqPct:F1}%"; _n1Labels[eng].ForeColor = c; }
            if (_engLabels != null && eng < _engLabels.Length) _engLabels[eng].ForeColor = running ? Color.Cyan : Color.DimGray;
        }

        private void SetPistonRow(int eng, float rpm, bool running)
        {
            var c = running ? Color.Yellow : Color.Gray;
            var ct = running ? Color.OrangeRed : Color.Gray;
            var cf = running ? Color.GreenYellow : Color.Gray;
            if (_n1Labels != null && eng < _n1Labels.Length) { _n1Labels[eng].Text = $"RPM: {rpm:F0}"; _n1Labels[eng].ForeColor = c; }
            if (_engLabels != null && eng < _engLabels.Length) _engLabels[eng].ForeColor = running ? Color.Yellow : Color.DimGray;
        }

        public EngineMonitorPanel()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(15, 20, 30);
            this.Padding = new Padding(5);
            this.Size = new Size(400, 100);
            RebuildLayout();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private void RebuildLayout()
        {
            // Limpiar controles existentes
            this.Controls.Clear();

            _engLabels = new Label[_engineCount];
            _n1Labels = new Label[_engineCount];

            // Layout horizontal con TableLayoutPanel
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = _engineCount,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(2),
                Margin = new Padding(0)
            };

            for (int i = 0; i < _engineCount; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _engineCount));
            }

            for (int i = 0; i < _engineCount; i++)
            {
                var enginePanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(20, 25, 38),
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(2),
                    Padding = new Padding(5)
                };

                // Título del motor
                var lblEngine = new Label
                {
                    Text = $"ENGINE {i + 1}",
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.Cyan,
                    Dock = DockStyle.Top,
                    Height = 20,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                enginePanel.Controls.Add(lblEngine);
                _engLabels[i] = lblEngine;

                // Layout vertical para los datos
                var dataPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    BackColor = Color.Transparent,
                    Padding = new Padding(5),
                    Margin = new Padding(0)
                };

                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

                // N1
                _n1Labels[i] = new Label
                {
                    Text = "N1: 0%",
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.LightGreen,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_n1Labels[i], 0, 0);

                enginePanel.Controls.Add(dataPanel);
                layout.Controls.Add(enginePanel, i, 0);
            }

            this.Controls.Add(layout);
        }
    }
}