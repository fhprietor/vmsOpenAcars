# vmsOpenAcars — Guía para Claude

## Proyecto

Cliente ACARS de escritorio (Windows Forms, .NET 4.8, C# 7.3) que conecta simuladores de vuelo con aerolíneas virtuales basadas en phpVMS v7. Lee datos del simulador vía FSUIPC/XUIPC y los envía a la API REST de phpVMS.

**Versión actual:** v0.9.52  
**IDE:** Visual Studio 2017 (compilar siempre desde el IDE, nunca desde CLI)

## Stack

- **FSUIPC** (FSUIPCClientDLL 3.3.16) · **NAudio** (2.3.0) · **Newtonsoft.Json** · **GMap.NET** (2.1.7)
- **System.Data.SQLite** (1.0.119) — `landing_log.sqlite` + `NavData_cache.sqlite`
- **phpVMS v7** — backend REST · **SimBrief** — planes de vuelo / OFP

> **Credenciales — dev vs. distribución:**
> - `App.config` es la **configuración local de desarrollo**: contiene las claves del
>   entorno propio para no reconfigurar la app en cada compilación. No se distribuye.
> - `App.Release.config` es la **plantilla de la configuración que el piloto descarga una sola vez**
>   desde la web, **aparte** del cliente: nunca debe llevar credenciales, URLs de aerolínea ni rutas
>   de máquina, solo placeholders. **El zip nunca incluye `vmsOpenACars.exe.config`**: el `Updater`
>   copia sin borrar nada, así que el config del piloto sobrevive a las actualizaciones; meterlo lo
>   pisaría, y zipear `bin\Release` en bloque filtraría la `vms_api_key`.
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
- `Languages/es.json` y `en.json` se mantienen **simétricos** (hoy 500 claves cada uno): toda
  clave que se añade o se quita va en los dos.
- **Antes de escribir un rótulo, mirar si la clave ya existe**: los `.json` tienen claves definidas y sin
  cablear (128 contadas en v0.9.45, casi todo `Fp_*` y `Status_*`), y **un rótulo de botón o de casilla
  no se escribe a mano** —`ButtonLabelsTests` exige `_("Clave")` o un símbolo puro—. **El tamaño de la
  leyenda lo decide el grupo, nunca cada tecla**: la barra **reparte el ancho** (`MainButtonBar.ButtonWidthFor`,
  suelo 108 px y mínimo de ventana 910) y `McduKeyButton.CommonLegendSize` encoge **todas a la vez** si hace
  falta. Se mide y se dibuja la cadena **entera** con GDI: por carácter la medida se infla (24 px por letra
  donde la cadena mide 16) y encogía rótulos que cabían.
- **Los rótulos de `SettingsForm` se traducen por `_(clave)` con el texto inglés como clave**
  (`CreateLabel("Duration (s)")` → *Duración (s)*). Una clave que falte **no cae al inglés**:
  `LocalizationService.GetString` devuelve `[[clave]]` y se pinta `[[Airspace]]` en la pantalla. Al añadir
  una fila hay que añadir su clave a los dos `.json`.
- Al añadir un criterio a `ScoringService`: su test, su entrada en `PirepBuilder._critKeyMap`
  y su clave `Score_Crit*` en ambos idiomas.

**Documentación**
- **`CLAUDE.md` tiene techo duro** (`Docs/SETUP-ENTORNO.md` §3.1): el presupuesto de instrucciones es de
  ~65 KB y el harness **trunca en silencio** lo que sobra **empezando por el final** —o sea *Próximas
  áreas*—. Pasó en v0.9.13 (67 KB) y volvió a pasar en v0.9.45: **lo largo va a `Docs/architecture.md`**,
  y antes de añadir hay que medir con `(Get-Item CLAUDE.md).Length`.
- `Docs/CHANGELOG.md` es el **canónico**: cada entrada lleva causa raíz y las cifras que la
  demuestran. Se actualiza junto con `CLAUDE.md`, en el mismo cambio.
- `Docs/README.md` es el índice: un `.md` nuevo se lista ahí o queda huérfano.
- **`Docs/MEMORY.md` y `Docs/project_vmsOpenAcars.md` no son fuente de verdad** (se quedan atrás: `MEMORY.md` dijo v0.9.2 con el cliente en
  v0.9.9). La verdad es `CLAUDE.md` + `Docs/CHANGELOG.md`.
- Para montar el proyecto en otro equipo está `Docs/SETUP-ENTORNO.md` (toolchain y `dsh`).

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
- **Publicar una release («PUBLICA RELEASE»)**: la receta completa —versión, notas del piloto, zip
  sin el `.config`, etiqueta, Release con el binario y verificación— está en
  **`Docs/PUBLICA-RELEASE.md`**, con las dos trampas de esta sesión de agente: `git add <ruta>` no
  stagea —solo `git add -A`— y el caso `vmsOpenACars` (namespace) contra `vmsOpenACars` (directorio).

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
| Touchdown Zone | 7 | **v0.9.30, proporcional a la pista** (`Helpers/TouchdownZonePolicy.cs`): 0 si ≤ `max(1.500 ft, 15 % de la pista)` con techo **3.000 ft**; 3 si ≤ `min(3.000 ft, mitad de pista)`; 7 por encima. **Sin dato de longitud → 1.500/2.500.** El techo no se negocia: en pista larga, 3.200 ft siguen siendo 7 |
| Centreline Deviation | 7 | ≤10 ft=0, ≤30=3, >30=7 — activo si `CenterlineDeviationFt>0` |
| Localizer Alignment | 5 | ILS not tuned=−3; **una desviación >5° = −1** (`ExcursionCounter`: 0,5 s fuera y re-arma al volver; cap 2). Omitido si NAV1 difiere >0.05 MHz del ILS a 1000 ft |
| Minimums Compliance | 5 | −5 si `BelowMinimums=true`. Omitido si Localizer fue omitido |
| Procedure Speed | 10 | 3 pts/violación de restricción SID/STAR al pasar el fix, cap 10 |
| Engine Stabilization | 5 | −5 si algún motor en marcha no establecido (aceite/N2) al entrar en pista |
| **Single Engine Taxi** | **+5** | ≥50 % del **tiempo** en movimiento con un motor, por fase (salida **o** llegada), **medido por desplazamiento** (`GroundMotion`: la GS del sim publica 4-7 kt con el avión aparcado) y **sin contar el tiempo sin motor en marcha**. El rodaje completo se publica en el log, pero **no relaja nada**: `(a+b)/(w1+w2) ≥ 0,5` con las dos fases por debajo es imposible (`SingleEngineTaxiPolicyTests`) |

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
static Task<NavAirspacesResult> GetAirspacesAsync(lat, lon)
    // el radio lo declara el servidor (`radius_nm`): 200 nm con su índice propio (29/09/2026;
    // había bajado a 54). El resultado distingue «no pude preguntar» (Unavailable) de «aquí no
    // hay nada» (200 vacío), respeta `Retry-After` del 503 POR PUNTO y trae `source`
    // (local|openaip_api), `capped` y `countries`, que el log de la ruta pinta
static Task<NavApiTestResult>   TestApiAsync(apiKeyOverride, urlOverride = null)  // llama NavDataCache.SyncAirac()
static Task<BriefingCheckResult> CheckAnnouncementAsync(phase, lang)
static Task<byte[]>              FetchBytesAsync(path)
static Task<NavWeather>          GetWeatherAsync(icao)        // TTL 5 min en memoria
```

Caché por capas: (1) `ConcurrentDictionary` en sesión por ICAO → (2) `NavDataCache` SQLite por AIRAC → para airspaces: (3) `_airspaceMemCache` en sesión + (4) `airspace_entries` SQLite TTL 7 días.

Auth: `X-API-Key` + `X-Origin-Domain`. **La clave NO sale del `.config`** (desde 0.9.26 no hay respaldo: si alguien la escribe ahí, se ignora): llega en el sobre de phpVMS, y sin sobre la sesión vuela **sin NavData** avisando una vez con el motivo.

**La clave ya no viaja en el `.config` y no tiene respaldo (v0.9.25–v0.9.26).** La `navdata_api_key` **estaba publicada** en el
`.config` que se descarga del gestor de ficheros —**hay que rotarla**, el cambio solo evita que viaje
de aquí en adelante—, así que el `.config` publicado la deja **vacía** y se pide a phpVMS
(`GET {vms_api_url}/api/navdata` con **`X-API-KEY`**; `Authorization: Bearer` **no** vale, 401) en un
sobre **HKDF-SHA256 + AES-256-CBC + HMAC-SHA256** con **BCL pura** (GCM exigiría BouncyCastle):
salt/AAD `vmsopenacars/navdata/v1`, info `navdata-api-key-cbc`, 64 B de material (32 enc + 32 mac),
IKM = la `vms_api_key` del piloto, **MAC antes de descifrar** y en tiempo constante. Se abre **una vez
por sesión** (límite del servidor: **30/min por piloto**), vive **solo en memoria** hasta `expires_at`
y **nunca** se escribe en config, logs ni telemetría; `503`/`401`/`400` dejan la sesión sin NavData y
**el vuelo sigue**. La `url` del sobre (**base completa**) se usa **tal cual**, y solo si no viene se
cae a `navdata_api_url`. Código: `Helpers/NavDataCipher.cs`, `Helpers/NavDataKeyState.cs`,
`Helpers/NavDataKeyPolicy.cs`, `Services/NavDataKeyProvider.cs`, `Services/Http/NavDataRequest.cs`.

**Aviso de NavData del 29/09/2026** (respuesta y confirmación en `Docs/`): de sus seis cambios,
**cinco no nos tocaban** —no leemos `parkings[].heading`, ni `ils_freq_mhz` sin comprobar `null`, ni
`airway.direction`, ni `has_vertical_angle`; y el 404 de `/nearest/approach-airport/` ya se trataba
como «sin resultado»—. **El que dolía era `/airspaces/`**: devolvía **503** cuando OpenAIP no
respondía y no había caché, y eso llegaba como lista vacía = «cielo vacío». Medido entonces en un
SKCG→KBOS: **12 de 16 muestras daban 503**. **Ya está arreglado en su lado** (índice propio desde los
bulk exports por país, 129 países, radio de vuelta a 200 nm): recorriendo la ruta con el muestreo
actual, **24/24 consultas OK y 613 espacios aéreos** (antes 11). Confirmado a NavData que **enviamos
rumbo verdadero**; cerrado con `heading_reference=true`, que hace que el servidor convierta con el
`mag_var` del candidato —medido en SKCG: `heading_diff_deg` de 7,8° a **0,7°**—. Pendiente menor:
las comparaciones **locales** de pista (`SelectApproachThreshold`) siguen siendo verdadero contra
magnético; se arreglaría con el `mag_var` del aeropuerto, **ya publicado** (30/09/2026) en `/airport/{icao}/` y `/runways/` o
leyendo la variación del simulador.

**La sección *NavData API* del formulario se retiró en v0.9.27** (la clave llega en el sobre y la URL ya no se edita: cambiarla exige el `.exe.config`, como antes de v0.9.11). **Histórico (v0.9.11):** la sección *NavData API* del formulario tiene ahora campo
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
filas de 35 px, así que **cada fila nueva hay que pagarla**: 12 filas = 420 px y de ahí la ventana
en 920×530 (`MinimumSize` al mismo alto, para no poder encogerla hasta cortar la rejilla). Un
resultado de texto largo no debe compartir celda con botones ni resolverse con aritmética de
`Resize`: eso es lo que recortaba el mensaje de estado de NavData hasta v0.9.12. Se usan filas
separadas y `FlowLayoutPanel` para los grupos de botones.

Rótulos y campos de la columna izquierda, en orden: ApiUrl, ApiKey, SimbriefUser, Airline,
Language · *── SimBrief Dispatch ──* · SimbriefUnits, SimbriefCI, SimbriefRmk · *── NavData API ──* ·
NavData (resultado del test), y una fila sin rótulo para `[REFRESH] [TEST]`. **Los campos de la URL y de la clave se retiraron en v0.9.27.**

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

Task InitRouteAsync(originIcao, destIcao)  // muestrea el arco cada 0.75×radio (AirspaceRouteSampler)
                                           // y no pisa lo cargado si no hay datos frescos
void CheckPosition(lat, lon, altFt)        // ray-casting GeoJSON + límites verticales
void TriggerIvaoRefresh()
// LastAirspaceLoadUnavailable / Partial / Samples / Count / Source / Capped / Countries: cómo fue
// la última carga, para que el log no diga «0 espacios aéreos» cuando el servidor no contestó (503)
// y para poder reportar a NavData los corredores servidos por el respaldo o sin export de país
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

**Crédito de las teselas del mapa (v0.9.16).** El mapa pinta en su esquina inferior derecha el crédito
del proveedor activo —lo exigen las condiciones de CARTO («CARTO and OpenStreetMap must be credited on
every map») y las de ESRI—. Cada proveedor lo publica en el campo `Copyright` que GMap.NET define para
esto, `MapAttribution.For` decide el texto y **`MapAttributionTests` impide que un proveedor quede sin
crédito**: al añadir uno nuevo al combo hay que darle el suyo. El rótulo es transparente al ratón
(`AttributionLabel` → `HTTRANSPARENT`), para que esa esquina siga arrastrando el mapa.

---

## CabinAnnouncementService — `Services/CabinAnnouncementService.cs` (v0.5.9, voz 0.9.48)

**Clip si existe, voz SAPI si no.** Clips de `/briefing/check/` + `/briefing/download/` (caché `%TEMP%\vmsacars\briefing\`); NavData **no publicará más fases**, así que las que faltan las locuta `Services/CabinVoice` (`System.Speech`, ya referenciado) con el texto puro de `Helpers/CabinAnnouncementScript` (es/en, datos opcionales: sin dato la frase desaparece; destino **por su nombre** de `name`+`city`, que llegan abreviados y sin tildes; hora local de `Helpers/LocalTimeOfDay`, longitud ÷ 15°). `Helpers/SapiVoicePolicy` elige el idioma **contra las voces instaladas**: sin voz española se locuta el texto inglés.

**13 fases.** Por entrada de fase: `pushback`, `taxi_out`, `top_of_descent`, `approach`, `slides_disarm` (AfterLanding), `taxi_in`, `on_block`; `boarding` al acabar el prefetch; `after_takeoff` a 10 000 ft AGL (Climb/Enroute); `cruise` >10 000 ft AGL 30 s; `on_runway` a la 1.ª luz con GS ≤ 40 kt; y la turbulencia (`turbulence_on`/`turbulence_off`, evento por `Helpers/TurbulenceAdvisor`: **oscilaciones** de g `0x11BA` (3 cruces de banda en 6 s), 10 min de enfriamiento; el fin solo en crucero (`CabinCalloutPolicy`)). `top_of_descent`/`taxi_in` refrescan el METAR (3 s) y cada PA deja línea en el log.

**Idioma:** `Pilot.AirlineCountry` → `SpanishCountries` → `"es"`/`"en"` (internacional no anglohablante: inglés y luego nativo). **Supresión:** `aircraftSeats ∈ (0,40)`. **Settings:** `chkCabinAnnouncements`, `trkCabinVolume` (en caliente), `btnTestCabin` (13 fases). Claves: `cabin_announcements_enabled` / `_volume`.

Detalle: `architecture.md` → "Los anuncios de cabina".

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
                             + copia de las DOS trazas (aproximación y flare) — v0.9.34
await FilePirep()         ← llama ResetFlightState() → _activePlan=null, touchdown data=0
ResetTelemetry            ← corre TelemetryCoordinator.Reset() → vacía el buffer del flare
éxito → record.Score = LastFlightScore  ← única propiedad que NO se resetea
      → LandingLogService.SaveFlight(record, approachSnapshot) + SaveFlareTrack(flareSnapshot)
```

**Las dos trazas se copian antes del `await` (v0.9.34)**: el reset del vuelo vacía el buffer del flare
y el guardado va después, así que leerlo en vivo dejaba `flare_track` con **0 filas en todos los
vuelos**. `flights.flare_capture_armed` guarda «la captura se armó» y **no** es «hay filas en
`flare_track`». El caso medido, en **`Docs/architecture.md` → "El orden de la copia de las trazas"**.

`LandingAnalysisForm`: 4 gráficos VERTICAL/LATERAL/IAS/VS, eje X 5NM→0. Suavizado Gaussiano (window=7) en LATERAL/IAS/VS, no en VERTICAL. **En el aterrizaje se guarda la meteo** (METAR del destino + viento del simulador y sus componentes en cara/cruzada, `Helpers/WindComponents.cs`), que va al PIREP en el `notes`, y el perfil vertical tiene un **closeup del toque** con la pista, el umbral y el punto de toque (`Helpers/TouchdownCloseupGeometry.cs`) — v0.9.31. **El analisis del aterrizaje y el flare** (v0.9.31 a v0.9.38) esta contado entero, con sus causas y sus cifras, en **`Docs/architecture.md` -> "El analisis del aterrizaje y el flare"**, y su deuda de verificacion en **`Docs/ESTADO-FLARE-2026-10-08.md`**. En una linea: traza fina del flare en `flare_track` a 10 Hz nominales (~8 reales, v0.9.37), closeup del toque con la Y autoescalada y el eje X en pies, ventana propia del FLARE, flaps y corte de potencia (N1), cabecera de datos para compartir el grafico, y **el analisis fileado a phpVMS**; la traza de 2 s es el respaldo cuando no hay fina, y el grafico dice cual usa.

---

## ILS / Approach Detection — v0.4.4

Carga al entrar en fase Approach → `Task.Run(LoadApproachData)` → `FlightManager.SetApproachData(ils, approach, fixes)`.

A 1000 ft AGL: compara `Nav1FrequencyMhz` vs `_expectedIls.FrequencyMhz` (±0.05 MHz).
- Coincide → ILS confirmado, scoring normal.
- No coincide → anula ILS, omite Localizer + Minimums sin penalizar (`Log_IlsApproachSkipped`).

Bajo 500 ft AGL: `_localizerViolations++` si |hdgDelta|>5° (cap 2). Check DA para `_belowMinimums`.

FsuipcService offsets: `0x0350 INT16` NAV1 freq BCD (`100+d3×10+d2+d1×0.1+d0×0.01`), `0x0C4E INT16` NAV1 OBS. **Cada ciclo de 20 Hz** (`polling_interval_ms` 50 ms) hace **una** llamada a `FSUIPCConnection.Process()` que trae **todos** los offsets: muestrear más fino **no cuesta lecturas nuevas**, solo decodificar.

---

## Aeropuerto de llegada distinto al planeado — v0.8.8–v0.9.42

Aterrizar en un aeropuerto distinto al planeado se detecta con `NavDataService.FindApproachAirport`
tras seis filtros y fuera del corredor de la llegada del plan; el PIREP se corrige al filear.
**v0.9.42**: la puerta previa a la consulta (`Helpers/DiversionQueryGate`) pregunta por la **altitud
MSL** (techo 10 000 ft), no por el AGL del destino planeado; `CurrentAGL` no se toca.

> Causa raíz, cifras y los casos reales (`wk0896ajpxKalPrp` a SKMR, falso SKTL/SKGY, KBOS, SKBO) en
> **`Docs/architecture.md` → "Aeropuerto de llegada distinto al planeado"**: lo largo va allí.

## RAAS y guía de rodaje — v0.9.14–v0.9.15

Dos piezas: los avisos tipo RAAS y la guía giro a giro por la ruta que elige el piloto.

- **Cuándo (v0.9.17)**: el popup sale **después del pushback**, nunca durante. Las tres salidas para
  abrirlo son el fin del empuje (freno puesto), la luz de taxi ya con el avión libre y `TaxiOut`
  para un puesto remoto; además, **«Cambiar ruta de rodaje (ATC)»** en el menú de la bandeja se
  salta el «una vez por vuelo» y el filtro de «la ruta no cambió», y recalcula desde la posición
  actual. El popup trae la lista de pistas (por defecto la del OFP, `SimbriefPlan.OriginRunway`), la
  ruta editable que sugiere el grafo y los ajustes de RAAS.
- **Avisos** (`Helpers/RaasAdvisor.cs`, puro y con estado): `APROXIMANDO PISTA` y `ESPERA ANTES DE
  PISTA` a 150 m / 40 m del hold-short **yendo hacia él**; `CALLE X A LA DERECHA/IZQUIERDA EN N M`
  a 250 m del cruce y `GIRA AHORA` a 60 m; `FUERA DE RUTA` (15 s fuera de ruta y **≥50 m** de
  progreso hacia el umbral); `RUTA COMPLETA`. Uno por situación, con **20 s** de enfriamiento y
  re-armado al terminar: nunca repite el mismo aviso cada sondeo. Se evalúan a **1 Hz sobre la
  telemetría cruda**, no sobre el envío de posiciones a phpVMS; **con la guía activa ese envío baja
  a 5 s desde 0.9.23** (`UpdateIntervalPolicy`, que decide las **tres** puertas que lo frenaban: el
  intervalo, el suelo de envío y el umbral de deduplicación de `HasSignificantChange`); **sin guía
  no cambia nada** — y desde v0.9.31 la guia entera se apaga con un **checkbox en Settings** (`taxi_guidance_enabled`): corta avisos, voz y popup de ruta, y devuelve la traza a 30 s, todo **sin reiniciar**.
- **Grafo** (`Helpers/TaxiGraph.cs`, puro): extremos de segmento a ≤45 m son un nodo, Dijkstra
  hasta el umbral de la pista → secuencia de calles; resuelve también el cruce y el lado del giro.
  El `node_id` está implementado y **apagado**. Desde 0.9.23 el cliente **publica su ruta propuesta**
  (`planned` con `text`, `runway`, `polyline` y `dataset_version`) por
  `POST /taxi-routes/observations`, una vez por vuelo (**apagado por defecto desde 0.9.24** hasta que NavData publique su almacén: `taxi_planned_observation_enabled`; con el interruptor apagado no se construye el cuerpo ni se sale a la red).
- **Voz SAPI** (`Services/RaasVoice.cs`): cola FIFO en hilo propio —el hilo de telemetría nunca se
  bloquea—, volumen propio, y **degrada en silencio** si el equipo no tiene voces (lo dice una vez
  en el log). Usa `System.Speech`, que va con .NET Framework: nada que distribuir.

> El detalle —historia del punto de espera, base de rutas, dos fuentes de coordenadas, empalmes,
> `node_id` y el caso G74 → A3— está en **`Docs/architecture.md` → "El grafo de rodaje"**.

## Aeronave del simulador vs. OFP — v0.9.16

Tres datos del simulador para identificar la aeronave: el *title* (`0x3D00` → `AircraftTitle`,
«777-200LR»), el **modelo ATC** (`0x0618` → `AircraftIcao`, de **familia**: `B777`, `B737`,
`B747`) y `AircraftModel` (`0x0B26`). SimBrief publica en `aircraft.icao_code` el **designador ICAO
de tipo**, de **variante** (`B77L`, `B77W`, `B738`). Compararlo todo con igualdad exacta daba un
falso «AIRCRAFT MISMATCH» con el avión correcto (PMDG B77L en MSFS 2024, bloqueaba el START).

- `Helpers/AircraftTypeMatch.cs` (puro, con test) resuelve en **dos niveles**: **exacto** cuando la
  variante se puede resolver del modelo/título (`VariantFromText`, tabla de más específico a más
  genérico) y el OFP trae designador de variante —ahí `B77W` contra `B77L` **sí avisa**, que es lo
  que pidió el mantenedor—; y por **familia** (tres primeros caracteres) cuando al simulador solo le
  sale el modelo ATC. La tabla incompleta cae a familia, nunca a un aviso equivocado. Sin dato
  (`????`, vacío) no se bloquea nada.
- La variante se resuelve del `AircraftModel` **y** del `AircraftTitle`: el log ya lo enseñaba
  (`✈️ Aeronave: 777-200LR` con `📋 ICAO: B777`), solo que no se usaba para comparar. La línea del
  log pasa a ser `📋 ICAO: B77L  (modelo ATC: B777)` (`Log_IcaoResolved`) para que diga lo mismo que
  decide la validación.
- **Una pintura no lleva dígitos** (`Helpers/AircraftLivery.cs`, puro y con test): el detector de
  pintura vivía dentro `FsuipcService.GetAircraftLivery` y su último recurso devolvía cualquier
  token de 3–4 caracteres en mayúsculas, así que con el título `777-200LR` imprimía «🎨 Pintura:
  777» —el modelo disfrazado de pintura—. Si el título solo dice el modelo, no se pinta la línea.
- **El ACARS no escanea el disco del piloto** (decisión del mantenedor, v0.9.16). El nombre de la
  pintura no está en FSUIPC; se implementó resolverlo leyendo la matrícula (`0x3138`) y buscando su
  `livery.cfg` en la carpeta de paquetes del simulador, y se **retiró**: «no quiero que escanee mi
  carpeta de community; si no se puede por FSUIPC, prefiero no tener esa exactitud». Consecuencia
  asumida: un avión cuyo título solo publica el modelo no muestra pintura. No reintroducir ese
  recorrido sin que lo pida el mantenedor.
- `FlightPlannerForm` compara el OFP contra el **tipo del vuelo licitado**, no contra el
  simulador, y ahí se mantiene la igualdad exacta: el plan tiene que ser el del avión asignado.

**En X-Plane el tipo sale del `.acf` (v0.9.49).** XPUIPC **no publica** el tipo (`0x0618` vacío) y su
`0x3D00` es el nombre de la **carpeta** (`ToLissA320_V1p2p1`), de donde se deducía `A320` en un avión
cuyo `.acf` dice **`A20N`**. `Helpers/XPlaneAcfIdentity.cs` lee `P acf/_ICAO` del `.acf` cargado
(carpeta del `0x3D00` + raíz del proceso; consenso de los varios `.acf`) y **`AircraftTypeSource` dice
de dónde salió el designador**: `AcfFile` manda sobre el título y un `TitleGuess` **solo compara por
familia**. `FamilyKey` agrupa A19/A20/A21/A31/A32 —`A320` y `A20N` no comparten prefijo—. Detalle en
`Docs/architecture.md`; el origen se persiste en `flights.aircraft_type_source`.

**Un mensaje largo no puede compartir caja con los botones**: en `EcamDialog` el mensaje va en un
panel con `AutoScroll` y `Dock.Fill` y los botones en `Dock.Bottom`, con la ventana ajustada al texto
(`FitToMessage`). El porqué y `EcamDialogTests`, en `Docs/architecture.md`.

## Detección de fases — umbrales

| Transición | Condición | Debounce |
|---|---|---|
| Climb → Enroute | VS<200 fpm + cerca crucero, o timeout 5 min + VS<100 fpm | 10 s |
| Climb/Enroute → Descent | VS<−500 fpm **y** alt<máx−500 ft | 20 s |
| Enroute → Climb step | VS>500 fpm + alt<crucero−500 ft | 10 s |
| Descent → Approach | dist<10% totalDist o AGL<aglThreshold | inmediato |
| Approach → Go-around | VS>600 fpm + AGL 100–3000 ft + ≥30 s en Approach | 10 s |
| TaxiOut → TakeoffRoll | GS>30 kt + Pitch<1.0° | 5 s (v0.9.2) |
| Pushback → TaxiOut | **(freno de parqueo puesto —fin del empuje— o 300 s en la fase) + GS>5 kt** | 3 s (v0.9.40; tope 300 s desde v0.9.41) |

**Gracia de entrada de fase (v0.9.40).** El −5 de «Luces TAXI APAGADAS durante el rodaje» del PIREP
real `XPNkY9jDneZ6ByGQ` se cobró a las 01:58:56, en el mismo tick en que se declaró el (falso) rodaje
con el avión aún **remolcado** y los motores apagados: al ser una entrada de fase, era irreversible.
Desde v0.9.40 `ApproachValidator.CheckPhaseEntryLights(TaxiOut)` **no evalúa: arma**
(`ArmTaxiOutLightsCheck`), y `TickDeferredTaxiOutLights` —desde el ciclo de telemetría— decide
`TaxiOutLightsGraceSec = 5` s después, con el estado de luces **vivo**; si la fase dejó de ser
`TaxiOut`, se cancela sin cobrar. El **−5 de IVAO se difiere con la misma gracia**
(`MainViewModel.CheckIvaoAtBlocksOffWithGrace`). Rodar >5 s con el taxi light apagado **sigue
penalizando**. El helper puro es `Helpers/PhaseEntryGrace.cs` (reloj inyectable, con test).
**El tope de `PushbackMaxSec` son 300 s desde v0.9.41, no 90**: con 90 s se disparaba a mitad de
**todos** los pushbacks reales medidos —**142 s** en `XPNkY9jDneZ6ByGQ`, **148 s** en
`wk0896ajpxKalPrp`—, así que **el timeout no es la puerta habitual, solo el rescate** del piloto que
nunca pone el freno; la puerta normal es el **freno de parqueo**, que es la señal semántica correcta.
**El estado de motores NO es puerta de `Pushback → TaxiOut`, y es una decisión**: `Eng1Running`/
`Eng2Running` se derivan de N1/RPM/torque, así que «cero motores» y «el addon no lo publica» son
indistinguibles; una puerta así dejaría atrapado en `Pushback` al que no publica motores, y en este
caso no habría ayudado (a las 01:58:56 no había arrancado ninguno: arrancaron a las 01:59:06).

Umbrales elevados evitan falsas transiciones por cambios de QNH o turbulencia leve (~100 fpm). **Tren y spoilers** pasan por `Helpers/StateChangeGate` (2,5 s; la 1.ª muestra siembra): el spoiler se leía como booleano y **`0x0BD8` es posición 0–16383** —se despliega al **10 %** (`Helpers/SpoilersPosition`) y `0x0BCC` es armado, no despliegue—. **Reversas**: al desplegarse (`FirstDetection`, 10 %), no en el tick del toque. **Panel (0.9.51/0.9.52)**: el AP se decide con las banderas `0x07BC`/`0x07C4..0x0804` (`AutopilotPanel`) y **nunca con `0x07CC` —es el rumbo del bug—**; autobrake `0x2F80` (`0x0C46` = «actuando») y carteles `0x341C`/`0x341D`, solo válidos si el valor **cambia** (`SignalAvailability`). **En PMDG eso está muerto**: su bloque SDK (`Helpers/PmdgSdk`) exige `[SDK] EnableDataBroadcast=1`.

---

## Falso "TAKEOFF ROLL" durante taxi rápido — v0.9.2

La regla que quedó: **`TaxiOut → TakeoffRoll` exige `GS > 30 kt` sostenido 5 s**
(`TakeoffRollConfirmSec`), porque un pico breve durante el rodaje declaraba un despegue que ya no se
revertía y cobraba luces y QNH de salida de forma irreversible. La causa raíz completa —el
`ProjectOnRunway` que devolvía la pista por rumbo sin validar posición, y los dos guards de un solo
intento por vuelo— está en **`Docs/architecture.md` → "El falso «TAKEOFF ROLL» durante el rodaje"**.

---

## Fase de vuelo con el avión en el suelo — v0.9.43

Del PIREP real **`RBmrBApLOjWlYmO2`** (BE58, `vmsOpenACars/0.9.38`): un rebote en la toma declaró un
touch-and-go **inexistente** 5 s después del toque (88 kt, **−178 fpm**), la fase se fue a `Climb` y no
volvió —`OnBlock` sólo se alcanza desde `TaxiIn`—, así que el piloto se quedó **sin poder enviar el
PIREP** y lo único que le quedaba era cancelar, que lo borra. Cuatro reglas nuevas, cada una con su
helper puro y su test:

- **`Helpers/TouchAndGoPolicy.cs`**: además de `GS > 60` y ≥5 s del toque, exige **ascenso sostenido**
  (+200 fpm, 3 s). La velocidad no distingue rodar de despegar; la tasa vertical sí.
- **`Helpers/GroundContactGate.cs`**: la bandera de suelo se confirma **en las dos direcciones** (3
  frames). El código ya sabía que el sim publica `OnGround=0` brevemente tras el primer contacto.
- **`Helpers/StuckAirPhasePolicy.cs`**: el reparto suelo/aire **no se fía sólo de esa bandera** (<40 kt
  y sin tasa vertical no se vuela) y una fase de vuelo con el avión rodando **vuelve al suelo** a los
  15 s, a `AfterLanding` si ya aterrizó. Es la garantía de que `OnBlock` siempre es alcanzable.
- **Referencia de elevación**: con un toque capturado es el campo de **llegada** en cualquier fase. Sin
  eso la fase equivocada medía el AGL contra el **origen** y daba **264 ft** con el avión aparcado.

Y **`ON BLOCK` manual** (`FlightManager.ForceOnBlock`) en la bandeja y en el diálogo de `CANCEL`, que
ahora avisa de que cancela **eliminando** el PIREP. Hace el **mismo** cierre que la detección y
**publica la transición**: sin eso `MainViewModel` no habilita `SEND PIREP`.

## Teclas y piel MCDU — v0.9.45

Los 34 botones son teclas de cuerpo negro con la leyenda **retroiluminada** con el color del botón
(`Controls/McduKeyRenderer.cs`, puro y con test); el color ya **no rellena** (`MainButtonSpec.LegendColor`,
convenio en `UI/McduLegend.cs`). Y la ventana es un MCDU: carcasa de plástico gastado (`UI/McduChassis.cs`)
y cada grupo hundido como pantalla LCD (`Controls/McduSurface.cs`), con la paleta **muestreada de la foto
del A320** en `UI/McduPalette.cs`; lo apaga `mcdu_look`. Tabla, decisiones y cifras en **Docs/PROPUESTA-MCDU-CARCASA.md**.
**Dos trampas**: nada lleva `BackColor = Color.Transparent` sobre una superficie pintada a mano, y la rampa del plástico se calcula con `McduChassis.RampAt` (no con un degradado de GDI+ ni `PointToScreen`).
## Referencias de archivos clave

El índice de archivos está en **`Docs/architecture.md`**.

---

## Tests

`vmsOpenAcars.Tests/` (proyecto hermano de `vmsOpenAcars`, en la solución). **945 tests** (1 se omite sola):
`ScoringService` (17 criterios, umbrales en ambos lados, bonus de single-engine, suelo de 0,
casos de "sin datos de aterrizaje"), la clasificación de estado de PIREP
(`Pirep.IsActiveState`, que decide el fallback de `FilePirep()`), la geometría flat-earth y
el respaldo regional de TA/TL, el orden de posiciones ATC, la definición de "está en final"
(`SelectApproachThreshold` + `IsWithinFinalCone` + `IsPlausibleDiversionDescent` +
`IsPlausibleDiversionDistance`), el corredor de la llegada planificada (`RouteCorridor`), la puerta
de la consulta de aeropuerto de aproximación (`DiversionQueryGateTests`, con el caso real
`wk0896ajpxKalPrp`) y la cadena que decide el desvío en ese mismo vuelo
(`SkrmRealDiversionChainTests`: los cuatro filtros geométricos reproducibles offline, el cono que
para a SKCZ y el corredor ULQ4E a 285 NM; el filtro 5 —`along ≤ 0`, pediría las pistas de SKMR— y el
6 —persistencia, privada— no se pueden re-ejecutar sin red), la
comparación entre la aeronave del simulador y la del OFP (`AircraftTypeMatchTests`), la pintura del
título (`AircraftLiveryTests`), la URL de teselas de CARTO con su clave (`CartoTileUrlTests`), el
muestreo de la ruta para espacios aéreos (`AirspaceRouteSamplerTests`, con el SKCG→KBOS real), el
crédito de las teselas por proveedor (`MapAttributionTests`), el punto de espera del rodaje
(`HoldShortSelectorTests`, con los 26 puntos reales de SKBO y el inventario fijado), el reparto de
espacio del popup de confirmación (`EcamDialogTests`, el único que mide un formulario WinForms: por
eso el proyecto de tests referencia `System.Windows.Forms`) y el
RAAS con la guía de rodaje (`RaasTests`: grafo, ruta editable y avisos; `RaasReplayTests`: el
rodaje real del `MNjR664PBAr25RbD` entero contra el motor de avisos, volcado a
`%TEMP%\raas_replay_MNjR664.txt`; `TaxiRouteCaseTests`: el caso G74 → A3 de la 14L con 237 segmentos
reales de SKBO en `Fixtures/SKBO-taxi-2026-09-29.csv`).
Y desde v0.9.43, las reglas de la fase con el avión en el suelo y del motor único:
`TouchAndGoTests` (la carrera de aterrizaje del BE58 contra la máquina), `AirPhaseRescueTests`,
`GroundContactGateTests`, `StuckAirPhasePolicyTests`, `SampleClockTests`,
`SingleEngineTaxiPolicyTests` (el 46 % real, el umbral exacto, el truncado y la propiedad de que la
suma no puede aprobar lo que una fase no apruebe), `TerrainReferenceTests` (**264 ft** fantasma contra
**3 ft** reales), `ManualOnBlockTests` y `MainButtonBarTests`.
Los casos centrales usan **coordenadas, altitudes y navlogs de vuelos reales**, no geometría
inventada: el falso SKTL, el SKCG legítimo, los tres falsos de KBOS y el rodaje completo del
`MNjR664PBAr25RbD` (106 segmentos reales de SKBO y las posiciones del pushback y del rodaje).

`InternalsVisibleTo("vmsOpenAcars.Tests")` en `Properties/AssemblyInfo.cs` permite que los
tests accedan a los tipos `internal` sin tener que hacerlos públicos.

```bash
msbuild vmsOpenAcars.sln /p:Configuration=Debug
vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
```

El scoring vive en un `WinExe`, así que el proyecto de tests lo referencia vía
`ProjectReference` con `ReferenceOutputAssembly=false` (**solo** para forzar el orden de
compilación) más un `<Reference>` a `..\vmsOpenAcars\bin\$(Configuration)\vmsOpenAcars.exe`
que sí lo copia para el runtime. En **Release** el proyecto de tests no se compila.


---

## Próximas áreas

Cerradas en v0.9.8: MetarRaw en el LOGBOOK, fallback regional de TA/TL, panel ATC/ATIS
detallado y los tramos de carta sin coordenadas. Cerrada en v0.9.9: la clase de falso desvío por
alineación casual con un aeródromo de la derrota, con dos casos reales (SKTL en un SKRG→SKBQ y
28M/1B9/KOWD en un SKCG→KBOS). Pendiente por decisión de diseño, no por falta de trabajo:

- **SayIntentions.AI — investigación cerrada y NADA implementado (07/10/2026).** Todo está en
  `Docs/2026-10-07_VMSOPENACARS_SAYINTENTIONS_sayintentions_01_base-integracion.md` (claves, LVARs,
  `flight.json`, la palanca de anuncios y sus dos matices) y en `Docs/TRANSCRIPCION-ATC.md` (primera
  pieza). Orden acordado: **1) `flight.json` + LVARs al mapa → 2) `getCommsHistory` → 3) arbitraje de
  anuncios**. La documentación buena de SAPI no es la KB: es `portal.sayintentions.ai/p2d/docs/`.

