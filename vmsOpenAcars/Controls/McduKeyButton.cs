using System;
using System.Drawing;
using System.Windows.Forms;

namespace vmsOpenAcars.Controls
{
    /// <summary>
    /// Tecla estilo MCDU A320 para el ACARS: **cuerpo negro siempre**, leyenda en el color que diga
    /// <see cref="LegendColor"/>.
    ///
    /// Sustituye a `Button` sin tocar nada más: `Text` sigue siendo el rótulo, `Enabled` el estado y
    /// `Click` el manejador. Lo único que se retira es el color de fondo como portador de significado —
    /// `BackColor` vuelve a ser lo que dice su nombre (el fondo, heredado del panel, que es lo que asoma
    /// en las esquinas redondeadas) y el color del botón vive en `LegendColor`.
    ///
    /// Esto importa porque hoy `BackColor` **es** el convenio de color de la barra: `MainButtonSpec` lo
    /// lleva por botón y `MainViewModel` lo reenvía en cada cambio de estado (START → ABORT → SEND PIREP).
    /// Con el cambio, ese mismo color pasa a pintar la leyenda, así que el convenio se conserva entero y
    /// lo único que se mueve es dónde se aplica.
    /// </summary>
    internal sealed class McduKeyButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private Color _legendColor = Color.FromArgb(214, 222, 232);

        /// <summary>
        /// El cuerpo de letra común del grupo (en puntos), o `-1` mientras no se haya calculado. Se tira
        /// cuando cambia algo que puede moverlo: el texto, la fuente, el tamaño o el padre.
        /// </summary>
        private float _groupLegendSize = -1f;

        internal McduKeyButton()
        {
            // El control se pinta entero a mano: sin esto, `Button` seguiría pintando su propio marco.
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
        }

        /// <summary>Color de la leyenda. Es el antiguo `BackColor` de la barra.</summary>
        internal Color LegendColor
        {
            get { return _legendColor; }
            set
            {
                if (_legendColor == value) return;
                _legendColor = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            // El fondo se pinta aquí y no en `OnPaintBackground` para que `DrawToBitmap` (que solo manda
            // `WM_PRINTCLIENT`) no deje las esquinas del capuchón en negro. Un `BackColor` transparente —el
            // de los botones de la cabecera— se resuelve al del padre, que es lo que hay detrás.
            Color background = BackColor;
            if (background.A == 0 && Parent != null) background = Parent.BackColor;

            using (SolidBrush brush = new SolidBrush(background))
                pevent.Graphics.FillRectangle(brush, ClientRectangle);

            McduKeyRenderer.Paint(pevent.Graphics, ClientRectangle, Text, _legendColor, Font,
                                  CurrentState(), CommonLegendSize());

            // El foco se pierde al pintar a mano y sin él no se sabe qué tecla tiene el teclado: el
            // rectángulo punteado se dibuja solo cuando Windows pide enseñarlo.
            if (Focused && ShowFocusCues && Enabled)
            {
                Rectangle inner = ClientRectangle;
                inner.Inflate(-3, -3);
                ControlPaint.DrawFocusRectangle(pevent.Graphics, inner, _legendColor, background);
            }
        }

        /// <summary>
        /// **El cuerpo de letra que comparten las teclas hermanas**, para que no haya unas más pequeñas que
        /// otras: se mira el de todas las teclas del mismo padre, del **mismo ancho** y de la **misma
        /// fuente**, y se toma el menor que necesita la más larga. Es lo que se vio en la barra al 150 % de
        /// escalado —`DESPACHO` y `BITÁCORA` encogidas y el resto no—, y la regla es la misma que hace que
        /// el control no encoja por su cuenta: la armonía manda sobre el tamaño individual.
        ///
        /// Devuelve `0` cuando no hay nada que decidir (sin padre, o el grupo cabe entero en su cuerpo
        /// configurado), que es el caso normal: así el renderizador usa la fuente tal cual.
        /// </summary>
        internal float CommonLegendSize()
        {
            if (_groupLegendSize >= 0f) return _groupLegendSize;
            if (Parent == null) return 0f;

            float limit = ClientSize.Width - 2f * McduKeyRenderer.LegendInset;
            float common = Font.Size;

            foreach (Control sibling in Parent.Controls)
            {
                McduKeyButton key = sibling as McduKeyButton;
                if (key == null || key == this) continue;
                if (key.Font.Size != Font.Size || key.ClientSize.Width != ClientSize.Width) continue;
                if (string.IsNullOrEmpty(key.Text)) continue;

                float fitted = McduKeyRenderer.FittedSize(key.Text, key.Font, limit);
                if (fitted < common) common = fitted;
            }

            // También el propio rótulo: la tecla más exigente del grupo puede ser esta.
            float mine = McduKeyRenderer.FittedSize(Text, Font, limit);
            if (mine < common) common = mine;

            _groupLegendSize = common < Font.Size ? common : 0f;
            return _groupLegendSize;
        }

        private McduKeyState CurrentState()
        {
            if (!Enabled) return McduKeyState.Disabled;
            if (_pressed) return McduKeyState.Pressed;
            if (_hover)   return McduKeyState.Hover;
            return McduKeyState.Normal;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
            base.OnMouseUp(mevent);
        }

        protected override void OnKeyDown(KeyEventArgs kevent)
        {
            if (kevent.KeyCode == Keys.Space || kevent.KeyCode == Keys.Enter)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnKeyDown(kevent);
        }

        protected override void OnKeyUp(KeyEventArgs kevent)
        {
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
            base.OnKeyUp(kevent);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        // Cualquier cosa que pueda mover el cuerpo de letra común tira el cálculo: el rótulo (cambia de
        // idioma en caliente), la fuente, el tamaño de la tecla y el padre (la tecla que entra o sale del
        // grupo). Se recalcula en el siguiente pintado, que es cuando se conoce el ancho de verdad.
        protected override void OnTextChanged(EventArgs e)
        {
            _groupLegendSize = -1f;
            base.OnTextChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            _groupLegendSize = -1f;
            base.OnFontChanged(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            _groupLegendSize = -1f;
            base.OnSizeChanged(e);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            _groupLegendSize = -1f;
            base.OnParentChanged(e);
        }
    }
}
