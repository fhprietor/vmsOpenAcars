# vmsOpenAcars — Documentación de Arquitectura

> Versión del documento: 0.9.15  
> Última actualización: 2026-09-24

---

## ¿Qué es vmsOpenAcars?

Cliente ACARS de escritorio para Windows que conecta un simulador de vuelo con una aerolínea virtual basada en **phpVMS v7**. El piloto vuela en el simulador y la aplicación registra, valida y envía el PIREP automáticamente, sin intervención manual.

---

## Stack Técnico

| Componente | Tecnología |
|---|---|
| Plataforma | .NET Framework 4.8, C# 7.3 |
| UI | Windows Forms (WinForms) |
| Sim → App | FSUIPC / FSUIPC7 — FSUIPCClientDLL 3.3.16 · XUIPC (X-Plane) |
| App → VA | phpVMS v7 REST API |
| Serialización | Newtonsoft.Json 13 |
| Plan de vuelo | SimBrief API (JSON) |
| BD de pista | NavData API (REST, HTTP client estático con caché ICAO) |
| BD de historial | System.Data.SQLite 1.0.119 (landing_log.sqlite) |
| Gráficos | System.Windows.Forms.DataVisualization (incluido en .NET 4.8) |
| PDF | PdfiumViewer 2.13 + pdfium.dll x64 |
| Audio cabina | NAudio 2.3.0 (`AudioFileReader` + `WaveOutEvent`) |
| Clima / QNH | METAR vía WeatherService |
| Localización | JSON (en.json / es.json) |
| Configuración | App.config + Settings.settings |

---

## Estructura de Carpetas

```
vmsOpenAcars/
├── Controls/               EngineMonitorPanel (panel N1/aceite/reversa por motor)
├── Core/
│   ├── Flight/             FlightManager (partial), FlightPhaseStateMachine,
│   │                       ApproachValidator, TouchdownState, PenaltyState,
│   │                       FlightManager.Telemetry, FlightManager.Lifecycle,
│   │                       EngineStartMonitor, ThrustReverserMonitor,
│   │                       FlightTimer, PirepBuilder
│   └── Helpers/            AppInfo
├── Db/                     Solo tipos resultado (RunwayTouchdownResult, IlsData, ApproachInfo…)
├── Docs/                   BRIEFING.md (guía usuario), architecture.md, CHANGELOG.md
├── Helpers/                AppConfig, Constants, FlightPhaseHelper, L (localización), UnitConverter, SystemInfoHelper
├── Languages/              en.json, es.json
├── Models/                 Aircraft, Flight, Pirep, SimbriefPlan, FlightPhase,
│                           FlightScoreData (ScoredEngineType), TouchdownData, TakeoffData,
│                           FlightRecord, ApproachTrackPoint, NavData
├── Properties/             AssemblyInfo, Resources, Settings
├── Services/               ApiService, FsuipcService, ScoringService, MetarService,
│                           IvaoService, SimbriefEnhancedService, LandingLogService,
│                           NavDataService, NavDataClient, NavDataCache,
│                           AirspaceMonitorService, CabinAnnouncementService
├── UI/
│   ├── Forms/              MainForm, FlightPlannerForm, OFPViewerForm, SettingsForm,
│   │                       MetarDecodeForm, EcamDialog,
│   │                       FlightHistoryForm, LandingAnalysisForm,
│   │                       OsdOverlayForm, MapForm, ApproachChartForm
│   ├── Map/                MapRouteController (+ .Approach.cs + .Helpers.cs),
│   │                       MapOverlayManager, SidebarController
│   └── Theme.cs            Paleta de colores centralizada
└── ViewModels/             MainViewModel, TelemetryCoordinator, AcarsReporter
```

---

## Arquitectura General

El proyecto sigue un patrón **MVVM ligero**:

```
MainForm.cs  (Vista — WinForms)
    │
    └── MainViewModel.cs  (ViewModel — coordinación, eventos, lógica de botones)
            │
            ├── TelemetryCoordinator.cs   Puente FsuipcService→FlightManager; throttling OSD/map
            ├── AcarsReporter.cs          Envío PIREP, resume desde historial, checkpoint 60 s
            ├── FlightManager.cs          Núcleo de vuelo (partial + archivos satélite):
            │     ├── FlightPhaseStateMachine.cs   Máquina de estados; umbrales y debounce
            │     ├── ApproachValidator.cs          Gate 1000 ft; localizer; DA/MDA
            │     ├── TouchdownState.cs             TDZ, centreline, g-force, bank, pitch
            │     ├── PenaltyState.cs               Acumulación de penalizaciones y violaciones
            │     ├── EngineStartMonitor.cs         Idle time y estabilización de motores
            │     ├── ThrustReverserMonitor.cs      Cool-down post-reversa
            │     ├── FlightManager.Telemetry.cs    Procesamiento de RawTelemetryData
            │     └── FlightManager.Lifecycle.cs    Ciclo de vida PIREP (prefile, file, resume)
            ├── FsuipcService.cs          Polling del simulador (FSUIPC/XUIPC)
            ├── ApiService.cs             HTTP client → phpVMS REST API
            ├── PhpVmsFlightService.cs    Vuelos, bids, PIREPs
            ├── SimbriefEnhancedService.cs Plan de vuelo + OFP PDF
            ├── WeatherService.cs         QNH vía METAR
            ├── NavDataService.cs         Runway/taxiway/ILS/approach (NavDataClient + caché)
            ├── AirspaceMonitorService.cs Espacios aéreos + ATC IVAO
            └── LandingLogService.cs      Historial de aterrizajes (SQLite)
```

La comunicación ViewModel → Vista se hace mediante **eventos** (`Action<T>`, `Func<T>`). La Vista nunca llama directamente a servicios de infraestructura.

---

## Fases de Vuelo

La máquina de estados central (`FlightManager`) gestiona las siguientes fases:

```
Idle
 └─► Boarding
      └─► Pushback ──────────────────────────────────────────┐
      └─► TaxiOut (directo sin pushback)                     │
           └─► TakeoffRoll                                   │ (pushback → taxi)
                └─► Takeoff                                  │
                     └─► Climb                               │
                          └─► Enroute                        │
                               └─► Descent                   │
                                    └─► Approach             │
                                         │                   │
                                         ├─► Climb (go-around)
                                         │
                                         └─► (touchdown)
                                              └─► AfterLanding
                                                   └─► TaxiIn
                                                        └─► OnBlock
                                                             └─► Arrived
                                                                  └─► Completed
```

Cada transición envía un código de estado al servidor phpVMS:

| Fase | Código phpVMS |
|---|---|
| Boarding | `BST` |
| Pushback | `PBT` |
| TaxiOut / TaxiIn | `TXI` |
| Takeoff | `TOF` |
| Climb | `ICL` |
| Enroute | `ENR` |
| Descent | `APR` |
| Approach | `FIN` |
| Landing | `LDG` |
| OnBlock / Completed | `ARR` |

---

## Módulos

### FsuipcService

Servicio de lectura del simulador. Opera en un timer a **50 ms** e interpola los offsets FSUIPC.

**Offsets principales leídos:**

| Dato | Offset | Conversión |
|---|---|---|
| Latitud | 0x0560 | `raw × 90 / (10001750 × 65536²)` |
| Longitud | 0x0568 | `raw × 360 / 2⁶⁴` |
| Altitud MSL | 0x0570 | `(raw / 65536²) × 3.28084` ft |
| Heading | 0x0580 | `raw × 360 / 2³²` |
| Ground Speed | 0x02B4 | `(raw / 65536) × 1.94384` kts |
| Vertical Speed | 0x02C8 | `(raw / 256) × 196.85` fpm |
| IAS | 0x02BC | `raw / 128` kts |
| Radar Altímetro | 0x31E4 | metros → feet |
| Luces | 0x0D0C | bitfield (ver tabla de luces) |
| QNH (Kohlsman) | 0x0330 | `raw / 16.0` hPa |
| Viento velocidad | 0x0E90 | kts directo |
| Viento dirección | 0x0E92 | `raw × 360 / 65536` grados |
| OAT | 0x0E8C | `raw / 256` °C |
| G-Force | 0x11BA | `raw / 625.0` |

**Bitfield de luces (0x0D0C):**

| Bit | Luz |
|---|---|
| 0 | NAV |
| 1 | BEACON |
| 2 | LANDING |
| 3 | TAXI |
| 4 | STROBE |
| 5 | SEAT BELT SIGN |

**Detección de eventos con debounce:**

| Evento | Tipo | Umbral |
|---|---|---|
| NAV light | Hold (v0.6.7) | 2.5 s estable |
| STROBE light | Hold (v0.6.7) | 2.5 s estable |
| BEACON light | Hold (v0.6.7) | 2.5 s estable |
| LANDING light | Hold (v0.6.7) | 2.5 s estable |
| TAXI light | Hold (v0.6.7) | 2.5 s estable |
| Parking Brake | Cooldown | 2.0 s |
| Flaps | Cooldown | 500 ms + histéresis 1% |
| Touchdown / Liftoff | Cooldown | 2.0 s + umbral GS |

> **Hold vs. Cooldown:** el hold debounce exige que el nuevo estado se mantenga estable `N` segundos antes de disparar el evento — cualquier revertido cancela el contador. El cooldown dispara inmediatamente y bloquea repetidos durante `N` segundos. Para las luces se usa hold porque los glitches del sim (~1.6 s) revertían el estado antes de que expirara el cooldown anterior (1.5 s), generando falsos positivos.

**Telemetría adaptativa por fase:**

| Fase | Intervalo |
|---|---|
| Taxi | 30 s |
| Takeoff / Approach | 5 s |
| Climb / Descent | 15 s |
| Enroute | 30 s |
| Otros | 30 s |

---

### FlightManager

Núcleo de la lógica del vuelo, implementado como `partial class` en varios archivos satélite:

| Archivo | Responsabilidad |
|---|---|
| `FlightManager.cs` | Propiedades públicas, `CheckProcedureAtPhaseEntry`, `CheckViolations`, `GetEngineLifecycleSnapshot` |
| `FlightPhaseStateMachine.cs` | Máquina de estados, umbrales de fase, debounce, `_boardingStationaryConfirmed` |
| `ApproachValidator.cs` | Gate 1 000 ft AGL (speed, VS, bank, pitch, gear, flaps), localizer, DA/MDA |
| `TouchdownState.cs` | TDZ, centreline, g-force, bank, pitch; reset en `ResetFlightState()` |
| `PenaltyState.cs` | Contadores de violaciones + `EngineWarmupViolation` / `EngineCooldownViolation` |
| `EngineStartMonitor.cs` | Idle time por motor, estabilización N2/aceite, `CheckPreTakeoff()` |
| `ThrustReverserMonitor.cs` | Despliegue de reversas post-touchdown, cool-down 180 s |
| `FlightManager.Telemetry.cs` | Procesamiento ciclo a ciclo de `RawTelemetryData` |
| `FlightManager.Lifecycle.cs` | `PrefilePirep`, `FilePirep`, `ResetFlightState`, `BuildScoreData()` |
| `PirepBuilder.cs` | `ComputeScore(FlightScoreData)`, `LogScore(ScoringResult)`, `BuildPayload()` |

Recibe `RawTelemetryData` cada ciclo y:

1. Actualiza propiedades públicas (altitud, GS, VS, luces, motores…)
2. Aplica **debounce de luces** (2.5 s hold) antes de actualizar los campos internos usados en compliance
3. Detecta transiciones de fase con umbrales relativos al plan de vuelo
4. Llama a `CheckProcedureAtPhaseEntry()` en cada transición
5. Llama a `CheckViolations()` cada ciclo mientras el avión está airborne y hay PIREP activo

**Propiedades públicas de touchdown** (expuestas para LandingLogService):

```csharp
public double TouchdownDistanceFt   => _touchdownDistanceFt;
public double TouchdownCenterlineFt => _touchdownCenterlineDeviationFt;
public string TouchdownRunwayName   => _touchdownRunwayName;
public double TouchdownGForce       => _touchdownGForce;
```

#### Cálculo de AGL

| Condición | Fuente |
|---|---|
| En tierra (`IsOnGround = true`) | `0` siempre |
| Fase **Enroute** | Radar altímetro (`RadarAltitude`); fallback a baro si = 0 |
| Resto de fases aéreas | `CurrentAltitude − ReferenceAirportElevation` |

`ReferenceAirportElevation` devuelve la elevación del aeropuerto de **salida** en fases de despegue/climb, y la elevación del aeropuerto de **destino** en fases de llegada.

> El radar altímetro **no se usa en aproximación** porque en terrenos montañosos (Andes, Alpes, Himalaya) lee la orografía bajo las ruedas, que puede estar muy por encima del aeropuerto, dando un AGL incorrecto.

#### Detección de Go-Around

Un go-around se confirma si se cumplen **todos** los criterios simultáneamente:

| Criterio | Valor |
|---|---|
| VS positivo | > 600 fpm |
| Duración sostenida | ≥ 8 segundos consecutivos |
| Tiempo mínimo en fase Approach | ≥ 30 segundos |
| AGL (MSL − destElev) | entre 100 y 3 000 ft |

El umbral de 600 fpm elimina falsos positivos por turbulencia o ajustes de pitch en final.

#### Compliance de Procedimientos (penalizaciones)

| Momento | Verificación | Penalización |
|---|---|---|
| Motores ON | BEACON encendida | −5 pts |
| Pushback | NAV encendida | −5 pts |
| TaxiOut | NAV + TAXI encendidas | −5 pts c/u |
| TakeoffRoll | STROBE + LANDING encendidas | −5 pts c/u |
| Vuelo < 9 500 ft AGL | LANDING encendida | −5 pts |
| TakeoffRoll | QNH ±2 hPa vs METAR origen | −5 pts |
| Gate 1 000 ft AGL (Approach) | QNH ±2 hPa vs METAR destino (o alterno si desvío) | −5 pts |

#### Detección de Pushback — `_boardingStationaryConfirmed` (v0.8.6)

El contador de pushback (`_pushbackStartTime`) solo comienza a correr después de que el avión haya estado estático (GS ≤ 0.5 kt) al menos una vez durante la fase Boarding. Esto evita que un avión que ya viene en movimiento al pulsar START (tractor enganchado, jitter de GS del sim) dispare un pushback falso tras exactamente `PushbackMinSec = 8 s`.

```csharp
// FlightPhaseStateMachine.cs — case Boarding:
if (inp.GroundSpeed <= 0.5)
{
    _boardingStationaryConfirmed = true;
    _pushbackStartTime = DateTime.MinValue;
}
else if (_boardingStationaryConfirmed)
{
    // evalúa pushback/taxi normalmente
}
// else: ya en movimiento al hacer START → ignora hasta detenerse
```

#### Debounce en TaxiOut → TakeoffRoll (v0.9.2)

Único caso de `FlightPhaseStateMachine.cs` que carecía del patrón "temporizador pendiente,
confirmar tras N s sostenidos" ya usado por el resto de transiciones sensibles (ver tabla de
umbrales arriba). Un pico breve de GS>30 kt durante un rodaje ágil (confirmado en vivo: 1-2 s
por encima de 30 kt en una calle paralela a la pista) bastaba para disparar `TakeoffRoll` y, con
él, de forma irreversible, la tabla de "Compliance de Procedimientos" de arriba (STROBE+LANDING,
QNH de salida) — pensada para un avión genuinamente en pista.

```csharp
// FlightPhaseStateMachine.cs — case TaxiOut:
if (inp.GroundSpeed > 30 && inp.Pitch < 1.0)
{
    if (_takeoffRollStart == DateTime.MinValue) _takeoffRollStart = DateTime.UtcNow;
    else if ((DateTime.UtcNow - _takeoffRollStart).TotalSeconds >= TakeoffRollConfirmSec) // 5 s
        TransitionTo(FlightPhase.TakeoffRoll, prev);
}
else { _takeoffRollStart = DateTime.MinValue; }
```

Complementado por dos fixes fuera del state machine (mismo incidente real,
`CLAUDE.md` § "Falso TAKEOFF ROLL durante taxi rápido"):
- `NavDataService.ProjectOnRunway` ahora retorna `null` (en vez del mejor match por rumbo sin
  validar posición) cuando ninguna pista pasa `WithinFootprint` — evita reportar una pista
  "detectada" con una desviación de centerline geométricamente imposible.
- `ApproachValidator._departureQnhChecked`/`_takeoffLightsChecked` — guard de un solo disparo
  por vuelo para el QNH de salida y las luces de `TakeoffRoll`, evitando doble penalización si
  la fase rebota o si hay un rechazo de despegue real seguido de un segundo intento.

---

### NavDataService — `Services/NavDataService.cs`

Reemplaza a `RunwayService`. Misma interfaz pública; datos vía `NavDataClient` en lugar de SQLite. Configurado en `App.config` con claves `navdata_api_url`, `navdata_api_key`, `navdata_api_domain`.

**API pública:**

```csharp
bool IsAvailable  // siempre true
void PrefetchAirport(icao)  // dispara carga en caché; llamar al iniciar vuelo
RunwayTouchdownResult FindTouchdownRunway(airport, lat, lon, heading)  // touchdown zone + centreline
RunwayTouchdownResult FindTakeoffRunway(airport, lat, lon, heading)    // pista de despegue
RunwayEntry           FindRunwayEntry(airport, lat, lon, heading)      // entrada a pista
string                FindNearestTaxiway(airport, lat, lon, heading)   // taxiway más cercano (heading opcional; penaliza ×2,5 segmentos >50°)
double                FindTaxiwaySegmentBearing(airport, taxiwayName, lat, lon)  // bearing del segmento más cercano (v0.5.8)
HoldingPoint          FindHoldingPoint(airport, lat, lon, heading)     // holding short
ParkingSpot           FindNearestParking(airport, lat, lon)            // gate / parking
RunwayTouchdownResult GetRunwayThreshold(airport, lat, lon, heading)   // umbral de aproximación — exige heading-delta ≤15°, |cross| ≤2 NM, along<0
(double DistNm, double LateralFt) ComputeApproachMetrics(...)         // proyección flat-earth (static)
IlsData              GetIlsForRunway(airport, runwayName)
ApproachInfo         GetApproachType(airport, runwayName)
IList<ApproachFix>   GetApproachFixes(airport, runwayName)
```

`RunwayTouchdownResult` incluye: `ThresholdDistanceFt`, `CenterlineDeviationFt`, `RunwayName`, `ThresholdLat`, `ThresholdLon`, `ThresholdHeading`.

**ILS / Approach result types:**
- `IlsData` — `FrequencyMhz`, `Course`, `GlideSlopePitch`, `RunwayName`, `ThresholdLat/Lon/ElevFt`
- `ApproachInfo` — `ApproachId`, `Type` ("ILS", "RNAV"…), `RunwayName`, `HasVerticalGuidance`
- `ApproachFix` — `Name`, `FixType` ("IF", "FAF", "MAP"), `Lat`, `Lon`, `AltitudeFt`

**Geometría flat-earth:**

```
dN = (lat - thLat) × 111320
dE = (lon - thLon) × 111320 × cos(thLat_rad)
along = dE × sin(hdg_rad) + dN × cos(hdg_rad)   → distancia desde extremo físico (m)
cross = dE × cos(hdg_rad) − dN × sin(hdg_rad)   → desviación centreline (m, signed)
distFt = along × 3.28084 − NavRunway.OffsetThresholdFt   → distancia desde umbral legal
ThresholdDistanceFt = Math.Max(0.0, distFt)              → 0 si toca antes del umbral
```

