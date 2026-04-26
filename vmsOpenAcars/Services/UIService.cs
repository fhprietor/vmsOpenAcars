using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Services
{
    public class UIService
    {
        private readonly MainForm _form;
        private readonly FlightManager _flightManager;
        private readonly ApiService _apiService;

        public UIService(MainForm form, FlightManager flightManager, ApiService apiService)
        {
            _form = form;
            _flightManager = flightManager;
            _apiService = apiService;
        }

        private void Notify(string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_form.InvokeRequired)
            {
                _form.Invoke(new Action(() => _form.ShowNotification(message, icon)));
            }
            else
            {
                _form.ShowNotification(message, icon);
            }
        }

        public void AddLog(string message, Color color)
        {
            // Mostrar en el log (código existente)
            if (_form.txtIncomingMsg.InvokeRequired)
            {
                _form.txtIncomingMsg.Invoke(new Action(() =>
                {
                    _form.txtIncomingMsg.SelectionStart = 0;
                    _form.txtIncomingMsg.SelectionColor = color;
                    _form.txtIncomingMsg.SelectedText = $"{DateTime.UtcNow:HH:mm:ss} - {message}\n";
                }));
            }
            else
            {
                _form.txtIncomingMsg.SelectionStart = 0;
                _form.txtIncomingMsg.SelectionColor = color;
                _form.txtIncomingMsg.SelectedText = $"{DateTime.UtcNow:HH:mm:ss} - {message}\n";
            }
            if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId))
            {
                SendLogToAcars(message);
            }
            // Notificaciones para eventos importantes
            if (message.Contains("Takeoff"))
                Notify("🛫 Takeoff", ToolTipIcon.Info);
            else if (message.Contains("Landing") || message.Contains("Touchdown"))
                Notify("🛬 Landing", ToolTipIcon.Info);
            else if (message.Contains("Approach"))
                Notify("🛬 Entering approach", ToolTipIcon.Warning);
            else if (message.Contains("Go-around"))
                Notify("🔄 Go-around! Execute missed approach", ToolTipIcon.Warning);
            else if (message.Contains("Flight cancelled"))
                Notify("✖️ Flight cancelled", ToolTipIcon.Error);
            else if (message.Contains("PIREP filed"))
                Notify("✅ PIREP filed successfully", ToolTipIcon.Info);
            else if (message.Contains("OFP mismatch"))
                Notify("❌ OFP mismatch", ToolTipIcon.Error);
            else if (message.Contains("Simulator disconnected"))
                Notify("🔌 Simulator disconnected", ToolTipIcon.Warning);
        }

        // En Services/UIService.cs

        private void SendLogToAcars(string message)
        {
            try
            {
                // Obtener posición actual del simulador si está disponible
                double lat = 0;
                double lon = 0;

                if (_flightManager != null)
                {
                    lat = _flightManager.CurrentLat;
                    lon = _flightManager.CurrentLon;
                }

                var logRecord = new AcarsPosition
                {
                    type = 2,                                    // LOG
                    status = "SCH",                              // SCHEDULED
                    log = message,                               // El texto del mensaje
                    lat = lat,                                   // Latitud actual
                    lon = lon,                                   // Longitud actual
                    altitude_agl = 0,                            // No aplica para logs
                    altitude_msl = 0,                            // No aplica para logs
                    ias = null,
                    gs = null,
                    sim_time = DateTime.UtcNow,
                    source = "vmsOpenAcars"
                };

                var update = new AcarsPositionUpdate { positions = new[] { logRecord } };

                // Enviar de forma asíncrona sin bloquear
                Task.Run(async () =>
                {
                    await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending log to ACARS: {ex.Message}");
            }
        }

        public void UpdatePosition(string positionText)
        {
            if (_form.lblPos.InvokeRequired)
                _form.lblPos.Invoke(new Action(() => _form.lblPos.Text = positionText));
            else
                _form.lblPos.Text = positionText;
        }

        public void UpdatePhase(FlightPhase phase)
        {
            if (_form.lblPhase.InvokeRequired)
            {
                _form.lblPhase.Invoke(new Action(() => SetPhaseText(phase)));
            }
            else
            {
                SetPhaseText(phase);
            }
        }

        private void SetPhaseText(FlightPhase phase)
        {
            _form.lblPhase.Text = "PHASE\n" + phase.ToString().ToUpper();
            _form.lblPhase.ForeColor = GetPhaseColor(phase);
        }

        private Color GetPhaseColor(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.Idle: return Color.Gray;
                case FlightPhase.Boarding:
                case FlightPhase.Pushback:
                case FlightPhase.TaxiOut:
                case FlightPhase.TaxiIn:
                    return Theme.Taxi;
                case FlightPhase.Takeoff:
                    return Theme.Takeoff;
                case FlightPhase.Climb:
                case FlightPhase.Enroute:
                    return Theme.Enroute;
                case FlightPhase.Descent:
                case FlightPhase.Approach:
                    return Theme.Approach;
                case FlightPhase.Completed:
                    return Theme.Arrived;
                default:
                    return Theme.MainText;
            }
        }

        public void UpdateAirStatus(FlightPhase phase)
        {
            if (_form.lblAir.InvokeRequired)
            {
                _form.lblAir.Invoke(new Action(() => SetAirStatus(phase)));
            }
            else
            {
                SetAirStatus(phase);
            }
        }

        private void SetAirStatus(FlightPhase phase)
        {
            if (phase == FlightPhase.Idle)
            {
                _form.lblAir.Text = "AIR\n---";
                _form.lblAir.ForeColor = Color.Gray;
                return;
            }

            if (phase.IsAirborne())
            {
                _form.lblAir.Text = "AIR\nAIRBORNE";
                _form.lblAir.ForeColor = Theme.Enroute;
            }
            else
            {
                _form.lblAir.Text = "AIR\nGROUND";
                _form.lblAir.ForeColor = Theme.Taxi;
            }
        }

        public void UpdateValidationUI(ValidationStatus status)
        {
            if (_form.lblValidationStatus.InvokeRequired)
            {
                _form.lblValidationStatus.Invoke(new Action(() => SetValidationText(status)));
            }
            else
            {
                SetValidationText(status);
            }
            UpdateStartButtonState(status);
        }
        private void UpdateStartButtonState(ValidationStatus status)
        {
            // El botón solo se habilita si:
            // - ICAO coincide (plan vs aeropuerto asignado)
            // - GPS es válido (estás en el lugar correcto)
            // - El simulador está conectado
            bool shouldEnable = status.IcaoMatch && status.GpsValid && _flightManager?.IsSimulatorConnected == true;

            // Logs para depuración
#if DEBUG
          //  System.Diagnostics.Debug.WriteLine($"UpdateStartButtonState - ICAO: {status.IcaoMatch}, GPS: {status.GpsValid}, Sim: {_flightManager?.IsSimulatorConnected}, Enable: {shouldEnable}");
#endif
            if (_form.btnStartStop.InvokeRequired)
            {
                _form.btnStartStop.Invoke(new Action(() => {
                    _form.btnStartStop.Enabled = shouldEnable;
                }));
            }
            else
            {
                _form.btnStartStop.Enabled = shouldEnable;
            }
        }
        private void SetValidationText(ValidationStatus status)
        {
            string icaoIcon = status.IcaoMatch ? "✅" : (string.IsNullOrEmpty(status.SimbriefAirport) ? "---" : "❌");
            string gpsIcon  = status.GpsValid  ? "✅" : (status.DistanceFromAirport > 0 ? $"❌ {status.DistanceFromAirport:F1}NM" : "⏳");

            _form.lblValidationStatus.Text = $"ICAO {icaoIcon}  GPS {gpsIcon}";
            _form.lblValidationStatus.ForeColor = (status.IcaoMatch && status.GpsValid)
                ? Color.LightGreen
                : Color.Orange;
        }


        public void UpdateSimulatorName(string name)
        {
            if (_form.lblSimName.InvokeRequired)
                _form.lblSimName.Invoke(new Action(() => _form.lblSimName.Text = name));
            else
                _form.lblSimName.Text = name;
        }

        public void UpdateAcarsStatus(bool isOnline)
        {
            if (_form.lblAcarsStatus.InvokeRequired)
            {
                _form.lblAcarsStatus.Invoke(new Action(() => SetAcarsStatus(isOnline)));
            }
            else
            {
                SetAcarsStatus(isOnline);
            }
        }

        private void SetAcarsStatus(bool isOnline)
        {
            _form.lblAcarsStatus.Text = isOnline ? "📡 Online" : "⚠️ Offline";
            _form.lblAcarsStatus.ForeColor = isOnline ? Color.LightGreen : Color.Orange;
        }

        public void UpdateCurrentAirport(string airport)
        {
            if (_form.lblCurrentAirport.InvokeRequired)
                _form.lblCurrentAirport.Invoke(new Action(() => _form.lblCurrentAirport.Text = airport));
            else
                _form.lblCurrentAirport.Text = airport;
        }

        public void UpdateFlightInfo()
        {
            if (_form == null || _form.IsDisposed) return;

            if (_form.InvokeRequired)
            {
                _form.Invoke(new Action(() => _form.UpdateFlightInfoPanel()));
            }
            else
            {
                _form.UpdateFlightInfoPanel();
            }
        }
    }
}