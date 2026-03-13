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

        private TextBox txtApiUrl;
        private TextBox txtApiKey;
        private TextBox txtSimbriefUser;
        private TextBox txtAirline;
        private ComboBox cmbLanguage;
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
            this.Size = new Size(450, 350);
            this.MinimumSize = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(20, 30, 40);
            this.Padding = new Padding(2); // Para el borde
            this.Font = new Font("Consolas", 10);

            // Dibujar borde
            this.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            };

            // Panel de título (barra de arrastre)
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
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 30, 40),
                Padding = new Padding(10)
            };

            // Layout para los controles
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            for (int i = 0; i < 6; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));

            // Etiquetas y campos
            layout.Controls.Add(CreateLabel("ApiUrl"), 0, 0);
            txtApiUrl = CreateTextBox();
            layout.Controls.Add(txtApiUrl, 1, 0);

            layout.Controls.Add(CreateLabel("ApiKey"), 0, 1);
            txtApiKey = CreateTextBox();
            txtApiKey.UseSystemPasswordChar = true;
            layout.Controls.Add(txtApiKey, 1, 1);

            layout.Controls.Add(CreateLabel("SimbriefUser"), 0, 2);
            txtSimbriefUser = CreateTextBox();
            layout.Controls.Add(txtSimbriefUser, 1, 2);

            layout.Controls.Add(CreateLabel("Airline"), 0, 3);
            txtAirline = CreateTextBox();
            layout.Controls.Add(txtAirline, 1, 3);

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

            // Panel de botones
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent
            };
            btnSave = new Button
            {
                Text = _("Save"),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 100, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 10, FontStyle.Bold)
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = _("Cancel"),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(100, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 10, FontStyle.Bold)
            };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnCancel);
            layout.Controls.Add(btnPanel, 1, 5);

            contentPanel.Controls.Add(layout);
            this.Controls.Add(contentPanel);
            this.Controls.Add(pnlTitleBar);

            // Ajustar posición del botón cerrar al redimensionar
            this.Resize += (s, e) =>
            {
                btnClose.Location = new Point(this.ClientSize.Width - 35, 5);
            };
        }

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

        private void LoadSettings()
        {
            txtApiUrl.Text = ConfigurationManager.AppSettings["vms_api_url"];
            txtApiKey.Text = ConfigurationManager.AppSettings["vms_api_key"];
            txtSimbriefUser.Text = ConfigurationManager.AppSettings["simbrief_user"];
            txtAirline.Text = ConfigurationManager.AppSettings["airline"];
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
                SetValue(config, "vms_api_url", txtApiUrl.Text);
                SetValue(config, "vms_api_key", txtApiKey.Text);
                SetValue(config, "simbrief_user", txtSimbriefUser.Text);
                SetValue(config, "airline", txtAirline.Text);
                if (cmbLanguage.SelectedItem != null)
                    SetValue(config, "language", cmbLanguage.SelectedItem.ToString());

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                // Aplicar idioma inmediatamente
                if (cmbLanguage.SelectedItem != null)
                {
                    LocalizationService.Instance.LoadLanguage(cmbLanguage.SelectedItem.ToString());
                }

                // Informar al usuario usando EcamDialog
                string message = "Configuración guardada.\n" +
                                 "• Reinicie la aplicación para que los cambios tengan efecto.";

                using (var dlg = new EcamDialog(message, "INFORMACIÓN",EcamDialogButtons.OK))
                {
                    dlg.ShowDialog(this);
                }

                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                using (var dlg = new EcamDialog($"Error: {ex.Message}", "ERROR"))
                {
                    dlg.ShowDialog(this);
                }
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