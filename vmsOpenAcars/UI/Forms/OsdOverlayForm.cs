using System;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.UI.Forms
{
    public enum OsdSeverity { Info, Success, Warning, Critical }

    public sealed class OsdOverlayForm : Form
    {
        private const int WM_NCHITTEST  = 0x0084;
        private const int HTTRANSPARENT = -1;

        private readonly Label _label;
        private readonly System.Windows.Forms.Timer _animTimer;
        private readonly System.Windows.Forms.Timer _flashTimer;

        private enum AnimState { Idle, FadeIn, Hold, FadeOut }
        private AnimState _state        = AnimState.Idle;
        private int       _holdTicks    = 0;
        private int       _flashPhase   = 0;
        private double    _targetOpacity = 0.90;

        private static readonly Color[] TextColors =
        {
            Color.FromArgb(160, 220, 255),  // Info — light blue
            Color.FromArgb(100, 255, 130),  // Success — lime
            Color.FromArgb(255, 215,   0),  // Warning — gold
            Color.FromArgb(255, 110, 110),  // Critical — red
        };

        private static readonly Color BgNormal   = Color.FromArgb(18, 22, 32);
        private static readonly Color BgFlashOff = Color.FromArgb(55,  0,  0);
        private static readonly Color BgFlashOn  = Color.FromArgb(150, 0,  0);

        protected override bool ShowWithoutActivation => true;

        public OsdOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            TopMost         = true;
            BackColor       = BgNormal;
            Opacity         = 0;
            Size            = new Size(540, 78);
            StartPosition   = FormStartPosition.Manual;

            _label = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Consolas", 17, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            Controls.Add(_label);

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += AnimTick;

            _flashTimer = new System.Windows.Forms.Timer { Interval = 220 };
            _flashTimer.Tick += FlashTick;

            PositionOnScreen(AppConfig.OsdScreenIndex);
        }

        private void PositionOnScreen(int screenIndex)
        {
            var screens = Screen.AllScreens;
            var screen  = (screenIndex >= 0 && screenIndex < screens.Length)
                          ? screens[screenIndex] : Screen.PrimaryScreen;
            // Use Bounds (full screen area) so the OSD is centered over the display
            // regardless of taskbar position or fullscreen/windowed sim mode.
            Location = new Point(
                screen.Bounds.Left + (screen.Bounds.Width  - Width)  / 2,
                screen.Bounds.Top  + 40);
        }

        /// <summary>Shows a notification. Safe to call from any thread.</summary>
        public void ShowMessage(string text, OsdSeverity severity, int durationMs = 4000)
        {
            if (InvokeRequired) { Invoke(new Action(() => ShowMessage(text, severity, durationMs))); return; }

            PositionOnScreen(AppConfig.OsdScreenIndex);

            _label.Text      = text;
            _label.ForeColor = TextColors[(int)severity];
            _holdTicks       = Math.Max(1, (int)((durationMs - 650.0) / 16));
            _targetOpacity   = Math.Max(0.10, Math.Min(1.0, AppConfig.OsdOpacity / 100.0));

            if (!Visible) Show();

            // If already animating: update content and reset hold timer without visual restart.
            // This prevents rapid-fire messages from tripling the perceived display time.
            if (_state == AnimState.FadeIn || _state == AnimState.Hold)
            {
                _flashTimer.Stop();
                BackColor = BgNormal;
                // Force at least a Hold after FadeIn completes; no opacity reset.
                return;
            }

            // FadeOut in progress: reverse to FadeIn with current opacity so the message
            // smoothly returns to full brightness instead of starting from 0.
            if (_state == AnimState.FadeOut)
            {
                BackColor = BgNormal;
                _state    = AnimState.FadeIn;
                return;
            }

            // Idle: full fresh start.
            _animTimer.Stop();
            _flashTimer.Stop();
            _flashPhase = 0;

            if (severity == OsdSeverity.Critical)
            {
                Opacity     = _targetOpacity;
                BackColor   = BgFlashOff;
                _flashPhase = 0;
                _flashTimer.Start();
            }
            else
            {
                BackColor = BgNormal;
                Opacity   = 0;
                _state    = AnimState.FadeIn;
                _animTimer.Start();
            }
        }

        /// <summary>Immediately hides the OSD. Safe to call from any thread.</summary>
        public void HideOsd()
        {
            if (InvokeRequired) { Invoke(new Action(HideOsd)); return; }
            _animTimer.Stop();
            _flashTimer.Stop();
            _state  = AnimState.Idle;
            Opacity = 0;
            if (Visible) Hide();
        }

        private void AnimTick(object sender, EventArgs e)
        {
            switch (_state)
            {
                case AnimState.FadeIn:
                    Opacity = Math.Min(Opacity + 0.06, _targetOpacity);
                    if (Opacity >= _targetOpacity) _state = AnimState.Hold;
                    break;

                case AnimState.Hold:
                    if (--_holdTicks <= 0) _state = AnimState.FadeOut;
                    break;

                case AnimState.FadeOut:
                    Opacity = Math.Max(Opacity - 0.04, 0);
                    if (Opacity <= 0) { _animTimer.Stop(); _state = AnimState.Idle; Hide(); }
                    break;
            }
        }

        private void FlashTick(object sender, EventArgs e)
        {
            _flashPhase++;
            BackColor = (_flashPhase % 2 == 1) ? BgFlashOn : BgFlashOff;

            if (_flashPhase >= 6)   // 3 full on/off cycles
            {
                _flashTimer.Stop();
                BackColor = BgNormal;
                _state    = AnimState.Hold;
                _animTimer.Start();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _animTimer.Dispose(); _flashTimer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
