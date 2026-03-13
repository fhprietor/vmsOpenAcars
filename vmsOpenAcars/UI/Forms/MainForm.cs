// MainForm.cs
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using static vmsOpenAcars.Helpers.L;
using vmsOpenAcars.UI;

using ContentAlignment = System.Drawing.ContentAlignment;

namespace vmsOpenAcars.UI.Forms
{
    public class MainForm : Form
    {
        private bool _dragging = false;
        private Point _dragStartPoint;
        // Variable para almacenar el último log (opcional)
        private string _lastLogMessage = "";

        // ========== CONTROLES PRINCIPALES ==========
        private TableLayoutPanel mainLayout;
        private Panel pnlHeader;
        private Panel pnlMessage;
        private Panel pnlFma;
        private Panel pnlStatus;
        private Panel pnlButtons;
        private Panel pnlMessageInfo;  // Para datos de vuelo (fila 1)
        private Panel pnlIncoming;      // Para mensajes entrantes (fila 2)


        // ========== CONTROLES DE CABECERA ==========
        private Label lblTitle;
        private Label lblSubTitle;
        // ========== LABELS PARA DATOS DE VUELO ==========
        private Label _lblFlightNo;
        private Label _lblDepArr;
        private Label _lblDuration;

        // ========== LABELS PARA EL FMA ==========
        private Label lblPhase;
        private Label lblSpeed;
        private Label lblAltitude;
        private Label lblAir;

        private Label lblRoute;
        // ========== LABELS PARA DATOS DE SIMBRIEF ==========
        private Label _lblRoute;
        private Label _lblFuel;
        private Label _lblAircraft;
        private Label _lblAlternate;
        private Label _lblAltitude; // opcional
        private Label _lblType;
        private Label _lblRegistration;

        // ========== CONTROLES DE MESSAGE ==========
        private Label lblMessageTitle;
        private Label lblFlightInfo;
        private Label lblIncomingMsg;
        //private ListView lstIncomingMsg;
        private RichTextBox txtIncomingMsg;

        // ========== CONTROLES DE STATUS ==========
        private Label lblStatusTitle;
        private Label lblAcarsStatus;
        private Label lblEtd;
        private Label lblPos;
        private Label lblComm;
        private Label lblUplink;
        private Label lblProgress;
        private Label lblValidationStatus;
        private Label lblCurrentAirport;
        private Label lblSimName;

        // ========== BOTONES ==========
        private Button btnMenu;
        private Button btnLogin;
        private Button btnAtis;
        private Button btnOfp;
        private Button btnMsg;
        private Button btnWeather;
        private Button btnSimbrief;
        private Button btnStartStop;
        private Button btnCancel;

        // ========== SERVICIOS ==========
        private ApiService _apiService;
        private FlightManager _flightManager;
        private FsuipcService _fsuipc;
        private PhpVmsFlightService _phpVmsFlightService;
        private SimbriefEnhancedService _simbriefEnhancedService;
        // Eliminamos _activePilot y _activePlan, ahora viven en FlightManager
        private Timer _uiTimer = new Timer { Interval = 1000 };
        private int _reconnectCounter = 0;

        // ========== TELEMETRIA ==========
        private DateTime _lastPositionUpdate = DateTime.MinValue;
        private readonly TimeSpan _positionUpdateInterval = TimeSpan.FromSeconds(5); // Cada 5 segundos
        private object _lastTelemetry; // Guardar la última telemetría para enviar cuando toque

        public MainForm()
        {
            EnsureConfigKeys();
            InitializeForm();
            InitializeLayout();
            InitializeHeader();
            InitializeMessageSection();
            InitializeFmaPanel();
            InitializeStatusSection();
            InitializeButtons();
            InitializeServices();
            _uiTimer.Tick += UiTimer_Tick;
            StartTimers();
            SubscribeToFlightManagerEvents();
            SubscribeToFsuipcEvents();
        }

        private void InitializeForm()
        {
            this.Text = "vmsOpenAcars - ACARS Flight Deck";
            this.Size = new Size(1024, 768);
            this.MinimumSize = new Size(800, 600);
            this.BackColor = Color.FromArgb(10, 10, 20);
            this.Font = new Font("Consolas", 10, FontStyle.Regular);

            this.FormBorderStyle = FormBorderStyle.None;
            this.Padding = new Padding(2);

            // ===== ESTABLECER ICONO DE LA VENTANA =====
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "logo.png");
                if (File.Exists(iconPath))
                {
                    // Convertir PNG a Icono
                    using (Bitmap bitmap = new Bitmap(iconPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        this.Icon = Icon.FromHandle(hIcon);
                        // Importante: liberar el handle cuando ya no se necesite
                        // (el Icon se encargará de ello al ser destruido)
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando icono: {ex.Message}");
            }
            // ===== LEER POSICIÓN GUARDADA =====
            try
            {
                // Leer valores guardados
                string savedLeft = ConfigurationManager.AppSettings["window_left"];
                string savedTop = ConfigurationManager.AppSettings["window_top"];
                string savedWidth = ConfigurationManager.AppSettings["window_width"];
                string savedHeight = ConfigurationManager.AppSettings["window_height"];

                int left, top, width, height;

                // Si existen todos los valores, restaurar posición y tamaño
                if (!string.IsNullOrEmpty(savedLeft) && !string.IsNullOrEmpty(savedTop) &&
                    !string.IsNullOrEmpty(savedWidth) && !string.IsNullOrEmpty(savedHeight))
                {
                    if (int.TryParse(savedLeft, out left) && int.TryParse(savedTop, out top) &&
                        int.TryParse(savedWidth, out width) && int.TryParse(savedHeight, out height))
                    {
                        // Verificar que la posición esté dentro de alguna pantalla visible
                        Rectangle windowRect = new Rectangle(left, top, width, height);
                        bool isValidPosition = false;

                        foreach (Screen screen in Screen.AllScreens)
                        {
                            if (screen.WorkingArea.IntersectsWith(windowRect))
                            {
                                isValidPosition = true;
                                break;
                            }
                        }

                        if (isValidPosition)
                        {
                            this.StartPosition = FormStartPosition.Manual;
                            this.Location = new Point(left, top);
                            this.Size = new Size(width, height);
                        }
                        else
                        {
                            // Posición inválida, centrar en pantalla principal
                            CenterInScreen(0);
                        }
                    }
                    else
                    {
                        CenterInScreen(0);
                    }
                }
                else
                {
                    CenterInScreen(0);
                }
            }
            catch
            {
                // Si hay cualquier error, centrar en pantalla principal
                CenterInScreen(0);
            }

            // Dibujar borde sutil
            this.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            };
        }

