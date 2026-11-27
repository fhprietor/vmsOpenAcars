using System;
using System.Configuration;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    /// <summary>
    /// **El login del primer arranque** (v1.0.0). Hasta ahora el piloto tenía que copiar su `api_key`
    /// del perfil de phpVMS y pegarla en la configuración: fricción y una fuente de errores (clave mal
    /// copiada, caducada, regenerada). Aquí entra con el usuario y la contraseña que ya usa en la
    /// web y el cliente recibe la clave.
    ///
    /// Tres decisiones que no son de estilo:
    ///
    /// 1. **La contraseña no se guarda**: ni en disco, ni en el log, ni en el mensaje de error. Se
    ///    borra del campo en cuanto el intento falla.
    /// 2. **La URL se pide aquí** porque una instalación limpia no la tiene: la plantilla lleva un
    ///    marcador (`your_phpvms_url`), no una dirección de aerolínea.
    /// 3. **Solo HTTPS**: la respuesta entrega una credencial de larga vida. `AuthService` rechaza un
    ///    `http://` con un mensaje que se lee en la propia pantalla.
    /// </summary>
    internal sealed class LoginForm : Form
    {
        private static readonly Color Background = Color.FromArgb(30, 40, 50);
        private static readonly Color FieldBackground = Color.FromArgb(20, 30, 40);
        private static readonly Color Accent = Color.FromArgb(40, 80, 120);

        private readonly TextBox _url = new TextBox();
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _password = new TextBox();
        private readonly Label _status = new Label();
        private readonly Button _enter = new Button();
        private readonly Button _exit = new Button();
        private bool _working;

        /// <summary>`ident` del piloto si el login lo devolvió (`VHR017`), para saludarlo por su nombre.</summary>
        internal string PilotIdent { get; private set; }

        internal LoginForm(string initialUrl)
        {
            Text = L._("Login_Title");
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 372);
            BackColor = Background;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            var title = new Label
            {
                Text = L._("Login_Heading"),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(24, 18),
                AutoSize = true
            };

            var hint = new Label
            {
                Text = L._("Login_Hint"),
                ForeColor = Color.FromArgb(190, 200, 210),
                Location = new Point(24, 52),
                Size = new Size(470, 34)
            };

            AddLabel(L._("Login_Url"), 24, 96);
            AddField(_url, 24, 118);
            _url.Text = initialUrl ?? string.Empty;

            AddLabel(L._("Login_User"), 24, 156);
            AddField(_user, 24, 178);

            AddLabel(L._("Login_Password"), 24, 216);
            AddField(_password, 24, 238);
            _password.UseSystemPasswordChar = true;

            // Por encima de los botones: con el estado en y=272 y los botones en y=296, un mensaje de
            // error quedaba **debajo** de ellos y el piloto no leía por qué había fallado (1.0.1).
            _status.Location = new Point(24, 266);
            _status.Size = new Size(470, 44);
            _status.ForeColor = Color.FromArgb(220, 120, 120);

            _enter.Text = L._("Login_Enter");
            _enter.Size = new Size(140, 32);
            _enter.Location = new Point(226, 330);
            _enter.BackColor = Accent;
            _enter.ForeColor = Color.White;
            _enter.FlatStyle = FlatStyle.Flat;
            _enter.Click += OnEnter;

            _exit.Text = L._("Login_Exit");
            _exit.Size = new Size(120, 32);
            // Dentro del ancho: x=474 con 120 px de ancho se salía 74 px y el botón quedaba cortado (1.0.1).
            _exit.Location = new Point(374, 330);
            _exit.BackColor = Color.FromArgb(45, 55, 65);
            _exit.ForeColor = Color.White;
            _exit.FlatStyle = FlatStyle.Flat;
            _exit.DialogResult = DialogResult.Cancel;

            Controls.Add(title);
            Controls.Add(hint);
            Controls.Add(_status);
            Controls.Add(_enter);
            Controls.Add(_exit);

            AcceptButton = _enter;
            // Enter entra, y al fallar el foco vuelve a la contraseña para reintentar sin ratón.
            Shown += (s, e) => { if (_user.Text.Trim().Length > 0) _password.Focus(); };
            CancelButton = _exit;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 210, 220)
            });
        }

        private void AddField(TextBox box, int x, int y)
        {
            box.Location = new Point(x, y);
            box.Size = new Size(470, 26);
            box.BackColor = FieldBackground;
            box.ForeColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);
        }

        private void ShowStatus(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
        }

        private void SetWorking(bool working)
        {
            _working = working;
            _enter.Enabled = !working;
            _exit.Enabled = !working;
            _url.Enabled = !working;
            _user.Enabled = !working;
            _password.Enabled = !working;
            UseWaitCursor = working;
            if (working) ShowStatus(L._("Login_InProgress"), Color.FromArgb(190, 200, 210));
        }

        /// <summary>
        /// Guarda la URL en el `.config` para los próximos arranques. Es un extra: si el piloto no
        /// tiene permisos sobre su propio `.config`, la sesión ya funciona y solo tendría que
        /// volver a escribirla la próxima vez.
        /// </summary>
        private static void PersistUrl(string url)
        {
            try
            {
                string value = (url ?? string.Empty).Trim();
                if (value.Length == 0) return;
                string error;
                if (!ConfigFileWriter.SetKeys(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                        new System.Collections.Generic.Dictionary<string, string> { ["vms_api_url"] = value },
                        out error))
                    return;
            }
            catch { /* sin permiso de escritura: la sesión ya tiene la URL */ }
        }

        private async void OnEnter(object sender, EventArgs e)
        {
            if (_working) return;

            SetWorking(true);
            bool close = false;
            try
            {
                LoginResult result = await AuthService.LoginAsync(_url.Text, _user.Text, _password.Text);

                switch (result.Outcome)
                {
                    case LoginOutcome.Ok:
                        if (!ApiKeyStore.Save(result.ApiKey))
                        {
                            // Sin poder guardarla, la sesión no la vería (`AppConfig` lee del almacén):
                            // es un fallo real y se dice, no se sigue como si nada.
                            ShowStatus(L._("Login_SaveFailed"), Color.OrangeRed);
                            break;
                        }

                        // La URL que el piloto acaba de escribir vale **ya** en esta sesión: el
                        // `ConfigurationManager` ya leyó el fichero y no vería el cambio hasta
                        // reiniciar. Y se guarda para los arranques siguientes.
                        AppConfig.SessionVmsApiUrl = _url.Text;
                        PersistUrl(_url.Text);

                        PilotIdent = result.PilotIdent;
                        if (result.Created) ShowStatus(L._("Login_Created"), Color.LightGreen);
                        close = true;
                        break;

                    case LoginOutcome.InvalidCredentials:
                        ShowStatus(L._("Login_Invalid"), Color.OrangeRed);
                        _password.Clear();
                        _password.Focus();
                        break;

                    case LoginOutcome.AccountNotActive:
                        ShowStatus(L._("Login_NotActive"), Color.OrangeRed);
                        break;

                    case LoginOutcome.TooManyAttempts:
                        ShowStatus(L._("Login_TooMany"), Color.OrangeRed);
                        break;

                    case LoginOutcome.BadRequest:
                        ShowStatus(L._("Login_Rejected"), Color.OrangeRed);
                        break;

                    default:
                        ShowStatus(string.Format(L._("Login_Unavailable"), result.Detail ?? "?"), Color.OrangeRed);
                        break;
                }
            }
            finally
            {
                SetWorking(false);
            }

            if (close)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
