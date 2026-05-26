using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.UI.Forms
{
    public class SettingsForm : Form
    {
        private Panel pnlTitleBar;
        private Label lblTitle;
        private Button btnClose;
        private bool _dragging = false;
        private Point _dragStartPoint;

        // Campos existentes
        private TextBox txtApiUrl;
        private TextBox txtApiKey;
        private TextBox txtSimbriefUser;
        private TextBox txtAirline;
        private ComboBox cmbLanguage;

        // Campos SimBrief dispatch
        private ComboBox cmbSimbriefUnits;
        private TextBox txtSimbriefCi;
        private TextBox txtSimbriefExtraRmk;

        // NavData API
        private TextBox txtNavDataApiKey;
        private Label   lblNavDataStatus;

        // Landing log database
        private TextBox txtLandingLogPath;

        // OSD Overlay
        private CheckBox chkOsdEnabled;
        private CheckBox chkOsdSound;
        private Button   btnTestOsd;
        private NumericUpDown nudOsdDuration;
        private NumericUpDown nudOsdOpacity;

        // Cabin Announcements
        private CheckBox  chkCabinAnnouncements;
        private Button    btnTestCabin;
        private TrackBar  trkCabinVolume;
        private Label     lblCabinVolVal;
        private Label     lblCabinStatus;

        /// <summary>Set by MainForm to route TEST OSD clicks to the live OSD overlay.</summary>
        public Action<string, OsdSeverity> TestOsdCallback { get; set; }

        /// <summary>Set by MainForm to route TEST CABIN clicks to the active announcement service.</summary>
        public Func<string, Task<string>> TestCabinAnnouncementCallback { get; set; }

        /// <summary>Set by MainForm to propagate live volume changes to the active announcement service.</summary>
        public Action<int> CabinVolumeChangedCallback { get; set; }

        private Button btnSave;
        private Button btnCancel;

        public SettingsForm()
        {
            InitializeForm();
            LoadSettings();
            LoadLanguages();
        }

        private void InitializeForm()
        {
            // Landscape: wider form, roughly half the original height
            this.Size        = new Size(920, 560);
            this.MinimumSize = new Size(760, 520);
            this.StartPosition    = FormStartPosition.CenterParent;
            this.FormBorderStyle  = FormBorderStyle.None;
            this.BackColor        = Color.FromArgb(20, 30, 40);
            this.Padding          = new Padding(2);
            this.Font             = new Font("Consolas", 10);

            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        this.ClientSize.Width - 1, this.ClientSize.Height - 1);
            };

            // ── Title bar ────────────────────────────────────────────────────
            pnlTitleBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 35,
                BackColor = Color.FromArgb(30, 40, 50)
            };
            lblTitle = new Label
            {
                Text      = _("SettingsTitle"),
                Font      = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location  = new Point(10, 8),
                AutoSize  = true
            };
            btnClose = new Button
            {
                Text      = "✕",
                Font      = new Font("Arial", 12, FontStyle.Bold),
                Size      = new Size(30, 25),
                Location  = new Point(this.ClientSize.Width - 35, 5),
                BackColor = Color.FromArgb(150, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => DialogResult = DialogResult.Cancel;
            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(btnClose);

            pnlTitleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging      = true;
                    _dragStartPoint = new Point(e.X, e.Y);
                }
            };
            pnlTitleBar.MouseMove += (s, e) =>
            {
                if (_dragging)
                {
                    var p = PointToScreen(e.Location);
                    this.Location = new Point(p.X - _dragStartPoint.X, p.Y - _dragStartPoint.Y);
                }
            };
            pnlTitleBar.MouseUp += (s, e) => _dragging = false;

            // ── Bottom button panel ───────────────────────────────────────────
            var pnlButtons = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 44,
                BackColor = Color.Transparent
            };
            btnSave = new Button
            {
                Text      = _("Save"),
                Size      = new Size(110, 30),
                BackColor = Color.FromArgb(0, 100, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 10, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            btnCancel = new Button
            {
                Text      = _("Cancel"),
                Size      = new Size(110, 30),
                BackColor = Color.FromArgb(100, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 10, FontStyle.Bold)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            pnlButtons.Controls.Add(btnSave);
            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Resize += (s, e) =>
            {
                btnSave.Location   = new Point(pnlButtons.Width - 232, 7);
                btnCancel.Location = new Point(pnlButtons.Width - 118, 7);
            };

            // ── Fill content area ─────────────────────────────────────────────
            var contentPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 30, 40),
                Padding   = new Padding(8)
            };

            // ── Outer layout: left | thin separator | right ───────────────────
            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = Color.Transparent
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6F));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var divider = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 80, 120)
            };

            // ── Left table: Connection / SimBrief / NavData (12 rows × 35 px) ─
            var left = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 12,
                BackColor   = Color.Transparent
            };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            for (int i = 0; i < 12; i++)
                left.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));

            // row 0 — API URL
            left.Controls.Add(CreateLabel("ApiUrl"), 0, 0);
            txtApiUrl = CreateTextBox();
            left.Controls.Add(txtApiUrl, 1, 0);

            // row 1 — API Key
            left.Controls.Add(CreateLabel("ApiKey"), 0, 1);
            txtApiKey = CreateTextBox();
            txtApiKey.UseSystemPasswordChar = true;
            left.Controls.Add(txtApiKey, 1, 1);

            // row 2 — SimBrief User
            left.Controls.Add(CreateLabel("SimbriefUser"), 0, 2);
            txtSimbriefUser = CreateTextBox();
            left.Controls.Add(txtSimbriefUser, 1, 2);

            // row 3 — Airline
            left.Controls.Add(CreateLabel("Airline"), 0, 3);
            txtAirline = CreateTextBox();
            left.Controls.Add(txtAirline, 1, 3);

            // row 4 — Language
            left.Controls.Add(CreateLabel("Language"), 0, 4);
            cmbLanguage = new ComboBox
            {
                Dock          = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Color.FromArgb(50, 50, 60),
                ForeColor     = Color.White,
                Font          = new Font("Consolas", 10)
            };
            left.Controls.Add(cmbLanguage, 1, 4);

            // row 5 — SimBrief separator
            var sepSimBrief = CreateSeparator("── SimBrief Dispatch ──");
            left.SetColumnSpan(sepSimBrief, 2);
            left.Controls.Add(sepSimBrief, 0, 5);

            // row 6 — SimBrief Units
            left.Controls.Add(CreateLabel("SimbriefUnits"), 0, 6);
            cmbSimbriefUnits = new ComboBox
            {
                Dock          = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Color.FromArgb(50, 50, 60),
                ForeColor     = Color.White,
                Font          = new Font("Consolas", 10)
            };
            cmbSimbriefUnits.Items.AddRange(new object[] { "lbs", "kgs" });
            left.Controls.Add(cmbSimbriefUnits, 1, 6);

            // row 7 — Cost Index
            left.Controls.Add(CreateLabel("SimbriefCI"), 0, 7);
            txtSimbriefCi = CreateTextBox();
            left.Controls.Add(txtSimbriefCi, 1, 7);

            // row 8 — Extra Remarks
            left.Controls.Add(CreateLabel("SimbriefRmk"), 0, 8);
            txtSimbriefExtraRmk = CreateTextBox();
            left.Controls.Add(txtSimbriefExtraRmk, 1, 8);

            // row 9 — NavData separator
            var sepNavData = CreateSeparator("── NavData API ──");
            left.SetColumnSpan(sepNavData, 2);
            left.Controls.Add(sepNavData, 0, 9);

            // row 10 — NavData API Key
            left.Controls.Add(CreateLabel("NavDataKey"), 0, 10);
            txtNavDataApiKey = CreateTextBox();
            txtNavDataApiKey.UseSystemPasswordChar = true;
            left.Controls.Add(txtNavDataApiKey, 1, 10);

            // row 11 — NavData status + TEST
            left.Controls.Add(CreateLabel("NavData"), 0, 11);
            var navDataPanel = new Panel { Dock = DockStyle.Fill };
            lblNavDataStatus = new Label
            {
                Text      = AppConfig.NavDataApiUrl,
                ForeColor = Color.FromArgb(160, 200, 160),
                Font      = new Font("Consolas", 9),
                Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Left      = 0,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var btnTestApi = new Button
            {
                Text      = "TEST",
                Width     = 46,
                Height    = 22,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.FromArgb(50, 70, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 8)
            };
            btnTestApi.FlatAppearance.BorderSize = 0;
            btnTestApi.Click += async (s, ev) =>
            {
                btnTestApi.Enabled  = false;
                lblNavDataStatus.Text      = "Connecting...";
                lblNavDataStatus.ForeColor = Color.FromArgb(200, 200, 100);
                var result = await NavDataClient.TestApiAsync(txtNavDataApiKey.Text.Trim())
                                               .ConfigureAwait(true);
                if (!result.Reachable)
                {
                    lblNavDataStatus.Text      = _("Stg_ConnFailed");
                    lblNavDataStatus.ForeColor = Color.FromArgb(220, 80, 80);
                }
                else if (!result.KeyValid)
                {
                    lblNavDataStatus.Text      = _("Stg_NavDataKeyInvalid");
                    lblNavDataStatus.ForeColor = Color.FromArgb(220, 140, 0);
                }
                else
                {
                    string cycle      = result.NavStatus?.AiracCycle      ?? "—";
                    string validUntil = result.NavStatus?.AiracValidUntil ?? "—";
                    bool   expired    = !string.IsNullOrEmpty(result.NavStatus?.AiracValidUntil)
                                        && DateTime.TryParse(result.NavStatus.AiracValidUntil,
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.None, out var d)
                                        && d.Date < DateTime.UtcNow.Date;
                    lblNavDataStatus.Text      = expired
                        ? $"AIRAC {cycle}  until {validUntil}  ⚠ EXPIRED"
                        : $"AIRAC {cycle}  until {validUntil}";
                    lblNavDataStatus.ForeColor = expired
                        ? Color.FromArgb(220, 120, 0)
                        : Color.FromArgb(100, 220, 100);
                }
                btnTestApi.Enabled = true;
            };
            navDataPanel.Controls.Add(lblNavDataStatus);
            navDataPanel.Controls.Add(btnTestApi);
            navDataPanel.Resize += (s, ev) =>
            {
                btnTestApi.Left          = navDataPanel.Width - btnTestApi.Width;
                btnTestApi.Top           = (navDataPanel.Height - btnTestApi.Height) / 2;
                lblNavDataStatus.Width   = navDataPanel.Width - btnTestApi.Width - 4;
                lblNavDataStatus.Top     = (navDataPanel.Height - lblNavDataStatus.Height) / 2;
            };
            left.Controls.Add(navDataPanel, 1, 11);

            // ── Right table: Landing Log / OSD / Cabin (10 rows × 35 px + 1 status) ──
            var right = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 11,
                BackColor   = Color.Transparent
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            for (int i = 0; i < 10; i++)
                right.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F)); // status row

            // row 0 — Landing Log separator
            var sepLog = CreateSeparator("── Landing Log ──");
            right.SetColumnSpan(sepLog, 2);
            right.Controls.Add(sepLog, 0, 0);

            // row 1 — Landing log DB path
            right.Controls.Add(CreateLabel("Landing DB"), 0, 1);
            var logPanel = new Panel { Dock = DockStyle.Fill };
            txtLandingLogPath = new TextBox
            {
                BackColor   = Color.FromArgb(50, 50, 60),
                ForeColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Consolas", 10),
                Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Left        = 0,
                Height      = 22
            };
            var btnBrowseLog = new Button
            {
                Text      = "...",
                Width     = 28,
                Height    = 22,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.FromArgb(50, 70, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 9)
            };
            btnBrowseLog.FlatAppearance.BorderSize = 0;
            btnBrowseLog.Click += (s, ev) =>
            {
                using (var ofd = new OpenFileDialog
                {
                    Title           = "Select landing log database",
                    Filter          = "SQLite DB (*.sqlite)|*.sqlite|All files (*.*)|*.*",
                    CheckFileExists = false,
                    FileName        = string.IsNullOrEmpty(txtLandingLogPath.Text)
                                          ? "landing_log.sqlite"
                                          : txtLandingLogPath.Text
                })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                        txtLandingLogPath.Text = ofd.FileName;
                }
            };
            logPanel.Controls.Add(txtLandingLogPath);
            logPanel.Controls.Add(btnBrowseLog);
            logPanel.Resize += (s, ev) =>
            {
                btnBrowseLog.Left       = logPanel.Width - btnBrowseLog.Width;
                btnBrowseLog.Top        = (logPanel.Height - btnBrowseLog.Height) / 2;
                txtLandingLogPath.Width = logPanel.Width - btnBrowseLog.Width - 2;
                txtLandingLogPath.Top   = (logPanel.Height - txtLandingLogPath.Height) / 2;
            };
            right.Controls.Add(logPanel, 1, 1);

            // row 2 — OSD separator
            var sepOsd = CreateSeparator("── OSD Overlay ──");
            right.SetColumnSpan(sepOsd, 2);
            right.Controls.Add(sepOsd, 0, 2);

            // row 3 — OSD Enabled
            right.Controls.Add(CreateLabel("OSD"), 0, 3);
            chkOsdEnabled = new CheckBox
            {
                Dock      = DockStyle.Fill,
                Text      = "Enabled",
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10)
            };
            chkOsdEnabled.CheckedChanged += (s, ev) =>
            {
                AppConfig.OsdEnabled = chkOsdEnabled.Checked;
                SaveConfigKey("osd_enabled", chkOsdEnabled.Checked.ToString().ToLower());
            };
            right.Controls.Add(chkOsdEnabled, 1, 3);

            // row 4 — Duration
            right.Controls.Add(CreateLabel("Duration (s)"), 0, 4);
            nudOsdDuration = new NumericUpDown
            {
                Dock      = DockStyle.Fill,
                Minimum   = 1,
                Maximum   = 30,
                Value     = 4,
                BackColor = Color.FromArgb(50, 50, 60),
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10)
            };
            nudOsdDuration.ValueChanged += (s, ev) =>
            {
                AppConfig.OsdDurationSeconds = (int)nudOsdDuration.Value;
                SaveConfigKey("osd_duration_seconds", ((int)nudOsdDuration.Value).ToString());
            };
            right.Controls.Add(nudOsdDuration, 1, 4);

            // row 5 — Opacity
            right.Controls.Add(CreateLabel("Opacity (%)"), 0, 5);
            nudOsdOpacity = new NumericUpDown
            {
                Dock      = DockStyle.Fill,
                Minimum   = 10,
                Maximum   = 100,
                Increment = 5,
                Value     = 90,
                BackColor = Color.FromArgb(50, 50, 60),
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10)
            };
            nudOsdOpacity.ValueChanged += (s, ev) =>
            {
                AppConfig.OsdOpacity = (int)nudOsdOpacity.Value;
                SaveConfigKey("osd_opacity", ((int)nudOsdOpacity.Value).ToString());
            };
            right.Controls.Add(nudOsdOpacity, 1, 5);

            // row 6 — OSD chimes + TEST OSD button
            right.Controls.Add(CreateLabel("Chimes"), 0, 6);
            var chimesPanel = new FlowLayoutPanel
            {
                Dock         = DockStyle.Fill,
                BackColor    = Color.Transparent,
                WrapContents = false,
                Padding      = new Padding(0)
            };
            chkOsdSound = new CheckBox
            {
                Text      = "Play cockpit chimes",
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10),
                AutoSize  = true,
                Checked   = true,
                Margin    = new Padding(0, 6, 8, 0)
            };
            chkOsdSound.CheckedChanged += (s, ev) =>
            {
                AppConfig.OsdSoundEnabled = chkOsdSound.Checked;
                SaveConfigKey("osd_sound_enabled", chkOsdSound.Checked.ToString().ToLower());
            };
            btnTestOsd = new Button
            {
                Text      = "TEST ▾",
                Font      = new Font("Consolas", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 60, 90),
                ForeColor = Color.Cyan,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(72, 24),
                Margin    = new Padding(0, 4, 0, 0)
            };
            btnTestOsd.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 180);
            btnTestOsd.Click += BtnTestOsd_Click;
            chimesPanel.Controls.Add(chkOsdSound);
            chimesPanel.Controls.Add(btnTestOsd);
            right.Controls.Add(chimesPanel, 1, 6);

            // row 7 — Cabin Announcements separator
            var sepCabin = CreateSeparator("── Cabin Announcements ──");
            right.SetColumnSpan(sepCabin, 2);
            right.Controls.Add(sepCabin, 0, 7);

            // row 8 — Cabin toggle + TEST cabin button
            right.Controls.Add(CreateLabel("Cabin Ann."), 0, 8);
            var cabinPanel = new FlowLayoutPanel
            {
                Dock         = DockStyle.Fill,
                BackColor    = Color.Transparent,
                WrapContents = false,
                Padding      = new Padding(0)
            };
            chkCabinAnnouncements = new CheckBox
            {
                Text      = "Enabled",
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10),
                AutoSize  = true,
                Margin    = new Padding(0, 6, 8, 0)
            };
            chkCabinAnnouncements.CheckedChanged += (s, ev) =>
            {
                AppConfig.CabinAnnouncementsEnabled = chkCabinAnnouncements.Checked;
                SaveConfigKey("cabin_announcements_enabled",
                    chkCabinAnnouncements.Checked.ToString().ToLower());
            };
            btnTestCabin = new Button
            {
                Text      = "TEST ▾",
                Font      = new Font("Consolas", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 60, 90),
                ForeColor = Color.Cyan,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(72, 24),
                Margin    = new Padding(0, 4, 0, 0)
            };
            btnTestCabin.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 180);
            btnTestCabin.Click += BtnTestCabin_Click;
            cabinPanel.Controls.Add(chkCabinAnnouncements);
            cabinPanel.Controls.Add(btnTestCabin);
            right.Controls.Add(cabinPanel, 1, 8);

            // row 9 — volume slider
            right.Controls.Add(CreateLabel("Volume"), 0, 9);
            var volPanel = new FlowLayoutPanel
            {
                Dock         = DockStyle.Fill,
                BackColor    = Color.Transparent,
                WrapContents = false,
                Padding      = new Padding(0)
            };
            trkCabinVolume = new TrackBar
            {
                Minimum       = 0,
                Maximum       = 200,
                TickFrequency = 10,
                SmallChange   = 5,
                LargeChange   = 10,
                Width         = 130,
                Height        = 28,
                BackColor     = Color.FromArgb(28, 36, 48)
            };
            lblCabinVolVal = new Label
            {
                Text      = "80%",
                ForeColor = Color.White,
                Font      = new Font("Consolas", 9),
                AutoSize  = true,
                Margin    = new Padding(4, 8, 0, 0)
            };
            trkCabinVolume.ValueChanged += (s, ev) =>
            {
                lblCabinVolVal.Text = trkCabinVolume.Value + "%";
                CabinVolumeChangedCallback?.Invoke(trkCabinVolume.Value);
                SaveConfigKey("cabin_announcements_volume", trkCabinVolume.Value.ToString());
            };
            volPanel.Controls.Add(trkCabinVolume);
            volPanel.Controls.Add(lblCabinVolVal);
            right.Controls.Add(volPanel, 1, 9);

            // row 10 — cabin test status (spans both columns)
            lblCabinStatus = new Label
            {
                Dock      = DockStyle.Fill,
                Text      = "",
                ForeColor = Color.FromArgb(140, 200, 140),
                Font      = new Font("Consolas", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(4, 0, 0, 0)
            };
            right.SetColumnSpan(lblCabinStatus, 2);
            right.Controls.Add(lblCabinStatus, 0, 10);

            // ── Assemble ──────────────────────────────────────────────────────
            outer.Controls.Add(left,    0, 0);
            outer.Controls.Add(divider, 1, 0);
            outer.Controls.Add(right,   2, 0);

            contentPanel.Controls.Add(outer);

            // Add in docking priority order: Fill first, Bottom second, Top last
            this.Controls.Add(contentPanel);
            this.Controls.Add(pnlButtons);
            this.Controls.Add(pnlTitleBar);

            this.Resize += (s, e) =>
                btnClose.Location = new Point(this.ClientSize.Width - 35, 5);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Label CreateLabel(string key)
        {
            return new Label
            {
                Text      = _(key),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.LightGreen,
                Font      = new Font("Consolas", 10)
            };
        }

        private TextBox CreateTextBox()
        {
            return new TextBox
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.FromArgb(50, 50, 60),
                ForeColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Consolas", 10)
            };
        }

        private Label CreateSeparator(string text)
        {
            return new Label
            {
                Dock      = DockStyle.Fill,
                Text      = text,
                ForeColor = Color.FromArgb(80, 160, 220),
                Font      = new Font("Consolas", 8, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        private void LoadSettings()
        {
            txtApiUrl.Text       = ConfigurationManager.AppSettings["vms_api_url"]  ?? "";
            txtApiKey.Text       = ConfigurationManager.AppSettings["vms_api_key"]  ?? "";
            txtSimbriefUser.Text = ConfigurationManager.AppSettings["simbrief_user"] ?? "";
            txtAirline.Text      = ConfigurationManager.AppSettings["airline"]       ?? "";

            string units = ConfigurationManager.AppSettings["simbrief_units"] ?? "lbs";
            cmbSimbriefUnits.SelectedItem =
                cmbSimbriefUnits.Items.Contains(units) ? (object)units : "lbs";

            txtSimbriefCi.Text       = ConfigurationManager.AppSettings["simbrief_civalue"]  ?? "30";
            txtSimbriefExtraRmk.Text = ConfigurationManager.AppSettings["simbrief_extrarmk"] ?? "";
            txtNavDataApiKey.Text    = ConfigurationManager.AppSettings["navdata_api_key"]    ?? "";
            lblNavDataStatus.Text    = AppConfig.NavDataApiUrl;
            txtLandingLogPath.Text   = ConfigurationManager.AppSettings["landing_log_path"]   ?? "";

            bool osdEnabled = true;
            if (bool.TryParse(ConfigurationManager.AppSettings["osd_enabled"], out bool osdParsed))
                osdEnabled = osdParsed;
            chkOsdEnabled.Checked = osdEnabled;

            bool osdSound = true;
            if (bool.TryParse(ConfigurationManager.AppSettings["osd_sound_enabled"], out bool osdSoundParsed))
                osdSound = osdSoundParsed;
            chkOsdSound.Checked = osdSound;

            int osdDuration = 4;
            if (int.TryParse(ConfigurationManager.AppSettings["osd_duration_seconds"], out int durParsed))
                osdDuration = durParsed;
            nudOsdDuration.Value = Math.Max(nudOsdDuration.Minimum,
                                   Math.Min(nudOsdDuration.Maximum, osdDuration));

            int osdOpacity = 90;
            if (int.TryParse(ConfigurationManager.AppSettings["osd_opacity"], out int opacityParsed))
                osdOpacity = opacityParsed;
            nudOsdOpacity.Value = Math.Max(nudOsdOpacity.Minimum,
                                  Math.Min(nudOsdOpacity.Maximum, osdOpacity));

            bool cabinAnn = true;
            if (bool.TryParse(ConfigurationManager.AppSettings["cabin_announcements_enabled"], out bool cabinParsed))
                cabinAnn = cabinParsed;
            chkCabinAnnouncements.Checked = cabinAnn;

            int cabinVol = 80;
            if (int.TryParse(ConfigurationManager.AppSettings["cabin_announcements_volume"], out int cabinVolParsed))
                cabinVol = Math.Max(0, Math.Min(200, cabinVolParsed));
            trkCabinVolume.Value  = cabinVol;
            lblCabinVolVal.Text   = cabinVol + "%";
        }

        private void LoadLanguages()
        {
            string langPath = Path.Combine(Application.StartupPath, "Languages");
            if (Directory.Exists(langPath))
            {
                var files = Directory.GetFiles(langPath, "*.json")
                                     .Select(Path.GetFileNameWithoutExtension)
                                     .ToArray();
                cmbLanguage.Items.AddRange(files);
                string currentLang = ConfigurationManager.AppSettings["language"] ?? "es";
                if (cmbLanguage.Items.Contains(currentLang))
                    cmbLanguage.SelectedItem = currentLang;
            }
        }

        private bool HasChanges()
        {
            string Cfg(string key, string def = "") =>
                ConfigurationManager.AppSettings[key] ?? def;

            return
                txtApiUrl.Text.Trim()                              != Cfg("vms_api_url")                     ||
                txtApiKey.Text.Trim()                              != Cfg("vms_api_key")                     ||
                txtSimbriefUser.Text.Trim()                        != Cfg("simbrief_user")                   ||
                txtAirline.Text.Trim()                             != Cfg("airline")                         ||
                (cmbLanguage.SelectedItem?.ToString()      ?? "")  != Cfg("language", "es")                  ||
                (cmbSimbriefUnits.SelectedItem?.ToString() ?? "")  != Cfg("simbrief_units", "lbs")           ||
                txtSimbriefCi.Text.Trim()                          != Cfg("simbrief_civalue", "30")          ||
                txtSimbriefExtraRmk.Text.Trim()                    != Cfg("simbrief_extrarmk")               ||
                txtNavDataApiKey.Text.Trim()                       != Cfg("navdata_api_key")                 ||
                txtLandingLogPath.Text.Trim()                      != Cfg("landing_log_path");
                // osd_* and cabin_announcements_* are auto-saved on change — excluded from HasChanges
        }

        // ── Test handlers ─────────────────────────────────────────────────────

        private void BtnTestOsd_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(20, 30, 40),
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10)
            };
            foreach (OsdSeverity sev in Enum.GetValues(typeof(OsdSeverity)))
            {
                var s     = sev;
                string label = "TEST  —  " + s.ToString().ToUpper();
                var item  = new ToolStripMenuItem(s.ToString())
                {
                    BackColor = Color.FromArgb(20, 30, 40),
                    ForeColor = Color.White
                };
                item.Click += (o, a) =>
                {
                    OsdAudio.Play(s, forcePlay: chkOsdSound.Checked);
                    TestOsdCallback?.Invoke(label, s);
                };
                menu.Items.Add(item);
            }
            menu.Show(btnTestOsd, new Point(0, btnTestOsd.Height));
        }

        private void BtnTestCabin_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(20, 30, 40),
                ForeColor = Color.White,
                Font      = new Font("Consolas", 10)
            };
            string[] phases = { "boarding", "taxi_out", "on_runway", "cruise",
                                 "top_of_descent", "approach", "taxi_in" };
            foreach (string phase in phases)
            {
                string p    = phase;
                var item = new ToolStripMenuItem(p)
                {
                    BackColor = Color.FromArgb(20, 30, 40),
                    ForeColor = Color.White
                };
                item.Click += async (o, a) =>
                {
                    if (TestCabinAnnouncementCallback == null)
                    {
                        lblCabinStatus.ForeColor = Color.FromArgb(200, 140, 40);
                        lblCabinStatus.Text = "No active session";
                        return;
                    }
                    btnTestCabin.Enabled  = false;
                    lblCabinStatus.ForeColor = Color.FromArgb(200, 200, 100);
                    lblCabinStatus.Text   = "Fetching...";
                    string result = await TestCabinAnnouncementCallback(p).ConfigureAwait(true);
                    lblCabinStatus.Text      = result;
                    lblCabinStatus.ForeColor = result.StartsWith("OK")
                        ? Color.FromArgb(100, 220, 100)
                        : Color.FromArgb(220, 100, 100);
                    if (result.StartsWith("OK"))
                        TestOsdCallback?.Invoke("CABIN TEST  —  " + p.Replace('_', ' ').ToUpper(), OsdSeverity.Info);
                    btnTestCabin.Enabled = true;
                };
                menu.Items.Add(item);
            }
            menu.Show(btnTestCabin, new Point(0, btnTestCabin.Height));
        }

        // ── Save ──────────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, EventArgs e)
        {
            bool changed = HasChanges();

            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                SetValue(config, "vms_api_url",       txtApiUrl.Text.Trim());
                SetValue(config, "vms_api_key",       txtApiKey.Text.Trim());
                SetValue(config, "simbrief_user",     txtSimbriefUser.Text.Trim());
                SetValue(config, "airline",           txtAirline.Text.Trim());

                if (cmbLanguage.SelectedItem != null)
                    SetValue(config, "language", cmbLanguage.SelectedItem.ToString());

                if (cmbSimbriefUnits.SelectedItem != null)
                    SetValue(config, "simbrief_units", cmbSimbriefUnits.SelectedItem.ToString());
                SetValue(config, "simbrief_civalue",  txtSimbriefCi.Text.Trim());
                SetValue(config, "simbrief_extrarmk", txtSimbriefExtraRmk.Text.Trim());
                SetValue(config, "navdata_api_key",   txtNavDataApiKey.Text.Trim());
                SetValue(config, "landing_log_path",  txtLandingLogPath.Text.Trim());

                // osd_* and cabin_announcements_* are auto-saved on change — not saved here

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                if (!changed)
                {
                    DialogResult = DialogResult.OK;
                    return;
                }

                using (var dlg = new EcamDialog(
                    "Configuración guardada.\nLa aplicación se reiniciará para aplicar los cambios.",
                    "REINICIANDO", EcamDialogButtons.OK))
                {
                    dlg.ShowDialog(this);
                }

                Application.Restart();
            }
            catch (Exception ex)
            {
                using (var dlg = new EcamDialog($"Error: {ex.Message}", "ERROR"))
                    dlg.ShowDialog(this);
            }
        }

        private void SetValue(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] != null)
                config.AppSettings.Settings[key].Value = value;
            else
                config.AppSettings.Settings.Add(key, value);
        }

        private void SaveConfigKey(string key, string value)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                SetValue(config, key, value);
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch { }
        }
    }
}
