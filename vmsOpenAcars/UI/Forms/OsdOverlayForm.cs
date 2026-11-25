using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI;
using static vmsOpenAcars.Helpers.OsdCardText;

namespace vmsOpenAcars.UI.Forms
{
    public enum OsdSeverity { Info, Success, Warning, Critical }

    /// <summary>
    /// El OSD del cliente: una tarjeta **pintada a mano** con GDI+, sin `Label`, click-through y
    /// `TopMost`.
    ///
    /// Hay dos plantillas: la **simple**, para todos los mensajes que ya existían (mismo texto,
    /// otro estilo), y la **tarjeta** del toque, con la composición de la referencia. Las dos
    /// comparten la animación de opacidad y el parpadeo de `Critical`; el texto de los mensajes
    /// existentes no se toca, solo se pinta distinto.
    ///
    /// **Compromiso de las esquinas**: se recorta con una `Region` hecha con `GraphicsPath`
    /// redondeado para que sigan funcionando `Opacity` y `HTTRANSPARENT`. La `Region` no tiene
    /// antialias y el borde de la esquina queda algo escalonado; el relleno y el texto sí van
    /// suavizados. La alternativa (`UpdateLayeredWindow`) daría esquinas perfectas pero exigiría
    /// rehacer la animación y el click-through, y el mantenedor no lo pidió.
    /// </summary>
    public sealed class OsdOverlayForm : Form
    {
        private const int WM_NCHITTEST  = 0x0084;
        private const int HTTRANSPARENT = -1;

        private readonly System.Windows.Forms.Timer _animTimer;
        private readonly System.Windows.Forms.Timer _flashTimer;

        private enum AnimState { Idle, FadeIn, Hold, FadeOut }
        private AnimState _state     = AnimState.Idle;
        private int       _holdTicks = 0;
        private int       _flashPhase = 0;
        private double    _targetOpacity = 0.90;

        private bool          _simple = true;
        private string        _simpleText = string.Empty;
        private OsdSeverity   _simpleSeverity = OsdSeverity.Info;
        private OsdCardPayload _card;
        private bool          _flashOn;

        protected override bool ShowWithoutActivation => true;

        public OsdOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            TopMost         = true;
            BackColor       = OsdStyle.CardBackground;
            Opacity         = 0;
            StartPosition   = FormStartPosition.Manual;
            DoubleBuffered  = true;

            Size = OsdCardPlacement.CardSize(DeviceDpi);
            ApplyRoundedRegion();

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += AnimTick;

            _flashTimer = new System.Windows.Forms.Timer { Interval = 220 };
            _flashTimer.Tick += FlashTick;

            PositionOnScreen(AppConfig.OsdScreenIndex);
            var _ = Handle; // Force HWND creation so ShowMessage works before first Show()
        }

        // ── API pública ──────────────────────────────────────────────────────────

        /// <summary>Shows a notification. Safe to call from any thread.</summary>
        public void ShowMessage(string text, OsdSeverity severity, int durationMs = 4000)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => ShowMessage(text, severity, durationMs))); return; }

            _simple         = true;
            _simpleText     = text ?? string.Empty;
            _simpleSeverity = severity;
            _card           = null;

            Size = OsdCardRenderer.MeasureSimple(_simpleText, severity, DeviceDpi);
            ApplyRoundedRegion();
            Prepare(severity, durationMs);
        }

        /// <summary>
        /// Pinta la tarjeta del toque. Safe to call from any thread. El payload son los datos
        /// reales del contacto; si falta uno, `OsdCardText` lo omite en vez de inventarlo.
        /// </summary>
        internal void ShowCard(OsdCardPayload payload, int durationMs = 4000)
        {
            if (payload == null) return;
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => ShowCard(payload, durationMs))); return; }

            _simple = false;
            _card   = payload;

            Size = OsdCardPlacement.CardSize(DeviceDpi);
            ApplyRoundedRegion();
            Prepare(payload.Severity, durationMs);
        }

        /// <summary>Immediately hides the OSD. Safe to call from any thread.</summary>
        public void HideOsd()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(HideOsd)); return; }
            _animTimer.Stop();
            _flashTimer.Stop();
            _state   = AnimState.Idle;
            _flashOn = false;
            Opacity  = 0;
            if (Visible) Hide();
        }

        // ── Animación (idéntica a la de v0.5.4) ──────────────────────────────────

        private void Prepare(OsdSeverity severity, int durationMs)
        {
            PositionOnScreen(AppConfig.OsdScreenIndex);

            _holdTicks     = Math.Max(1, (int)((durationMs - 650.0) / 16));
            _targetOpacity = Math.Max(0.10, Math.Min(1.0, AppConfig.OsdOpacity / 100.0));

            if (!Visible) Show();

            // Si ya está animando: actualizar contenido y reiniciar la espera sin reiniciar el
            // fundido. Evita que una ráfaga de mensajes triplique el tiempo percibido.
            if (_state == AnimState.FadeIn || _state == AnimState.Hold)
            {
                _flashTimer.Stop();
                _flashOn = false;
                Invalidate();
                return;
            }

            // FadeOut en curso: volver a FadeIn desde la opacidad actual, sin empezar en 0.
            if (_state == AnimState.FadeOut)
            {
                _flashOn = false;
                _state   = AnimState.FadeIn;
                Invalidate();
                return;
            }

            _animTimer.Stop();
            _flashTimer.Stop();
            _flashPhase = 0;

            if (severity == OsdSeverity.Critical)
            {
                Opacity     = _targetOpacity;
                _flashOn    = true;
                _flashPhase = 0;
                _flashTimer.Start();
                Invalidate();
            }
            else
            {
                _flashOn = false;
                Opacity  = 0;
                _state   = AnimState.FadeIn;
                _animTimer.Start();
            }
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
            _flashOn = !_flashOn;
            Invalidate();

            if (_flashPhase >= 6)   // 3 ciclos completos on/off
            {
                _flashTimer.Stop();
                _flashOn = false;
                Invalidate();
                _state = AnimState.Hold;
                _animTimer.Start();
            }
        }

        // ── Pintado ──────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_simple)
                OsdCardRenderer.PaintSimple(e.Graphics, ClientSize, _simpleText, _simpleSeverity, _flashOn);
            else if (_card != null)
                OsdCardRenderer.PaintCard(e.Graphics, ClientSize, _card, _flashOn);
        }

        private void ApplyRoundedRegion()
        {
            float scale = DeviceDpi / 96f;
            using (var path = OsdCardRenderer.CreateRoundedPath(
                       new RectangleF(0, 0, Width, Height), 14f * scale))
            {
                var old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private void PositionOnScreen(int screenIndex)
        {
            var screens = Screen.AllScreens;
            var screen  = (screenIndex >= 0 && screenIndex < screens.Length)
                          ? screens[screenIndex] : Screen.PrimaryScreen;
            // Bounds (no WorkingArea): el OSD se centra sobre la pantalla completa, también en
            // modo ventana o con la barra de tareas en otra posición.
            var point = OsdCardPlacement.CenterTop(
                new Size(Width, Height), screen.Bounds.Size, DeviceDpi);
            Location = new Point(screen.Bounds.Left + point.X, screen.Bounds.Top + point.Y);
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
