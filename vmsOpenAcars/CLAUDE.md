# vmsOpenAcars — Guía para Claude

## Proyecto

Cliente ACARS de escritorio (Windows Forms, .NET 4.8, C# 7.3) que conecta simuladores de vuelo con aerolíneas virtuales basadas en phpVMS v7. Lee datos del simulador vía FSUIPC/XUIPC y los envía a la API REST de phpVMS.

**Versión actual:** v0.9.11  
**IDE:** Visual Studio 2017 (compilar siempre desde el IDE, nunca desde CLI)

## Stack

- **FSUIPC** (FSUIPCClientDLL 3.3.16) · **NAudio** (2.3.0) · **Newtonsoft.Json** · **GMap.NET** (2.1.7)
- **System.Data.SQLite** (1.0.119) — `landing_log.sqlite` + `NavData_cache.sqlite`
- **phpVMS v7** — backend REST · **SimBrief** — planes de vuelo / OFP

> **Credenciales — dev vs. distribución:**
> - `App.config` es la **configuración local de desarrollo**: contiene las claves del
>   entorno propio para no reconfigurar la app en cada compilación. No se distribuye.
> - `App.Release.config` es la **plantilla que se publica a los pilotos**: nunca debe
>   llevar credenciales, URLs de aerolínea ni rutas de máquina, solo placeholders.
> - `Helpers/AppConfig.cs` no debe tener credenciales como valor por defecto: un default
>   en código sobrevive aunque el despliegue limpie su `.config`.

---

## Reglas de trabajo

Escritas aquí porque **el repo es la única memoria que sobrevive a un cambio de modelo, de
sesión o de máquina**. Lo que no está en un archivo, no existe.

**Verificación**
- **Reproducir contra el servicio real antes de tocar código.** NavData, phpVMS y SimBrief se
  consultan en vivo (las claves están en `App.config`, que es local y no se distribuye). Un
  diagnóstico sin reproducción es una hipótesis, no un hecho.
- **Los tests de una corrección usan datos reales**: coordenadas, altitudes, rumbos y navlogs
  de vuelos del piloto. Nada de geometría inventada. `ApproachThresholdTests` y
  `RouteCorridorTests` son el patrón.
- **Si algo no se pudo verificar, decirlo explícitamente** y anotarlo en "Próximas áreas".
  Nunca presentar una corrección no validada con la misma confianza que una validada.
- **Correr build (Debug y Release) y la suite completa antes de dar algo por terminado.** La
  guía dice compilar desde el IDE; la verificación automatizada se hace con el `MSBuild.exe`
  de VS2017 desde la línea de comandos — mismo toolchain, sin la interfaz. Si aparece un fallo
  raro de binding redirects o de `SQLite.Interop`, probar desde el IDE antes de culpar al código.

**Diseño**
- **Un helper puro por regla, con su test.** Las decisiones (qué es un desvío, qué es estar en
  final, qué es estar en la llegada planificada) van a `internal static` sin red ni WinForms:
  `GeoMath`, `RouteCorridor`, `TransitionDefaults`, `AtcStationOrder` y los `*Plausible*` de
  `NavDataService` siguen ese patrón.
- **Un filtro nuevo no reemplaza a los otros.** Cada regla cubre un escenario distinto;
  documentar en el propio test qué decide cada una. El caso KOWD lo ilustra: solo lo para el
  cono angular, ni la distancia ni el gradiente.
- **Comentario en español explicando el *por qué*** —la causa raíz, con las cifras reales que
  la demuestran—, no el qué. El código ya dice qué hace.
- **Los filtros de plausibilidad degradan sin datos**: si falta el dato no bloquean, y deciden
  las otras reglas. Nunca suprimir una acción por una suposición.

**Idioma**
- `Languages/es.json` y `en.json` se mantienen **simétricos** (hoy 395 claves cada uno): toda
  clave que se añade o se quita va en los dos.
- **Los rótulos de `SettingsForm` se traducen por `_(clave)` con el propio texto en inglés como
  clave** (`CreateLabel("Duration (s)")` → *Duración (s)*). Una clave que falte **no cae al
  inglés**: `LocalizationService.GetString` devuelve `[[clave]]`, así que aparece literalmente
  `[[Airspace]]` en la pantalla. Al añadir una fila al formulario hay que añadir su clave a los
  dos `.json` (`Restricted zones` y `Stg_AirspaceOsdAlerts` lo son desde v0.9.10).
- Al añadir un criterio a `ScoringService`: su test, su entrada en `PirepBuilder._critKeyMap`
  y su clave `Score_Crit*` en ambos idiomas.

**Documentación**
- `Docs/CHANGELOG.md` es el **canónico**: cada entrada lleva causa raíz y las cifras que la
  demuestran. Se actualiza junto con `CLAUDE.md`, en el mismo cambio.
- `Docs/README.md` es el índice: un `.md` nuevo se lista ahí o queda huérfano.
- **`Docs/MEMORY.md` y `Docs/project_vmsOpenAcars.md` no son fuente de verdad** (son resúmenes
  de mantenimiento y se quedan atrás: `MEMORY.md` llegó a decir v0.9.2 con el cliente en
  v0.9.9). La verdad es `CLAUDE.md` + `Docs/CHANGELOG.md`.

**Git**
- **No commitear por iniciativa propia**: el mantenedor pide el commit.
- No commitear artefactos generados (PDF/HTML/PNG) ni `App.config`.

**Versionado**
- Publicar una versión toca **tres** atributos de `Properties/AssemblyInfo.cs`, no dos:
  `AssemblyVersion`, `AssemblyFileVersion` y **`AssemblyInformationalVersion`**. `AppInfo.Version`
  (`Core/Helpers/AppInfo.cs`) **prefiere el informativo** sobre el numérico, y ese valor es el que
  se pinta en la cabecera de la ventana, el que se escribe en el log de ACARS
  (`vmsOpenAcars v{AppInfo.Version}`) y el que se envía a phpVMS. Subir solo los dos primeros deja
  al cliente mostrando la versión **anterior** aunque el `.exe` recién compilado tenga otro
  `FileVersion` — pasó exactamente al publicar 0.9.10, que se seguía viendo como 0.9.9.
  Comprobación: `(Get-Item bin\Release\vmsOpenAcars.exe).VersionInfo` → `ProductVersion`.
- La versión del proyecto de tests se alinea a la del cliente. En **Release ese proyecto no se
  compila**, así que es fácil que se quede atrás sin que nadie lo note (estuvo en 0.9.4.0 hasta
  v0.9.10).

---

## Scoring — `Services/ScoringService.cs`

17 criterios + 1 bonificación. Parte de 100, deduce (mín 0), luego bonus (máx 100):

| Criterio | Máx | Umbrales / condición |
|---|---|---|
| Landing Rate | 40 | ≤150=0, ≤250=5, ≤350=15, ≤450=25, ≤650=35, >650=40 |
| G-Force | 15 | ≤1.5g=0, ≤1.7g=7, >1.7g=15 (omitido si dato=0) |
| Bank Angle | 10 | ≤2°=0, ≤5°=5, >5°=10 |
| Pitch Angle | 10 | 1°–7°=0; <−2°=10; −2°–1°=5; >8°=5 |
| Overspeed | 15 | 0=0, 1=7, ≥2=15 |
| Lights Compliance | 10 | 5 pts/violación cap 10; Beacon exempto en `BeaconStrobeSharedAircraft` (DH8D) |
| Stabilized Approach 1000 ft | 15 | speed±Vref=−5, VS<−1000=−5, VS>−100=−5, bank>7°=−3, pitch±límites=−3, gear up=−5, flaps<50%=−4 |
| QNH Compliance | 10 | Δ>2 hPa=−5 ×2: salida vs METAR origen; llegada vs QNH (bajo TL) |
| Standard Pressure | 5 | −5 si no se aplica 1013 al cruzar la TA (`StdPressureViolation`) |
| IVAO Offline | 5 | −5 si desconectado al iniciar TaxiOut |
| On-Time Departure | 5 | −5 si Blocks Off difiere >10 min de `sched_out` |
| Touchdown Zone | 7 | ≤1500 ft=0, ≤2500=3, >2500=7 — activo si `TouchdownDistanceFt>0` |
| Centreline Deviation | 7 | ≤10 ft=0, ≤30=3, >30=7 — activo si `CenterlineDeviationFt>0` |
| Localizer Alignment | 5 | ILS not tuned=−3; heading>5°=−1 each (cap 2). Omitido si NAV1 difiere >0.05 MHz del ILS esperado a 1000 ft AGL |
| Minimums Compliance | 5 | −5 si `BelowMinimums=true`. Omitido si Localizer fue omitido |
| Procedure Speed | 10 | 3 pts/violación de restricción SID/STAR al pasar el fix, cap 10 |
| Engine Stabilization | 5 | −5 si algún motor en marcha no establecido (aceite/N2) al entrar en pista |
| **Single Engine Taxi** | **+5** | Multi-motor ≥50% de movimiento con un motor en TaxiOut o TaxiIn |

---

## NavData

### NavDataService — `Services/NavDataService.cs`

```csharp
void PrefetchAirport(icao)
RunwayTouchdownResult FindTouchdownRunway / FindTakeoffRunway / GetRunwayThreshold(airport, lat, lon, heading)
RunwayTouchdownResult SelectApproachThreshold(runways, lat, lon, heading)   // internal static, puro (v0.9.9)
    // "¿está en final?" = rumbo ±15° (magnético) + ≤2 NM del eje VERDADERO + along ≤ 0 (antes del umbral)
bool IsWithinFinalCone(crossTrackNm, distToThresholdNm)                     // internal static, puro (v0.9.9)
    // plausibilidad angular: |cross| ≤ max(0.25 NM, dist·tan 4°)
bool IsPlausibleDiversionDescent(altMslFt, fieldElevFt, distToThresholdNm)  // internal static, puro (v0.9.9)
    // gradiente de descenso usable: AGL/dist ≤ 700 ft/NM (6.6°)
bool IsPlausibleDiversionDistance(alternateNm, plannedNm)                   // internal static, puro (v0.9.9)
RunwayEntry    FindRunwayEntry(airport, lat, lon, heading)
string         FindNearestTaxiway(airport, lat, lon, heading)       // penaliza ×2.5 segmentos >50°
double         FindTaxiwaySegmentBearing(airport, taxiwayName, lat, lon)
HoldingPoint   FindHoldingPoint / ParkingSpot FindNearestParking
IlsData        GetIlsForRunway / ApproachInfo GetApproachType / IList<ApproachFix> GetApproachFixes
(double DistNm, double LateralFt) ComputeApproachMetrics(...)  // static
Task<NearestApproachAirportResult> FindApproachAirport(lat, lon, heading, radiusNm=20, headingTolDeg=15)
    // GET /nearest/approach-airport/ — resuelve aeropuerto+pista por posición/heading,
    // desambigua pistas paralelas por cross-track (score); ver sección dedicada abajo (v0.8.9)
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
static Task<NavApiTestResult>   TestApiAsync(apiKeyOverride, urlOverride = null)  // llama NavDataCache.SyncAirac()
static Task<BriefingCheckResult> CheckAnnouncementAsync(phase, lang)
static Task<byte[]>              FetchBytesAsync(path)
static Task<NavWeather>          GetWeatherAsync(icao)        // TTL 5 min en memoria
```

Caché por capas: (1) `ConcurrentDictionary` en sesión por ICAO → (2) `NavDataCache` SQLite por AIRAC → para airspaces: (3) `_airspaceMemCache` en sesión + (4) `airspace_entries` SQLite TTL 7 días.

Auth: `X-API-Key` + `X-Origin-Domain` de `App.config` (`navdata_api_key`, `navdata_api_domain`).

**Editable desde Settings (v0.9.11):** la sección *NavData API* del formulario tiene ahora campo
para `navdata_api_url` (fila *NavData URL* → *URL NavData*), encima de la API Key. Hasta v0.9.11
la URL solo se podía cambiar editando `vmsOpenAcars.exe.config` a mano, aunque el `BRIEFING` ya
la documentaba como campo de esa pantalla. Guardarla reinicia la app (`HasChanges()` lo detecta);
el botón **TEST** en cambio usa lo que haya escrito en ese momento —`TestApiAsync(key, urlOverride)`—
para poder validar una URL nueva sin guardar. `navdata_api_domain` **sigue** sin campo: se deduce
del host de `vms_api_url` y solo se define a mano si NavData vive en otro dominio.

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

App.config: `osd_enabled`, `osd_sound_enabled`, `osd_duration_seconds` (def 4), `osd_screen_index`, `osd_opacity` (def 90), `osd_airspace_alerts` (def true).

**Avisos de zona restringida en el OSD (v0.9.10):** `osd_airspace_alerts` (fila *Restricted zones*
→ *Alert on the OSD* en Settings → OSD, claves `Restricted zones` y `Stg_AirspaceOsdAlerts`) silencia
**solo el OSD** de las zonas acotadas —`AIRSPACE
{PROHIBITED|RESTRICTED|DANGER}`, `AIRSPACE AHEAD` y `ABOVE … DO NOT DESCEND`—, que es el aviso
intrusivo (severidad Crítica con chime). El log de vuelo y el polígono en el mapa **no** se
suprimen: el piloto que apaga el OSD sigue teniendo el registro y la traza. Se evalúa en el
momento de disparar (`MainViewModel.LogBoundedAirspace` y el handler de `OnAirspaceOverflight`),
así que el cambio aplica en vuelo sin reiniciar. Las entradas/salidas de CTR/TMA/RMZ
(`OnAirspaceEntered`, informativas) no dependen de este ajuste.

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

## Detección de fases — umbrales

| Transición | Condición | Debounce |
|---|---|---|
| Climb → Enroute | VS<200 fpm + cerca crucero, o timeout 5 min + VS<100 fpm | 10 s |
| Climb/Enroute → Descent | VS<−500 fpm **y** alt<máx−500 ft | 20 s |
| Enroute → Climb step | VS>500 fpm + alt<crucero−500 ft | 10 s |
| Descent → Approach | dist<10% totalDist o AGL<aglThreshold | inmediato |
| Approach → Go-around | VS>600 fpm + AGL 100–3000 ft + ≥30 s en Approach | 10 s |
| TaxiOut → TakeoffRoll | GS>30 kt + Pitch<1.0° | 5 s (v0.9.2) |

Umbrales elevados evitan falsas transiciones por cambios de QNH o turbulencia leve (~100 fpm).

---

## Falso "TAKEOFF ROLL" durante taxi rápido — v0.9.2

PIREP real (`E7DK47e88XdabzoL`, SKRG rwy01→SKBQ) reveló que un pico breve de velocidad en
tierra durante el rodaje (GS>30 kt por 1-2 s en la calle A, paralela a la pista 01/19) disparaba
`FlightPhase.TakeoffRoll` — la única transición de `FlightPhaseStateMachine.cs` sin debounce
(todas las demás usan el patrón "temporizador pendiente, confirmar tras N s sostenidos", ver
tabla de arriba). La fase revertía a `TaxiOut` segundos después (GS volvía a caer bajo 30 kt),
pero para entonces `FlightManager.CheckProcedureAtPhaseEntry` ya había disparado, de forma
**irreversible**, penalizaciones pensadas para un despegue real: luces strobe/landing apagadas
(`ApproachValidator.CheckPhaseEntryLights`, caso `TakeoffRoll`) y el QNH de salida
(`ApproachValidator.CheckQnhAsync`) — ninguna es razonable mientras el avión sigue rodando, aún
sin autorización a pista.

Evidencia adicional del mismo bug: `NavDataService.ProjectOnRunway` (usada por
`FindTakeoffRunway`/`FindTouchdownRunway`) seleccionaba la pista "más cercana" solo por **rumbo**
(tolerancia 45°, sin validar posición) y, si ninguna pista pasaba `WithinFootprint`, devolvía
igual el mejor candidato por rumbo en vez de `null` — por eso el log mostró
`🛫️ PISTA 19 | ALINEACIÓN: 6325 ft desde umbral | CL: 614 ft de desviación`, un resultado
geométricamente imposible (614 ft de desviación excede varias veces el ancho real de cualquier
pista) que delataba que el avión estaba en la calle paralela, no en la pista.

**Fix, tres partes:**
1. `FlightPhaseStateMachine.cs` — `TaxiOut → TakeoffRoll` ahora exige `GS>30 kt` sostenido
   **5 s** (nuevo `_takeoffRollStart`/`TakeoffRollConfirmSec`), mismo patrón que
   `_climbStableStart`/`_stepClimbStart`/`_descentStart`.
2. `NavDataService.ProjectOnRunway` — si ninguna pista pasa `WithinFootprint` tras la
   desambiguación de pistas paralelas, retorna `null` (antes devolvía el mejor match por rumbo
   sin validar posición) — mismo criterio que ya usaba `GetRunwayThreshold` para el caso
   análogo. Los callers (`TelemetryCoordinator.LookupRunwayData`/`LookupTakeoffRunwayData`) ya
   manejan `null` correctamente.
3. **Defensa en profundidad** — ni el QNH de salida ni las luces de `TakeoffRoll` tenían guard
   de "ya evaluado este vuelo" (a diferencia del STD de climb `_originQnhChecked` o el QNH de
   llegada `_destQnhChecked`/`_arrivalQnhFinalized`): un rebote repetido de fase, o un rechazo
   de despegue real seguido de un segundo intento, habría penalizado dos veces. Nuevos
   `ApproachValidator._departureQnhChecked`/`_takeoffLightsChecked` (reset en `Reset()`, un
   solo intento por vuelo — mismo comportamiento ya aceptado en este codebase para los demás
   checks de un solo disparo, sin reintento si el METAR no está disponible).

No se agregó heurística de posición/pista al propio state machine — es "pure state machine" por
diseño, sin acceso a NavData; el debounce de 5 s resuelve el caso real sin ese acoplamiento.

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
| `vmsOpenAcars.csproj` | `GenerateBindingRedirectsOutputType=true` — impide sobreescribir binding redirect manual de SQLite |

---

## Tests

`vmsOpenAcars.Tests/` (proyecto hermano de `vmsOpenAcars`, en la solución). **229 tests**:
`ScoringService` (17 criterios, umbrales en ambos lados, bonus de single-engine, suelo de 0,
casos de "sin datos de aterrizaje"), la clasificación de estado de PIREP
(`Pirep.IsActiveState`, que decide el fallback de `FilePirep()`), la geometría flat-earth y
el respaldo regional de TA/TL, el orden de posiciones ATC, la definición de "está en final"
(`SelectApproachThreshold` + `IsWithinFinalCone` + `IsPlausibleDiversionDescent` +
`IsPlausibleDiversionDistance`), y el corredor de la llegada planificada (`RouteCorridor`).
Los casos centrales usan **coordenadas, altitudes y navlogs de vuelos reales**, no geometría
inventada: el falso SKTL, el SKCG legítimo y los tres falsos de KBOS.

`InternalsVisibleTo("vmsOpenAcars.Tests")` en `Properties/AssemblyInfo.cs` permite que los
tests accedan a los tipos `internal` (helpers) sin tener que hacerlos públicos.

```bash
msbuild vmsOpenAcars.sln /p:Configuration=Debug
vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
```

El scoring vive en un `WinExe`, así que el proyecto de tests lo referencia vía
`ProjectReference` con `ReferenceOutputAssembly=false` (**solo** para forzar el orden de
compilación) más un `<Reference>` a `..\vmsOpenAcars\bin\$(Configuration)\vmsOpenAcars.exe`
que sí lo copia para el runtime. En **Release** el proyecto de tests no se compila.

Al añadir un criterio a `ScoringService`: añadir su test, su entrada en
`PirepBuilder._critKeyMap` y su clave `Score_Crit*` en `Languages/{en,es}.json`.

---

## Próximas áreas

Cerradas en v0.9.8: MetarRaw en el LOGBOOK, fallback regional de TA/TL, panel ATC/ATIS
detallado y los tramos de carta sin coordenadas. Cerrada en v0.9.9: la clase de falso desvío por
alineación casual con un aeródromo de la derrota, con dos casos reales (SKTL en un SKRG→SKBQ y
28M/1B9/KOWD en un SKCG→KBOS). Pendiente por decisión de diseño, no por falta de trabajo:

- **El rumbo del avión no está en la misma referencia que los rumbos de NavData (sin arreglar,
  evidencia recogida).** El cliente lee `0x0580` (`FsuipcService._headingOffset`) y
  `NavDataService` compara ese valor contra `rwy.Heading`, que es **magnético** (verificado: para
  KBOS 04R NavData declara 33.4 y el eje geográfico del umbral→fin es 19.67, o sea 13.7° de
  variación; para SKCG 01, 10.8 contra 2.31). Pero **el valor que llega del simulador coincide
  con el rumbo verdadero**, no con el magnético: en el despegue de SKCG el log registra rumbo 3°
  (verdadero 2.31, magnético 10.8) y en la final de KBOS 19° (verdadero 19.67, magnético 33.4),
  en ambos casos con la aeronave sobre el eje (CL 9 ft y 15 ft) y viento flojo, así que no es
  crab. Consecuencia: la comparación tiene un sesgo sistemático igual a la variación local —
  inofensivo en Colombia (8.4°) pero casi letal en Boston (13.7°), donde la tolerancia efectiva
  de 15° queda en ~1.3° y el matcher puede no encontrar la final del destino. **No se ha tocado**
  porque no se puede distinguir desde el cliente entre "el offset es verdadero" y "el simulador
  tiene la variación a cero", y el arreglo difiere. Candidato seguro para ambos mundos: comparar
  contra el rumbo magnético **y** contra el verdadero y aceptar si cualquiera de los dos pasa
  (sólo puede ampliar la puerta, que ya no es la que discrimina). Confirmar antes si el simulador
  tiene activada la variación magnética.

- **Decisiones del mantenedor pendientes** (no son trabajo técnico, y hoy solo estaban en el
  hilo de conversación):
  - **Rotar las claves** de phpVMS y NavData: siguen en el **historial de git** de commits
    anteriores. `App.config` ya no se trackea, así que no puede volver a pasar, pero el
    historial viejo las tiene y solo el mantenedor puede rotarlas.
  - **`vmsOpenAcars.txt`** (540 KB en la raíz, sí trackeado): un volcado generado de todo el
    código fuente, desactualizado y con el usuario de SimBrief dentro. Candidato a eliminar;
    falta la decisión.
  - **El vuelo SKCG→KBOS del 22-23/09/2026 no tiene PIREP en la API** (se comprobaron los 20;
    ninguno con KBOS) aunque su log muestra la puntuación y el aterrizaje registrado. O el
    `/file` falló esa vez, o el PIREP se borró: ese vuelo no estaría acreditado.

- **Touch-and-go de entrenamiento (varios ciclos)** — v0.9.4 desbloquea la máquina de fases
  tras un touch/stop-and-go (`TaxiIn` maneja el despegue y `_wasOnGround` se refresca sin
  condiciones) y la segunda aproximación se puntúa desde cero. Lo que **no** está soportado
  es un circuito de varios ciclos: la primera toma ya quedó registrada en el PIREP vía ACARS
  y el `ApproachBuffer` se reinicia, así que solo se conserva el último aterrizaje.
- **`landing_rate = 0` en el payload** — cuando no hubo touchdown capturado, `PirepBuilder`
  normaliza el centinela `NoLandingData` a `0` porque phpVMS espera un número. Si se
  confirmara que acepta `null`, sería más honesto omitir el campo.
- **Códigos de estado de phpVMS (`PirepState`)** — solo `InProgress = 0` está verificado
  contra producción; el resto sigue la tabla del servidor. Un valor equivocado solo puede
  hacer que un vuelo *no* se dé por enviado (lado conservador), nunca lo contrario.
  Confirmar si se observa un PIREP real.

Deuda estructural restante, sin impacto funcional conocido:

- **Métodos gigantes que sobrevivieron al refactor** — `MapRouteController.LoadRoute` ~700 l,
  `ApproachChartForm.PaintProfileView` ~580 l, `MainForm` ~2 550 l.
- **`DistanceToDestinationNm` hardcodeado a -1** en `BuildPhaseInput()`: deja muerta la rama
  de distancia de la transición `Descent → Approach`. Es la causa raíz ya documentada del
  caso de desvío severo, mitigada por la re-confirmación en `Descent`.
- **`ProjectOnRunway` / `WithinFootprint`** siguen siendo la única parte de la geometría que
  no pasa por `Helpers/GeoMath.cs` (tiene su propia escala y desambiguación de paralelas).
