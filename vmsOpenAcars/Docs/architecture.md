# vmsOpenAcars — Documentación de Arquitectura

> Versión del documento: 0.7.6  
> Última actualización: 2026-06-24

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
├── Controls/               Controles visuales personalizados (gauges, engine monitor)
├── Core/
│   ├── Flight/             FlightManager, FlightTimer
│   └── Helpers/            AppInfo
├── Db/                     Solo tipos resultado (RunwayTouchdownResult, IlsData, ApproachInfo…)
├── Docs/                   BRIEFING.md (guía usuario), architecture.md
├── Helpers/                AppConfig, Constants, FlightPhaseHelper, L (localización), UnitConverter, SystemInfoHelper
├── Languages/              en.json, es.json
├── Models/                 Aircraft, Flight, Pirep, SimbriefPlan, FlightPhase,
│                           FlightScoreData, TouchdownData, TakeoffData,
│                           FlightRecord, ApproachTrackPoint
├── Properties/             AssemblyInfo, Resources, Settings
├── Services/               ApiService, FsuipcService, ScoringService, MetarService,
│                           IvaoService, SimbriefEnhancedService, LandingLogService,
│                           NavDataService, NavDataClient,
│                           CabinAnnouncementService
├── UI/
│   ├── Forms/              MainForm, FlightPlannerForm, OFPViewerForm, SettingsForm,
│   │                       MetarDecodeForm, EcamDialog,
│   │                       FlightHistoryForm, LandingAnalysisForm,
│   │                       OsdOverlayForm, MapForm
│   └── Theme.cs            Paleta de colores centralizada
└── ViewModels/             MainViewModel
```

---

## Arquitectura General

El proyecto sigue un patrón **MVVM ligero**:

```
MainForm.cs  (Vista — WinForms)
    │
    └── MainViewModel.cs  (ViewModel — coordinación, eventos, lógica de botones)
            │
            ├── FlightManager.cs          Máquina de estados del vuelo
            ├── FsuipcService.cs          Polling del simulador (FSUIPC/XUIPC)
            ├── ApiService.cs             HTTP client → phpVMS REST API
            ├── PhpVmsFlightService.cs    Vuelos, bids, PIREPs
            ├── SimbriefEnhancedService.cs Plan de vuelo + OFP PDF
            ├── WeatherService.cs         QNH vía METAR
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

Núcleo de la lógica del vuelo. Recibe `RawTelemetryData` cada ciclo y:

1. Actualiza propiedades públicas (altitud, GS, VS, luces, motores…)
2. Aplica **debounce de luces** (2 s) antes de actualizar los campos internos usados en compliance
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

### ScoringService

Calcula un score de 0–100 al finalizar el vuelo. El score comienza en 100 y se aplican deducciones:

| Criterio | Máx. deducción | Escala |
|---|---|---|
| Landing Rate | −40 pts | ≤150 fpm: 0 / ≤250: −5 / ≤350: −15 / ≤450: −25 / ≤650: −35 / >650: −40 |
| G-Force touchdown | −15 pts | ≤1.5g: 0 / ≤1.7g: −7 / >1.7g: −15 |
| Bank Angle touchdown | −10 pts | ≤2°: 0 / ≤5°: −5 / >5°: −10 |
| Pitch Angle touchdown | −10 pts | 1°–5°: 0 (ideal) / fuera de rango: −5 a −10 |
| Overspeed | −15 pts | 0 eventos: 0 / 1: −7 / ≥2: −15 |
| Lights Compliance | −10 pts | −5 pts por violación, cap −10 |
| Stabilized Approach (1000 ft) | −15 pts | Evalúa speed, VS, bank, pitch, gear y flaps a 1000 ft AGL |
| QNH Compliance | −10 pts | −5 pts si Δ > 2 hPa — salida (TakeoffRoll) + llegada (gate 1000 ft AGL), independientes |
| IVAO Offline | −5 pts | −5 si el vuelo se realizó sin conexión IVAO |
| On-Time Departure | −5 pts | −5 si Blocks Off difiere > 10 min del STD (`sched_out`) |
| Touchdown Zone | −7 pts | ≤1500 ft = 0 / ≤2500 ft = −3 / >2500 ft = −7 · requiere NavData API |
| Centreline Deviation | −7 pts | ≤10 ft = 0 / ≤30 ft = −3 / >30 ft = −7 · requiere NavData API |
| Localizer Alignment | −5 pts | ILS not tuned −3; heading >5° x2 max −2; cap −5 · requiere NavData API + ILS approach |
| Minimums Compliance | −5 pts | −5 si descenso bajo DA (threshold elevation + 200 ft) sin aterrizar |

