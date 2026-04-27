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
using vmsOpenAcars.Core.Helpers;
using System.IO.Compression;
using System.Net.Http;
using vmsOpenAcars;


namespace vmsOpenAcars.UI.Forms
{
    /// <summary>
    /// Panel con double-buffering activado para eliminar parpadeo en actualizaciones frecuentes.
    /// </summary>
    public sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
    public partial class MainForm : Form
    {
        private bool _dragging = false;
        private Point _dragStartPoint;

        // ========== CONTROLES ==========
        private TableLayoutPanel mainLayout;
        private BufferedPanel pnlHeader;
        private BufferedPanel pnlMessage;
        private BufferedPanel pnlFma;
        private BufferedPanel pnlStatus;
        private BufferedPanel pnlButtons;
        private BufferedPanel pnlMessageInfo;
        private BufferedPanel pnlIncoming;
        public Label lblTitle;
        public Label lblSubTitle;
        public Label _lblFlightNo;
        public Label _lblDepArr;
        public Label _lblDuration;
        public Label lblPhase;
        public Label lblAir;
        public Label lblRoute;
        public Label _lblRoute;
        public Label _lblFuel;
        public Label _lblAircraft;
        public Label _lblAlternate;
        public Label _lblType;
        public Label _lblRegistration;
        public RichTextBox txtIncomingMsg;
        public Label lblAcarsStatus;
        public Label lblPos;
        public Label _lblProgress;
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
        private bool _isFlightActive = false;

        // Controles del Flight Information Panel - PROGRESO
        private Label _lblRestante;
        private Label _lblEta;
        private Label _lblTiempoAire;

        // AERODINÁMICA
        private Label _lblIas;
        private Label _lblGs;
        private Label _lblVs;
        private Label _lblMach;

        // FUEL
        private Label _lblFuelInit;
        private Label _lblFuelCurrent;
        private Label _lblFuelUsed;

        // ALTITUD
        private Label _lblAltitudeVal;
        private Label _lblCruiseVal;
        private Label _lblAglVal;
        private Label _lblQnhVal;

        // CLIMA
        private Label _lblWind;
        private Label _lblOat;
        private Label _lblTat;
        private Label _lblTr;

        // SISTEMAS
        private Label _lblGear;
        private Label _lblFlaps;
        private Label _lblSpoilers;
        private Label _lblAutobrake;
        private Label _lblSeatBelt;
        private Label _lblAutopilot;
        private Label _lblStabilized;

        // LUCES
        private Label _lblNavLight;
        private Label _lblBeaconLight;
        private Label _lblLandingLight;
        private Label _lblTaxiLight;
        private Label _lblStrobeLight;

        // Motores compacto
        private Label _lblEng1Val;
        private Label _lblEng2Val;

        // MOTORES
        private Controls.EngineMonitorPanel _engineMonitorPanel;


        // ========== VIEWMODEL Y SERVICIOS ==========
        private MainViewModel _viewModel;
        private UIService _uiService;

        public MainForm()
        {
            SetStyle(
                ControlStyles.DoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint,
                true);
            UpdateStyles();
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
            // Verificar actualizaciones al iniciar (no bloquea la UI)
            this.Shown += async (s, e) => await CheckForUpdatesAsync();
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
                    var weatherService = new WeatherService();
                    var flightManager = new FlightManager(apiService, weatherService);
                    var fsuipc = new FsuipcService();
                    var phpVmsFlightService = new PhpVmsFlightService(apiService);
                    var simbriefEnhancedService = new SimbriefEnhancedService(apiService);

                    _viewModel = new MainViewModel(flightManager, fsuipc, apiService, phpVmsFlightService, simbriefEnhancedService);
                    _uiService = new UIService(this, flightManager, apiService);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inicializando servicios: {ex.Message}");
            }
            _viewModel.OnFlightStarted += () =>
            {
                _isFlightActive = true;
                UpdateCancelButton();
            };

            _viewModel.OnFlightEnded += () =>
            {
                _isFlightActive = false;
                UpdateCancelButton();
            };
        }
        private void UpdateCancelButton()
        {
            if (btnCancel.InvokeRequired)
            {
                btnCancel.Invoke(new Action(UpdateCancelButton));
                return;
            }

            if (_isFlightActive)
            {
                btnCancel.Text = "CANCEL";
                btnCancel.BackColor = Color.FromArgb(150, 0, 0);
            }
            else
            {
                btnCancel.Text = "EXIT";
                btnCancel.BackColor = Color.FromArgb(100, 0, 0);
            }
        }

