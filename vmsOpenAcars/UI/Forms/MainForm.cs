using System;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using vmsOpenAcars.ViewModels;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.UI.Forms
{
    public partial class MainForm : Form
    {
        private bool _dragging = false;
        private Point _dragStartPoint;

        // ========== CONTROLES (TODOS TUS CONTROLES EXISTENTES) ==========
        private TableLayoutPanel mainLayout;
        private Panel pnlHeader;
        private Panel pnlMessage;
        private Panel pnlFma;
        private Panel pnlStatus;
        private Panel pnlButtons;
        private Panel pnlMessageInfo;
        private Panel pnlIncoming;
        public Label lblTitle;
        public Label lblSubTitle;
        public Label _lblFlightNo;
        public Label _lblDepArr;
        public Label _lblDuration;
        public Label lblPhase;
        public Label lblSpeed;
        public Label lblAltitude;
        public Label lblAir;
        public Label lblRoute;
        public Label _lblRoute;
        public Label _lblFuel;
        public Label _lblAircraft;
        public Label _lblAlternate;
        public Label _lblType;
        public Label _lblRegistration;
        public RichTextBox txtIncomingMsg;
        public Label lblStatusTitle;
        public Label lblAcarsStatus;
        public Label lblEtd;
        public Label lblPos;
        public Label lblComm;
        public Label lblUplink;
        public Label lblProgress;
        public Label lblValidationStatus;
        public Label lblCurrentAirport;
        public Label lblSimName;
        private Button btnMenu;
        private Button btnLogin;
        private Button btnAtis;
        private Button btnOfp;
        private Button btnMsg;
        private Button btnWeather;
        private Button btnSimbrief;
        public Button btnStartStop;
        private Button btnCancel;

        // ========== NUEVO: ViewModel y Servicios ==========
        private MainViewModel _viewModel;
        private UIService _uiService;

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

            // ===== INICIALIZAR VIEWMODEL Y SERVICIOS =====
            InitializeViewModel();

            // ===== CONECTAR EVENTOS =====
            ConnectViewModelEvents();

            // ===== INICIAR TIMERS =====
            _viewModel?.Start();
        }

        private void InitializeViewModel()
        {
            try
            {
                string apiUrl = ConfigurationManager.AppSettings["vms_api_url"];
                string apiKey = ConfigurationManager.AppSettings["vms_api_key"];

                if (!string.IsNullOrEmpty(apiUrl) && !string.IsNullOrEmpty(apiKey))
                {
                    var apiService = new ApiService(apiUrl, apiKey);
                    var flightManager = new FlightManager(apiService);
                    var fsuipc = new FsuipcService();
                    var phpVmsFlightService = new PhpVmsFlightService(apiService);
                    var simbriefEnhancedService = new SimbriefEnhancedService(apiService);

                    _viewModel = new MainViewModel(flightManager, fsuipc, apiService, phpVmsFlightService, simbriefEnhancedService);
                    _uiService = new UIService(this, flightManager);

                    // flightManager.OnLog += _uiService.AddLog;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inicializando servicios: {ex.Message}");
            }
        }

        private void ConnectViewModelEvents()
        {
            if (_viewModel == null || _uiService == null) return;

            _viewModel.OnLog += _uiService.AddLog;
            _viewModel.OnPositionUpdate += _uiService.UpdatePosition;
            _viewModel.OnPhaseChanged += _uiService.UpdatePhase;
            _viewModel.OnAirStatusChanged += _uiService.UpdateAirStatus;
            _viewModel.OnAltitudeChanged += _uiService.UpdateAltitude;
            _viewModel.OnSpeedChanged += _uiService.UpdateSpeed;
            _viewModel.OnValidationStatusChanged += _uiService.UpdateValidationUI;
            _viewModel.OnFlightInfoChanged += _uiService.UpdateFlightInfo;
            _viewModel.OnSimulatorNameChanged += _uiService.UpdateSimulatorName;
            _viewModel.OnAcarsStatusChanged += _uiService.UpdateAcarsStatus;
            _viewModel.OnAirportChanged += _uiService.UpdateCurrentAirport;
            _viewModel.OnButtonStateChanged += UpdateButtonState;
            _viewModel.OnOpenFlightPlanner += () =>
            {
                var planner = new FlightPlannerForm(
                    _viewModel.PhpVmsFlightService,
                    _viewModel.SimbriefEnhancedService,
                    _viewModel.FlightManager,
                    _viewModel.FlightManager.ActivePilot,
                    _viewModel.FlightManager.CurrentAirport
                );

                if (planner.ShowDialog() == DialogResult.OK)
                {
                    var plan = planner.GetLoadedPlan();
                    if (plan != null)
                    {
                        _viewModel.SetActivePlan(plan);
                        //_viewModel.FlightManager.SetActivePlan(plan);
                        //_viewModel.UpdateFlightInfo(); 
                        //_uiService.UpdateValidationUI(_viewModel.FlightManager.PositionValidationStatus);
                    }
                }
            };

            _viewModel.OnShowMessage += (message, title) =>
            {
                MessageBox.Show(message, title);
            };
            _viewModel.OnShowConfirmation += async (message, title, buttons) =>
            {
                // Como esto se llama desde un hilo de Task, necesitamos Invoke
                DialogResult result = DialogResult.None;

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        result = EcamDialog.Show(this, message, title, buttons);
                    }));
                }
                else
                {
                    result = EcamDialog.Show(this, message, title, buttons);
                }

                // Esperar un momento para que el diálogo se cierre (opcional)
                await Task.Delay(10);
                return result;
            };
        }

        // ===== TODOS TUS MÉTODOS DE INICIALIZACIÓN DE UI (sin cambios) =====
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
                    using (Bitmap bitmap = new Bitmap(iconPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        this.Icon = Icon.FromHandle(hIcon);
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
                string savedLeft = ConfigurationManager.AppSettings["window_left"];
                string savedTop = ConfigurationManager.AppSettings["window_top"];
                string savedWidth = ConfigurationManager.AppSettings["window_width"];
                string savedHeight = ConfigurationManager.AppSettings["window_height"];

                int left, top, width, height;

                if (!string.IsNullOrEmpty(savedLeft) && !string.IsNullOrEmpty(savedTop) &&
                    !string.IsNullOrEmpty(savedWidth) && !string.IsNullOrEmpty(savedHeight))
                {
                    if (int.TryParse(savedLeft, out left) && int.TryParse(savedTop, out top) &&
                        int.TryParse(savedWidth, out width) && int.TryParse(savedHeight, out height))
                    {
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
                _uiService.AddLog($"⚠️ Error cargando logo: {ex.Message}", Theme.Warning);
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

                    // Refrescar textos de la UI
                    RefreshUILanguage();
                }
            }
        }

        private void RefreshUILanguage()
        {
            btnLogin.Text = _("BtnLogin");
            btnSimbrief.Text = _("BtnSimbrief");
            btnStartStop.Text = _("BtnStartStop");

            // Usar el ViewModel en lugar de _flightManager
            bool pilotIsNull = _viewModel?.ActivePilot == null;
            btnCancel.Text = pilotIsNull ? _("BtnExit") : _("BtnCancel");
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

        // ===== MÉTODOS AUXILIARES =====
        private void UpdateButtonState(string buttonText, Color backColor, bool enabled)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateButtonState(buttonText, backColor, enabled)));
                return;
            }

            btnStartStop.Text = buttonText;
            btnStartStop.BackColor = backColor;
            btnStartStop.Enabled = enabled;
        }

        private void EnsureConfigKeys() { /* tu código existente */ }

        // ===== EVENTOS DE BOTONES (SOLO DELEGAN EN VIEWMODEL) =====
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            _viewModel?.Login();
        }

        private void BtnSimbrief_Click(object sender, EventArgs e)
        {
            _viewModel?.OpenFlightPlanner();
        }

        private async void BtnStartStop_Click(object sender, EventArgs e)
        {
            await _viewModel?.HandleStartStopButton(btnStartStop.Text);
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
            else
            {
                _viewModel?.CancelFlight();
            }
        }

        private void BtnOfp_Click(object sender, EventArgs e)
        {
            _viewModel?.ShowOFP();
        }

        private void GenericButton_Click(object sender, EventArgs e)
        {
            var btn = sender as Button;
            _viewModel?.LogButtonPress(btn?.Text);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _viewModel?.Stop();

            base.OnFormClosing(e);

            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Guardar posición y tamaño
                config.AppSettings.Settings["window_left"].Value = this.Location.X.ToString();
                config.AppSettings.Settings["window_top"].Value = this.Location.Y.ToString();
                config.AppSettings.Settings["window_width"].Value = this.Size.Width.ToString();
                config.AppSettings.Settings["window_height"].Value = this.Size.Height.ToString();

                // Guardar índice de pantalla
                int screenIndex = 0;
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    if (Screen.AllScreens[i].Bounds.Contains(this.Location))
                    {
                        screenIndex = i;
                        break;
                    }
                }

                if (config.AppSettings.Settings["last_screen"] != null)
                    config.AppSettings.Settings["last_screen"].Value = screenIndex.ToString();
                else
                    config.AppSettings.Settings.Add("last_screen", screenIndex.ToString());

                config.Save(ConfigurationSaveMode.Modified);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error guardando configuración: {ex.Message}");
            }
        }
    }
}