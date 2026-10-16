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
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.ViewModels;
using vmsOpenAcars.Helpers;
using static vmsOpenAcars.Helpers.L;
using System.Reflection;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Controls;
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
        private McduChassisTable mainLayout;
        private McduChassisPanel pnlHeader;
        private McduDisplayPanel pnlFma;
        private McduChassisPanel pnlStatus;
        private McduChassisPanel pnlButtons;
        private McduDisplayPanel pnlMessageInfo;
        private McduDisplayPanel pnlIncoming;
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
        private McduKeyButton btnLogin;
        private McduKeyButton btnOfp;
        private McduKeyButton btnMap;
        private MapForm _mapForm;
        private McduKeyButton btnWeather;
        private McduKeyButton btnSimbrief;
        private McduKeyButton btnLogbook;
        // `internal` y no `public`: el tipo de la tecla es interno como el resto de helpers, y solo lo usa
        // `UIService` en este mismo ensamblado (los tests lo ven por `InternalsVisibleTo`).
        internal McduKeyButton btnStartStop;
        private McduKeyButton btnCancel;
        // Las teclas de la barra, en orden: `LayoutButtonBar` reparte el ancho entre ellas.
        private readonly List<McduKeyButton> _barKeys = new List<McduKeyButton>();
        private NotifyIcon _notifyIcon;
        private bool _isFlightActive = false;

        // Controles del Flight Information Panel - PROGRESO
        private Label _lblRestante;
        private Label _lblTiempoAire;

        // AERODINÁMICA
        private Label _lblIas;
        private Label _lblGs;
        private Label _lblVs;

        // FUEL
        private Label _lblFuelInit;
        private Label _lblFuelCurrent;
        private Label _lblFuelUsed;

        // ALTITUD
        private Label _lblAltitudeVal;
        private Label _lblCruiseVal;
        private Label _lblAglVal;
        private Label _lblQnhVal;

        // SISTEMAS
        private Label _lblGear;
        private Label _lblFlaps;
        private Label _lblSpoilers;
        private Label _lblAutobrake;
        private Label _lblAutopilot;
        private Label _lblStabilized;

        // STATUS BAR RADIO
        private Label _lblCom1;
        private Label _lblNav1;

        // FMA PLAN LINES + DEPARTURE COUNTDOWN
        private Label _lblFmaPlanLine1;
        private Label _lblFmaPlanLine2;
        private Label _lblFmaPlanLine3;
        private Label _lblDepartureCdw;
        private SimbriefPlan _lastRenderedPlan;

        // METAR PANEL
        private Label[]         _metarLabels         = new Label[4];
        private BufferedPanel[] _metarPanels         = new BufferedPanel[4];
        private MetarData[]     _metarDataArray      = new MetarData[4];
        private Color[]         _metarConditionColors = new[] { McduPalette.LcdDim, McduPalette.LcdDim, McduPalette.LcdDim, McduPalette.LcdDim };

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
        private OsdOverlayForm _osd;

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
            InitializeNotifications();
            InitializeHeader();
            InitializeMessageSection();
            InitializeFmaPanel();
            InitializeStatusSection();
            InitializeButtons();
            InitializeViewModel();
            ConnectViewModelEvents();
            _uiService?.AddLog($"vmsOpenAcars v{vmsOpenAcars.Core.Helpers.AppInfo.Version}", Theme.MainText);
            SystemInfoHelper.Initialize();
            _uiService?.AddLog(SystemInfoHelper.OsSummary,  Theme.SecondaryText);
            _uiService?.AddLog(SystemInfoHelper.CpuSummary, Theme.SecondaryText);
            _uiService?.AddLog(SystemInfoHelper.GpuSummary, Theme.SecondaryText);
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
                    var apiService          = new ApiService(apiUrl, apiKey);
                    var weatherService      = new WeatherService();
                    var metarService        = new MetarService();
                    var navDataService      = new NavDataService();
                    var landingLogService   = new LandingLogService(AppConfig.LandingLogPath);
                    var flightManager       = new FlightManager(apiService, weatherService);
                    var fsuipc              = new FsuipcService();
                    var phpVmsFlightService     = new PhpVmsFlightService(apiService);
                    var simbriefEnhancedService = new SimbriefEnhancedService();

                    _viewModel = new MainViewModel(
                        flightManager, fsuipc, apiService,
                        phpVmsFlightService, simbriefEnhancedService,
                        metarService, navDataService, landingLogService);
                    _uiService = new UIService(this, flightManager, apiService);

                    _osd = new OsdOverlayForm();
                }
                else
                {
                    // Sin URL/API key no hay servicios: avisar y no continuar, porque todo
                    // el resto de la inicialización depende de _viewModel.
                    MessageBox.Show(
                        "Falta configurar la aerolínea virtual.\n\n" +
                        "Abre vmsOpenAcars.exe.config (o el diálogo SETTINGS) e introduce la " +
                        "API URL y la API Key de tu phpVMS antes de iniciar la aplicación.",
                        "Configuración incompleta",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inicializando servicios: {ex.Message}");
                return;
            }

            // Guarda defensiva: sin servicios no hay nada que suscribir, y continuar
            // provocaría un NullReferenceException en el constructor del formulario.
            if (_viewModel == null) return;

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

            // El color ya no cambia con el estado: la tecla es negra y su leyenda es roja en los dos casos
            // (viene de `MainButtonBar`, `McduLegend.Danger`). Hasta v0.9.44 el fondo se aclaraba cuando
            // había vuelo activo, pero un rojo más apagado sobre un cuerpo negro —que es lo que es ahora—
            // deja de leerse: la diferencia la dice el rótulo, CANCELAR contra SALIR.
            btnCancel.Text = _isFlightActive ? _("BtnCancel") : _("BtnExit");
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
            _viewModel.OnMetarUpdated      += UpdateMetarPanel;
            _viewModel.OnMetarStateChanged += UpdateMetarPanelState;
            _viewModel.OnMapPositionUpdate += (lat, lon, hdg) =>
            {
                if (_mapForm != null && !_mapForm.IsDisposed)
                    _mapForm.UpdatePosition(lat, lon, hdg);
            };

            _viewModel.OnAirspacesReady += airspaces =>
            {
                if (_mapForm != null && !_mapForm.IsDisposed)
                    _mapForm.SetAirspaces(airspaces);
            };

            _viewModel.OnAtcStationsUpdated += stations =>
            {
                if (_mapForm != null && !_mapForm.IsDisposed)
                    _mapForm.SetAtcStations(stations);
            };

            _viewModel.OnPlanChanged += plan =>
            {
                if (_mapForm != null && !_mapForm.IsDisposed)
                {
                    _mapForm.LoadRoute(plan?.Waypoints, plan?.Origin, plan?.OriginRunway,
                        plan?.Destination, plan?.DestinationRunway, plan?.Alternate,
                        plan?.SidName, plan?.StarName);
                    _mapForm.SetAircraftCategory(_viewModel.GetAircraftCategory());
                }

                if (btnOfp != null)
                {
                    bool hasPlan = plan != null;
                    if (btnOfp.InvokeRequired)
                        btnOfp.BeginInvoke(new Action(() => btnOfp.Enabled = hasPlan));
                    else
                        btnOfp.Enabled = hasPlan;
                }
            };

            _viewModel.OnOsdMessage += (msg, sev) =>
            {
                OsdAudio.Play(sev);
                if (AppConfig.OsdEnabled)
                    _osd?.ShowMessage(msg, sev, AppConfig.OsdDurationSeconds * 1000);
            };
            _viewModel.OnShowMessage += (message, title) =>
            {
                MessageBox.Show(message, title);
            };

            // ── RAAS: popup de rodaje ─────────────────────────────────────────────
            // El coordinador lo pide al encender la luz de taxi o al entrar en TaxiOut, una vez
            // por vuelo. Aquí solo se pinta y se devuelve la elección: la lógica de los avisos
            // vive en el coordinador y en los helpers puros, no en el formulario.
            _viewModel.OnTaxiRoutePromptRequested += p =>
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        using (var dlg = new TaxiRouteForm(p, AppConfig.RaasEnabled,
                                                           AppConfig.RaasVoiceEnabled,
                                                           AppConfig.RaasVolume))
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK)
                            {
                                _uiService.AddLog("🎙️ RAAS: guía de rodaje no activada", Theme.Taxi);
                                return;
                            }
                            AppConfig.RaasVoiceEnabled = dlg.VoiceEnabled;
                            AppConfig.RaasVolume       = dlg.Volume;
                            _viewModel.StartTaxiGuidance(p.Icao, dlg.SelectedRunway, dlg.RouteText,
                                                         dlg.RaasEnabled, dlg.VoiceEnabled,
                                                         dlg.Volume);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    _uiService.AddLog("⚠️ RAAS: no se pudo mostrar el popup de rodaje (" + ex.Message + ")",
                                      Theme.Warning);
                }
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

        /// <summary>
        /// Comando que **hace** el botón START/ABORT/ENVIAR ahora mismo. Va aparte del rótulo a
        /// propósito: hasta v0.9.42 el despacho se hacía comparando el **texto visible**
        /// (`HandleStartStopButton(btnStartStop.Text)` contra `"START"`/`"ABORT"`/`"SEND PIREP"`), de
        /// modo que traducir el rótulo habría dejado el botón sin hacer nada —y en inglés funcionaba
        /// solo porque el literal coincidía—. El comando lo fija <see cref="UpdateButtonState"/>.
        /// </summary>
        private string _startStopCommand = "START";

        private void UpdateButtonState(string command, Color backColor, bool enabled)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateButtonState(command, backColor, enabled)));
                return;
            }

            _startStopCommand      = command;
            btnStartStop.Text      = ButtonCommandLabel(command);
            // El color del comando enciende la leyenda de la tecla (cuerpo negro, siempre): START ámbar,
            // ABORT rojo, ENVIAR PIREP verde. `MainViewModel` y `AcarsReporter` no cambian.
            btnStartStop.LegendColor = backColor;
            btnStartStop.Enabled   = enabled;
        }

        /// <summary>
        /// Rótulo del botón para un comando estable. El comando no se traduce —es lo que despacha
        /// `MainViewModel.HandleStartStopButton`—; lo que se traduce es lo que se ve.
        /// </summary>
        private static string ButtonCommandLabel(string command)
        {
            switch (command)
            {
                case "START":      return _("BtnStartStop");
                case "ABORT":      return _("BtnAbort");
                case "SEND PIREP": return _("BtnSendPirep");
                default:           return command;   // comando desconocido: se enseña tal cual
            }
        }

        private void RefreshUILanguage()
        {
            btnLogin.Text     = _("BtnLogin");
            btnOfp.Text       = _("BtnOfp");
            btnMap.Text       = _("BtnMap");
            btnWeather.Text   = _("BtnWeather");
            btnSimbrief.Text  = _("BtnSimbrief");
            btnLogbook.Text   = _("BtnLogbook");
            // Desde el comando, no con la clave fija: si el vuelo ya está en vuelo el botón dice
            // ABORT, y ponerle «INICIAR» a la fuerza lo dejaba mintiendo.
            btnStartStop.Text = ButtonCommandLabel(_startStopCommand);
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

        /// <summary>
        /// El fondo de la ventana es la **carcasa**: el plástico azuloso gastado. `mainLayout` es
        /// transparente desde siempre, así que los huecos entre paneles y los márgenes muestran este
        /// plástico, y cada panel que enseña plástico pinta su trozo de la misma rampa
        /// (`McduChassis.OffsetY`), de modo que la ventana se lee como un solo aparato.
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            McduChassis.Paint(e.Graphics, ClientRectangle, 0, Math.Max(1, ClientSize.Height));
        }

        private void InitializeForm()
        {

            this.Text = "vmsOpenAcars - ACARS Flight Deck";
            this.Size = new Size(1024, 768);
            // El ancho mínimo sale de la barra de botones: es ella la que no puede recortarse, porque
            // el panel no tiene `AutoScroll` y lo que no quepa no se puede pulsar. Con una sola
            // fuente, mover uno de los dos números no puede desalinearlos (MainButtonBarTests).
            this.MinimumSize = new Size(MainButtonBar.MinWindowWidth, 600);
            // La piel del MCDU, con su interruptor: apagada conserva colores y biseles y quita la textura.
            McduPalette.Textured = AppConfig.McduLook;
            this.BackColor = McduPalette.ChassisMid;
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
            catch { }

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
                using (Pen pen = new Pen(McduPalette.LcdCyan, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            };
        }

        private void InitializeLayout()
        {
            // El layout pinta la carcasa: sus huecos son la mayor superficie de plástico de la ventana.
            mainLayout = new McduChassisTable
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // Row 0: Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));  // Row 1: FMA
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));  // Row 2: Message (datos)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Row 3: Incoming Msg
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // Row 4: Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // Row 5: Status Bar

            this.Controls.Add(mainLayout);
        }

        private void InitializeNotifications()
        {
            var trayMenu = new ContextMenuStrip();
            // **Cambiar la ruta de rodaje a mano.** ATC suele dar la autorización por radio *después* del
            // pushback y de tener el avión preparado, así que la ruta aceptada al terminar el empuje puede
            // quedarse vieja. Esto reabre el popup con la sugerencia recalculada desde la posición actual y
            // manda lo que teclee el piloto: su autorización (que en la base de rutas cuenta doble) frente
            // a la del grafo.
            trayMenu.Items.Add("Cambiar ruta de rodaje (ATC)", null,
                               (s, ev) => _viewModel.RequestTaxiRouteChange());

            trayMenu.Items.Add(new ToolStripSeparator());

            // **Cerrar el vuelo a mano.** Si la detección de `OnBlock` no llega —el PIREP real
            // `RBmrBApLOjWlYmO2` se quedó en `Climb` tras un falso touch-and-go y nunca volvió al
            // suelo—, `SEND PIREP` no aparece nunca y al piloto sólo le queda cancelar, que **borra**
            // el PIREP. Esta es la salida que no borra nada: cierra el vuelo con el mismo cierre que
            // habría hecho la detección.
            trayMenu.Items.Add(_("Tray_CloseFlight"), null, (s, ev) => CloseFlightManuallyFromTray());

            // **Borrar la caché del mapa a mano.** La obligación de CARTO no es solo el TTL: si se deja
            // de usar el servicio, la caché del dispositivo tiene que poder borrarse. Y de paso sirve
            // para forzar teselas nuevas cuando un proveedor cambia algo.
            trayMenu.Items.Add("Borrar caché del mapa", null, (s, ev) =>
            {
                try
                {
                    int n = GMap.NET.GMaps.Instance.PrimaryCache.DeleteOlderThan(DateTime.MaxValue, null);
                    _uiService.AddLog($"🗺️ Caché del mapa borrada ({n} teselas)", Theme.SecondaryText);
                }
                catch (Exception ex)
                {
                    _uiService.AddLog("⚠️ No se pudo borrar la caché del mapa: " + ex.Message, Theme.Warning);
                }
            });
            trayMenu.Items.Add("Restaurar", null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Salir", null, (s, e) => ExitFromTray());

            _notifyIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Visible = true,
                Text = "vmsOpenAcars",
                BalloonTipTitle = "vmsOpenAcars",
                BalloonTipIcon = ToolTipIcon.Info,
                ContextMenuStrip = trayMenu
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
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
            pnlHeader = new McduChassisPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // ===== LOGO =====
            PictureBox pbLogo = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = McduPalette.ChassisHigh
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
                    pbLogo.BackColor = McduPalette.ChassisLow;
                    pbLogo.Paint += (s, e) =>
                    {
                        using (Font font = new Font("Consolas", 8))
                        using (SolidBrush brush = new SolidBrush(McduPalette.LcdCyan))
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
                // Grabado en oscuro sobre el plástico: el blanco se queda en 2,9:1 y el cian en 1,5:1.
                ForeColor = McduPalette.Ink,
                BackColor = McduPalette.ChassisHigh,
                Location = new Point(60, 10),
                AutoSize = true
            };

            // ===== NOMBRE DE AEROLÍNEA =====
            string airlineName = ConfigurationManager.AppSettings["airline"] ?? "vmsOpenAcars";
            lblSubTitle = new Label
            {
                Text = airlineName,
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = McduPalette.Ink,
                BackColor = McduPalette.ChassisHigh,
                Location = new Point(60, 35),
                AutoSize = true
            };

            // ===== LABEL DE VERSIÓN (a la izquierda del engranaje) =====
            Label lblVersion = new Label
            {
                Text = $"v{AppInfo.Version}",
                Font = new Font("Consolas", 9, FontStyle.Italic),
                ForeColor = McduPalette.Ink,
                BackColor = McduPalette.ChassisHigh,
                AutoSize = true,
                // La posición Y se ajustará en el evento Resize
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // ===== BOTÓN DE CONFIGURACIÓN =====
            McduKeyButton btnSettings = new McduKeyButton
            {
                Text        = "⚙️",
                Font        = new Font("Segoe UI", 14, FontStyle.Regular),
                Size        = new Size(40, 40),
                LegendColor = McduLegend.Neutral,
                Anchor      = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSettings.Click += BtnSettings_Click;

            // ===== BOTÓN MINIMIZAR A TASKBAR =====
            McduKeyButton btnMinimize = new McduKeyButton
            {
                Text        = "─",
                Font        = new Font("Consolas", 14, FontStyle.Bold),
                Size        = new Size(40, 40),
                LegendColor = McduLegend.Neutral,
                Anchor      = AnchorStyles.Top | AnchorStyles.Right
            };
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            // ===== BOTÓN MINIMIZAR AL SYSTEM TRAY =====
            McduKeyButton btnTray = new McduKeyButton
            {
                Text        = "▾",
                Font        = new Font("Segoe UI", 12, FontStyle.Regular),
                Size        = new Size(40, 40),
                LegendColor = McduLegend.Neutral,
                Anchor      = AnchorStyles.Top | AnchorStyles.Right
            };
            btnTray.Click += (s, e) => MinimizeToTray();
            new ToolTip().SetToolTip(btnTray, "Minimizar al área de notificaciones");

            // Agregar controles al panel
            pnlHeader.Controls.AddRange(new Control[] {
        pbLogo, lblTitle, lblSubTitle, lblVersion, btnSettings, btnTray, btnMinimize
    });

            mainLayout.Controls.Add(pnlHeader, 0, 0);

            // Ajustar posiciones al redimensionar
            pnlHeader.Resize += (s, e) =>
            {
                int rightMargin = 10;
                int btnWidth = 40;
                int spacing = 4;

                // Posicionar: [lblVersion] [btnSettings] [btnTray] [btnMinimize]
                btnMinimize.Location = new Point(pnlHeader.Width - btnWidth - rightMargin, 10);
                btnTray.Location = new Point(btnMinimize.Left - btnWidth - spacing, 10);
                btnSettings.Location = new Point(btnTray.Left - btnWidth - spacing, 10);

                // Calcular el centro vertical del botón
                int buttonCenterY = btnSettings.Top + (btnSettings.Height / 2);

                // Posicionar la versión a la izquierda del botón de ajustes, centrada verticalmente
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
                settingsForm.TestOsdCallback = (msg, sev) =>
                    _osd?.ShowMessage(msg, sev, AppConfig.OsdDurationSeconds * 1000);
                settingsForm.TestCabinAnnouncementCallback = phase =>
                    _viewModel?.TestCabinAnnouncementAsync(phase) ?? Task.FromResult("No active session");
                settingsForm.CabinVolumeChangedCallback = volume =>
                    _viewModel?.SetCabinVolume(volume);
                // La guía de rodaje aplica en caliente: apagarla devuelve la traza a los 30 s y
                // corta la voz sin reiniciar (el checkbox se auto-guarda).
                settingsForm.TaxiGuidanceChangedCallback = enabled =>
                    _viewModel?.OnTaxiGuidanceSettingChanged(enabled);
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
            pnlMessageInfo = new McduDisplayPanel
            {
                Dock = DockStyle.Fill,
                Title = _("FlightInfoTitle")
            };

            // Layout principal — una sola columna (motores en topGrid)
            var mainGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(2),
                BackColor = McduPalette.LcdBackground
            };
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(2),
                BackColor = McduPalette.LcdBackground
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
                BackColor = McduPalette.LcdBackground
            };
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));

            var progressPanel = CreateCompactInfoCard(_("Panel_Progress"), out _lblProgress, out _lblRestante, out _lblTiempoAire);
            _lblProgress.Text = "DIST: 0/0 NM"; _lblRestante.Text = "REST: 0 NM / ETA: --:--"; _lblTiempoAire.Text = "⏱️ T/A: 00:00:00";
            topGrid.Controls.Add(progressPanel, 0, 0);

            var aeroPanel = CreateCompactInfoCard(_("Panel_Aero"), out _lblIas, out _lblGs, out _lblVs);
            _lblIas.Text = "IAS: --- kt"; _lblGs.Text = "GS: --- kt / MACH: ----"; _lblVs.Text = "VS: ---- fpm";
            topGrid.Controls.Add(aeroPanel, 1, 0);

            var fuelPanel = CreateCompactInfoCard(_("Panel_Fuel"), out _lblFuelInit, out _lblFuelCurrent, out _lblFuelUsed);
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
                BackColor = McduPalette.LcdBackground
            };
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

            var altPanel = CreateInfoCard(_("Panel_Altitude"), out _lblAltitudeVal, out _lblAglVal, out _lblCruiseVal, out _lblQnhVal);
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
            pnlIncoming = new McduDisplayPanel
            {
                Dock = DockStyle.Fill,
                Title = _("IncomingTitle")
            };

            txtIncomingMsg = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = McduPalette.LcdBackground,
                ForeColor = McduPalette.LcdGreenSoft,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            txtIncomingMsg.PreviewKeyDown += (s, e) => e.IsInputKey = false;
            pnlIncoming.Controls.Add(txtIncomingMsg);

            // El título lo pinta la propia pantalla (`McduDisplayPanel.Title`), en blanco y en su banda:
            // una etiqueta aparte lo duplicaba en ámbar debajo del cristal.

            var pnlMetar = InitializeMetarPanel();
            var row3Split = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                Padding     = Padding.Empty,
                Margin      = Padding.Empty,
                BackColor   = McduPalette.LcdBackground
            };
            row3Split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row3Split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row3Split.Controls.Add(pnlIncoming, 0, 0);
            row3Split.Controls.Add(pnlMetar,    1, 0);
            mainLayout.Controls.Add(row3Split, 0, 3);
        }

        private Panel CreateCompactInfoCard(string title, out Label label1, out Label label2, out Label label3)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = McduPalette.LcdBackground, Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = McduPalette.LcdBackground,
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
                ForeColor = McduPalette.LcdCyan,
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
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = McduPalette.LcdBackground, Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                BackColor = McduPalette.LcdBackground,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            for (int i = 0; i < 6; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label { Text = _("Panel_Systems"), Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = McduPalette.LcdCyan, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);

            _lblGear = MakeSysLabel("GEAR: ---");
            _lblFlaps = MakeSysLabel("FLAPS: --");
            _lblSpoilers = MakeSysLabel("SPOIL: RET");
            _lblAutobrake = MakeSysLabel("A/BRK: ---");
            _lblAutopilot = MakeSysLabel("A/P: OFF");
            _lblStabilized = MakeSysLabel("");

            layout.Controls.Add(_lblGear, 0, 1);
            layout.Controls.Add(_lblFlaps, 0, 2);
            layout.Controls.Add(_lblSpoilers, 0, 3);
            layout.Controls.Add(_lblAutobrake, 0, 4);
            layout.Controls.Add(_lblAutopilot, 0, 5);
            layout.Controls.Add(_lblStabilized, 0, 6);
            panel.Controls.Add(layout);
            return panel;
        }

        /// <summary>Panel LUCES — título en fila 0 del TLP para evitar que tape la fila 1.</summary>
        private Panel CreateLucesPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = McduPalette.LcdBackground, Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = McduPalette.LcdBackground,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            for (int i = 0; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label { Text = _("Panel_Lights"), Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = McduPalette.LcdCyan, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);

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
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = McduPalette.LcdBackground, Margin = new Padding(1) };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = McduPalette.LcdBackground,
                Padding = new Padding(3, 8, 3, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14)); // título
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // espaciador

            layout.Controls.Add(new Label { Text = _("Panel_Engines"), Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = McduPalette.LcdCyan, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);

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
            ForeColor = McduPalette.LcdGreenSoft,
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
                BackColor = McduPalette.LcdBackground,
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
                BackColor = McduPalette.LcdBackground,
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
                BackColor = McduPalette.LcdBackground
            };
            layout.Controls.Add(topSpacer, 0, 0);

            // Título
            var lblTitle = new Label
            {
                Text = _("Panel_Engines"),
                Font = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor = McduPalette.LcdCyan,
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
                BackColor = McduPalette.LcdBackground,
                Margin = new Padding(1)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = McduPalette.LcdBackground,
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
                ForeColor = McduPalette.LcdCyan,
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
            pnlFma = new McduDisplayPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 26, 12, 12)
            };

            // Outer: 2 cols (izquierda líneas SimBrief / derecha columna de estado)
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // [col 0] Tres líneas SimBrief apiladas
            var planLines = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty
            };
            planLines.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++)
                planLines.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            var monoFont = new Font("Consolas", 12.25f, FontStyle.Regular, GraphicsUnit.Point);
            var planFore1 = McduPalette.Ink;
            var planFore2 = McduPalette.Ink;

            _lblFmaPlanLine1 = new Label
            {
                Dock = DockStyle.Fill,
                Font = monoFont,
                ForeColor = planFore1,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };
            _lblFmaPlanLine2 = new Label
            {
                Dock = DockStyle.Fill,
                Font = monoFont,
                ForeColor = planFore2,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };
            _lblFmaPlanLine3 = new Label
            {
                Dock = DockStyle.Fill,
                Font = monoFont,
                ForeColor = planFore2,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };

            planLines.Controls.Add(_lblFmaPlanLine1, 0, 0);
            planLines.Controls.Add(_lblFmaPlanLine2, 0, 1);
            planLines.Controls.Add(_lblFmaPlanLine3, 0, 2);

            // [col 1] PHASE / AIR / Countdown apilados verticalmente
            var rightCol = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty
            };
            rightCol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++)
                rightCol.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            lblPhase = CreateFmaLabel("PHASE", "STANDBY");
            lblAir   = CreateFmaLabel("",   "GROUND");

            _lblDepartureCdw = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 20f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = McduPalette.LcdGreen,
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                Text = ""
            };

            rightCol.Controls.Add(lblPhase,         0, 0);
            rightCol.Controls.Add(lblAir,           0, 1);
            rightCol.Controls.Add(_lblDepartureCdw, 0, 2);

            outer.Controls.Add(planLines, 0, 0);
            outer.Controls.Add(rightCol,  1, 0);

            pnlFma.Controls.Add(outer);
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
                BackColor = McduPalette.LcdBackground,
                Margin = Padding.Empty,
                Text = $"{title} {value}"
            };
        }

        private void InitializeStatusSection()
        {
            pnlStatus = new McduChassisPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(3, 0, 3, 0)
            };

            var flow = new McduChassisFlow
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0)
            };

            lblSimName        = CreateStatusPill(_("Waiting"), McduPalette.LcdDim);
            lblAcarsStatus    = CreateStatusPill("⚠️ Offline", McduPalette.LcdAmber);
            lblCurrentAirport = CreateStatusPill("---", McduPalette.LcdDim);
            lblValidationStatus = CreateStatusPill("ICAO --- GPS ---", McduPalette.LcdDim);
            lblPos            = CreateStatusPill("---", McduPalette.LcdDim);
            _lblCom1          = CreateStatusPill("COM ---", McduPalette.LcdDim);
            _lblNav1          = CreateStatusPill("NAV ---", McduPalette.LcdDim);

            flow.Controls.AddRange(new Control[] {
                lblSimName, lblAcarsStatus, lblCurrentAirport, lblValidationStatus, lblPos,
                _lblCom1, _lblNav1
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
                ForeColor = McduPalette.ForLog(fore),
                BackColor = McduPalette.LcdBackground,
                AutoSize = true,
                Padding = new Padding(7, 2, 7, 2),
                Margin = new Padding(3, 3, 3, 3)
            };
        }

        private void InitializeButtons()
        {
            pnlButtons = new McduChassisPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4)
            };

            // La barra sale de `MainButtonBar` (nombres, claves de rótulo y colores en una sola
            // lista, para que no puedan desalinearse) y **reparte el ancho del panel**: antes cada tecla
            // medía 90 px fijos y sobraban ~270 px a la derecha mientras `DESPACHO` y `BITÁCORA` se
            // quedaban sin sitio al 150 % de escalado. El ancho se recalcula en cada `Resize`
            // (`LayoutButtonBar`), porque el panel está dockeado y crece con la ventana.
            foreach (var spec in MainButtonBar.Specs)
            {
                McduKeyButton btn = new McduKeyButton
                {
                    Text        = _(spec.LabelKey),
                    Size        = new Size(MainButtonBar.MinimumButtonWidth, MainButtonBar.ButtonHeight),
                    LegendColor = spec.LegendColor,
                    Font        = new Font("Consolas", MainButtonBar.ButtonFontSize, FontStyle.Bold)
                };

                switch (spec.Name)
                {
                    case "LOGIN":
                        btnLogin = btn;
                        btn.Click += BtnLogin_Click;
                        break;
                    case "DISPATCH":
                        btnSimbrief = btn;
                        btn.Click += BtnSimbrief_Click;
                        break;
                    case "LOGBOOK":
                        btnLogbook = btn;
                        btn.Click += BtnLogbook_Click;
                        break;
                    case "START":
                        btnStartStop = btn;
                        btn.Click += BtnStartStop_Click;
                        break;
                    case "CANCEL":
                        btnCancel = btn;
                        btn.Click += BtnCancel_Click;
                        btnCancel.Text = _("BtnExit");
                        break;
                    case "OFP":
                        btnOfp = btn;
                        btn.Click += BtnOfp_Click;
                        btn.Enabled = false;   // enabled once a plan is loaded
                        break;
                    case "WEATHER":
                        btnWeather = btn;
                        btn.Click += BtnWeather_Click;
                        break;
                    case "MAP":
                        btnMap = btn;
                        btn.Enabled = false;
                        btn.Click += BtnMap_Click;
                        break;
                    default:
                        // Un botón declarado en `MainButtonBar` y sin `case` aquí **no hace nada**:
                        // es exactamente lo que le pasó a `ATIS`, que estuvo versiones en la barra
                        // cayendo en un manejador genérico que solo escribía la pulsación en el log.
                        // Antes se callaba; ahora se queja, porque un botón muerto ocupa sitio en una
                        // barra cuyo ancho está medido contra el mínimo de la ventana.
                        _uiService?.AddLog($"⚠️ Botón sin acción en la barra: {spec.Name}", Theme.Warning);
                        break;
                }

                pnlButtons.Controls.Add(btn);
                _barKeys.Add(btn);
            }

            // Reparto del ancho: una vez ahora y en cada cambio de tamaño del panel.
            pnlButtons.Resize += (s, e) => LayoutButtonBar();
            // Las rayitas van detrás de las teclas, así que las pinta el panel.
            pnlButtons.Paint += (s, e) => DrawLineSelectDashes(e.Graphics);
            LayoutButtonBar();

            mainLayout.Controls.Add(pnlButtons, 0, 4);
        }

        /// <summary>
        /// Las rayitas blancas del bisel, una por tecla. En el MCDU real cada tecla de línea tiene la suya
        /// apuntando a su renglón, y aquí son lo que convierte la barra en la columna de LSK del aparato.
        /// Van detrás de las teclas: las pinta el panel y las teclas se dibujan encima.
        /// </summary>
        private void DrawLineSelectDashes(Graphics g)
        {
            foreach (McduKeyButton key in _barKeys)
                McduChassis.Dash(g, key.Left + 12, key.Top - 5, 22);
        }

        /// <summary>
        /// Reparte el ancho del panel entre las teclas de la barra: todas del **mismo** ancho, que es lo
        /// que mantiene la armonía de los rótulos (unos más pequeños que otros era justo lo que se veía
        /// cuando cada tecla medía 90 px fijos y `DESPACHO`/`BITÁCORA` no cabían al 150 % de escalado).
        /// El ancho común lo decide `MainButtonBar.ButtonWidthFor`, que es puro y está probado.
        /// </summary>
        private void LayoutButtonBar()
        {
            if (_barKeys.Count == 0) return;

            int width = MainButtonBar.ButtonWidthFor(pnlButtons.ClientSize.Width, _barKeys.Count);
            int x = MainButtonBar.StartX;

            foreach (McduKeyButton key in _barKeys)
            {
                key.SetBounds(x, 6, width, MainButtonBar.ButtonHeight);
                x += width + MainButtonBar.Spacing;
            }
        }

        private Label CreateInfoLabel(string text, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Consolas", 10, style),
                ForeColor = McduPalette.LcdGreenSoft,
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
                            lblPhase.ForeColor = McduPalette.LcdGreen;
                        }
                        else
                        {
                            lblPhase.Text = "🔴 NO SIM";
                            lblPhase.ForeColor = McduPalette.LcdRed;
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
                        _lblVs.ForeColor = McduPalette.LcdGreen;
                    else if (fm.CurrentVerticalSpeed < -500)
                        _lblVs.ForeColor = McduPalette.LcdAmber;
                    else
                        _lblVs.ForeColor = McduPalette.LcdGreenSoft;
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
                    _lblFuelCurrent.ForeColor = fm.CurrentFuel < 500 ? McduPalette.LcdRed
                        : fm.CurrentFuel < 1000 ? McduPalette.LcdAmber
                        : McduPalette.LcdGreenSoft;
                }

                if (_lblFuelUsed != null)
                    _lblFuelUsed.Text = $"USO: {fuelUsed:F0}";

                // ===== ALTITUD =====
                if (_lblAltitudeVal != null)
                    _lblAltitudeVal.Text = $"ALT: {fm.CurrentAltitude:F0} ft";

                if (_lblAglVal != null)
                {
                    if (fm.IsOnGround)
                        _lblAglVal.Text = "AGL: 0 ft";
                    else
                        _lblAglVal.Text = $"AGL: {Math.Max(0, fm.CurrentAGL):F0} ft";
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
                        _lblQnhVal.ForeColor = McduPalette.LcdDim;
                    }
                }

                // ===== SISTEMAS =====
                if (_lblGear != null)
                {
                    _lblGear.Text = fm.IsGearDown ? "GEAR: DOWN" : "GEAR: UP";
                    _lblGear.ForeColor = fm.IsGearDown ? McduPalette.LcdGreen : McduPalette.LcdAmber;
                }
                if (_lblFlaps != null)
                {
                    _lblFlaps.Text = $"FLAPS: {fm.FlapsLabel}";
                    _lblFlaps.ForeColor = (fm.FlapsLabel != "UP" && fm.FlapsLabel != "0") ? McduPalette.LcdAmber : McduPalette.LcdGreenSoft;
                }
                if (_lblSpoilers != null)
                {
                    _lblSpoilers.Text = fm.AreSpoilersDeployed ? "SPOIL: DEP" : "SPOIL: RET";
                    _lblSpoilers.ForeColor = fm.AreSpoilersDeployed ? McduPalette.LcdAmber : McduPalette.LcdGreenSoft;
                }
                if (_lblAutobrake != null) _lblAutobrake.Text = $"A/BRK: {fm.AutobrakeSetting}";

                // Autopilot
                if (_lblAutopilot != null)
                {
                    if (fm.AutopilotEngaged)
                    {
                        _lblAutopilot.Text = $"A/P: {fm.ApNavMode}/{fm.ApVertMode}";
                        _lblAutopilot.ForeColor = McduPalette.LcdCyan;
                    }
                    else
                    {
                        _lblAutopilot.Text = "A/P: OFF";
                        _lblAutopilot.ForeColor = McduPalette.LcdDim;
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
                        _lblStabilized.ForeColor = fm.IsApproachStabilized ? McduPalette.LcdGreen : McduPalette.LcdRed;
                    }
                }


                // ===== LUCES =====
                if (_lblNavLight != null)
                {
                    _lblNavLight.Text = fm.IsNavLightOn ? "🟡 NAV: ON" : "🟡 NAV: OFF";
                    _lblNavLight.ForeColor = fm.IsNavLightOn ? McduPalette.LcdCyan : McduPalette.LcdDim;
                }
                if (_lblBeaconLight != null)
                {
                    _lblBeaconLight.Text = fm.IsBeaconLightOn ? "🔴 BCN: ON" : "🔴 BCN: OFF";
                    _lblBeaconLight.ForeColor = fm.IsBeaconLightOn ? McduPalette.LcdRed : McduPalette.LcdDim;
                }
                if (_lblLandingLight != null)
                {
                    _lblLandingLight.Text = fm.IsLandingLightOn ? "⚪ LAND: ON" : "⚪ LAND: OFF";
                    _lblLandingLight.ForeColor = fm.IsLandingLightOn ? McduPalette.LcdWhite : McduPalette.LcdDim;
                }
                if (_lblTaxiLight != null)
                {
                    _lblTaxiLight.Text = fm.IsTaxiLightOn ? "🔵 TAXI: ON" : "🔵 TAXI: OFF";
                    _lblTaxiLight.ForeColor = fm.IsTaxiLightOn ? McduPalette.LcdCyan : McduPalette.LcdDim;
                }
                if (_lblStrobeLight != null)
                {
                    _lblStrobeLight.Text = fm.IsStrobeLightOn ? "🟢 STROBE: ON" : "🟢 STROBE: OFF";
                    _lblStrobeLight.ForeColor = fm.IsStrobeLightOn ? McduPalette.LcdGreen : McduPalette.LcdDim;
                }

                // ===== RADIO (COM1 / NAV1) =====
                if (_lblCom1 != null || _lblNav1 != null)
                {
                    var raw = fm.LastRawData;
                    double com1 = raw?.Com1FrequencyMhz ?? 0;
                    double nav1 = raw?.Nav1FrequencyMhz ?? 0;
                    int    nav1Crs = raw?.Nav1ObsCourse ?? 0;

                    if (_lblCom1 != null)
                    {
                        if (com1 > 0)
                        {
                            _lblCom1.Text      = $"COM {com1:F2}";
                            _lblCom1.ForeColor = McduPalette.LcdGreenSoft;
                        }
                        else
                        {
                            _lblCom1.Text      = "COM ---";
                            _lblCom1.ForeColor = McduPalette.LcdDim;
                        }
                    }
                    if (_lblNav1 != null)
                    {
                        if (nav1 > 0)
                        {
                            _lblNav1.Text = nav1Crs > 0
                                ? $"NAV {nav1:F2}/{nav1Crs:D3}"
                                : $"NAV {nav1:F2}";
                            _lblNav1.ForeColor = McduPalette.LcdCyan;
                        }
                        else
                        {
                            _lblNav1.Text      = "NAV ---";
                            _lblNav1.ForeColor = McduPalette.LcdDim;
                        }
                    }
                }

                // ===== FMA PLAN LINES =====
                if (_lblFmaPlanLine1 != null && plan != _lastRenderedPlan)
                {
                    _lastRenderedPlan = plan;
                    if (plan != null)
                    {
                        string originIata = string.IsNullOrEmpty(plan.OriginIata) ? "---" : plan.OriginIata;
                        string destIata   = string.IsNullOrEmpty(plan.DestinationIata) ? "---" : plan.DestinationIata;
                        string planDate   = plan.ScheduledOffTime > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(plan.ScheduledOffTime).UtcDateTime.ToString("ddMMMyyyy").ToUpper()
                            : DateTimeOffset.UtcNow.ToString("ddMMMyyyy").ToUpper();

                        _lblFmaPlanLine1.Text =
                            $"{plan.Airline}{plan.FlightNumber}  {plan.Origin}/{originIata}  {plan.Destination}/{destIata}" +
                            $"  CI {plan.CostIndex}  {planDate}  {plan.Registration} {plan.AircraftIcao}";

                        string isaStr  = plan.AvgIsaDev >= 0
                            ? $"P{plan.AvgIsaDev:D3}"
                            : $"M{Math.Abs(plan.AvgIsaDev):D3}";
                        string windStr = $"{plan.AvgWindDir:D3}/{plan.AvgWindSpd:D3}";

                        string flStr = plan.CruiseAltitude > 0
                            ? $"FL{plan.CruiseAltitude / 100}"
                            : "FL---";

                        string tripStr = plan.TripFuel > 0 ? $"  TRIP {plan.TripFuel:F0}" : "";
                        _lblFmaPlanLine2.Text =
                            $"PAX {plan.PaxCount}  FUEL {plan.BlockFuel:F0}{tripStr}  CARGO {plan.CargoWeight:F0}  {flStr}" +
                            $"   AVG WIND {windStr}  AVG ISA {isaStr}";

                        _lblFmaPlanLine3.Text = !string.IsNullOrEmpty(plan.Route)
                            ? $"RTE  {plan.Route}"
                            : "";
                    }
                    else
                    {
                        _lblFmaPlanLine1.Text = "";
                        _lblFmaPlanLine2.Text = "";
                        _lblFmaPlanLine3.Text = "";
                    }
                }

                // ===== DEPARTURE COUNTDOWN =====
                if (_lblDepartureCdw != null)
                {
                    long cdwTarget = plan != null && plan.ScheduledOutTime > 0
                        ? plan.ScheduledOutTime
                        : (plan != null ? plan.ScheduledOffTime : 0);
                    bool showCdw = plan != null &&
                                   cdwTarget > 0 &&
                                   (fm.CurrentPhase == FlightPhase.Idle ||
                                    fm.CurrentPhase == FlightPhase.Boarding);
                    if (showCdw)
                    {
                        long secondsRemaining = cdwTarget -
                                                DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        Color cdwColor;
                        if (secondsRemaining > 600 || secondsRemaining < -600)
                            cdwColor = McduPalette.LcdRed;
                        else if (secondsRemaining > 300 || secondsRemaining < -300)
                            cdwColor = McduPalette.LcdAmber;
                        else
                            cdwColor = McduPalette.LcdGreen;

                        string cdwText;
                        if (secondsRemaining >= 0)
                        {
                            cdwText = secondsRemaining >= 3600
                                ? $"{secondsRemaining / 3600}:{(secondsRemaining % 3600) / 60:D2}"
                                : $"{secondsRemaining / 60:D2}:{secondsRemaining % 60:D2}";
                        }
                        else
                        {
                            long delay = -secondsRemaining;
                            cdwText = $"+{delay / 60:D2}:{delay % 60:D2}";
                        }
                        _lblDepartureCdw.Text      = cdwText;
                        _lblDepartureCdw.ForeColor  = cdwColor;
                        _lblDepartureCdw.Visible = true;
                    }
                    else
                    {
                        _lblDepartureCdw.Visible = false;
                    }
                }

                // ===== MOTORES =====
                if (_engineMonitorPanel != null && fm.LastRawData != null)
                {
                    _engineMonitorPanel.UpdateEngines(fm.LastRawData);
                    _engineMonitorPanel.UpdateLifecycle(fm.GetEngineLifecycleSnapshot());
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
            catch { }
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
            if (phase == FlightPhase.Enroute) return McduPalette.LcdGreen;
            if (phase == FlightPhase.Takeoff || phase == FlightPhase.Climb) return McduPalette.LcdAmber;
            if (phase == FlightPhase.Descent || phase == FlightPhase.Approach) return McduPalette.LcdAmber;
            return McduPalette.LcdCyan;
        }

        private string GetMachString(FlightManager fm)
        {
            if (fm.CurrentIndicatedAirspeed <= 0 || fm.CurrentAltitude < 10000) return "----";
            // ISA standard atmosphere: temperature lapse up to tropopause (36089 ft)
            double stdTempK = 288.15 - 0.001981 * Math.Min(fm.CurrentAltitude, 36089);
            double speedOfSoundKt = 661.5 * Math.Sqrt(stdTempK / 288.15);
            // Density ratio σ = (T/T₀)^4.256 → TAS from IAS
            double sigma = Math.Pow(stdTempK / 288.15, 4.256);
            double tasKt = fm.CurrentIndicatedAirspeed / Math.Sqrt(sigma);
            return $"{tasKt / speedOfSoundKt:F2}";
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
                var pilot = _viewModel.ActivePilot;
                if (pilot != null)
                {
                    btnMap.Enabled = true;
                    _mapForm?.CenterOnAirport(pilot.CurrentAirport);
                }
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
                        BtnMap_Click(null, EventArgs.Empty);
                        _uiService.AddLog($"✅ Plan loaded: {completePlan.Origin} → {completePlan.Destination}", Theme.Success);
                        // Descargas de red que no deben bloquear la UI al cargar un plan.
                        // Sus fallos se registran en el log en vez de perderse.
                        FireAndForget.Run(() => _viewModel.TriggerMetarFetchAsync(),
                            ex => _uiService.AddLog($"⚠️ METAR: {ex.Message}", Theme.Warning),
                            "fetch de METAR");
                        if (_viewModel.HasOFPPdf())
                            FireAndForget.Run(() => _viewModel.DownloadOFPPdfAsync(),
                                ex => _uiService.AddLog($"⚠️ OFP PDF: {ex.Message}", Theme.Warning),
                                "descarga de OFP");
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
            // Por el comando, no por el rótulo: ver `_startStopCommand`.
            await _viewModel?.HandleStartStopButton(_startStopCommand);
        }

        private async void BtnCancel_Click(object sender, EventArgs e)
        {
            if (_isFlightActive)
            {
                // Hay vuelo activo. Si **además** la fase no ha llegado a `OnBlock`, `SEND PIREP` no
                // está disponible y cancelar sería la única salida de la aplicación… borrando el
                // PIREP. En ese caso se ofrece primero el cierre manual, que es el mismo cierre que
                // hace la detección (PIREP real `RBmrBApLOjWlYmO2`).
                bool canClose = _viewModel?.CanCloseFlightManually == true;

                var result = EcamDialog.Show(this,
                    _(canClose ? "ConfirmCancelFlightOrClose" : "ConfirmCancelFlight"),
                    _(canClose ? "FlightNotClosedTitle"      : "CancelTitle"),
                    canClose ? EcamDialogButtons.YesNoCancel : EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    // Con la tercera opción en pantalla: YES = cerrar el vuelo (y poder enviar).
                    // Sin ella: YES = cancelar, que es lo que pregunta el diálogo de siempre.
                    if (canClose)
                    {
                        _viewModel.CloseFlightManually();
                        _osd?.ShowMessage(_("Osd_ManualOnBlock"), OsdSeverity.Success,
                                          AppConfig.OsdDurationSeconds * 1000);
                    }
                    else
                    {
                        await _viewModel?.CancelFlight();
                    }
                }
                else if (result == DialogResult.No && canClose)
                {
                    // NO = cancelar de verdad (se elimina el PIREP). CANCEL = volver sin hacer nada.
                    await _viewModel?.CancelFlight();
                }
            }
            else
            {
                // No hay vuelo activo -> salir de la aplicación
                var result = EcamDialog.Show(this,
                    _("ConfirmExit"),
                    _("ExitTitle"),
                    EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// **Cerrar el vuelo a mano desde la bandeja**, para el piloto que ya no encuentra el botón de
        /// enviar. Cierra con el mismo cierre que la detección y deja la fase en `OnBlock`, que es lo
        /// que habilita `SEND PIREP`; si no había nada que cerrar lo dice en el log en vez de quedarse
        /// mudo —el piloto ha ido a buscar esta opción porque algo no cuadraba—.
        /// </summary>
        private void CloseFlightManuallyFromTray()
        {
            if (_viewModel == null) return;

            if (!_viewModel.CloseFlightManually())
            {
                _uiService?.AddLog(_("Log_NothingToClose"), Theme.Warning);
                return;
            }

            _uiService?.AddLog(_("Log_FlightClosedManually"), Theme.Success);
            _osd?.ShowMessage(_("Osd_ManualOnBlock"), OsdSeverity.Success,
                              AppConfig.OsdDurationSeconds * 1000);
        }

        private void BtnMap_Click(object sender, EventArgs e)
        {
            if (_mapForm == null || _mapForm.IsDisposed)
            {
                _mapForm = new MapForm();
                _mapForm.OnProcedureChanged += (orgRwy, sid, dstRwy, star) =>
                    _viewModel.UpdateProcedureOverrides(orgRwy, sid, dstRwy, star);
                int x = Right + 6;
                if (x + _mapForm.Width > Screen.FromControl(this).WorkingArea.Right)
                    x = Left + 40;
                _mapForm.Location = new Point(x, Top);
                _mapForm.Show();
                var airspaces = _viewModel.GetAirspaces();
                if (airspaces?.Count > 0) _mapForm.SetAirspaces(airspaces);
                var atc = _viewModel.GetAtcStations();
                if (atc?.Count > 0) _mapForm.SetAtcStations(atc);
                _mapForm.SetAircraftCategory(_viewModel.GetAircraftCategory());
                var plan = _viewModel.ActivePlan;
                if (plan?.Waypoints?.Count > 1)
                    _mapForm.LoadRoute(plan.Waypoints, plan.Origin, plan.OriginRunway,
                        plan.Destination, plan.DestinationRunway, plan.Alternate,
                        plan.SidName, plan.StarName);
                else
                    _mapForm.CenterOnAirport(_viewModel.ActivePilot?.CurrentAirport);
            }
            else
            {
                if (_mapForm.WindowState == FormWindowState.Minimized)
                    _mapForm.WindowState = FormWindowState.Normal;
                _mapForm.BringToFront();
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

        private void BtnLogbook_Click(object sender, EventArgs e)
        {
            var svc = _viewModel?.LandingLogService;
            if (svc == null || !svc.IsAvailable)
            {
                using (var dlg = new EcamDialog(
                    "Landing log not configured.\nSet the Landing DB path in Settings first.",
                    "LANDING LOG", EcamDialogButtons.OK))
                    dlg.ShowDialog(this);
                return;
            }
            using (var form = new FlightHistoryForm(svc))
                form.ShowDialog(this);
        }

        private void BtnWeather_Click(object sender, EventArgs e)
        {
            if (_viewModel != null)
                Task.Run(() => _viewModel.TriggerMetarFetchAsync());
        }

        private Panel InitializeMetarPanel()
        {
            var pnl = new McduDisplayPanel
            {
                Dock  = DockStyle.Fill,
                Title = _("MetarTitle")
            };

            var grid = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 4,
                Padding     = Padding.Empty,
                BackColor   = McduPalette.LcdBackground
            };
            for (int i = 0; i < 4; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            string[] stationLabels = { "ORIG", "DEST", "ALT", "ENRT" };
            var monoFont = new Font("Consolas", 12.25f, FontStyle.Regular, GraphicsUnit.Point);

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var cell = new BufferedPanel
                {
                    Dock      = DockStyle.Fill,
                    BackColor = McduPalette.LcdBackground,
                    Margin    = new Padding(0, 0, 0, 1),
                    Cursor    = Cursors.Hand
                };

                var inner = new TableLayoutPanel
                {
                    Dock        = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount    = 1,
                    Padding     = new Padding(10, 2, 4, 2),
                    BackColor   = McduPalette.LcdBackground
                };
                inner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
                inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                inner.Controls.Add(new Label
                {
                    Text      = stationLabels[i],
                    Font      = new Font("Consolas", 8, FontStyle.Bold),
                    ForeColor = McduPalette.LcdCyan,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin    = Padding.Empty
                }, 0, 0);

                _metarLabels[idx] = new Label
                {
                    Text         = "---",
                    Font         = monoFont,
                    ForeColor    = McduPalette.Ink,
                    Dock         = DockStyle.Fill,
                    TextAlign    = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Margin       = Padding.Empty
                };
                inner.Controls.Add(_metarLabels[idx], 1, 0);
                cell.Controls.Add(inner);

                cell.Paint += (s, ev) =>
                {
                    using (var br = new SolidBrush(_metarConditionColors[idx]))
                        ev.Graphics.FillRectangle(br, 0, 0, 4, ((Panel)s).Height);
                };

                cell.Click                += (s, ev) => ShowMetarDecode(idx);
                _metarLabels[idx].Click   += (s, ev) => ShowMetarDecode(idx);

                _metarPanels[idx] = cell;
                grid.Controls.Add(cell, 0, i);
            }

            pnl.Controls.Add(grid);
            return pnl;
        }

private void UpdateMetarPanel(MetarData[] metars)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<MetarData[]>(UpdateMetarPanel), new object[] { metars });
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                _metarDataArray[i] = metars?[i];
                if (_metarLabels[i] == null) continue;
                _metarLabels[i].Text      = _metarDataArray[i]?.Raw ?? "---";
                _metarLabels[i].ForeColor = McduPalette.Ink;
                _metarConditionColors[i]  = ConditionToColor(_metarDataArray[i]?.Condition ?? MetarCondition.Unknown);
                _metarPanels[i]?.Invalidate();
            }

            if (_mapForm != null && !_mapForm.IsDisposed)
                _mapForm.SetMetarData(
                    _metarDataArray[0]?.WindDir, _metarDataArray[0]?.WindSpeedKt,
                    _metarDataArray[1]?.WindDir, _metarDataArray[1]?.WindSpeedKt);
        }

        private void UpdateMetarPanelState(MetarFetchState state)
        {
            if (InvokeRequired) { BeginInvoke(new Action<MetarFetchState>(UpdateMetarPanelState), state); return; }
            if (state != MetarFetchState.Fetching) return;
            for (int i = 0; i < 4; i++)
            {
                if (_metarLabels[i] == null || _metarDataArray[i] != null) continue;
                _metarLabels[i].Text      = "fetching...";
                _metarLabels[i].ForeColor = McduPalette.Ink;
            }
        }

        private static Color ConditionToColor(MetarCondition c)
        {
            switch (c)
            {
                case MetarCondition.VMC:  return McduPalette.LcdGreen;
                case MetarCondition.MVMC: return McduPalette.LcdAmber;
                case MetarCondition.IMC:  return McduPalette.LcdRed;
                default:                  return McduPalette.LcdDim;
            }
        }

        private void ShowMetarDecode(int idx)
        {
            var data = _metarDataArray[idx];
            if (data == null) return;
            new MetarDecodeForm(data).Show(this);
        }

        #endregion

        #region Cierre

        private void MinimizeToTray()
        {
            this.Hide();
            _notifyIcon?.ShowBalloonTip(2000, "vmsOpenAcars", "La aplicación sigue ejecutándose.", ToolTipIcon.Info);
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void ExitFromTray()
        {
            string msg = _isFlightActive
                ? _("Dlg_ActiveFlightExitMsg")
                : _("Dlg_ExitConfirmMsg");
            string title = _isFlightActive
                ? _("Dlg_ActiveFlightExitTitle")
                : _("Dlg_ExitConfirmTitle");

            // Sin owner: el form está oculto y ShowDialog(owner) falla si el parent no es visible.
            var result = EcamDialog.Show(msg, title, EcamDialogButtons.YesNo);
            if (result != DialogResult.Yes) return;

            if (_isFlightActive)
            {
                Task.Run(async () =>
                {
                    await _viewModel?.CancelFlight();
                    _viewModel?.Stop();
                    if (!IsDisposed)
                        BeginInvoke((Action)(() =>
                        {
                            _osd?.Close();
                            _osd = null;
                            _isFlightActive = false;
                            Close();
                        }));
                });
            }
            else
            {
                Close();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isFlightActive)
            {
                var result = EcamDialog.Show(this,
                    _("Dlg_ActiveFlightExitMsg"),
                    _("Dlg_ActiveFlightExitTitle"),
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
                        // Volver al hilo UI y cerrar el form principal limpiamente
                        if (!IsDisposed)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                _osd?.Close();
                                _osd = null;
                                _isFlightActive = false; // evitar reentrada en OnFormClosing
                                Close();
                            }));
                        }
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
                _osd?.Close();
                _osd = null;
                _viewModel?.Stop();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
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
                }
                catch { }
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

                // Conexión — estos cuatro quedan vacíos a propósito: son los datos de la
                // aerolínea virtual y del piloto. Un valor por defecto aquí sobrevive a
                // cualquier limpieza del .config y se comitearía al repositorio. El piloto
                // los introduce en Settings.
                { "language",      "es" },
                { "vms_api_url",   "" },
                { "airline",       "" },
                { "vms_api_key",   "" },
                { "simbrief_user", "" },

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

                // OSD
                { "osd_enabled",          "true" },
                { "osd_sound_enabled",    "true" },
                { "osd_duration_seconds", "4" },
                { "osd_screen_index",     "1" },
                { "osd_opacity",          "90" },
                { "osd_airspace_alerts",  "true" },

                // RAAS (guía de rodaje)
                { "raas_enabled",         "true" },
                { "raas_voice_enabled",   "true" },
                { "raas_volume",          "80" },

                // Guía de rodaje: interruptor maestro del piloto (checkbox «Taxi guidance»).
                // ACTIVADO por defecto: quien ya usaba la guía no puede notar el cambio.
                { "taxi_guidance_enabled", "true" },

                // Cabin Announcements
                { "cabin_announcements_enabled", "true" },
                { "cabin_announcements_volume",  "80" },

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
                {
                    var response = await Services.Http.HttpClientProvider.General.GetAsync(
                                       downloadUrl, HttpCompletionOption.ResponseHeadersRead);

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

                // 3. Obtener Updater.exe: preferir el del paquete de actualización (ya extraído
                //    en tempFolder); sólo copiar el instalado si el paquete no trae uno.
                //    Sobreescribir el Updater del paquete con el instalado crearía una
                //    dependencia circular que impediría recibir correcciones del propio Updater.
                string updaterSource = Path.Combine(appFolder, "Updater.exe");
                string updaterDest = Path.Combine(tempFolder, "Updater.exe");
                if (!File.Exists(updaterDest) && File.Exists(updaterSource))
                    File.Copy(updaterSource, updaterDest, overwrite: false);

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