        // Método auxiliar para centrar en una pantalla específica
        private void CenterInScreen(int screenIndex)
        {
            if (screenIndex >= 0 && screenIndex < Screen.AllScreens.Length)
            {
                Screen targetScreen = Screen.AllScreens[screenIndex];
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(
                    targetScreen.WorkingArea.Left + (targetScreen.WorkingArea.Width - this.Width) / 2,
                    targetScreen.WorkingArea.Top + (targetScreen.WorkingArea.Height - this.Height) / 2
                );
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }
        private void InitializeLayout()
        {
            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.Transparent
            };

            // Configurar altas de filas
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));   // Message (datos)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // FMA (fila 3) - altura fija para el panel de fase
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // Incoming Msg
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Buttons

            this.Controls.Add(mainLayout);
        }

        private void InitializeHeader()
        {
            pnlHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 30, 40),
                Padding = new Padding(10)
            };

            // ===== LOGO =====
            PictureBox pbLogo = new PictureBox
            {
                Size = new Size(40, 40), // Tamaño fijo (cuadrado)
                Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.Zoom, // Mantiene proporción
                BackColor = Color.Transparent
            };

            // Cargar el logo
            try
            {
                string logoPath = System.IO.Path.Combine(Application.StartupPath, "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    pbLogo.Image = Image.FromFile(logoPath);
                }
                else
                {
                    // Si no existe, crear un placeholder (opcional)
                    pbLogo.BackColor = Color.FromArgb(50, 50, 60);
                    pbLogo.Paint += (s, e) =>
                    {
                        using (Font font = new Font("Consolas", 8))
                        using (SolidBrush brush = new SolidBrush(Color.Cyan))
                        {
                            e.Graphics.DrawString("LOGO", font, brush, new PointF(5, 12));
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ Error cargando logo: {ex.Message}", Theme.Warning);
            }

            // ===== TÍTULO PRINCIPAL (con desplazamiento por el logo) =====
            lblTitle = new Label
            {
                Text = _("MainTitle"),
                Font = new Font("Consolas", 16, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(60, 10), // 10 (margen) + 40 (ancho logo) + 10 (espacio)
                AutoSize = true
            };

            // ===== NOMBRE DE AEROLÍNEA =====
            string airlineName = ConfigurationManager.AppSettings["airline"] ?? "vmsOpenAcars";
            lblSubTitle = new Label
            {
                Text = airlineName,
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.LightGreen,
                Location = new Point(60, 35), // Alineado con el título
                AutoSize = true
            };

            // ===== BOTÓN DE CONFIGURACIÓN =====
            Button btnSettings = new Button
            {
                Text = "⚙️",
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                Size = new Size(40, 40),
                Location = new Point(pnlHeader.Width - 50, 10),
                BackColor = Color.Transparent,
                ForeColor = Color.Cyan,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += BtnSettings_Click;

            // ===== AÑADIR CONTROLES AL PANEL =====
            pnlHeader.Controls.AddRange(new Control[] { pbLogo, lblTitle, lblSubTitle, btnSettings });
            mainLayout.Controls.Add(pnlHeader, 0, 0);

            // ===== AJUSTAR POSICIÓN DEL BOTÓN AL REDIMENSIONAR =====
            pnlHeader.Resize += (s, e) =>
            {
                btnSettings.Location = new Point(pnlHeader.Width - 50, 10);
            };

            // ===== ARRASTRE DE LA VENTANA =====
            pnlHeader.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _dragStartPoint = new Point(e.X, e.Y);
                }
            };

            pnlHeader.MouseMove += (s, e) =>
            {
                if (_dragging)
                {
                    Point p = PointToScreen(e.Location);
                    this.Location = new Point(p.X - _dragStartPoint.X, p.Y - _dragStartPoint.Y);
                }
            };

            pnlHeader.MouseUp += (s, e) =>
            {
                _dragging = false;
            };
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new SettingsForm())
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    // Actualizar UI con nuevo nombre de aerolínea e idioma
                    string airline = ConfigurationManager.AppSettings["airline"] ?? "vmsOpenAcars";
                    lblSubTitle.Text = airline;

                    // Refrescar todos los textos (si has hecho que los paneles usen LocalMsg)
                    RefreshUILanguage();
                }
            }
        }

        private void RefreshUILanguage()
        {
            // Aquí actualizas todos los textos de la interfaz que dependen del idioma.
            // Por ejemplo:
            // lblStatusTitle.Text = LocalMsg("StatusTitle");
            // lblAcarsStatus.Text = LocalMsg("AcarsStatus") + "  CONNECTED";
            // etc.

            // También podrías reasignar los textos de los botones si cambian con el idioma.
            btnLogin.Text = _("BtnLogin");
            btnSimbrief.Text = _("BtnSimbrief");
            btnStartStop.Text = _("BtnStartStop");
            btnCancel.Text = _flightManager?.ActivePilot == null ? _("BtnExit") : _("BtnCancel");
            // ... más
        }

        private Label CreateInfoLabel(string text, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Consolas", 10, style),
                ForeColor = Color.LightGreen,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
        }

        private void InitializeMessageSection()
        {
            // ===== FILA 1: FLIGHT INFORMATION =====
            pnlMessageInfo = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // TableLayoutPanel para organizar los datos
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            // Configurar columnas iguales
            for (int i = 0; i < 4; i++)
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            // Configurar filas automáticas
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ========== CREAR TODOS LOS LABELS ==========
            _lblFlightNo = CreateInfoLabel("FLT NO: ----", FontStyle.Bold);
            _lblDepArr = CreateInfoLabel("DEP/ARR: ----/----", FontStyle.Bold);
            _lblAlternate = CreateInfoLabel("ALTN: ----", FontStyle.Bold);
            _lblRoute = CreateInfoLabel("ROUTE: ----", FontStyle.Bold);
            _lblAircraft = CreateInfoLabel("ACFT: ----", FontStyle.Bold);
            _lblFuel = CreateInfoLabel("FUEL: ----   ", FontStyle.Bold);
            _lblType = CreateInfoLabel("TYPE: ----", FontStyle.Bold);
            _lblRegistration = CreateInfoLabel("REG: ----", FontStyle.Bold);

            // ========== AGREGAR AL TABLELAYOUTPANEL ==========
            // Fila 0
            tlp.Controls.Add(_lblFlightNo, 0, 0);
            tlp.Controls.Add(_lblDepArr, 1, 0);
            tlp.Controls.Add(_lblAlternate, 2, 0);
            tlp.Controls.Add(_lblRoute, 3, 0);

            // Fila 1
            tlp.Controls.Add(_lblAircraft, 0, 1);
            tlp.Controls.Add(_lblFuel, 1, 1);
            tlp.Controls.Add(_lblType, 2, 1);
            tlp.Controls.Add(_lblRegistration, 3, 1);

            // Agregar tlp al panel (primero, para que ocupe el espacio restante)
            pnlMessageInfo.Controls.Add(tlp);

            // Título (se agregará arriba)
            var lblFlightInfoTitle = new Label
            {
                Text = "FLIGHT INFORMATION",
                Font = new Font("Consolas", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlMessageInfo.Controls.Add(lblFlightInfoTitle);

            mainLayout.Controls.Add(pnlMessageInfo, 0, 1);

            // ===== FILA 2: INCOMING MSG =====
            pnlIncoming = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // RichTextBox para mensajes entrantes (reemplaza al ListView)
            txtIncomingMsg = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = true,      // Evita barra horizontal
                ScrollBars = RichTextBoxScrollBars.Vertical // Solo barra vertical
            };

            // Opcional: Prevenir edición con teclado
            txtIncomingMsg.PreviewKeyDown += (s, e) => e.IsInputKey = false;

            // Opcional: Hacer que el scroll siempre se mantenga abajo si se desea
            // (pero como insertas arriba, no es necesario)

            pnlIncoming.Controls.Add(txtIncomingMsg);

            var lblIncomingTitle = new Label
            {
                Text = "INCOMING MSG",
                Font = new Font("Consolas", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlIncoming.Controls.Add(lblIncomingTitle);

            mainLayout.Controls.Add(pnlIncoming, 0, 2); // Nota: cambiado a índice 2 (fila 2)
        }

        /*
        private void InitializeMessageSection()
        {
            // ===== FILA 1: FLIGHT INFORMATION =====
            pnlMessageInfo = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // TableLayoutPanel para organizar los datos
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            // Configurar columnas iguales
            for (int i = 0; i < 4; i++)
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            // Configurar filas automáticas
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ========== CREAR TODOS LOS LABELS ==========
            _lblFlightNo = CreateInfoLabel("FLT NO: ----", FontStyle.Bold);
            _lblDepArr = CreateInfoLabel("DEP/ARR: ----/----", FontStyle.Bold);
            _lblAlternate = CreateInfoLabel("ALTN: ----", FontStyle.Bold);
            _lblRoute = CreateInfoLabel("ROUTE: ----", FontStyle.Bold);
            _lblAircraft = CreateInfoLabel("ACFT: ----", FontStyle.Bold);
            _lblFuel = CreateInfoLabel("FUEL: ----   ", FontStyle.Bold);
            _lblType = CreateInfoLabel("TYPE: ----", FontStyle.Bold);
            _lblRegistration = CreateInfoLabel("REG: ----", FontStyle.Bold);

            // ========== AGREGAR AL TABLELAYOUTPANEL ==========
            // Fila 0
            tlp.Controls.Add(_lblFlightNo, 0, 0);
            tlp.Controls.Add(_lblDepArr, 1, 0);
            tlp.Controls.Add(_lblAlternate, 2, 0);
            tlp.Controls.Add(_lblRoute, 3, 0);

            // Fila 1
            tlp.Controls.Add(_lblAircraft, 0, 1);
            tlp.Controls.Add(_lblFuel, 1, 1);
            tlp.Controls.Add(_lblType, 2, 1);
            tlp.Controls.Add(_lblRegistration, 3, 1);

            // Agregar tlp al panel (primero, para que ocupe el espacio restante)
            pnlMessageInfo.Controls.Add(tlp);

            // Título (se agregará arriba)
            var lblFlightInfoTitle = new Label
            {
                Text = "FLIGHT INFORMATION",
                Font = new Font("Consolas", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlMessageInfo.Controls.Add(lblFlightInfoTitle);

            mainLayout.Controls.Add(pnlMessageInfo, 0, 1);

            // ===== FILA 2: INCOMING MSG =====
            pnlIncoming = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            lstIncomingMsg = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                View = View.Details,  // Modo detalles
                HeaderStyle = ColumnHeaderStyle.None, // Sin cabeceras
                FullRowSelect = true,
                GridLines = false,
                OwnerDraw = true, // Necesario para colores personalizados (opcional)
                ShowGroups = false
            };
            // Añadir una sola columna que ocupe todo el ancho
            lstIncomingMsg.Columns.Add("Logs", -2); // -2 = ancho automático

            // Manejar el dibujo personalizado (si quieres más control)
            lstIncomingMsg.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                if (e.Item != null)
                {
                    using (SolidBrush brush = new SolidBrush(e.Item.ForeColor))
                    {
                        e.Graphics.DrawString(e.Item.Text, e.Item.Font, brush, e.Bounds);
                    }
                }
                e.DrawFocusRectangle();
            };

            pnlIncoming.Controls.Add(lstIncomingMsg);

            var lblIncomingTitle = new Label
            {
                Text = "INCOMING MSG",
                Font = new Font("Consolas", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlIncoming.Controls.Add(lblIncomingTitle);

            mainLayout.Controls.Add(pnlIncoming, 0, 3);
        }
        */
        private void InitializeFmaPanel()
        {
            pnlFma = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Theme.FMAPanelBackground
            };

            for (int i = 0; i < 5; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            pnlFma.Controls.Add(layout);

            lblPhase = CreateFmaLabel("PHASE", "STANDBY");
            lblSpeed = CreateFmaLabel("SPD", "--- KT");
            lblAltitude = CreateFmaLabel("ALT", "---- FT");
            lblAir = CreateFmaLabel("AIR", "GROUND");
            lblRoute = CreateFmaLabel("ROUTE", "----/----");

            layout.Controls.Add(lblPhase, 0, 0);
            layout.Controls.Add(lblSpeed, 1, 0);
            layout.Controls.Add(lblAltitude, 2, 0);
            layout.Controls.Add(lblAir, 3, 0);
            layout.Controls.Add(lblRoute, 4, 0);
            mainLayout.Controls.Add(pnlFma, 0, 2);
        }
        private Label CreateFmaLabel(string title, string value)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.LargeFont,
                ForeColor = Theme.MainText,
                BackColor = Theme.FMAPanelBackground,
                Text = $"{title}\n{value}"
            };
        }

        private void InitializeStatusSection()
        {
            pnlStatus = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            lblStatusTitle = new Label
            {
                Text = "STATUS",
                Font = new Font("Consolas", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Location = new Point(10, 10),
                AutoSize = true
            };

            int startY = 45;
            int col1X = 30;
            int col2X = 250;
            int lineHeight = 30;

            lblAcarsStatus = CreateStatusLabel("ACARS:", "⚠️ Offline", col1X, startY, Color.Orange);
            lblEtd = CreateStatusLabel("ETD:", "XXXX", col1X, startY + lineHeight);
            lblPos = CreateStatusLabel("POS: ", $"{_("Waiting")}...", col1X, startY + lineHeight * 2);
            lblComm = CreateStatusLabel("COMM:", "VHF", col2X, startY);
            lblUplink = CreateStatusLabel("UPLINK MSG:", "0", col2X, startY + lineHeight);

            // Nuevo label para validación
            lblValidationStatus = new Label
            {
                Text = "VALIDACIÓN: ---",
                Font = new Font("Consolas", 10, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = new Point(col2X, startY + lineHeight * 2),
                AutoSize = true
            };

            lblProgress = new Label
            {
                Text = "COMM DATA RETRIEVAL IN PROGRESS",
                Font = new Font("Consolas", 11, FontStyle.Italic),
                ForeColor = Color.Yellow,
                Location = new Point(30, startY + lineHeight * 3 + 20),
                AutoSize = true
            };

            lblCurrentAirport = new Label
            {
                Text = "APT: ---",
                Font = new Font("Consolas", 10),
                ForeColor = Color.Cyan,
                Location = new Point(col2X, startY + lineHeight * 3), // Debajo de UPLINK
                AutoSize = true
            };

            // Simulador
            lblSimName = new Label
            {
                Text = $"SIM: {_("Waiting")}",
                Font = new Font("Consolas", 10),
                ForeColor = Color.Cyan,
                Location = new Point(col1X, startY + lineHeight * 3), // Debajo de POS
                AutoSize = true
            };

            pnlStatus.Controls.AddRange(new Control[] {
                lblStatusTitle,
                lblAcarsStatus,
                lblEtd,
                lblPos,
                lblComm,
                lblUplink,
                lblValidationStatus,
                lblProgress,
                lblCurrentAirport,
                lblSimName
            });

            mainLayout.Controls.Add(pnlStatus, 0, 4);
        }


        private Label CreateStatusLabel(string label, string value, int x, int y, Color? color = null)
        {
            if (color == null)
            {
                color = Color.LightGreen;
            }
            Label lbl = new Label
            {
                Text = $"{label}  {value}",
                Font = new Font("Consolas", 11),
                ForeColor = color.Value,
                Location = new Point(x, y),
                AutoSize = true
            };
            return lbl;
        }

        private void InitializeButtons()
        {
            pnlButtons = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 40, 50),
                Padding = new Padding(10)
            };

            string[] buttonNames = { "MENU", "LOGIN", "ATIS", "OFP", "MSG", "WEATHER", "SIMBRIEF", "START", "CANCEL" };
            Color[] buttonColors = {
                Color.FromArgb(60, 70, 80),  // MENU
                Color.FromArgb(0, 120, 200), // LOGIN (azul más vivo)
                Color.FromArgb(60, 70, 80),  // ATIS
                Color.FromArgb(0, 100, 200), // OFP
                Color.FromArgb(60, 70, 80),  // MSG
                Color.FromArgb(60, 70, 80),  // WEATHER
                Color.FromArgb(0, 150, 0),   // SIMBRIEF
                Color.FromArgb(200, 100, 0), // START
                Color.FromArgb(150, 0, 0)    // CANCEL
            };

            int xPos = 10;
            int buttonWidth = 90;
            int buttonHeight = 40;
            int spacing = 5;

            for (int i = 0; i < buttonNames.Length; i++)
            {
                Button btn = new Button
                {
                    Text = buttonNames[i],
                    Location = new Point(xPos, 15),
                    Size = new Size(buttonWidth, buttonHeight),
                    BackColor = buttonColors[i],
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Consolas", 10, FontStyle.Bold)
                };

                switch (buttonNames[i])
                {
                    case "LOGIN":
                        btnLogin = btn;
                        btn.Click += BtnLogin_Click;
                        break;
                    case "SIMBRIEF":
                        btnSimbrief = btn;
                        btn.Click += BtnSimbrief_Click;
                        break;
                    case "START":
                        btnStartStop = btn;
                        btn.Click += BtnStartStop_Click;
                        break;
                    case "CANCEL":
                        btnCancel = btn;
                        btn.Click += BtnCancel_Click;
                        btnCancel.Text = "EXIT";
                        break;
                    case "OFP":
                        btnOfp = btn;
                        btn.Click += BtnOfp_Click;
                        break;
                    default:
                        btn.Click += GenericButton_Click;
                        break;
                }

                pnlButtons.Controls.Add(btn);
                xPos += buttonWidth + spacing;
            }

            mainLayout.Controls.Add(pnlButtons, 0, 5);
        }

        private void InitializeServices()
        {
            try
            {
                string apiUrl = System.Configuration.ConfigurationManager.AppSettings["vms_api_url"];
                string apiKey = System.Configuration.ConfigurationManager.AppSettings["vms_api_key"];

                if (!string.IsNullOrEmpty(apiUrl) && !string.IsNullOrEmpty(apiKey))
                {
                    _apiService = new ApiService(apiUrl, apiKey);
                    _flightManager = new FlightManager(_apiService);
                    _phpVmsFlightService = new PhpVmsFlightService(_apiService);
                    _simbriefEnhancedService = new SimbriefEnhancedService(_apiService);
                    _fsuipc = new FsuipcService();

                    _flightManager.OnLog += AddLog;
                    AddLog($"{_("AcarsCore")}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ Error inicializando servicios: {ex.Message}");
            }
        }

        private void SubscribeToFlightManagerEvents()
        {
            if (_flightManager == null) return;

            // Evento de validación de posición (ICAO y GPS)
            _flightManager.OnPositionValidated += (status) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => UpdateValidationUI(status)));
                }
                else
                {
                    UpdateValidationUI(status);
                }
            };

            // Evento de cambio de fase de vuelo
            _flightManager.PhaseChanged += (phase) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        AddLog($"✈️ Fase cambiada: {phase}");
                        if (phase == FlightPhase.Completed)
                        {
                            // Vuelo completado → cambiar botón a SEND PIREP
                            btnStartStop.Text = "SEND PIREP";
                            btnStartStop.BackColor = Color.Green;
                            btnStartStop.Enabled = true;
                            btnCancel.Text = "EXIT";
                        }
                    }));
                }
                else
                {
                    AddLog($"✈️ Fase cambiada: {phase}");
                    if (phase == FlightPhase.Completed)
                    {
                        // Vuelo completado → cambiar botón a SEND PIREP
                        btnStartStop.Text = "SEND PIREP";
                        btnStartStop.BackColor = Color.Green;
                        btnStartStop.Enabled = true;
                        btnCancel.Text = "EXIT";
                    }
                }
            };

            // Evento de cambio de aeropuerto del piloto (opcional, para debug)
            _flightManager.OnAirportChanged += (airport) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => AddLog($"{_("AirportUpdated")}: {airport}")));
                }
                else
                {
                    AddLog($"{_("AirportUpdated")}: {airport}");
                }
            };

            // ===== FORZAR UNA ACTUALIZACIÓN INICIAL DEL ESTADO DE VALIDACIÓN =====
            // Esto es clave para que nada más iniciar, si el simulador ya estaba conectado,
            // se ejecute la validación GPS y se muestre el estado en la UI.
            try
            {
                if (_fsuipc != null)
                {
                    bool connected = _fsuipc.IsConnected;
                    double? lat = connected ? _fsuipc.GetLatitude() : (double?)null;
                    double? lon = connected ? _fsuipc.GetLongitude() : (double?)null;
                    _flightManager.SetSimulatorConnected(connected, lat, lon);
                }
                else
                {
                    // Si FSUIPC no está inicializado, indicar desconectado
                    _flightManager.SetSimulatorConnected(false);
                }
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ Error al forzar validación inicial: {ex.Message}");
            }
        }

        private void UpdateValidationUI(ValidationStatus status)
        {
            string gpsIcon = status.GpsValid ? "✅" : (status.DistanceFromAirport > 0 ? "❌" : "⏳");
            string gpsText = status.DistanceFromAirport > 0 ? $" ({status.DistanceFromAirport:F1}km)" : "";

            if (status.IcaoMatch)
            {
                if (status.GpsValid)
                {
                    // TODO correcto: verde
                    lblValidationStatus.Text = $"VALIDACIÓN: ICAO ✅  GPS: ✅{gpsText}";
                    lblValidationStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    // ICAO bien pero GPS mal: naranja (advertencia)
                    lblValidationStatus.Text = $"VALIDACIÓN: ICAO ✅  GPS: {gpsIcon}{gpsText}";
                    lblValidationStatus.ForeColor = Color.Orange;
                }
            }
            else
            {
                // ICAO no coincide: naranja
                if (string.IsNullOrEmpty(status.SimbriefAirport))
                    lblValidationStatus.Text = $"VALIDACIÓN: Sin plan  GPS: {gpsIcon}{gpsText}";
                else
                    lblValidationStatus.Text = $"VALIDACIÓN: ICAO ❌ (phpVMS: {status.PhpVmsAirport} vs SIMBRIEF: {status.SimbriefAirport})  GPS: {gpsIcon}{gpsText}";

                lblValidationStatus.ForeColor = Color.Orange;
            }

            // Botón START solo si ICAO coincide Y GPS válido Y simulador conectado
            btnStartStop.Enabled = status.IcaoMatch && status.GpsValid && _flightManager?.IsSimulatorConnected == true;
        }

        private void SubscribeToFsuipcEvents()
        {
            double lat = 0, lon = 0, alt = 0, gs = 0, hdg = 0, vs = 0, fuel = 0;
            bool isOnGround = true;

            _fsuipc.Connected += (s, e) => {
                Invoke(new Action(() =>
                {
                    _flightManager?.SetSimulatorConnected(true,
                        _fsuipc.GetLatitude(),
                        _fsuipc.GetLongitude());
                    lblSimName.Text = $"SIM: {_fsuipc.SimulatorName}";
                }));
            };

            _fsuipc.Disconnected += (s, e) => {
                Invoke(new Action(() => {
                    _flightManager?.SetSimulatorConnected(false);
                    lblSimName.Text = $"SIM: {_("Waiting")}";
                    lblPos.Text = $"POS: {_("Waiting")}...";
                }));
            };
            _fsuipc.DataUpdated += (s, e) => {
                // Actualizar posición en tiempo real
                lblPos.Text = $"POS: {e.Latitude:F4}° / {e.Longitude:F4}°";

                // Actualizar FMA (usar try-catch para evitar que errores de UI detengan la telemetría)
                try
                {
                    UpdateAltitude((int)e.Altitude);
                    UpdateSpeed((int)e.GroundSpeed);
                    if (_flightManager != null)
                    {
                        UpdatePhase(_flightManager.CurrentPhase);
                        UpdateAirStatus(_flightManager.CurrentPhase);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error actualizando FMA: {ex.Message}");
                }

                // Actualizar FlightManager (con los 13 parámetros que ahora requiere)
                try
                {
                    _flightManager?.UpdateTelemetry(
                        (int)e.Altitude,
                        (int)e.GroundSpeed,
                        (int)e.VerticalSpeed,
                        e.IsOnGround,
                        e.FuelTotal,
                        e.Latitude,           // NUEVO: latitud
                        e.Longitude,          // NUEVO: longitud
                        e.IndicatedAirspeed,
                        e.FuelFlow,
                        e.TransponderCode,
                        e.AutopilotMaster,
                        e.SimulationZuluTime,
                        e.RadarAltitude,
                        e.Order
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error en UpdateTelemetry: {ex.Message}");
                }

                // Validación de posición si no hay vuelo activo
                if (_flightManager != null && string.IsNullOrEmpty(_flightManager.ActivePirepId))
                {
                    _flightManager.UpdatePositionValidation(e.Latitude, e.Longitude);
                }

                // Preparar telemetría COMPLETA para phpVMS (si hay vuelo activo)
                if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId))
                {
                    try
                    {
                        // Calcular distancia desde última posición
                        double? distance = null;
                        if (_lastPosition.HasValue)
                        {
                            distance = CalculateDistance(
                                _lastPosition.Value.lat,
                                _lastPosition.Value.lon,
                                e.Latitude,
                                e.Longitude
                            );
                        }
                        _lastPosition = (e.Latitude, e.Longitude);

                        // Calcular AGL de forma inteligente
                        double agl;
                        if (e.RadarAltitude > 10)
                        {
                            agl = e.RadarAltitude;
                        }
                        else
                        {
                            double terrainElevation = GetTerrainElevation(e.Latitude, e.Longitude, _flightManager.CurrentPhase);
                            agl = terrainElevation > 0 ? e.Altitude - terrainElevation : e.Altitude;
                        }

                        var position = new AcarsPosition
                        {
                            // Metadatos de la posición
                            type = 0,
                            nav_type = e.NavType,
                            order = e.Order,
                            name = GetPhaseName(_flightManager.CurrentPhase),
                            status = GetPhpVmsStatusCode(_flightManager.CurrentPhase),
                            log = GetCurrentLog(),

                            // Posición geográfica
                            lat = e.Latitude,
                            lon = e.Longitude,

                            // Distancia recorrida
                            distance = distance,

                            // Dirección
                            heading = (int)Math.Round(e.Heading, 0),

                            // Altitudes
                            altitude = Math.Round(e.Altitude, 0),
                            altitude_agl = Math.Round(agl, 0),
                            altitude_msl = Math.Round(e.Altitude, 0),

                            // Velocidades
                            vs = Math.Round(e.VerticalSpeed, 0),
                            gs = (int)Math.Round(e.GroundSpeed, 0),
                            ias = (int)Math.Round(e.IndicatedAirspeed, 0),

                            // Sistemas
                            transponder = e.TransponderCode,
                            autopilot = e.AutopilotMaster,

                            // Combustible
                            fuel_flow = Math.Round(e.FuelFlow, 0),
                            fuel = Math.Round(e.FuelTotal, 0),

                            // Tiempo
                            sim_time = e.SimulationZuluTime,
                            source = "vmsOpenAcars"
                        };

                        _lastTelemetry = new AcarsPositionUpdate
                        {
                            positions = new[] { position }
                        };
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error preparando telemetría: {ex.Message}");
                    }
                }
            };

        }
        private static readonly Dictionary<FlightPhase, string> PhaseToCode = new Dictionary<FlightPhase, string>
        {
            [FlightPhase.Boarding] = "BST",
            [FlightPhase.Pushback] = "PBT",
            [FlightPhase.TaxiOut] = "TXI",
            [FlightPhase.TaxiIn] = "TXI",
            [FlightPhase.Takeoff] = "TOF",
            [FlightPhase.Climb] = "ICL",
            [FlightPhase.Enroute] = "ENR",
            [FlightPhase.Descent] = "APR",
            [FlightPhase.Approach] = "FIN",
            [FlightPhase.Landing] = "LDG",
            [FlightPhase.Landed] = "LAN",
            [FlightPhase.Completed] = "ARR"
        };
        private double GetTerrainElevation(double lat, double lon, FlightPhase phase)
        {
            var plan = _flightManager?.ActivePlan;
            if (plan == null) return 0;

            // Si estamos en fases cercanas al destino, usar elevación del destino
            if (phase == FlightPhase.Descent || phase == FlightPhase.Approach || phase == FlightPhase.Landing)
            {
                return plan.DestinationElevation;
            }
            // Si estamos en fases cercanas al origen, usar elevación del origen
            else if (phase == FlightPhase.Takeoff || phase == FlightPhase.Climb)
            {
                return plan.OriginElevation;
            }
            // En ruta, no tenemos elevación del terreno, devolvemos 0 (o podríamos usar altitud MSL, pero mejor 0)
            else
            {
                return 0;
            }
        }
        private string GetPhpVmsStatusCode(FlightPhase phase)
        {
            return PhaseToCode.TryGetValue(phase, out string code) ? code : "INI";
        }
        private void UpdateIndicatedAirspeed(int ias)
        {
            // Si tienes un label para IAS en el FMA, actualízalo
            // Por ahora solo registramos
            if (ias > 0)
            {
                // Puedes añadir un label específico si lo deseas
            }
        }
        private void UpdateAltitude(int altitude)
        {
            lblAltitude.Text = $"ALT\n{altitude} FT";

            if (_flightManager.CurrentPhase.IsAirborne())
                lblAltitude.ForeColor = Theme.Enroute;   // Verde
            else
                lblAltitude.ForeColor = Color.Gray;
        }
        private void UpdateSpeed(int speed)
        {
            lblSpeed.Text = $"SPD\n{speed} KT";

            if (speed > 3)
                lblSpeed.ForeColor = Theme.Enroute;
            else
                lblSpeed.ForeColor = Color.Gray;
        }
        public void UpdateAirStatus(FlightPhase phase)
        {
            if (phase == FlightPhase.Idle)
            {
                lblAir.Text = "AIR\n---";
                lblAir.ForeColor = Color.Gray;
                return;
            }

            if (phase.IsAirborne())
            {
                lblAir.Text = "AIR\nAIRBORNE";
                lblAir.ForeColor = Theme.Enroute;
            }
            else
            {
                lblAir.Text = "AIR\nGROUND";
                lblAir.ForeColor = Theme.Taxi;
            }
        }

        public void UpdatePhase(FlightPhase phase)
        {
            lblPhase.Text = "PHASE\n" + phase.ToString().ToUpper();

            Color phaseColor;

            switch (phase)
            {
                case FlightPhase.Idle:
                    phaseColor = Color.Gray;
                    break;
                case FlightPhase.Boarding:
                case FlightPhase.Pushback:
                case FlightPhase.TaxiOut:
                case FlightPhase.TaxiIn:
                    phaseColor = Theme.Taxi;
                    break;

                case FlightPhase.Takeoff:
                    phaseColor = Theme.Takeoff;
                    break;

                case FlightPhase.Climb:
                case FlightPhase.Enroute:
                    phaseColor = Theme.Enroute;
                    break;

                case FlightPhase.Descent:
                case FlightPhase.Approach:
                    phaseColor = Theme.Approach;
                    break;

                case FlightPhase.Completed:
                    phaseColor = Theme.Arrived;
                    break;

                default:
                    phaseColor = Theme.MainText;
                    break;
            }

            lblPhase.ForeColor = phaseColor;
        }
        
        private void StartTimers()
        {
            _fsuipc?.Start(); // Inicia el heartbeat
            _uiTimer.Start(); // Timer de UI
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_flightManager != null)
            {
                UpdateFlightInfo();
            }

            // Enviar posición a phpVMS si hay vuelo activo y ha pasado el intervalo
            if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId) && _lastTelemetry != null)
            {
                if (DateTime.UtcNow - _lastPositionUpdate >= _positionUpdateInterval)
                {
                    // Enviar en segundo plano para no bloquear la UI
                    Task.Run(async () =>
                    {
                        bool success = await _apiService.SendPositionUpdate(
                            _flightManager.ActivePirepId,
                            _lastTelemetry
                        );
                        this.Invoke(new Action(() => {
                            UpdateNetworkUI(success);
                            if (success)
                                _lastPositionUpdate = DateTime.UtcNow;
                        }));
                    });
                }
            }

            /*
            // Actualizar posición GPS si está conectado
            if (_fsuipc != null && _fsuipc.IsConnected)
            {
                double lat = _fsuipc.GetLatitude();
                double lon = _fsuipc.GetLongitude();
                lblPos.Text = $"POS: {lat:F4}° / {lon:F4}°";

                // Validar posición periódicamente si no hay vuelo activo
                if (_flightManager != null && string.IsNullOrEmpty(_flightManager.ActivePirepId))
                {
                    _flightManager.UpdatePositionValidation(lat, lon);
                }
            }
            */

            // Actualizar nombre del simulador
            if (lblSimName != null)
            {
                lblSimName.Text = (_fsuipc != null && _fsuipc.IsConnected)
                    ? $"SIM: {_fsuipc.GetSimName()}"
                    : $"SIM: {_("Waiting")}";
            }

            // Intentar reconectar FSUIPC si está desconectado (cada 5 segundos)
            if (_fsuipc != null && !_fsuipc.IsConnected)
            {
                // Usar un contador en lugar de depender de los segundos del reloj
                if (_reconnectCounter >= 5)
                {
                    Task.Run(() => _fsuipc.TryReconnect());
                    _reconnectCounter = 0;
                }
                else
                {
                    _reconnectCounter++;
                }
            }
            else
            {
                // Si está conectado, resetear el contador
                _reconnectCounter = 0;
            }
        }
        private void UpdateNetworkUI(bool isSuccessful)
        {
            if (lblAcarsStatus == null) return;

            if (isSuccessful)
            {
                lblAcarsStatus.Text = "ACARS: 📡 Online";
                lblAcarsStatus.ForeColor = Color.LightGreen;
            }
            else
            {
                lblAcarsStatus.Text = "ACARS: ⚠️ Offline";
                lblAcarsStatus.ForeColor = Color.Orange;
            }
        }
        private void UpdateFlightInfo()
        {
            var plan = _flightManager?.ActivePlan;
            if (plan != null)
            {
                _lblFlightNo.Text = $"FLT NO: {plan.Airline}{plan.FlightNumber}";
                _lblDepArr.Text = $"DEP/ARR: {plan.Origin}/{plan.Destination}";
                _lblAlternate.Text = $"ALTN: {plan.Alternate ?? "N/A"}";
                _lblRoute.Text = $"ROUTE: {plan.Route}";
                _lblAircraft.Text = $"ACFT: {plan.AircraftIcao}";
                _lblFuel.Text = $"FUEL: {plan.BlockFuel:F0} {plan.Units ?? "kg"}";
                _lblType.Text = $"TYPE: {plan.Aircraft}";
                _lblRegistration.Text = $"REG: {plan.Registration}";
            }
            else
            {
                _lblFlightNo.Text = "FLT NO: ----";
                _lblDepArr.Text = "DEP/ARR: ----/----";
                _lblAlternate.Text = "ALTN: ----";
                _lblRoute.Text = "ROUTE: ----";
                _lblAircraft.Text = "ACFT: ----";
                _lblFuel.Text = "FUEL: ---- kg";
                _lblType.Text = "TYPE: ----";
                _lblRegistration.Text = "REG: ----";
            }
        }
        // Actualizar AddLog para guardar el último mensaje
        private void AddLog(string message, Color color)
        {
            string timestamp = $"{DateTime.UtcNow:HH:mm:ss} - ";
            string fullMessage = timestamp + message;

            // Guardar el último mensaje de log (sin timestamp para el campo "log")
            _lastLogMessage = message;

            // Resto del código existente de AddLog...
            Action addAction = () =>
            {
                txtIncomingMsg.SelectionStart = 0;
                txtIncomingMsg.SelectionLength = 0;
                txtIncomingMsg.SelectionColor = color;
                txtIncomingMsg.SelectedText = fullMessage + "\n";
            };

            if (txtIncomingMsg.InvokeRequired)
                txtIncomingMsg.Invoke(addAction);
            else
                addAction();
        }

        // Sobrecarga para usar el color por defecto del tema
        private void AddLog(string message)
        {
            AddLog(message, Theme.MainText);
        }
        // Obtener el log actual para incluirlo en la posición
        private string GetCurrentLog()
        {
            return _lastLogMessage;
        }

        // Variable para última posición (para calcular distancia)
        private (double lat, double lon)? _lastPosition = null;

        // Método para calcular distancia (Haversine)
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Radio de la Tierra en km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
        // Método para obtener nombre del punto según fase
        private string GetPhaseName(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.Boarding: return "BOARDING";
                case FlightPhase.Pushback: return "PUSHBACK";
                case FlightPhase.TaxiOut: return "TAXI OUT";
                case FlightPhase.Takeoff: return "TAKEOFF";
                case FlightPhase.Climb: return "CLIMB";
                case FlightPhase.Enroute: return "ENROUTE";
                case FlightPhase.Descent: return "DESCENT";
                case FlightPhase.Approach: return "APPROACH";
                case FlightPhase.TaxiIn: return "TAXI IN";
                case FlightPhase.Completed: return "COMPLETED";
                case FlightPhase.Idle: return "IDLE";
                default: return phase.ToString().ToUpper();
            }
        }


        // ========== EVENTOS DE BOTONES ==========

        private void BtnSimbrief_Click(object sender, EventArgs e)
        {
            // Verificar que hay piloto activo
            if (_flightManager?.ActivePilot == null)
            {
                MessageBox.Show("Debes iniciar sesión primero.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Detectar aeropuerto actual (opcional, el FlightManager ya tiene CurrentAirport)
            string currentAirport = _flightManager.CurrentAirport;
            if (string.IsNullOrEmpty(currentAirport))
                currentAirport = "SKBO";

            var planner = new FlightPlannerForm(
                _phpVmsFlightService,
                _simbriefEnhancedService,
                _flightManager,
                _flightManager.ActivePilot,
                currentAirport
            );

            if (planner.ShowDialog() == DialogResult.OK)
            {
                var plan = planner.GetLoadedPlan();
                if (plan != null)
                {
                    _flightManager.SetActivePlan(plan);
                    UpdateFlightInfo();
                    AddLog($"✅ PLAN {_("Loaded")}: {plan.Origin} → {plan.Destination}");
                }
            }
        }

        private async void BtnStartStop_Click(object sender, EventArgs e)
        {
            switch (btnStartStop.Text)
            {
                case "START":
                    // Verificar si se puede iniciar según FlightManager
                    if (!_flightManager.CanStartFlight())
                    {
                        AddLog("⛔ No se cumplen las condiciones para iniciar el vuelo", Theme.Warning);
                        return;
                    }

                    // Intentar iniciar vuelo
                    bool started = await _flightManager.StartFlight(
                        _flightManager.ActivePlan,
                        _flightManager.ActivePilot
                    );

                    if (started)
                    {
                        btnStartStop.Text = "ABORT";
                        btnStartStop.BackColor = Color.Red;
                        btnCancel.Text = "CANCEL";
                        AddLog(_("FlightStarted"), Theme.Success);
                    }
                    else
                    {
                        AddLog("❌ No se pudo iniciar el vuelo (revisa logs)", Theme.Danger);
                    }
                    break;

                case "ABORT":
                    if (EcamDialog.Show(this, "¿Cancelar el vuelo actual? Se eliminará el PIREP en curso.",
                        "CONFIRMAR", EcamDialogButtons.YesNo) == DialogResult.Yes)
                    {
                        bool aborted = await _flightManager.CancelFlight();
                        if (aborted)
                        {
                            btnStartStop.Text = "START";
                            btnStartStop.BackColor = Color.FromArgb(200, 100, 0);
                            btnStartStop.Enabled = false; // Se habilitará cuando vuelva a cumplir condiciones
                            btnCancel.Text = "EXIT";
                            AddLog("✖️ Vuelo cancelado", Theme.Warning);
                        }
                    }
                    break;

                case "SEND PIREP":
                    if (EcamDialog.Show(this, "¿Enviar el informe de vuelo a phpVMS?",
                        "CONFIRMAR", EcamDialogButtons.YesNo) == DialogResult.Yes)
                    {
                        // NO forzar fase ARRIVED aquí, el FlightManager ya debería estar en Completed
                        // Simplemente envía el PIREP
                        bool filed = await _flightManager.FilePirep();
                        if (filed)
                        {
                            btnStartStop.Text = "START";
                            btnStartStop.BackColor = Color.FromArgb(200, 100, 0);
                            btnStartStop.Enabled = false;
                            btnCancel.Text = "EXIT";
                            _lastTelemetry = null;
                            _lastPositionUpdate = DateTime.MinValue;
                            AddLog("✅ Vuelo reportado, listo para siguiente vuelo", Theme.Success);
                        }
                    }
                    break;
            }
        }
        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (_apiService == null)
            {
                AddLog("❌ ApiService no inicializado");
                return;
            }

            AddLog($"{_("LoggingIn")}...");
            btnLogin.Enabled = false; // Deshabilitar mientras se procesa

            try
            {
                var result = await _apiService.GetPilotData();
                if (result.Data != null)
                {
                    Pilot pilot = result.Data;
                    _flightManager?.SetActivePilot(pilot);
                    AddLog($"{_("SuccessfulLogin")}: {pilot.Name} ({_("Rank")}: {pilot.Rank})");
                    AddLog($"{_("AirportAssigned")}: {pilot.CurrentAirport}");
                    // Actualizar label del aeropuerto actual
                    if (lblCurrentAirport != null)
                        lblCurrentAirport.Text = $"APT: {pilot.CurrentAirport}";
                }
                else
                {
                    AddLog($"❌ Error de login: {result.Error}");
                    MessageBox.Show($"Error de login: {result.Error}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ Excepción en login: {ex.Message}");
            }
            finally
            {
                btnLogin.Enabled = true;
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (btnCancel.Text == "EXIT")
            {
                if (EcamDialog.Show(this, "¿Salir de la aplicación?", "SALIR", EcamDialogButtons.YesNo) == DialogResult.Yes)
                {
                    Application.Exit();
                }
            }
            else // CANCEL
            {
                if (EcamDialog.Show(this, "¿Cancelar el vuelo actual?", "CANCELAR VUELO", EcamDialogButtons.YesNo) == DialogResult.Yes)
                {
                    _flightManager?.CancelFlight();
                    btnStartStop.Text = "START";
                    btnStartStop.BackColor = Color.FromArgb(200, 100, 0);
                    btnCancel.Text = "EXIT";
                    AddLog("✖️ Vuelo cancelado por el usuario");
                }
            }
        }


        private void BtnOfp_Click(object sender, EventArgs e)
        {
            var plan = _flightManager?.ActivePlan;
            if (plan != null)
            {
                MessageBox.Show($"OFP: {plan.Route}\nCombustible: {plan.BlockFuel} kg",
                    "Operational Flight Plan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No hay OFP cargado.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void GenericButton_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            AddLog($"🔘 Botón {btn.Text} presionado");
        }

        // Método público para que otros formularios (ej. login) puedan establecer el piloto
        public void SetActivePilot(Pilot pilot)
        {
            _flightManager?.SetActivePilot(pilot);
        }

        // También podrías exponer el FlightManager si es necesario
        public FlightManager FlightManager => _flightManager;
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _uiTimer.Enabled = false;
            // Desuscribir eventos
            if (_fsuipc != null)
            {
                _fsuipc.Connected -= null; // No podemos quitar específicamente porque usamos lambdas, mejor guardar referencias
                // Una forma simple es no desuscribir si el formulario se cierra, pero para ser correctos, podríamos usar métodos con nombre.
            }
            base.OnFormClosing(e);

            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Guardar posición y tamaño
                config.AppSettings.Settings["window_left"].Value = this.Location.X.ToString();
                config.AppSettings.Settings["window_top"].Value = this.Location.Y.ToString();
                config.AppSettings.Settings["window_width"].Value = this.Size.Width.ToString();
                config.AppSettings.Settings["window_height"].Value = this.Size.Height.ToString();

                // También guardar el índice de pantalla donde está (opcional)
                int screenIndex = 0;
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    if (Screen.AllScreens[i].Bounds.Contains(this.Location))
                    {
                        screenIndex = i;
                        break;
                    }
                }
                config.AppSettings.Settings["last_screen"].Value = screenIndex.ToString();

                config.Save(ConfigurationSaveMode.Modified);
            }
            catch (Exception ex)
            {
                // Si hay error al guardar, solo lo ignoramos (no interrumpir el cierre)
                System.Diagnostics.Debug.WriteLine($"Error guardando configuración: {ex.Message}");
            }
        }
        private void EnsureConfigKeys()
        {
            // < add key = "last_screen" value = "0" />
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            string[] keys = { "window_left", "window_top", "window_width", "window_height", "last_screen" };

            foreach (string key in keys)
            {
                if (config.AppSettings.Settings[key] == null)
                {
                    string defaultValue = "";

                    // Switch tradicional compatible con C# 7.2
                    switch (key)
                    {
                        case "window_left":
                            defaultValue = "100";
                            break;
                        case "window_top":
                            defaultValue = "100";
                            break;
                        case "window_width":
                            defaultValue = "1024";
                            break;
                        case "window_height":
                            defaultValue = "768";
                            break;
                        case "last_screen":
                            defaultValue = "0";
                            break;
                    }

                    config.AppSettings.Settings.Add(key, defaultValue);
                }
            }

            config.Save(ConfigurationSaveMode.Modified);
        }

    }
}