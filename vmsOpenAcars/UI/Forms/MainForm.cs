using System;
using System.Collections.Generic;
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
using System.Reflection;
using System.Diagnostics;
using vmsOpenAcars.Core.Helpers;

namespace vmsOpenAcars.UI.Forms
{
    public partial class MainForm : Form
    {
        private bool _dragging = false;
        private Point _dragStartPoint;

        // ========== CONTROLES ==========
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
        private NotifyIcon _notifyIcon;

        // ========== VIEWMODEL Y SERVICIOS ==========
        private MainViewModel _viewModel;
        private UIService _uiService;

        public MainForm()
        {
            EnsureAllConfigKeys();
            InitializeForm();
            InitializeLayout();
            InitializeHeader();
            InitializeMessageSection();
            InitializeFmaPanel();
            InitializeStatusSection();
            InitializeButtons();
            InitializeViewModel();
            ConnectViewModelEvents();
            _viewModel?.Start();
        }

        #region Inicialización

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
            _viewModel.OnShowMessage += (message, title) =>
            {
                MessageBox.Show(message, title);
            };

            _viewModel.OnShowConfirmation += async (message, title, buttons) =>
            {
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

                await Task.Delay(10);
                return result;
            };

        }

        #endregion

        #region Métodos de UI

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

        private void RefreshUILanguage()
        {
            btnLogin.Text = _("BtnLogin");
            btnSimbrief.Text = _("BtnSimbrief");
            btnStartStop.Text = _("BtnStartStop");
            btnCancel.Text = _viewModel?.ActivePilot == null ? _("BtnExit") : _("BtnCancel");
        }

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

        #endregion

        #region Inicialización de UI (Tus métodos existentes)

        private void InitializeForm()
        {
            this.Text = "vmsOpenAcars - ACARS Flight Deck";
            this.Size = new Size(1024, 768);
            this.MinimumSize = new Size(800, 600);
            this.BackColor = Color.FromArgb(10, 10, 20);
            this.Font = new Font("Consolas", 10, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Padding = new Padding(2);

            // Icono
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

            // Posición guardada
            try
            {
                string savedLeft = ConfigurationManager.AppSettings["window_left"];
                string savedTop = ConfigurationManager.AppSettings["window_top"];
                string savedWidth = ConfigurationManager.AppSettings["window_width"];
                string savedHeight = ConfigurationManager.AppSettings["window_height"];

                int left, top, width, height;

                // Verificar que TODAS las claves existen y tienen valores válidos
                if (!string.IsNullOrEmpty(savedLeft) && !string.IsNullOrEmpty(savedTop) &&
                    !string.IsNullOrEmpty(savedWidth) && !string.IsNullOrEmpty(savedHeight))
                {
                    if (int.TryParse(savedLeft, out left) && int.TryParse(savedTop, out top) &&
                        int.TryParse(savedWidth, out width) && int.TryParse(savedHeight, out height))
                    {
                        Rectangle windowRect = new Rectangle(left, top, width, height);
                        if (IsValidScreenPosition(windowRect))
                        {
                            this.StartPosition = FormStartPosition.Manual;
                            this.Location = new Point(left, top);
                            this.Size = new Size(width, height);
                        }
                        else
                        {
                            CenterInScreen(GetSavedScreenIndex());
                        }
                    }
                    else
                    {
                        CenterInScreen(GetSavedScreenIndex());
                    }
                }
                else
                {
                    CenterInScreen(GetSavedScreenIndex());
                }
            }
            catch
            {
                CenterInScreen(0);
            }

            // Borde
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

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));   // Message (datos)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // FMA
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // Incoming Msg
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Buttons

