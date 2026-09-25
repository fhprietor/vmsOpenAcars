# vmsOpenAcars — Guía para Claude

## Proyecto

Cliente ACARS de escritorio (Windows Forms, .NET 4.8, C# 7.3) que conecta simuladores de vuelo con aerolíneas virtuales basadas en phpVMS v7. Lee datos del simulador vía FSUIPC/XUIPC y los envía a la API REST de phpVMS.

**Versión actual:** v0.9.15  
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
- `Languages/es.json` y `en.json` se mantienen **simétricos** (hoy 417 claves cada uno): toda
  clave que se añade o se quita va en los dos.
- **Los rótulos de `SettingsForm` se traducen por `_(clave)` con el propio texto en inglés como
  clave** (`CreateLabel("Duration (s)")` → *Duración (s)*). Una clave que falte **no cae al
  inglés**: `LocalizationService.GetString` devuelve `[[clave]]`, así que aparece literalmente
  `[[Airspace]]` en la pantalla. Al añadir una fila al formulario hay que añadir su clave a los
  dos `.json` (`Restricted zones` y `Stg_AirspaceOsdAlerts` lo son desde v0.9.10).
- Al añadir un criterio a `ScoringService`: su test, su entrada en `PirepBuilder._critKeyMap`
  y su clave `Score_Crit*` en ambos idiomas.

**Documentación**
- **`CLAUDE.md` tiene un techo duro de 65 536 bytes** (64 KB): es el presupuesto de instrucciones
  del agente, y el harness **trunca en silencio** lo que sobra, empezando por el final del archivo
  —o sea *Próximas áreas*, justo lo que no se puede perder—. Se pasó de 67 KB en v0.9.13 y se
  movieron a `Docs/architecture.md` la narrativa de desvíos (~21 KB) y el índice de archivos
  (~10 KB), quedando en ~38 KB. **Lo largo va a `Docs/architecture.md`**; aquí se deja un resumen
  con puntero. Antes de añadir una sección grande: `(Get-Item CLAUDE.md).Length`.
- `Docs/CHANGELOG.md` es el **canónico**: cada entrada lleva causa raíz y las cifras que la
  demuestran. Se actualiza junto con `CLAUDE.md`, en el mismo cambio.
- `Docs/README.md` es el índice: un `.md` nuevo se lista ahí o queda huérfano.
- **`Docs/MEMORY.md` y `Docs/project_vmsOpenAcars.md` no son fuente de verdad** (son resúmenes
  de mantenimiento y se quedan atrás: `MEMORY.md` llegó a decir v0.9.2 con el cliente en
  v0.9.9). La verdad es `CLAUDE.md` + `Docs/CHANGELOG.md`.
- Para montar el proyecto en otro equipo (o cambiar de agente) está `Docs/SETUP-ENTORNO.md`:
  qué **no** viaja por git —`App.config` y `packages/` están ignorados—, el toolchain y cómo se
  levanta `dsh` con `%USERPROFILE%\.dsh`.

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

**Rejilla del formulario: una celda, un control.** `SettingsForm` construye todo con índices de
fila explícitos y sin comprobación: dos controles en la **misma celda** de un `TableLayoutPanel`
no dan error —el último se pinta encima— y el síntoma es un rótulo tapado, no un fallo. Pasó al
añadir la URL en v0.9.11 (el rótulo *NavData API* se quedó en la fila de la Key) y se corrigió en
v0.9.12. Al insertar una fila hay que **renumerar todo lo que va debajo** y comprobar el resultado
celda por celda, no solo que compile.

**Reparto de espacio en `SettingsForm` (v0.9.12).** El alto útil de contenido es `alto de ventana
− 99` px: barra de título 35 + panel de botones 44 + `Padding` 4 y 16. La columna izquierda son
filas de 35 px, así que **cada fila nueva hay que pagarla**: 14 filas = 490 px y de ahí la ventana
en 920×600 (`MinimumSize` al mismo alto, para no poder encogerla hasta cortar la rejilla). Un
resultado de texto largo no debe compartir celda con botones ni resolverse con aritmética de
`Resize`: eso es lo que recortaba el mensaje de estado de NavData hasta v0.9.12. Se usan filas
separadas y `FlowLayoutPanel` para los grupos de botones.

Rótulos y campos de la columna izquierda, en orden: ApiUrl, ApiKey, SimbriefUser, Airline,
Language · *── SimBrief Dispatch ──* · SimbriefUnits, SimbriefCI, SimbriefRmk · *── NavData API ──* ·
NavData URL, NavDataKey, NavData (resultado del test), y una fila sin rótulo para `[REFRESH] [TEST]`.

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

Resuelve el aterrizaje en un aeropuerto distinto al planeado (emergencia, regreso a origen,
desvío real) sin que el PIREP quede con el `arr_airport_id`/`diversion-airport` equivocados y sin
puntuar QNH/Localizer/Minimums contra el METAR de un destino nunca alcanzado.

- **Detección**: `NavDataService.FindApproachAirport` (`GET /nearest/approach-airport/`) durante
  Descent y Approach, cada 5 s.
- **Un desvío se marca solo si pasa seis filtros** —lateral 3 NM, cono angular 4°, gradiente de
  descenso ≤700 ft/NM contra la elevación del campo emparejado, alterno más cerca que el destino,
  establecido en final (`along ≤ 0`) y persistencia de 2 sondeos— **y** no está dentro del corredor
  de la llegada del propio plan (`Helpers/RouteCorridor.IsOnArrival`: 5 NM a cada lado de los
  últimos 40 NM de la traza del navlog).
- **QNH de llegada**: provisional durante el vuelo, se decide una sola vez al filear
  (`FinalizeArrivalQnhAsync`), con el destino final y el QNH actual del avión.
- **Al filear**: `UpdatePirep(arr_airport_id)` + `diversion-airport` en el payload; la reubicación
  del piloto la hace phpVMS. La elevación de referencia (AGL) se corrige con el campo real.
- **Ayudas puras, cada una con su test**: `SelectApproachThreshold`, `IsWithinFinalCone`,
  `IsPlausibleDiversionDescent`, `IsPlausibleDiversionDistance`, `RouteCorridor` y
  `GeoMath.DistanceToSegmentNm`.

> El detalle —causa raíz, cifras medidas y los casos reales (falso SKTL, SKGY, los tres falsos de
> KBOS y SKCL→SKBO)— está en **`Docs/architecture.md` → "Aeropuerto de llegada distinto al
> planeado"**. Se movió allí en **v0.9.13** porque `CLAUDE.md` superaba el presupuesto de
> instrucciones del agente (65 536 bytes) y el harness lo truncaba, perdiendo justo el final de
> "Próximas áreas".

## RAAS y guía de rodaje — v0.9.14–v0.9.15

Dos piezas: los avisos tipo RAAS y la guía giro a giro por la ruta que elige el piloto.

- **Cuándo**: al encender la **luz de taxi** o al entrar en **TaxiOut**, lo que ocurra antes y una
  vez por vuelo, sale `TaxiRouteForm`: lista de **pistas del aeropuerto** (por defecto la del OFP,
  `SimbriefPlan.OriginRunway`), **ruta editable separada por espacios** (la sugiere el grafo de
  calles) y los ajustes de RAAS / voz / volumen. `raas_enabled` en App.config lo desactiva.
- **Segundo aviso en el punto de inicio (v0.9.15)**: el popup puede salir dos veces. El segundo se
  pide al **poner el freno de parqueo en fase `Pushback`** (fin del empuje) o, sin pushback —puesto
  remoto—, al entrar en `TaxiOut`; la máquina de fases ya pasa de `Boarding` a `TaxiOut` por
  movimiento sostenido, sin beacon ni motores, así que no hace falta heurística nueva. Recalcula
  desde la posición actual y **solo abre la ventana si el grafo cambia de propuesta**
  (`TaxiRoutePlan.SameRoute`): medido, desde el puesto G49 y desde el fin de su pushback la ruta es
  la misma. El recálculo va en el punto de inicio y no rodando: ya en `B9` el grafo propone
  `B9 C P G…`, que manda volver a `C`.
- **Avisos** (`Helpers/RaasAdvisor.cs`, puro y con estado): `APROXIMANDO PISTA` y `ESPERA ANTES DE
  PISTA` a 150 m / 40 m del hold-short **yendo hacia él**; `CALLE X A LA DERECHA/IZQUIERDA EN N M`
  a 250 m del cruce y `GIRA AHORA` a 60 m; `FUERA DE RUTA`; `RUTA COMPLETA`. Uno por situación, con
  20 s de enfriamiento y re-armado al terminar: nunca repite el mismo aviso cada sondeo.
- **Grafo** (`Helpers/TaxiGraph.cs`, puro): extremos de segmento a ≤45 m son un nodo, Dijkstra
  hasta el umbral de la pista → secuencia de calles. Resuelve también el cruce y el lado del giro.
- **Voz SAPI** (`Services/RaasVoice.cs`): cola FIFO en hilo propio —el hilo de telemetría nunca se
  bloquea—, volumen propio, y **degrada en silencio** si el equipo no tiene voces (lo dice una vez
  en el log). Usa `System.Speech`, que va con .NET Framework: nada que distribuir.
- **Los avisos se evalúan a 1 Hz sobre la telemetría cruda**, no sobre el envío de posiciones a
  phpVMS: ese va cada 30 s en rodaje y a 15 kt son ~230 m entre muestras, demasiado para avisar a
  150 m de un hold-short.
- **`FUERA DE RUTA` y `RUTA COMPLETA` (v0.9.15)**: el primero exige **15 s** fuera de ruta
  **sin acercarse** a la pista (≥50 m respecto al punto más cercano del episodio) — la ruta del
  grafo y la de ATC llegan al mismo sitio por calles distintas, y en el rodaje real eso produjo 8
  avisos falsos; sin dato de distancia manda solo la insistencia. El segundo significa **estar
  dentro de la pista** (`OnRunway`) y se dice una sola vez por guía: antes se disparaba al agotar
  la lista de calles, tres minutos antes de entrar. Los dos los vigila `RaasReplayTests` sobre la
  traza real.
- **Bug corregido de paso**: `FindHoldingPoint` filtraba por el `heading` del hold-short, que es el
  **eje de la pista**. Con el avión rodando perpendicular —267–270° reales contra 136° del eje en
  SKBO— descartaba justo los hold-shorts que tenía delante; ahora exige ir **hacia** el punto.

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

El índice de archivos con lo relevante de cada uno —qué hace cada partial, dónde viven los
helpers puros, qué cubre cada suite de tests— está en **`Docs/architecture.md` → "Referencias de
archivos clave"** (movido allí en **v0.9.13** por el mismo motivo de presupuesto que la sección
anterior).