Los criterios **Touchdown Zone** y **Centreline Deviation** solo se evalúan si `TouchdownDistanceFt > 0` (es decir, si NavDataClient pudo obtener datos de la API). Los criterios **Localizer Alignment** y **Minimums Compliance** solo se evalúan si se detectó un procedimiento ILS para la pista de aterrizaje.

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
void SeedMockData()             // solo disponible en #if DEBUG — 5 vuelos SKRG RWY 01
```

**Flujo de captura de aproximación:**

```
Phase → Approach
    → _approachBuffer.Clear(); _approachThreshold = null; _approachDestination = null
OnRawDataUpdated (cada 50 ms, fase = Approach)
    → si _approachThreshold == null:
         1. GetRunwayThreshold(plan.Destination, lat, lon, hdg)
               requiere heading-delta ≤15° AND |cross| ≤2 NM AND along<0
         2. si null → GetRunwayThreshold(plan.Alternate, lat, lon, hdg)  [v0.7.4]
               si encontrado → _approachDestination = alt
                             → log "⚠️ Approaching ALTERNATE — XXXX"
                             → FlightManager.SetEffectiveDestination(alt)
         3. si null en ambos → no captura, reintenta el siguiente ciclo
         al adquirir → _approachDestination = icao resuelto
                     → Task.Run(LoadApproachData(_approachDestination, runway))
                          → GetIlsForRunway() + GetApproachType() + GetApproachFixes()
                          → _flightManager.SetApproachData(ils, approach, fixes)
    → si AGL < 3000 ft && ≥ 2 s desde último punto:
         ComputeApproachMetrics(threshold, lat, lon) → (distNm, lateralFt)
         _approachBuffer.Add(ApproachTrackPoint)
SendPirep()
    → SnapshotLandingRecord()          ← captura plan + touchdown ANTES de FilePirep
    → FilePirep() → ResetFlightState() ← borra _activePlan y touchdown data
    → éxito → SaveLandingRecord(record)
        → record.Score = LastFlightScore  ← no se resetea en ResetFlightState
        → LandingLogService.SaveFlight(record, _approachBuffer)
        → _approachBuffer.Clear()
        → log de diagnóstico (éxito o motivo de fallo) (v0.4.9)
```

> `SnapshotLandingRecord()` debe ejecutarse **antes** de awaitar `FilePirep()`. `LastFlightScore`
> es la única propiedad que `ResetFlightState()` no borra, por lo que puede leerse después.

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
| `navdata_api_url` | _(vacío)_ | URL base del servicio NavData API |
| `navdata_api_key` | _(vacío)_ | API key del servicio NavData |
| `navdata_api_domain` | _(vacío)_ | Dominio de la aerolínea (cabecera `X-Origin-Domain`) |
| `landing_log_path` | _(vacío)_ | Ruta al archivo `landing_log.sqlite` |
| `osd_enabled` | true | Activa el overlay OSD |
| `osd_sound_enabled` | true | Activa los chimes del OSD (Info/Success/Warning/Critical) |
| `osd_duration_seconds` | 4 | Tiempo de visualización por notificación (s) |
| `osd_screen_index` | 0 | Índice de pantalla para el OSD (0 = primaria) |
| `osd_opacity` | 90 | Opacidad del OSD (10–100 %) |
| `cabin_announcements_enabled` | true | Activa los anuncios de cabina pregrabados |
| `cabin_announcements_volume` | 80 | Volumen de los anuncios (0–100 %) |

---

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
- `SeedMockData()` en `LandingLogService` solo compila en configuración **Debug** (`#if DEBUG`)

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
