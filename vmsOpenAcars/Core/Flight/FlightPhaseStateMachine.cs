using System;
using System.Drawing;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    // Input snapshot passed to Update() on every telemetry cycle.
    internal struct PhaseInput
    {
        public int    Altitude, GroundSpeed, VerticalSpeed;
        public bool   IsOnGround;
        public double Pitch;
        public double Lat, Lon, Heading;

        // Plan data
        public int    CruiseAltitude;
        public double TotalDistanceNm, DestinationElevation;
        public double DistanceToDestinationNm; // -1 = unknown

        // Airports
        public string Origin, Destination;

        // Engine / hotel mode
        public bool AreEnginesOn, HotelModeActive;

        // Freno de parqueo puesto: es la señal de **fin del empuje** estando en `Pushback`
        // (`TelemetryCoordinator.OnParkingBrakeChanged` ya la usa para el popup de ruta).
        public bool ParkingBrakeSet;

        // Touch-and-go: seconds since last touchdown (double.MaxValue = no touchdown)
        public double SecondsSinceTouchdown;
    }

    // Pure phase-state machine extracted from FlightManager.
    // Owns all phase-transition timers.
    // All side-effects are delegated via events; no direct API or logging dependencies.
    internal sealed class FlightPhaseStateMachine
    {
        // ── Reloj ─────────────────────────────────────────────────────────────
        // Inyectable para que los tests puedan fijar la secuencia real del pushback
        // (01:57:12 → 01:59:34) sin esperar 90 s de reloj de pared. En vuelo es `DateTime.UtcNow`.
        private readonly Func<DateTime> _now;
        private DateTime Now() => _now();

        // ── Phase timer state ─────────────────────────────────────────────────
        private DateTime _phaseStartTime;
        private DateTime _lastTaxiPosEvent     = DateTime.MinValue;
        private DateTime _climbStableStart     = DateTime.MinValue;
        private DateTime _descentStart         = DateTime.MinValue;
        private DateTime _stepClimbStart       = DateTime.MinValue;
        private DateTime _descentToClimbStart  = DateTime.MinValue;
        private DateTime _goAroundStart        = DateTime.MinValue;
        private DateTime _pushbackStartTime    = DateTime.MinValue;
        private DateTime _stoppedStartTime     = DateTime.MinValue;
        private DateTime _takeoffRollStart     = DateTime.MinValue;
        /// <summary>
        /// Desde cuándo el avión parece estar volviendo a volar tras el toque
        /// (<see cref="TouchAndGoPolicy.Indicates"/>). El touch-and-go ya no se decide con un ciclo
        /// suelto: ver `TouchAndGoPolicy`.
        /// </summary>
        private DateTime _touchAndGoStart      = DateTime.MinValue;
        /// <summary>
        /// Desde cuándo el avión está en el suelo con una fase de vuelo. A los 15 s sostenidos
        /// (<see cref="StuckAirPhasePolicy.ShouldWarn"/>) la máquina vuelve al camino del suelo.
        /// </summary>
        private DateTime _stuckAirPhaseStart   = DateTime.MinValue;
        private double   _maxAltitudeReached;
        private bool     _wasOnGround          = true;
        private bool     _hasLandedThisFlight;
        // True once GS ≤ 0.5 kt is observed during Boarding — gates pushback/taxi detection
        // so that pre-existing movement at StartFlight does not trigger an immediate false positive.
        private bool     _boardingStationaryConfirmed;

        // ── Fin del pushback (v0.9.40) ────────────────────────────────────────
        // Se enciende cuando, **estando en `Pushback`**, se pone el freno de parqueo: es el fin
        // real del empuje, la señal que ya usa el popup de ruta (`OnParkingBrakeChanged`).
        private bool     _pushbackEndedByBrake;
        // Instante en que la GS pasó de 5 kt estando en `Pushback`, para exigir que la velocidad
        // se **sostenga** (no una sola muestra como la que disparó el falso `TaxiOut` de 01:58:56).
        private DateTime _pushbackAboveSpeedStart = DateTime.MinValue;

        // ── Constants ─────────────────────────────────────────────────────────
        private const double PushbackMaxSpeed       = 6.0;
        private const int    PushbackMinSec         = 8;
        private const double TaxiOutMinSpeed        = 5.0;
        private const int    TaxiOutMinSec          = 2;
        private const double SingleEngineMinRatio   = 0.5;
        private const int    TakeoffRollConfirmSec  = 5;
        /// <summary>Tope de seguridad en `Pushback`: pasado este tiempo sin freno, el rodaje se
        /// declara igual. Sin él, el piloto que nunca pone el freno se queda atrapado en `Pushback`
        /// para siempre — un fallo peor que el que se arregla aquí.
        /// Eran **90 s** (v0.9.40) y **se disparaban a mitad de *todos* los pushbacks reales
        /// medidos** —**142 s** en `XPNkY9jDneZ6ByGQ` (01:57:12→01:59:34) y **148 s** en
        /// `wk0896ajpxKalPrp` (03:19:42→03:22:10)—: a los 90 s el avión seguía **remolcado**, con
        /// los motores sin arrancar y el freno sin poner, y el remolque lo movía por encima de 5 kt,
        /// así que esta puerta abría el `TaxiOut` en pleno empuje (en `wk0896ajpxKalPrp` a las
        /// 03:21:12, y la gracia de 5 s no lo canceló porque el avión siguió moviéndose).
        /// **El timeout no puede ser la puerta habitual, solo el rescate** del piloto que nunca pone
        /// el freno: con **300 s** la puerta habitual pasa a ser el **freno de parqueo** —la señal
        /// semántica correcta, que llega a los ~145 s reales— y el rescate sigue existiendo (a los
        /// 112 s del caso de hoy quedaban 188 s de margen).</summary>
        internal const int    PushbackMaxSec            = 300;
        /// <summary>Segundos que la GS debe superar <see cref="TaxiOutMinSpeed"/> para declarar
        /// `TaxiOut`: el remolque rozó los 5 kt con una sola muestra (01:58:56).</summary>
        internal const int    PushbackToTaxiSustainSec  = 3;

        // ── Public state ──────────────────────────────────────────────────────
        public FlightPhase CurrentPhase { get; private set; } = FlightPhase.Idle;

        // ── Events ────────────────────────────────────────────────────────────
        // Fired on every phase change; (from, to).
        public event Action<FlightPhase, FlightPhase> OnTransitioned;

        // Fired once when wheels-off rotation is detected (gs, alt, vs).
        public event Action<int, int, int> OnTakeoffDetected;

        // Fired once at touchdown (verticalSpeed).
        public event Action<int> OnTouchdownDetected;

        // Fired every 2 s while taxiing (lat, lon, heading, airportIcao, isTaxiIn).
        public event Action<double, double, double, string, bool> OnTaxiPositionUpdate;

        // Fired when stopped + engines off/hotel mode (block on condition met).
        public event Action OnBlockOnDetected;

        // Fired when movement starts from Boarding (block off trigger).
        public event Action OnBlockOffNeeded;

        // Fired on go-around or touch-and-go — caller must reset approach gate data.
        public event Action OnApproachGateReset;

        // Fired specifically on touch-and-go (caller resets touchdown state).
        public event Action OnTouchAndGo;

        // Logging / OSD (subscribers format and display; no direct UI dependency here).
        public event Action<string, Color>       OnLog;
        public event Action<string, OsdSeverity> OnOsdMessage;

        // ── API ───────────────────────────────────────────────────────────────
        /// <param name="clock">Reloj inyectable; `null` = reloj del sistema (vuelo real).</param>
        internal FlightPhaseStateMachine(Func<DateTime> clock = null)
        {
            _now            = clock ?? (() => DateTime.UtcNow);
            _phaseStartTime = Now();
        }

        public void SetPhase(FlightPhase phase)
        {
            CurrentPhase   = phase;
            _phaseStartTime = Now();
        }

        /// <summary>
        /// **Cierra el vuelo a mano**: pone la fase en `OnBlock` y **publica la transición**, saltándose
        /// las condiciones de `HandleTaxiIn` (parado 90 s con los motores apagados o en hotel mode).
        ///
        /// Existe por el PIREP real **`RBmrBApLOjWlYmO2`**: su BE58 se quedó clasificado en `Climb`
        /// desde el falso touch-and-go de las 03:36:02 y **nunca volvió al suelo**, así que `OnBlock`
        /// —al que sólo se llega desde `TaxiIn`— fue inalcanzable, `SEND PIREP` (que `MainViewModel`
        /// habilita sólo en `OnBlock` o `Completed`) no apareció nunca y al piloto **sólo le quedaba
        /// cancelar, que borra el PIREP**. El vuelo se salvó porque un administrador lo cerró a mano
        /// desde phpVMS.
        ///
        /// **Es un override y se llama desde la UI, no desde la telemetría**: no sustituye a la
        /// detección —eso es el rescate de `StuckAirPhasePolicy`— sino que da al piloto una salida
        /// cuando la detección ya falló. Si la fase ya es `OnBlock`/`Completed` no hace nada y no
        /// publica nada: repetirlo no puede duplicar el cierre.
        /// </summary>
        public bool ForceOnBlock()
        {
            var prev = CurrentPhase;
            if (prev == FlightPhase.OnBlock || prev == FlightPhase.Completed) return false;

            CurrentPhase    = FlightPhase.OnBlock;
            _phaseStartTime = Now();
            OnTransitioned?.Invoke(prev, FlightPhase.OnBlock);
            return true;
        }

        public void Reset()
        {
            CurrentPhase          = FlightPhase.Idle;
            _phaseStartTime       = Now();
            _lastTaxiPosEvent     = DateTime.MinValue;
            _climbStableStart     = DateTime.MinValue;
            _descentStart         = DateTime.MinValue;
            _stepClimbStart       = DateTime.MinValue;
            _descentToClimbStart  = DateTime.MinValue;
            _goAroundStart        = DateTime.MinValue;
            _pushbackStartTime           = DateTime.MinValue;
            _stoppedStartTime            = DateTime.MinValue;
            _takeoffRollStart            = DateTime.MinValue;
            _touchAndGoStart             = DateTime.MinValue;
            _stuckAirPhaseStart          = DateTime.MinValue;
            _maxAltitudeReached          = 0;
            _wasOnGround                 = true;
            _hasLandedThisFlight         = false;
            _boardingStationaryConfirmed = false;
            _pushbackEndedByBrake        = false;
            _pushbackAboveSpeedStart     = DateTime.MinValue;
        }

        // ── Core update ───────────────────────────────────────────────────────
        public void Update(PhaseInput inp)
        {
            var prev = CurrentPhase;

            if (inp.Altitude > _maxAltitudeReached)
                _maxAltitudeReached = inp.Altitude;

            int    cruiseAlt    = inp.CruiseAltitude > 0 ? inp.CruiseAltitude : 10000;
            double totalDist    = inp.TotalDistanceNm > 0 ? inp.TotalDistanceNm : 100;
            double approachThr  = Math.Min(totalDist * 0.1, 20);
            double aglThr       = Math.Min(5000, cruiseAlt * 0.2);
            bool   isClimbing   = inp.VerticalSpeed > 100;
            bool   canChange    = (Now() - _phaseStartTime).TotalSeconds >= 5;

            // ── Rotation detection (while still on ground per _wasOnGround) ──
            if (_wasOnGround && inp.GroundSpeed > 60 && inp.Pitch > 2.0 && inp.Pitch < 20
                && !_hasLandedThisFlight)
            {
                if (CurrentPhase != FlightPhase.Takeoff && CurrentPhase != FlightPhase.TakeoffRoll)
                {
                    OnTakeoffDetected?.Invoke(inp.GroundSpeed, inp.Altitude, inp.VerticalSpeed);
                    TransitionTo(FlightPhase.Takeoff, prev);
                    prev = CurrentPhase;
                }
            }

            // ── Liftoff (wheels leave ground) ─────────────────────────────────
            // _wasOnGround is the primary airborne/on-ground tracker. It must be
            // refreshed unconditionally (except on the touchdown early-return below):
            // when it was only updated inside the Takeoff case, a second liftoff after
            // a touch-and-go or a stop-and-go left it stuck at true, so HandleAirPhases
            // never ran again and the phase machine froze on the ground.
            if (!inp.IsOnGround && _wasOnGround && CurrentPhase == FlightPhase.Takeoff)
                _wasOnGround = false;

            // ── Touchdown detection (air → ground) ───────────────────────────
            if (!_wasOnGround && inp.IsOnGround && CurrentPhase != FlightPhase.AfterLanding)
            {
                if (CurrentPhase == FlightPhase.Descent  ||
                    CurrentPhase == FlightPhase.Approach ||
                    CurrentPhase == FlightPhase.Landing)
                {
                    _hasLandedThisFlight = true;
                    // A new landing invalidates any pending takeoff-roll timer: timestamps
                    // are absolute and would otherwise survive into the next ground roll,
                    // firing TakeoffRoll instantly on the first sample above 30 kt.
                    _takeoffRollStart = DateTime.MinValue;
                    OnTouchdownDetected?.Invoke(inp.VerticalSpeed);
                    TransitionTo(FlightPhase.AfterLanding, prev);
                    _wasOnGround = inp.IsOnGround;
                    return;
                }
            }

            // ── ¿En el suelo? Lo dice la bandera **o** lo que hace el avión ─────────
            // La bandera del simulador no puede ser la única fuente: en el PIREP real
            // `RBmrBApLOjWlYmO2` publicó «en el aire» con el avión **rodando y aparcado** durante casi
            // tres minutos —`msl` clavado en 873-880 ft, GS 6-21 kt, freno de parqueo puesto—, y con el
            // reparto de tareas colgado de esa bandera el camino del suelo quedaba muerto: `OnBlock`
            // sólo se alcanza desde `TaxiIn`, así que el piloto se quedó sin poder enviar el PIREP.
            //
            // La evidencia no sustituye a la bandera, la **complementa**: por debajo de 40 kt y sin
            // tasa vertical no se vuela, diga lo que diga el simulador. `_wasOnGround` en cambio sigue
            // siendo la bandera **cruda**: es el detector de flancos del toque y del despegue, y
            // mezclarle la evidencia cambiaría cuándo se registra un aterrizaje.
            bool onGroundEvidence = inp.IsOnGround
                                 || StuckAirPhasePolicy.LooksOnGround(inp.GroundSpeed, inp.VerticalSpeed);

            if (onGroundEvidence)
                HandleGroundPhases(inp, prev);
            else
                HandleAirPhases(inp, prev, cruiseAlt, approachThr, aglThr, canChange, isClimbing);

            _wasOnGround = inp.IsOnGround;
        }

        // ── Ground phase logic ────────────────────────────────────────────────
        private void HandleGroundPhases(PhaseInput inp, FlightPhase prev)
        {
            switch (CurrentPhase)
            {
                // ── Fase de vuelo con el avión en el suelo: se vuelve al camino del suelo ─────
                // Es la **garantía** de que `OnBlock` siempre es alcanzable. `OnBlock` sólo se llega
                // desde `TaxiIn`, y `TaxiIn` sólo desde `AfterLanding`, así que una fase de vuelo con
                // el avión rodando es un callejón sin salida del que el piloto no puede salir: es
                // exactamente el estado en que quedó el PIREP real `RBmrBApLOjWlYmO2` —clasificado en
                // `Climb` desde el falso touch-and-go de las 03:36:02 hasta que se cerró la
                // aplicación— y por lo que se quedó **sin poder enviar el vuelo**.
                //
                // Espera los mismos 15 s sostenidos que el aviso (`StuckAirPhasePolicy.ShouldWarn`) y
                // por el mismo motivo: no reaccionar a un rebote. El cambio de fase se ve en el log por
                // el camino de siempre (`── AFTER LANDING ──`), así que el rescate no añade línea
                // propia; el aviso del `FlightManager` es el que explica el porqué.
                case FlightPhase.Takeoff:
                case FlightPhase.Climb:
                case FlightPhase.Enroute:
                case FlightPhase.Descent:
                    if (StuckAirPhasePolicy.IsStuck(CurrentPhase, inp.GroundSpeed, inp.VerticalSpeed))
                    {
                        if (_stuckAirPhaseStart == DateTime.MinValue)
                            _stuckAirPhaseStart = Now();
                        else if (StuckAirPhasePolicy.ShouldWarn((Now() - _stuckAirPhaseStart).TotalSeconds))
                        {
                            _stuckAirPhaseStart = DateTime.MinValue;
                            // Si el vuelo ya aterrizó, `AfterLanding` es la fase que le toca —y de ahí
                            // sale sola hacia `TaxiIn`—; si no llegó a aterrizar, el avión está en el
                            // suelo y su sitio es el rodaje.
                            TransitionTo(_hasLandedThisFlight ? FlightPhase.AfterLanding : FlightPhase.TaxiIn,
                                         prev);
                        }
                    }
                    else { _stuckAirPhaseStart = DateTime.MinValue; }
                    break;

                case FlightPhase.Boarding:
                    if (inp.GroundSpeed <= 0.5)
                    {
                        // Aircraft is stationary — confirm we've seen a stop, reset any pending timer.
                        _boardingStationaryConfirmed = true;
                        _pushbackStartTime = DateTime.MinValue;
                    }
                    else if (_boardingStationaryConfirmed)
                    {
                        // Movement detected after a confirmed stop — now safe to evaluate pushback/taxi.
                        if (_pushbackStartTime == DateTime.MinValue)
                            _pushbackStartTime = Now();

                        double secMoving = (Now() - _pushbackStartTime).TotalSeconds;

                        if (inp.GroundSpeed <= PushbackMaxSpeed && secMoving >= PushbackMinSec)
                        {
                            _pushbackStartTime = DateTime.MinValue;
                            // Entrada limpia en `Pushback`: el freno puesto de antes de empezar
                            // el empuje no puede contar como «fin del pushback».
                            _pushbackEndedByBrake    = false;
                            _pushbackAboveSpeedStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Pushback, prev);
                            OnBlockOffNeeded?.Invoke();
                        }
                        else if (inp.GroundSpeed > TaxiOutMinSpeed && secMoving >= TaxiOutMinSec)
                        {
                            _pushbackStartTime = DateTime.MinValue;
                            TransitionTo(FlightPhase.TaxiOut, prev);
                            OnBlockOffNeeded?.Invoke();
                        }
                    }
                    // else: aircraft was already moving at StartFlight — ignore until it stops.
                    break;

                case FlightPhase.Pushback:
                {
                    // ── Fin del pushback (v0.9.40) ────────────────────────────────
                    // PIREP real `XPNkY9jDneZ6ByGQ` (SKCG→SKBQ, 13/10/2026 UTC): a las 01:58:56 se
                    // declaró `TaxiOut` con el avión **aún remolcado** —empuje de 01:57:12 a
                    // 01:59:34, motores apagados, freno liberado hasta el final— porque el
                    // remolque rozó los 5 kt con **una sola muestra**. `CheckPhaseEntryLights`
                    // cobró entonces «TAXI APAGADAS» (−5) y el punto no se revirtió.
                    // Ahora `TaxiOut` exige **tres** cosas: que el empuje haya terminado (freno
                    // puesto, o el tope de `PushbackMaxSec` para el piloto que nunca lo pone) y
                    // que la GS supere 5 kt **sostenidos** `PushbackToTaxiSustainSec`. Con los
                    // datos reales, el rodaje se habría declarado a las **01:59:34**, cuando el
                    // avión ya estaba libre, y no a las 01:58:56.
                    if (inp.ParkingBrakeSet)
                        _pushbackEndedByBrake = true;

                    // Ventana de velocidad sostenida: se rearma sola al caer a 5 kt o menos.
                    if (inp.GroundSpeed > TaxiOutMinSpeed)
                    {
                        if (_pushbackAboveSpeedStart == DateTime.MinValue)
                            _pushbackAboveSpeedStart = Now();
                    }
                    else
                        _pushbackAboveSpeedStart = DateTime.MinValue;

                    bool pushbackEnded = _pushbackEndedByBrake
                                      || (Now() - _phaseStartTime).TotalSeconds >= PushbackMaxSec;
                    bool speedSustained = _pushbackAboveSpeedStart != DateTime.MinValue
                        && (Now() - _pushbackAboveSpeedStart).TotalSeconds >= PushbackToTaxiSustainSec;

                    if (pushbackEnded && speedSustained)
                    {
                        _pushbackAboveSpeedStart = DateTime.MinValue;
                        _pushbackEndedByBrake    = false;
                        TransitionTo(FlightPhase.TaxiOut, prev);
                    }
                    break;
                }

                case FlightPhase.TaxiOut:
                    EmitTaxiPosition(inp.Lat, inp.Lon, inp.Heading, inp.Origin, false);
                    if (inp.GroundSpeed > 30 && inp.Pitch < 1.0)
                    {
                        if (_takeoffRollStart == DateTime.MinValue) _takeoffRollStart = Now();
                        else if ((Now() - _takeoffRollStart).TotalSeconds >= TakeoffRollConfirmSec)
                        {
                            _takeoffRollStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.TakeoffRoll, prev);
                        }
                    }
                    else { _takeoffRollStart = DateTime.MinValue; }
                    break;

                case FlightPhase.TakeoffRoll:
                    if (inp.GroundSpeed > 50 && inp.Pitch > 2.0)
                    {
                        OnLog?.Invoke(_("Log_TakeoffRotation", inp.GroundSpeed, inp.Pitch.ToString("F1")), Theme.Success);
                        OnTakeoffDetected?.Invoke(inp.GroundSpeed, inp.Altitude, inp.VerticalSpeed);
                        TransitionTo(FlightPhase.Takeoff, prev);
                    }
                    else if (inp.GroundSpeed < 30)
                    {
                        TransitionTo(FlightPhase.TaxiOut, prev);
                    }
                    break;

                case FlightPhase.AfterLanding:
                    EmitTaxiPosition(inp.Lat, inp.Lon, inp.Heading, inp.Destination, true);
                    if (inp.GroundSpeed < 40)
                        TransitionTo(FlightPhase.TaxiIn, prev);
                    break;

                case FlightPhase.TaxiIn:
                    EmitTaxiPosition(inp.Lat, inp.Lon, inp.Heading, inp.Destination, true);
                    HandleTaxiIn(inp, prev);
                    break;
            }
        }

        private void HandleTaxiIn(PhaseInput inp, FlightPhase prev)
        {
            // Stop-and-go: the aircraft accelerates on the runway instead of parking.
            // Handled in HandleAirPhases when wheels leave the ground, but clearing the
            // timer here keeps the ground-roll state from going stale while taxiing.
            if (inp.GroundSpeed > 60)
                _takeoffRollStart = DateTime.MinValue;

            if (inp.GroundSpeed < 1)
            {
                if (_stoppedStartTime == DateTime.MinValue)
                    _stoppedStartTime = Now();

                if ((Now() - _stoppedStartTime).TotalSeconds >= 90 &&
                    (!inp.AreEnginesOn || inp.HotelModeActive))
                {
                    _stoppedStartTime = DateTime.MinValue;
                    OnBlockOnDetected?.Invoke();
                    TransitionTo(FlightPhase.OnBlock, prev);
                }
            }
            else
            {
                _stoppedStartTime = DateTime.MinValue;
            }
        }

        // ── Air phase logic ───────────────────────────────────────────────────
        private void HandleAirPhases(PhaseInput inp, FlightPhase prev,
            int cruiseAlt, double approachThr, double aglThr, bool canChange, bool isClimbing)
        {
            double altAboveDest = inp.Altitude - inp.DestinationElevation;

            switch (CurrentPhase)
            {
                case FlightPhase.Takeoff:
                case FlightPhase.TakeoffRoll:
                    if (!inp.IsOnGround && (isClimbing || inp.VerticalSpeed > 0))
                        TransitionTo(FlightPhase.Climb, prev);
                    break;

                case FlightPhase.Climb:
                {
                    double altDiff    = Math.Abs(inp.Altitude - cruiseAlt);
                    bool   nearCruise = altDiff < 500;
                    bool   lowVs      = Math.Abs(inp.VerticalSpeed) < 200;
                    bool   timeout    = (Now() - _phaseStartTime).TotalMinutes >= 5;
                    bool   veryLowVs  = Math.Abs(inp.VerticalSpeed) < 100;

                    if ((nearCruise && lowVs) || (timeout && veryLowVs))
                    {
                        if (_climbStableStart == DateTime.MinValue)
                            _climbStableStart = Now();
                        else if ((Now() - _climbStableStart).TotalSeconds >= 10)
                        {
                            _climbStableStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Enroute, prev);
                        }
                    }
                    else { _climbStableStart = DateTime.MinValue; }

                    if (inp.VerticalSpeed < -500 && inp.Altitude < _maxAltitudeReached - 500
                        && (Now() - _phaseStartTime).TotalSeconds >= 5)
                    {
                        if (_descentStart == DateTime.MinValue) _descentStart = Now();
                        else if ((Now() - _descentStart).TotalSeconds >= 20)
                        {
                            _descentStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Descent, prev);
                        }
                    }
                    else { _descentStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.Enroute:
                {
                    if (inp.VerticalSpeed > 500 && inp.Altitude < cruiseAlt - 500)
                    {
                        if (_stepClimbStart == DateTime.MinValue) _stepClimbStart = Now();
                        else if ((Now() - _stepClimbStart).TotalSeconds >= 10)
                        {
                            _stepClimbStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _stepClimbStart = DateTime.MinValue; }

                    if (inp.VerticalSpeed < -500 && inp.Altitude < _maxAltitudeReached - 500)
                    {
                        if (_descentStart == DateTime.MinValue) _descentStart = Now();
                        else if ((Now() - _descentStart).TotalSeconds >= 20)
                        {
                            _descentStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Descent, prev);
                        }
                    }
                    else { _descentStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.Descent:
                {
                    bool nearDest = (inp.DistanceToDestinationNm > 0
                                        && inp.DistanceToDestinationNm < approachThr)
                                 || (altAboveDest < aglThr && altAboveDest > 0);

                    if (nearDest)
                    {
                        if (canChange)
                        {
                            OnLog?.Invoke(_("Log_ApproachStarted"), Theme.Approach);
                            TransitionTo(FlightPhase.Approach, prev);
                        }
                        _descentToClimbStart = DateTime.MinValue;
                    }
                    else if (inp.VerticalSpeed > 500)
                    {
                        if (_descentToClimbStart == DateTime.MinValue)
                            _descentToClimbStart = Now();
                        else if ((Now() - _descentToClimbStart).TotalSeconds >= 20)
                        {
                            _descentToClimbStart = DateTime.MinValue;
                            OnLog?.Invoke(_("Log_ResumingClimb", inp.Altitude), Theme.MainText);
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _descentToClimbStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.Approach:
                {
                    bool goAround = inp.VerticalSpeed > 600
                                 && altAboveDest > 100 && altAboveDest < 3000
                                 && (Now() - _phaseStartTime).TotalSeconds >= 30;

                    if (goAround)
                    {
                        if (_goAroundStart == DateTime.MinValue)
                            _goAroundStart = Now();
                        else if ((Now() - _goAroundStart).TotalSeconds >= 8)
                        {
                            _goAroundStart = DateTime.MinValue;
                            OnLog?.Invoke(_("Log_GoAround", (int)altAboveDest, inp.VerticalSpeed), Theme.Warning);
                            OnOsdMessage?.Invoke("GO AROUND", OsdSeverity.Warning);
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _goAroundStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.AfterLanding:
                case FlightPhase.TaxiIn:
                    // Touch-and-go / stop-and-go: el avión volvió a volar después de aterrizar.
                    // `TaxiIn` se mira aquí también porque un aterrizaje lento deja que la transición
                    // `AfterLanding → TaxiIn` (GS<40) se dispare antes de que el piloto aplique
                    // potencia: sin esto la máquina se quedaría en `TaxiIn` todo el segundo vuelo y no
                    // llegaría nunca a `OnBlock`.
                    //
                    // **La decisión es de `TouchAndGoPolicy` y exige ascenso sostenido**, no un ciclo
                    // suelto: con `GS > 60 && SecondsSinceTouchdown >= 5` el BE58 del PIREP real
                    // `RBmrBApLOjWlYmO2` declaró un touch-and-go **inexistente** en plena carrera de
                    // aterrizaje (96 kt al tocar, 88 kt cinco segundos después, VS **−178 fpm**) y la
                    // fase se fue a `Climb` para no volver: `OnBlock` sólo se alcanza desde `TaxiIn`, y
                    // el piloto se quedó sin poder enviar el PIREP.
                    if (TouchAndGoPolicy.Indicates(CurrentPhase, inp.IsOnGround, inp.GroundSpeed,
                                                   inp.VerticalSpeed, inp.SecondsSinceTouchdown))
                    {
                        if (_touchAndGoStart == DateTime.MinValue)
                            _touchAndGoStart = Now();
                        else if (TouchAndGoPolicy.Confirmed((Now() - _touchAndGoStart).TotalSeconds))
                        {
                            _touchAndGoStart     = DateTime.MinValue;
                            _hasLandedThisFlight = false;
                            _takeoffRollStart    = DateTime.MinValue;
                            OnLog?.Invoke(_("Log_TouchAndGo", inp.GroundSpeed), Theme.Warning);
                            OnTouchAndGo?.Invoke();
                            OnApproachGateReset?.Invoke();
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _touchAndGoStart = DateTime.MinValue; }
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void TransitionTo(FlightPhase to, FlightPhase from)
        {
            if (from == FlightPhase.Approach && to == FlightPhase.Climb)
                OnApproachGateReset?.Invoke();
            CurrentPhase    = to;
            _phaseStartTime = Now();
            OnTransitioned?.Invoke(from, to);
        }

        private void EmitTaxiPosition(double lat, double lon, double heading, string airport, bool isTaxiIn)
        {
            if ((Now() - _lastTaxiPosEvent).TotalSeconds >= 2.0)
            {
                _lastTaxiPosEvent = Now();
                OnTaxiPositionUpdate?.Invoke(lat, lon, heading, airport, isTaxiIn);
            }
        }
    }
}
