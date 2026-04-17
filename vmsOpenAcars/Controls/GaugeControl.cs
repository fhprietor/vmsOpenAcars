using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace vmsOpenAcars.Controls
{
    public class GaugeControl : Control
    {
        private float _value = 0;
        private float _minValue = 0;
        private float _maxValue = 100;
        private string _title = "N1";
        private string _units = "%";
        private Color _barColor = Color.Cyan;
        private Color _warningColor = Color.Orange;
        private Color _dangerColor = Color.Red;
        private float _warningThreshold = 90;
        private float _dangerThreshold = 98;

        public float Value
        {
            get => _value;
            set
            {
                _value = Math.Max(_minValue, Math.Min(_maxValue, value));
                Invalidate();
            }
        }

        public float MinValue { get => _minValue; set { _minValue = value; Invalidate(); } }
        public float MaxValue { get => _maxValue; set { _maxValue = value; Invalidate(); } }
        public string Title { get => _title; set { _title = value; Invalidate(); } }
        public string Units { get => _units; set { _units = value; Invalidate(); } }
        public float WarningThreshold { get => _warningThreshold; set { _warningThreshold = value; Invalidate(); } }
        public float DangerThreshold { get => _dangerThreshold; set { _dangerThreshold = value; Invalidate(); } }

        public GaugeControl()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.DoubleBuffer |
                          ControlStyles.ResizeRedraw |
                          ControlStyles.Opaque, true);  // ← Agregar Opaque
            this.Size = new Size(75, 75);
            this.BackColor = Color.FromArgb(20, 25, 35);  // ← Usar color sólido en lugar de Transparent
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // No llamar a base para evitar el fondo predeterminado
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int cx = Width / 2;
            int cy = Height / 2;
            int radius = Math.Min(Width, Height) / 2 - 12;

            float angle = -135 + (Value / (MaxValue - MinValue)) * 270;
            float radians = angle * (float)Math.PI / 180;

            Color needleColor = Value >= DangerThreshold ? _dangerColor :
                                Value >= WarningThreshold ? _warningColor : _barColor;

            // Arco de fondo
            using (Pen backPen = new Pen(Color.FromArgb(60, 60, 80), 6))
            {
                g.DrawArc(backPen, cx - radius, cy - radius, radius * 2, radius * 2, -135, 270);
            }

            // Arco de progreso
            float sweepAngle = (Value / (MaxValue - MinValue)) * 270;
            using (Pen progressPen = new Pen(needleColor, 6))
            {
                g.DrawArc(progressPen, cx - radius, cy - radius, radius * 2, radius * 2, -135, sweepAngle);
            }

            // Aguja
            int needleLength = radius - 6;
            float needleX = cx + (float)Math.Cos(radians) * needleLength;
            float needleY = cy + (float)Math.Sin(radians) * needleLength;

            using (Pen needlePen = new Pen(needleColor, 2))
            using (SolidBrush centerBrush = new SolidBrush(needleColor))
            {
                g.DrawLine(needlePen, cx, cy, needleX, needleY);
                g.FillEllipse(centerBrush, cx - 3, cy - 3, 6, 6);
            }

            // Título
            using (Font titleFont = new Font("Consolas", 7, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(180, 180, 220)))
            {
                SizeF titleSize = g.MeasureString(Title, titleFont);
                g.DrawString(Title, titleFont, titleBrush,
                    cx - titleSize.Width / 2, cy - radius - 2);
            }

            // Valor
            string valueText = $"{Value:F0}{Units}";
            using (Font valueFont = new Font("Consolas", 9, FontStyle.Bold))
            using (SolidBrush valueBrush = new SolidBrush(needleColor))
            {
                SizeF valueSize = g.MeasureString(valueText, valueFont);
                g.DrawString(valueText, valueFont, valueBrush,
                    cx - valueSize.Width / 2, cy + radius - 14);
            }
        }
    }
}