using System;
using System.Drawing;
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

        public UIService(MainForm form, FlightManager flightManager)
        {
            _form = form;
            _flightManager = flightManager;
        }

        public void AddLog(string message, Color color)
        {
            if (_form.txtIncomingMsg.InvokeRequired)
            {
                _form.txtIncomingMsg.Invoke(new Action(() =>
                {
                    _form.txtIncomingMsg.SelectionStart = 0;
                    _form.txtIncomingMsg.SelectionLength = 0;
                    _form.txtIncomingMsg.SelectionColor = color;
                    _form.txtIncomingMsg.SelectedText = $"{DateTime.UtcNow:HH:mm:ss} - {message}\n";
                }));
            }
            else
            {
                _form.txtIncomingMsg.SelectionStart = 0;
                _form.txtIncomingMsg.SelectionLength = 0;
                _form.txtIncomingMsg.SelectionColor = color;
                _form.txtIncomingMsg.SelectedText = $"{DateTime.UtcNow:HH:mm:ss} - {message}\n";
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

        public void UpdateAltitude(int altitude)
        {
            if (_form.lblAltitude.InvokeRequired)
            {
                _form.lblAltitude.Invoke(new Action(() => SetAltitude(altitude)));
            }
            else
            {
                SetAltitude(altitude);
            }
        }

        private void SetAltitude(int altitude)
        {
            _form.lblAltitude.Text = $"ALT\n{altitude} FT";
            _form.lblAltitude.ForeColor = _flightManager.CurrentPhase.IsAirborne() ? Theme.Enroute : Color.Gray;
        }

        public void UpdateSpeed(int speed)
        {
            if (_form.lblSpeed.InvokeRequired)
            {
                _form.lblSpeed.Invoke(new Action(() => SetSpeed(speed)));
            }
            else
            {
                SetSpeed(speed);
            }
        }

        private void SetSpeed(int speed)
        {
            _form.lblSpeed.Text = $"SPD\n{speed} KT";
            _form.lblSpeed.ForeColor = speed > 3 ? Theme.Enroute : Color.Gray;
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
            string gpsIcon = status.GpsValid ? "✅" : (status.DistanceFromAirport > 0 ? "❌" : "⏳");
            string gpsText = status.DistanceFromAirport > 0 ? $" ({status.DistanceFromAirport:F1}km)" : "";

            if (status.IcaoMatch)
            {
                if (status.GpsValid)
                {
                    _form.lblValidationStatus.Text = $"VALIDACIÓN: ICAO ✅  GPS: ✅{gpsText}";
                    _form.lblValidationStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    _form.lblValidationStatus.Text = $"VALIDACIÓN: ICAO ✅  GPS: {gpsIcon}{gpsText}";
                    _form.lblValidationStatus.ForeColor = Color.Orange;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(status.SimbriefAirport))
                    _form.lblValidationStatus.Text = $"VALIDACIÓN: Sin plan  GPS: {gpsIcon}{gpsText}";
                else
                    _form.lblValidationStatus.Text = $"VALIDACIÓN: ICAO ❌ (phpVMS: {status.PhpVmsAirport} vs SIMBRIEF: {status.SimbriefAirport})  GPS: {gpsIcon}{gpsText}";

                _form.lblValidationStatus.ForeColor = Color.Orange;
            }
        }

        public void UpdateFlightInfo(string info)
        {
            if (_form._lblFlightNo.InvokeRequired)
            {
                _form._lblFlightNo.Invoke(new Action(() => SetFlightInfo(info)));
            }
            else
            {
                SetFlightInfo(info);
            }
        }

        private void SetFlightInfo(string info)
        {
            // Asumiendo que info tiene el formato que construiste en el ViewModel
            // Puedes parsearla o crear propiedades separadas en el ViewModel
            // Por simplicidad, actualizamos solo un label
        }

        public void UpdateSimulatorName(string name)
        {
            if (_form.lblSimName.InvokeRequired)
            {
                _form.lblSimName.Invoke(new Action(() => _form.lblSimName.Text = $"SIM: {name}"));
            }
            else
            {
                _form.lblSimName.Text = $"SIM: {name}";
            }
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
            if (isOnline)
            {
                _form.lblAcarsStatus.Text = "ACARS: 📡 Online";
                _form.lblAcarsStatus.ForeColor = Color.LightGreen;
            }
            else
            {
                _form.lblAcarsStatus.Text = "ACARS: ⚠️ Offline";
                _form.lblAcarsStatus.ForeColor = Color.Orange;
            }
        }

        public void UpdateCurrentAirport(string airport)
        {
            if (_form.lblCurrentAirport.InvokeRequired)
            {
                _form.lblCurrentAirport.Invoke(new Action(() => _form.lblCurrentAirport.Text = $"APT: {airport}"));
            }
            else
            {
                _form.lblCurrentAirport.Text = $"APT: {airport}";
            }
        }
    }
}