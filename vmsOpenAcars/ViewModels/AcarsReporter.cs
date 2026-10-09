using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.ViewModels
{
    internal sealed class AcarsReporter
    {
        private readonly FlightManager           _flightManager;
        private readonly IApiService             _apiService;
        private readonly FsuipcService           _fsuipc;
        private readonly ILandingLogService      _landingLogService;
        private readonly SimbriefEnhancedService _simbriefEnhanced;
        private readonly TelemetryCoordinator    _tc;
        private readonly IMetarService           _metarService;
        private readonly AcarsReporterCallbacks  _cb;

        internal DateTime LastCheckpointSent { get; set; } = DateTime.MinValue;
        internal int      ProcSpdViolations  { get; set; } = 0;

        /// <summary>
        /// **La meteo del aterrizaje, capturada en el instante del contacto** (ver
        /// <see cref="LandingWeather"/>). Null mientras no haya habido touchdown o tras `Reset()`.
        /// Es lo que permite que el dato sea el del aterrizaje y no el de la hora de filear: el
        /// METAR del destino se refresca cada 5 min y entre la toma y el SEND PIREP pasan los minutos
        /// del rodaje. Sobrevive al reset del vuelo porque <see cref="SnapshotLandingRecord"/> —que
        /// es quien lo consume— corre **antes** de `await FilePirep()`.
        /// </summary>
        private LandingWeather _landingWx;

        internal AcarsReporter(
            FlightManager           flightManager,
            IApiService             apiService,
            FsuipcService           fsuipc,
            ILandingLogService      landingLogService,
            SimbriefEnhancedService simbriefEnhanced,
            TelemetryCoordinator    tc,
            IMetarService           metarService,
            AcarsReporterCallbacks  cb)
        {
            _flightManager     = flightManager;
            _apiService        = apiService;
            _fsuipc            = fsuipc;
            _landingLogService = landingLogService;
            _simbriefEnhanced  = simbriefEnhanced;
            _tc                = tc;
            _metarService      = metarService;
            _cb                = cb;
        }

        internal void Reset()
        {
            LastCheckpointSent = DateTime.MinValue;
            ProcSpdViolations  = 0;
            // La meteo del aterrizaje es de ESTE vuelo: un vuelo nuevo (o uno cancelado) no puede
            // heredar el METAR del anterior.
            _landingWx         = null;
        }

        internal bool ShouldSendCheckpoint(int intervalSeconds)
            => (DateTime.UtcNow - LastCheckpointSent).TotalSeconds >= intervalSeconds;

        // ── Plan summary log ──────────────────────────────────────────────────────

        internal void LogPlanSummary(SimbriefPlan p)
        {
            if (p == null) return;
            string origIata = string.IsNullOrEmpty(p.OriginIata)      ? "---" : p.OriginIata;
            string destIata = string.IsNullOrEmpty(p.DestinationIata) ? "---" : p.DestinationIata;
            string date     = p.ScheduledOffTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(p.ScheduledOffTime).UtcDateTime.ToString("ddMMMyyyy").ToUpper()
                : DateTimeOffset.UtcNow.ToString("ddMMMyyyy").ToUpper();
            string tripStr  = p.TripFuel > 0 ? $"  TRIP {p.TripFuel:F0}" : "";
            _cb.Log?.Invoke(
                $"📋 {p.Airline}{p.FlightNumber}  {p.Origin}/{origIata} → {p.Destination}/{destIata}" +
                $"  {p.AircraftIcao} {p.Registration}  {date}", Theme.Success);
            _cb.Log?.Invoke(
                $"   PAX {p.PaxCount}  FUEL {p.BlockFuel:F0}{tripStr}  CARGO {p.CargoWeight:F0}  FL{p.CruiseAltitude / 100}",
                Theme.MainText);
        }

        // ── Landing / Block position reports ─────────────────────────────────────

        internal void HandleLandingDetected(int verticalSpeed, double gforce, double pitch, double bank)
        {
            // **El momento del aterrizaje.** Es el único punto del vuelo en el que el METAR vigente
            // del destino y el viento del simulador son los de la toma; se capturan aquí y no al
            // filear. Va lo primero de todo: lo que sigue ya manda la posición a phpVMS.
            CaptureLandingWeather();

            var rec = new AcarsPosition
            {
                type         = 0,  status   = "LDG", nav_type = 0, name = "TOUCHDOWN",
                lat          = _flightManager.CurrentLat,
                lon          = _flightManager.CurrentLon,
                altitude     = _flightManager.CurrentAltitude,
                altitude_agl = 0,
                heading      = (int)_fsuipc.CurrentHeading,
                vs           = verticalSpeed,
                gs           = _flightManager.CurrentGroundSpeed,
                ias          = _flightManager.CurrentIndicatedAirspeed,
                gforce       = gforce,  pitch = pitch,  bank = bank,
                sim_time     = DateTime.UtcNow,  source = "vmsOpenAcars"
            };
            Task.Run(async () =>
            {
                var upd = new AcarsPositionUpdate { positions = new[] { rec } };
                await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, upd);
                // La línea del aterrizaje gana el tiempo umbral→toma, los flaps y el corte de potencia,
                // leídos del buffer del flare **en memoria**: en vuelo no hay que ir a la base.
                _cb.Log?.Invoke(_("Log_LandingRecorded", verticalSpeed, $"{gforce:F2}",
                    (int)_fsuipc.CurrentHeading, $"{pitch:F1}", $"{bank:F1}")
                    + LandingTrackNote(), Theme.Success);
            });

            int abs = Math.Abs(verticalSpeed);
            OsdSeverity sev   = abs <= 300 ? OsdSeverity.Success : abs <= 600 ? OsdSeverity.Warning : OsdSeverity.Critical;
            string      label = abs <= 300 ? "TOUCHDOWN"         : abs <= 600 ? "FIRM LANDING"      : "HARD LANDING";
            _cb.OsdMessage?.Invoke($"{label}  {verticalSpeed} FPM  {gforce:F2} G", sev);
        }

        /// <summary>
        /// **Lo que la traza fina del flare dice de este aterrizaje**, para la línea del log: el tiempo
        /// umbral→toma, los flaps con los que se tomó y dónde se cortó la potencia. Los tres salen de
        /// los helpers puros que los publican en el resto de la aplicación, y los tres **degradan
        /// callándose**: sin dato no se añade nada, nunca un cero.
        ///
        /// **La espera acotada tiene causa raíz.** El ciclo de telemetría que detecta el toque corre
        /// **antes** de que ese mismo ciclo guarde su muestra del flare —el estado de fase se
        /// actualiza en `FlightManager.UpdateTelemetry`, y la captura del flare va después, en
        /// `ProcessRawData`—, así que en la primera lectura la traza acaba en la última muestra **en
        /// el aire** y los helpers, que exigen la transición a tierra, no devuelven número. La muestra
        /// en tierra llega en el ciclo siguiente (captura a 10 Hz sobre un sondeo de 50 ms), así que se
        /// espera como mucho **1 s** a que aparezca. Es espera dentro del `Task.Run` que ya está
        /// mandando la posición del toque —no en el hilo de telemetría ni en el de UI— y, si no
        /// llega, se degrada: la línea del aterrizaje sale sin el dato, nunca con un cero.
        ///
        /// Los tres se calculan sobre **el mismo snapshot**: el de la última lectura, ya con la
        /// muestra en tierra, para que los números que se enseñan juntos sean del mismo instante.
        /// </summary>
        private string LandingTrackNote()
        {
            if (_tc == null) return "";

            var samples  = _tc.SnapshotFlareBuffer();
            var timeline = ThresholdToTouchdown.Compute(samples);
            var deadline = DateTime.UtcNow.AddSeconds(1.0);
            while (timeline.Status == ThresholdToTouchdownStatus.NoGroundTransition
                   && _tc.FlareBufferCount > 0
                   && DateTime.UtcNow < deadline)
            {
                System.Threading.Thread.Sleep(50);
                samples  = _tc.SnapshotFlareBuffer();
                timeline = ThresholdToTouchdown.Compute(samples);
            }

            string family = _fsuipc != null ? _fsuipc.AircraftIcao : null;
            var flaps = FlapTrackSummary.Compute(samples, family);
            var power = PowerCut.Compute(samples);

            var sb = new System.Text.StringBuilder();
            if (timeline.HasValue)
                sb.Append("  ·  ").Append(_("Landing_ThrToTd", timeline.Seconds));

            // Los flaps del aterrizaje: el ajuste en el umbral es el que el piloto recuerda, y el
            // aviso de cambio es lo que explica un flotado largo. Con la familia desconocida el helper
            // devuelve el porcentaje, nunca una compuerta inventada.
            if (flaps.HasTrack && flaps.AtThreshold != null)
            {
                sb.Append("  ·  ").Append(L._("Landing_FlapsHeader")).Append(" ").Append(flaps.AtThreshold.Text);
                if (flaps.AtTouchdown != null && flaps.Changed)
                    sb.Append("  ").Append(L._("Landing_FlapsChanged",
                                               flaps.AtThreshold.ShortText, flaps.AtTouchdown.ShortText));
            }

            if (power.HasValue)
                sb.Append("  ·  ").Append(L._(power.EngineCount == 2 ? "Landing_PowerCut"
                                                                    : "Landing_PowerCutEngine1",
                                              power.SecondsBeforeTouchdown));

            return sb.ToString();
        }

        /// <summary>
        /// **Los flaps del aterrizaje tal como viajan al `notes` del PIREP**: el ajuste que el piloto
        /// tenía puesto **al cruzar el umbral**, que es el que responde a «¿con cuánto flap tomé?».
        ///
        /// **El cambio durante el flare no entra aquí**, y es una decisión de espacio, no un olvido: la
        /// línea del `notes` ya lleva el METAR entero, el viento con sus componentes, el `THR-TD`, este
        /// ajuste y el corte de potencia, y un segundo ajuste la alarga y la vuelve difícil de leer de
        /// un vistazo. El cambio **sí** se publica donde hay sitio para explicarlo: la línea del
        /// aterrizaje del log y el resumen de la ventana FLARE (`Landing_FlapsChanged`).
        ///
        /// `null` sin dato, para que el llamante no concatene nada.
        /// </summary>
        private static string FlapPirepSuffix(FlapTrackSummary flaps)
        {
            if (flaps == null || !flaps.HasTrack || flaps.AtThreshold == null) return null;
            return flaps.AtThreshold.PirepText;
        }

        internal void HandleBlockDetected()
        {
            _cb.Log?.Invoke(_("Log_OnBlockDetected"), Theme.Success);
            var rec = new AcarsPosition
            {
                type     = 0,  status = "ARR",  name = "ON BLOCK",
                lat      = _flightManager.CurrentLat,
                lon      = _flightManager.CurrentLon,
                altitude = _flightManager.CurrentAltitude,
                heading  = (int)_fsuipc.CurrentHeading,
                sim_time = DateTime.UtcNow,  source = "vmsOpenAcars"
            };
            Task.Run(async () =>
            {
                if (!string.IsNullOrEmpty(_flightManager.ActivePirepId))
                    await _apiService.SendPositionUpdate(
                        _flightManager.ActivePirepId,
                        new AcarsPositionUpdate { positions = new[] { rec } });
            });
        }

        // ── SendPirep ─────────────────────────────────────────────────────────────

        internal async Task SendPirep()
        {
            _flightManager.SetProcedureSpdViolations(ProcSpdViolations);
            // El snapshot va **antes** de `await FilePirep()` por el orden crítico documentado: el
            // fileo llama a `ResetFlightState()`, que borra el plan activo y los datos de touchdown.
            var pendingRecord = SnapshotLandingRecord();

            // Las dos trazas se copian aquí, pegadas al registro y por el mismo motivo: en cuanto el
            // PIREP se da por enviado, `ResetTelemetry` corre `TelemetryCoordinator.Reset()` y los
            // buffers en vivo dejan de existir. Copiarlas aquí hace que la persistencia **no dependa
            // del orden de las sentencias** de este método.
            //
            // Es la causa raíz de que `flare_track` quedara vacía: el guardado corría después del
            // reset, el buffer ya estaba a cero y la condición «hay muestras» no se cumplía nunca (0
            // filas en toda la base local con capturas de ~200 muestras terminadas). El buffer de
            // aproximación se libraba porque `Reset()` no lo vacía, y eso es lo que hacía que el
            // fallo pasara desapercibido: el vuelo se guardaba, solo perdía la traza fina.
            //
            // El «¿se armó la captura?» también se lee ahora: después del reset el estado en vivo ya
            // no lo sabe, y es lo que la ventana del flare necesita para no contar una captura
            // perdida como un vuelo anterior a la traza.
            var pendingApproach = _tc?.SnapshotApproachBuffer() ?? new List<ApproachTrackPoint>();
            var pendingFlare    = _tc?.SnapshotFlareBuffer()    ?? new List<FlareTrackPoint>();
            bool flareArmed     = _tc != null && _tc.FlareCaptureStarted;

            // La MISMA meteo que se acaba de volcar al logbook viaja al PIREP, compuesta una sola
            // vez desde el snapshot: dos lecturas distintas podrían dar dos textos distintos.
            string landingWx = LandingWeatherLine.Build(
                pendingRecord.MetarRaw,
                pendingRecord.LandingMetarObsUtc,
                pendingRecord.WindAtLanding);

            // El tiempo umbral→toma va **al final** de la línea de meteo, que es el único camino que
            // hoy llega al `notes` del PIREP. Solo con la **traza fina**: con la de 2 s el helper
            // devuelve `CoarseTrack` y no se publica —±1 s disfrazado de décimas es peor que no
            // tener el dato—, y sin línea de meteo no hay dónde colgarlo.
            string thrToTd = ThresholdToTouchdown.PirepSuffix(
                ThresholdToTouchdown.Compute(pendingFlare));
            if (!string.IsNullOrEmpty(thrToTd) && !string.IsNullOrEmpty(landingWx))
                landingWx += " | " + thrToTd;

            // Los flaps y el corte de potencia van **detrás del tiempo** y con la línea de meteo ya
            // compuesta. La línea resultante queda en unos 140 caracteres en el peor caso (METAR
            // completo + viento + componentes + los tres datos), que un `notes` de texto libre aguanta
            // sin volverse ilegible; por eso entran los dos y no solo uno.
            //
            // **Sin dato no se añade nada** —ni un cero ni un guion—: cada sufijo es `null` cuando su
            // helper no tiene número, y la línea se queda como estaba. Y los dos son **cortos y
            // autodescriptivos** (`~FLAPS 30`, `PWR-CUT 4.1s`) porque comparten renglón con el METAR.
            //
            // La familia para etiquetar los flaps sale del **snapshot**: el `AirportIcao` que se acaba
            // de guardar en el registro es el del avión que voló, no el tipo del OFP —que puede no
            // coincidir—.
            string flapsPirep = FlapPirepSuffix(FlapTrackSummary.Compute(pendingFlare, pendingRecord.AircraftIcao));
            string powerPirep = PowerCut.PirepSuffix(PowerCut.Compute(pendingFlare));
            if (!string.IsNullOrEmpty(landingWx))
            {
                if (!string.IsNullOrEmpty(flapsPirep)) landingWx += " | " + flapsPirep;
                if (!string.IsNullOrEmpty(powerPirep)) landingWx += " | " + powerPirep;
            }

            // Camino de campo personalizado, **apagado por defecto**: un campo de `pirep_fields` lo
            // tiene que crear phpVMS antes (ver AppConfig.PirepLandingWeatherFieldEnabled). El
            // `notes` de abajo es el camino que sí funciona hoy.
            if (AppConfig.PirepLandingWeatherFieldEnabled && !string.IsNullOrEmpty(landingWx))
                _flightManager.SendLandingWeatherField(landingWx);

            bool filed = false;
            try
            {
                filed = await _flightManager.FilePirep(landingWx);
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke($"⚠️ Error al enviar PIREP: {ex.Message}", Theme.Danger);
                return;
            }

            if (filed)
            {
                int pirepScore = _flightManager.LastFlightScore;
                OsdSeverity scoreSev = pirepScore >= 80 ? OsdSeverity.Success
                                     : pirepScore >= 60 ? OsdSeverity.Info
                                     : OsdSeverity.Warning;
                _cb.OsdMessage?.Invoke($"PIREP FILED   SCORE {pirepScore} / 100", scoreSev);
                _cb.ButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                _cb.ResetTelemetry?.Invoke();
                LastCheckpointSent = DateTime.MinValue;
                _cb.Log?.Invoke("✅ Vuelo reportado, listo para siguiente vuelo", Theme.Success);
                _cb.FlightEnded?.Invoke();
                SaveLandingRecord(pendingRecord, pendingApproach, pendingFlare, flareArmed);
                // Releer los datos del piloto implica una llamada HTTP y 5 s de espera; no
                // debe bloquear el cierre del vuelo, pero un fallo debe quedar registrado.
                FireAndForget.Run(RefreshPilotDataAfterPirep,
                    ex => _cb.Log?.Invoke(_("Log_BaseUpdateError", ex.Message), Theme.Warning),
                    "refresh de datos del piloto");
            }
            else
            {
                _cb.Log?.Invoke("⚠️ No se pudo enviar el PIREP. Verifique la conexión e intente nuevamente.", Theme.Danger);
            }
        }

        // ── Snapshot / Save ───────────────────────────────────────────────────────

        /// <summary>
        /// **Captura la meteo en el instante del touchdown.**
        ///
        /// - **METAR del destino**: se toma del servicio, que ya lo tiene descargado en memoria
        ///   (`MetarService.CurrentMetars`, refresco cada 5 min). No se pide uno nuevo: esto corre en
        ///   el hilo de telemetría y lo que interesa es el METAR que el piloto **tenía delante** al
        ///   tomar tierra, no el que devuelva una petición un segundo después.
        /// - **Viento del simulador**: FSUIPC ya lo lee en cada sondeo (0x0E92 dirección, 0x0E90
        ///   intensidad) y aquí se copia tal cual, que es el viento que el avión estaba volando.
        /// - **Racha**: del METAR del destino. FSUIPC no publica offset de racha; inventarlo sería
        ///   leer un valor que no es.
        ///
        /// Degrada sin datos: sin METAR (o sin red) se guarda solo el viento, y al revés.
        /// </summary>
        private void CaptureLandingWeather()
        {
            var metar = GetArrivalMetar();
            string raw = string.IsNullOrWhiteSpace(metar?.Raw) ? null : metar.Raw.Trim();

            bool connected = _fsuipc != null && _fsuipc.IsConnected;

            _landingWx = new LandingWeather
            {
                MetarRaw      = raw,
                ObservedAtUtc = MetarObservationTime.Parse(raw, DateTime.UtcNow),
                // Sin simulador conectado no hay viento: null, no 0/0 (que sería «calma»).
                WindDirDeg    = connected ? (double?)_fsuipc.CurrentWindDirDeg   : null,
                WindSpeedKt   = connected ? (double?)_fsuipc.CurrentWindSpeedKt  : null,
                WindGustKt    = metar?.WindGustKt,
            };
        }

        private FlightRecord SnapshotLandingRecord()
        {
            var fm   = _flightManager;
            var plan = fm.ActivePlan;

            // El METAR del aterrizaje manda; si la captura no llegó a haberla (vuelo reanudado, o
            // el touchdown no pasó por aquí) se cae a lo que el servicio tenga en ese momento, que es
            // lo que se guardaba antes. Degradar sin datos, no perder el dato.
            string metarRaw = _landingWx?.MetarRaw ?? GetDestinationMetarRaw();
            DateTime? metarObs = _landingWx != null
                ? _landingWx.ObservedAtUtc
                : MetarObservationTime.Parse(metarRaw, DateTime.UtcNow);

            // Componentes del viento contra el eje VERDADERO de la pista de la toma. Sin viento o
            // sin pista resuelta el helper devuelve `Available = false` y se guarda NULL en las
            // componentes (el viento en crudo sí se conserva).
            var wind = WindComponents.Compute(_landingWx?.WindDirDeg, _landingWx?.WindSpeedKt,
                                              _landingWx?.WindGustKt,
                                              fm.TouchdownRunwayTrueHeadingDeg);

            return new FlightRecord
            {
                FlightNumber    = plan?.FlightNumber     ?? "",
                Origin          = plan?.Origin           ?? "",
                Destination     = plan?.Destination      ?? "",
                RunwayName      = fm.TouchdownRunwayName ?? "",
                FlightDate      = DateTime.UtcNow,
                // NoLandingData (-1) when no touchdown was captured — never 0, which would
                // read as a real 0 fpm "Butter" landing in the logbook.
                LandingRateFpm  = fm.TouchdownDataCaptured ? (fm.TouchdownFpm ?? 0)
                                                           : ScoringService.NoLandingData,
                GForce          = fm.TouchdownGForce,
                TouchdownDistFt = fm.TouchdownDistanceFt,
                CenterlineDevFt = fm.TouchdownCenterlineFt,
                // METAR de llegada vigente en el momento del aterrizaje (slot 1 = DEST). El snapshot
                // es síncrono y refleja lo que el piloto tenía delante. Si el servicio no pudo
                // obtenerlo, queda vacío — el LOGBOOK lo muestra como sin dato.
                MetarRaw        = metarRaw,

                LandingMetarObsUtc   = metarObs,
                LandingWindDirDeg    = wind.WindDirDeg,
                LandingWindSpeedKt   = wind.WindSpeedKt,
                LandingWindGustKt    = wind.GustKt,
                LandingRunwayTrueDeg = wind.RunwayHeadingTrueDeg,
                LandingHeadwindKt    = wind.Available ? (double?)wind.HeadwindKt  : null,
                LandingCrosswindKt   = wind.Available ? (double?)wind.CrosswindKt : null,

                // Longitud de la pista de la toma, para el closeup del perfil vertical. `0` es «sin
                // dato» en `TouchdownState` y se guarda como NULL: el closeup prefiere no dibujar la
                // pista antes que dibujarla de un largo inventado.
                RunwayLengthFt       = fm.TouchdownRunwayLengthFt > 0.0
                                           ? (double?)fm.TouchdownRunwayLengthFt : null,

                // La familia del avión, para poder etiquetar los flaps al releer el vuelo. Se guarda
                // el **modelo ATC** del simulador (`B737`, `A320`…), que es el que dice la familia;
                // `????` es «no lo publica» y va como null, no como un texto que parezca un avión.
                AircraftIcao         = _fsuipc != null && !string.IsNullOrEmpty(_fsuipc.AircraftIcao)
                                       && _fsuipc.AircraftIcao != "????"
                                           ? _fsuipc.AircraftIcao.Trim() : null,
            };
        }

        /// <summary>
        /// METAR del aeropuerto de llegada, o null si no se descargó.
        /// Si el aterrizaje ocurrió en un aeropuerto distinto al planeado (desvío
        /// confirmado), se prefiere el METAR del destino real cuando el servicio lo tiene:
        /// el slot DEST corresponde al plan, y en un desvío ese ya no es el aeropuerto
        /// donde se aterrizó.
        /// </summary>
        private string GetDestinationMetarRaw() => GetArrivalMetar()?.Raw;

        /// <summary>
        /// El METAR del aeropuerto de llegada **entero** (no solo el texto): además del `Raw` hace
        /// falta la racha, que es la única fuente de racha que tenemos (FSUIPC no la publica).
        /// </summary>
        private MetarData GetArrivalMetar()
        {
            var metars = _metarService?.CurrentMetars;
            if (metars == null) return null;

            string arrival = _flightManager.EffectiveDestination
                          ?? _flightManager.ActivePlan?.Destination;

            // Buscar primero el slot que corresponde al aeropuerto de llegada real.
            if (!string.IsNullOrEmpty(arrival))
            {
                foreach (var m in metars)
                {
                    if (m != null &&
                        string.Equals(m.RequestedIcao, arrival, StringComparison.OrdinalIgnoreCase))
                        return m;
                }
            }

            return metars.Length > 1 ? metars[1] : null;
        }

        /// <summary>
        /// Persiste el vuelo y sus dos trazas. **Las trazas llegan como argumentos, ya copiadas**
        /// (`SendPirep` las copia antes de `await FilePirep()`): leerlas aquí en vivo ataba el guardado
        /// a que nadie hubiera vaciado los buffers por el camino, y el reset del vuelo —que corre en
        /// este mismo método, después— vacía el del flare. Con la copia en la mano, el orden de las
        /// sentencias deja de importar.
        /// </summary>
        private void SaveLandingRecord(FlightRecord record,
                                       IList<ApproachTrackPoint> approachTrack,
                                       IList<FlareTrackPoint>    flareTrack,
                                       bool                      flareCaptureArmed)
        {
            int  bufCount = approachTrack?.Count ?? 0;
            bool svcOk    = _landingLogService?.IsAvailable ?? false;

            if (!svcOk)
            {
                _cb.Log?.Invoke(_("Log_LandingLogNoService"), Theme.Warning);
                return;
            }
            if (bufCount < 3)
            {
                _cb.Log?.Invoke(_("Log_LandingLogTooFew", bufCount), Theme.Warning);
                return;
            }
            try
            {
                record.Score = _flightManager.LastFlightScore;
                // «La captura del flare se armó» viaja con el vuelo: con cero filas en `flare_track`
                // es lo único que separa «esta captura se perdió» de «este vuelo es anterior a la
                // traza del flare», y el estado en vivo ya no existe a estas alturas.
                record.FlareCaptureArmed = flareCaptureArmed;
                int newId = _landingLogService.SaveFlight(record, approachTrack);
                if (newId > 0)
                {
                    _cb.Log?.Invoke(_("Log_LandingLogSaved", newId, bufCount, record.RunwayName), Theme.Success);

                    // La traza fina del flare, en su propia tabla y **solo si existe**. Va aquí y no
                    // dentro de `SaveFlight` porque el `flight_id` solo se conoce tras el INSERT del
                    // vuelo; y si falla no arrastra al vuelo, que ya está guardado. Un vuelo sin
                    // captura de flare no estrena filas y el gráfico dirá que no hay datos — que es
                    // la verdad, no un hueco que rellenar con la traza de 2 s.
                    if (flareTrack != null && flareTrack.Count > 0)
                    {
                        int flareCount = _landingLogService.SaveFlareTrack(newId, flareTrack);
                        if (flareCount > 0)
                            _cb.Log?.Invoke(_("Log_FlareTrackSaved", flareCount), Theme.Success);
                    }

                    // Solo descartar la trayectoria si realmente se persistió: si `SaveFlight` falla, el
                    // buffer en vivo sigue siendo la única copia que queda en el proceso.
                    _tc?.ClearApproachBuffer();
                    _tc?.ClearFlareBuffer();
                }
                else
                {
                    _cb.Log?.Invoke(_("Log_LandingLogBadId", newId), Theme.Danger);
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_LandingLogError", ex.Message), Theme.Danger);
            }
        }

        // ── RefreshPilotData ──────────────────────────────────────────────────────

        private async Task RefreshPilotDataAfterPirep()
        {
            await Task.Delay(5000);
            try
            {
                var result = await _apiService.GetPilotData();
                if (result.Data != null)
                {
                    _flightManager.SetActivePilot(result.Data);
                    _cb.Log?.Invoke(_("Log_BaseUpdated", result.Data.CurrentAirport), Theme.Success);
                    _cb.AirportChanged?.Invoke(result.Data.CurrentAirport);
                    if (_fsuipc.IsConnected)
                    {
                        _flightManager.UpdatePositionValidation(
                            _fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                        _cb.ValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_BaseUpdateError", ex.Message), Theme.Warning);
            }
        }

        // ── CheckAndCleanActivePireps ─────────────────────────────────────────────

        internal async Task<bool> CheckAndCleanActivePireps()
        {
            try
            {
                var activePireps = await _apiService.GetActivePireps();
                if (!activePireps.Any()) return true;

                var pirepInfo = string.Join("\n", activePireps.Select(p =>
                    $"✈️ {p.FlightNumber} | {p.Origin} → {p.Destination} | {p.StateDescription}"));
                var message = $"⚠️ ACTIVE FLIGHT(S) DETECTED ⚠️\n\n" +
                              $"You have {activePireps.Count} active flight(s) in the system:\n" +
                              $"{pirepInfo}\n" +
                              $"• DELETE the active flight(s) and continue\n" +
                              $"• or close this dialog and do nothing";

                if (_cb.ShowConfirmation != null)
                {
                    var result = await _cb.ShowConfirmation(message, "ACTIVE FLIGHTS", EcamDialogButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        bool allDeleted = true;
                        foreach (var pirep in activePireps)
                        {
                            bool deleted = await _apiService.DeletePirepById(pirep.Id);
                            if (!deleted)
                            {
                                _cb.Log?.Invoke(_("Log_ActiveFlightDeleteFail", pirep.FlightNumber), Theme.Danger);
                                allDeleted = false;
                            }
                            else
                            {
                                _cb.Log?.Invoke(_("Log_OrphanedFlightDeleted", pirep.FlightNumber), Theme.Success);
                            }
                        }
                        _cb.Log?.Invoke(
                            allDeleted ? _("Log_OrphansCleared") : _("Log_OrphansPartial"),
                            allDeleted ? Theme.Success : Theme.Warning);
                        return allDeleted;
                    }
                    else
                    {
                        _cb.Log?.Invoke(_("Log_PlannerCancelled"), Theme.MainText);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_ActiveFlightsError", ex.Message), Theme.Danger);
                return true;
            }
        }

        // ── Scoring checkpoint ────────────────────────────────────────────────────

        private string BuildCheckpointLog()
        {
            var fm = _flightManager;
            return $"SC:ov={fm.OverspeedCount}" +
                   $",lt={fm.LightsViolationCount}" +
                   $",sa={fm.StabilizedApproachDeductions}" +
                   $",qnh={fm.QnhViolationCount}" +
                   $",it={(fm.IsOfflineFlight ? 1 : 0)}" +
                   $",od={(fm.DepartedLate ? 1 : 0)}" +
                   $",spd={ProcSpdViolations}" +
                   $",lz={fm.LocalizerViolations}" +
                   $",bm={(fm.BelowMinimums ? 1 : 0)}" +
                   $",ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        }

        internal async Task SendScoringCheckpointAsync()
        {
            string pirepId = _flightManager?.ActivePirepId;
            if (string.IsNullOrEmpty(pirepId)) return;

            LastCheckpointSent = DateTime.UtcNow;

            double lat = _flightManager.CurrentLat;
            double lon = _flightManager.CurrentLon;
            int    hdg = (int)_fsuipc.CurrentHeading;

            var chk = new AcarsPositionUpdate
            {
                positions = new[]
                {
                    new AcarsPosition
                    {
                        lat     = lat,
                        lon     = lon,
                        heading = hdg,
                        status  = "CHK",
                        log     = BuildCheckpointLog(),
                        source  = "vmsOpenAcars"
                    }
                }
            };
            await _apiService.SendPositionUpdate(pirepId, chk);
        }

        // ── Resume helpers ────────────────────────────────────────────────────────

        internal async Task ResumeFromAcarsHistoryAsync(string pirepId)
        {
            var acars = await _apiService.GetPirepAcarsAsync(pirepId);
            if (acars == null || acars.Count == 0) return;

            var nonChk = acars
                .Where(a => a.status != "CHK" && !string.IsNullOrEmpty(a.log))
                .ToList();
            foreach (var entry in nonChk.Skip(Math.Max(0, nonChk.Count - 20)))
                _cb.Log?.Invoke($"  [{entry.status ?? "SCH"}]  {entry.log}", Theme.MainText);

            var lastChk = acars.LastOrDefault(a => a.status == "CHK" && !string.IsNullOrEmpty(a.log));
            if (lastChk != null)
                TryRestoreScoringCheckpoint(lastChk.log);
            else
                _cb.OsdMessage?.Invoke("RESUME  NO CHECKPOINT FOUND", OsdSeverity.Warning);
        }

        private void TryRestoreScoringCheckpoint(string log)
        {
            if (string.IsNullOrEmpty(log) || !log.StartsWith("SC:")) return;
            try
            {
                var fields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in log.Substring(3).Split(','))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && int.TryParse(kv[1], out int val))
                        fields[kv[0]] = val;
                }
                int  Get(string k)  => fields.TryGetValue(k, out var v) ? v : 0;
                bool GetB(string k) => Get(k) != 0;

                _flightManager.SetResumedPenalties(
                    overspeed:  Get("ov"),
                    lights:     Get("lt"),
                    stabilized: Get("sa"),
                    qnh:        Get("qnh"),
                    offline:    GetB("it"),
                    late:       GetB("od"),
                    procSpd:    0,
                    localizer:  Get("lz"),
                    belowMins:  GetB("bm"));
                ProcSpdViolations = Get("spd");

                long ts = 0;
                if (fields.TryGetValue("ts", out var tsInt)) ts = tsInt;
                var checkpointTime = ts > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
                    : DateTime.UtcNow;

                _cb.Log?.Invoke(
                    $"  [CHK]  Penalties restored — ov={Get("ov")} lt={Get("lt")} " +
                    $"sa={Get("sa")} qnh={Get("qnh")} spd={Get("spd")} " +
                    $"lz={Get("lz")} (checkpoint {(int)(DateTime.UtcNow - checkpointTime).TotalMinutes} min ago)",
                    Theme.Success);
                _cb.OsdMessage?.Invoke("RESUME  PENALTIES RESTORED", OsdSeverity.Success);
            }
            catch
            {
                _cb.Log?.Invoke("  [CHK]  Could not parse scoring checkpoint", Theme.Warning);
            }
        }

        private async Task ResumeFromSimbriefAsync(Models.Pirep pirep)
        {
            try
            {
                string simbriefUser = System.Configuration.ConfigurationManager.AppSettings["simbrief_user"];
                if (string.IsNullOrEmpty(simbriefUser)) return;

                var plan = await _simbriefEnhanced.FetchAndParseOFP(simbriefUser);
                if (plan == null) return;

                bool originMatch = string.Equals(plan.Origin,      pirep.Origin,      StringComparison.OrdinalIgnoreCase);
                bool destMatch   = string.Equals(plan.Destination, pirep.Destination, StringComparison.OrdinalIgnoreCase);

                if (originMatch && destMatch)
                {
                    _cb.SetActivePlan?.Invoke(plan);
                    _cb.Log?.Invoke(
                        $"  [OFP]  SimBrief plan loaded — {plan.Origin}→{plan.Destination}  FL{plan.CruiseAltitude / 100}",
                        Theme.Success);
                }
                else
                {
                    _cb.Log?.Invoke(
                        $"  [OFP]  SimBrief plan mismatch ({plan.Origin}→{plan.Destination}), skipped",
                        Theme.Warning);
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke($"  [OFP]  SimBrief reload failed: {ex.Message}", Theme.Warning);
            }
        }

        internal async Task CheckAndResumeFlight(Pilot pilot)
        {
            try
            {
                var activePireps = await _apiService.GetActivePireps();
                if (!activePireps.Any()) return;

                var candidate = activePireps
                    .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    .First();

                var detail = await _apiService.GetPirepDetail(candidate.Id);
                if (detail == null) detail = candidate;

                var lastUpdate = !string.IsNullOrEmpty(detail.UpdatedAt) ? detail.UpdatedAt : detail.CreatedAt;
                var minutesAgo = "";
                if (DateTime.TryParse(lastUpdate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var lastDt))
                    minutesAgo = $"{(int)(DateTime.UtcNow - lastDt.ToUniversalTime()).TotalMinutes} min ago";

                var message = $"🔄 ACTIVE FLIGHT FOUND\n\n" +
                              $"Flight:       {detail.FlightNumber}\n" +
                              $"Route:        {detail.Origin} → {detail.Destination}\n" +
                              $"Aircraft:     {detail.AircraftType}\n" +
                              $"Flight time:  {detail.FlightTime} min\n" +
                              $"Last update:  {minutesAgo}\n\n" +
                              $"Do you want to resume this flight?";

                if (_cb.ShowConfirmation == null) return;

                var result = await _cb.ShowConfirmation(message, "RESUME FLIGHT?", EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    _flightManager.ResumeFlight(detail, pilot);
                    LastCheckpointSent = DateTime.MinValue;
                    _cb.UpdateFlightInfo?.Invoke();
                    _cb.ButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                    _cb.Log?.Invoke(_("Log_FlightResumed"), Theme.Success);
                    await ResumeFromAcarsHistoryAsync(detail.Id);
                    await ResumeFromSimbriefAsync(detail);
                }
                else
                {
                    _cb.Log?.Invoke(_("Log_ResumeDeclined"), Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_ResumeCheckError", ex.Message), Theme.Warning);
            }
        }
    }
}