- **Base de rutas de rodaje reales en NavData (respuesta recibida, base por implementar)** — caso de
  validación: `TaxiRouteCaseTests`; el pedido está en **`Docs/PEDIDO-NAVDATA-RUTAS-TAXI.md`** y
  nuestra respuesta a su respuesta en **`Docs/RESPUESTA-NAVDATA-RUTAS-TAXI-2026-09-29.md`**.
  NavData **aceptó el diseño** (contrato, umbrales y moderación experta, abajo) y ha **desplegado los
  dos campos aditivos y el arreglo de los puntos de espera**: el **nombre de la calle** (`taxiway` +
  `taxiways`), el **`node_id`** en los extremos de `/taxiways/` (52 bits, `long`) y los puntos de
  espera **desde los tipos de nodo del escenario** en vez de geometría. Pendiente suyo: **`crossings`**
  (cruces de pista, con `has_hold_short`) y la fecha de la base. Nuestro siguiente paso es el grafo
  exacto con `node_id`, que **borra el umbral de 45 m** (afectaba a **318 de los 569 segmentos de SKBO**,
  el 56%). La
  información **la mantiene NavData** y sirve a toda su comunidad: el cliente solo **envía
  observaciones** (puesto, pista, secuencia de calles) y **lee** la ruta acostumbrada, con caché
  local de la respuesta. **Nada de conocimiento local por piloto**: la fuente buena de la observación
  es el **texto que el piloto escribe o edita en el popup** (la autorización de ATC tal como la
  entiende, más limpio que la traza) y `source` distingue `typed` de `traced` (con **peso doble**
  para `typed` en su agregación). Dos lecturas, no una: la ruta acostumbrada y las **estadísticas de
  uso por calle** (`taxiway-stats`), que es lo que deja ponderar el grafo —una calle bien formada que
  nunca es de paso, como `B5`, deja de ser candidata— y arregla también donde nadie ha rodado.
  Umbrales acordados: `total ≥ 5` y `confidence ≥ 0.6` para publicar `customary`; ventana de 3 años
  con semivida de 18 meses; `customary: null` (nunca 503) por debajo; validación de las calles contra
  el dataset vigente en ingesta **y** en lectura; moderación experta (`source: "curated"`, que gana
  sobre la votación sin ocultar el apoyo). Límites: 200 observaciones por POST, 64 KB, 60 POST/min,
  5.000/día por clave, `observed_at` hasta 90 días atrás, un POST por rodaje de salida.