`threshold_lat/lon` es el **extremo físico** del pavimento; `OffsetThresholdFt` es el desplazamiento al umbral legal de aterrizaje. Restar el offset garantiza que la penalización TDZ se mide desde donde empieza legalmente la pista (v0.7.2). Pistas sin umbral desplazado tienen `OffsetThresholdFt = 0`; el comportamiento es idéntico al anterior.

---

### NavDataClient — `Services/NavDataClient.cs`

Cliente HTTP estático con caché por ICAO. Configura URL/key/domain desde `AppConfig`.

```csharp
static bool IsReachable   // true tras primer fetch exitoso
static bool IsKeyValid    // true si la key pasó el check en /airport/LEMD/runways/
static void PrefetchAirport(icao)              // GetOrAdd en caché; no bloquea
static List<NavRunway>    GetRunways(icao)
static List<NavTaxiway>   GetTaxiways(icao)
static List<NavParking>   GetParkings(icao)
static List<NavHoldShort> GetHoldShorts(icao)
static List<NavApproach>  GetApproaches(icao)  // incluye Transitions por approach (v0.7.0)
static List<NavProcedure> GetSids(icao)
static List<NavProcedure> GetStars(icao)
static NavAirportInfo     GetAirportInfo(icao)  // transition_altitude_ft / transition_level_ft (double?, null si no disponible)
static Task<NavApiTestResult> TestApiAsync(string apiKeyOverride = null)
    // Paso 1: GET /status/ → reachability
    // Paso 2: GET /airport/LEMD/runways/ con key → 401/403 = key inválida
static Task<BriefingCheckResult> CheckAnnouncementAsync(string phase, string lang)
    // GET /briefing/check/?phase={phase}&lang={lang} → { available, version }
static Task<byte[]> FetchBytesAsync(string path)
    // GET {NavDataApiUrl}/{path} — descarga binaria genérica; usado por CabinAnnouncementService
static void ClearMemoryCache()
    // Limpia todos los ConcurrentDictionary de aeropuertos/procedimientos en sesión.
    // No toca _airspaceMemCache. Llamar junto con NavDataCache.PurgeAirportData()
    // para forzar re-descarga completa desde el API. (v0.7.0)
```

- Caché: `ConcurrentDictionary<string, Task<NavAirportCache>>` — carga paralela de 6 endpoints por aeropuerto.
- Auth: cabeceras `X-API-Key` + `X-Origin-Domain`.
- Todos los endpoints usan la forma singular `/airport/` (no `/airports/`).

### NavDataCache — `Services/NavDataCache.cs`

Caché SQLite persistente (`NavData_cache.sqlite` junto al exe) para datos estáticos de NavData, renovados con el ciclo AIRAC.

**Tres tablas:**

| Tabla | Clave | Contenido |
|---|---|---|
| `airport_entries` | `(icao, data_type)` | Block, SIDs, STARs, ILS, waypoints por ICAO + ciclo AIRAC |
| `navaid_entries` | `cache_key` | Navaids por clave personalizada + ciclo AIRAC |
| `airspace_entries` | `tile_key` | Polígonos de espacio aéreo — TTL 7 días, **no vinculado al AIRAC** |

**API pública:**

```csharp
static void Initialize()       // crea esquema; auto-purga si airac_valid_until venció
static void SyncAirac(cycle, validUntil)
    // purga airport_entries + navaid_entries del ciclo anterior; no toca airspace_entries
static void PurgeAirportData() // (v0.7.0) elimina TODAS las filas de airport_entries
                               // y navaid_entries sin condición de ciclo; deja airspace_entries intacto
static bool TryGet*(icao, cycle, out json)   // lectura por ICAO + data_type
static void Store*(icao, cycle, json)        // escritura por ICAO + data_type
static bool TryGetAirspace(tileKey, out json, out cachedAt)  // TTL 7 días
static void StoreAirspace(tileKey, json)
```

`PurgeAirportData()` es el complemento de `ClearMemoryCache()` en NavDataClient: juntos garantizan que la siguiente llamada a `PrefetchAirport(icao)` descargue datos frescos tanto de la BD como del API, sin reiniciar la aplicación.

**DTOs en `Models/NavData.cs` relacionados:**

- `BriefingCheckResult`: `bool Available`, `string Version`.
- `NavApproach`: campos principales + `Transitions` (`List<NavApproachTransition>`, v0.7.0).
- `NavApproachTransition` (v0.7.0): `Fix` (nombre del IAF), `FixType`, `FixRegion`, `Type`, `Legs` (`List<NavApproachLeg>`).
- `NavAirspace`, `NavAirspaceGeometry`, `NavAirspaceFreq`, `NavAirspaceLimit`.

---

### CabinAnnouncementService — `Services/CabinAnnouncementService.cs` (v0.5.9)

Reproduce anuncios de cabina pregrabados descargados desde la NavData API. Opera en segundo plano con una cola FIFO de ítems de audio.

**Fases soportadas:** `boarding`, `taxi_out`, `on_runway`, `cruise`, `top_of_descent`, `approach`, `taxi_in`

**Caché local:** `%TEMP%\vmsacars\briefing\` — los MP3 se guardan como `{phase}_{lang}_{version}.mp3` y se reutilizan si ya existen.

**Idioma:** determinado por `airline.country` del piloto (phpVMS `GET /api/user`). Países hispanohablantes → `es`; resto → `en`. En vuelos internacionales (prefijo ICAO de país distinto) se reproducen los dos idiomas: inglés primero, luego nativo.

**Supresión automática:** si `Pilot.AircraftSeats > 0 && AircraftSeats < 40` (aeronave pequeña), el prefetch se cancela y no se reproducen anuncios.

**API pública:**

```csharp
Task   PrefetchAsync(string originIcao, string destIcao, string airlineCountry, int aircraftSeats = 0)
void   QueueAnnouncement(string phase)    // encola chime WAV + MP3 para la fase dada
Task<string> TestAnnouncementAsync(string phase, string lang = "en")  // descarga y reproduce en Settings
void   Reset()                            // limpia cola, rutas y caché de disco
void   Dispose()                          // ClearCacheFiles()
```

**Orden de cola por anuncio:**

| Vuelo | Ítems encolados |
|---|---|
| Doméstico / aerolínea anglohablante | `[__chime__, native.mp3]` |
| Internacional (nativo ≠ en) | `[__chime__, en.mp3, native.mp3]` |

**Playback:** NAudio `AudioFileReader` + `WaveOutEvent` + `ManualResetEventSlim` — bloquea el hilo `Task.Run` hasta que `PlaybackStopped` se dispara. El chime WAV usa `System.Media.SoundPlayer` desde recurso embebido (`Resources/Audio/chime_warning.wav`).

**Volumen en tiempo real:** `_currentReader` (volatile `AudioFileReader`) se asigna antes de `Play()` y se limpia tras `done.Wait()`. `SetVolume(int volume)` actualiza `AppConfig.CabinAnnouncementsVolume` y aplica `_currentReader.Volume = volume / 100f` inmediatamente. Cadena: `trkCabinVolume.ValueChanged` → `CabinVolumeChangedCallback` → `MainViewModel.SetCabinVolume()` → `SetVolume()`.

**Auto-save de controles de cabina:** el slider de volumen y el toggle "Enabled" llaman a `SaveConfigKey(key, value)` (helper privado de `SettingsForm`) en su propio handler, persistiendo el cambio en `App.config` en el acto. No participan en `HasChanges()` ni en `BtnSave_Click`, por lo que modificarlos no activa el botón Save ni provoca el cierre del diálogo.

**Stop:** `StopCurrent()` llama `_currentOutput?.Stop()` → dispara `PlaybackStopped` → libera `done` → el `Task.Run` termina limpiamente. Invocado en `TestAnnouncementAsync` (al seleccionar nueva fase de test) y en `Reset()` (fin o cancelación de vuelo).

**Triggers en MainViewModel:**

| Fase | Origen del trigger |
|---|---|
| `boarding` | `PrefetchAsync()` completado (al hacer START) |
| `taxi_out` | `OnFlightPhaseChanged(TaxiOut)` |
| `on_runway` | `LandingLightChanged(on=true)` o `StrobeLightChanged(on=true)` con GS ≤ 40 kt |
| `cruise` | `OnRawDataUpdated`: Enroute + AGL > 10 000 ft sostenido 30 s |
| `top_of_descent` | `OnFlightPhaseChanged(Descent)` |
| `approach` | `OnFlightPhaseChanged(Approach)` |
| `taxi_in` | `OnFlightPhaseChanged(TaxiIn)` |

Flags de guarda: `_cabinCruiseSent`, `_cabinOnRunwaySent` (reset en `StartFlight()` y en los tres exit paths). Configurable con `AppConfig.CabinAnnouncementsEnabled` (toggle live desde Settings).

---

### EngineStartMonitor — `Core/Flight/EngineStartMonitor.cs` (v0.8.5)

Monitorea el tiempo de ralentí de cada motor desde el arranque hasta el despegue, y evalúa criterios de estabilización de aceite.

**Umbrales de calentamiento:**

| OAT | Tiempo mínimo en ralentí |
|---|---|
| ≥ 5 °C | 120 s (2 min) |
| < 5 °C (cold soak) | 300 s (5 min) |

**Criterios de estabilización (por motor):** N2 ≥ 58 % + presión de aceite ≥ 15 PSI + temperatura de aceite ≥ 40 °C. Si el addon no escribe el offset N2 (siempre 0), el criterio N2 se omite (`_n2DataSeen` guard).

**Patrón `_needsInit`:** en el primer ciclo tras `Reset()`, `_eng1PrevRunning` se inicializa desde el estado actual del simulador — garantiza que motores ya en marcha al iniciar el vuelo no generen un falso "arranque tardío". Consecuencia: `Eng1IdleTime == TimeSpan.Zero` para motores pre-arrancados; el panel muestra `STAB ✓` / `CALENTANDO...` en su lugar.

**API pública:**

```csharp
bool     Eng1Stabilized, Eng2Stabilized   // N2 + aceite en rango verde
TimeSpan Eng1IdleTime, Eng2IdleTime       // 0 si motor pre-arrancado
EngineReadinessResult CheckPreTakeoff(double oatCelsius, bool eng1Running, bool eng2Running)
void Update(eng1Running, eng2Running, n2_1, n2_2, oilPress1, oilPress2, oilTemp1, oilTemp2)
void Reset()
```

**`EngineLifecycleSnapshot` (struct):** snapshot para el panel de UI. Incluye `Eng1/2Running`, `Eng1/2Stabilized`, `Eng1/2IdleTime`, `OilTemp1/2`, `OilPress1/2`, `OatCelsius`, `RequiredIdleSeconds`, más los campos de reversa de `ThrustReverserMonitor`.

---

### ThrustReverserMonitor — `Core/Flight/ThrustReverserMonitor.cs` (v0.8.5)

Monitorea el despliegue de reversas tras el aterrizaje y controla el cool-down mínimo antes de apagar motores.

**Umbral de reversa:** > 0.6 % de despliegue (offset FSUIPC FLOAT64 `0x207C`/`0x217C`, multiplicado × 100). Si tras 30 s post-touchdown el offset retorna siempre 0, se marca como "offset no soportado" por el addon.

**Cool-down requerido:** 180 s desde el touchdown si las reversas fueron usadas. `CheckShutdown()` devuelve un mensaje de advertencia si los motores se apagan antes de completar el cool-down (solo durante TaxiIn).

**API pública:**

```csharp
const int COOLDOWN_REQUIRED_SECONDS = 180
bool   ReversersUsed, ReverserDataAvailable
double MaxEng1RevPct, MaxEng2RevPct
int    SecondsSinceTouchdown
void   OnTouchdown()
void   Update(rev1Pct, rev2Pct)
string CheckShutdown()     // null si OK; mensaje si cool-down insuficiente
string CheckAddonSupport() // null si OK; mensaje si offset sin datos tras 30 s
void   Reset()
```

---

### EngineMonitorPanel — `Controls/EngineMonitorPanel.cs` (v0.8.5–0.8.6)

Control WinForms personalizado que muestra el estado de 1–4 motores en tiempo real. Se ubica en el panel de información lateral de `MainForm`.

**Layout por motor (3 filas):**

| Fila | Contenido |
|---|---|
| Row 0 (40 %) | `N1: 85.2 %` / `RPM: 1800` / `TRQ: 72.5 %` según categoría |
| Row 1 (35 %) | Estado idle: `IDLE 45s/2m` rojo → amarillo → `IDLE 2m05s/2m ✓` verde |
| Row 2 (25 %) | `OIL 55°C  28PSI` — rojo <40 °C / amarillo 40–70 °C / verde >70 °C |

Barra inferior (`DockStyle.Bottom`, 16 px): estado de reversas post-aterrizaje.

**Métodos públicos:**

```csharp
void UpdateEngines(RawTelemetryData data)          // N1/RPM/TRQ; rebuild si cambia categoría
void UpdateLifecycle(EngineLifecycleSnapshot snap)  // idle labels + oil labels + reverser bar
int  EngineCount { get; set; }                     // 1–4; rebuild automático
```

Actualizado cada ciclo desde `MainForm` vía:
```csharp
_engineMonitorPanel.UpdateEngines(fm.LastRawData);
_engineMonitorPanel.UpdateLifecycle(fm.GetEngineLifecycleSnapshot());
```

---

### ScoringService

Calcula un score de 0–100 al finalizar el vuelo. El score comienza en 100 y se aplican deducciones de 17 criterios (la suma bruta supera 100, por lo que el resultado se acota a 0):

| Criterio | Máx. deducción | Escala |
|---|---|---|
| Landing Rate | −40 pts | ≤150 fpm: 0 / ≤250: −5 / ≤350: −15 / ≤450: −25 / ≤650: −35 / >650: −40 |
| G-Force touchdown | −15 pts | ≤1.5g: 0 / ≤1.7g: −7 / >1.7g: −15 |
| Bank Angle touchdown | −10 pts | ≤2°: 0 / ≤5°: −5 / >5°: −10 |
| Pitch Angle touchdown | −10 pts | 1°–7°: 0 (ideal) / <−2°: −10 / −2°–1°: −5 / >8°: −5 |
| Overspeed | −15 pts | 0 eventos: 0 / 1: −7 / ≥2: −15 |
| Lights Compliance | −10 pts | −5 pts por violación, cap −10 |
| Stabilized Approach (1000 ft) | −15 pts | Evalúa speed, VS, bank, pitch, gear y flaps a 1000 ft AGL |
| QNH Compliance | −10 pts | −5 pts si Δ > 2 hPa — salida (TakeoffRoll) + llegada (gate 1000 ft AGL), independientes |
| Standard Pressure | −5 pts | −5 si no se aplica 1013 hPa al cruzar la altitud de transición (climb), vía `StdPressureViolation` |
| IVAO Offline | −5 pts | −5 si el vuelo se realizó sin conexión IVAO |
| On-Time Departure | −5 pts | −5 si Blocks Off difiere > 10 min del STD (`sched_out`) |
| Touchdown Zone | −7 pts | ≤1500 ft = 0 / ≤2500 ft = −3 / >2500 ft = −7 · requiere NavData API |
| Centreline Deviation | −7 pts | ≤10 ft = 0 / ≤30 ft = −3 / >30 ft = −7 · requiere NavData API |
| Localizer Alignment | −5 pts | ILS not tuned −3; heading >5° ×2 máx −2; cap −5 · requiere NavData API + ILS approach |
| Minimums Compliance | −5 pts | −5 si descenso bajo DA (threshold elevation + 200 ft) sin aterrizar |
| Procedure Speed | −10 pts | −3 pts por violación de restricción SID/STAR al pasar el fix; cap −10 |
| Engine Stabilization | −5 pts | −5 si algún motor en marcha no estaba estabilizado (aceite/N2) al entrar en pista |

**Single Engine Taxi bonus (+5 pts):** requiere ≥ 50 % del tiempo de rodaje con un solo motor. Elegibilidad por tipo de propulsión:

| Tipo | Regla |
|---|---|
| **Piston** | Nunca elegible |
| **Turboprop** (Q400, ATR, etc.) | Siempre elegible — no requiere lifecycle compliance |
| **Jet** / Unknown | Elegible solo si `EngineWarmupViolation = false` **y** `EngineCooldownViolation = false` |

Si la bonificación se deniega a un jet, `ScoringResult.SingleEngineTaxiDeniedReason` contiene la razón (`"warm-up insuficiente"`, `"cool-down insuficiente"` o `"warm-up + cool-down incumplidos"`), y `PirepBuilder.LogScore` la registra como Warning.

Los criterios **Touchdown Zone** y **Centreline Deviation** solo se evalúan si `TouchdownDistanceFt > 0`. Los criterios **Localizer Alignment** y **Minimums Compliance** solo se evalúan si se detectó un procedimiento ILS y la frecuencia NAV1 coincide con el ILS esperado a 1 000 ft AGL (±0.05 MHz).

**`ScoredEngineType` (enum en `Models/FlightScoreData.cs`):** `Jet` (default), `Turboprop`, `Piston`. Poblado en `BuildScoreData()` desde `LastRawData.EngineCategory`.

**Landing ratings:** Butter (≤150 fpm) · Smooth · Normal · Hard · Very Hard · Slam (>650 fpm)

---

### LandingLogService — `Services/LandingLogService.cs`

Gestiona la base de datos SQLite local `landing_log.sqlite`. Ruta configurada en `App.config` clave `landing_log_path`.

**Tablas:**

```sql
flights (
    id INTEGER PRIMARY KEY,
    flight_number, origin, destination, runway_name, metar_raw TEXT,
    flight_date TEXT,  -- ISO 8601 UTC
    landing_rate_fpm INTEGER,
    score INTEGER,
    g_force REAL,
    touchdown_dist_ft REAL,
    centerline_dev_ft REAL
)

approach_track (
    id INTEGER PRIMARY KEY,
    flight_id INTEGER,  -- FK → flights.id
    seq_no INTEGER,
    lat REAL, lon REAL,
    alt_ft REAL, agl_ft REAL,
    ias_kt REAL, vs_fpm REAL,
    heading_deg REAL,
    dist_nm REAL,       -- distancia al umbral (positiva = antes del umbral)
    lateral_ft REAL     -- desviación centreline (signed: + derecha, - izquierda)
)
```

**API pública:**

```csharp
bool IsAvailable
int  SaveFlight(FlightRecord record, IList<ApproachTrackPoint> track)
void DeleteFlight(int id)       // borra en transacción: approach_track primero, luego flights
List<FlightRecord>        GetFlights()
List<ApproachTrackPoint>  GetTrackPoints(int flightId)
bool HasFlights()
```

**Flujo de captura de aproximación:**

```
{Descent, Approach} tratado como un superestado único (v0.8.10) — el reset solo dispara
al ENTRAR al par desde afuera (ej. Climb → Descent), no en la transición interna
Descent → Approach, así lo ya resuelto durante Descent sobrevive esa transición:
    → _approachBuffer.Clear(); _approachThreshold = null; _approachDestination = null
         _lastApproachAirportQuery = DateTime.MinValue
