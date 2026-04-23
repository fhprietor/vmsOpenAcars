using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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
            // 9 filas de datos + 1 fila de botones = 10 filas × 35px + título 35px + padding
            this.Size = new Size(500, 445);
            this.MinimumSize = new Size(460, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(20, 30, 40);
            this.Padding = new Padding(2);
            this.Font = new Font("Consolas", 10);

            // Borde exterior
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        this.ClientSize.Width - 1, this.ClientSize.Height - 1);
            };

            // ── Barra de título ──────────────────────────────────────────────
            pnlTitleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(30, 40, 50)
            };

            lblTitle = new Label
            {
                Text = _("SettingsTitle"),
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(10, 8),
                AutoSize = true
            };

            btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Size = new Size(30, 25),
                Location = new Point(this.ClientSize.Width - 35, 5),
                BackColor = Color.FromArgb(150, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => DialogResult = DialogResult.Cancel;

            pnlTitleBar.Controls.Add(lblTitle);
            pnlTitleBar.Controls.Add(btnClose);

            // Arrastre
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
                    var p = PointToScreen(e.Location);
                    this.Location = new Point(p.X - _dragStartPoint.X, p.Y - _dragStartPoint.Y);
                }
            };
            pnlTitleBar.MouseUp += (s, e) => _dragging = false;

            // ── Panel de contenido ────────────────────────────────────────────
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 30, 40),
                Padding = new Padding(10)
            };

            // ── TableLayoutPanel: 2 columnas, 10 filas ────────────────────────
            // Filas 0-8: campos de datos (35px cada una)
            // Fila 9   : botones Guardar/Cancelar (40px)
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));

            for (int i = 0; i < 9; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F)); // fila botones

            // Fila 0 — API URL
            layout.Controls.Add(CreateLabel("ApiUrl"), 0, 0);
            txtApiUrl = CreateTextBox();
            layout.Controls.Add(txtApiUrl, 1, 0);

            // Fila 1 — API Key
            layout.Controls.Add(CreateLabel("ApiKey"), 0, 1);
            txtApiKey = CreateTextBox();
            txtApiKey.UseSystemPasswordChar = true;
            layout.Controls.Add(txtApiKey, 1, 1);

            // Fila 2 — SimBrief User
            layout.Controls.Add(CreateLabel("SimbriefUser"), 0, 2);
            txtSimbriefUser = CreateTextBox();
            layout.Controls.Add(txtSimbriefUser, 1, 2);

            // Fila 3 — Airline
            layout.Controls.Add(CreateLabel("Airline"), 0, 3);
            txtAirline = CreateTextBox();
            layout.Controls.Add(txtAirline, 1, 3);

            // Fila 4 — Language
            layout.Controls.Add(CreateLabel("Language"), 0, 4);
            cmbLanguage = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 60),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            layout.Controls.Add(cmbLanguage, 1, 4);

            // ── Separador visual ──────────────────────────────────────────────
            var sep = new Label
            {
                Dock = DockStyle.Fill,
                Text = "── SimBrief Dispatch ──",
                ForeColor = Color.FromArgb(80, 160, 220),
                Font = new Font("Consolas", 8, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.SetColumnSpan(sep, 2);
            layout.Controls.Add(sep, 0, 5);

            // Fila 6 — SimBrief Units
            layout.Controls.Add(CreateLabel("SimbriefUnits"), 0, 6);
            cmbSimbriefUnits = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 60),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            cmbSimbriefUnits.Items.AddRange(new object[] { "lbs", "kgs" });
            layout.Controls.Add(cmbSimbriefUnits, 1, 6);

            // Fila 7 — Cost Index
            layout.Controls.Add(CreateLabel("SimbriefCI"), 0, 7);
            txtSimbriefCi = CreateTextBox();
            layout.Controls.Add(txtSimbriefCi, 1, 7);

            // Fila 8 — Extra Remarks
            layout.Controls.Add(CreateLabel("SimbriefRmk"), 0, 8);
            txtSimbriefExtraRmk = CreateTextBox();
            layout.Controls.Add(txtSimbriefExtraRmk, 1, 8);

            // Fila 9 — Botones (ocupa las 2 columnas, alineados a la derecha)
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };

            btnSave = new Button
            {
                Text = _("Save"),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(0, 100, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 10, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = _("Cancel"),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(100, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 10, FontStyle.Bold)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnCancel);

            layout.SetColumnSpan(btnPanel, 2);
            layout.Controls.Add(btnPanel, 0, 9);

            contentPanel.Controls.Add(layout);
            this.Controls.Add(contentPanel);
            this.Controls.Add(pnlTitleBar);

            this.Resize += (s, e) =>
                btnClose.Location = new Point(this.ClientSize.Width - 35, 5);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Label CreateLabel(string key)
        {
            return new Label
            {
                Text = _(key),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10)
            };
        }

        private TextBox CreateTextBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10)
            };
        }

        // ── Carga / Guardado ──────────────────────────────────────────────────

        private void LoadSettings()
        {
            txtApiUrl.Text = ConfigurationManager.AppSettings["vms_api_url"] ?? "";
            txtApiKey.Text = ConfigurationManager.AppSettings["vms_api_key"] ?? "";
            txtSimbriefUser.Text = ConfigurationManager.AppSettings["simbrief_user"] ?? "";
            txtAirline.Text = ConfigurationManager.AppSettings["airline"] ?? "";

            // SimBrief dispatch
            string units = ConfigurationManager.AppSettings["simbrief_units"] ?? "lbs";
            cmbSimbriefUnits.SelectedItem =
                cmbSimbriefUnits.Items.Contains(units) ? (object)units : "lbs";

            txtSimbriefCi.Text = ConfigurationManager.AppSettings["simbrief_civalue"] ?? "30";
            txtSimbriefExtraRmk.Text = ConfigurationManager.AppSettings["simbrief_extrarmk"] ?? "";
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

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                SetValue(config, "vms_api_url", txtApiUrl.Text.Trim());
                SetValue(config, "vms_api_key", txtApiKey.Text.Trim());
                SetValue(config, "simbrief_user", txtSimbriefUser.Text.Trim());
                SetValue(config, "airline", txtAirline.Text.Trim());

                if (cmbLanguage.SelectedItem != null)
                    SetValue(config, "language", cmbLanguage.SelectedItem.ToString());

                // SimBrief dispatch params
                if (cmbSimbriefUnits.SelectedItem != null)
                    SetValue(config, "simbrief_units", cmbSimbriefUnits.SelectedItem.ToString());
                SetValue(config, "simbrief_civalue", txtSimbriefCi.Text.Trim());
                SetValue(config, "simbrief_extrarmk", txtSimbriefExtraRmk.Text.Trim());

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                // Aplicar idioma inmediatamente
                if (cmbLanguage.SelectedItem != null)
                    LocalizationService.Instance.LoadLanguage(cmbLanguage.SelectedItem.ToString());

                using (var dlg = new EcamDialog(
                    "Configuración guardada.\n• Reinicie la aplicación para aplicar los cambios.",
                    "INFORMACIÓN", EcamDialogButtons.OK))
                {
                    dlg.ShowDialog(this);
                }

                DialogResult = DialogResult.OK;
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
    }
}