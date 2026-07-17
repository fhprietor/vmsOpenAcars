using System;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Controls
{
    public class EngineMonitorPanel : Panel
    {
        private Label[] _engLabels;
        private Label[] _n1Labels;
        private Label[] _idleLabels;
        private Label[] _oilLabels;
        private Label   _reverserLabel;

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

        public void UpdateEngines(RawTelemetryData data)
        {
            if (data == null) return;

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
                            rpm:     isEng1 ? data.Rpm_1     : data.Rpm_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;

                    case FsuipcService.AircraftCategory.Turboprop:
                        SetTurbopropRow(i,
                            torqPct: isEng1 ? data.TorquePct_1 : data.TorquePct_2,
                            running: isEng1 ? data.Eng1Running  : data.Eng2Running);
                        break;

                    default:
                        SetJetRow(i,
                            n1:      isEng1 ? data.N1_1       : data.N1_2,
                            running: isEng1 ? data.Eng1Running : data.Eng2Running);
                        break;
                }
            }
        }

        public void UpdateLifecycle(EngineLifecycleSnapshot snap)
        {
            UpdateIdleLabel(0, snap.Eng1Running, snap.Eng1Stabilized, snap.Eng1IdleTime, snap.RequiredIdleSeconds);
            if (_engineCount >= 2)
                UpdateIdleLabel(1, snap.Eng2Running, snap.Eng2Stabilized, snap.Eng2IdleTime, snap.RequiredIdleSeconds);

            UpdateOilLabel(0, snap.Eng1Running, snap.OilTemp1, snap.OilPress1);
            if (_engineCount >= 2)
                UpdateOilLabel(1, snap.Eng2Running, snap.OilTemp2, snap.OilPress2);

            if (_reverserLabel == null) return;
            if (snap.ReversersUsed)
            {
                int remaining = Math.Max(0, snap.CooldownSecondsRequired - snap.CooldownSecondsElapsed);
                if (remaining > 0)
                {
                    _reverserLabel.Text      = $"REV COOL-DOWN  {remaining}s";
                    _reverserLabel.ForeColor = Color.Orange;
                }
                else
                {
                    _reverserLabel.Text      = $"REV OK  {snap.MaxEng1RevPct:F0}%/{snap.MaxEng2RevPct:F0}%";
                    _reverserLabel.ForeColor = Color.LimeGreen;
                }
            }
            else if (snap.CooldownSecondsElapsed > 0)
            {
                _reverserLabel.Text      = snap.ReverserDataAvailable ? "REV —" : "REV N/D";
                _reverserLabel.ForeColor = snap.ReverserDataAvailable ? Color.DimGray : Color.DarkOrange;
            }
            else
            {
                _reverserLabel.Text      = "";
                _reverserLabel.ForeColor = Color.DimGray;
            }
        }

        private void UpdateIdleLabel(int eng, bool running, bool stabilized, TimeSpan idleTime, int requiredSec)
        {
            if (_idleLabels == null || eng >= _idleLabels.Length || _idleLabels[eng] == null) return;
            var lbl = _idleLabels[eng];

            if (!running)
            {
                lbl.Text      = "";
                lbl.ForeColor = Color.DimGray;
                return;
            }

            if (idleTime == TimeSpan.Zero)
            {
                // Engine was already running when monitoring started — show stabilisation only.
                lbl.Text      = stabilized ? "STAB ✓" : "CALENTANDO...";
                lbl.ForeColor = stabilized ? Color.LimeGreen : Color.Yellow;
                return;
            }

            string timeStr = idleTime.TotalMinutes >= 1
                ? $"{(int)idleTime.TotalMinutes}m{idleTime.Seconds:D2}s"
                : $"{(int)idleTime.TotalSeconds}s";

            string target = requiredSec >= 60 ? $"{requiredSec / 60}m" : $"{requiredSec}s";
            bool metTime  = idleTime.TotalSeconds >= requiredSec;

            if (stabilized && metTime)
            {
                lbl.Text      = $"IDLE {timeStr}/{target} ✓";
                lbl.ForeColor = Color.LimeGreen;
            }
            else if (metTime)
            {
                // Time met but oil/N2 not yet at threshold.
                lbl.Text      = $"IDLE {timeStr}/{target}";
                lbl.ForeColor = Color.Yellow;
            }
            else
            {
                lbl.Text      = $"IDLE {timeStr}/{target}";
                lbl.ForeColor = Color.OrangeRed;
            }
        }

        // Oil temperature thresholds (°C): below MIN = not in green range, above WARM = fully warmed.
        private const float OIL_TEMP_MIN  = 40f;
        private const float OIL_TEMP_WARM = 70f;

        private void UpdateOilLabel(int eng, bool running, float oilTemp, float oilPress)
        {
            if (_oilLabels == null || eng >= _oilLabels.Length || _oilLabels[eng] == null) return;
            var lbl = _oilLabels[eng];

            if (!running || (oilTemp <= 0f && oilPress <= 0f))
            {
                lbl.Text      = "";
                lbl.ForeColor = Color.DimGray;
                return;
            }

            lbl.Text = $"OIL {oilTemp:F0}°C  {oilPress:F0}PSI";

            if (oilTemp < OIL_TEMP_MIN)
                lbl.ForeColor = Color.OrangeRed;
            else if (oilTemp < OIL_TEMP_WARM)
                lbl.ForeColor = Color.Yellow;
            else
                lbl.ForeColor = Color.LimeGreen;
        }

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
            if (_n1Labels != null && eng < _n1Labels.Length)
            {
                _n1Labels[eng].Text      = $"TRQ: {torqPct:F1}%";
                _n1Labels[eng].ForeColor = running ? Color.Cyan : Color.Gray;
            }
            if (_engLabels != null && eng < _engLabels.Length)
                _engLabels[eng].ForeColor = running ? Color.Cyan : Color.DimGray;
        }

        private void SetPistonRow(int eng, float rpm, bool running)
        {
            if (_n1Labels != null && eng < _n1Labels.Length)
            {
                _n1Labels[eng].Text      = $"RPM: {rpm:F0}";
                _n1Labels[eng].ForeColor = running ? Color.Yellow : Color.Gray;
            }
            if (_engLabels != null && eng < _engLabels.Length)
                _engLabels[eng].ForeColor = running ? Color.Yellow : Color.DimGray;
        }

        public EngineMonitorPanel()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(15, 20, 30);
            this.Padding   = new Padding(5);
            this.Size      = new Size(400, 145);
            RebuildLayout();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        private void RebuildLayout()
        {
            this.Controls.Clear();

            _engLabels  = new Label[_engineCount];
            _n1Labels   = new Label[_engineCount];
            _idleLabels = new Label[_engineCount];
            _oilLabels  = new Label[_engineCount];

            // Reverser status bar — docked to bottom so Fill layout leaves room for it.
            _reverserLabel = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 16,
                Font      = new Font("Consolas", 7.5f, FontStyle.Regular),
                ForeColor = Color.DimGray,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Text      = ""
            };
            this.Controls.Add(_reverserLabel);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = _engineCount,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(2),
                Margin      = new Padding(0)
            };

            for (int i = 0; i < _engineCount; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _engineCount));

            for (int i = 0; i < _engineCount; i++)
            {
                var enginePanel = new Panel
                {
                    Dock        = DockStyle.Fill,
                    BackColor   = Color.FromArgb(20, 25, 38),
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin      = new Padding(2),
                    Padding     = new Padding(5)
                };

                var lblEngine = new Label
                {
                    Text      = $"ENGINE {i + 1}",
                    Font      = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.Cyan,
                    Dock      = DockStyle.Top,
                    Height    = 20,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                enginePanel.Controls.Add(lblEngine);
                _engLabels[i] = lblEngine;

                var dataPanel = new TableLayoutPanel
                {
                    Dock        = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount    = 3,
                    BackColor   = Color.Transparent,
                    Padding     = new Padding(5),
                    Margin      = new Padding(0)
                };

                // Row 0 (40%): N1/RPM/Torque
                // Row 1 (35%): Idle time + stabilisation status
                // Row 2 (25%): Oil temperature + pressure
                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
                dataPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

                _n1Labels[i] = new Label
                {
                    Text      = "N1: 0%",
                    Font      = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.LightGreen,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_n1Labels[i], 0, 0);

                _idleLabels[i] = new Label
                {
                    Text      = "",
                    Font      = new Font("Consolas", 7.5f, FontStyle.Regular),
                    ForeColor = Color.DimGray,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_idleLabels[i], 0, 1);

                _oilLabels[i] = new Label
                {
                    Text      = "",
                    Font      = new Font("Consolas", 7.5f, FontStyle.Regular),
                    ForeColor = Color.DimGray,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                dataPanel.Controls.Add(_oilLabels[i], 0, 2);

                enginePanel.Controls.Add(dataPanel);
                layout.Controls.Add(enginePanel, i, 0);
            }

            this.Controls.Add(layout);
        }
    }
}