OnRawDataUpdated (fase = Descent O Approach — extendido a Descent en v0.8.10), throttled
a 5 s, activo mientras AGL > 1000 ft:
    → Task.Run(ReconfirmApproachRunway(lat, lon, hdg))
         → NavDataService.FindApproachAirport(lat, lon, hdg, radiusNm=20, headingTolDeg=15)
              GET /nearest/approach-airport/ — desambigua pistas paralelas por score
              (dominado por cross_track_nm — validado NavData para SKBO 14L/14R, ~355 m)
         → si null (404 o sin match) → no hace nada, reintenta en 5 s
         → si icao == plan.Destination → ClearDivertedAirport() y sigue la rama de runway de abajo
         → si diverted (icao ≠ plan.Destination): un pre-filtro contra el propio plan [v0.9.9]
              y seis filtros independientes. Se rechaza en cuanto uno falla, con log único por
              aeródromo+motivo (LogRejectedMatchOnce → Lnm_DiversionRejected*).
              Pre-filtro — RouteCorridor.IsOnArrival(plan.Waypoints, lat, lon) [v0.9.9]: si el
                   avión está dentro de los últimos 40 NM de la traza del navlog de SimBrief
                   (corredor de 5 NM a cada lado), está donde su propio plan dice, y que el
                   matcher nombre otro aeródromo solo puede ser la llegada pasando cerca de él →
                   RevertDiversionIfAny() + return. Sin navlog (plan de phpVMS o sin OFP) no
                   opina. Medido con el OFP real del SKRG→SKBQ: el falso SKTL estaba a 25–33 NM
                   de esa traza, así que la regla no lo toca; actúa en el caso opuesto.
              1. lateral — !CrossTrackNm.HasValue || CrossTrackNm <= 3.0 NM  [v0.9.1]: un giro de
                   STAR puede alinear casualmente el heading con la pista de un aeródromo
                   cercano estando a >10 NM del eje (SKGY: heading_diff 3.1° pero
                   cross_track_nm 11.36 — el endpoint ya envía el campo, antes se descartaba)
              2. angular — IsWithinFinalCone: |cross| ≤ max(0.25 NM, dist_umbral·tan 4°). El
                   corte lateral fijo no separa los casos reales —el falso SKTL pasó por
                   0.007 NM— pero como ángulo están a un factor de cuatro (9.0° contra 2.1°)
              3. vertical — IsPlausibleDiversionDescent: AGL medido contra la elevación del
                   campo EMPAREJADO, dividido entre las NM que faltan, ≤ 700 ft/NM. Caza la
                   clase SKTL desde el primer sondeo (906–2 224 ft/NM y empeorando al acercarse);
                   los falsos de Boston tenían perfil normal (295–622) y pasan
              4. distancia — IsPlausibleDiversionDistance: el alterno tiene que estar más cerca
                   que el destino planeado. Detiene el 28M de Boston (15.1 NM) con Logan a
                   6.5 NM; KOWD (6.58 NM) estaba MÁS cerca que Logan (11.35) y solo lo para el
                   filtro 2 — cada regla cubre un caso distinto
              5. establecido en final — GetRunwayThreshold/SelectApproachThreshold: rumbo ±15°
                   (magnético contra magnético) + ≤2 NM del eje VERDADERO + along ≤ 0
              6. persistencia — DiversionConfirmPolls = 2 sondeos consecutivos nombrando el
                   mismo alterno: una sola muestra no dispara el OSD ni reorienta el destino
              (el último alterno que pasó el filtro 1 se recuerda en _lastAlternateCandidateIcao
              aunque no se actúe sobre él — alimenta TouchdownFallbacks)
         → al pasar todo, si aún no estaba marcado:
              _approachDestination = icao; SetEffectiveDestination(icao); SetDivertedAirport(icao)
              GetAirportElevationFt(icao) → si éxito: SetArrivalAirportElevation(elevFt)  [v0.9.0]
                   corrige ReferenceAirportElevation/CurrentAGL y BuildPhaseInput().DestinationElevation
                   (antes seguían usando la elevación del destino planeado incluso tras confirmar
                   el desvío — con diferencias grandes de elevación, el AGL calculado nunca bajaba
                   lo suficiente y el buffer de aproximación no capturaba puntos)
              log Lnm_DiversionDetected + OSD Critical
              (se marca ANTES de resolver el threshold completo — los gates de QNH/Localizer
              necesitan EffectiveDestination lo antes posible, no solo al filear)
         → si !runwayChanged:
              si !diverted → ClearDivertedAirport()  [v0.9.1] — revierte un desvío marcado por
                   un poll anterior si este poll ya vuelve a confirmar el destino planeado sin
                   que cambien ni el ICAO ni la pista localmente resueltos
              return
         → si runwayChanged (icao o runway.name distintos al ya resuelto):
              (el threshold ya viene resuelto del filtro 5 de arriba — newThreshold)
              si resuelto → _approachThreshold = nuevo; _approachDestination = icao
                          → ApproachBuffer.Clear() (puntos previos, umbral equivocado)
                          → Task.Run(LoadApproachData(icao, runway.name))
                               → GetIlsForRunway() + GetApproachType() + GetApproachFixes()
                               → _flightManager.SetApproachData(ils, approach, fixes)
                          → si no diverted y ya había resuelto antes:
                               ClearDivertedAirport()  [v0.9.1]
                               si había un desvío marcado → log Lnm_DiversionReverted
                               si no → log "↻ RUNWAY UPDATED"
    → si AGL < 3000 ft && ≥ 2 s desde último punto && _approachThreshold != null:
         ComputeApproachMetrics(threshold, lat, lon) → (distNm, lateralFt)
         _approachBuffer.Add(ApproachTrackPoint)
