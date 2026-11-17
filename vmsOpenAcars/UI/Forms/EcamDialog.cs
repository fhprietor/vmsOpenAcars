// EcamDialog.cs
using System;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Controls;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.UI.Forms
{
    public enum EcamDialogButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public class EcamDialog : Form
    {
        private Panel pnlTitleBar;
        private Label lblTitle;
        private Button btnClose;
        private bool _dragging = false;
        private Point _dragStartPoint;

        private Label lblMessage;
        private Button btn1;
        private Button btn2;
        private Button btn3;
        private Panel contentPanel;
        private Panel pnlMessage;
        private FlowLayoutPanel panelBotones;
        private string _message;
        private string _title;
        private EcamDialogButtons _buttons;

        /// <summary>
        /// Rótulos propios para los botones de aceptar/cancelar. `null` = los del idioma
        /// (`BtnYes`/`BtnNo`), que es el caso de todos los avisos de siempre.
        ///
        /// Existe por el **aviso de ruta**: sus dos salidas no son «sí/no» sino «he revisado la
        /// ruta» y «corregir el plan», y un SÍ/NO delante de un cruce de espacio aéreo no dice qué
        /// hace cada botón. La regla de `ButtonLabelsTests` se mantiene: los rótulos llegan ya
        /// traducidos por el localizador desde quien construye el diálogo.
        /// </summary>
        private readonly string _yesText;
        private readonly string _noText;

        // Para poder comprobar el reparto de espacio sin enseñar la ventana (tests): los dos
        // rectángulos están en coordenadas de `contentPanel`, así que son comparables.
        internal Rectangle MessageHostBounds => pnlMessage.Bounds;
        internal Rectangle ButtonsBounds     => panelBotones.Bounds;

        // Rótulos de los botones, para poder comprobar sin enseñar la ventana que salen del fichero
        // de idioma y no escritos a mano (tests). El popup es el de toda la aplicación: hasta la
        // 0.9.43 decía YES / NO / CANCEL en inglés aunque el mensaje sí estuviera traducido.
        internal string Button1Text => btn1?.Text;
        internal string Button2Text => btn2?.Text;
        internal string Button3Text => btn3?.Text;

        /// <summary>Ancho que ocupan las teclas de la fila, con sus márgenes (tests del reparto).</summary>
        internal int ButtonsRowWidth
        {
            get
            {
                int total = 0;
                if (panelBotones != null)
                    foreach (Control button in panelBotones.Controls)
                        total += button.Width + button.Margin.Horizontal;
                return total;
            }
        }

        /// <summary>Alto que pide el texto ya envuelto: si no cabe en su panel, hay que desplazar.</summary>
        internal int MessagePreferredHeight  => lblMessage.PreferredHeight;

        public EcamDialog(string message, string title, EcamDialogButtons buttons = EcamDialogButtons.YesNo,
                          string yesText = null, string noText = null)
        {
            _title = title;
            _message = message;
            _buttons = buttons;
            _yesText = string.IsNullOrWhiteSpace(yesText) ? null : yesText;
            _noText  = string.IsNullOrWhiteSpace(noText)  ? null : noText;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(450, 250);
            this.MinimumSize = new Size(400, 220);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(20, 30, 40);
            this.Padding = new Padding(2);

            // Borde sutil
            this.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            };

            // ===== BARRA DE TÍTULO PERSONALIZADA =====
            pnlTitleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(30, 40, 50)
            };

            lblTitle = new Label
            {
                Text = _title,
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(10, 8),
                AutoSize = true
            };

            btnClose = new McduKeyButton
            {
                Text = "✕",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Size = new Size(30, 25),
                Location = new Point(this.ClientSize.Width - 35, 5),
                LegendColor = McduLegend.Danger,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; Close(); };

            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(btnClose);

            // Arrastre desde la barra de título
            pnlTitleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _dragStartPoint = new Point(e.X, e.Y);
                }
            };
            pnlTitleBar.MouseMove += (s, e) =>
            {
                if (_dragging)
                {
                    Point p = PointToScreen(e.Location);
                    this.Location = new Point(p.X - _dragStartPoint.X, p.Y - _dragStartPoint.Y);
                }
            };
            pnlTitleBar.MouseUp += (s, e) => _dragging = false;

            // Panel de contenido
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 20),
                Padding = new Padding(10)
            };

            // Mensaje. Va dentro de un panel con desplazamiento y **dockeado**, no en posición
            // absoluta: con `Location` fija y `AutoSize` el texto crecía hacia abajo y se pintaba
            // ENCIMA de los botones —el aviso de discrepancia de aeronave son ~10 líneas y dejaba
            // el NO inalcanzable—. Con el mensaje en `Dock.Fill` y los botones en `Dock.Bottom`
            // los dos se reparten el panel y no pueden solaparse por mucho que crezca el texto;
            // si aun así no cabe, el panel saca barra de desplazamiento.
            pnlMessage = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = true
            };

            lblMessage = new Label
            {
                Text = _message,
                Font = new Font("Consolas", 11, FontStyle.Bold),
                ForeColor = Color.LightGreen,
                Location = new Point(0, 0),
                AutoSize = true,
                MaximumSize = new Size(380, 0)
            };
            pnlMessage.Controls.Add(lblMessage);

            // Panel para botones (FlowLayoutPanel para alinearlos a la derecha)
            panelBotones = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.Transparent
            };
            contentPanel.Controls.Add(pnlMessage);
            contentPanel.Controls.Add(panelBotones);

            // Crear botones según la configuración
            switch (_buttons)
            {
                case EcamDialogButtons.OK:
                    btn1 = CreateButton(_("BtnOk"), McduLegend.Confirm);
                    btn1.Click += (s, e) => { this.DialogResult = DialogResult.OK; Close(); };
                    panelBotones.Controls.Add(btn1);
                    break;

                case EcamDialogButtons.OKCancel:
                    btn1 = CreateButton(_("BtnOk"), McduLegend.Confirm);
                    btn1.Click += (s, e) => { this.DialogResult = DialogResult.OK; Close(); };
                    panelBotones.Controls.Add(btn1);

                    btn2 = CreateButton(_("BtnCancel"), McduLegend.Danger);
                    btn2.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; Close(); };
                    panelBotones.Controls.Add(btn2);
                    break;

                case EcamDialogButtons.YesNo:
                    btn1 = CreateButton(_yesText ?? _("BtnYes"), McduLegend.Confirm);
                    btn1.Click += (s, e) => { this.DialogResult = DialogResult.Yes; Close(); };
                    panelBotones.Controls.Add(btn1);

                    btn2 = CreateButton(_noText ?? _("BtnNo"), McduLegend.Danger);
                    btn2.Click += (s, e) => { this.DialogResult = DialogResult.No; Close(); };
                    panelBotones.Controls.Add(btn2);
                    break;

                case EcamDialogButtons.YesNoCancel:
                    btn1 = CreateButton(_yesText ?? _("BtnYes"), McduLegend.Confirm);
                    btn1.Click += (s, e) => { this.DialogResult = DialogResult.Yes; Close(); };
                    panelBotones.Controls.Add(btn1);

                    btn2 = CreateButton(_noText ?? _("BtnNo"), McduLegend.Danger);
                    btn2.Click += (s, e) => { this.DialogResult = DialogResult.No; Close(); };
                    panelBotones.Controls.Add(btn2);

                    btn3 = CreateButton(_("BtnCancel"), McduLegend.Neutral);
                    btn3.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; Close(); };
                    panelBotones.Controls.Add(btn3);
                    break;
            }

            this.Controls.Add(contentPanel);
            this.Controls.Add(pnlTitleBar);

            // Alto a la medida del texto (ver FitToMessage) y, al redimensionar, recolocar el
            // botón de cerrar sin tocar el reparto mensaje/botones, que lo hace el docking.
            FitToButtons();
            FitToMessage();
            this.Resize += (s, e) =>
            {
                btnClose.Location = new Point(this.ClientSize.Width - 35, 5);
                FitToMessage();
            };
        }

        /// <summary>
        /// Ajusta el alto de la ventana al texto del mensaje. El alto fijo de 250 px no daba para
        /// un aviso largo: la discrepancia de aeronave, o la lista de vuelos activos, no cabían y
        /// el texto se comía los botones. Aquí se calcula lo que ocupan el título, el mensaje ya
        /// envuelto al ancho disponible y la fila de botones; si la pantalla no da para tanto, el
        /// alto se corta en el área de trabajo y el mensaje se desplaza dentro de su panel.
        /// </summary>
        private void FitToMessage()
        {
            // El ancho se calcula por aritmética y no leyendo el `ClientSize` de los paneles:
            // al construir, el layout todavía no ha corrido y devolvería 0.
            int width = this.ClientSize.Width
                        - this.Padding.Horizontal - contentPanel.Padding.Horizontal
                        - SystemInformation.VerticalScrollBarWidth - 4;
            if (width < 120) width = 120;
            lblMessage.MaximumSize = new Size(width, 0);

            int needed = pnlTitleBar.Height + this.Padding.Vertical + contentPanel.Padding.Vertical
                       + panelBotones.Height + lblMessage.PreferredHeight + 12;

            int maxHeight;
            try { maxHeight = Screen.FromControl(this).WorkingArea.Height - 80; }
            catch { maxHeight = 800; }

            this.Height = Math.Max(this.MinimumSize.Height, Math.Min(needed, maxHeight));
        }

        /// <summary>
        /// Una tecla de confirmación. `legendColor` es el color con el que se **enciende la leyenda**
        /// (el cuerpo es negro): el verde de aceptar y el rojo de cancelar salen de `McduLegend`, que es el
        /// convenio compartido con la barra principal.
        ///
        /// **El ancho sale del rótulo** (con un suelo de 90 px y un techo de 190): un rótulo corto
        /// como «SÍ» deja la tecla de siempre, y uno de dos palabras como «CORREGIR PLAN» no se
        /// encoge hasta ser ilegible. El techo existe para que un rótulo absurdo no haga la ventana
        /// infinita: por encima de él manda el encogido de `McduKeyRenderer`.
        /// </summary>
        private McduKeyButton CreateButton(string text, Color legendColor)
        {
            var font = new Font("Consolas", 10, FontStyle.Bold);

            float width = 90f;
            try
            {
                width = McduKeyRenderer.MeasureLegend(text, font)
                      + 2f * McduKeyRenderer.LegendInset + 8f;
            }
            catch { /* sin medida, la tecla de siempre */ }
            if (width < 90f)  width = 90f;
            if (width > 190f) width = 190f;

            return new McduKeyButton
            {
                Text = text,
                Font = font,
                LegendColor = legendColor,
                Size = new Size((int)Math.Ceiling(width), 35),
                Margin = new Padding(5)
            };
        }

        /// <summary>
        /// Ensancha la ventana si los rótulos de los botones no caben en el ancho de siempre. Sin esto,
        /// dos teclas de dos palabras se salían del diálogo en un monitor escalado.
        /// </summary>
        private void FitToButtons()
        {
            if (panelBotones == null || panelBotones.Controls.Count == 0) return;

            int needed = 0;
            foreach (Control button in panelBotones.Controls)
                needed += button.Width + button.Margin.Horizontal;

            int width = needed + this.Padding.Horizontal + contentPanel.Padding.Horizontal + 24;
            if (width > this.Width) this.Width = width;
        }

        // Métodos estáticos para facilitar el uso
        public static DialogResult Show(string message, string title, EcamDialogButtons buttons = EcamDialogButtons.YesNo)
        {
            using (var dialog = new EcamDialog(message, title, buttons))
            {
                return dialog.ShowDialog();
            }
        }

        public static DialogResult Show(IWin32Window owner, string message, string title, EcamDialogButtons buttons = EcamDialogButtons.YesNo)
        {
            return Show(owner, message, title, buttons, null, null);
        }

        /// <summary>
        /// El mismo diálogo con rótulos propios en los botones de aceptar/cancelar. Lo usa el aviso de
        /// ruta, cuyas dos salidas son «he revisado la ruta» y «corregir el plan».
        /// </summary>
        public static DialogResult Show(IWin32Window owner, string message, string title,
                                        EcamDialogButtons buttons, string yesText, string noText)
        {
            using (var dialog = new EcamDialog(message, title, buttons, yesText, noText))
            {
                return dialog.ShowDialog(owner);
            }
        }
    }
}