---

## Tests

`vmsOpenAcars.Tests/` (proyecto hermano de `vmsOpenAcars`, en la solución). **248 tests**:
`ScoringService` (17 criterios, umbrales en ambos lados, bonus de single-engine, suelo de 0,
casos de "sin datos de aterrizaje"), la clasificación de estado de PIREP
(`Pirep.IsActiveState`, que decide el fallback de `FilePirep()`), la geometría flat-earth y
el respaldo regional de TA/TL, el orden de posiciones ATC, la definición de "está en final"
(`SelectApproachThreshold` + `IsWithinFinalCone` + `IsPlausibleDiversionDescent` +
`IsPlausibleDiversionDistance`), el corredor de la llegada planificada (`RouteCorridor`) y el
RAAS con la guía de rodaje (`RaasTests`: grafo, ruta editable y avisos; `RaasReplayTests`: el
rodaje real del `MNjR664PBAr25RbD` entero contra el motor de avisos, volcado a
`%TEMP%\raas_replay_MNjR664.txt`).
Los casos centrales usan **coordenadas, altitudes y navlogs de vuelos reales**, no geometría
inventada: el falso SKTL, el SKCG legítimo, los tres falsos de KBOS y el rodaje completo del
`MNjR664PBAr25RbD` (106 segmentos reales de SKBO y las posiciones del pushback y del rodaje).

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

