# vmsOpenAcars — Guía para Claude

## Qué es el proyecto

Cliente ACARS de escritorio (Windows Forms, .NET 4.8, C# 7.3) que conecta simuladores de vuelo con aerolíneas virtuales basadas en phpVMS v7. Lee datos del simulador via FSUIPC/XUIPC, los procesa y los envía a la API REST de phpVMS.

**Versión actual:** v0.4.17  
**IDE:** Visual Studio 2017 (el usuario compila desde el IDE, nunca desde CLI)

## Estructura de carpetas

```
vmsOpenAcars/
├── Core/Flight/          → FlightManager.cs  (máquina de estados de vuelo)
│   └── FlightTimer.cs
├── Db/                   → RunwayService.cs
├── Helpers/              → AppConfig, Constants, UnitConverter, L (localización)
├── Models/               → Aircraft, Flight, FlightScoreData, TouchdownData, TakeoffData,
│                           SimbriefPlan, Pirep, FlightRecord, ApproachTrackPoint
├── Services/             → ApiService, FsuipcService, ScoringService, MetarService,
│                           IvaoService, SimbriefEnhancedService, LandingLogService
├── ViewModels/           → MainViewModel.cs  (coordinación UI ↔ dominio)
├── UI/Forms/             → MainForm, SettingsForm, MetarDecodeForm, OFPViewerForm,
│                           FlightHistoryForm, LandingAnalysisForm, EcamDialog,
│                           OsdOverlayForm
├── Docs/                 → BRIEFING.md (guía de usuario final)
└── Controls/             → GaugeControl, LinearGauge, EngineMonitorPanel
```

## Stack tecnológico

- **FSUIPC** (FSUIPCClientDLL 3.3.16) — lectura de datos del simulador
- **System.Data.SQLite** (1.0.119) — LittleNavMap BD + landing_log.sqlite
- **System.Windows.Forms.DataVisualization** — gráficos de aproximación (incluido en .NET 4.8)
- **Newtonsoft.Json** — serialización REST
- **phpVMS v7** — backend de la aerolínea virtual (API REST)
- **SimBrief** — planes de vuelo / OFP

---

## Estado actual del sistema — todo implementado y funcional

### Scoring de aterrizaje — `Services/ScoringService.cs`

12 criterios, puntuación parte de 100 y deduce (score mínimo = 0):

| Criterio | Deducción máx | Umbrales / condición |
|---|---|---|
| Landing Rate | 40 pts | ≤100=0, ≤200=5, ≤300=15, ≤400=25, ≤600=35, >600=40 |
| G-Force | 15 pts | ≤1.3g=0, ≤1.5g=7, >1.5g=15 (omitido si dato = 0) |
| Bank Angle | 10 pts | ≤2°=0, ≤5°=5, >5°=10 |
| Pitch Angle | 10 pts | 1°–7°=0 (ideal nose-up); <−2°=10; −2°–1°=5; >8°=5 |
| Overspeed | 15 pts | 0=0, 1=7, ≥2=15 |
| Lights Compliance | 10 pts | 5 pts por violación, cap 10; Beacon exempto en aeronaves con switch compartido beacon/strobe (ver `BeaconStrobeSharedAircraft`) |
| Stabilized Approach (1000 ft) | 15 pts | 6 criterios al cruzar 1000 ft AGL ↓: speed fuera [Vref−Vref+X]=−5, VS<−1000=−5, VS>−100=−5, bank>7°=−3, pitch fuera [−2.5°,+10°]=−3, gear up=−5, flaps<50%=−4 |
| QNH Compliance | 10 pts | 5 pts si Δ>2 hPa — salida: TakeoffRoll; llegada: gate 1000 ft AGL (mismo momento que Stabilized, contador independiente) |
| IVAO Offline | 5 pts | −5 si piloto no conectado a IVAO al iniciar TaxiOut |
| On-Time Departure | 5 pts | −5 si Blocks Off difiere >10 min del STD (`sched_out`) |
| Touchdown Zone | 7 pts | ≤1500 ft=0, ≤2500=3, >2500=7 — activo solo si `TouchdownDistanceFt > 0` (requiere LNM DB) |
| Centreline Deviation | 7 pts | ≤10 ft=0, ≤30=3, >30=7 — activo solo si `CenterlineDeviationFt > 0` (requiere LNM DB) |
| Localizer Alignment | 5 pts | activo si ILS detectado. ILS not tuned=−3; heading >5° (x2 max)=−1 each; cap 5 |
| Minimums Compliance | 5 pts | −5 si `BelowMinimums = true` (descendió bajo DA sin aterrizar) |

### RunwayService — `Db/RunwayService.cs`

```csharp
bool IsAvailable
RunwayTouchdownResult FindTouchdownRunway(airport, lat, lon, heading)
RunwayTouchdownResult FindTakeoffRunway(airport, lat, lon, heading)
RunwayEntry           FindRunwayEntry(airport, lat, lon, heading)
string                FindNearestTaxiway(airport, lat, lon, heading)  // heading opcional; penaliza ×2,5 segmentos >50° desalineados (v0.4.17)
HoldingPoint          FindHoldingPoint(airport, lat, lon, heading)
ParkingSpot           FindNearestParking(airport, lat, lon)
RunwayTouchdownResult GetRunwayThreshold(airport, lat, lon, heading) // captura de aproximación — exige heading-delta ≤15°, |cross| ≤2 NM, along<0 (avión antes del umbral). Devuelve null si ninguna cumple → captura se difiere
(double DistNm, double LateralFt) ComputeApproachMetrics(...)       // public static
// ILS / Approach (v0.4.4)
IlsData              GetIlsForRunway(airport, runwayName)   // frecuencia MHz, curso, gs_pitch; null si no existe o no es ILS
ApproachInfo         GetApproachType(airport, runwayName)   // mejor procedimiento (ILS>RNAV>otro); fallback via runway_end_id
IList<ApproachFix>   GetApproachFixes(approachId)           // fixes IF/FAF/MAP del procedimiento
```

`RunwayTouchdownResult` incluye: `ThresholdDistanceFt`, `CenterlineDeviationFt`, `RunwayName`, `ThresholdLat`, `ThresholdLon`, `ThresholdHeading`.

**BD LittleNavMap** — ruta configurada en `App.config` clave `lnm_db_path`. Usuario la cambia desde SettingsForm sección "NavMap Database".

**Esquema BD** (verificado en producción):
```
airport    → airport_id, ident, lonx, laty
runway     → runway_id, airport_id, primary_end_id, secondary_end_id, width (ft), length (ft)
runway_end → runway_end_id, name, heading, lonx, laty, offset_threshold
taxi_path  → taxi_path_id, airport_id, type ('T'=taxiway, 'P'=pavement), name, start_lonx/laty, end_lonx/laty
parking    → parking_id, airport_id, type, name, number, suffix, radius, lonx, laty
```

**Geometría flat-earth:**
```
dN = (lat - thLat) * 111320
dE = (lon - thLon) * 111320 * cos(thLat_rad)
along = dE * sin(heading_rad) + dN * cos(heading_rad)   → dist al umbral (metros)
cross = dE * cos(heading_rad) - dN * sin(heading_rad)   → desviación centreline (metros)
```

---

### Panel FMA — `UI/Forms/MainForm.cs`

```
outer (TableLayoutPanel 2 cols: 70% izq / 30% der)
├── [col 0] planLines (3 filas Percent 33%)
│   ├── _lblFmaPlanLine1  → "{Airline}{FlightNo}  {Orig}/{OrigIATA}  {Dest}/{DestIATA}  CI {CI}  {Fecha}  {Reg} {Tipo}"
│   ├── _lblFmaPlanLine2  → "PAX {n}  FUEL {block}  TRIP {trip}  CARGO {cargo}  FL{alt}  AVG WIND {dir}/{spd}  AVG ISA {isa}"
│   └── _lblFmaPlanLine3  → "RTE  {Route}"
└── [col 1] rightCol (3 filas Percent 33%)
    ├── lblPhase          → "PHASE BOARDING" / "PHASE TAXIOUT" / etc.
    ├── lblAir            → "GROUND" / "AIRBORNE" / "---"
    └── _lblDepartureCdw  → cuenta atrás de salida (20pt bold Consolas)
```

---

### SimBrief — `Models/SimbriefPlan.cs`

- `BlockFuel` ← `fuel.plan_ramp`
- `TripFuel`  ← `fuel.enroute_burn`
- `DepartureFuel` ← `fuel.plan_ramp` (alias)
- `ScheduledOutTime` ← `times.sched_out` (Unix, blocks-off — countdown ETD en FMA)
- `ScheduledOffTime` ← `times.sched_off` (Unix, wheels-off = sched_out + taxi_out — fecha en FMA)

---

## Landing Analysis — v0.3.16

### Base de datos: `landing_log.sqlite`

Ruta configurada en `App.config` → clave `landing_log_path` (vacío por defecto).  
En SettingsForm sección "Landing Log": usa `OpenFileDialog` con `CheckFileExists = false`.

**Tablas:**
- `flights` — un registro por vuelo (rate, G, dist, cl, score, METAR, runway, ruta…)
- `approach_track` — puntos de trayectoria (cada 2 s, AGL < 3000 ft)

### Archivos del sistema Landing Analysis

| Archivo | Propósito |
|---|---|
| `Models/FlightRecord.cs` | POCO tabla `flights` |
| `Models/ApproachTrackPoint.cs` | POCO tabla `approach_track` |
| `Services/LandingLogService.cs` | CRUD SQLite: `SaveFlight`, `GetFlights`, `GetTrackPoints`, `DeleteFlight`, `HasFlights`, `SeedMockData` |
| `UI/Forms/FlightHistoryForm.cs` | Modal: historial DataGridView con comparación y borrado |
| `UI/Forms/LandingAnalysisForm.cs` | No-modal: 4 gráficos con modo comparación |

### LandingLogService — métodos clave

- `SaveFlight(FlightRecord, IList<ApproachTrackPoint>)` — INSERT en transacción, dos comandos separados (INSERT + `SELECT last_insert_rowid()`)
- `DeleteFlight(int id)` — borra en transacción: primero `approach_track`, luego `flights`
- `SeedMockData()` — 5 vuelos SKRG RWY 01 (thLat=6.149458, thLon=-75.423049, thHdg=359.66°); solo disponible en `#if DEBUG`

### FlightHistoryForm

- `MultiSelect = true` en el DataGridView
- Botones (derecha → izquierda): CLOSE · VIEW ANALYSIS · COMPARE · DELETE
- `UpdateButtonStates()`: VIEW ANALYSIS activo con exactamente 1 seleccionado; COMPARE con 2-5; DELETE con ≥1
- DELETE usa `EcamDialog.Show(this, msg, "CONFIRM DELETE", EcamDialogButtons.YesNo)` para confirmación
- COMPARE: `_grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as FlightRecord).OrderBy(f => f.FlightDate)`
- SEED DEMO DATA solo en `#if DEBUG`

### LandingAnalysisForm

**Constructor:** `LandingAnalysisForm(IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> flights)`

- `IsComparison` → `_flights.Count > 1`
- Modo single: header con 7 celdas de stats; modo comparison: una fila por vuelo con punto de color + stats inline incluyendo fecha `MM-dd HH:mm`
- 4 gráficos: VERTICAL (AGL ft), LATERAL (desviación ft ± signed), IAS (kt), VS (fpm)
- Eje X invertido: 5 NM izquierda → 0 (umbral) derecha
- Líneas de referencia: planeo 3° (AGL = dist × 319), centreline cero, Vref promedio
- Suavizado Gaussiano (window=7, σ=window/4): aplicado a LATERAL, IAS, VS — NO a VERTICAL
- Nombres de series en comparación: `"{rec.FlightNumber} #{i+1}"` (único, evita `ArgumentException` con mismo callsign)
- Paleta de colores: azul, naranja, verde, violeta, dorado (`TrackColors[]`)

### Flujo de captura de aproximación

```
Phase → Approach
    → _approachBuffer.Clear(), _approachThreshold = null
OnRawDataUpdated (cada 50 ms, fase = Approach)
    → si _approachThreshold == null:
         GetRunwayThreshold(dest, lat, lon, hdg)
         requiere heading-delta ≤15° AND |cross| ≤2 NM AND along<0
         si null → no captura, reintenta el siguiente ciclo
    → al adquirir pista: Task.Run(LoadApproachData(dest, runway))
    → si AGL<3000 ft && ≥2 s elapsed:
         ComputeApproachMetrics(threshold, lat, lon) → distNm, lateralFt
         _approachBuffer.Add(ApproachTrackPoint)
SendPirep()
    → SnapshotLandingRecord()          ← captura plan + touchdown ANTES de FilePirep
    → FilePirep() → ResetFlightState() ← aquí se borran _activePlan y touchdown data
    → éxito → SaveLandingRecord(record)
        → record.Score = LastFlightScore  ← LastFlightScore NO se resetea en ResetFlightState
        → LandingLogService.SaveFlight(record, _approachBuffer)
        → _approachBuffer.Clear()
        → log de diagnóstico (éxito o motivo de fallo)
```

> **Importante:** `FilePirep()` llama internamente a `ResetFlightState()`, que pone `_activePlan = null`
> y resetea todos los campos de touchdown. `SnapshotLandingRecord()` debe ejecutarse **antes** de
> awaitar `FilePirep()`. `LastFlightScore` es la única propiedad que no se resetea y puede leerse
> de forma segura después.

### Propiedades públicas añadidas a FlightManager

```csharp
public double TouchdownDistanceFt   => _touchdownDistanceFt;
public double TouchdownCenterlineFt => _touchdownCenterlineDeviationFt;
public string TouchdownRunwayName   => _touchdownRunwayName;
public double TouchdownGForce       => _touchdownGForce;
// ILS / Approach (v0.4.4)
public void SetApproachData(IlsData ils, ApproachInfo approach, IList<ApproachFix> fixes)
```

### ILS / Approach Detection — v0.4.4

**New result types in `Db/RunwayService.cs`:**

| Clase | Propiedades clave |
|---|---|
| `IlsData` | `FrequencyMhz`, `Course`, `GlideSlopePitch`, `RunwayName`, `ThresholdLat/Lon/ElevFt` |
| `ApproachInfo` | `ApproachId`, `Type`, `RunwayName`, `HasVerticalGuidance` |
| `ApproachFix` | `Name`, `FixType` (IF/FAF/MAP), `Lat`, `Lon`, `AltitudeFt` |

**Flujo de carga de datos de aproximación:**
```
OnFlightPhaseChanged(Approach)
    → GetRunwayThreshold() → _approachThreshold (ya existente)
    → Task.Run(() => LoadApproachData(dest, rwyName))
        → GetIlsForRunway()  → ils
        → GetApproachType()  → approach
        → GetApproachFixes() → fixes
        → _flightManager.SetApproachData(ils, approach, fixes)
```

**Lógica de scoring ILS en FlightManager:**
- Al cruzar 1000 ft AGL (`CheckStabilizedApproachGate`): compara `data.Nav1FrequencyMhz` con `_expectedIls.FrequencyMhz` (tolerancia ±0.05 MHz). Si no coincide: `_ilsTunedCorrectly = false`, `_localizerViolations++`.
- Por debajo de 500 ft AGL (`CheckApproachBelowGate`): monitorea desviación de rumbo vs curso ILS. Si |hdgDelta| > 5°: `_localizerViolations++` (cap 2). Comprueba DA para `_belowMinimums`.
- Secuenciación de fixes: avanza `_nextFixIndex` cuando la aeronave está a <0.5 NM del siguiente fix.

**FsuipcService — nuevos offsets:**
- `0x0350 · INT16` — NAV1 active frequency (BCD). Decode: cada nibble = un dígito decimal → MHz (e.g. `0x1113` = 111.3 MHz)
- `0x0C4E · INT16` — NAV1 OBS / ILS course (0–359°)

---

## OSD Overlay — v0.4.6

### OsdOverlayForm — `UI/Forms/OsdOverlayForm.cs`

Ventana TopMost, sin borde, click-through (`WM_NCHITTEST → HTTRANSPARENT`), sin entrada en taskbar. Centrada horizontalmente en la pantalla configurada, 40 px desde el borde superior.

```csharp
public enum OsdSeverity { Info, Success, Warning, Critical }
public void ShowMessage(string text, OsdSeverity severity, int durationMs = 4000)
public void HideOsd()
```

`ShowMessage()` es thread-safe. Recalcula posición en cada llamada con `Screen.Bounds` (no `WorkingArea`).

**Animación:** FadeIn (0.06/tick) → Hold → FadeOut (0.04/tick). Critical: `_flashTimer` (220 ms, 3 ciclos on/off) antes del Hold.

### Configuración — App.config

| Clave | Default | Helper |
|---|---|---|
| `osd_enabled` | true | `AppConfig.OsdEnabled` |
| `osd_duration_seconds` | 4 | `AppConfig.OsdDurationMs` (× 1000) |
| `osd_screen_index` | 0 | `AppConfig.OsdScreenIndex` |
| `osd_opacity` | 90 | `AppConfig.OsdOpacity` |

### Puntos de disparo (MainViewModel → `OnOsdMessage`)

| Momento | Texto | Severidad |
|---|---|---|
| `StartFlight()` ok | `ACARS ACTIVE` | Success |
| Fase TaxiOut | `TAXI OUT` | Info |
| Fase TakeoffRoll | `TAKEOFF ROLL` | Info |
| Fase Enroute | `CRUISE` | Info |
| Fase Descent | `DESCENDING` | Info |
| Fase Approach | `APPROACH` | Info |
| Fase OnBlock | `ON BLOCK` | Info |
| Touchdown | `<calificación>  −XXX fpm  X.Xg` | Success/Info/Warning/Critical según fpm |
| Touch-and-go | `TOUCH AND GO` | Warning |
| PIREP filed | `PIREP FILED — SCORE: XX/100` | Success |
| Descent ≤ 10 500 ft AGL, landing lights apagadas | `LANDING LT OFF` | Warning (solo aviso, sin penalización) — flag `_landingLightReminderSent` en FlightManager; se resetea si AGL > 10 500 ft (v0.4.15) |
| Penalty lights (pushback/taxi/takeoff/below 9 500 ft AGL) | `PENALTY  NAV/TAXI/STROBE/LANDING LT  −5 PTS` | Warning |
| Penalty QNH | `PENALTY  QNH  −5 PTS` | Warning |
| Overspeed | `OVERSPEED  XXX KTS` | Critical |
| Unstabilized approach | `UNSTABILIZED  −N PTS` | Critical |
| Go-around | `GO AROUND` | Warning |

### Integración en MainForm

```csharp
_viewModel.OnOsdMessage += (text, severity) =>
    _osd.ShowMessage(text, severity, AppConfig.OsdDurationMs);
```

MENU button (antes del botón START) → submenú "Test OSD" con 4 opciones (Info/Success/Warning/Critical).

---

## Flujo completo del scoring (touchdown zone + centreline)

```
FSUIPC → TouchdownDetected(lat, lon, heading)
    → MainViewModel.LookupRunwayData()  [Task.Run]
    → RunwayService.FindTouchdownRunway()
    → FlightManager.SetRunwayTouchdownData(distFt, clFt, rwyName)
FilePirep() → ScoringService.Calculate(FlightScoreData)
    → TDZ y CL activos si dist/cl > 0
→ ApiService.FilePirep() → phpVMS
→ LandingLogService.SaveFlight()
```

---

## Referencias de líneas clave

| Archivo | Línea aprox. | Contenido |
|---|---|---|
| `Db/RunwayService.cs` | ~267 | `SafeProjectOnRunway()` con `WithinFootprint` para desambiguar pistas paralelas |
| `Db/RunwayService.cs` | ~175 | `FindNextIntersection()` — próxima intersección de calle de rodaje adelante |
| `Models/SimbriefPlan.cs` | ~119 | `BlockFuel`, `TripFuel` |
| `Services/ScoringService.cs` | ~213 | Touchdown Zone + Centreline deductions |
| `Services/ScoringService.cs` | ~247 | Localizer Alignment + Minimums Compliance deductions (v0.4.4) |
| `Models/FlightScoreData.cs` | ~85 | `TouchdownDistanceFt`, `CenterlineDeviationFt`, `RunwayName` |
| `Core/Flight/FlightManager.cs` | ~61 | Variables privadas touchdown |
| `Models/FlightScoreData.cs` | ~125 | `LnmDbAvailable` (v0.4.12) — penalización de 14 pts si BD LNM no disponible |
| `Core/Flight/FlightManager.cs` | ~224 | `LnmDbAvailable` property — seteado desde MainViewModel al iniciar vuelo (v0.4.12) |
| `Core/Flight/FlightManager.cs` | ~1679 | `FilePirep()` incluye `LnmDbAvailable` en score data (v0.4.12) |
| `ViewModels/MainViewModel.cs` | ~1092 | `LnmDbAvailable` y log inmediato si BD LNM ausente (v0.4.12) |
| `Core/Flight/FlightManager.cs` | ~852 | `SetRunwayTouchdownData()` |
| `Core/Flight/FlightManager.cs` | ~870 | `SetApproachData()` — carga ILS/approach/fixes para scoring (v0.4.4) |
| `Core/Flight/FlightManager.cs` | ~705 | `CheckApproachBelowGate()` — localizer alignment + DA check (v0.4.4) |
| `Core/Flight/FlightManager.cs` | ~580 | `CheckViolations()` beacon exemption: `BeaconStrobeSharedAircraft` (DH8D) |
| `Core/Flight/FlightManager.cs` | ~1022 | Touchdown detection con guardia de fase: solo Descent/Approach/Landing (evita falsos touchdowns en Takeoff/Climb) |
| `ViewModels/MainViewModel.cs` | 535-560 | `LookupRunwayData()` post-touchdown |
| `ViewModels/MainViewModel.cs` | 620-637 | `LookupTakeoffRunwayData()` |
| `ViewModels/MainViewModel.cs` | ~562 | `HandleTaxiPositionUpdate()` — histéresis 3 ciclos para cambio de taxiway; pasa `heading` a `FindNearestTaxiway` (v0.4.17) |
| `ViewModels/MainViewModel.cs` | ~177 | `_pendingTaxiway`, `_pendingTaxiwayCount` — estado de histéresis de taxiway (v0.4.17) |
| `Services/LocalizationService.cs` | ~20 | Idioma forzado a `"es"` — ignora Settings (v0.4.17) |
| `ViewModels/MainViewModel.cs` | ~210 | Approach capture start log: `Lnm_ApproachCaptureStart` (pista, AGL, distancia) |
| `Services/MetarService.cs` | ~57 | `DoFetchAsync` refactorizado v0.4.10: wrappers `SafeFetch*` independientes, `OnMetarUpdated` en finally, evento `OnLog`, `ParseMetarToken` en try/catch |
| `Services/WeatherService.cs` | | QNH-only (usado por scoring) — independiente de MetarService (panel de METARs) |
| `UI/Forms/MainForm.cs` | ~2013 | `UpdateMetarPanel` usa `BeginInvoke(..., new object[] {metars})` — evita covarianza de arrays en `params object[]` (v0.4.10) |
| `UI/Forms/MainForm.cs` | ~2031 | `UpdateMetarPanelState` usa `BeginInvoke` no-bloqueante (v0.4.10) |
| `ViewModels/MainViewModel.cs` | ~1101 | `SendPirep()` → llama `SnapshotLandingRecord()` antes de `FilePirep()` |
| `ViewModels/MainViewModel.cs` | ~1120 | `SnapshotLandingRecord()` — captura plan+touchdown antes del reset |
| `ViewModels/MainViewModel.cs` | ~1138 | `SaveLandingRecord(FlightRecord)` — añade Score y persiste a SQLite |
| `UI/Forms/MainForm.cs` | ~1018 | Construcción del panel FMA |
| `UI/Forms/MainForm.cs` | ~1490 | Update loop FMA plan lines |
| `UI/Forms/MainForm.cs` | — | Botón LOGBOOK (9.º botón) · Botón MENU (10.º) · Botón START (11.º) |
| `UI/Forms/MainForm.cs` | — | `OnOsdMessage` handler → `_osd.ShowMessage()` |
| `UI/Forms/OsdOverlayForm.cs` | — | OSD overlay completo (v0.4.6) |
| `Services/UIService.cs` | ~143 | `SetPhaseText()` |
| `Services/UIService.cs` | ~186 | `SetAirStatus()` |
| `UI/Forms/SettingsForm.cs` | — | Sección Landing Log con OpenFileDialog (CheckFileExists=false) |
| `UI/Forms/SettingsForm.cs` | — | Sección OSD: checkBox + numericUpDown duration/opacity/screen |
| `ViewModels/MainViewModel.cs` | — | `OnOsdMessage` event (`Action<string, OsdSeverity>`) |
| `ViewModels/MainViewModel.cs` | — | `SetActivePlan()` → `OnButtonStateChanged("START", enabled=true)` (v0.4.6) |

---

## Detección de fases — umbrales clave

| Transición | Condición | Debounce |
|---|---|---|
| Climb → Enroute | VS < 200 fpm + cerca de crucero, o timeout 5 min + VS < 100 fpm | 10 s |
| Climb → Descent | VS < −500 fpm **y** alt < máx−500 ft | 20 s |
| Enroute → Climb (step) | VS > 500 fpm + alt < crucero−500 ft | 10 s |
| Enroute → Descent | VS < −500 fpm **y** alt < máx−500 ft | 20 s |
| Descent → Approach | dist < 10% totalDist o AGL < aglThreshold | inmediato |
| Descent → Climb | VS > 500 fpm (sin estar en zona approach) | 20 s |
| Approach → Go-around | VS > 600 fpm + AGL 100–3000 ft + ≥30 s en Approach | 10 s |

Los umbrales elevados (−500 fpm, 20 s) evitan que cambios de QNH o turbulencia suave (~100 fpm) disparen falsas transiciones.

## Próximas áreas de desarrollo (sin prioridad definida)

- **Touch-and-go real** — el guard de 5 s filtra rebotes, pero un T&G real reinicia el estado ILS/approach. Verificar que scoring y approach buffer se resetean correctamente para el segundo aterrizaje.
- **MetarRaw en logbook** — `FlightRecord.MetarRaw` existe en el esquema pero no se popula en `SnapshotLandingRecord()`; el METAR de destino podría obtenerse de `MetarService` en ese momento.
- **ILS - aeropuertos sin `runway_name` en `approach`** — `GetApproachType` ya tiene fallback via `runway_end_id`, pero algunos aeropuertos pueden no tener filas en `approach` para ILS (solo en tabla `ils`). En esos casos `ApproachInfo` será null aunque `IlsData` sea válido.
- **DA calculada** — actualmente DA = threshold elevation + 200 ft (constante conservadora). Podría calcularse dinámicamente desde el `approach_leg` runway threshold fix (`altitude1`) cuando esté disponible.
- **OSD en go-around** — actualmente no hay mensaje OSD al detectar go-around. Podría añadirse `GO AROUND` (Warning) en `OnFlightPhaseChanged` cuando la fase vuelve a Climb desde Approach.
