using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.ViewModels;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.UI.Forms
{
    /// <summary>
    /// Popup de rodaje: el piloto confirma la pista de despegue y revisa (o reescribe) la ruta
    /// que propone el grafo de calles. Sale al encender la luz de taxi o al detectarse el
    /// TaxiOut, una vez por vuelo.
    ///
    /// La ruta es **editable** y se escribe separada por espacios ("B M K1 V") porque la
    /// sugerencia es la más corta por el grafo, no la que ha dado ATC: la instrucción real la
    /// conoce el piloto, y teclearla es más rápido que discutir con una ruta calculada.
    /// </summary>
    internal sealed class TaxiRouteForm : Form
    {
        private readonly TaxiRoutePrompt _prompt;

        private ComboBox       cmbRunway;
        private TextBox        txtRoute;
        private CheckBox       chkRaas;
        private CheckBox       chkVoice;
        private TrackBar       trkVolume;
        private Label          lblVolume;
        private Label          lblHint;
        private Button         btnRecompute;

        internal string SelectedRunway => cmbRunway?.SelectedItem?.ToString() ?? "";
        internal string RouteText      => txtRoute?.Text?.Trim() ?? "";
        internal bool   RaasEnabled    => chkRaas?.Checked ?? false;
        internal bool   VoiceEnabled   => chkVoice?.Checked ?? false;
        internal int    Volume         => trkVolume?.Value ?? 80;

        internal TaxiRouteForm(TaxiRoutePrompt prompt,
                               bool raasEnabled, bool voiceEnabled, int volume)
        {
            _prompt = prompt ?? new TaxiRoutePrompt();

            Text            = _("Raas_Title") + "  —  " + _prompt.Icao;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ClientSize      = new Size(560, 268);
            BackColor       = Color.FromArgb(20, 30, 40);
            ForeColor       = Color.White;
            Font            = new Font("Consolas", 10);

            BuildUi(raasEnabled, voiceEnabled, volume);

            if (cmbRunway.Items.Count > 0)
            {
                object def = _prompt.DefaultRunway ?? "";
                cmbRunway.SelectedItem = cmbRunway.Items.Contains(def) ? def : cmbRunway.Items[0];
            }
            // El evento SelectedIndexChanged del combo ya recalcula la ruta al fijar la pista.
        }

        private void BuildUi(bool raasEnabled, bool voiceEnabled, int volume)
        {
            // ── Pista ─────────────────────────────────────────────────────────────
            var lblRunway = new Label
            {
                Text = _("Raas_RunwayLabel"), Left = 14, Top = 18, Width = 150,
                ForeColor = Color.LightGreen
            };
            cmbRunway = new ComboBox
            {
                Left = 172, Top = 14, Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 60), ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            foreach (string r in _prompt.Runways) cmbRunway.Items.Add(r);
            cmbRunway.SelectedIndexChanged += (s, e) => RecomputeRoute();

            btnRecompute = new Button
            {
                Text = _("Raas_Recompute"), Left = 322, Top = 13, Width = 110, Height = 26,
                BackColor = Color.FromArgb(30, 60, 90), ForeColor = Color.Cyan,
                FlatStyle = FlatStyle.Flat, Font = new Font("Consolas", 8)
            };
            btnRecompute.FlatAppearance.BorderSize = 0;
            btnRecompute.Click += (s, e) => RecomputeRoute();

            // ── Ruta editable ─────────────────────────────────────────────────────
            var lblRoute = new Label
            {
                Text = _("Raas_RouteLabel"), Left = 14, Top = 62, Width = 150,
                ForeColor = Color.LightGreen
            };
            txtRoute = new TextBox
            {
                Left = 172, Top = 58, Width = 374,
                BackColor = Color.FromArgb(50, 50, 60), ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 12),
                Text = _prompt.SuggestedRoute ?? ""
            };

            lblHint = new Label
            {
                Left = 14, Top = 92, Width = 532, Height = 46,
                ForeColor = Color.FromArgb(150, 180, 210), Font = new Font("Consolas", 8),
                // Si el popup vuelve después del pushback, el piloto tiene que saber por qué: lo
                // que cambió no es la pista, es el punto desde el que arranca el rodaje.
                Text = _prompt.Recalculated ? _("Raas_RepromptHint") : _("Raas_Hint")
            };

            // ── RAAS y voz ────────────────────────────────────────────────────────
            chkRaas = new CheckBox
            {
                Text = _("Raas_RaasOn"), Left = 14, Top = 148, Width = 240, Checked = raasEnabled,
                ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            chkVoice = new CheckBox
            {
                Text = _("Raas_VoiceOn"), Left = 14, Top = 176, Width = 240, Checked = voiceEnabled,
                ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            trkVolume = new TrackBar
            {
                Left = 260, Top = 168, Width = 200, Height = 32,
                Minimum = 0, Maximum = 100, TickFrequency = 10,
                Value = volume < 0 ? 0 : (volume > 100 ? 100 : volume),
                BackColor = Color.FromArgb(28, 36, 48)
            };
            lblVolume = new Label
            {
                Left = 470, Top = 176, Width = 60, Text = trkVolume.Value + "%",
                ForeColor = Color.White
            };
            trkVolume.ValueChanged += (s, e) =>
            {
                lblVolume.Text = trkVolume.Value + "%";
                Services.RaasVoice.Volume = trkVolume.Value;
                // Que el piloto oiga de verdad cómo suena antes de arrancar la guía.
                Services.RaasVoice.Speak(_("Raas_TestPhrase"));
            };

            // ── Botones ───────────────────────────────────────────────────────────
            var btnStart = new Button
            {
                Text = _("Raas_Start"), Left = 300, Top = 216, Width = 156, Height = 32,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(30, 90, 50), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Consolas", 9, FontStyle.Bold)
            };
            btnStart.FlatAppearance.BorderSize = 0;

            var btnLater = new Button
            {
                Text = _("Raas_Later"), Left = 464, Top = 216, Width = 82, Height = 32,
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(60, 40, 40), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Consolas", 9)
            };
            btnLater.FlatAppearance.BorderSize = 0;

            AcceptButton = btnStart;
            CancelButton = btnLater;

            Controls.AddRange(new Control[]
            {
                lblRunway, cmbRunway, btnRecompute, lblRoute, txtRoute, lblHint,
                chkRaas, chkVoice, trkVolume, lblVolume, btnStart, btnLater
            });
        }

        private void RecomputeRoute()
        {
            if (_prompt.SuggestRoute == null) return;
            string suggested;
            try { suggested = _prompt.SuggestRoute(SelectedRunway) ?? ""; }
            catch { suggested = ""; }

            if (suggested.Length > 0)
            {
                txtRoute.Text      = suggested;
                lblHint.ForeColor  = Color.FromArgb(150, 180, 210);
            }
            else
            {
                // Sin ruta no se inventa nada: se dice y el piloto la escribe (o vuela sin guía).
                txtRoute.Text     = "";
                lblHint.Text      = _("Raas_NoRoute");
                lblHint.ForeColor = Color.FromArgb(220, 160, 80);
            }
        }
    }
}
