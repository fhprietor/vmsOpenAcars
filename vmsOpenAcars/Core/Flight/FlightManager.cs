using System;
using System.Threading.Tasks;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Core.Flight
{
    public class FlightManager
    {
        private readonly ApiService _apiService;
        private bool _wasOnGround = true;
        private bool _touchdownCaptured = false;


        public int CurrentIndicatedAirspeed { get; private set; }
        public int CurrentAltitude { get; private set; }
        public int CurrentVerticalSpeed { get; private set; }
        public int? TouchdownFpm { get; private set; }
        public FlightPhase CurrentPhase { get; private set; }
        public int CurrentGroundSpeed { get; private set; }
        public double CurrentFuel { get; private set; }
        public bool IsOnGround { get; private set; }
        public string ActivePirepId { get; private set; } = "";
        public DateTime FlightStartTime { get; private set; }

        public event Action<string> OnPhaseChanged;
        public event Action<FlightPhase> PhaseChanged;
        
        public event Action<string> OnLog;

        public FlightManager(ApiService apiService)
        {
            _apiService = apiService;
            CurrentPhase = FlightPhase.Idle;
        }

        public async Task<bool> StartFlight(SimbriefPlan plan, Pilot pilot)
        {
            try
            {
                // Validación de seguridad: ¿Tenemos API Service?
                if (_apiService == null)
                {
                    OnLog?.Invoke("ERROR: ApiService no está configurado.");
                    return false;
                }

                OnLog?.Invoke("Enviando Prefile a phpVMS...");

                // Aquí es donde ocurría la excepción
                ActivePirepId = await _apiService.PrefileFlight(plan, pilot);

                if (!string.IsNullOrEmpty(ActivePirepId))
                {
                    _touchdownCaptured = false;
                    TouchdownFpm = null;
                    CurrentPhase = FlightPhase.Boarding;
                    FlightStartTime = DateTime.Now;
                    OnLog?.Invoke($"Vuelo iniciado con ID: {ActivePirepId}");
                    return true;
                }
                else
                {
                    OnLog?.Invoke("ERROR: El servidor no devolvió un ID de PIREP.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // AQUÍ enviamos el mensaje "Pilot is not at departure airport" al Log del Formulario
                OnLog?.Invoke($"❌ ERROR API: {ex.Message}");
                return false;
            }
        }
        public void UpdatePhase(int altitude, int groundSpeed, bool isOnGround, int verticalSpeed)
        {
            var previousPhase = CurrentPhase;

            // ===== TOUCHDOWN DETECTION =====
            if (_wasOnGround == false && isOnGround == true)
            {
                RegisterTouchdown(verticalSpeed);
                CurrentPhase = FlightPhase.AfterLanding;
            }

            if (isOnGround)
            {
                if (CurrentPhase == FlightPhase.Boarding && groundSpeed > 3)
                    CurrentPhase = FlightPhase.TaxiOut;

                else if (CurrentPhase == FlightPhase.TaxiOut && groundSpeed > 40)
                    CurrentPhase = FlightPhase.Takeoff;

                else if (CurrentPhase == FlightPhase.AfterLanding && groundSpeed < 40)
                    CurrentPhase = FlightPhase.TaxiIn;

                else if (CurrentPhase == FlightPhase.TaxiIn && groundSpeed < 2)
                    CurrentPhase = FlightPhase.Completed;
            }
            else
            {
                if (CurrentPhase == FlightPhase.Takeoff && altitude > 1500)
                    CurrentPhase = FlightPhase.Climb;

                else if (CurrentPhase == FlightPhase.Climb && altitude > 10000)
                    CurrentPhase = FlightPhase.Enroute;

                else if (CurrentPhase == FlightPhase.Enroute && altitude < 8000)
                    CurrentPhase = FlightPhase.Descent;

                else if (CurrentPhase == FlightPhase.Descent && altitude < 3000)
                    CurrentPhase = FlightPhase.Approach;
            }

            _wasOnGround = isOnGround;

            if (previousPhase != CurrentPhase)
            {
                if (PhaseChanged != null)
                    PhaseChanged(CurrentPhase);
            }
        }
        private void RegisterTouchdown(int verticalSpeed)
        {
            if (_touchdownCaptured)
                return;

            TouchdownFpm = verticalSpeed;
            _touchdownCaptured = true;

            Console.WriteLine($"Touchdown registered: {verticalSpeed} FPM");
        }
        private void SetPhase(FlightPhase newPhase)
        {
            if (CurrentPhase == newPhase)
                return;

            var previous = CurrentPhase;

            CurrentPhase = newPhase;

            // Aquí puedes loggear
            Console.WriteLine($"Phase change: {previous} → {newPhase}");

            PhaseChanged?.Invoke(newPhase);
        }
        public void UpdateTelemetry(int altitude, int groundSpeed, int verticalSpeed, bool isOnGround, double fuel)
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;
            CurrentAltitude = altitude;
            CurrentGroundSpeed = groundSpeed;
            CurrentVerticalSpeed = verticalSpeed;
            CurrentFuel = fuel;
            IsOnGround = isOnGround;

            // Si no tienes IAS real, usamos GS como fallback
            CurrentIndicatedAirspeed = groundSpeed;

            UpdatePhase(altitude, groundSpeed, isOnGround, CurrentVerticalSpeed);
        }
    }
}