using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.UI.Forms
{
    public class MetarDecodeForm : Form
    {
        private readonly MetarData _data;

        public MetarDecodeForm(MetarData data)
        {
            _data = data;
            Build();
        }

        private void Build()
        {
            Text            = $"METAR — {_data?.FetchedIcao ?? "N/A"}";
            Size            = new Size(480, 380);
            BackColor       = Color.FromArgb(15, 15, 25);
            ForeColor       = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = new Font("Consolas", 9.5f);

            if (_data == null)
            {
                Controls.Add(MakeLbl("Sin datos", Color.Gray));
                return;
            }

            var rawLabel = new Label
            {
                Text         = _data.Raw ?? "---",
                Font         = new Font("Consolas", 8.5f),
                ForeColor    = Color.LightBlue,
                Dock         = DockStyle.Top,
                Height       = 42,
                Padding      = new Padding(10, 8, 10, 0),
                AutoEllipsis = false
            };

            var divider = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 1,
                BackColor = Color.FromArgb(60, 60, 80)
            };

            var grid = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 0,
                Padding     = new Padding(10, 8, 10, 8),
                BackColor   = Color.Transparent
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(grid, "💨 Viento",     FormatWind());
            AddRow(grid, "👁️ Visib.",    FormatVisib());
            AddRow(grid, "☁️ Nubes",     FormatClouds());
            AddRow(grid, "🌡️ Temp/Dew",  FormatTemp());
            AddRow(grid, "📊 QNH",        FormatQnh());
            AddRow(grid, "📋 Tendencia",  _data.Trend ?? "---");
            AddRow(grid, ConditionIcon() + " Condición", ConditionText());

            Controls.Add(grid);
            Controls.Add(divider);
            Controls.Add(rawLabel);
        }

        private static void AddRow(TableLayoutPanel grid, string field, string value)
        {
            int row = grid.RowCount;
            grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            grid.Controls.Add(MakeLbl(field,  Color.FromArgb(180, 190, 210)), 0, row);
            grid.Controls.Add(MakeLbl(value,  Color.White),                   1, row);
        }

        private static Label MakeLbl(string text, Color fore) => new Label
        {
            Text      = text,
            Font      = new Font("Consolas", 9.5f),
            ForeColor = fore,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(2)
        };

        private string FormatWind()
        {
            if (_data.WindDir == null && _data.WindSpeedKt == null) return "---";
            string dir = _data.WindDir.HasValue ? $"{_data.WindDir:D3}°" : "VRB";
            string spd = _data.WindSpeedKt.HasValue ? $"{_data.WindSpeedKt} kt" : "--- kt";
            string gust = _data.WindGustKt.HasValue ? $"  G{_data.WindGustKt} kt" : "";
            return $"{dir} / {spd}{gust}";
        }

        private string FormatVisib()
        {
            if (!_data.VisibilityKm.HasValue) return "---";
            return _data.VisibilityKm >= 9.9 ? "> 10 km" : $"{_data.VisibilityKm:F1} km";
        }

        private string FormatClouds() =>
            _data.CeilingFt.HasValue ? $"Techo {_data.CeilingFt:N0} ft" : "Sin techo reportado";

        private string FormatTemp()
        {
            if (!_data.TempC.HasValue) return "---";
            string dew = _data.DewPointC.HasValue ? $"{_data.DewPointC:F0}°" : "---";
            return $"{_data.TempC:F0}° / {dew}";
        }

        private string FormatQnh() =>
            _data.QnhHpa.HasValue ? $"{_data.QnhHpa:F0} hPa" : "---";

        private string ConditionText()
        {
            switch (_data.Condition)
            {
                case MetarCondition.VMC:  return "VMC";
                case MetarCondition.MVMC: return "MVMC";
                case MetarCondition.IMC:  return "IMC";
                default:                  return "Desconocido";
            }
        }

        private string ConditionIcon()
        {
            switch (_data.Condition)
            {
                case MetarCondition.VMC:  return "🟢";
                case MetarCondition.MVMC: return "🟡";
                case MetarCondition.IMC:  return "🔴";
                default:                  return "⚪";
            }
        }
    }
}