        private void ConnectViewModelEvents()
        {
            if (_viewModel == null || _uiService == null) return;

            _viewModel.OnLog += _uiService.AddLog;
            _viewModel.OnPositionUpdate += _uiService.UpdatePosition;
            _viewModel.OnPhaseChanged += _uiService.UpdatePhase;
            _viewModel.OnAirStatusChanged += _uiService.UpdateAirStatus;
            _viewModel.OnValidationStatusChanged += _uiService.UpdateValidationUI;
            _viewModel.OnFlightInfoChanged += () => _uiService.UpdateFlightInfo();
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

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // Row 0: Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // Row 1: FMA
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));  // Row 2: Message (datos)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Row 3: Incoming Msg
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // Row 4: Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // Row 5: Status Bar

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
            pnlHeader = new BufferedPanel
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
            // ===== PANEL FLIGHT INFORMATION =====
            pnlMessageInfo = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(3),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTitle = new Label
            {
                Text = "FLIGHT INFORMATION",
                Font = new Font("Consolas", 10, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlMessageInfo.Controls.Add(lblTitle);

            // Layout principal — una sola columna (motores en topGrid)
            var mainGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(2),
                BackColor = Color.Transparent
            };
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(2),
                BackColor = Color.Transparent
            };
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

            // ===== FILA 1: PROGRESO · AERODINÁMICA · FUEL · MOTORES =====
            var topGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(1),
                BackColor = Color.Transparent
            };
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));

            var progressPanel = CreateCompactInfoCard("📊 PROGRESO", out _lblProgress, out _lblRestante, out _lblTiempoAire);
            _lblProgress.Text = "DIST: 0/0 NM"; _lblRestante.Text = "REST: 0 NM / ETA: --:--"; _lblTiempoAire.Text = "⏱️ T/A: 00:00:00";
            topGrid.Controls.Add(progressPanel, 0, 0);

            var aeroPanel = CreateCompactInfoCard("✈️ AERODINÁMICA", out _lblIas, out _lblGs, out _lblVs);
            _lblIas.Text = "IAS: --- kt"; _lblGs.Text = "GS: --- kt / MACH: ----"; _lblVs.Text = "VS: ---- fpm";
            topGrid.Controls.Add(aeroPanel, 1, 0);

            var fuelPanel = CreateCompactInfoCard("⛽ FUEL", out _lblFuelInit, out _lblFuelCurrent, out _lblFuelUsed);
            _lblFuelInit.Text = "INI: 0 kg"; _lblFuelCurrent.Text = "ACT: 0 kg"; _lblFuelUsed.Text = "USO: 0 kg";
            topGrid.Controls.Add(fuelPanel, 2, 0);

            topGrid.Controls.Add(CreateMotorCompactPanel(), 3, 0);
            leftGrid.Controls.Add(topGrid, 0, 0);

            // ===== FILA 2: ALTITUD · SISTEMAS · LUCES =====
            var bottomGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(1),
                BackColor = Color.Transparent
            };
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

            var altPanel = CreateInfoCard("📈 ALTITUD", out _lblAltitudeVal, out _lblAglVal, out _lblCruiseVal, out _lblQnhVal);
            _lblAltitudeVal.Text = "ALT: 0 ft"; _lblAglVal.Text = "AGL: 0 ft";
            _lblCruiseVal.Text = "CRZ: ----"; _lblQnhVal.Text = "QNH: --- hPa";
            bottomGrid.Controls.Add(altPanel, 0, 0);
            bottomGrid.Controls.Add(CreateSistemasPanel(), 1, 0);
            bottomGrid.Controls.Add(CreateLucesPanel(), 2, 0);

            leftGrid.Controls.Add(bottomGrid, 0, 1);
            mainGrid.Controls.Add(leftGrid, 0, 0);

            pnlMessageInfo.Controls.Add(mainGrid);
            mainLayout.Controls.Add(pnlMessageInfo, 0, 2);

            // ===== PANEL DE MENSAJES ENTRANTES (INCOMING MSG) =====
            pnlIncoming = new BufferedPanel
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
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlIncoming.Controls.Add(lblIncomingTitle);

            mainLayout.Controls.Add(pnlIncoming, 0, 3);
        }

        private Panel CreateCompactInfoCard(string title, out Label label1, out Label label2, out Label label3)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 25, 35), Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Color.Transparent,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            for (int i = 0; i < 3; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador: absorbe altura sobrante

            layout.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor = Color.Gold,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);

            label1 = MakeSysLabel("");
            label2 = MakeSysLabel("");
            label3 = MakeSysLabel("");

            layout.Controls.Add(label1, 0, 1);
            layout.Controls.Add(label2, 0, 2);
            layout.Controls.Add(label3, 0, 3);

            panel.Controls.Add(layout);
            return panel;
        }
        /// <summary>Panel SISTEMAS — título en fila 0 del TLP para evitar que tape la fila 1.</summary>
        private Panel CreateSistemasPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 25, 35), Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                BackColor = Color.Transparent,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            for (int i = 0; i < 7; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label { Text = "🛠️ SISTEMAS", Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = Color.Gold, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);

            _lblGear = MakeSysLabel("GEAR: ---");
            _lblFlaps = MakeSysLabel("FLAPS: --");
            _lblSpoilers = MakeSysLabel("SPOIL: RET");
            _lblAutobrake = MakeSysLabel("A/BRK: ---");
            _lblSeatBelt = MakeSysLabel("🔔 SEATBLT: OFF");
            _lblAutopilot = MakeSysLabel("A/P: OFF");
            _lblStabilized = MakeSysLabel("");

            layout.Controls.Add(_lblGear, 0, 1);
            layout.Controls.Add(_lblFlaps, 0, 2);
            layout.Controls.Add(_lblSpoilers, 0, 3);
            layout.Controls.Add(_lblAutobrake, 0, 4);
            layout.Controls.Add(_lblSeatBelt, 0, 5);
            layout.Controls.Add(_lblAutopilot, 0, 6);
            layout.Controls.Add(_lblStabilized, 0, 7);
            panel.Controls.Add(layout);
            return panel;
        }

        /// <summary>Panel LUCES — título en fila 0 del TLP para evitar que tape la fila 1.</summary>
        private Panel CreateLucesPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 25, 35), Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = Color.Transparent,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            for (int i = 0; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label { Text = "💡 LUCES", Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = Color.Gold, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);

            _lblNavLight = MakeSysLabel("● NAV: OFF");
            _lblBeaconLight = MakeSysLabel("● BCN: OFF");
            _lblLandingLight = MakeSysLabel("● LAND: OFF");
            _lblTaxiLight = MakeSysLabel("● TAXI: OFF");
            _lblStrobeLight = MakeSysLabel("● STROBE: OFF");

            layout.Controls.Add(_lblNavLight, 0, 1);
            layout.Controls.Add(_lblBeaconLight, 0, 2);
            layout.Controls.Add(_lblLandingLight, 0, 3);
            layout.Controls.Add(_lblTaxiLight, 0, 4);
            layout.Controls.Add(_lblStrobeLight, 0, 5);
            panel.Controls.Add(layout);
            return panel;
        }

        /// <summary>Panel MOTORES — título en fila 0 del TLP para evitar que tape la fila 1.</summary>
        private Panel CreateMotorCompactPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 25, 35), Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label { Text = "🚀 MOTORES", Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = Color.Gold, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);

            _lblEng1Val = MakeSysLabel("ENG1: ---");
            _lblEng2Val = MakeSysLabel("ENG2: ---");
            layout.Controls.Add(_lblEng1Val, 0, 1);
            layout.Controls.Add(_lblEng2Val, 0, 2);
            panel.Controls.Add(layout);
            return panel;
        }

        /// <summary>Label estándar para paneles de sistema.</summary>
        private static Label MakeSysLabel(string text) => new Label
        {
            Text = text,
            Font = new Font("Consolas", 8, FontStyle.Bold),
            ForeColor = Color.LightGreen,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            Margin = Padding.Empty
        };
        private Panel CreateEnginePanelCompact()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 25, 35),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            // Habilitar DoubleBuffering
            panel.GetType().GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(pnlMessageInfo, true, null);

            // Usar TableLayoutPanel para controlar el layout vertical
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 15));  // Espaciador superior
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));  // Título
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Motores

            // Espaciador
            var topSpacer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            layout.Controls.Add(topSpacer, 0, 0);

            // Título
            var lblTitle = new Label
            {
                Text = "🚀 MOTORES",
                Font = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor = Color.Gold,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(3, 0, 0, 0)
            };
            layout.Controls.Add(lblTitle, 0, 1);

            // Panel de motores
            _engineMonitorPanel = new Controls.EngineMonitorPanel
            {
                Dock = DockStyle.Fill,
                EngineCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.Controls.Add(_engineMonitorPanel, 0, 2);

            panel.Controls.Add(layout);
            return panel;
        }
        private Panel CreateInfoCard(string title, out Label label1, out Label label2, out Label label3, out Label label4)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 25, 35),
                Margin = new Padding(1)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.Transparent,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            for (int i = 0; i < 4; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor = Color.Gold,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            }, 0, 0);

            label1 = MakeSysLabel("");
            label2 = MakeSysLabel("");
            label3 = MakeSysLabel("");
            label4 = MakeSysLabel("");

            layout.Controls.Add(label1, 0, 1);
            layout.Controls.Add(label2, 0, 2);
            layout.Controls.Add(label3, 0, 3);
            layout.Controls.Add(label4, 0, 4);

            panel.Controls.Add(layout);
            return panel;
        }
        private void InitializeFmaPanel()
        {
            pnlFma = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.FMAPanelBackground
            };

            for (int i = 0; i < 3; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            pnlFma.Controls.Add(layout);

            lblPhase = CreateFmaLabel("PHASE", "STANDBY");
            lblAir = CreateFmaLabel("AIR", "GROUND");
            lblRoute = CreateFmaLabel("ROUTE", "----/----");

            layout.Controls.Add(lblPhase, 0, 0);
            layout.Controls.Add(lblAir, 1, 0);
            layout.Controls.Add(lblRoute, 2, 0);
            mainLayout.Controls.Add(pnlFma, 0, 1);
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
            pnlStatus = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 12, 20),
                Padding = new Padding(3, 0, 3, 0)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            lblSimName        = CreateStatusPill(_("Waiting"), Color.DimGray);
            lblAcarsStatus    = CreateStatusPill("⚠️ Offline", Color.Orange);
            lblCurrentAirport = CreateStatusPill("---", Color.DimGray);
            lblValidationStatus = CreateStatusPill("ICAO --- GPS ---", Color.DimGray);
            lblPos            = CreateStatusPill("---", Color.DimGray);

            flow.Controls.AddRange(new Control[] {
                lblSimName, lblAcarsStatus, lblCurrentAirport, lblValidationStatus, lblPos
            });

            pnlStatus.Controls.Add(flow);
            mainLayout.Controls.Add(pnlStatus, 0, 5);
        }

        private Label CreateStatusPill(string text, Color fore)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Consolas", 9),
                ForeColor = fore,
                BackColor = Color.FromArgb(28, 32, 48),
                AutoSize = true,
                Padding = new Padding(7, 2, 7, 2),
                Margin = new Padding(3, 3, 3, 3)
            };
        }

        private void InitializeButtons()
        {
            pnlButtons = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 40, 50),
                Padding = new Padding(4)
            };

            string[] buttonNames = { "MENU", "LOGIN", "ATIS", "OFP", "MSG", "WEATHER", "DISPATCH", "START", "CANCEL" };
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

            int xPos = 6;
            int buttonWidth = 90;
            int buttonHeight = 34;
            int spacing = 4;

            for (int i = 0; i < buttonNames.Length; i++)
            {
                Button btn = new Button
                {
                    Text = buttonNames[i],
                    Location = new Point(xPos, 6),
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
                    case "DISPATCH":
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

            mainLayout.Controls.Add(pnlButtons, 0, 4);
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

        public void UpdateFlightInfoPanel()
        {
            try
            {
                if (_viewModel?.FlightManager == null) return;

                var fm = _viewModel.FlightManager;
                var plan = fm.ActivePlan;

                // ===== CABECERA - Verificar que los controles existan =====
                if (_lblFlightNo != null)
                    _lblFlightNo.Text = (plan != null) ? $"FLT: {plan.Airline}{plan.FlightNumber}" : "FLT: ----";

                if (_lblDepArr != null)
                    _lblDepArr.Text = (plan != null) ? $"{plan.Origin} → {plan.Destination}" : "--- → ---";

                if (lblRoute != null)
                    lblRoute.Text = (plan != null)
                        ? $"ROUTE\n{plan.Origin} → {plan.Destination}"
                        : "ROUTE\n----/----";

                if (_lblAircraft != null)
                    _lblAircraft.Text = (plan != null) ? (plan.AircraftIcao ?? "----") : (fm.HasSimulatorData ? "CONNECTED" : "DISCONNECTED");

                if (_lblCruiseVal != null)
                    _lblCruiseVal.Text = (plan != null) ? $"CRZ: FL{plan.CruiseAltitude / 100}" : "CRZ: ----";

                // ===== FASE =====
                if (lblPhase != null)
                {
                    if (!string.IsNullOrEmpty(fm.ActivePirepId))
                    {
                        string phaseIcon = GetPhaseIcon(fm.CurrentPhase);
                        lblPhase.Text = $"{phaseIcon} {fm.CurrentPhase}";
                        lblPhase.ForeColor = GetPhaseColor(fm.CurrentPhase);
                    }
                    else
                    {
                        if (fm.HasSimulatorData)
                        {
                            lblPhase.Text = "🟢 SIM READY";
                            lblPhase.ForeColor = Color.Lime;
                        }
                        else
                        {
                            lblPhase.Text = "🔴 NO SIM";
                            lblPhase.ForeColor = Color.Red;
                        }
                    }
                }

                // ===== PROGRESO =====
                if (!string.IsNullOrEmpty(fm.ActivePirepId) && plan != null)
                {
                    double totalDistanceNm = plan.Distance > 0
                        ? plan.Distance
                        : (fm.ActivePlan?.Distance ?? 0);
                    double flownDistanceNm = fm.TotalDistanceKm * 0.539957;
                    double remainingNm = Math.Max(0, totalDistanceNm - flownDistanceNm);

                    if (_lblProgress != null) _lblProgress.Text = $"DIST: {flownDistanceNm:F0}/{totalDistanceNm:F0} NM";
                    if (_lblRestante != null) _lblRestante.Text = $"REST: {remainingNm:F0} NM / ETA: {GetEtaString(fm, remainingNm)}";
                    if (_lblTiempoAire != null) _lblTiempoAire.Text = $"⏱️ T/A: {fm.CurrentTimerDisplay}";
                }
                else
                {
                    if (_lblProgress != null) _lblProgress.Text = "DIST: --/-- NM";
                    if (_lblRestante != null) _lblRestante.Text = "REST: -- NM / ETA: --:--";
                    if (_lblTiempoAire != null) _lblTiempoAire.Text = "⏱️ T/A: 00:00:00";
                }

                // ===== AERODINÁMICA =====
                if (_lblIas != null) _lblIas.Text = $"IAS: {fm.CurrentIndicatedAirspeed} kt";
                if (_lblGs != null) _lblGs.Text = $"GS: {fm.CurrentGroundSpeed} kt / MACH: {GetMachString(fm)}";

                string vsSign = fm.CurrentVerticalSpeed >= 0 ? "+" : "";
                if (_lblVs != null)
                {
                    _lblVs.Text = $"VS: {vsSign}{fm.CurrentVerticalSpeed} fpm";
                    if (fm.CurrentVerticalSpeed > 500)
                        _lblVs.ForeColor = Color.Lime;
                    else if (fm.CurrentVerticalSpeed < -500)
                        _lblVs.ForeColor = Color.Orange;
                    else
                        _lblVs.ForeColor = Color.LightGreen;
                }

                // === FUEL ===
                string fuelUnit = fm.ActivePlan?.Units ?? "kg";
                // CurrentFuel e InitialFuel ya están en kg internamente
                double convFactor = fuelUnit.Equals("lbs", StringComparison.OrdinalIgnoreCase)
                    ? 2.20462 : 1.0;

                double fuelIni = fm.InitialFuel * convFactor;
                double fuelAct = fm.CurrentFuel * convFactor;
                double fuelUsed = Math.Max(0, fuelIni - fuelAct);

                if (_lblFuelInit != null)
                    _lblFuelInit.Text = $"INI: {fuelIni:F0} {fuelUnit}";

                if (_lblFuelCurrent != null)
                {
                    _lblFuelCurrent.Text = $"ACT: {fuelAct:F0} {fuelUnit}";
                    // umbrales en kg internamente
                    _lblFuelCurrent.ForeColor = fm.CurrentFuel < 500 ? Color.Red
                        : fm.CurrentFuel < 1000 ? Color.Orange
                        : Color.LightGreen;
                }

                if (_lblFuelUsed != null)
                    _lblFuelUsed.Text = $"USO: {fuelUsed:F0}";

                // ===== ALTITUD =====
                if (_lblAltitudeVal != null)
                    _lblAltitudeVal.Text = $"ALT: {fm.CurrentAltitude:F0} ft";

                if (_lblAglVal != null)
                {
                    if (fm.IsOnGround)
                    {
                        _lblAglVal.Text = "AGL: 0 ft";
                    }
                    else
                    {
                        double radarFt = fm.RadarAltitude;
                        if (radarFt > 5 && radarFt < 2500)
                            _lblAglVal.Text = $"AGL: {radarFt:F0} ft ▼";
                        else
                            _lblAglVal.Text = $"AGL: {Math.Max(0, fm.CurrentAGL):F0} ft";
                    }
                }

                if (_lblQnhVal != null)
                {
                    double qnh = fm.AircraftQnhMb;
                    if (qnh > 800 && qnh < 1100)   // rango válido de QNH
                    {
                        double qnhInHg = qnh / 33.8639;
                        _lblQnhVal.Text = $"QNH: {qnh:F0} hPa ({qnhInHg:F2} inHg)";
                        _lblQnhVal.ForeColor = Color.LightCyan;
                    }
                    else
                    {
                        _lblQnhVal.Text = "QNH: --- hPa";
                        _lblQnhVal.ForeColor = Color.DimGray;
                    }
                }

                // ===== SISTEMAS =====
                if (_lblGear != null)
                {
                    _lblGear.Text = fm.IsGearDown ? "GEAR: DOWN" : "GEAR: UP";
                    _lblGear.ForeColor = fm.IsGearDown ? Color.Lime : Color.Orange;
                }
                if (_lblFlaps != null)
                {
                    _lblFlaps.Text = $"FLAPS: {fm.FlapsLabel}";
                    _lblFlaps.ForeColor = (fm.FlapsLabel != "UP" && fm.FlapsLabel != "0") ? Color.Yellow : Color.LightGreen;
                }
                if (_lblSpoilers != null)
                {
                    _lblSpoilers.Text = fm.AreSpoilersDeployed ? "SPOIL: DEP" : "SPOIL: RET";
                    _lblSpoilers.ForeColor = fm.AreSpoilersDeployed ? Color.Orange : Color.LightGreen;
                }
                if (_lblAutobrake != null) _lblAutobrake.Text = $"A/BRK: {fm.AutobrakeSetting}";

                // Seat Belt Sign
                if (_lblSeatBelt != null)
                {
                    bool sb = fm.LastRawData?.SeatBeltSign == true;
                    _lblSeatBelt.Text = sb ? "🔔 SEATBELT: ON" : "🔔 SEATBELT: OFF";
                    _lblSeatBelt.ForeColor = sb ? Color.Yellow : Color.Gray;
                }

                // Autopilot
                if (_lblAutopilot != null)
                {
                    if (fm.AutopilotEngaged)
                    {
                        _lblAutopilot.Text = $"A/P: {fm.ApNavMode}/{fm.ApVertMode}";
                        _lblAutopilot.ForeColor = Color.Cyan;
                    }
                    else
                    {
                        _lblAutopilot.Text = "A/P: OFF";
                        _lblAutopilot.ForeColor = Color.Gray;
                    }
                }

                // Aproximación estabilizada
                if (_lblStabilized != null)
                {
                    bool inApp = fm.CurrentPhase == FlightPhase.Approach ||
                                 fm.CurrentPhase == FlightPhase.Landing;
                    if (!inApp)
                    {
                        _lblStabilized.Text = "";
                    }
                    else
                    {
                        _lblStabilized.Text = fm.IsApproachStabilized ? "✅ STABLE" : "⚠️ UNSTABLE";
                        _lblStabilized.ForeColor = fm.IsApproachStabilized ? Color.Lime : Color.Red;
                    }
                }


                // ===== LUCES =====
                if (_lblNavLight != null)
                {
                    _lblNavLight.Text = fm.IsNavLightOn ? "🟡 NAV: ON" : "🟡 NAV: OFF";
                    _lblNavLight.ForeColor = fm.IsNavLightOn ? Color.Gold : Color.Gray;
                }
                if (_lblBeaconLight != null)
                {
                    _lblBeaconLight.Text = fm.IsBeaconLightOn ? "🔴 BCN: ON" : "🔴 BCN: OFF";
                    _lblBeaconLight.ForeColor = fm.IsBeaconLightOn ? Color.Red : Color.Gray;
                }
                if (_lblLandingLight != null)
                {
                    _lblLandingLight.Text = fm.IsLandingLightOn ? "⚪ LAND: ON" : "⚪ LAND: OFF";
                    _lblLandingLight.ForeColor = fm.IsLandingLightOn ? Color.White : Color.Gray;
                }
                if (_lblTaxiLight != null)
                {
                    _lblTaxiLight.Text = fm.IsTaxiLightOn ? "🔵 TAXI: ON" : "🔵 TAXI: OFF";
                    _lblTaxiLight.ForeColor = fm.IsTaxiLightOn ? Color.Cyan : Color.Gray;
                }
                if (_lblStrobeLight != null)
                {
                    _lblStrobeLight.Text = fm.IsStrobeLightOn ? "🟢 STROBE: ON" : "🟢 STROBE: OFF";
                    _lblStrobeLight.ForeColor = fm.IsStrobeLightOn ? Color.Lime : Color.Gray;
                }

                // ===== MOTORES =====
                if (_engineMonitorPanel != null && fm.LastRawData != null)
                {
                    _engineMonitorPanel.UpdateEngines(fm.LastRawData);
                }
                else if (fm.LastRawData != null && _lblEng1Val != null)
                {
                    var d = fm.LastRawData;
                    switch (d.EngineCategory)
                    {
                        case Services.FsuipcService.AircraftCategory.Piston:
                            _lblEng1Val.Text = $"ENG1: {d.Rpm_1:F0} RPM";
                            _lblEng2Val.Text = $"ENG2: {d.Rpm_2:F0} RPM";
                            break;
                        case Services.FsuipcService.AircraftCategory.Turboprop:
                            _lblEng1Val.Text = $"ENG1: {d.TorquePct_1:F0}% TRQ";
                            _lblEng2Val.Text = $"ENG2: {d.TorquePct_2:F0}% TRQ";
                            break;
                        default:
                            _lblEng1Val.Text = $"ENG1: {d.N1_1:F0}% N1";
                            _lblEng2Val.Text = $"ENG2: {d.N1_2:F0}% N1";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en UpdateFlightInfoPanel: {ex.Message}");
            }
        }

        // Métodos auxiliares
        private string GetPhaseIcon(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.Boarding: return "🚪";
                case FlightPhase.Pushback: return "🔄";
                case FlightPhase.TaxiOut: return "🛻";
                case FlightPhase.TakeoffRoll: return "🏁";
                case FlightPhase.Takeoff: return "🛫";
                case FlightPhase.Climb: return "⬆️";
                case FlightPhase.Enroute: return "✈️";
                case FlightPhase.Descent: return "⬇️";
                case FlightPhase.Approach: return "🛬";
                case FlightPhase.AfterLanding: return "🛬";
                case FlightPhase.TaxiIn: return "🛻";
                case FlightPhase.OnBlock: return "🅿️";
                default: return "●●●";
            }
        }

        private Color GetPhaseColor(FlightPhase phase)
        {
            if (phase == FlightPhase.Enroute) return Color.Lime;
            if (phase == FlightPhase.Takeoff || phase == FlightPhase.Climb) return Color.Yellow;
            if (phase == FlightPhase.Descent || phase == FlightPhase.Approach) return Color.Orange;
            return Color.Cyan;
        }

        private string GetMachString(FlightManager fm)
        {
            if (fm.CurrentAltitude > 25000 && fm.CurrentIndicatedAirspeed > 0)
                return $"{fm.CurrentIndicatedAirspeed / 661.5:F2}";
            return "----";
        }

        private string GetEtaString(FlightManager fm, double remainingNm)
        {
            if (fm.CurrentGroundSpeed > 50 && remainingNm > 0)
            {
                double hoursRemaining = remainingNm / fm.CurrentGroundSpeed;
                DateTime eta = DateTime.UtcNow.AddHours(hoursRemaining);
                return $"{eta:HH:mm} UTC";
            }
            return "--:-- UTC";
        }

        #region Eventos de Botones

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Enabled = false;
            try
            {
                await _viewModel.Login();
            }
            finally
            {
                btnLogin.Enabled = true;
            }
        }

        // En UI/Forms/MainForm.cs

        // En UI/Forms/MainForm.cs

        private async void BtnSimbrief_Click(object sender, EventArgs e)
        {
            if (_viewModel?.FlightManager?.ActivePilot == null)
            {
                _uiService.AddLog("⚠️ Debes iniciar sesión primero", Theme.Warning);
                return;
            }

            // Verificar y limpiar PIREPs activos (huérfanos)
            var canContinue = await _viewModel.CheckAndCleanActivePireps();

            if (!canContinue)
            {
                // El usuario canceló o hubo un error
                return;
            }

            // Continuar con la apertura normal del planificador
            using (var planner = new FlightPlannerForm(
                _viewModel.ApiService,
                _viewModel.PhpVmsFlightService,
                _viewModel.SimbriefEnhancedService,
                _viewModel.FlightManager,
                _viewModel.FlightManager.ActivePilot,
                _viewModel.FlightManager.CurrentAirport,
                _uiService))
            {
                if (planner.ShowDialog(this) == DialogResult.OK)
                {
                    // PRIORIDAD 1: Usar el plan completo de SimBrief si existe
                    var completePlan = planner.GetLoadedPlan();
                    if (completePlan != null)
                    {
                        _viewModel.SetActivePlan(completePlan);
                        _uiService.AddLog($"✅ Plan loaded: {completePlan.Origin} → {completePlan.Destination}", Theme.Success);
                        // Pre-descargar el PDF del OFP en background para tenerlo listo al instante
                        if (_viewModel.HasOFPPdf())
                        {
                            var preload = _viewModel.DownloadOFPPdfAsync();
                        }
                        _viewModel.UpdateFlightInfo();
                        return;
                    }

                    // PRIORIDAD 2: Si no hay plan, usar el vuelo seleccionado (plan básico)
                    var selectedFlight = planner.GetSelectedFlight();
                    if (selectedFlight != null)
                    {
                        _viewModel.LoadFlightFromBid(selectedFlight);
                        _uiService.AddLog($"✅ Flight selected: {selectedFlight.Airline}{selectedFlight.FlightNumber}", Theme.Success);
                        _viewModel.UpdateFlightInfo();
                    }
                }
            }
        }
        private async void BtnStartStop_Click(object sender, EventArgs e)
        {
            await _viewModel?.HandleStartStopButton(btnStartStop.Text);
        }

        private async void BtnCancel_Click(object sender, EventArgs e)
        {
            if (_isFlightActive)
            {
                // Hay vuelo activo -> cancelar vuelo
                var result = EcamDialog.Show(this,
                    "There is an active flight.\n\nDo you want to cancel it?",
                    "CANCEL FLIGHT",
                    EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    await _viewModel?.CancelFlight();
                }
            }
            else
            {
                // No hay vuelo activo -> salir de la aplicación
                var result = EcamDialog.Show(this,
                    "¿Salir de la aplicación?",
                    "SALIR",
                    EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    Application.Exit();
                }
            }
        }

        private async void BtnOfp_Click(object sender, EventArgs e)
        {
            if (_viewModel == null) return;

            if (!_viewModel.HasOFPPdf())
            {
                _viewModel.ShowOFP();
                return;
            }

            // Si el PDF ya fue pre-descargado y el archivo existe, abrirlo sin espera
            string cached = _viewModel.GetCachedOFPPath();
            if (cached != null)
            {
                new OFPViewerForm(cached, _viewModel.GetOFPTitle()).Show(this);
                return;
            }

            btnOfp.Enabled = false;
            try
            {
                string pdfPath = await _viewModel.DownloadOFPPdfAsync();
                if (string.IsNullOrEmpty(pdfPath))
                {
                    MessageBox.Show("No se pudo descargar el OFP.", "OFP",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                new OFPViewerForm(pdfPath, _viewModel.GetOFPTitle()).Show(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error descargando el OFP:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnOfp.Enabled = true;
            }
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
            if (_isFlightActive)
            {
                var result = EcamDialog.Show(this,
                    "There is an active flight.\n\nIf you close the application, the flight will be lost.\n\nDo you want to cancel the flight and exit?",
                    "ACTIVE FLIGHT DETECTED",
                    EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    // Cancelar el vuelo de forma asíncrona y luego salir
                    Task.Run(async () =>
                    {
                        if (_viewModel != null)
                        {
                            await _viewModel.CancelFlight();
                            _viewModel.Stop();
                        }
                        Application.Exit();
                    });
                    e.Cancel = true; // Cancelamos el cierre inmediato, lo hacemos manual
                }
                else
                {
                    e.Cancel = true; // Cancelar el cierre
                }
            }
            else
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

            var defaults = new Dictionary<string, string>
            {
                // Ventana
                { "window_left",   "100" },
                { "window_top",    "100" },
                { "window_width",  "1024" },
                { "window_height", "768" },
                { "last_screen",   "0" },

                // Conexión
                { "language",      "es" },
                { "vms_api_url",   "https://vholar.co/" },
                { "airline",       "VHOLAR - FLIGHT OPERATIONS DEPARTMENT - FLIGHT DATA ANALYSIS" },
                { "vms_api_key",   "your_phpvms_apikey" },
                { "simbrief_user", "your_simbrief_user" },

                // Polling
                { "polling_interval_ms",    "50" },

                // Intervalos por fase (segundos)
                { "update_interval_taxi",     "30" },
                { "update_interval_takeoff",  "5" },
                { "update_interval_climb",    "15" },
                { "update_interval_cruise",   "30" },
                { "update_interval_descent",  "15" },
                { "update_interval_approach", "5" },
                { "update_interval_other",    "30" },

                // Eventos
                { "report_gear_changes",    "true" },
                { "report_flap_changes",    "true" },
                { "report_spoiler_changes", "true" },
                { "report_light_changes",   "true" },
                { "report_engine_changes",  "true" },

                // Combustible
                { "fuel_tolerance_percent",  "10" },
                { "fuel_tolerance_absolute", "50" },

                // Cliente
                { "ClientSettingsProvider.ServiceUri", "" }
            };

            foreach (var kvp in defaults)
            {
                if (config.AppSettings.Settings[kvp.Key] == null)
                    config.AppSettings.Settings.Add(kvp.Key, kvp.Value);
            }

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }



        #region Actualización automática

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var info = await UpdateChecker.CheckGitHub();

                if (UpdateChecker.IsNewer(info))
                {
                    string currentVersion = UpdateChecker.GetLocalVersion().ToString(3);
                    string newVersion = info.Version.ToString(3);
                    string notes = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                                            ? "(sin notas de versión)"
                                            : info.ReleaseNotes;

                    var result = MessageBox.Show(
                        $"Nueva versión disponible: {newVersion}\n" +
                        $"Versión actual: {currentVersion}\n\n" +
                        $"Novedades:\n{notes}\n\n" +
                        "¿Deseas actualizar ahora?",
                        "Actualización disponible",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information
                    );

                    if (result == DialogResult.Yes)
                        await DownloadAndUpdate(info.DownloadUrl);
                }
            }
            catch
            {
                // Sin internet o falla la API → la app continúa normal
            }
        }

        private async Task DownloadAndUpdate(string downloadUrl)
        {
            string tempZip = Path.Combine(Path.GetTempPath(), "vmsOpenAcars_update.zip");
            string tempFolder = Path.Combine(Path.GetTempPath(), "vmsOpenAcars_update");
            string appFolder = AppDomain.CurrentDomain.BaseDirectory;

            var progressForm = new ProgressForm();
            progressForm.Show();

            try
            {
                // 1. Descargar ZIP
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars-Updater");

                    var response = await client.GetAsync(downloadUrl,
                                       HttpCompletionOption.ResponseHeadersRead);

                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Error al descargar: {response.StatusCode}");

                    long? totalBytes = response.Content.Headers.ContentLength;
                    long downloaded = 0;

                    using (var fs = new FileStream(tempZip, FileMode.Create))
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        byte[] buffer = new byte[8192];
                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read);
                            downloaded += read;
                            if (totalBytes.HasValue)
                                progressForm.SetProgress(
                                    (int)(downloaded * 100 / totalBytes.Value));
                        }
                    }
                }

                progressForm.Close();

                // 2. Extraer ZIP en carpeta temporal
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
                ZipFile.ExtractToDirectory(tempZip, tempFolder);

                // 3. Copiar Updater.exe a la carpeta temporal
                string updaterSource = Path.Combine(appFolder, "Updater.exe");
                string updaterDest = Path.Combine(tempFolder, "Updater.exe");
                File.Copy(updaterSource, updaterDest, overwrite: true);

                // 4. Lanzar Updater y cerrar la app
                string args = string.Format("\"{0}\" \"{1}\" \"{2}\"",
                    tempFolder.TrimEnd('\\'),
                    appFolder.TrimEnd('\\'),
                    Application.ExecutablePath);

                var psi = new ProcessStartInfo
                {
                    FileName = updaterDest,
                    Arguments = args,
                    UseShellExecute = true,   // ← clave: abre en su propia ventana
                    CreateNoWindow = false,  // ← fuerza ventana visible
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(psi);

                Application.Exit();
            }
            catch (Exception ex)
            {
                progressForm.Close();
                MessageBox.Show(
                    $"Error durante la actualización:\n{ex.Message}\n\n" +
                    "Puedes actualizar manualmente desde:\n" +
                    "https://github.com/fhprietor/vmsOpenAcars/releases/latest",
                    "Error de actualización",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        #endregion

    }


}