OnTouchdownDetectedEvent → LookupRunwayData(data)  [red de seguridad, v0.8.8]
    → FindTouchdownRunway(_approachDestination ?? plan.Destination, lat, lon, hdg)
    → si null → prueba en orden TouchdownFallbacks [v0.9.9]: plan.Origin → último alterno
         propuesto por el matcher (_lastAlternateCandidateIcao) → destino planeado, sin
         duplicados. Cubre tres huecos: que la capa de Descent/Approach no resolviera nada
         (aproximación muy corta, servicio caído), un desvío real sin final recta (circuito o
         final corta, que nunca pasa el filtro 5) y un _approachDestination equivocado, que
         antes dejaba el destino planeado sin probar nunca
         si encontrado →
             _approachDestination = airport; SetEffectiveDestination(airport); SetDivertedAirport(airport)
             GetAirportElevationFt(airport) → si éxito: SetArrivalAirportElevation(elevFt)  [v0.9.0]
             → log Lnm_ArrivalAirportMismatch + OSD Critical
    → CheckFlownDistance(plannedDest)  ← si distancia volada <60% de la planeada, loguea
         Lnm_DistanceMismatch para revisión manual, independientemente de si hubo match de pista

    [v0.8.10] El fallback adicional a GetNearestAirport (phpVMS /api/airports/nearest) se
    RETIRÓ — confirmado roto en producción (404 "No query results for model
    [App\Models\Airport] NEAREST", esa ruta no existe). Fallaba siempre en silencio
    (catch{} vacío).
    [v0.9.8] Ese método y su único llamador (FlightManager.DetectNearestAirport, que a su
    vez no tenía ningún llamador) se ELIMINARON por completo. Si algún día se confirma la
    ruta correcta de phpVMS, hay que reimplementarlo desde cero.

SendPirep()
    → SnapshotLandingRecord()          ← captura plan + touchdown ANTES de FilePirep
    → FilePirep()
        → arrivalIcao = _effectiveDestination ?? plan.Destination  (calculado una vez,
             reusado por la reconciliación de QNH y por la corrección del PIREP debajo)
        → await FinalizeArrivalQnhAsync(arrivalIcao, AircraftQnhMb)  [v0.8.10, ver bloque
             QNH provisional abajo] — ANTES de BuildScoreData()/ComputeScore()
        → BuildScoreData() / ComputeScore()
        → si _effectiveDestination != plan.Destination:
             UpdatePirep(id, { arr_airport_id: _effectiveDestination })  [v0.8.8]
             log Log_ArrivalAirportCorrected
             (no se llama a ningún método de reubicación de piloto: se intentó con
              MovePilotAsync (PUT api/user) y se confirmó roto en producción — 405 "PUT
              method not supported for route api/user". [v0.9.8] El método se eliminó de
              ApiService/IApiService. Confirmado en vuelo real que phpVMS reubica
              curr_airport por su cuenta al procesar diversion-airport en el payload de
              /file más abajo)
        → BuildPayload() — Dictionary<string,object>, no objeto anónimo, para poder omitir
             la clave por completo cuando no aplica (v0.8.9):
             incluye arr_airport_id siempre (refuerzo del UpdatePirep previo)
             incluye diversion-airport SOLO si _divertedAirport != null — phpVMS solo procesa
                  una diversión si el pirep INCLUYE esa clave; un valor null explícito no basta
        → ResetFlightState() ← borra _activePlan, touchdown data, _divertedAirport
    → éxito → SaveLandingRecord(record)
        → record.Score = LastFlightScore  ← no se resetea en ResetFlightState
        → LandingLogService.SaveFlight(record, _approachBuffer)
        → _approachBuffer.Clear()
        → log de diagnóstico (éxito o motivo de fallo) (v0.4.9)
```

> `SnapshotLandingRecord()` debe ejecutarse **antes** de awaitar `FilePirep()`. `LastFlightScore`
> es la única propiedad que `ResetFlightState()` no borra, por lo que puede leerse después.

> **v0.8.8 — corrección de `arr_airport_id`:** validado contra producción (vholar.co) que
> `PUT /api/pireps/{id}` con `arr_airport_id` es aceptado por phpVMS mientras el PIREP está
> `state=0` (`in_progress`) — exactamente el momento en que `FilePirep()` lo llama, antes de
> transicionar a `Accepted` vía `/file`. Un PIREP ya `Accepted` (`state=2`) rechaza el mismo
> `PUT` con `503 "This action is unauthorized"` — no afecta el flujo normal.

> **v0.8.10 — por qué `FlightPhase.Approach` puede no alcanzarse nunca:**
> `FlightManager.Telemetry.cs` hardcodea `DistanceToDestinationNm = -1` siempre en
> `BuildPhaseInput()`, dejando muerta la rama de distancia en la transición
> `Descent → Approach` de `FlightPhaseStateMachine`. Solo puede disparar la rama de
> altitud (`altAboveDest = altitud − elevación del DESTINO PLANEADO < aglThr`). Si el
> aterrizaje real es en un aeropuerto con elevación muy distinta a la planeada (caso
> real: SKCL 3162 ft planeado vs SKBO 8361 ft real), `altAboveDest` puede no bajar del
> umbral ni en el touchdown — la fase interna `Approach` nunca se alcanza (el log de
> status salta de `APR` directo a `LDG`, sin pasar por `FIN`). Por esto la
> re-confirmación de `ReconfirmApproachRunway` se extendió a `FlightPhase.Descent`.
> Efecto colateral conocido, no corregido: `CheckStabilizedApproachGate`/
> `CheckApproachBelowGate` solo corren `if (CurrentPhase == FlightPhase.Approach)` —
> el criterio Stabilized Approach completo (hasta 15 pts) no se evalúa en este
> escenario (omisión neutra).

## Aeropuerto de llegada distinto al planeado — v0.8.8–v0.9.9

Corrige el caso donde el avión aterriza en un aeropuerto distinto al destino planeado (emergencia, regreso a origen, desvío real) sin que el PIREP quede registrado con el `arr_airport_id`/`diversion-airport` correctos en phpVMS, y sin penalizar QNH/Localizer/Minimums contra el METAR de un destino nunca alcanzado.

**Mecanismo principal — `NavDataService.FindApproachAirport` (`GET /nearest/approach-airport/`):** dado lat/lon/heading, NavData devuelve el aeropuerto+pista más alineados dentro de `radius_nm` (20) y `heading_tol` (15°), con desambiguación de **pistas paralelas** por `score` (dominado por `cross_track_nm`, la desviación perpendicular al eje extendido de cada pista — validado por NavData para SKBO 14L/14R, ~355 m de separación). Se confía directamente en `icao`/`runway.name`/`score`, sin reimplementar el desempate localmente.

**Re-confirmación continua durante Descent + Approach** (`TelemetryCoordinator.ProcessRawData` + `ReconfirmApproachRunway`, throttled a 5 s, activo mientras AGL > 1000 ft — **extendido a `FlightPhase.Descent` en v0.8.10**, antes solo corría en `Approach`): a diferencia de una resolución única, sigue consultando el endpoint durante todo el descenso/aproximación — necesario tanto por pistas paralelas con fix inicial compartido (ambigüedad geométrica que solo se resuelve al divergir hacia el curso final) como por desvíos severos donde la fase interna `Approach` puede no alcanzarse nunca (ver más abajo). Si el ICAO/pista resuelto cambia respecto al ya confirmado:
- **Diversión** (ICAO ≠ destino planeado): se marca **solo cuando el avión está establecido en una final** para esa pista, tras pasar seis filtros (lateral ≤3 NM, cono angular de 4°, gradiente de descenso ≤700 ft/NM, alterno más cerca que el destino, `SelectApproachThreshold` con `along ≤ 0` y persistencia de 2 sondeos — v0.9.9, ver la subsección dedicada) vía `FlightManager.SetEffectiveDestination` + `SetDivertedAirport`, log `Lnm_DiversionDetected`, OSD Critical. Sigue ocurriendo **antes** de los gates de QNH (TL−1000 ft y 1000 ft AGL en `ApproachValidator.cs`, que usan `EffectiveDestination ?? DestIcao`) — clave para que el METAR/ILS correcto se use en tiempo real, no solo al filear.
- **Corrección de pista paralela sin diversión** (mismo ICAO, pista distinta): re-resuelve `_approachThreshold` vía `GetRunwayThreshold` (geometría más estricta, necesaria para el buffer de aproximación/`ComputeApproachMetrics`), limpia `ApproachBuffer` (los puntos previos se calcularon contra el umbral equivocado) y relanza `LoadApproachData` para recargar el ILS/approach de la pista correcta.

`TelemetryCoordinator.OnPhaseChanged` trata `{Descent, Approach}` como un superestado único: el reset de `_approachThreshold`/`_approachDestination` solo ocurre al **entrar** al par desde afuera (ej. `Climb → Descent`), no en la transición interna `Descent → Approach` — así lo ya resuelto durante Descent sobrevive si la máquina de fases sí llega a `Approach` más tarde.

**Por qué la fase `Approach` puede no alcanzarse nunca (v0.8.10):** `FlightManager.Telemetry.cs` hardcodea `DistanceToDestinationNm = -1` siempre, dejando muerta la rama de distancia en `FlightPhaseStateMachine`'s transición `Descent → Approach`; solo puede disparar la rama de altitud (`altAboveDest = altitud − elevación del DESTINO PLANEADO < aglThr`). Si el aterrizaje real ocurre en un aeropuerto con elevación muy distinta a la planeada (caso real: SKCL 3162 ft planeado vs SKBO 8361 ft real, ~5200 ft de diferencia), `altAboveDest` puede no bajar del umbral — la fase interna `Approach` puede no alcanzarse nunca, el log de status salta de `APR` (código de `Descent`) directo a `LDG` sin pasar por `FIN`. La re-confirmación en `Descent` (párrafo anterior) es lo que permite detectar el desvío de todas formas.

**Elevación de referencia (AGL) corregida tras confirmar un desvío (v0.9.0):** `FlightManager.ReferenceAirportElevation`/`CurrentAGL`, `BuildPhaseInput().DestinationElevation` (el mismo campo que alimenta `altAboveDest` arriba) y `TelemetryCoordinator.PrepareTelemetry`'s `altitude_agl` (enviado a phpVMS) usaban **siempre** `_activePlan.DestinationElevation` — la elevación del destino planeado — incluso después de confirmar un desvío. Con SKCL/SKBO (~5200 ft de diferencia), el AGL calculado nunca bajaba de 3000 ft ni en touchdown real, así que el buffer de aproximación nunca capturaba puntos (`⚠️ Landing log no grabado: solo 0 puntos en buffer`, confirmado en vuelo real) y el gate de Stabilized Approach tampoco disparaba. Fix: `FlightManager.SetArrivalAirportElevation(double)`/`ArrivalAirportElevationFt` (nuevo campo `_arrivalAirportElevation`), seteado en los mismos dos call sites de `ReconfirmApproachRunway`/`LookupRunwayData` vía `NavDataService.GetAirportElevationFt(icao)` (nuevo, usa `NavDataClient.GetAirportInfo(icao)?.ElevationFt`). `ReferenceAirportElevation` y `BuildPhaseInput()` ahora hacen `_arrivalAirportElevation ?? <elevación planeada>` (v0.9.5: el campo se guarda como patrón de bits en un `long` y se accede con `Interlocked`, con `double.NaN` como "sin dato" — C# no permite `volatile` sobre `double`, y el campo se publica desde `Task.Run` mientras el hilo de polling lo lee) — esto también resuelve de rebote la transición `Descent → Approach` para desvíos con diferencia de elevación (una vez `altAboveDest` usa la elevación real), por lo que el criterio Stabilized Approach (hasta 15 pts) ahora sí puede evaluarse en un desvío detectado a tiempo.

**Red de seguridad en touchdown** (`LookupRunwayData`): si el aeropuerto que resolvió la fase de aproximación no matchea la huella de pista del touchdown, prueba una lista ordenada y sin duplicados — **aeropuerto resuelto → origen → último alterno propuesto por el matcher → destino planeado** (v0.9.9; antes solo resuelto → origen, así que un `_approachDestination` equivocado dejaba el destino planeado sin probar nunca). El fallback adicional a `IApiService.GetNearestAirport` (phpVMS `/api/airports/nearest`) se retiró en v0.8.10 y **[v0.9.8] el método se eliminó por completo** — estaba roto en producción (`404 "No query results for model [App\Models\Airport] NEAREST"`, esa ruta no existe en esta instalación de phpVMS) y su único llamador (`FlightManager.DetectNearestAirport`) no tenía a su vez ningún llamador. También evalúa `CheckFlownDistance`: si la distancia volada es <60% de la planeada, loguea aviso de revisión (`Lnm_DistanceMismatch`) **independientemente** de si hubo match de pista.

**Corrección del PIREP y del piloto** — `FlightManager.Lifecycle.cs` (`FilePirep`): si `_effectiveDestination` difiere del destino planeado, antes de filear:
1. `_apiService.UpdatePirep(id, new { arr_airport_id = ... })` (mecanismo PUT ya usado para `block_off_time`/status) → log `Log_ArrivalAirportCorrected`.
2. `PirepBuilder.BuildPayload` incluye `diversion-airport` (solo si `_divertedAirport != null` — el payload se construye como `Dictionary<string,object>`, no objeto anónimo, para que la clave quede **ausente** y no `null` cuando no hay desvío; phpVMS solo procesa una diversión si el pirep **incluye** ese campo) y `arr_airport_id` como refuerzo del `UpdatePirep` previo.

La reubicación del piloto **no la hace el cliente**: se intentó con `MovePilotAsync` (`PUT /api/user { curr_airport_id }`) y se confirmó roto en producción (`405 "The PUT method is not supported for route api/user"`). [v0.9.8] El método se eliminó junto con `GetNearestAirport` (404 confirmado) y `FlightManager.DetectNearestAirport` (sin llamadores). Confirmado en un vuelo real desviado que phpVMS reubica al piloto por su cuenta al procesar `diversion-airport` en el payload de `/file`.

vmsOpenAcars es responsable de garantizar `arr_airport_id` — la reubicación de `curr_airport` del piloto queda a cargo de phpVMS vía `diversion-airport`.

**Validado contra producción (vholar.co):** el `PUT arr_airport_id` es aceptado mientras el PIREP está `state=0/in_progress` (justo cuando se llama, antes de `/file`); un PIREP ya `Accepted` (`state=2`) lo rechaza con 503 — irrelevante para el flujo normal.

### QNH de llegada: provisional durante el vuelo, confirmado/revertido al filear (v0.8.10)

El check de QNH de llegada (`ApproachValidator`, gate TL−1000 ft o fallback a 1000 ft AGL) usa `EffectiveDestination ?? DestIcao` — si dispara antes de que el desvío se detecte (posible incluso con la re-confirmación en Descent, ej. a mucha altitud/distancia todavía), penalizaba contra el destino planeado equivocado **de forma permanente e irreversible** (caso real: avión a 1028 hPa comparado contra SKCL a 1011 hPa mientras en realidad se aproximaba a SKBO). Rediseñado estilo "comisarios de F1": se advierte en tiempo real, pero el veredicto que puntúa se decide una sola vez, al filear, con la mejor información disponible:

- `ApproachValidator.CheckArrivalQnhProvisionalAsync(icao, aircraftQnhMb)` — reemplaza a `CheckQnhAsync` en los dos call sites de llegada (TL−1000 ft y fallback 1000 ft AGL). Loguea/OSD en tiempo real (`Log_QnhPenaltyProvisional`) pero **no toca `QnhViolations`** — guarda el veredicto en `_provisionalArrivalQnhViolation` (tri-state: null/true/false).
- `ApproachValidator.FinalizeArrivalQnhAsync(finalDestIcao, currentAircraftQnhMb)` — único punto que suma a `QnhViolations` para el componente de llegada. Llamado una sola vez, **awaited**, desde `FlightManager.Lifecycle.FilePirep()` **antes** de `BuildScoreData()`/`ComputeScore()`, usando el destino final (`_effectiveDestination ?? plan.Destination`) y el QNH **actual** del avión (no el capturado en el check temprano — el piloto pudo haber corregido el altímetro después). Si no hay METAR definitivo, nunca puntúa en ningún sentido (ni penaliza ni revierte silenciosamente lo ya marcado).
- Los checks de salida (vs METAR de origen) y de clima (vs STD 1013) **no cambiaron** — nunca son ambiguos, siguen siendo inmediatos y definitivos vía `CheckQnhAsync`/`CheckStdPressure`.
- Claves nuevas: `Log_QnhPenaltyProvisional`, `Log_QnhFinalPenalty`, `Log_QnhPenaltyReversed`, `Log_QnhFinalIndeterminate`.

**Nuevos tipos/métodos (v0.8.8–v0.8.9):** `Models/NavData.cs` → `NavApproachAirportResponse`/`NavApproachAirportRunway`; `Db/RunwayService.cs` → `NearestApproachAirportResult`; `NavDataClient.GetNearestApproachAirportAsync`; `NavDataService`/`INavDataService.FindApproachAirport`; `FlightManager.SetDivertedAirport`/`DivertedAirport`.

**Claves de log nuevas (v0.8.8–v0.8.9):** `Lnm_ArrivalAirportMismatch`, `Lnm_DiversionDetected`, `Lnm_DistanceMismatch`, `Log_ArrivalAirportCorrected`, `Log_ErrorArrivalAirportUpdate`, `Log_ErrorPilotPositionUpdate` (`Languages/en.json` + `es.json`).

### Falsos positivos de desvío durante giros de STAR (v0.9.1)

Vuelo real (`9QJYgErnggMdgKPO`, SKLT→SKBO) reveló que `ReconfirmApproachRunway` podía marcar como "desvío" un aeropuerto que el avión solo cruzaba de pasada durante un giro de STAR, a decenas de NM de distancia — y que ese falso positivo **nunca se revertía** aunque el tracking local volviera a confirmar el destino planeado segundos después, sobreviviendo incluso un touch-and-go y una segunda aproximación completa. Confirmado en vivo contra NavData con las coordenadas exactas del log: `GET /nearest/approach-airport/` matcheó SKGY con `heading_diff_deg: 3.1` (coincidencia casual de rumbo durante el giro) pero `cross_track_nm: 11.36` — el avión estaba a más de 11 NM del eje extendido de esa pista, nada parecido a una aproximación real.

Dos causas independientes, ambas corregidas:

1. **Sin filtro de plausibilidad geométrica** — `NearestApproachAirportResult` no exponía `cross_track_nm` (el endpoint ya lo devuelve, se descartaba). Ahora se mapea (`NavDataService.FindApproachAirport`) y `ReconfirmApproachRunway` exige `cross_track_nm ≤ 3.0` (o ausente) antes de aceptar un match como desvío genuino — la rama `GetRunwayThreshold` más abajo, que resuelve el runway real, no cambia (ya tenía su propia tolerancia estricta de 2 NM).
2. **Sin reconciliación** — `SetEffectiveDestination`/`SetDivertedAirport`/`SetArrivalAirportElevation` solo se reseteaban en `ResetFlightState()`/`ResumeFlight()` (inicio/fin de vuelo). Nuevo `FlightManager.ClearDivertedAirport()`, llamado desde `ReconfirmApproachRunway` en cuanto un poll no-desviado reconfirma el destino planeado (tanto si `runwayChanged` como si no) — revierte los tres campos a `null`, que `ApproachValidator`/`FilePirep()` ya tratan como "usar el destino planeado" (`EffectiveDestination ?? DestIcao`). Log `Lnm_DiversionReverted` cuando había un desvío marcado que se revierte.

Fuera de alcance (evaluado y descartado): filtro de tamaño de pista (`length_ft`/`width_ft`, propuesto por el usuario con SKMA/SKGY como evidencia — ni el endpoint `/nearest/approach-airport/` los expone ni hacía falta, el filtro de `cross_track_nm` ya cubre el caso real con datos que el endpoint ya envía) y conciencia de fixpoints de STAR (mismo motivo).

**Claves de log nuevas:** `Lnm_DiversionReverted` (`Languages/en.json` + `es.json`).

### Falso desvío por alineación casual con un aeródromo de la derrota (v0.9.9)

Reportado en vuelo (PIREP `E7DK47e88XdabzoL`, SKRG→SKBQ del 20/09/2026 con desvío simulado a
SKCG): a las 15:12:47, descendiendo a ~17 000 ft en lat 9.22349, lon -75.43016, rumbo 348, el
cliente anunció `DIVERTING TO SKTL`. **SKTL comparte la alineación costera de la llegada** —su
rwy 35 va a 348.9° magnética y el avión volaba a 348, `heading_diff_deg` **0.9°**— así que el
endpoint lo devolvió durante **siete minutos** de descenso.

El filtro de 3 NM de v0.9.1 lo dejó pasar por **0.007 NM** (`cross_track_nm` 2.993) durante **un
solo sondeo**; 5 s después ya era 3.06 NM. Clave del diagnóstico: el match falso y el real están
**ambos a ~19 NM del umbral** (19.13 vs 19.04), así que la distancia no discrimina — lo que los
separa es el **ángulo** (9.0° contra 2.1°).

`ReconfirmApproachRunway` exige ahora **seis** filtros geométricos independientes, más una
comprobación contra el propio plan de vuelo:

1. **Lateral** — `cross_track_nm ≤ MaxCrossTrackNm` (3 NM), como en v0.9.1.
2. **Angular** — `NavDataService.IsWithinFinalCone`: `|cross| ≤ max(FinalConeFloorNm 0.25 NM,
   dist_umbral · tan(MaxFinalConeAngleDeg 4°))`. El corte lateral fijo no puede separar los
   casos reales; como cono están a un factor de cuatro. El suelo evita que el cono colapse a cero
   cerca del umbral, donde el ángulo degenera.
3. **Vertical** — `NavDataService.IsPlausibleDiversionDescent`: el AGL sobre el aeródromo
   emparejado dividido entre las NM que faltan no puede pasar de `MaxDiversionGradientFtPerNm`
   (700 ft/NM ≈ 6.6°, justo por encima de la aproximación publicada más empinada del mundo; una
   senda estándar son 318 ft/NM). Caza por sí solo la clase SKTL desde el primer sondeo —su serie
   corrió a 906–2 224 ft/NM y **empeoraba** al acercarse— y **no** caza los falsos de Boston,
   cuyo perfil era normal (2.8°–5.8°). Se mide contra la elevación del aeródromo **emparejado**,
   no la del destino planeado.
4. **El alterno más cerca que el destino** — `IsPlausibleDiversionDistance`: no se desvía uno a
   un aeropuerto que está más lejos que aquel al que ya iba.
5. **Establecido en final** — `SelectApproachThreshold` (`GetRunwayThreshold`, con `along ≤ 0`
   además de ≤2 NM laterales y rumbo dentro de 15°).
6. **Persistencia** — `DiversionConfirmPolls` (2) sondeos consecutivos nombrando el mismo
   alterno, para que una sola muestra no dispare el OSD ni reoriente el destino efectivo.

### La llegada del propio plan: "¿estoy donde debería?" (v0.9.9)

Antes de todo lo anterior, `ReconfirmApproachRunway` comprueba si el avión está dentro del
corredor de la llegada que él mismo presentó: **`Helpers/RouteCorridor.IsOnArrival`**, 5 NM a cada
lado de los últimos 40 NM de la traza del navlog de SimBrief (`SimbriefPlan.Waypoints`, que
`SimbriefEnhancedService` llena con las coordenadas de cada fix). Si está dentro, un match que
nombre otro aeródromo no es un desvío —es la llegada pasando cerca de él— y se revierte cualquier
bandera. Es la comprobación más directa de todas y la única que usa lo que el piloto planificó.

**Validación con datos reales:** el último OFP del usuario en SimBrief (`simbrief_user` en
`App.config`) seguía siendo el del vuelo SKRG→SKBQ, así que se descargó entero. Los puntos de su
llegada planificada dan 0 NM y el avión estuvo a **25–33 NM** de esa traza durante todo el
descenso del falso SKTL (se había ido hacia SKCG): la regla **no** toca ese caso. Actúa en el
opuesto, que es donde ocurrieron los tres falsos de KBOS. **De ese caso no se pudo verificar el
beneficio**: ese vuelo no tiene PIREP en la API (comprobados los 20; ninguno con KBOS), así que no
hay ruta ni OFP que medir. Es la única pieza del arreglo sin validar contra vuelo real.

El corredor se define por distancia, no por las banderas `IsSidStar`/`Stage`: en ese OFP real el
fix de transición **LOLUD** llegó con `is_sid_star = 0` pese a pertenecer a la llegada, así que
filtrar por bandera dejaba el corredor en un tramo de 11 NM. Un desvío real empieza saliéndose de
la llegada, así que el coste es retrasar la detección lo que se tarde en abandonar 5 NM. Sin
navlog (plan desde phpVMS, o sin OFP) la regla no opina.

`GeoMath.DistanceToSegmentNm` es nueva: el código solo tenía proyección sobre la recta infinita y
medir desviación de traza necesita la recta **recortada** a sus extremos.

### Tres falsos desvíos en la aproximación a KBOS (v0.9.9, segundo caso real)

PIREP del 22-23/09/2026 (SKCG→KBOS, A320, **v0.9.2**): durante el descenso y el viraje de encaje
a la final de Logan el cliente anunció `DIVERTING TO 28M` (03:44:33), `TO 1B9` (03:45:38) y
`TO KOWD` (03:46:09), y revirtió a KBOS a las 03:47:49 — dejando además la captura de
aproximación apuntando a la pista 28 de Norwood mientras el avión estaba en Logan
(`INICIO CAPTURA APROX: PISTA 28 | Dist 3,4 NM`, 03:47:16).

Mecanismo **distinto** al de SKTL: aquí el destino real estaba **dentro del radio de 20 NM** del
matcher (a 6.5 NM en el primer evento), pero durante el viraje no tenía ninguna pista a menos de
15° del rumbo, así que el endpoint devolvía el aeródromo pequeño mejor alineado. Los tres
`cross_track_nm` —1.244, 2.993 y 1.203 NM— pasaban el corte de 3 NM de v0.9.1 (el de 1B9 por
0.007 NM, igual que SKTL). Es el caso que justifica el **filtro 4**: el "alterno" 28M estaba a
15.1 NM mientras el avión tenía Logan a 6.5 NM, y esa regla lo detiene con un margen de 2.3×
donde el cono sólo lo detenía por 0.7°. KOWD, en cambio, estaba a 6.58 NM —**más cerca** que
Logan a 11.35 NM— así que sólo el cono lo detiene: las dos reglas cubren cosas distintas.

Replay de la aproximación real (14 posiciones, 03:44–03:50) contra el endpoint en vivo con la
puerta nueva: **cero falsos desvíos**, y KBOS 04R resuelta desde 9.45 NM — 33 s antes de la
reversión observada, de modo que la captura de aproximación habría sido la correcta.

Replay equivalente del primer caso (22 posiciones, 15:11–15:24): SKTL **rechazado en los 13
sondeos** en que el endpoint lo devolvió, y SKCG **aceptado** desde 17.4 NM con 0.58 NM del eje.

**Claves de log nuevas:** `Lnm_DiversionRejectedCrossTrack`, `Lnm_DiversionRejectedCone`,
`Lnm_DiversionRejectedFarther`, `Lnm_DiversionRejectedNotOnFinal`.

**El eje de proyección debe ser el verdadero, no el magnético (v0.9.9).** `SelectApproachThreshold`
proyectaba con `rwy.Heading` (magnético) mientras `ProjectOnRunway` ya usaba `TrueRunwayBearing`.
La variación local (8.4°E aquí) rota el eje y produce `distancia × sin(variación)` de error: 2.8 NM
a 19 NM. Con ese error el desvío **real** a SKCG (0.70 NM del eje según el endpoint, 0.703) se
calculaba como 3.51 NM y superaba la tolerancia de 2 NM — la primera versión del fix habría
**dejado pasar el desvío real mientras rechazaba el falso**, por el motivo equivocado. La
comparación de rumbo sigue siendo magnética-contra-magnética (ambos valores vienen de referencias
magnéticas y la variación se cancela); solo la proyección usa el bearing verdadero. Y
`ThresholdHeading` devuelve el verdadero porque alimenta `ComputeApproachMetrics`, que proyecta
con él (hasta ~600 ft de error lateral en el landing log con variación ≥ 13°).

Marcar el desvío algo más tarde (al establecerse en final, no al primer match) no degrada nada:
desde v0.8.10 el QNH de llegada es provisional y se decide al filear, y los gates de QNH/ILS
disparan a TL−1000 ft / 1000 ft AGL, después de esa final. Los rechazos se registran **una sola
vez** por aeródromo y motivo (`Lnm_DiversionRejectedCrossTrack`, `Lnm_DiversionRejectedCone`,
`Lnm_DiversionRejectedFarther`, `Lnm_DiversionRejectedNotOnFinal`) — el endpoint devuelve el
mismo aeródromo en cada sondeo de 5 s durante todo el descenso.

---


### QNH de llegada — provisional durante el vuelo, confirmado/revertido al filear (v0.8.10)

El check de QNH de llegada (`ApproachValidator`, gate TL−1000 ft o fallback 1000 ft AGL)
puede disparar antes de que `EffectiveDestination` se resuelva (a mucha distancia/
altitud todavía), penalizando contra el destino planeado — potencialmente equivocado —
de forma antes irreversible. Rediseño estilo "comisarios de F1":

```
CheckViolations() / CheckStabilizedApproachGate()  (gates TL−1000 ft / 1000 ft AGL)
    → CheckArrivalQnhProvisionalAsync(destIcao, ctx.QnhMb)
         destIcao = EffectiveDestination ?? DestIcao  ← puede seguir siendo el planeado
         log/OSD en tiempo real (Log_QnhPenaltyProvisional si Δ>2 hPa)
         → NO toca QnhViolations — guarda _provisionalArrivalQnhViolation (null/true/false)

FilePirep()  (antes de BuildScoreData())
    → await FinalizeArrivalQnhAsync(arrivalIcao, AircraftQnhMb)
         arrivalIcao = destino FINAL conocido; AircraftQnhMb = QNH ACTUAL (no el
         capturado en el check temprano — el piloto pudo haber corregido después)
         → re-consulta METAR fresco contra arrivalIcao
         → si Δ>2 hPa: QnhViolations++ ; log Log_QnhFinalPenalty
         → si Δ≤2 hPa y había flag provisional: log Log_QnhPenaltyReversed (sin sumar)
         → sin METAR definitivo: no puntúa en ningún sentido; descarta el flag
           provisional con Log_QnhFinalIndeterminate en vez de mantenerlo silenciosamente
```

Los checks de salida (vs METAR de origen, `CheckQnhAsync`) y de clima (vs STD 1013,
`CheckStdPressure`) **no cambiaron** — nunca son ambiguos, siguen siendo inmediatos y
definitivos. El de STD sí cambió de contabilidad (v0.9.3): escribe `StdPressureViolation`
en lugar de incrementar `QnhViolations`, porque compartir contador con el QNH de
salida/llegada permitía que un STD incorrecto agotara el tope de −10 pts y enmascarara la
penalización del QNH de llegada.

---

### LandingAnalysisForm — `UI/Forms/LandingAnalysisForm.cs`

Ventana no-modal que muestra 4 gráficos de la trayectoria de aproximación. Soporta modo individual y modo comparación (2-5 vuelos).

**Constructor:** `LandingAnalysisForm(IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> flights)`

**Gráficos:**

| Gráfico | Y | Referencia |
|---|---|---|
| Vertical Profile | AGL (ft) | Línea 3° = dist_nm × 319 ft/NM |
| Lateral Deviation | Desviación (ft, signed ±) | Línea cero = eje de pista |
| IAS | Velocidad indicada (kt) | Línea promedio ≈ Vref |
| VS | Vertical speed (fpm) | Línea cero |

- Eje X invertido: 5 NM izquierda → 0 (umbral) derecha
- Suavizado Gaussiano (window=7, σ=window/4) aplicado a Lateral, IAS, VS — NO a Vertical Profile
- En comparación, cada vuelo usa un color de la paleta `TrackColors[]` (azul, naranja, verde, violeta, dorado)
- Nombres de series: `"{FlightNumber} #{i+1}"` en comparación; `"Actual"` en modo individual

---

### OsdOverlayForm — `UI/Forms/OsdOverlayForm.cs`

Ventana de notificaciones en pantalla (OSD — On-Screen Display). TopMost, sin borde, sin entrada en la barra de tareas, completamente click-through (`WM_NCHITTEST → HTTRANSPARENT`).

**Enum de severidad:**

```csharp
public enum OsdSeverity { Info, Success, Warning, Critical }
```

**Colores de texto por severidad:**

| Severidad | Color |
|---|---|
| Info | Azul claro `#A0DCFF` |
| Success | Lima `#64FF82` |
| Warning | Dorado `#FFD700` |
| Critical | Rojo `#FF6E6E` |

**Estados de animación:**

```
Idle → FadeIn (Opacity 0→target, paso 0.06/tick) → Hold (_holdTicks) → FadeOut (paso −0.04/tick) → Idle+Hide
```

Para **Critical**: en lugar de FadeIn, activa el `_flashTimer` (220 ms) que alterna `BgFlashOn`/`BgFlashOff` durante 3 ciclos completos, luego transiciona a Hold → FadeOut.

**API pública:**

```csharp
void ShowMessage(string text, OsdSeverity severity, int durationMs = 4000)
void HideOsd()
```

`ShowMessage()` es thread-safe (usa `InvokeRequired`). Recalcula la posición en pantalla en cada llamada usando `Screen.Bounds` (área completa, incluyendo zona de taskbar) para funcionar correctamente tanto en modo ventana como en fullscreen.

**Posicionamiento:** centrado horizontalmente, 40 px desde el borde superior de la pantalla configurada (`osd_screen_index`). Si el índice está fuera de rango, usa la pantalla primaria.

**Puntos de disparo en MainViewModel:**

| Evento | Mensaje | Severidad |
|---|---|---|
| `StartFlight()` confirmado | `ACARS ACTIVE` | Success |
| `OnFlightPhaseChanged(TaxiOut)` | `TAXI OUT` | Info |
| `OnFlightPhaseChanged(TakeoffRoll)` | `TAKEOFF ROLL` | Info |
| `OnFlightPhaseChanged(Enroute)` | `CRUISE` | Info |
| `OnFlightPhaseChanged(Descent)` | `DESCENDING` | Info |
| `OnFlightPhaseChanged(Approach)` | `APPROACH` | Info |
| `OnFlightPhaseChanged(OnBlock)` | `ON BLOCK` | Info |
| Touchdown detectado | `<calificación>  −XXX fpm  X.Xg` | varía por fpm |
| Touch-and-go | `TOUCH AND GO` | Warning |
| `SendPirep()` exitoso | `PIREP FILED — SCORE: XX/100` | Success |
| Airspace predictivo (heading hacia espacio restringido) | `AIRSPACE AHEAD  {TYPE}  {ICAO}` | Warning |
| Sobrevuelo de espacio restringido (encima del límite superior) | `ABOVE  {ICAO}  DO NOT DESCEND` | Warning |

**Integración en MainForm:**

```csharp
_viewModel.OnOsdMessage += (text, severity) =>
    _osd.ShowMessage(text, severity, AppConfig.OsdDurationMs);
```

---

### MapForm — `UI/Forms/MapForm.cs` (v0.7.0)

Ventana no-modal con mapa en movimiento basado en **GMap.NET 17.2.0**. Se abre con el botón MAP y se mantiene sincronizada con la posición del simulador.

**Actualización de posición:** evento `OnMapPositionUpdate(lat, lon, heading)` disparado cada 5 ciclos de `RawDataUpdated` (~250 ms) desde `MainViewModel`. Thread-safe vía `BeginInvoke`.

**Overlays:**

| Overlay | Contenido |
|---|---|
| `_routeOverlay` | Polilínea de ruta suavizada (Bézier) |
| `_waypointOverlay` | Marcadores de waypoints (fixes, SID/STAR, navaids) |
| `_ambientOverlay` | Waypoints ambient de origen/destino (zoom ≥ 10) |
| `_approachOverlay` | Legs de aproximación + extended centerline + missed approach |
| `_airspaceOverlay` | Polígonos GeoJSON de espacios aéreos |
| `_atcOverlay` | Formas ATC IVAO (círculo/estrella) + label markers + text-box área |
| `_aircraftOverlay` | Marcador de aeronave |

**Capas toggleables (v0.6.7):** cuatro `CheckBox` en la barra inferior con `DockStyle.Right`:
- **TILES**: conmuta entre el proveedor activo y `EmptyProvider.Instance`.
- **ROUTE**: `_routeOverlay.IsVisibile` + `_waypointOverlay.IsVisibile` + `_ambientOverlay.IsVisibile` + `_approachOverlay.IsVisibile`.
- **SPACES**: `_airspaceOverlay.IsVisibile`.
- **IVAO**: `_atcOverlay.IsVisibile`.

> Nota: `GMapOverlay.IsVisibile` — así deletreado en GMap.NET (no `IsVisible`).

**Marcador de aeronave (`AircraftMarker`, v0.6.7):** dibuja siluetas diferentes según `FsuipcService.AircraftCategory`. Tamaño 32×32 px, centrado en la posición. `public FsuipcService.AircraftCategory Category { get; set; }` — actualizado desde `MainForm.SetAircraftCategory()` al abrir el mapa y al cambiar el OFP. `GetShape(cat)` devuelve `PointF[]` por categoría; la rotación por heading se aplica con `g.RotateTransform()`.

**ATC IVAO (v0.6.7) — `SetAtcStations(IList<IvaoAtcStation>)`:**

Posiciones locales (TWR/GND/DEL) agrupadas por ICAO. Para cada grupo:
1. `MakeCirclePolygon` si hay TWR (primer polígono — capa inferior).
2. `MakeStarPolygon(startDeg=0)` si hay GND (N/S/E/W).
3. `MakeStarPolygon(startDeg=45)` si hay DEL (NE/SE/SW/NW).
4. `AtcLabelMarker` con ICAO text + dot — `TooltipContent` = posiciones + frecuencias.

Posiciones de área (APP/CTR/DEP/FSS): `AtcStationMarker` text-box sin cambios.

**Conversión geográfica para polígonos de 20 nm:**
```
latDelta = R / 60.0 × cos(θ_rad)
lonDelta = R / 60.0 / cos(lat_rad) × sin(θ_rad)
```
donde θ es el azimut desde el Norte (grados) y R es el radio en nm. Los 8 vértices de la estrella alternan `outerNm` / `innerNm` a pasos de 45° desde `startDeg`.

**Espacios aéreos — `SetAirspaces(IList<NavAirspace>)` (v0.6.7):** opacidades reducidas al 50 % respecto a v0.6.6. GeoJSON `[lon, lat]` → `PointLatLng(lat, lon)`. Fill α ∈ 5–20, stroke α ∈ 40–95.

**Proveedores de mapa:**
| Opción | Provider |
|---|---|
| Dark (Carto) | `GMapProviders.GoogleChinaSatelliteMap` remapeado a Carto Dark (defecto) |
| Street (Carto) | `GMapProviders.OpenStreetMap` |
| Satellite (ESRI) | `GMapProviders.ArcGIS_World_Imagery` |

Preferencia persistida en `App.config` clave `map_provider_index`.

**Panel ATC/ATIS detallado — `UI/Forms/AtcPanel.cs` (v0.9.8):**

Botón **ATC ▸** en la barra inferior que despliega un `Panel` acoplado a la derecha
(`DockStyle.Right`, 320 px) con **todas** las posiciones activas en IVAO: callsign,
frecuencia y **el texto completo del ATIS** de cada estación.

Complementa las formas geográficas del `AtcOverlay`: aquellas indican *dónde* está cada
posición a 20 NM, este panel dice *qué* hay activo y con qué frecuencia, y es el único
sitio donde el ATIS se lee entero sin pasar el ratón por encima de cada marcador.

- Se repuebla desde `MapForm.SetAtcStations`, es decir con cada poll de IVAO (3 min).
- `SetStations` es thread-safe (`InvokeRequired` → `BeginInvoke`): el poll entrega desde
  el thread-pool.
- Agrupa por aeropuerto y ordena las dependencias locales primero
  (DEL → GND → TWR → ATIS → APP → DEP → CTR), porque durante el rodaje lo urgente es la
  torre, no el centro de área.
- Orden de docking en `BuildLayout`: `_map` (Fill) → `bar` (Bottom) → `_sidebarPanel`
  (Left) → `_atcPanel` (Right) → `titleBar` (Top, última prioridad).

La regla de orden vive en `Helpers/AtcStationOrder.cs`, **fuera** del control WinForms: es
una decisión de dominio, no de dibujo, y así se prueba sin arrastrar
`System.Windows.Forms` al proyecto de tests.

**Sidebar de procedimientos (v0.6.5 / ampliado v0.7.0):**

Campos internos:
```csharp
ComboBox _cmbDestRwy, _cmbStar, _cmbStarTrans, _cmbApproach, _cmbApproachTrans;
string   _selApproachKey, _selApproachTransition;
```

Métodos relevantes:
- `FillApproachTransCombo(cmb, approach, ref selection)` — puebla `_cmbApproachTrans` con `(none)` + `approach.Transitions` ordenados por `Fix`. Preserva la selección si el fix sigue disponible.
- `OnApproachChanged` — limpia `_selApproachTransition`, llama `FillApproachTransCombo`, invoca `DrawApproachOverlay(app, null, rwy, ils)`.
- `OnApproachTransChanged` — resuelve la `NavApproachTransition` seleccionada, llama `DrawApproachOverlay(app, trans, rwy, ils)`.
- `DrawApproachOverlay(NavApproach app, NavApproachTransition trans, NavRunway rwy, NavIls ils)` — si `trans != null`, prepende los legs de transición con coordenadas a la lista de puntos antes de los legs del procedimiento. Firma ampliada en v0.7.0 (antes era `(app, rwy, ils)`).
- `GetCompatibleRunways(procedures, runways)` — devuelve pistas cuyos nombres aparecen en al menos un procedimiento de la lista; usado en `OnSidChanged` y `OnStarChanged` (v0.7.0).
- `MatchProcedure(name, procedures)` — lookup en cuatro pasos: exacto, base (trunca al primer punto), base invertida, prefijo 4 chars. Garantiza preselección de SID/STAR con nombres de sufijo NavData (ej. `"BIVI3C.01"` → `"BIVI3C"`) (v0.7.0).

**Controles de la barra inferior (de izquierda a derecha):**
Status label (DockStyle.Fill, v0.7.0) · TILES · ROUTE · SPACES · IVAO · [+] [−] · dropdown proveedor · FOLLOW

> **v0.7.0:** `_lblStatus` cambiado de `DockStyle.Left` (ancho fijo 380 px) a `DockStyle.Fill`, añadido al final de la secuencia `Controls.Add`. Los controles `DockStyle.Right` siempre quedan visibles independientemente del ancho del formulario.

---

### AirspaceMonitorService — `Services/AirspaceMonitorService.cs` (v0.7.1)

Monitorea espacios aéreos de la ruta activa e IVAO ATC/ATIS. Thread-safe; eventos en thread-pool.

**Eventos:**

```csharp
event Action<NavAirspace>                  OnAirspaceAlert       // Prohibited/Restricted/Danger — entró
event Action<NavAirspace>                  OnAirspaceApproaching // heading hacia espacio restringido (predictivo)
event Action<NavAirspace>                  OnAirspaceOverflight  // dentro del polígono pero sobre el límite superior
event Action<NavAirspace, NavAirspaceFreq> OnAirspaceEntered     // CTR/TMA/RMZ entrada
event Action<NavAirspace>                  OnAirspaceExited      // CTR/TMA/RMZ salida
event Action<IList<IvaoAtcStation>>        OnAtcUpdated          // poll IVAO completo (cada 3 min)
```

**Flujo de inicialización:**

```
SetActivePlan() / StartFlight()
    → Task.Run → InitRouteAsync(origin, dest)
         → NavDataClient.GetAirspacesAsync(oLat, oLon)   // origen (200 nm fijos)
         → NavDataClient.GetAirspacesAsync(dLat, dLon)   // destino
         → NavDataClient.GetAirspacesAsync(mLat, mLon)   // midpoint (si dist > 100 nm)
         → deduplicar por Id → relevant.Add(originIcao) + relevant.Add(destIcao)  ← v0.6.7
         → _pollTimer (dueTime=0, period=3 min) → PollIvaoAsync()
```

> **Por qué se añaden origin/dest explícitamente (v0.6.7):** `_relevantIcaos` se construía únicamente a partir de `ExtractIcao()` de los objetos airspace. Si ningún airspace devuelto coincidía con el ICAO del aeropuerto (p. ej. SKRG cuya CTR puede tener nombre distinto), las posiciones TWR/GND/DEL del aeropuerto quedaban filtradas en `PollIvaoAsync`.

**IVAO polling:** `GET https://api.ivao.aero/v2/tracker/whazzup` → `root["clients"]["atcs"]`. Callsign `{ICAO}_{POS}`:
- Match exacto: `relevant.Contains(icao)`
- Match FIR: `icao.StartsWith(r.Substring(0, 2))` para cualquier `r` en `_relevantIcaos`

**Filtrado de estaciones ATC (v0.6.9):** `PollIvaoAsync` aplica tres filtros antes de disparar `OnAtcUpdated`:
1. **Suppressión de duplicados consecutivos** — `_lastAtcPoll` (Dictionary por callsign) compara frequency, position y AtisText con el poll anterior; si no hay cambios, la estación se omite.
2. **Filtro de distancia** — `_airportCoordsCache` (lazy, via `NavDataClient.GetAirportInfo`) calcula la distancia al avión. Se omiten estaciones a >150 NM (80 NM en Approach/Landing). ICAOs sin coordenadas en caché (matches de prefijo FIR) pasan sin filtrar.
3. **Priorización por fase** — en Approach/Landing, solo se muestran estaciones del destino + APP/DEP de aeropuertos cercanos.

`UpdateAircraftState(lat, lon, phase, destIcao)` se llama desde MainViewModel cada 30 s para mantener la posición y fase actuales para el filtrado.

**`CheckPosition(lat, lon, altFt, headingDeg=0, groundSpeedKts=0)` (v0.7.1):**

Para cada espacio aéreo de la lista:

1. **Entrada/salida** (igual que antes): ray-casting GeoJSON en `Coordinates[0]` + `IsWithinVerticalLimits`. Dispara `OnAirspaceAlert` (Prohibited/Restricted/Danger) o `OnAirspaceEntered/Exited` (CTR/TMA/ATZ/RMZ/CTA) al entrar/salir. Al entrar, `_approachingIds` se limpia para ese ID.

2. **Sobrevuelo** (alert-type, no dentro): si `laterallyInside && altFt > GetUpperLimitFt(a)` → dispara `OnAirspaceOverflight` (una sola vez por entrada lateral). Se limpia al salir del polígono. OSD: `ABOVE  {ICAO}  DO NOT DESCEND` (Warning).

3. **Predictivo** (alert-type, fuera del polígono, GS ≥ 30 kt): proyecta `lookaheadNm = min(GS×3/60, 20)` hacia `headingDeg` con `ProjectPosition`. Si la posición proyectada cae en el polígono Y dentro de `IsWithinVerticalLimits` → dispara `OnAirspaceApproaching` (una sola vez mientras la trayectoria apunte al espacio). Se limpia al girar fuera. OSD: `AIRSPACE AHEAD  {TYPE}  {ICAO}` (Warning).

```
_approachingIds  — HashSet<string>  IDs donde ya se disparó OnAirspaceApproaching
_overflightIds   — HashSet<string>  IDs donde ya se disparó OnAirspaceOverflight
```

`ParseAltDisplay(string display)` — parsea `NavAirspaceLimit.Display` cuando `ValueFt == null`:
- `"FL095"` → 9500 ft · `"GND"`/`"SFC"` → 0 ft · cadenas numéricas → ft · `"UNL"` → null

`GetUpperLimitFt(NavAirspace)` — `UpperLimit.ValueFt ?? ParseAltDisplay(UpperLimit.Display)`.

`ProjectPosition(lat, lon, headingDeg, distNm)` — desplazamiento flat-earth, misma fórmula que `DispGeoNm` en MapForm.

Throttleado a 30 s en `MainViewModel.OnRawDataUpdated`.

**`IvaoAtcStation`:** `Callsign`, `Icao`, `Position`, `Frequency`, `AtisLines`, `AtisText`.

---

### Criterio Angular de Cambio de Calle de Rodaje (v0.5.8)

La transición entre calles de rodaje en `HandleTaxiPositionUpdate` (MainViewModel) usa un criterio **angular** en lugar de puramente temporal:

1. Si el avión ya está en una calle confirmada (`_lastLoggedTaxiway`), se consulta `FindTaxiwaySegmentBearing()` para obtener el bearing geográfico del segmento más cercano de esa calle.
2. Se calcula `HeadingDeltaBidirectional` = `min(|heading − bearing|, |heading − (bearing+180°)|)`.
3. El contador de histéresis (`_pendingTaxiwayCount`) solo avanza si `HeadingDeltaBidirectional > 25°`.
4. Si la divergencia es ≤ 25°, el contador se resetea a cero: la calle candidata se descarta mientras el avión siga el rumbo de la calle actual.
5. Al alcanzar 3 ciclos consecutivos con divergencia > 25°, se confirma el cambio de calle.

Esto elimina los falsos cambios causados por calles paralelas o de cruce que momentáneamente resultan más próximas. El umbral de primera detección (cuando `_lastLoggedTaxiway == null`) sigue siendo solo temporal (3 ciclos).

---

### AircraftPerformanceTable y Detección de Overspeed

#### ¿Qué es Vmo?

**Vmo** (Velocity Maximum Operating) es la velocidad máxima operativa publicada en el FCOM/AFM de cada aeronave, expresada en nudos IAS. En vmsOpenAcars es el único umbral de velocidad para penalizar al piloto (no se implementa Mmo por requerir datos de temperatura y presión variable con la altitud).

#### Resolución del Tipo de Aeronave

```
1. Match exacto (case-insensitive)   "B738"  → Boeing 737-800, Vmo 340 kts
        ↓ no encontrado
2. Prefijo de 4 chars
        ↓ no encontrado
3. Prefijo de 3 chars                "B38M"  → prefijo "B3" → familia B737 MAX, Vmo 340 kts
        ↓ no encontrado
4. Default genérico                  320 kts
```

#### Tabla de Vmo por Categoría

**Pistones ligeros y GA**

| Tipo ICAO | Aeronave | Vmo (kts) |
|---|---|---|
| C172 | Cessna 172 | 163 |
| C182 | Cessna 182 | 175 |
| C208 | Cessna Caravan | 175 |
| PA28 | Piper PA-28 | 148 |
| PA44 | Piper Seminole | 169 |
| BE58 | Beechcraft Baron 58 | 195 |
| BE20 | King Air 200 | 260 |
| BE30 | King Air 300/350 | 260 |
| PC12 | Pilatus PC-12 | 210 |

**Turboprops regionales**

| Tipo ICAO | Aeronave | Vmo (kts) |
|---|---|---|
| AT42–AT46 | ATR 42 (todas variantes) | 250 |
| AT72–AT76 | ATR 72 (todas variantes) | 250 |
| DH8A / DH8B | Dash 8-100/200 | 220 |
| DH8C / DH8D | Dash 8-300/400 (Q400) | 260 |
| SB20 | Saab 2000 | 290 |
| SF34 | Saab 340 | 250 |
| E120 | Embraer 120 | 255 |

**Jets regionales**

| Tipo ICAO | Aeronave | Vmo (kts) |
|---|---|---|
| CRJ2 | CRJ-200 | 320 |
| CRJ7 | CRJ-700 | 320 |
| CRJ9 | CRJ-900 | 320 |
| CRJX | CRJ-1000 | 320 |
| E135 / E145 | ERJ-135 / ERJ-145 | 320 |
| E170 / E175 | Embraer E170 / E175 | 320 |
| E190 / E195 | Embraer E190 / E195 | 320 |

**Narrow-body jets**

| Tipo ICAO | Aeronave | Vmo (kts) |
|---|---|---|
| A318–A321 | Airbus A318/319/320/321 | 350 |
| A20N / A21N | A320neo / A321neo | 350 |
| B731–B739 | Boeing 737 Clásico y NG | 340 |
| B37M–B3XM | Boeing 737 MAX 7/8/9/10 | 340 |
| MD82 / MD83 | McDonnell Douglas MD-80 | 340 |

**Wide-body jets**

| Tipo ICAO | Aeronave | Vmo (kts) |
|---|---|---|
| A332 / A333 / A339 | Airbus A330-200/300/900neo | 330 |
| A342–A346 | Airbus A340 (todas variantes) | 330 |
| A359 / A35K | Airbus A350-900/1000 | 330 |
| A388 | Airbus A380-800 | 330 |
| B752 / B753 | Boeing 757-200/300 | 350 |
| B762–B764 | Boeing 767-200/300/400 | 350 |
| B772–B77W | Boeing 777-200/300/ER/LR | 330 |
| B779 | Boeing 777X | 330 |
| B787–B78X | Boeing 787-8/9/10 | 330 |
| B744 / B748 | Boeing 747-400/8 | 365 |
| B74F / B74S | Boeing 747-400F / 747SP | 365 |

**Prefijos fallback (3 chars):**

| Prefijo | Familia | Vmo (kts) |
|---|---|---|
| A32 | A320 family | 350 |
| A33 | A330 family | 330 |
| A34 | A340 family | 330 |
| A35 | A350 family | 330 |
| A38 | A380 family | 330 |
| B73 | B737 family | 340 |
| B3  | B737 MAX | 340 |
| B74 | B747 family | 365 |
| B75 | B757 family | 350 |
| B76 | B767 family | 350 |
| B77 | B777 family | 330 |
| B78 | B787 family | 330 |
| AT4 | ATR 42 family | 250 |
| AT7 | ATR 72 family | 250 |
| DH8 | Dash 8 family | 260 |
| DHC | DHC family | 215 |
| CRJ | CRJ family | 320 |
| E17 | E-jet 170 | 320 |
| E19 | E-jet 190 | 320 |
| BE2 | King Air | 260 |
| BE3 | King Air 350 | 260 |
| C20 | Caravan | 175 |

Si ningún prefijo coincide → **320 kts** (default genérico conservador).

#### Ciclo de Detección de Overspeed (v0.7.3)

`CheckViolations()` se llama **una vez por ciclo de telemetría** (~50 ms) mientras airborne con PIREP activo:

```
Por cada ciclo:
    IAS actual > Vmo?
        SÍ y _wasOverspeed=false → _overspeedCount++
                                   IsOnAtcFrequency?.Invoke() != true → _overspeedPenaltyCount++
                                   log Warning + OSD Critical
        NO                       → _wasOverspeed=false
```

El flag `_wasOverspeed` actúa como latch: un overspeed sostenido cuenta como **un solo evento**.

`IsOnAtcFrequency` es un `Func<bool>` inyectado por `MainViewModel` que comprueba si `FsuipcService.Com1FrequencyMhz` (offset `0x034E` BCD) coincide con alguna `IvaoAtcStation.Frequency` en `AirspaceMonitorService.GetAtcStations()` (tolerancia ±0.005 MHz). Devuelve `false` si la lista está vacía (IVAO offline) o si COM1 está en 122.8 (UNICOM, nunca presente en la lista ATC).

| Eventos penalizados (`_overspeedPenaltyCount`) | Deducción |
|---|---|
| 0 | 0 pts |
| 1 | −7 pts |
| ≥ 2 | −15 pts (máximo) |

Los eventos exentos (COM1 en ATC) se contabilizan en `_overspeedCount` (total) pero no en `_overspeedPenaltyCount`. El desglose del score muestra ambos: `"3 event(s), 1 penalized (ATC exempt: 2)"`.

La misma lógica aplica al sub-criterio de velocidad del **gate de 1 000 ft AGL** (`CheckStabilizedApproachGate`): la deducción de −5 pts por velocidad fuera de Vapp se suprime si `IsOnAtcFrequency?.Invoke() == true`; el log Warning sigue activo.

---

### Turboprop — Hotel Mode y offset TRQ (v0.7.5)

#### Offset TRQ corregido

En FSUIPC7 el bloque de motor FLOAT64 (0x2000 por motor 1, 0x2100 por motor 2) tiene la siguiente distribución:

| Offset (+base) | Variable |
|---|---|
| +0x00 (0x2000) | N1 % |
| +0x08 (0x2008) | RPM eje |
| +0x20 **(0x2020)** | **Torque % del máximo** |
| +0x28 (0x2028) | Throttle lever % |
| +0x38 (0x2038) | Torque ft·lb absoluto |
| +0x40 (0x2040) | Prop RPM |
| +0x68 (0x2068) | Fuel flow (lb/hr) ← anteriormente se leía como TRQ% |

`FsuipcService._eng1TorquePctF64` usa ahora `0x2020` (motor 1) y `0x2120` (motor 2).

#### Hotel Mode

Algunos turbohélices (ATR72-600, etc.) soportan **Hotel Mode**: arrancan la turbina del motor 2 como generador de tierra con la hélice bloqueada. El Beacon permanece apagado correctamente en este estado.

**Detección** (`FsuipcService.EmitRawData`, categoría Turboprop):
```
hotelModeActive = (eng1Running && propRpm_1 < 50) || (eng2Running && propRpm_2 < 50)
```

Propagado en `RawTelemetryData.HotelModeActive`.

**Exención de beacon** (`FlightManager`): hay dos puntos de verificación, ambos exentos cuando `HotelModeActive`:
1. Transición `EnginesRunning OFF→ON` (usa `data.HotelModeActive` directamente — `_hotelModeActive` aún no se ha actualizado en ese punto).
2. Loop continuo `CheckViolations` (`beaconExempt ||= _hotelModeActive`).

En cuanto `PropRpm ≥ 50` (hélice girando), hotel mode se desactiva y el Beacon vuelve a ser obligatorio.

---

### Correcciones v0.7.6

#### Hotel Mode — Block Off falso y Block On bloqueado

**Timing invariant:** en `FlightManager.OnRawDataUpdated`, el campo `_hotelModeActive` se actualiza al final del método (~línea 1774), **después** del bloque de detección de motores. Todos los checks dentro de ese bloque deben usar `data.HotelModeActive` (valor del frame actual) en lugar de `_hotelModeActive` (valor del frame anterior).

**Block Off falso (salida):** la sub-rama que registra Block Off al detectar el arranque de motores en fase Boarding incluye ahora la guarda `!data.HotelModeActive`:

```csharp
// FlightManager.cs ~línea 1766
if (CurrentPhase == FlightPhase.Boarding && !_blockOffRecorded && !data.HotelModeActive)
    UpdateBlockOffTime();
```

Sin esta guarda, arrancar el motor 2 en Hotel Mode durante el boarding registraba Blocks Off inmediatamente con un delta de 0 s respecto al momento de START.

**Block On bloqueado (llegada):** la condición de Block On en fase TaxiIn requería `!_areEnginesOn`. En Hotel Mode el motor 2 mantiene `Eng2Running = true`, de modo que `_areEnginesOn` nunca bajaba a `false` y el sistema nunca transitaba a OnBlock. Corrección: Hotel Mode se admite como equivalente de motores apagados:

```csharp
// FlightManager.cs ~línea 1478
if ((DateTime.UtcNow - _stoppedStartTime).TotalSeconds >= 90 &&
    (!_areEnginesOn || data.HotelModeActive))
```

La lógica de `CancelFlight` no se ve afectada: sigue protegida por `IsBlockOnRecorded`.

#### SEND/CANCEL — tres causas simultáneas

Situación reportada: al finalizar un vuelo el PIREP se enviaba exitosamente pero el botón SEND permanecía activo y CANCEL no cambiaba a EXIT. Al pulsar CANCEL se eliminaba el PIREP ya archivado.

| # | Causa | Corrección |
|---|---|---|
| 1 | `SendPirep()` carecía de rama `else` y `try/catch` — un fallo posterior al éxito HTTP dejaba la UI sin actualizar | `try/catch` con log de error; rama `else` con mensaje de reintento; SEND permanece activo para reintento |
| 2 | `ActivePirepId` no se limpiaba hasta `ResetFlightState()` — si algo fallaba antes, `CancelFlight()` encontraba el ID y borraba el PIREP ya archivado | `ActivePirepId = ""` inmediatamente tras el `await` exitoso de la API, antes de `ResetFlightState()` |
| 3 | `OnFlightPhaseChanged` (async void) podía re-habilitar SEND tras el éxito si un evento asíncrono tardío llegaba con fase OnBlock | Guard `!string.IsNullOrEmpty(_flightManager.ActivePirepId)` en la condición de habilitación del botón |

Principio resultante: **si la API confirma el PIREP, `ActivePirepId` queda vacío de inmediato**. Cualquier código posterior que dependa de ese campo para tomar decisiones destructivas (borrar, reenviar) encontrará string vacío y no actuará.

---

### Correcciones v0.7.7

#### FilePirep — verificación de estado tras respuesta HTTP no-2xx

**Síntoma:** phpVMS archivaba el PIREP correctamente (quedaba "pendiente de aprobar" en el servidor) pero devolvía un código HTTP no-2xx. `FilePirep()` retornaba `false`, el mensaje "No se pudo enviar el PIREP" aparecía, SEND permanecía activo, y si el piloto pulsaba CANCEL el PIREP archivado se eliminaba.

**Causa raíz:** `ApiService.FilePirep()` solo comprueba `response.IsSuccessStatusCode`. Si phpVMS procesa el request (crea/actualiza el PIREP) pero el código de respuesta no es 2xx (comportamiento observado en instalaciones en producción), el cliente no puede distinguir un fallo real de un éxito con error de protocolo.

**Corrección — `FlightManager.FilePirep()`** (línea ~1971):

```csharp
bool success = await _apiService.FilePirep(ActivePirepId, finalData);
if (!success)
{
    // phpVMS puede procesar el PIREP y devolver un código no-2xx.
    // Verificamos el estado real antes de asumir fallo.
    try
    {
        var pirepDetail = await _apiService.GetPirepDetail(ActivePirepId);
        if (pirepDetail?.Status != null &&
            pirepDetail.Status != "1" && pirepDetail.Status != "6")
        {
            // Estado distinto de in_progress/paused → el PIREP fue archivado
            success = true;
        }
    }
    catch { }
}
```

**Estados phpVMS relevantes:** `1` = in_progress, `6` = paused, `2` = pending (archivado, pendiente de aprobación), `3` = accepted, `4` = rejected, `5` = cancelled. Cualquier estado distinto de 1/6 indica que el PIREP salió del ciclo activo y fue procesado.

**Comportamiento resultante:**

| Escenario | Resultado |
|---|---|
| HTTP 2xx | éxito inmediato — sin cambios |
| HTTP no-2xx + PIREP en servidor con status ≠ 1/6 | GET confirma éxito — `ActivePirepId` limpio, UI actualizada |
| HTTP no-2xx + PIREP sigue in_progress | GET confirmará status=1 → fallo real → SEND habilitado para reintento |
| HTTP no-2xx + GET también falla (sin red) | catch silencioso → fallo real → SEND habilitado para reintento |

#### Hotel Mode Block On — variable `data` inaccesible en UpdatePhase()

La corrección de v0.7.6 (`!_areEnginesOn || data.HotelModeActive`) usaba el parámetro `data` (de tipo `RawTelemetryData`) dentro del método `UpdatePhase(int, int, bool, int, double)`, que no recibe ese parámetro. Corregido usando el campo de instancia `_hotelModeActive`, que `UpdateTelemetry()` actualiza en cada ciclo antes de que `UpdatePhase()` sea invocado desde `MainViewModel`.

---

### Correcciones v0.7.8

#### PIREP — flujo de archivado y auto-aprobación

**Causa raíz identificada:** al entrar en fase `OnBlock`, `UpdatePhase()` lanzaba siempre:
```csharp
Task.Run(() => UpdatePirepStatus(FlightPhaseHelper.GetStatusCode(CurrentPhase)));
// → PUT /api/pireps/{id}  { status: "ARR" }
```

phpVMS, al recibir `status = "ARR"`, archivaba el PIREP internamente (lo movía a estado "pending") **sin pasar por el endpoint `/file`**. Consecuencias:
- El endpoint `/file` fallaba con HTTP no-2xx (PIREP ya en estado "pending"), produciendo el mensaje "No se pudo enviar el PIREP".
- La auto-aprobación de phpVMS no se disparaba porque se activó por la ruta incorrecta, requiriendo aprobación manual del admin.

**Tres correcciones:**

**1. Supresión de `UpdatePirepStatus` en fases terminales**

```csharp
// Antes:
if (previousPhase != CurrentPhase)
    Task.Run(() => UpdatePirepStatus(...));

// Después:
if (previousPhase != CurrentPhase &&
    CurrentPhase != FlightPhase.OnBlock &&
    CurrentPhase != FlightPhase.Completed)
    Task.Run(() => UpdatePirepStatus(...));
```

`FlightPhase.OnBlock` y `FlightPhase.Completed` devuelven el mismo código `"ARR"` en `FlightPhaseHelper`. Ambas fases quedan excluidas — el endpoint `/file` es el único que gestiona la transición de estado final en phpVMS.

**2. `block_on_time` consolidado en el payload de `FilePirep()`**

`_serverBlockOnTime` se establece sincrónicamente al detectar OnBlock (ya no depende del resultado de una llamada HTTP asíncrona). El valor se incluye en `finalData`:

```csharp
// En UpdatePhase, al detectar OnBlock:
_serverBlockOnTime = DateTime.UtcNow;
OnLog?.Invoke(_("Log_BlockOn", _serverBlockOnTime.ToString("HH:mm:ss")), Theme.MainText);

// En FilePirep(), en finalData:
block_on_time = (_serverBlockOnTime != default ? _serverBlockOnTime : DateTime.UtcNow)
                .ToString("yyyy-MM-dd HH:mm:ss"),
```

Esto garantiza que `block_on_time` llegue a phpVMS junto con el archivado, en un único request atómico.

**3. `UpdateBlockOffTime()` — eliminado acceso directo a HttpClient**

`UpdateBlockOffTime()` usaba `_apiService.HttpClient.PutAsync(...)` directamente (acceso a propiedad pública del `HttpClient`). Refactorizado para usar el método encapsulado `_apiService.UpdatePirep()`, consistente con el resto del código.

**Secuencia resultante:**

```
OnBlock detectado
  ├── _serverBlockOnTime = DateTime.UtcNow   ← local, inmediato
  ├── Log "Block On registrado a las HH:MM"
  └── (no se envía status a phpVMS)

Piloto pulsa SEND:
  POST /api/pireps/{id}/file
    { state: 2, block_on_time: ..., distance: ..., score: ..., ... }
    → phpVMS archiva + dispara auto-aprobación
```

---

### SystemInfoHelper — `Helpers/SystemInfoHelper.cs` (v0.6.2)

Clase estática interna que recopila información de hardware y simulador al arrancar la aplicación, sin dependencias de WMI (que requiere permisos elevados y es lento).

**Propiedades públicas:**

```csharp
string OsSummary   // "Windows 10 Home Single Language / RAM 16 GB"
string GpuSummary  // "NVIDIA GeForce 840M / VRAM ?"
string SimSummary  // "MSFS 2024 / 1.39.15"
```

**`Initialize()`** — llamado en `MainForm` tras `ConnectViewModelEvents()`. Rellena `OsSummary` y `GpuSummary`.

**`SetSimVersion(simName)`** — llamado en `MainViewModel.OnFsuipcConnected`. Rellena `SimSummary`.

**`GetPrefileNotes()`** — devuelve un bloque multilínea con versión + OS + GPU + Sim, incluido en el campo `notes` del prefile phpVMS via `ApiService.PrefileFlight`.

**Detección de GPU (`GetBestGpu`):**

1. Lee `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}` (subkeys numéricas = adaptadores de vídeo instalados).
2. Lee `DriverDesc` (nombre) y `HardwareInformation.MemorySize` (VRAM como QWORD, soporta >4 GB).
3. Filtra adaptadores virtuales: Microsoft Basic, Hyper-V, Remote Desktop, VMware, VirtualBox, Parsec, VDDM.
4. Asigna un rango discreto (`DiscreteRank`):

| Rango | Condición |
|---|---|
| 3 | Nombre contiene NVIDIA / GeForce / Quadro / RTX / GTX |
| 2 | Nombre contiene Radeon RX / Radeon Pro / AMD Radeon / Intel Arc |
| 1 | Cualquier otro fabricante |
| 0 | Nombre contiene Intel (integrado) |

5. Selecciona la GPU con **rango más alto** (VRAM como desempate dentro del mismo rango).

> **Portátiles NVIDIA Optimus:** la GPU discreta es `Render-Only Device` y su VRAM aparece como 0 bytes en el registro. La iGPU Intel muestra ~1 GB de memoria compartida. El criterio de rango primario garantiza que NVIDIA (3) siempre gane a Intel (0). La VRAM se muestra como `?` cuando es 0.

**Detección de RAM:** P/Invoke `GlobalMemoryStatusEx` (kernel32). Sin WMI, sin permisos elevados.

**Detección de versión del simulador:** `Process.GetProcessesByName(procName)` → `FileVersionInfo.GetVersionInfo(MainModule.FileName)` → `ProductVersion` recortado a 3 partes (`X.Y.Z`).

**Flujo en MainViewModel.StartFlight():** al confirmar el inicio del vuelo, se envía un `AcarsPositionUpdate` con 4 entradas log a la tabla ACARS de phpVMS:

```
AcarsPosition[0].log = OsSummary       → "Windows 10 Home / RAM 16 GB"
AcarsPosition[1].log = GpuSummary      → "NVIDIA GeForce 840M / VRAM ?"
AcarsPosition[2].log = SimSummary      → "MSFS 2024 / 1.39.15"
AcarsPosition[3].log = "{Type} / {Dev}"  → "B738 / PMDG"
```

Todos con `status = "ground"` y `source = "vmsOpenAcars"`. Envío asíncrono (`Task.Run`).

---

### SimbriefEnhancedService

**`GenerateDispatchUrl()`** — Genera URL para pre-cargar el plan en SimBrief:
- Hora de salida: UTC actual + 30 min
- Parámetros: aerolínea, número, origen, destino, tipo aeronave, matrícula, ruta, CI, unidades

**`FetchAndParseOFP()`** — Descarga y parsea el JSON de la API de SimBrief:
- Construye `SimbriefPlan` completo: routing, combustible, pesos, tiempos, elevaciones, URL del PDF
- Maneja que `files.pdf` puede ser string o objeto `{name, link}`
- Campos de combustible: `BlockFuel` ← `fuel.plan_ramp` · `TripFuel` ← `fuel.enroute_burn`

---

### OFP PDF — Flujo Completo

```
FlightPlannerForm (acepta OFP)
    │
    ├─► SetActivePlan(plan)
    └─► DownloadOFPPdfAsync()  ← background, fire-and-forget
              │
              └─► plan.LocalPdfPath = "/temp/vmsOFP_xxxx.pdf"

BtnOfp_Click
    ├─ GetCachedOFPPath()  ←─ archivo en disco?
    │       ├─ Sí → OFPViewerForm(cachedPath)  [instantáneo]
    │       └─ No → DownloadOFPPdfAsync() → OFPViewerForm(newPath)
    │
    └── OFPViewerForm.OnFormClosed → File.Delete(tempPath)
```

---

## Modelos Principales

### SimbriefPlan

```csharp
// Vuelo
string FlightNumber, Airline, Origin, Destination, Alternate, Route
int DestinationElevation, OriginElevation, CruiseAltitude, EstTimeEnroute
long TimeGenerated, ScheduledOffTime

// Aeronave
string Aircraft, AircraftIcao, Registration, FlightId, BidId

// Combustible / Pesos
double BlockFuel, TripFuel, DepartureFuel, PayLoad, ZeroFuelWeight
string Units   // "KG" | "LBS"
int PaxCount

// PDF
string PdfUrl, LocalPdfPath
```

### Pilot

```csharp
int    Id, AirlineId, IvaoId, AircraftSeats
string PilotId, Name, AirlineName, Rank, CurrentAirport, AirlineCountry
double? CurrentAirportLat, CurrentAirportLon
```

`AirlineCountry` — ISO-2 country code de la aerolínea (p. ej. `"CO"`, `"ES"`). Fuente: `airline.country` del `GET /api/user` de phpVMS.  
`AircraftSeats` — capacidad de asientos del avión asignado. Fuente: `curr_aircraft.subfleet.total_seats`. Cero = no disponible → anuncios de cabina activados (safe default).

---

### FlightRecord

```csharp
int    Id, LandingRateFpm, Score
string FlightNumber, Origin, Destination, RunwayName, MetarRaw
DateTime FlightDate
double GForce, TouchdownDistFt, CenterlineDevFt
// Display helpers:
string DisplayDate  → "yyyy-MM-dd HH:mm"
string DisplayRoute → "ORIG → DEST"
string DisplayScore → "score/100"
```

### ApproachTrackPoint

```csharp
int    FlightId, SeqNo
double Lat, Lon, AltFt, AglFt, IasKt, VsFpm, HeadingDeg, DistNm, LateralFt
```

### FlightPhase (enum)

`Idle → Boarding → Pushback → TaxiOut → TakeoffRoll → Takeoff → Climb → Enroute → Descent → Approach → Landing → Landed → AfterLanding → TaxiIn → OnBlock → Arrived → Completed`

---

## Localización

Dos archivos JSON en `Languages/`: `en.json` y `es.json`.  
Acceso mediante el helper estático `L._("key")` importado con `using static vmsOpenAcars.Helpers.L`.  
El idioma se selecciona en `SettingsForm` y se persiste en `App.config`.

---

## Configuración (App.config / AppConfig)

| Clave | Default | Descripción |
|---|---|---|
| `polling_interval_ms` | 50 | Intervalo de polling FSUIPC |
| `update_interval_taxi` | 30 | Telemetría en taxi (s) |
| `update_interval_takeoff` | 5 | Telemetría en despegue (s) |
| `update_interval_climb` | 15 | Telemetría en subida (s) |
| `update_interval_cruise` | 30 | Telemetría en crucero (s) |
| `update_interval_descent` | 15 | Telemetría en descenso (s) |
| `update_interval_approach` | 5 | Telemetría en approach (s) |
| `fuel_tolerance_percent` | 10 | Tolerancia combustible (%) |
| `fuel_tolerance_absolute` | 50 | Tolerancia combustible (kg) |
| `simbrief_civalue` | 30 | Cost Index para SimBrief |
| `simbrief_units` | lbs | Unidades combustible SimBrief |
| `navdata_api_url` | _(vacío)_ | URL base del servicio NavData API. Editable en Settings → NavData API desde v0.9.11; antes solo se cambiaba a mano en el `.config` |
| `navdata_api_key` | _(vacío)_ | API key del servicio NavData |
| `navdata_api_domain` | _(vacío)_ | Dominio de la aerolínea (cabecera `X-Origin-Domain`) |
| `landing_log_path` | _(vacío)_ | Ruta al archivo `landing_log.sqlite` |
| `osd_enabled` | true | Activa el overlay OSD |
| `osd_sound_enabled` | true | Activa los chimes del OSD (Info/Success/Warning/Critical) |
| `osd_duration_seconds` | 4 | Tiempo de visualización por notificación (s) |
| `osd_screen_index` | 0 | Índice de pantalla para el OSD (0 = primaria) |
| `osd_opacity` | 90 | Opacidad del OSD (10–100 %) |
| `raas_enabled` | true | Muestra el popup de rodaje (RAAS) al encender la luz de taxi o al entrar en TaxiOut (v0.9.14) |
| `raas_voice_enabled` | true | Voz SAPI del RAAS; se elige en el popup y se recuerda (v0.9.14) |
| `raas_volume` | 80 | Volumen de los avisos hablados, 0–100 (v0.9.14) |
| `osd_airspace_alerts` | true | Muestra en el OSD los avisos de zona restringida (Prohibited/Restricted/Danger, `AIRSPACE AHEAD`, `ABOVE … DO NOT DESCEND`). No afecta al log de vuelo ni al polígono del mapa (v0.9.10) |
| `cabin_announcements_enabled` | true | Activa los anuncios de cabina pregrabados |
| `cabin_announcements_volume` | 80 | Volumen de los anuncios (0–100 %) |

---

## Referencias de archivos clave

| Archivo | Contenido relevante |
|---|---|
| `Core/Flight/FlightManager.cs` | partial (532 l): `CheckStabilizedApproachGate`/`CheckApproachBelowGate`; `CheckViolations` (TA/TL/QNH/10k ft); `SetRunwayTouchdownData`; `SetApproachData`; `SetOriginTransitionAlt/SetDestTransitionLevel`; `TransitionTo`; `SetResumedPenalties`; `BeaconStrobeSharedAircraft` (DH8D beacon exemption); `SetArrivalAirportElevation`/`ArrivalAirportElevationFt` — override de elevación de referencia tras desvío confirmado (v0.9.0); `ClearDivertedAirport()` — revierte un desvío marcado por error (v0.9.1) |
| `Core/Flight/FlightPhaseStateMachine.cs` | `TransitionTo(phase)`; umbrales y debounce de fase; timers de transición; `TaxiOut→TakeoffRoll` con debounce 5 s (`_takeoffRollStart`, v0.9.2) |
| `Core/Flight/ApproachValidator.cs` | gate 1 000 ft (speed, VS, bank, pitch, gear, flaps); `CheckLocalizerAlignment`; `CheckMinimums`; flag `IlsTunedCorrectly`; `CheckArrivalQnhProvisionalAsync`/`FinalizeArrivalQnhAsync` — QNH de llegada provisional/confirmado (v0.8.10); `CheckQnhAsync`/`CheckPhaseEntryLights` (TakeoffRoll) con guard una-vez-por-vuelo (`_departureQnhChecked`/`_takeoffLightsChecked`, v0.9.2) |
| `Core/Flight/TouchdownState.cs` | `LandingRate`, `GForce`, `BankAngle`, `PitchAngle`; geometría de pista (`DistanceFt`/`CenterlineDeviationFt`/`RunwayName`) publicada como una referencia inmutable `RunwayGeometry` para que el lector nunca vea un conjunto a medio actualizar (v0.9.5); reset en `ResetFlightState()` |
| `Core/Flight/PenaltyState.cs` | `OverspeedCount`, `LightsViolations`, `StabilizedPenalty`, `QnhPenalty`; `_singleEngineTaxiDistance`; consolidado en `ScoringService` |
| `Core/Flight/FlightManager.Telemetry.cs` | procesamiento de `RawTelemetryData`; actualiza `TouchdownState` y `PenaltyState` por ciclo |
| `Core/Flight/FlightManager.Lifecycle.cs` | `PrefilePirep`; `FilePirep` (incluye `block_on_time` en payload; corrige `arr_airport_id` vía `UpdatePirep` y agrega `diversion-airport` cuando hay desvío confirmado — v0.8.8–v0.8.9; awaita `FinalizeArrivalQnhAsync` antes de `BuildScoreData()` — v0.8.10; sin reubicación de piloto: `MovePilotAsync` se eliminó por roto, phpVMS reubica solo — v0.9.8); `CancelPirep`; `UpdatePirepStatus` (excluye OnBlock/Completed); `ResetFlightState` |
| `ViewModels/MainViewModel.cs` | 711 l: `WireAirspaceMonitor`; `StartFlight`+`SetActivePlan`; `GetAircraftCategory()`; `HandleTaxiPositionUpdate` (criterio angular 25°); `SnapshotLandingRecord`→`SaveLandingRecord`; `UpdateAircraftState` |
| `ViewModels/TelemetryCoordinator.cs` | puente `FsuipcService`→`FlightManager`; throttling OSD/map; eventos `OnFlightPhaseChanged`, `OnTouchdown`; **detección de aeropuerto de llegada distinto al planeado (v0.8.8–v0.9.9)** — ver sección dedicada abajo; `OnPhaseChanged` trata `{Descent, Approach}` como superestado (v0.8.10); `ReconfirmApproachRunway` con los seis filtros de desvío — lateral, cono angular, gradiente de descenso, alterno más cerca, "en final" y persistencia (v0.9.9); `TouchdownFallbacks` en `LookupRunwayData` (v0.9.9) |
| `ViewModels/AcarsReporter.cs` | `SendPirep`; `ResumeFromAcarsHistoryAsync`; `SendScoringCheckpointAsync` (CHK 60 s) |
| `UI/Forms/MapForm.cs` | 1 473 l: event wiring; zoom/tile; delega en `MapRouteController`, `MapOverlayManager`, `SidebarController`; capas toggleables TILES/ROUTE/SPACES/IVAO |
| `UI/Map/MapRouteController.cs` | `LoadRoute` + SID/STAR virtual + suavizado Bézier; `UpdatePosition`; `SetAircraftCategory` |
| `UI/Map/MapRouteController.Approach.cs` | `ClearApproachOverlay`; `DrawApproachOverlay` (transition, final, centerline, missed, hold racetrack) |
| `UI/Map/MapRouteController.Helpers.cs` | 22 helpers estáticos: `MatchProcedure`, `InterpolateArcLegs`, `BuildSmoothedRoutes`, `ComputeDmeArc`, `ComputeDepartureArc`, `ComputeHoldRacetrack`, `GeodesicBearing`, `DistanceKm`, etc. |
| `UI/Map/MapOverlayManager.cs` | `SetAirspaces` (polígonos GeoJSON, opacidades por tipo); `SetAtcStations` (TWR círculo rojo, GND/DEL estrella, formas WebEye) |
| `UI/Map/SidebarController.cs` | `BuildSidebar` (SID/STAR/APP con restricciones); `OpenApproachChart()` |
| `UI/Forms/ApproachChartForm.cs` | carta GDI+ (v0.6.8). Plan view: legs, arcos AF (`DrawDmeArc`), IAF/FAF/MAP. Profile: glideslope naranja, glidepath verde, DA/MDA rojo. Se abre desde `SidebarController.OpenApproachChart()` |
| `Services/NavDataService.cs` | `SelectApproachThreshold` — "está en final" (rumbo ±15° magnético-contra-magnético + ≤2 NM del eje **verdadero** + `along ≤ 0`), `internal static` y puro para poder testearlo (v0.9.9); `IsWithinFinalCone` + `MaxFinalConeAngleDeg` (4°) + `FinalConeFloorNm` (0.25) — plausibilidad angular de un match (v0.9.9); `IsPlausibleDiversionDescent` + `MaxDiversionGradientFtPerNm` (700) + `MinGradientDistanceNm` (0.5) — gradiente de descenso usable hasta el campo emparejado (v0.9.9); `IsPlausibleDiversionDistance` + `GetAirportDistanceNm` — un desvío tiene que ser a un aeropuerto más cercano que el destino (v0.9.9); `ProjectOnRunway`+`WithinFootprint` (retorna `null` si ninguna pista pasa el footprint, v0.9.2); `TrueRunwayBearing`; `FindTaxiwaySegmentBearing`; `NextIntersection`; `GetAirportElevationFt` (vía `NavDataClient.GetAirportInfo`, v0.9.0) |
| `Services/NavDataClient.cs` | `LoadAirportAsync` (6 endpoints paralelos); `GetAirspacesAsync` (sin radius_nm, caché 2 capas); `GetWeatherAsync` (TTL 5 min); `GetNearestApproachAirportAsync` (sin caché, posición cambia cada llamada — v0.8.9) |
| `Services/NavDataCache.cs` | `CreateSchema` (3 tablas); `TryGetAirspace/StoreAirspace` (TTL 7 días); `SyncAirac` (purga airport+navaid, no airspaces) |
| `Services/AirspaceMonitorService.cs` | `InitRouteAsync` (acepta initLat/initLon); `CheckPosition` (ray-casting GeoJSON); `PollIvaoAsync` (filtrado duplicados/distancia/fase); `UpdateAircraftState`; timer 3 min |
| `Services/FsuipcService.cs` | debounce 2.5 s luces: `_pendingXxxState/At` — nuevo estado estable ≥2.5 s antes de disparar evento; elimina falsos positivos por parpadeo ~1.6 s del sim |
| `Services/CabinAnnouncementService.cs` | `PrefetchAsync`; cola FIFO; NAudio playback; `TestAnnouncementAsync` |
| `Services/ScoringService.cs` | 17 criterios + bonus; TDZ+Centreline ~l213; Localizer+Minimums ~l247 |
| `Models/NavData.cs` | `NavAirspace`, `NavAirspaceGeometry` (GeoJSON [lon,lat]), `NavAirspaceFreq`; `BriefingCheckResult` |
| `Helpers/SystemInfoHelper.cs` | `GetBestGpu` (DXGI fallback, rango 0–3); `GetCpuString` (registro + ProcessorCount) |
| `vmsOpenAcars.Tests/ScoringServiceTests.cs` | 132 tests MSTest de `ScoringService`: un test por criterio y por frontera (ambos lados de cada umbral). Ver "Tests" más abajo |
| `vmsOpenAcars.Tests/PirepStateTests.cs` | 13 tests de la clasificación de estado de PIREP (`Pirep.IsActiveState`), que decide el fallback de `FilePirep()` (v0.9.6) |
| `vmsOpenAcars.Tests/GeoMathTests.cs` | 44 tests de geometría flat-earth (incluida `DistanceToSegmentNm` y su recorte en los extremos), del respaldo regional de TA/TL y de la lectura de `NavAirportInfo` (v0.9.8, v0.9.9) |
| `vmsOpenAcars.Tests/RouteCorridorTests.cs` | 7 tests de `RouteCorridor` sobre el **navlog real de SimBrief** del vuelo SKRG→SKBQ: la llegada son los últimos tramos por distancia y no los marcados `is_sid_star` (LOLUD llegó sin bandera), los puntos de la llegada dan 0 NM, el corredor aguanta 3 NM al lado y suelta a 7, un punto en crucero no cuenta como llegada, las posiciones reales del falso SKTL están a más de 20 NM del plan, y sin navlog no se suprime nada (v0.9.9) |
| `vmsOpenAcars.Tests/AtcPanelTests.cs` | 6 tests del orden de posiciones ATC (v0.9.8) |
| `vmsOpenAcars.Tests/ApproachThresholdTests.cs` | 26 tests de "está en final" (`SelectApproachThreshold`), del cono angular (`IsWithinFinalCone`), del gradiente de descenso (`IsPlausibleDiversionDescent`) y de la regla de distancia (`IsPlausibleDiversionDistance`). Nueve usan las **coordenadas y altitudes exactas de dos vuelos reales**: el falso SKTL (sus seis sondeos, todos rechazados por el gradiente) y el SKCG legítimo; más los tres falsos de la aproximación a KBOS —28M, 1B9 y KOWD, que **pasan** el gradiente porque su perfil era normal— y la final correcta de Logan 04R. Además comprueban que el eje devuelto es el **verdadero** (340.58° SKTL rwy 35, 2.31° SKCG rwy 01) y no el magnético, el límite y el suelo del cono, la frontera exacta y el suelo del gradiente, que el AGL se mide contra la elevación del campo emparejado, 1 NM pasado el umbral, 3 NM laterales, rumbo fuera de 15°, la recíproca y el desempate de paralelas (v0.9.9) |
| `Helpers/GeoMath.cs` | Único punto de verdad de la geometría flat-earth: `Project`, `ProjectPoint`, `DistanceNm/Km`, `BearingDeg`, `BearingDiffDeg`, `ToMeters`, `CosLat` con guarda polar, y `DistanceToSegmentNm` — recta **recortada** a los extremos, que es lo que hace falta para medir desviación de traza (v0.9.8, v0.9.9) |
| `Helpers/RouteCorridor.cs` | Corredor de la llegada planificada: `ArrivalFixes` (los últimos `ArrivalWindowNm` 40 NM de la traza del navlog, por distancia y no por `IsSidStar`), `DistanceFromArrivalNm`, `IsOnArrival` (5 NM). Sin navlog no opina (v0.9.9) |
| `Helpers/FireAndForget.cs` | `Run(work, onError, operationName)` — trabajo en segundo plano sin esperar, con la excepción **observada** y reportada al log (v0.9.8) |
| `Helpers/TransitionDefaults.cs` | Respaldo regional de TA/TL por `iso_country` cuando NavData no los publica; devuelve 0 si el país es desconocido (v0.9.8) |
| `Helpers/AtcStationOrder.cs` | Orden de presentación de posiciones ATC (locales primero); fuera del control WinForms para poder probarlo (v0.9.8) |
| `UI/Forms/AtcPanel.cs` | Panel lateral ATC/ATIS detallado del mapa, con el texto completo del ATIS por estación (v0.9.8) |
| `Helpers/TaxiGraph.cs` | Grafo de calles: fusión de extremos a ≤45 m, Dijkstra al umbral, cruce entre dos calles y lado del giro (v0.9.14) |
| `Helpers/RaasAdvisor.cs` | `TaxiRoutePlan` (ruta editable, `Parse`/`ToText`/`SameRoute`), `ResolveGuidance` (próximo giro y lado) y el motor de avisos con antirrebote; puro y sin reloj propio. Las reglas de «fuera de ruta» (insistencia sin acercarse a la pista), «ruta completa» (solo dentro de la pista, una vez) y la comparación de rutas son de v0.9.15 |
| `Services/RaasVoice.cs` | Voz SAPI con cola FIFO en hilo propio, volumen y degradación silenciosa si no hay voces (v0.9.14) |
| `UI/Forms/TaxiRouteForm.cs` | Popup de rodaje: pista, ruta editable, RAAS/voz/volumen y prueba hablada. Si vuelve tras el pushback cambia su texto de ayuda y explica por qué (`Raas_RepromptHint`) (v0.9.14, v0.9.15) |
| `ViewModels/TaxiRoutePrompt.cs` | Lo que el popup necesita para pintarse (pistas, pista por defecto, ruta sugerida, recalculador y si es el segundo aviso del vuelo) (v0.9.14, v0.9.15) |
| `vmsOpenAcars.Tests/RaasTests.cs` | 16 tests con 106 segmentos reales de SKBO y el rodaje real del `MNjR664PBAr25RbD`: ruta por grafo, parseo de la ruta, guía, avisos, y las reglas de «fuera de ruta» y «ruta completa» de v0.9.15 (v0.9.14, v0.9.15) |
| `vmsOpenAcars.Tests/RaasReplayTests.cs` | Un test que **reproduce el rodaje completo** del `MNjR664PBAr25RbD` (569 segmentos, 35 hold-shorts, 4 pistas, las 72 líneas `SCH` del log real y las 42 posiciones entre el pushback y el takeoff roll) y escribe la secuencia de avisos a `%TEMP%\raas_replay_MNjR664.txt`. Exige los cinco avisos y su orden, no solo los vuelca: es el banco para juzgar el RAAS sobre una traza real sin volar (v0.9.14, v0.9.15) |
| `vmsOpenAcars.csproj` | `GenerateBindingRedirectsOutputType=true` — impide sobreescribir binding redirect manual de SQLite. Referencia `System.Speech` para el RAAS |

---


## RAAS y guía de rodaje (v0.9.14–v0.9.15)

Avisos de rodaje tipo RAAS —con voz— y guía giro a giro por la ruta que elige el piloto. Todo el
cálculo vive en dos helpers puros; el coordinador solo aporta los hechos y reparte la salida.

**Flujo**

```
luz de taxi ON  ─┐
                 ├─► RequestTaxiRoutePrompt (una vez por vuelo, si raas_enabled)
fase → TaxiOut  ─┘        │
                          └─► Task.Run: pistas del aeropuerto (NavDataClient.GetRunways)
                              + ruta sugerida (TaxiGraph.Suggest desde la posición actual
                                al umbral de la pista por defecto, la del OFP)
                              → MainForm muestra TaxiRouteForm (pista, ruta editable,
                                RAAS/voz/volumen) → StartTaxiGuidance(...)

freno puesto en fase Pushback ─┐
                               ├─► RequestTaxiRoutePrompt("after pushback"), una vez por vuelo
fase → TaxiOut (sin pushback) ─┘        │
                                        └─► recalcula desde la posición actual y **solo** abre
                                            el popup si el grafo cambia de idea
                                            (TaxiRoutePlan.SameRoute contra la propuesta anterior)

cada frame de telemetría, 1 Hz y solo en fases de rodaje (GS ≤ 60 kt):
    FindNearestTaxiway  → calle actual
    FindRunwayEntry     → ¿estoy en pista?
    FindHoldingPoint    → hold-short más cercano + distancia + ¿voy hacia él?
    RaasAdvisor.ResolveGuidance → próxima calle, distancia al cruce, lado
    RaasAdvisor.Evaluate        → un aviso o ninguno (antirrebote 20 s, re-armado al cesar)
        → log (Theme.Taxi) + OSD (Info) + RaasVoice.Speak(texto i18n)
```

**Umbrales** (`RaasAdvisor`): hold-short a 150 m (aviso) y 40 m (insistencia); giro a 250 m y 60 m;
antirrebote de 20 s por situación. La distancia al giro es la recta al cruce, no el arco por el eje
de la calle: el dataset corta las calles en tramos de ~30 m y para decir «en 120 m» sobra.

**Fuera de ruta (v0.9.15)**: no basta con que la calle actual no esté en la lista — hay que
**insistir 15 s** (`OffRoutePersistSec`) **sin acercarse** a la pista al menos 50 m
(`OffRouteProgressM`) respecto al punto más cercano del episodio. La distancia recta al umbral de
la pista elegida entra como un hecho más (`Inputs.DistanceToRunwayM`, resuelto una vez al empezar
la guía) y **sin dato el aviso se decide solo por insistencia**. El motivo está medido en el
replay: la ruta del grafo y la de ATC llevan al mismo sitio por calles distintas. **`RUTA DE
RODAJE COMPLETA`** es haber entrado **en la pista** (`OnRunway` con guía activa) y se anuncia una
sola vez por guía; antes se disparaba al agotar la lista de calles.

**Grafo** (`Helpers/TaxiGraph.cs`): los extremos de segmento a ≤45 m se fusionan en un nodo —más
ajustado y un aeropuerto real queda desconectado, porque los datasets no comparten exactamente el
punto de unión—, cada segmento es una arista con su longitud, y un Dijkstra hasta el nodo más
cercano al umbral da la secuencia de calles. `TryFindJunction` cruza dos calles por el par de
extremos más próximos y exige que la distancia sea ≤2 × snap (si no, no se tocan y el «cruce» solo
produciría un giro falso).

**Voz**: `Services/RaasVoice.cs` con SAPI. Se eligió SAPI para la primera versión porque no obliga a
grabar ni distribuir una librería de frases; sus pegas quedan asumidas: depende de que Windows tenga
voz del idioma y el timbre cambia entre equipos. La síntesis va en un hilo propio con cola FIFO, así
que el hilo de telemetría nunca se bloquea; si no hay voces, se degrada en silencio y lo dice una vez
en el log. El texto sale de las mismas claves i18n que el log y el OSD.

**Por qué 1 Hz sobre telemetría cruda y no sobre el envío de posiciones**: el intervalo de rodaje
hacia phpVMS es de 30 s (`update_interval_taxi`); a 15 kt son ~230 m entre muestras, demasiado para
avisar a 150 m de un hold-short.

**Estado de la guía**: se crea al arrancar el rodaje y se borra en `Reset()` (vuelo nuevo) y no se
reactiva en vuelo; el piloto puede pararla no activándola en el popup.

**Segundo aviso, ya en el punto de inicio (v0.9.15)**: el popup puede salir dos veces por vuelo. El
segundo se pide al **poner el freno de parqueo en fase `Pushback`** —el fin del empuje, con el avión
parado— o, si no hubo pushback, **al entrar en `TaxiOut`**, que es lo que cubre los puestos remotos:
la máquina de fases pasa de `Boarding` a `TaxiOut` por movimiento sostenido (`GroundSpeed > 5 kt`
durante 2 s) sin exigir pushback, beacon ni motores estabilizados, así que no hace falta una
heurística nueva de «rodaje directo». Antes de abrir la ventana se recalcula la propuesta desde la
posición actual y se compara con la anterior (`TaxiRoutePlan.SameRoute`): si el grafo no cambia de
idea, no hay nada que contar y no aparece nada. Medido en el rodaje real del `MNjR664PBAr25RbD`:

| Desde | Propuesta del grafo |
|---|---|
| puesto G49 (`BST` 21:45:47) | `C P G N H M K K2 K1` (4 010 m) |
| fin del pushback (`freno` 21:52:39) | `C P G N H M K K2 K1` (4 021 m) — **la misma** |
| arranque del rodaje (`── TAXI OUT ──` 21:55:54) | `C P G N H M K K2 K1` — **la misma** |
| primer `TXI` (21:56:18), ya en `B9` | `B9 C P G N H M K K2 K1` — **otra, y peor**: manda volver a `C` |

De ahí las dos decisiones: el recálculo va **en el punto de inicio** (rodando, el grafo propone
volver atrás) y **solo se avisa si la propuesta cambia** (reabrir el popup para proponer lo mismo
sería fricción con el avión ya en movimiento).

### Replay del rodaje real (`RaasReplayTests`)

Pedido del mantenedor: «con los datos del pirep, haz un test y muéstrame los mensajes que saldrían
en el rodaje, desde el pushback hasta el takeoff roll». El test mete la traza real del
`MNjR664PBAr25RbD` (SKBO, 14R) por el mismo camino que la app —569 segmentos de calle, 35
hold-shorts, 4 pistas, las **72 líneas `SCH` del log completo** y las 42 posiciones del pushback,
el rodaje y la entrada en pista— y vuelca la secuencia a `%TEMP%\raas_replay_MNjR664.txt` (78
líneas). Las filas `CHK` de scoring se dejan fuera a propósito: no son posiciones nuevas —van
pegadas a un `TXI` o un `PBT`— y la app evalúa sobre telemetría cruda a 1 Hz, no sobre lo que se
envía a phpVMS.

El test no solo vuelca: **exige la secuencia de avisos**. Con los arreglos de v0.9.15 son cinco, en
este orden —`TurnAhead` (la K2 a 220 m), `HoldShortApproaching`, `HoldShortStop`, `HoldShortStop`
(la insistencia al reanudar tras 135 s de espera) y `RouteComplete`— y ni uno más.

Lo que sale, con la ruta que el grafo propuso (`C P G N H M K K2 K1`):

| Hora | Aviso | Realidad |
|---|---|---|
| 21:56:18–21:59:18 | `FUERA DE RUTA, VUELVE A CALLE B` ×8 | el avión rodaba por `B`, que no está en la ruta sugerida |
| 22:07:48 | `CALLE K2 A LA DERECHA EN 220 METROS` | correcto: el avión siguió por `K1` |
| 22:08:19 | `APROXIMANDO PISTA 14R` | correcto |
| 22:08:49 | `ESPERA ANTES DE PISTA 14R` | el freno se puso 14 s después y estuvo **135 s** |
| 22:09:19 | `RUTA DE RODAJE COMPLETA` | **prematuro**: faltaba entrar en pista |
| 22:11:49 | `ESPERA ANTES DE PISTA 14R` | repetido al reanudar, correcto |
| 22:12:19 | `RUTA DE RODAJE COMPLETA` | otra vez antes de `ENTRANDO PISTA 14R` (22:12:32) |

Esos dos fallos —el desvío que no era desvío y el «ruta completa» antes de la pista— están
**corregidos en v0.9.15** y el propio test los vigila:

1. **La ruta sugerida es la más corta por grafo, no la de ATC.** El grafo propuso `C P G N H M K K2
   K1` y el piloto hizo `C B9 B M K K1 V`: el mismo destino por otras calles. La comparación por
   nombre sigue siendo la única señal disponible, así que lo que cambió es **cuándo se avisa**:
   fuera de ruta 15 s sin acercarse a la pista. En el rodaje real el avión se acercaba todo el
   tiempo, así que ahora no dice nada, y el test lo exige (`Assert.IsFalse(... FUERA DE RUTA)`).
2. **`RUTA DE RODAJE COMPLETA` significa «estoy dentro de la pista»** (`OnRunway`), una sola vez
   por guía. En el volcado corregido sale a las 22:12:50 —el primer muestreo dentro de la pista,
   que en la app a 1 Hz es el mismo momento del `ENTRANDO PISTA 14R` de 22:12:32—.

Con los dos arreglos, la secuencia de avisos del rodaje real queda en **cinco avisos de cuatro
situaciones**: `CALLE K2 A LA DERECHA EN 220 METROS`, `APROXIMANDO PISTA 14R`, `ESPERA ANTES DE
PISTA 14R` (×2, antes y después de la espera de 135 s) y `RUTA DE RODAJE COMPLETA` al entrar en
pista. El `RUTA DE RODAJE COMPLETA` sale a las 22:12:50, que es el primer muestreo dentro de la
pista: el log marca `ENTRANDO PISTA 14R por CALLE V` a las 22:12:32 y en la app, que evalúa a
1 Hz, el aviso habría sonado en ese mismo momento.

Cautela al leer el volcado: la app evalúa a **1 Hz**, la traza solo tiene una posición cada 30 s (y
ninguna durante el pushback, porque `EmitTaxiPosition` no se llama en esa fase). El orden de los
avisos es el real; las horas son más gruesas que en vuelo.

## API phpVMS — endpoints verificados

Comprobado en vivo contra la instalación de producción, no deducido de la documentación.

| Endpoint | Estado | Notas |
|---|---|---|
| `GET api/pireps/{id}` | 200 | Ficha del PIREP, incluye `state`, `arr_airport_id`, `diversion-airport` |
| `GET api/pireps/{id}/acars` | **404** | `The route api/pireps/{id}/acars could not be found`. La documentación de phpVMS lo menciona, esta instalación no lo tiene |
| `GET api/pireps/{id}/acars/position` | **200** | La traza completa. Con el PIREP `MNjR664PBAr25RbD` (SKBO→MMGL, ya fileado, `state=2`): **967 entradas / 1,8 MB en ~2 s**, con lat/lon, rumbo, GS, MSL, VS, IAS, fase y `status` |
| `POST api/pireps/{id}/acars/position` | — | Es el que usa el cliente para enviar cada posición |
| `PUT api/pireps/{id}` | 200 | Aceptado mientras el PIREP está `state=0`; un PIREP `Accepted` (`state=2`) lo rechaza con 503 |
| `PUT/DELETE api/pireps/{id}/cancel` | — | Cancelación |
| `POST api/pireps/{id}/file` | — | Fileado; `diversion-airport` solo se procesa si la clave **está presente** |
| `api/airports/nearest` | **404** | No existe en esta instalación; el fallback que lo usaba se eliminó en v0.9.8 |
| `PUT api/user` | **405** | No soportado: phpVMS reubica al piloto por su cuenta al procesar `diversion-airport` |

**`status` de las entradas ACARS** (códigos que envía el cliente, mapeados en
`Helpers/FlightPhaseHelper.cs`): `INI` inicial, `BST` boarded, `SCH` apunte de log, `PBT`
pushback, `TXI` rodaje, `TOF` despegue, `ICL` ascenso inicial, `ENR` crucero, `APR` aproximación,
`FIN` final, `LDG` aterrizaje, `ARR` aparcado, `CHK` checkpoint de scoring (`SC:ov=…,ts=…`, cada
60 s, y es el que el "resume" usa para restaurar penalizaciones).

**El historial NO llega ordenado.** `/acars/position` entrega primero todos los `CHK` y después las
posiciones, y dentro de cada grupo tampoco respeta la hora. Lo que asuma "el más nuevo al final"
tiene que ordenar por `created_at`; se hace en `ApiService.GetPirepAcarsAsync`, en la frontera de la
API, desde v0.9.13 — antes de eso el resume restauraba el checkpoint de las 22:43 en vez del de las
02:52.
## Requisitos de Instalación

1. **Simulador**: ver tabla de compatibilidad

| Simulador | Plugin requerido |
|---|---|
| MSFS 2020 / 2024 | FSUIPC 7 |
| Prepar3D v5 / v6 | FSUIPC 6 |
| Prepar3D v4 | FSUIPC 5 o 6 |
| Prepar3D v1 / v2 / v3 | FSUIPC 4 |
| FSX / FSX: Steam Edition | FSUIPC 4 |
| X-Plane 11 / 12 | XUIPC (plugin para X-Plane) |

2. **pdfium.dll** (x64): incluido en el build (`PdfiumViewer.Native.x86_64.no_v8-no_xfa`)
3. **Plataforma destino**: x64 (`Prefer32Bit=false` en el proyecto)
4. **.NET Framework 4.8** en el sistema

---

## Notas de Build

- Configuración **Release**: `<AppConfig></AppConfig>` en el PropertyGroup de Release evita que MSBuild copie `App.config` sobre `vmsOpenAcars.exe.config`. El `App.Release.config` de producción en `bin\Release\` queda intacto.
- `pdfium.dll` se copia siempre al directorio de salida (`CopyToOutputDirectory=Always`)
- `Languages/*.json` se copian con `PreserveNewest`
- **No hay código de simulación.** `Services/MockSimulator.cs` y `LandingLogService.SeedMockData()`
  (con su botón SEED DEMO DATA en el LOGBOOK) se retiraron en v0.9.8: el binario distribuible no
  contiene datos de vuelo falsos ni rutas de prueba.

#### Credenciales: dev vs. distribución (v0.9.3)
Los dos archivos de configuración tienen roles **distintos y deliberados**:

| Archivo | Rol | ¿Lleva credenciales? |
|---|---|---|
| `App.config` | Configuración local del desarrollador. MSBuild la copia al `exe.config` de Debug. | **Sí** — es el entorno propio, para no reconfigurar la app en cada compilación |
| `App.Release.config` | Plantilla que se publica a los pilotos (ver `Docs/PRIMEROS_PASOS.md`). | **No** — solo placeholders |

`App.Release.config` **no se aplica automáticamente** en ningún build (`<AppConfig></AppConfig>`
en Release impide que `App.config` lo pise). Al preparar un paquete para publicar hay que
copiarlo sobre el `vmsOpenAcars.exe.config` del paquete:

```
copy App.Release.config  <carpeta-del-paquete>\vmsOpenAcars.exe.config
```

Sin ese paso, el paquete hereda la configuración del equipo que lo construyó — es decir, las
credenciales del desarrollador. Por eso `Helpers/AppConfig.cs` **no** debe tener credenciales
como valor por defecto: un default en código sobrevive aunque el despliegue limpie su `.config`.

### Binding Redirects y SQLite (v0.6.3)

El `.csproj` tiene `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>`, que hace que MSBuild genere redirects automáticamente analizando el árbol de dependencias. El problema: si el equipo del desarrollador no tiene `System.Data.SQLite` en su GAC, el auto-generador no produce ninguna entrada para SQLite y, al volcar el resultado sobre el `exe.config` de salida, elimina el redirect manual que sí estaba en `App.config`.

La solución (añadida en v0.6.3) es `<GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>` en el mismo `<PropertyGroup>`. Con esta flag, los redirects auto-generados se escriben como un tipo de output separado y no sobreescriben el contenido manual del `App.config`, preservando el redirect de SQLite en todos los builds.

El redirect manual en `App.config` es:

```xml
<dependentAssembly>
  <assemblyIdentity name="System.Data.SQLite" publicKeyToken="db937bc2d44ff139" culture="neutral" />
  <bindingRedirect oldVersion="0.0.0.0-1.0.119.0" newVersion="1.0.119.0" />
</dependentAssembly>
```

Cubre cualquier versión anterior de SQLite que pueda estar registrada en el GAC del usuario (p. ej. 1.0.115.5 instalada por Visual Studio o SQL Server Tools) y la redirige a la 1.0.119.0 que se distribuye con vmsOpenAcars.

### Tests (v0.9.15)

`vmsOpenAcars.Tests/` — proyecto MSTest hermano de `vmsOpenAcars`, incluido en
`vmsOpenAcars.sln`. **248 tests** en ocho suites:

| Suite | Cubre |
|---|---|
| `ScoringServiceTests` | Los 17 criterios con sus umbrales en **ambos lados**, el bonus de single-engine, el suelo de 0 y los casos de "sin datos de aterrizaje" |
| `PirepStateTests` | La clasificación de estado de PIREP (`Pirep.IsActiveState`), que decide el fallback de `FilePirep()` cuando phpVMS archiva el PIREP pero devuelve un código no-2xx |
| `GeoMathTests` | Geometría flat-earth compartida (incluida `DistanceToSegmentNm` y su recorte en los extremos), el respaldo regional de TA/TL y la lectura de `NavAirportInfo` (v0.9.8, v0.9.9) |
| `AtcPanelTests` | Orden de presentación de las posiciones ATC (v0.9.8) |
| `ApproachThresholdTests` | "Está en final" (`SelectApproachThreshold`), el cono angular (`IsWithinFinalCone`), el gradiente de descenso (`IsPlausibleDiversionDescent`) y la regla de distancia (`IsPlausibleDiversionDistance`), con las coordenadas y altitudes exactas de dos vuelos reales: el falso SKTL, el SKCG legítimo y los tres falsos de la aproximación a KBOS (v0.9.9) |
| `RouteCorridorTests` | El corredor de la llegada planificada sobre el navlog real de SimBrief de un SKRG→SKBQ, incluido que la llegada son los últimos tramos **por distancia** y no los marcados `is_sid_star` (v0.9.9) |
| `RaasTests` | RAAS y guía de rodaje sobre **106 segmentos reales de SKBO** y el rodaje del `MNjR664PBAr25RbD`: ruta por grafo hasta la 14R, parseo de la ruta editable, próximo giro y su lado, los cinco puntos reales frente al hold-short, y las reglas de «fuera de ruta» (insistencia + acercarse a la pista) y «ruta completa» (solo dentro de la pista, una vez) de v0.9.15 |
| `RaasReplayTests` | **Reproducción completa del rodaje real** del `MNjR664PBAr25RbD` (SKBO, 14R): 569 segmentos, 35 hold-shorts, 4 pistas, el log `SCH` completo y las 42 posiciones muestreadas del pushback al takeoff roll, alimentadas al mismo `Evaluate` que usa la app a 1 Hz. Vuelca la **secuencia de avisos** a `%TEMP%\raas_replay_MNjR664.txt` y exige que sean los cinco correctos. Además fija la ruta que propone el grafo desde el puesto, el fin del pushback, el arranque del rodaje y el primer `TXI`, que es lo que decide si el popup vuelve a salir (v0.9.14, v0.9.15) |

`InternalsVisibleTo("vmsOpenAcars.Tests")` en `Properties/AssemblyInfo.cs` da acceso a los
tipos `internal` (los helpers) sin tener que hacerlos públicos solo para probarlos.

```bash
msbuild vmsOpenAcars.sln /p:Configuration=Debug
vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
```

`ScoringService` vive en un `WinExe`, así que el proyecto de tests lo enlaza de dos formas:

| Mecanismo | Propósito |
|---|---|
| `ProjectReference` con `ReferenceOutputAssembly=false` | Solo fuerza el **orden** de compilación: la app debe estar construida antes que los tests |
| `<Reference>` a `..\vmsOpenAcars\bin\$(Configuration)\vmsOpenAcars.exe` con `Private=true` | Copia el assembly al output de tests para que el host de test lo resuelva **en runtime** |

En **Release** el proyecto de tests no se compila: la solución mapea su configuración
Release a `ActiveCfg = Debug` sin `Build.0`, de modo que nunca entra en el paquete
distribuible.

Convenciones:

- Cada frontera se comprueba en sus **dos lados** (`≤150` penaliza 0 y `151` penaliza 5):
  es donde un cambio de `<` por `<=` pasa desapercibido.
- Cada test parte de un vuelo perfecto (score 100, cero deducciones) y añade **una sola**
  violación, para que la deducción observada sea atribuible a ese criterio.
- `AllCriteria_AreIndividuallyAttributable` exige 17 líneas de desglose únicas: es el test
  de regresión del bug de contador compartido entre STD y QNH.

Al añadir un criterio a `ScoringService` hay que tocar cuatro sitios: el cálculo, su test,
`PirepBuilder._critKeyMap` y la clave `Score_Crit*` en `Languages/{en,es}.json`.
