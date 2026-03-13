// ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FlightManager _flightManager;
        private readonly FsuipcService _fsuipc;
        private readonly ApiService _apiService;
        private readonly PhpVmsFlightService _phpVmsFlightService;
        private readonly SimbriefEnhancedService _simbriefEnhancedService;

        // Eventos para comunicación con la UI
        public event Action<string, Color> LogMessage;
        public event Action<FlightPhase> PhaseChanged;
        public event Action<ValidationStatus> PositionValidated;
        public event Action<string> AirportChanged;
        public event Action<bool> NetworkStatusChanged;
        public event Action<string> SimulatorNameChanged;

        // Propiedades enlazables
        private string _flightInfo;
        public string FlightInfo
        {
            get => _flightInfo;
            set { _flightInfo = value; OnPropertyChanged(); }
        }

        private ValidationStatus _validationStatus;
        public ValidationStatus ValidationStatus
        {
            get => _validationStatus;
            set { _validationStatus = value; OnPropertyChanged(); }
        }

        private string _simulatorName;
        public string SimulatorName
        {
            get => _simulatorName;
            set { _simulatorName = value; OnPropertyChanged(); }
        }

        private string _positionText;
        public string PositionText
        {
            get => _positionText;
            set { _positionText = value; OnPropertyChanged(); }
        }

        private string _currentAirport;
        public string CurrentAirport
        {
            get => _currentAirport;
            set { _currentAirport = value; OnPropertyChanged(); }
        }

        private FlightPhase _currentPhase;
        public FlightPhase CurrentPhase
        {
            get => _currentPhase;
            set { _currentPhase = value; OnPropertyChanged(); }
        }

        // Propiedades para el estado de los botones
        private bool _canStart;
        public bool CanStart
        {
            get => _canStart;
            set { _canStart = value; OnPropertyChanged(); }
        }

        private string _startStopText;
        public string StartStopText
        {
            get => _startStopText;
            set { _startStopText = value; OnPropertyChanged(); }
        }

        private Color _startStopColor;
        public Color StartStopColor
        {
            get => _startStopColor;
            set { _startStopColor = value; OnPropertyChanged(); }
        }

        private string _cancelText;
        public string CancelText
        {
            get => _cancelText;
            set { _cancelText = value; OnPropertyChanged(); }
        }

        public MainViewModel(
            FlightManager flightManager,
            FsuipcService fsuipc,
            ApiService apiService,
            PhpVmsFlightService phpVmsFlightService,
            SimbriefEnhancedService simbriefEnhancedService)
        {
            _flightManager = flightManager;
            _fsuipc = fsuipc;
            _apiService = apiService;
            _phpVmsFlightService = phpVmsFlightService;
            _simbriefEnhancedService = simbriefEnhancedService;

            // Suscribirse a eventos del FlightManager
            _flightManager.OnLog += (msg, color) => LogMessage?.Invoke(msg, color);
            _flightManager.PhaseChanged += (phase) =>
            {
                CurrentPhase = phase;
                PhaseChanged?.Invoke(phase);
            };
            _flightManager.OnAirportChanged += (airport) =>
            {
                CurrentAirport = airport;
                AirportChanged?.Invoke(airport);
            };
            _flightManager.OnPositionValidated += (status) =>
            {
                ValidationStatus = status;
                PositionValidated?.Invoke(status);
                // Actualizar botón START según condiciones
                UpdateStartButtonState();
            };

            // Suscribirse a eventos de FSUIPC
            // En el constructor de MainViewModel
            _fsuipc.Connected += (sender, e) =>
            {
                SimulatorName = _fsuipc.SimulatorName;
                SimulatorNameChanged?.Invoke(SimulatorName);
                // Aquí podrías actualizar estado de conexión
            };

            _fsuipc.Disconnected += (sender, e) =>
            {
                SimulatorName = "AWAITING SIM";
                SimulatorNameChanged?.Invoke(SimulatorName);
            };
            _fsuipc.DataUpdated += OnDataUpdated;

            // Inicializar textos
            StartStopText = "START";
            StartStopColor = Color.FromArgb(200, 100, 0);
            CancelText = "EXIT";
        }

        private void OnDataUpdated(object sender, DataUpdatedEventArgs e)
        {
            // Actualizar posición en UI
            PositionText = $"POS: {e.Latitude:F4}° / {e.Longitude:F4}°";

            // Actualizar FlightManager
            _flightManager?.UpdateTelemetry(
                (int)e.Altitude,
                (int)e.GroundSpeed,
                (int)e.VerticalSpeed,
                e.IsOnGround,
                e.FuelTotal,
                e.Latitude,
                e.Longitude,
                e.IndicatedAirspeed,
                e.FuelFlow,
                e.TransponderCode,
                e.AutopilotMaster,
                e.SimulationZuluTime,
                e.RadarAltitude,
                e.Order
            );

            // Validación de posición si no hay vuelo activo
            if (string.IsNullOrEmpty(_flightManager.ActivePirepId))
            {
                _flightManager.UpdatePositionValidation(e.Latitude, e.Longitude);
            }

            // Preparar telemetría para phpVMS (si hay vuelo activo)
            if (!string.IsNullOrEmpty(_flightManager.ActivePirepId))
            {
                // La lógica de preparación de telemetría se moverá aquí también
                // pero necesitaremos un servicio aparte para eso.
            }
        }

        private void UpdateStartButtonState()
        {
            CanStart = _flightManager.CanStartFlight() && _fsuipc.IsConnected;
        }

        public async Task Login(string pilotId) // Simplificado, debería ser más completo
        {
            // Llamar a ApiService para obtener datos del piloto
            var result = await _apiService.GetPilotData(); // Esto necesita un identificador
            if (result.Data != null)
            {
                _flightManager.SetActivePilot(result.Data);
                LogMessage?.Invoke($"✅ Login exitoso: {result.Data.Name}", Theme.Success);
                CurrentAirport = result.Data.CurrentAirport;
            }
            else
            {
                LogMessage?.Invoke($"❌ Error de login: {result.Error}", Theme.Danger);
            }
        }

        public async Task LoadSimbriefPlan()
        {
            // Similar a BtnSimbrief_Click
            if (_flightManager.ActivePilot == null)
            {
                LogMessage?.Invoke("Debes iniciar sesión primero.", Theme.Warning);
                return;
            }
            // Aquí deberías abrir el FlightPlannerForm, pero eso es UI.
            // Mejor que el ViewMode no abra formularios. Delegaremos eso a la UI.
            // Lanzaremos un evento para que la UI muestre el planificador.
            // Por ahora, lo dejamos así; luego lo separamos.
        }

        public async Task StartStopAction()
        {
            switch (StartStopText)
            {
                case "START":
                    if (!_flightManager.CanStartFlight())
                    {
                        LogMessage?.Invoke("⛔ No se cumplen las condiciones para iniciar el vuelo", Theme.Warning);
                        return;
                    }
                    bool started = await _flightManager.StartFlight(_flightManager.ActivePlan, _flightManager.ActivePilot);
                    if (started)
                    {
                        StartStopText = "ABORT";
                        StartStopColor = Color.Red;
                        CancelText = "CANCEL";
                        LogMessage?.Invoke("Vuelo iniciado", Theme.Success);
                    }
                    break;
                case "ABORT":
                    // Aquí necesitas mostrar un diálogo de confirmación, eso es UI.
                    // Mejor lanzar un evento y que la UI maneje la confirmación.
                    // Por simplicidad, asumimos que ya se confirmó.
                    bool aborted = await _flightManager.AbortFlight();
                    if (aborted)
                    {
                        StartStopText = "START";
                        StartStopColor = Color.FromArgb(200, 100, 0);
                        CanStart = false;
                        CancelText = "EXIT";
                        LogMessage?.Invoke("✖️ Vuelo cancelado", Theme.Warning);
                    }
                    break;
                case "SEND PIREP":
                    bool filed = await _flightManager.FilePirep();
                    if (filed)
                    {
                        StartStopText = "START";
                        StartStopColor = Color.FromArgb(200, 100, 0);
                        CanStart = false;
                        CancelText = "EXIT";
                        LogMessage?.Invoke("✅ Vuelo reportado", Theme.Success);
                    }
                    break;
            }
        }

        public void CancelAction()
        {
            if (CancelText == "EXIT")
            {
                // Confirmar salida, eso es UI
                // Lanzar evento
            }
            else
            {
                // Cancelar vuelo
                // Confirmar
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}