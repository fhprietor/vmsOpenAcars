using System;
using System.Drawing;
using System.Windows.Forms;

namespace vmsOpenAcars.Controls
{
    public class LinearGauge : Control
    {
        private float _value = 0;
        private float _minValue = 0;
        private float _maxValue = 100;
        private string _label = "";
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
        public string Label { get => _label; set { _label = value; Invalidate(); } }
        public float WarningThreshold { get => _warningThreshold; set { _warningThreshold = value; Invalidate(); } }
        public float DangerThreshold { get => _dangerThreshold; set { _dangerThreshold = value; Invalidate(); } }

        public LinearGauge()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.DoubleBuffer |
                          ControlStyles.Opaque, true);
            this.Size = new Size(140, 20);
            this.BackColor = Color.FromArgb(20, 25, 35);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;

            float percent = (Value - MinValue) / (MaxValue - MinValue);
            percent = Math.Max(0, Math.Min(1, percent));

            Color barColor = Value >= DangerThreshold ? _dangerColor :
                            Value >= WarningThreshold ? _warningColor : _barColor;

            // Fondo
            using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(40, 40, 50)))
            {
                g.FillRectangle(backBrush, 0, 0, Width, Height);
            }

            // Borde
            using (Pen borderPen = new Pen(Color.FromArgb(80, 80, 100), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // Barra
            int barWidth = (int)((Width - 4) * percent);
            if (barWidth > 0)
            {
                using (SolidBrush barBrush = new SolidBrush(barColor))
                {
                    g.FillRectangle(barBrush, 2, 2, barWidth, Height - 4);
                }
            }

            // Texto
            string text = string.IsNullOrEmpty(Label) ? $"{Value:F0}" : $"{Label}: {Value:F0}";
            using (Font textFont = new Font("Consolas", 7, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                SizeF textSize = g.MeasureString(text, textFont);
                g.DrawString(text, textFont, textBrush,
                    Width - textSize.Width - 4, (Height - textSize.Height) / 2);
            }
        }
    }
}