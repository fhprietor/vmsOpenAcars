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
        private Label[] _n2Labels;
        private Label[] _egtLabels;
        private Label[] _ffLabels;

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
                            map: isEng1 ? data.Map_1 : data.Map_2,
                            cht: isEng1 ? data.Cht_1 : data.Cht_2,
                            oilTemp: isEng1 ? data.OilTemp_1 : data.OilTemp_2,
                            oilPress: isEng1 ? data.OilPress_1 : data.OilPress_2,
                            ff: isEng1 ? data.FuelFlow_1 : data.FuelFlow_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;

                    case FsuipcService.AircraftCategory.Turboprop:
                        SetTurbopropRow(i,
                            torqPct: isEng1 ? data.TorquePct_1 : data.TorquePct_2,
                            itt: isEng1 ? data.EGT_1 : data.EGT_2,
                            propRpm: isEng1 ? data.PropRpm_1 : data.PropRpm_2,
                            n1: isEng1 ? data.N1_1 : data.N1_2,
                            ff: isEng1 ? data.FuelFlow_1 : data.FuelFlow_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;

                    default: // Jet / Unknown
                        SetJetRow(i,
                            n1: isEng1 ? data.N1_1 : data.N1_2,
                            n2: isEng1 ? data.N2_1 : data.N2_2,
                            egt: isEng1 ? data.EGT_1 : data.EGT_2,
                            ff: isEng1 ? data.FuelFlow_1 : data.FuelFlow_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;
                }
            }
        }

        // ── Mantener SetEngineParameters para compatibilidad con código existente ──
        [Obsolete("Use UpdateEngines(RawTelemetryData) instead.")]
        public void SetEngineParameters(int engine, float n1, float n2, float egt, float fuelFlow)
        {
            if (engine >= _engineCount) return;
            if (_n1Labels != null && engine < _n1Labels.Length) _n1Labels[engine].Text = $"N1: {n1:F0}%";
            if (_n2Labels != null && engine < _n2Labels.Length) _n2Labels[engine].Text = $"N2: {n2:F0}%";
            if (_egtLabels != null && engine < _egtLabels.Length) _egtLabels[engine].Text = $"EGT: {egt:F0}°C";
            if (_ffLabels != null && engine < _ffLabels.Length) _ffLabels[engine].Text = $"FF: {fuelFlow:F0} kg/h";
        }

        private void SetJetRow(int eng, float n1, float n2, float egt, float ff, bool running)
        {
            var c = running ? Color.LimeGreen : Color.Gray;
            var ct = running ? Color.Orange : Color.Gray;
            var cf = running ? Color.Yellow : Color.Gray;
            if (_n1Labels != null && eng < _n1Labels.Length) { _n1Labels[eng].Text = $"N1:  {n1:F1}%"; _n1Labels[eng].ForeColor = c; }
            if (_n2Labels != null && eng < _n2Labels.Length) { _n2Labels[eng].Text = $"N2:  {n2:F1}%"; _n2Labels[eng].ForeColor = c; }
            if (_egtLabels != null && eng < _egtLabels.Length) { _egtLabels[eng].Text = $"EGT: {egt:F0}°C"; _egtLabels[eng].ForeColor = ct; }
            if (_ffLabels != null && eng < _ffLabels.Length) { _ffLabels[eng].Text = $"FF:  {ff:F0} kg/h"; _ffLabels[eng].ForeColor = cf; }
            if (_engLabels != null && eng < _engLabels.Length) _engLabels[eng].ForeColor = running ? Color.Cyan : Color.DimGray;
        }

        private void SetTurbopropRow(int eng, float torqPct, float itt, float propRpm, float n1, float ff, bool running)
        {
            var c = running ? Color.Cyan : Color.Gray;
            var ct = running ? Color.Orange : Color.Gray;
            var cf = running ? Color.Yellow : Color.Gray;
            if (_n1Labels != null && eng < _n1Labels.Length) { _n1Labels[eng].Text = $"TRQ: {torqPct:F1}%"; _n1Labels[eng].ForeColor = c; }
            if (_n2Labels != null && eng < _n2Labels.Length) { _n2Labels[eng].Text = $"N1:  {n1:F1}%"; _n2Labels[eng].ForeColor = c; }
            if (_egtLabels != null && eng < _egtLabels.Length) { _egtLabels[eng].Text = $"ITT: {itt:F0}°C"; _egtLabels[eng].ForeColor = ct; }
            if (_ffLabels != null && eng < _ffLabels.Length) { _ffLabels[eng].Text = $"PROP {propRpm:F0} RPM  FF {ff:F0} kg/h"; _ffLabels[eng].ForeColor = cf; }
            if (_engLabels != null && eng < _engLabels.Length) _engLabels[eng].ForeColor = running ? Color.Cyan : Color.DimGray;
        }

        private void SetPistonRow(int eng, float rpm, float map, float cht, float oilTemp, float oilPress, float ff, bool running)
        {
            var c = running ? Color.Yellow : Color.Gray;
            var ct = running ? Color.OrangeRed : Color.Gray;
            var cf = running ? Color.GreenYellow : Color.Gray;
            if (_n1Labels != null && eng < _n1Labels.Length) { _n1Labels[eng].Text = $"RPM: {rpm:F0}"; _n1Labels[eng].ForeColor = c; }
            if (_n2Labels != null && eng < _n2Labels.Length) { _n2Labels[eng].Text = $"MAP: {map:F1}\" Hg"; _n2Labels[eng].ForeColor = c; }
            if (_egtLabels != null && eng < _egtLabels.Length) { _egtLabels[eng].Text = $"CHT: {cht:F0}°C"; _egtLabels[eng].ForeColor = ct; }
            if (_ffLabels != null && eng < _ffLabels.Length) { _ffLabels[eng].Text = $"OIL {oilTemp:F0}°/{oilPress:F0}psi  FF {ff:F1}"; _ffLabels[eng].ForeColor = cf; }
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
            _n2Labels = new Label[_engineCount];
            _egtLabels = new Label[_engineCount];
            _ffLabels = new Label[_engineCount];

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

                // N2
                _n2Labels[i] = new Label
                {
                    Text = "N2: 0%",
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.LightGreen,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_n2Labels[i], 0, 1);

                // EGT
                _egtLabels[i] = new Label
                {
                    Text = "EGT: 0°C",
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.Orange,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_egtLabels[i], 0, 2);

                // Fuel Flow
                _ffLabels[i] = new Label
                {
                    Text = "FF: 0 kg/h",
                    Font = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.Yellow,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_ffLabels[i], 0, 3);

                enginePanel.Controls.Add(dataPanel);
                layout.Controls.Add(enginePanel, i, 0);
            }

            this.Controls.Add(layout);
        }
    }
}