            this.Controls.Add(mainLayout);
        }

        private void InitializeNotifications()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = this.Icon, // Usa el mismo icono de la aplicación
                Visible = true,
                BalloonTipTitle = "vmsOpenAcars",
                BalloonTipIcon = ToolTipIcon.Info
            };
        }

        // Método público para mostrar notificaciones
        public void ShowNotification(string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_notifyIcon == null) return;

            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(3000); // 3 segundos
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
                Size = new Size(40, 40),
                Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            // Cargar el logo
            try
            {
                string logoPath = Path.Combine(Application.StartupPath, "logo.png");
                if (File.Exists(logoPath))
                {
                    pbLogo.Image = Image.FromFile(logoPath);
                }
                else
                {
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
                _uiService?.AddLog($"⚠️ Error cargando logo: {ex.Message}", Theme.Warning);
            }

            // ===== TÍTULO PRINCIPAL =====
            lblTitle = new Label
            {
                Text = _("MainTitle"),
                Font = new Font("Consolas", 16, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(60, 10),
                AutoSize = true
            };

            // ===== NOMBRE DE AEROLÍNEA =====
            string airlineName = ConfigurationManager.AppSettings["airline"] ?? "vmsOpenAcars";
            lblSubTitle = new Label
            {
                Text = airlineName,
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.LightGreen,
                Location = new Point(60, 35),
                AutoSize = true
            };

            // ===== LABEL DE VERSIÓN (a la izquierda del engranaje) =====
            Label lblVersion = new Label
            {
                Text = $"v{AppInfo.Version}",
                Font = new Font("Consolas", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                // La posición Y se ajustará en el evento Resize
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // ===== BOTÓN DE CONFIGURACIÓN =====
            Button btnSettings = new Button
            {
                Text = "⚙️",
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                Size = new Size(40, 40),
                BackColor = Color.Transparent,
                ForeColor = Color.Cyan,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += BtnSettings_Click;

            // Agregar controles al panel
            pnlHeader.Controls.AddRange(new Control[] {
        pbLogo, lblTitle, lblSubTitle, lblVersion, btnSettings
    });

            mainLayout.Controls.Add(pnlHeader, 0, 0);

            // Ajustar posiciones al redimensionar
            pnlHeader.Resize += (s, e) =>
            {
                int rightMargin = 10;
                int settingsWidth = 40;
                int spacing = 8;

                // Posicionar el botón de configuración
                btnSettings.Location = new Point(pnlHeader.Width - settingsWidth - rightMargin, 10);

                // Calcular el centro vertical del botón
                int buttonCenterY = btnSettings.Top + (btnSettings.Height / 2);

                // Posicionar la versión a la izquierda del botón, centrada verticalmente
                lblVersion.Location = new Point(
                    btnSettings.Left - lblVersion.Width - spacing,
                    buttonCenterY - (lblVersion.Height / 2)
                );
            };

            // Forzar el ajuste inicial
            pnlHeader.Resize += (s, e) => { }; // Solo para invocar el evento

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
                    string airline = ConfigurationManager.AppSettings["airline"] ?? "vmsOpenAcars";
                    lblSubTitle.Text = airline;
                    RefreshUILanguage();
                }
            }
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

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            for (int i = 0; i < 4; i++)
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblFlightNo = CreateInfoLabel("FLT NO: ----", FontStyle.Bold);
            _lblDepArr = CreateInfoLabel("DEP/ARR: ----/----", FontStyle.Bold);
            _lblAlternate = CreateInfoLabel("ALTN: ----", FontStyle.Bold);
            _lblRoute = CreateInfoLabel("ROUTE: ----", FontStyle.Bold);
            _lblAircraft = CreateInfoLabel("ACFT: ----", FontStyle.Bold);
            _lblFuel = CreateInfoLabel("FUEL: ----   ", FontStyle.Bold);
            _lblType = CreateInfoLabel("TYPE: ----", FontStyle.Bold);
            _lblRegistration = CreateInfoLabel("REG: ----", FontStyle.Bold);

            tlp.Controls.Add(_lblFlightNo, 0, 0);
            tlp.Controls.Add(_lblDepArr, 1, 0);
            tlp.Controls.Add(_lblAlternate, 2, 0);
            tlp.Controls.Add(_lblRoute, 3, 0);
            tlp.Controls.Add(_lblAircraft, 0, 1);
            tlp.Controls.Add(_lblFuel, 1, 1);
            tlp.Controls.Add(_lblType, 2, 1);
            tlp.Controls.Add(_lblRegistration, 3, 1);

            pnlMessageInfo.Controls.Add(tlp);

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

            txtIncomingMsg = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            txtIncomingMsg.PreviewKeyDown += (s, e) => e.IsInputKey = false;
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

            mainLayout.Controls.Add(pnlIncoming, 0, 2);
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
                Location = new Point(col2X, startY + lineHeight * 3),
                AutoSize = true
            };

            lblSimName = new Label
            {
                Text = $"SIM: {_("Waiting")}",
                Font = new Font("Consolas", 10),
                ForeColor = Color.Cyan,
                Location = new Point(col1X, startY + lineHeight * 3),
                AutoSize = true
            };

            pnlStatus.Controls.AddRange(new Control[] {
                lblStatusTitle, lblAcarsStatus, lblEtd, lblPos, lblComm,
                lblUplink, lblValidationStatus, lblProgress, lblCurrentAirport, lblSimName
            });

            mainLayout.Controls.Add(pnlStatus, 0, 4);
        }

        private Label CreateStatusLabel(string label, string value, int x, int y, Color? color = null)
        {
            if (color == null)
                color = Color.LightGreen;

            return new Label
            {
                Text = $"{label}  {value}",
                Font = new Font("Consolas", 11),
                ForeColor = color.Value,
                Location = new Point(x, y),
                AutoSize = true
            };
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
                Color.FromArgb(60, 70, 80),
                Color.FromArgb(0, 120, 200),
                Color.FromArgb(60, 70, 80),
                Color.FromArgb(0, 100, 200),
                Color.FromArgb(60, 70, 80),
                Color.FromArgb(60, 70, 80),
                Color.FromArgb(0, 150, 0),
                Color.FromArgb(200, 100, 0),
                Color.FromArgb(150, 0, 0)
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

        #endregion

        /// <summary>
        /// Actualiza los labels del panel FLIGHT INFORMATION con los datos del plan activo
        /// </summary>
        public void UpdateFlightInfoPanel()
        {
            if (_viewModel?.FlightManager?.ActivePlan == null)
            {
                // Si no hay plan activo, mostrar valores por defecto
                _lblFlightNo.Text = "FLT NO: ----";
                _lblDepArr.Text = "DEP/ARR: ----/----";
                _lblAlternate.Text = "ALTN: ----";
                _lblRoute.Text = "ROUTE: ----";
                _lblAircraft.Text = "ACFT: ----";
                _lblFuel.Text = "FUEL: ---- kg";
                _lblType.Text = "TYPE: ----";
                _lblRegistration.Text = "REG: ----";
                return;
            }

            var plan = _viewModel.FlightManager.ActivePlan;

            // Actualizar los labels con los datos del plan
            _lblFlightNo.Text = $"FLT NO: {plan.Airline}{plan.FlightNumber}";
            _lblDepArr.Text = $"DEP/ARR: {plan.Origin}/{plan.Destination}";
            _lblAlternate.Text = $"ALTN: {plan.Alternate ?? "N/A"}";
            _lblRoute.Text = $"ROUTE: {plan.Route}";
            _lblAircraft.Text = $"ACFT: {plan.AircraftIcao}";
            _lblFuel.Text = $"FUEL: {plan.BlockFuel:F0} {plan.Units ?? "kg"}";
            _lblType.Text = $"TYPE: {plan.Aircraft}";
            _lblRegistration.Text = $"REG: {plan.Registration}";
        }

        #region Eventos de Botones

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            _viewModel?.Login();
        }

        private async void BtnSimbrief_Click(object sender, EventArgs e)
        {
            if (_viewModel?.FlightManager?.ActivePilot == null)
            {
                _uiService.AddLog("⚠️ Debes iniciar sesión primero", Theme.Warning);
                return;
            }

            using (var planner = new FlightPlannerForm(
                _viewModel.ApiService,
                _viewModel.PhpVmsFlightService,
                _viewModel.SimbriefEnhancedService,
                _viewModel.FlightManager,
                _viewModel.FlightManager.ActivePilot,
                _viewModel.FlightManager.CurrentAirport))
            {
                if (planner.ShowDialog(this) == DialogResult.OK)
                {
                    // PRIORIDAD 1: Usar el plan completo de SimBrief si existe
                    var completePlan = planner.GetLoadedPlan();
                    if (completePlan != null)
                    {
                        _viewModel.SetActivePlan(completePlan);
                        _uiService.AddLog($"✅ Plan loaded: {completePlan.Origin} → {completePlan.Destination}", Theme.Success);
                        UpdateFlightInfoPanel();
                        return; // Salir, ya tenemos el plan completo
                    }

                    // PRIORIDAD 2: Si no hay plan, usar el vuelo seleccionado (plan básico)
                    var selectedFlight = planner.GetSelectedFlight();
                    if (selectedFlight != null)
                    {
                        _viewModel.LoadFlightFromBid(selectedFlight);
                        _uiService.AddLog($"✅ Flight selected: {selectedFlight.Airline}{selectedFlight.FlightNumber}", Theme.Success);
                        UpdateFlightInfoPanel();
                    }
                }
            }
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

        #endregion

        #region Cierre

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _viewModel?.Stop();
            base.OnFormClosing(e);

            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Asegurar que las claves existen antes de asignar
                EnsureConfigKey(config, "window_left", this.Location.X.ToString());
                EnsureConfigKey(config, "window_top", this.Location.Y.ToString());
                EnsureConfigKey(config, "window_width", this.Size.Width.ToString());
                EnsureConfigKey(config, "window_height", this.Size.Height.ToString());

                int screenIndex = GetCurrentScreenIndex();
                EnsureConfigKey(config, "last_screen", screenIndex.ToString());

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                Debug.WriteLine("✅ Configuración guardada correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error guardando configuración: {ex.Message}");
            }
        }

        private void EnsureConfigKey(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] != null)
            {
                config.AppSettings.Settings[key].Value = value;
            }
            else
            {
                config.AppSettings.Settings.Add(key, value);
            }
        }

        private int GetCurrentScreenIndex()
        {
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                if (Screen.AllScreens[i].Bounds.Contains(this.Location))
                    return i;
            }
            return 0;
        }

        #endregion
        private bool IsValidScreenPosition(Rectangle windowRect)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(windowRect))
                    return true;
            }
            return false;
        }

        private int GetSavedScreenIndex()
        {
            string savedScreen = ConfigurationManager.AppSettings["last_screen"];
            if (int.TryParse(savedScreen, out int index) && index >= 0 && index < Screen.AllScreens.Length)
                return index;
            return 0;
        }
        private void EnsureAllConfigKeys()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            string[] defaultKeys = {
                "window_left", "window_top",
                "window_width", "window_height",
                "last_screen"
            };

            string[] defaultValues = {
                "100", "100", "1024", "768", "0"
            };

            for (int i = 0; i < defaultKeys.Length; i++)
            {
                if (config.AppSettings.Settings[defaultKeys[i]] == null)
                {
                    config.AppSettings.Settings.Add(defaultKeys[i], defaultValues[i]);
                }
            }

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

    }
}