- **Campos del PIREP y avisos del RAAS: hechos (v0.9.16).** Lo acordado con phpVMS el 29/09/2026:
  1. **`Departure Runway`, `Arrival Runway` y `Taxi Route`** — `Helpers/PirepFields.cs` (puro, con
     `PirepFieldsTests`) arma el diccionario `fields` con **el NOMBRE del campo, no el slug** (la API
     guarda la clave como nombre y deriva el slug: con `departure-runway` el PIREP pinta el slug), y
     **omite los que no tienen valor** para que el envío posterior de la ruta no borre con `""` la
     pista del prefile. Al prefilear van las dos pistas del OFP (`ApiService.PrefileFlight`); al
     confirmar la ruta en el popup, `FlightManager.SendTaxiRouteFields` manda `Taxi Route` tal cual la
     tecleó el piloto. Y **esos tres campos salen vacíos en todo PIREP ACARS** hasta que lleguen
     valores: es normal (se declaran y se rellenan con `""`), **no es un error**.
  2. **`fuel` por posición: YA se enviaba** y no lo sabíamos; la posición se arma en
     `TelemetryCoordinator.PrepareTelemetry` (`fuel = Math.Round(e.FuelLbs, 1)`, en **libras**, que es
     su unidad interna). Sus 0 filas eran su `$fillable`, no nuestro silencio, y su arreglo activa
     datos que ya viajaban; lo que faltaba al final era su **serialización** (corregido en `AcarsRoute`):
     el valor estaba en su base y el resource no lo exponía. Seguimos enviando **libras**. **`block_fuel` y `fuel_used` van en libras desde 0.9.28**: se enviaban en **kg** a una API que interpreta el numero como libras, asi que el consumo quedaba **2,20462 veces por debajo** (lo destapo la aerolinea con 8 PIREPs y lo confirma su propia respuesta: `internalUnit: "lbs"`); el **log sigue en kg** —es lo que entiende el piloto— y la conversion vive en `Helpers/PirepMassUnits.cs`, solo en el payload. `zfw` queda **sin unidad declarada** (phpVMS lo declara `float` sin cast): pendiente de que la aerolinea diga en cual lo espera. Antes de decir
     «no enviamos X», buscarlo en **todo** el proyecto.
  3. **Avisos del RAAS al PIREP: hechos**, con el prefijo **`RAAS:`** (`FlightManager.LogRaasCallout`
     desde `TelemetryCoordinator`, con la posición y en segundo plano). **Va por `POST /acars/logs`**,
     que es lo que phpVMS recomienda: el servidor fuerza `type = 2` (LOG) y el aviso **no entra en la**
     **traza de vuelo** —con `type = 0` entraba, y ese fue el remedio equivocado que probamos primero—.
     Se lee con **`GET /acars/logs`**. Y **`fuel` sí se guardaba**: lo que fallaba era su serialización
     (corregida en `AcarsRoute`), así que seguimos enviando **libras** y el cliente no cambia.
  De su lado queda **`crossings`** de NavData y nada más de phpVMS: el endpoint global, el
  `activity_log` y un `TXI_OUT`/`TXI_IN` quedaron **descartados de mutuo acuerdo**.
  **Receta del corpus, verificada:** `?id=<piloto>&source_name=vmsOpenACars&limit=1000` funciona (0,4 s);
  **`?limit=100` o más SIN filtro devuelve 503** — el filtro es lo que reduce el conjunto, así que
  **siempre con `source_name`**. Auth: **`X-API-KEY`**.