- **El fin del pushback no está en NavData (verificado, sin implementar).** Probados en vivo:
  `/airport/{icao}/pushback/`, `/pushbacks/`, `/stands/`, `/gates/`, `/aprons/` → **404**, y el
  objeto de parking solo trae `name, number, suffix, type, radius_ft, heading, has_jetway,
  airline_codes, lat, lon` (`airline_codes` llega en la respuesta y no se mapea en `NavParking`).

  Lo que se creía derivable era el **eje** del pushback: el segmento `P`/`PT` cuyo extremo arranca
  en el puesto apunta en el recíproco del morro — en SKBO puesto 49 (rumbo 216°, radio 75 ft) el
  extremo lejano del segmento `C/P` cae a **187 ft con rumbo 037°**, contra los 036° teóricos.
  **Pero no sirve ni el eje**: el pushback real de esa traza terminó a **432 ft con rumbo 083°**,
  es decir con el morro ya girado ~47° (el remolque acaba en curva), y el punto derivado queda a
  **327 ft** del real. Conclusión: NavData da el **puesto** (aquí a 3 ft), no el recorrido.

  La fuente correcta es la **telemetría**, y ya está implementada: el fin del pushback es el
  **freno de parqueo puesto estando en fase `Pushback`** — `ParkingBrakeChanged` →
  `OnParkingBrakeChanged` ya lo loguea (`Log_ParkingBrakeSet`) y esa línea **lleva la posición**.
  Aquí: `── PUSHBACK ──` 21:50:58 → freno puesto **21:52:39** en 4.69788,-74.13721, **101 s** de
  empuje; el último `PBT` muestreado quedó a **18 ft** de ese punto. Nada que derivar.

  Por qué el tramo final no tiene posiciones: `EmitTaxiPosition` solo se llama en `TaxiOut`,
  `AfterLanding` y `TaxiIn` — **no** en `Pushback` —, así que entre el último `PBT` (21:52:18) y
  el primer `TXI` (21:56:18) el avión estuvo parado con el freno puesto y sin muestrear.

  Evidencia: PIREP `MNjR664PBAr25RbD`, SKBO, log `SALIENDO DESDE G49` (el puesto que resuelve
  `FindNearestParking`/`BuildParkingName`) 21:45:40 → `BST` 21:45:47 → freno liberado 21:50:46 →
  `PBT` 21:51:18–21:52:18 → freno puesto 21:52:39.

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
