# vmsOpenAcars — Guía para Claude

## Proyecto

Cliente ACARS de escritorio (Windows Forms, .NET 4.8, C# 7.3) que conecta simuladores de vuelo con aerolíneas virtuales basadas en phpVMS v7. Lee datos del simulador vía FSUIPC/XUIPC y los envía a la API REST de phpVMS.

**Versión actual:** v0.7.5  
**IDE:** Visual Studio 2017 (compilar siempre desde el IDE, nunca desde CLI)

## Stack

- **FSUIPC** (FSUIPCClientDLL 3.3.16) · **NAudio** (2.3.0) · **Newtonsoft.Json** · **GMap.NET**
- **System.Data.SQLite** (1.0.119) — `landing_log.sqlite` + `NavData_cache.sqlite` + LittleNavMap BD
- **phpVMS v7** — backend REST · **SimBrief** — planes de vuelo / OFP

---

## Scoring — `Services/ScoringService.cs`

14 criterios + 1 bonificación. Parte de 100, deduce (mín 0), luego bonus (máx 100):

| Criterio | Máx | Umbrales / condición |
|---|---|---|
| Landing Rate | 40 | ≤150=0, ≤250=5, ≤350=15, ≤450=25, ≤650=35, >650=40 |
| G-Force | 15 | ≤1.5g=0, ≤1.7g=7, >1.7g=15 (omitido si dato=0) |
| Bank Angle | 10 | ≤2°=0, ≤5°=5, >5°=10 |
| Pitch Angle | 10 | 1°–7°=0; <−2°=10; −2°–1°=5; >8°=5 |
| Overspeed | 15 | 0=0, 1=7, ≥2=15 |
| Lights Compliance | 10 | 5 pts/violación cap 10; Beacon exempto en `BeaconStrobeSharedAircraft` (DH8D) |
| Stabilized Approach 1000 ft | 15 | speed±Vref=−5, VS<−1000=−5, VS>−100=−5, bank>7°=−3, pitch±límites=−3, gear up=−5, flaps<50%=−4 |
| QNH Compliance | 10 | Δ>2 hPa=−5: salida vs METAR origen; climb vs STD 1013 (tras TA); llegada vs QNH (bajo TL) |
| IVAO Offline | 5 | −5 si desconectado al iniciar TaxiOut |
| On-Time Departure | 5 | −5 si Blocks Off difiere >10 min de `sched_out` |
| Touchdown Zone | 7 | ≤1500 ft=0, ≤2500=3, >2500=7 — activo si `TouchdownDistanceFt>0` |
| Centreline Deviation | 7 | ≤10 ft=0, ≤30=3, >30=7 — activo si `CenterlineDeviationFt>0` |
| Localizer Alignment | 5 | ILS not tuned=−3; heading>5°=−1 each (cap 2). Omitido si NAV1 difiere >0.05 MHz del ILS esperado a 1000 ft AGL |
| Minimums Compliance | 5 | −5 si `BelowMinimums=true`. Omitido si Localizer fue omitido |
| **Single Engine Taxi** | **+5** | Multi-motor ≥50% de movimiento con un motor en TaxiOut o TaxiIn |

---

## NavData

### NavDataService — `Services/NavDataService.cs`

```csharp
void PrefetchAirport(icao)
RunwayTouchdownResult FindTouchdownRunway / FindTakeoffRunway / GetRunwayThreshold(airport, lat, lon, heading)
RunwayEntry    FindRunwayEntry(airport, lat, lon, heading)
string         FindNearestTaxiway(airport, lat, lon, heading)       // penaliza ×2.5 segmentos >50°
double         FindTaxiwaySegmentBearing(airport, taxiwayName, lat, lon)
HoldingPoint   FindHoldingPoint / ParkingSpot FindNearestParking
IlsData        GetIlsForRunway / ApproachInfo GetApproachType / IList<ApproachFix> GetApproachFixes
(double DistNm, double LateralFt) ComputeApproachMetrics(...)  // static
```

**Geometría flat-earth** (`ProjectOnRunway`/`WithinFootprint`):
- `along = dE·sin(bearing) + dN·cos(bearing)` → dist al umbral; `Math.Max(0,…)` es crítico
- `cross = dE·cos(bearing) - dN·sin(bearing)` → desviación centreline
- `bearing_rad` es el **bearing geográfico verdadero** via `TrueRunwayBearing(rwy)` (threshold→end WGS-84). Usar `rwy.Heading` (magnético) produce hasta 600 ft de error en aeropuertos con variación ≥13° (casos TJSJ −14°W, SKBO crab 3°).

### NavDataClient — `Services/NavDataClient.cs`

```csharp
static bool IsReachable, IsKeyValid, IsAiracExpired
static void PrefetchAirport(icao)
static List<NavRunway/Taxiway/Parking/HoldShort/Approach> Get*(icao)
static NavAirportInfo     GetAirportInfo(icao)          // transition_altitude/level_ft → double?
static List<NavProcedure> GetSids / GetStars(icao)
static List<NavIls>       GetIls(icao)
static List<NavAirportWaypoint> GetAirportWaypoints(icao, radiusNm)
static Task<List<NavAirspace>>  GetAirspacesAsync(lat, lon)   // sin radius_nm; servidor devuelve 200 nm fijos
static Task<NavApiTestResult>   TestApiAsync(apiKeyOverride)  // llama NavDataCache.SyncAirac()
static Task<BriefingCheckResult> CheckAnnouncementAsync(phase, lang)
static Task<byte[]>              FetchBytesAsync(path)
static Task<NavWeather>          GetWeatherAsync(icao)        // TTL 5 min en memoria
```

Caché por capas: (1) `ConcurrentDictionary` en sesión por ICAO → (2) `NavDataCache` SQLite por AIRAC → para airspaces: (3) `_airspaceMemCache` en sesión + (4) `airspace_entries` SQLite TTL 7 días.

Auth: `X-API-Key` + `X-Origin-Domain` de `App.config` (`navdata_api_key`, `navdata_api_domain`).

### NavDataCache — `Services/NavDataCache.cs`

SQLite `NavData_cache.sqlite` junto al exe. Tres tablas:
- `airport_entries (icao, data_type PK, airac_cycle, json_data)` — data_type ∈ block/sids/stars/ils/waypoints
- `navaid_entries (cache_key PK, airac_cycle, json_data)`
- `airspace_entries (tile_key PK, cached_at, json_data)` — TTL 7 días, **no vinculado al AIRAC**

`SyncAirac(cycle, validUntil)` purga `airport_entries` y `navaid_entries` del ciclo anterior (no toca airspaces). `Initialize()` auto-purga todo si `airac_valid_until` expiró.

Tile key airspaces: `"{round(lat)}:{round(lon)}"` — bucketing a 1° para maximizar hits de caché.

### AirspaceMonitorService — `Services/AirspaceMonitorService.cs` (v0.6.9)

Monitorea espacios aéreos de la ruta activa e IVAO ATC/ATIS. Thread-safe; eventos en thread-pool.

```csharp
event Action<NavAirspace>                  OnAirspaceAlert    // Prohibited/Restricted/Danger
event Action<NavAirspace, NavAirspaceFreq> OnAirspaceEntered  // CTR/TMA/RMZ entrada
event Action<NavAirspace>                  OnAirspaceExited   // CTR/TMA/RMZ salida
event Action<IList<IvaoAtcStation>>        OnAtcUpdated       // poll IVAO completo (3 min)

Task InitRouteAsync(originIcao, destIcao)  // fetch origen(200nm) + dest(200nm) + midpoint si dist>100nm
void CheckPosition(lat, lon, altFt)        // ray-casting GeoJSON + límites verticales
void TriggerIvaoRefresh()
```

IVAO polling: `GET https://api.ivao.aero/v2/tracker/whazzup` → `root["clients"]["atcs"]`. Callsign `{ICAO}_{POS}` — match por ICAO exacto o prefijo 2 chars FIR. **`originIcao` y `destIcao` se añaden explícitamente a `_relevantIcaos`** (v0.6.7) — garantiza que TWR/GND/DEL locales siempre se capturan incluso si NavData no devuelve ningún airspace cuyo `ExtractIcao()` coincida.

**Integración MainViewModel (v0.6.9):** `InitRouteAsync` dispara tanto en `StartFlight()` como en `SetActivePlan()` (background Task) con posición inicial del avión → airspaces + ATC visibles en cuanto se carga el OFP. `CheckPosition` + `UpdateAircraftState` throttleado a 30 s en `OnRawDataUpdated`. `TriggerIvaoRefresh()` en fases Descent y Approach. `Reset()` en los 3 exit paths. `PollIvaoAsync` aplica 3 filtros: suppressión de duplicados consecutivos, distancia (150 NM / 80 NM en approach), y priorización por fase (solo destino + APP/DEP en approach).

**MapForm.SetAirspaces:** `GMapPolygon` por tipo — opacidades al 50% respecto a v0.6.6. Prohibited=rojo(20,220,0,0 / 95,200,0,0), Restricted=naranja, Danger=amarillo, CTR=cyan, TMA=azul, ATZ=azul claro, RMZ=violeta. GeoJSON `[lon,lat]` → `PointLatLng(lat,lon)`.

**MapForm.SetAtcStations (v0.6.7):** formas geográficas `GMapPolygon` estilo WebEye — radio 20 nm, escalan con el zoom:
- TWR → círculo, borde rojo (170,220,50,50), relleno rojo muy bajo (30,220,50,50)
- GND → estrella 4 puntas alineada N/S/E/W, amarillo (170,220,190,0)
- DEL → estrella 4 puntas rotada 45°, naranja (170,255,130,0)
- Puntas de GND rozan el borde del círculo de TWR (mismo radio 20 nm)
- `AtcLabelMarker` centrado en ARP: texto ICAO 7pt Consolas Bold + shadow 4px + dot 4px
- APP/CTR/DEP/FSS → `AtcStationMarker` text-box sin cambios
- Helpers: `MakeCirclePolygon(lat, lon, radiusNm, fill, stroke, n=72)` / `MakeStarPolygon(lat, lon, outerNm, innerRatio, startDeg, fill, stroke)`

`IvaoAtcStation` (en `AirspaceMonitorService.cs`): `Callsign`, `Icao`, `Position`, `Frequency`, `AtisLines`. DTOs airspace en `Models/NavData.cs`: `NavAirspace`, `NavAirspaceLimit`, `NavAirspaceGeometry`, `NavAirspaceFreq`.

---

## CabinAnnouncementService — `Services/CabinAnnouncementService.cs` (v0.5.9)

Anuncios pregrabados: fetch `/briefing/check/` + `/briefing/download/`, caché `%TEMP%\vmsacars\briefing\`, reproducción FIFO chime WAV → MP3.

**Idioma:** `Pilot.AirlineCountry` (ISO-2) → `SpanishCountries` hashset → `"es"` o `"en"`. Internacional con aerolínea no anglohablante → inglés primero + nativo. Doméstico → solo nativo.

**Supresión:** `aircraftSeats ∈ (0,40)` → `PrefetchAsync` retorna sin hacer nada.

**Reproducción:** NAudio `AudioFileReader` + `WaveOutEvent` + `ManualResetEventSlim`. `_currentOutput`/`_currentReader` (volatile) para stop en tiempo real y volumen en caliente.

**Fases:**

| Fase | Trigger |
|---|---|
| `boarding` | `PrefetchAsync()` completado en `StartFlight()` |
| `taxi_out` / `top_of_descent` / `approach` / `taxi_in` | `OnFlightPhaseChanged` |
| `on_runway` | `LandingLight` o `StrobeLight` changed(on=true), GS ≤ 40 kt, una vez |
| `cruise` | Enroute + AGL > 10 000 ft sostenido 30 s |

**Settings:** `chkCabinAnnouncements` (live, auto-save), `trkCabinVolume` (0–100, live, auto-save), `btnTestCabin` (7 fases). Callbacks inyectados desde MainForm. Clave App.config: `cabin_announcements_enabled` / `cabin_announcements_volume`.

---

## SystemInfoHelper — `Helpers/SystemInfoHelper.cs` (v0.6.6)

```csharp
static string OsSummary   // "{ProductName} / RAM {n} GB" — detecta Win11 por BuildNumber ≥22000
static string CpuSummary  // "{ProcessorNameString} / {ProcessorCount} threads"
static string GpuSummary  // "{DriverDesc} / VRAM {n} GB"  (o "VRAM ?")
static string SimSummary  // asignado en SetSimVersion() al conectar FSUIPC
```

**GPU** (`GetBestGpu`): registro `HKLM\...\{4d36e968...}`. Rango: NVIDIA=3, AMD/Arc=2, otros=1, Intel=0. Filtra adaptadores virtuales. Fallback DXGI COM P/Invoke (`CreateDXGIFactory`) cuando VRAM del registro=0 — necesario para GPUs ≥4 GB (DWORD overflow) y Optimus Render-Only.

**CPU** (`GetCpuString`): `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0\ProcessorNameString` + `Environment.ProcessorCount`.

**Uso en StartFlight:** batch ACARS de 5 entradas `log` → phpVMS: versión, CPU, GPU, OS, NavData AIRAC.
**Prefile:** `GetPrefileNotes()` → campo `notes` phpVMS (versión + OS + GPU + Sim).

---

## OSD Overlay — v0.5.4

TopMost, click-through, centrado en pantalla configurada, 40 px desde borde. Thread-safe. Audio: `OsdAudio.Play(severity)`, 4 WAV EmbeddedResource.

App.config: `osd_enabled`, `osd_sound_enabled`, `osd_duration_seconds` (def 4), `osd_screen_index`, `osd_opacity` (def 90).

**Triggers OSD (MainViewModel → OnOsdMessage):**

| Momento | Texto | Sev |
|---|---|---|
| StartFlight ok | `ACARS ACTIVE` | Success |
| TaxiOut / TakeoffRoll / Cruise / Descending / Approach / OnBlock | texto de fase | Info |
| Touchdown | `<calificación>  −XXX fpm  X.Xg` | según fpm |
| PIREP filed | `PIREP FILED — SCORE: XX/100` | Success |
| Climb ≥10 000 ft AGL | `10 000 FT` | Info |
| Descent ≤10 500 ft luces apagadas | `LANDING LT OFF` | Warning |
| Penalty lights | `PENALTY  <LT>  −5 PTS` | Warning |
| TA / TL | `TRANS ALT SET STD 1013` / `TRANS LEVEL SET QNH` | Warning |
| Overspeed | `OVERSPEED  XXX KTS` | Critical |
| Unstabilized | `UNSTABILIZED  −N PTS` | Critical |
| Single engine taxi | `SINGLE ENGINE TAXI  +5 PTS` | Success |
| Airspace Prohibited/Restricted/Danger | `AIRSPACE  {TYPE}  {ICAO}` | Critical |
| Airspace CTR/TMA/RMZ entrada | `{TYPE}  {ICAO}  {freq} MHz` | Info |

---

## Landing Analysis

SQLite `landing_log.sqlite`. Tablas: `flights` (1/vuelo) + `approach_track` (puntos 2 s, AGL<3000 ft).

**Orden crítico en SendPirep:**
```
SnapshotLandingRecord()   ← ANTES de await FilePirep — captura plan + touchdown
await FilePirep()         ← llama ResetFlightState() → _activePlan=null, touchdown data=0
éxito → record.Score = LastFlightScore  ← única propiedad que NO se resetea
      → LandingLogService.SaveFlight(record, _approachBuffer)
```

`LandingAnalysisForm`: 4 gráficos VERTICAL/LATERAL/IAS/VS, eje X 5NM→0. Suavizado Gaussiano (window=7) en LATERAL/IAS/VS, no en VERTICAL.

---

## ILS / Approach Detection — v0.4.4

Carga al entrar en fase Approach → `Task.Run(LoadApproachData)` → `FlightManager.SetApproachData(ils, approach, fixes)`.

A 1000 ft AGL: compara `Nav1FrequencyMhz` vs `_expectedIls.FrequencyMhz` (±0.05 MHz).
- Coincide → ILS confirmado, scoring normal.
- No coincide → anula ILS, omite Localizer + Minimums sin penalizar (`Log_IlsApproachSkipped`).

Bajo 500 ft AGL: `_localizerViolations++` si |hdgDelta|>5° (cap 2). Check DA para `_belowMinimums`.

FsuipcService offsets: `0x0350 INT16` NAV1 freq BCD (`100+d3×10+d2+d1×0.1+d0×0.01`), `0x0C4E INT16` NAV1 OBS.

---

## Detección de fases — umbrales

| Transición | Condición | Debounce |
|---|---|---|
| Climb → Enroute | VS<200 fpm + cerca crucero, o timeout 5 min + VS<100 fpm | 10 s |
| Climb/Enroute → Descent | VS<−500 fpm **y** alt<máx−500 ft | 20 s |
| Enroute → Climb step | VS>500 fpm + alt<crucero−500 ft | 10 s |
| Descent → Approach | dist<10% totalDist o AGL<aglThreshold | inmediato |
| Approach → Go-around | VS>600 fpm + AGL 100–3000 ft + ≥30 s en Approach | 10 s |

Umbrales elevados evitan falsas transiciones por cambios de QNH o turbulencia leve (~100 fpm).

---

## Referencias de archivos clave

| Archivo | Contenido relevante |
|---|---|
| `Core/Flight/FlightManager.cs` | `CheckStabilizedApproachGate` / `CheckApproachBelowGate`; `CheckViolations` (TA/TL/QNH/10k ft); `SetRunwayTouchdownData`; `SetApproachData`; `SetOriginTransitionAlt/SetDestTransitionLevel`; `TransitionTo` (log fase); `SetResumedPenalties` (resume v0.6.4); `BeaconStrobeSharedAircraft` (DH8D beacon exemption) |
| `Services/NavDataService.cs` | `ProjectOnRunway` + `WithinFootprint`; `TrueRunwayBearing`; `FindTaxiwaySegmentBearing`; `NextIntersection` |
| `Services/NavDataClient.cs` | `LoadAirportAsync` (6 endpoints paralelos); `GetAirspacesAsync` (sin radius_nm, caché 2 capas); `GetWeatherAsync` (TTL 5 min) |
| `Services/NavDataCache.cs` | `CreateSchema` (3 tablas); `TryGetAirspace/StoreAirspace` (TTL 7 días); `SyncAirac` (purga airport+navaid, no airspaces) |
| `Services/AirspaceMonitorService.cs` | `InitRouteAsync` (v0.6.9: acepta initLat/initLon); `CheckPosition` (ray-casting GeoJSON); `PollIvaoAsync` (whazzup + filtrado duplicados/distancia/fase, v0.6.9); `UpdateAircraftState` (v0.6.9); `FilterAtcStations` (v0.6.9); timer 3 min |
| `Services/FsuipcService.cs` | Hold debounce 2.5 s luces (v0.6.7): `_pendingXxxState/At` — nuevo estado estable ≥2.5 s antes de disparar evento; elimina falsos positivos por parpadeo ~1.6 s del sim |
| `Services/CabinAnnouncementService.cs` | `PrefetchAsync`; cola FIFO; NAudio playback; `TestAnnouncementAsync` |
| `Models/NavData.cs` | `NavAirspace`, `NavAirspaceGeometry` (GeoJSON [lon,lat]), `NavAirspaceFreq`; `BriefingCheckResult` |
| `ViewModels/MainViewModel.cs` | `WireAirspaceMonitor`; `StartFlight` + `SetActivePlan` (v0.6.7: airspace init en ambos); `GetAircraftCategory()` (v0.6.7: lee `FsuipcService.EngineCategory`); `HandleTaxiPositionUpdate` (criterio angular 25°); `SnapshotLandingRecord` → `SaveLandingRecord`; `SendScoringCheckpointAsync` (CHK 60 s, v0.6.4); `ResumeFromAcarsHistoryAsync` (v0.6.4); `UpdateAircraftState` (v0.6.9: posición + fase para filtrado IVAO) |
| `UI/Forms/MapForm.cs` | `LoadRoute` (ruta suavizada, SID/STAR virtual); `BuildSidebar` (procedimientos, v0.6.5; link APPROACH CHART v0.6.8); `DrawApproachOverlay`; `SetAirspaces` (polígonos GeoJSON, opacidad 50%, v0.6.7); `SetAtcStations` (formas geográficas TWR/GND/DEL, v0.6.7); `SetAircraftCategory` (icono por categoría A-D, v0.6.7); capas toggleables TILES/ROUTE/SPACES/IVAO (CheckBox barra inferior, v0.6.7) |
| `UI/Forms/ApproachChartForm.cs` | Carta de aproximación dinámica GDI+ (v0.6.8). Plan view: north-up, legs, arcos AF (DrawDmeArc), símbolos IAF/FAF/MAP. Profile view: glideslope naranja (ils_gs), glidepath verde (vnav_path), advisory punteado, escalera (null); DA/MDA rojo. Datos: `NavDataClient.PrefetchAirport` → GetApproaches/GetIls/GetRunways/GetAirportInfo. Se abre desde `OpenApproachChart()` en MapForm |
| `Helpers/SystemInfoHelper.cs` | `GetBestGpu` (DXGI fallback, rango 0–3); `GetCpuString` (registro + ProcessorCount) |
| `Services/ScoringService.cs` | TDZ + Centreline ~213; Localizer + Minimums ~247 |
| `vmsOpenAcars.csproj` | `GenerateBindingRedirectsOutputType=true` — impide sobreescribir binding redirect manual de SQLite |

---

## Próximas áreas

- **Touch-and-go real** — scoring y approach buffer deben resetearse para el segundo aterrizaje.
- **MetarRaw en logbook** — `FlightRecord.MetarRaw` existe pero no se popula en `SnapshotLandingRecord`.
- **TA/TL fallback regional** — cuando NavData devuelve `null`, sin OSD ni check STD.
- **Panel ATC/ATIS detallado** — las formas geográficas ya muestran tipo y frecuencia vía tooltip; podría añadirse un panel lateral persistente con lista de todas las posiciones activas y ATIS completo.
- **Approach chart — leg CI/PI/FA** — tipos de leg sin coordenadas propias (course-to-intercept, etc.) actualmente omitidos; podrían renderizarse desde el fix anterior + curso.