- **El grafo y el `node_id`: hecho, medido y con el interruptor apagado (v0.9.16).** La identidad por
  id está implementada (por defecto `Suggest(..., useNodeIds = false)`), los **empalmes calculados** de
  NavData entran como aristas, el **umbral de confianza** va en dos niveles (0,5), y desde 0.9.22 el empalme llega al grafo con **su** confianza (`TelemetryCoordinator.JoinToSegment`): hasta entonces entraban todas con 1,0 y el umbral estaba **inerte** y el grafo enruta al
  **borde de plataforma** si el destino cae en otro componente. Medido sobre 37 PIREPs: **31/31 de
  cobertura con las dos políticas**, 14 idénticas, 17 distintas y **+329 m (+15%)** con ids → sigue
  **apagado**. Las tres mediciones del día, con sus correcciones, en **`Docs/architecture.md` →
  "El grafo de rodaje"**. **Ya se puede encender (30/09/2026)**: NavData cerró la conectividad —**7 de 7 con `components_after_joins: 1`**— y desaparece la razón de tenerlo apagado; nuestra medición dice que gana en precisión..
- **Tile proxy en NavData (propuesta del mantenedor, sin empezar)**: que NavData sirva las teselas
  de CARTO con **caché**, para que la clave no viaje a cada piloto y la cuota se divida por el
  número de pilotos en vez de multiplicarse. Resuelve de verdad eso, el 403 por `Referer` y el
  precalentado de los aeropuertos del OFP; **no** elimina el tráfico a CARTO (lo centraliza) y añade
  un punto único de fallo, así que el cliente debe caer a CARTO directo si NavData no responde.
  **Antes de diseñarlo hay que preguntar a CARTO si sus términos permiten cachear y reservir las
  teselas** —si el proxy se considera un servicio de teselas para terceros puede exigir plan
  comercial—. El pedido concreto para el equipo de NavData (endpoint, `style` en lista blanca para
  no ser un proxy abierto, validación de `z/x/y`, TTL y LRU) está en `Docs/PEDIDO-NAVDATA-TILES.md`. **Decidido (30/09/2026): el proxy es el camino** y se adopta en v0.9.18, con caída a CARTO. **Sin clave de CARTO no hay mapa**: responde `API KEY REQUIRED`, no una tesela marcada (medido el 30/09/2026), así que el proxy es lo único que hace funcionar el mapa en la distribución. **Adoptado en v0.9.18**: los dos proveedores piden al proxy y caen a CARTO directo si falla, con `ProxyFallbacks` y `PlaceholderTiles` para reportar.

