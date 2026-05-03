# vmsOpenAcars — Guía para Claude

## Qué es el proyecto

Cliente ACARS de escritorio (Windows Forms, .NET 4.8, C# 7.3) que conecta simuladores de vuelo con aerolíneas virtuales basadas en phpVMS v7. Lee datos del simulador via FSUIPC/XUIPC, los procesa y los envía a la API REST de phpVMS.

**Versión actual:** v0.3.16  
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
│                           FlightHistoryForm, LandingAnalysisForm, EcamDialog
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

11 criterios, puntuación parte de 100 y deduce:

| Criterio | Deducción máx | Umbrales |
|---|---|---|
| Landing Rate | 40 pts | ≤100=0, ≤200=5, ≤300=15, ≤400=25, ≤600=35, >600=40 |
| G-Force | 15 pts | ≤1.3g=0, ≤1.5g=7, >1.5g=15 |
| Bank Angle | 10 pts | ≤2°=0, ≤5°=5, >5°=10 |
| Pitch Angle | 10 pts | 1°-5°=0 (ideal), fuera de rango deduce 5-10 |
| Overspeed | 15 pts | 0=0, 1=7, ≥2=15 |
| Lights Compliance | 10 pts | 5 pts por violación, cap 10 |
| Stabilized Approach (1000 ft) | 15 pts | 6 criterios (speed, VS, bank, pitch, gear, flaps) |
| QNH Compliance | 5 pts | 5 pts si Δ>2 hPa |
| IVAO Offline | 5 pts | 5 si vuelo sin conexión IVAO |
| Touchdown Zone | 7 pts | ≤1500 ft=0, ≤2500=3, >2500=7 — activo si `TouchdownDistanceFt > 0` |
| Centreline Deviation | 7 pts | ≤10 ft=0, ≤30=3, >30=7 — activo si `CenterlineDeviationFt > 0` |

### RunwayService — `Db/RunwayService.cs`

```csharp
bool IsAvailable
RunwayTouchdownResult FindTouchdownRunway(airport, lat, lon, heading)
RunwayTouchdownResult FindTakeoffRunway(airport, lat, lon, heading)
RunwayEntry           FindRunwayEntry(airport, lat, lon, heading)
string                FindNearestTaxiway(airport, lat, lon)
HoldingPoint          FindHoldingPoint(airport, lat, lon, heading)
ParkingSpot           FindNearestParking(airport, lat, lon)
RunwayTouchdownResult GetRunwayThreshold(airport, heading)          // para captura de aproximación
(double DistNm, double LateralFt) ComputeApproachMetrics(...)       // public static
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
│   └── _lblFmaPlanLine3  → vacía (reservada)
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
    → RunwayService.GetRunwayThreshold(dest, heading) → _approachThreshold
    → _approachBuffer.Clear()
OnRawDataUpdated (cada 50 ms)
    → si phase=Approach && AGL<3000 ft && ≥2 s elapsed
    → ComputeApproachMetrics(threshold, lat, lon) → distNm, lateralFt
    → _approachBuffer.Add(ApproachTrackPoint)
SendPirep() → FilePirep() éxito → SaveLandingRecord()
    → LandingLogService.SaveFlight(FlightRecord, _approachBuffer)
    → _approachBuffer.Clear()
```

### Propiedades públicas añadidas a FlightManager

```csharp
public double TouchdownDistanceFt   => _touchdownDistanceFt;
public double TouchdownCenterlineFt => _touchdownCenterlineDeviationFt;
public string TouchdownRunwayName   => _touchdownRunwayName;
public double TouchdownGForce       => _touchdownGForce;
```

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
| `Db/RunwayService.cs` | — | RunwayService + GetRunwayThreshold + ComputeApproachMetrics |
| `Models/SimbriefPlan.cs` | ~119 | `BlockFuel`, `TripFuel` |
| `Services/ScoringService.cs` | 213-244 | Touchdown Zone + Centreline deductions |
| `Models/FlightScoreData.cs` | ~85 | `TouchdownDistanceFt`, `CenterlineDeviationFt`, `RunwayName` |
| `Core/Flight/FlightManager.cs` | ~61 | Variables privadas touchdown |
| `Core/Flight/FlightManager.cs` | ~852 | `SetRunwayTouchdownData()` |
| `Core/Flight/FlightManager.cs` | — | 4 public readonly props TouchdownDistanceFt/CenterlineFt/RunwayName/GForce |
| `ViewModels/MainViewModel.cs` | 535-560 | `LookupRunwayData()` post-touchdown |
| `ViewModels/MainViewModel.cs` | 620-637 | `LookupTakeoffRunwayData()` |
| `ViewModels/MainViewModel.cs` | 562-617 | `HandleTaxiPositionUpdate()` ground ops |
| `ViewModels/MainViewModel.cs` | — | `_approachBuffer`, `_approachThreshold`, `SaveLandingRecord()` |
| `UI/Forms/MainForm.cs` | ~1018 | Construcción del panel FMA |
| `UI/Forms/MainForm.cs` | ~1490 | Update loop FMA plan lines |
| `UI/Forms/MainForm.cs` | — | Botón LOGBOOK (9.º botón, antes de START) |
| `Services/UIService.cs` | ~143 | `SetPhaseText()` |
| `Services/UIService.cs` | ~186 | `SetAirStatus()` |
| `UI/Forms/SettingsForm.cs` | — | Sección Landing Log con OpenFileDialog (CheckFileExists=false) |

---

## Próximas áreas de desarrollo (sin prioridad definida)

- **Línea 3 del FMA** — actualmente en blanco; candidatos: tiempo estimado restante, distancia al destino, OAT, viento actual
- **Touch-and-go** — ya detectado en FlightManager; verificar que scoring y approach buffer se resetean correctamente para el segundo aterrizaje
- **Score en Landing Log** — actualmente se guarda 0; conectar con `ScoringResult.TotalScore` después de `FilePirep`