- **SimConnect y datarefs de X-Plane: evaluado y DESCARTADO — se sigue con FSUIPC/XUIPC**
  (v0.9.16). El mantenedor lo planteó para quitar a los pilotos de MSFS el requisito de instalar
  FSUIPC7, se midió lo que se gana y lo que se pierde, y lo cerró él mismo: «mejor dejémoslo quieto,
  seguimos con fsuipc». La balanza la decidió que el estado de sistemas de un avión complejo
  (PMDG/Fenix) vive en **LVars** que SimConnect a secas no lee —lo hace FSUIPC7 con un WASM dentro
  del simulador—. El análisis completo, con lo que se gana y lo que se pierde por simulador y los
  **disparadores** que reabrirían la decisión, está en **`Docs/architecture.md` → "Capa de
  telemetría"**. No proponer la migración otra vez sin uno de esos disparadores.

- **El rumbo del avión y los rumbos de NavData están en referencias distintas (a medias).** El
  cliente lee `0x0580` (`FsuipcService._headingOffset`) y **el simulador devuelve rumbo VERDADERO**
  (evidencia: en el despegue de SKCG la aeronave sobre el eje marcó 3° con verdadero 2,31 y magnético
  10,8; en la final de KBOS 19° contra 19,67 y 33,4), mientras que **todos** los rumbos de NavData
  —pista, parkings, STAR— son **magnéticos**. **Cerrado el camino de desvíos** (29/09/2026): la
  llamada a `/nearest/approach-airport/` lleva `heading_reference=true`, así que el servidor
  convierte con el `mag_var` de cada candidato —medido en SKCG: `heading_diff_deg` de 7,8° a 0,7°—.
  **Queda pendiente el ámbito local**: `SelectApproachThreshold` compara nuestro rumbo verdadero con
  `rwy.Heading` magnético, así que arrastra un sesgo igual a la variación local —inofensivo en
  Colombia (8,4°) pero de 13,7° en Boston, donde la tolerancia de 15° se queda en ~1,3° de margen—.
  Se arregla convirtiendo antes de comparar, y **el dato ya está** (30/09/2026): `mag_var` se publica
  en `/airport/{icao}/` y en `/runways/` (medido: **-8,58** en SKBO, **-13,73** en KBOS, +0,68 en LEMD). La
  evidencia del KBOS fija el signo: 19° verdadero contra 33,4 magnético con `mag_var` -13,73 →
  **`magnético = verdadero − mag_var`**, dentro de 0,7°. **HECHA en 0.9.21**: el campo `MagVar` (`Models/NavData.cs`, 0 = degrada sin datos) y la conversión `rwy.Heading + rwy.MagVar` en las tres comparaciones de `NavDataService` (`SelectApproachThreshold` y dos en `ProjectOnRunway`), con el fixture real `KbosRwy04R()`: a 2° del eje verdadero la pista se reconoce, y sin la variación el sesgo de 13,73° la deja fuera de los 15°. Simplificación futura anotada, no bloqueo: comparar contra `TrueRunwayBearing` en vez de depender de `mag_var`.
  **No usar la puerta doble** (aceptar magnético *o* verdadero): el filtro que para el falso desvío
  de KOWD es justamente el cono angular, y ensancharlo lo reabriría.

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
- **La cadena de desvío solo se prueba offline hasta el filtro 4** (v0.9.42): el 5 (`along ≤ 0`,
  vía `SelectApproachThreshold`) pediría las pistas de SKMR y la posición del sondeo, que no están
  en lo grabado de `/nearest/approach-airport/`, y el 6 (`ConfirmPendingDiversion`) es privado. El
  corredor tampoco: el navlog ULQ4E de SKCL no viaja en esa respuesta. Lo que sí pasa el test son
  el lateral, el cono, el gradiente y la distancia, en las dos finales del `wk0896ajpxKalPrp`.
