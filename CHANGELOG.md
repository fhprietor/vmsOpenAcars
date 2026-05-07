# Changelog - vmsOpenAcars

## [0.4.3]

### Fixed

- **Logbook columnas vacías (Flight, Route, RWY, VS, G)** — `FilePirep()` llama internamente a `ResetFlightState()` que pone `_activePlan = null` y resetea todos los campos de touchdown antes de retornar. `SaveLandingRecord()` se ejecutaba después y encontraba todo vacío. Solucionado con `SnapshotLandingRecord()`, que captura el plan y los datos de touchdown **antes** de awaitar `FilePirep()`. El `Score` se añade después porque `LastFlightScore` es la única propiedad que `ResetFlightState()` no borra.

## [0.4.2]

### Added

- **Penalización por salida fuera de horario** — se descuentan 5 puntos si el vuelo despega con más de ±10 minutos respecto al STD (`sched_out`). El aviso se emite en el log en el momento del Blocks Off con el delta y la dirección (`early`/`late`). Se añade el criterio `On-Time Departure` al desglose del score.
- **AGL real en crucero (Enroute)** — el AGL durante la fase de crucero ahora se calcula restando la elevación real del terreno bajo el avión (offset FSUIPC `0x0020`) en lugar de la elevación del aeropuerto de origen. Corrección visible cuando se sobrevuela zonas montañosas.
- **QNH verificado en aterrizaje** — el QNH de destino ahora se comprueba en el gate de 1000 ft AGL (igual que la salida se comprueba en TakeoffRoll). Antes se comprobaba al entrar en la fase Approach (~20 NM del aeropuerto), demasiado pronto para que el piloto hubiera sintonizado el QNH local. La penalización máxima por QNH sube de 5 a **10 pts** (5 salida + 5 llegada independientes).
- **Recuperación Descent → Climb** — si en fase Descent el VS supera +500 fpm durante 20 s y el avión no está en zona de aproximación al destino, la fase vuelve automáticamente a Climb. Cubre el caso de una falsa transición por cambio de QNH en salidas con altitud restringida.

### Changed

- **Umbrales de detección Climb/Enroute → Descent más tolerantes** — el umbral de VS para iniciar el descenso sube de −100/−300 fpm a **−500 fpm**, y el debounce de 10 s a **20 s**, en ambas fases (Climb y Enroute). Evita que un cambio de QNH (~100 fpm de fluctuación aparente) dispare erróneamente la transición a Descent.

### Fixed

- **GEAR UP sin AGL** — la transición "Gear UP" no mostraba altitud AGL en el log. Se debía a una condición de carrera entre el evento `GearChanged` y la actualización de telemetría (`UpdateTelemetry`). Corregido leyendo `_fsuipc.CurrentAltitudeFeet` directamente en el handler, sin depender de `FlightManager.CurrentAGL`.
- **Pista paralela incorrecta en captura de aproximación** — `GetRunwayThreshold()` solo usaba el rumbo para identificar la pista, seleccionando 14L en lugar de 14R (SKBO) porque ambas tienen rumbos similares. Ahora se añaden `lat`/`lon` del avión como parámetros y se usa la distancia lateral al eje de cada pista como desempate, eligiendo la pista cuyo eje esté más cerca del avión.
- **Flight Planner mostraba solo la primera página de vuelos** — `GetAvailableFlightsFromAirport` ahora itera todas las páginas de `api/flights` (paginación Laravel igual que la flota), mostrando la lista completa de vuelos disponibles.
- **Columnas del Flight Planner no ordenaban** — el grid de vuelos disponibles ahora ordena por cualquier columna al hacer clic en el encabezado (segundo clic invierte el orden). El orden inicial al cargar es por número de vuelo ascendente.
- **App.config sobreescrito al compilar Release** — en configuración Release, MSBuild ya no copia `App.config` → `vmsOpenAcars.exe.config` (`<AppConfig></AppConfig>` en el PropertyGroup de Release), y se eliminó el PostBuildEvent que copiaba `App.Release.config`. El archivo de configuración de producción en `bin\Release\` queda intacto.

## [0.4.1]

### Fixed

- **Cuenta atrás ETD usa blocks-off real** — el countdown del panel FMA ahora cuenta hacia el tiempo de `sched_out` (blocks off / inicio de pushback) en lugar de `sched_off` (wheels off). La diferencia era exactamente el taxi_out de SimBrief (ej: 20 min), mostrando la hora de despegue en lugar de la hora de salida. `SimbriefPlan.ScheduledOutTime` mapea `times.sched_out`; `ScheduledOffTime` mantiene `times.sched_off` para la fecha en el FMA.

## [0.3.19]

### Added

- **Debounce en Spoilers** — se implementó un sistema de filtrado (debounce) de 1.5 segundos para el estado de los spoilers, evitando falsos positivos o parpadeos en el log y en el estado del vuelo por ruido en la señal del simulador.
- **Excepción Beacon para switch compartido** — la penalización de Beacon apagado en vuelo ya no aplica a aeronaves con switch único beacon/strobe (ej: Q400/Dash 8), donde encender strobes apaga automáticamente el beacon. La lista de excepción está en `BeaconStrobeSharedAircraft`.
- **Taxi position: calle actual + próxima intersección** — el log de rodaje ahora muestra la calle por la que se rueda y la siguiente intersección por delante. Ej: `↳ CALLE C, Próximo a B7`. Cuando no hay intersección a la vista, muestra solo la calle actual como antes. Implementado en `RunwayService.FindNextIntersection()`. Además, durante `AfterLanding` se detecta la calle por la que se abandona la pista (`🛬 PISTA DESOCUPADA por CALLE K7`) y se activan los updates de posición en rodaje de llegada.
- **Log de inicio de captura de aproximación** — al comenzar la captura de telemetría de aproximación (AGL < 3000 ft), se registra en el log la pista detectada, el AGL y la distancia al umbral. Ej: `📡 INICIO CAPTURA APROX: PISTA 24R | AGL 2850 ft | Dist 8.3 NM`.

### Changed

- **Refactor Debounce** — `DebounceLight` renombrado a `DebounceState` genérico, aceptando el tiempo de debounce como parámetro. Todas las luces y spoilers comparten ahora la misma lógica.

### Fixed

- **Falso touchdown en despegue** — la detección de tomacontacto ahora solo se activa en fases de Descent, Approach o Landing. Los flickers del flag `SimOnGround` de FSUIPC durante Takeoff/Climb (rebotes en rotación, glitches del simulador) ya no disparan una transición incorrecta a `AfterLanding`.
- **Detección de pista en aeropuertos con pistas paralelas** — `FindTouchdownRunway()` ahora verifica que la posición del avión esté dentro de la huella (`WithinFootprint`) de la pista. Si la pista más cercana por rumbo no contiene el punto de toma de contacto, busca otra pista con rumbo similar que sí lo contenga. Corrige falsos positivos en KLAX (24L/24R), LEBL (25L/25R), etc.

## [0.3.18]

### Added

- **FMA Line 3** — ahora muestra la ruta completa del vuelo extraída de SimBrief (`RTE ...`).
- **Tercer escenario de Blocks Off** — se añadió el registro automático del "Blocks Off" al encender motores durante la fase de `Boarding`. Esto permite capturar correctamente el inicio del vuelo en aviones pequeños o posiciones de parking que no requieren pushback.
- **Penalización luz Beacon** — si la luz Beacon se apaga en cualquier momento mientras el avión está en vuelo (AIRBORNE), se registrará una infracción en la telemetría y penalizará el puntaje final.
- **Scoring en el Landing Log** — la puntuación final de cada vuelo ahora se expone en `LastFlightScore` y se almacena correctamente en la base de datos `landing_log.sqlite` al registrar el aterrizaje.

### Fixed

- **Fallo en captura de telemetría de aproximación** — resuelto un problema crítico donde el filtro de captura dependía del radar altímetro nativo (el cual enviaba valores inválidos desde un offset erróneo). Ahora se utiliza el `CurrentAGL` interno (MSL − elevación del aeropuerto de destino), asegurando que los datos de aproximación (por debajo de 3000 ft) se registren siempre de forma fiable.
- **Detección fallida de pista en aproximación (Approach Threshold)** — el sistema antes comprobaba el rumbo del avión *únicamente* en el momento exacto en el que pasaba a fase de Aproximación. Ahora verifica continuamente hasta encontrar alineación, evitando que los datos de aterrizaje se pierdan si el avión entró a la aproximación volando en viento en cola o desviado.
- **Cálculo AGL en ascenso** — resuelto el bug en la fase de CLIMB donde el AGL calculado retornaba el MSL debido a que la elevación de origen no era provista adecuadamente por SimBrief.
- **Offset del Radar Altímetro** — se ha cambiado de `0x0234` (ADF2) al offset correcto `0x31E4` de FSUIPC, permitiendo leer la distancia radial real hacia el suelo en fases críticas.
- **Regla de 10,000 ft usando MSL** — la penalización de luces por debajo de los 10,000 pies ha sido actualizada para utilizar la altitud AGL real del terreno en lugar del MSL. Ya no penaliza falsamente al despegar de aeropuertos de gran altitud como Bogotá (SKBO).
- **AGL en modo Crucero** — el AGL en fase de vuelo crucero (`Enroute`) ahora reporta el MSL de forma predeterminada como lo solicitan los pilotos, en lugar de intentar leer el radar altímetro a grandes altitudes.
## [0.3.17]

### Added

- **Resumen del plan al inicio del log** — al pulsar START, el primer registro del log muestra los datos del plan de SimBrief: vuelo, ruta, aeronave, matrícula, fecha, PAX, combustible, nivel de crucero y carga. Mismo contenido que el panel FMA.
- **AGL en eventos de luces** — los cambios de NAV, STROBE, LANDING y BEACON ahora incluyen la altitud AGL cuando el avión está por encima de 50 ft. Ejemplo: `💡 LANDING lights ON (10 240 ft AGL)`.
- **AGL en cambios de tren de aterrizaje** — `🛬 Gear UP` y `🛬 Gear DOWN` incluyen la altitud AGL en las mismas condiciones.

### Fixed

- **Detección de taxiways y holding short no funcionaba** — `RunwayWidthScale` reducido de `1.5` a `1.0`. Con el multiplicador anterior, el footprint de detección de pista se extendía hasta los taxiways paralelos cercanos (p. ej. TWY A a 36 m del eje en SKBO), lo que hacía que el avión fuera detectado como "en pista" durante todo el rodaje y se saltara la detección de taxiways, holding short y entrada a pista.

---

## [0.3.16]

### Added

- **Landing Analysis** — nuevo sistema de historial de aterrizajes almacenado en una base de datos SQLite local (`landing_log.sqlite`).
  - Botón **LOGBOOK** en la pantalla principal abre el historial de vuelos.
  - Cada aterrizaje registra: vertical speed, G-force, distancia al umbral, desviación de centreline, score y METAR.
  - Durante la fase de aproximación (AGL < 3 000 ft) se captura automáticamente la trayectoria cada 2 segundos.
- **LandingAnalysisForm** — ventana de análisis con 4 gráficos interactivos:
  - *Vertical Profile*: AGL vs distancia al umbral, con línea de referencia de planeo 3°.
  - *Lateral Deviation*: desviación de centreline (±ft) con línea cero.
  - *IAS*: velocidad indicada con línea de Vref promedio.
  - *Vertical Speed*: VS en fpm con línea cero.
  - Suavizado Gaussiano aplicado a Lateral, IAS y VS para mejorar la visualización.
  - Eje X invertido: 5 NM a la izquierda → umbral a la derecha.
- **Modo comparación** — selecciona entre 2 y 5 vuelos en el LOGBOOK y pulsa **COMPARE** para superponer sus trayectorias en los 4 gráficos, cada vuelo con un color distinto.
- **Borrado de registros** — botón **DELETE** en el LOGBOOK con confirmación antes de eliminar (soporta selección múltiple).

### Setup — Landing Log database

> Esta configuración es necesaria la primera vez que se usa el LOGBOOK.

1. Abre **Settings** y ve a la sección **Landing Log**.
2. Haz clic en el botón **[...]** y selecciona un archivo `.sqlite` existente, o escribe un nombre nuevo (p. ej. `landing_log.sqlite`) para crearlo.
3. Guarda la configuración. La base de datos se crea automáticamente al registrar el primer aterrizaje.

La base de datos es un archivo SQLite estándar; puedes hacer copias de seguridad simplemente copiando el archivo.

### Fixed

- El selector de archivo de la base de datos de Landing Log usaba `SaveFileDialog` (pedía confirmación de reemplazo); reemplazado por `OpenFileDialog` con `CheckFileExists = false`.
- Error en runtime al comparar dos vuelos con el mismo callsign: nombre de serie duplicado en `SeriesCollection` — corregido añadiendo índice al nombre de cada serie.
- Botón **SEED DEMO DATA** ahora solo visible en builds Debug (`#if DEBUG`).
- Proyecto `Updater` no compilaba por `App.config` faltante.

---

## [0.3.15]

### Added

- Ground operations tracking feature added to vmsOpenAcars using the LittleNavMap DB.

## [0.3.14]

### Added

- LittleNavMap SQLite runway scoring
  (touchdown zone and centerline deviation), touch-and-go detection, and LNM DB availability checks

### Fixed

- METAR retrieval process indicator while awaiting server response
- Touch-and-go detection. So a second landing after liftoff from AfterLanding captures fresh touchdown data for scoringTouch-and-go detection and storage of the PIREP with the second landing

Resumen de todo lo implementado:

Db/RunwayService.cs — consulta LNM SQLite: encuentra el aeropuerto por ICAO, itera los runway ends, elige el que esté
dentro de ±45° del heading del avión, y proyecta el punto de touchdown sobre el eje de pista con geometría flat-earth
para calcular distancia al umbral y desviación de centreline.

FlightManager — captura CurrentHeading cada ciclo de telemetría; en RegisterTouchdown guarda lat/lon/heading del
momento exacto; SetRunwayTouchdownData() permite al ViewModel inyectar los resultados; todo se resetea en
ResetFlightState y al detectar touch-and-go.

MainViewModel — al recibir TouchdownDetected (que ya incluye lat/lon/heading precisos de FSUIPC), lanza Task.Run →
LookupRunwayData → log de resultados → SetRunwayTouchdownData en FlightManager.

ScoringService — dos criterios nuevos:

- Touchdown Zone: 0 pts ≤1500 ft · −3 pts 1500-2500 ft · −7 pts >2500 ft
- Centreline: 0 pts ≤10 ft · −3 pts 10-30 ft · −7 pts >30 ft

SettingsForm — nueva sección "NavMap Database" con campo de texto + botón "..." para seleccionar el archivo, guardado
en la clave lnm_db_path.

## [0.3.12]

### Added

- METAR feature fully implemented. Added MetarService state machine,4 panels (ORIG/DEST/ALT/ENRT), MetarDecodeForm decode popup, and 4-station
- IVAO online verification with score penalty

## [0.3.11]

### Added

- OFP basic data on FMA panel

### Fixed

- duplicate log events
- false takeoff detection after landing
- Phase self-transitions
- and fuel unit inconsistencies
- MSFS autopilot display fix. AP state changes now log to the visible flight log
- AGL cruise
- MACH under FL250
- Countdown:
  - Only visible when a plan is loaded and the phase is Idle or Boarding
  - Disappears when the block off is registered (phase changes to Pushback or later)
  - Green → More than 5 minutes remaining (H:MM if > 1 hour, MM:SS if < 1 hour)
  - Yellow → 5 minutes or less remaining
  - Red → Delayed, displays the +MM:SS of the delay

## [0.3.10]

### Fixed

- stabilized approach fixes
- Q400 vmo -> 285

## [0.3.2]

### Added

- METAR check on departure and arrival

### Fixed

- Fuel units (from simbrief plan)
- fUEL INI: 2115 KGS (REAL 4843, PARECE CONVIERTE A KG NUEVAMENTE, CREYENDO QUE ESTAN EN LBS)
- USADO: 0, ff 83KG (REAL 89.7)
- N2 EN CERO
- EGT 3493 (723 REAL)
- FF 41 KG/H (REAL 1.04)

## [0.3.0]

### Added

- Aircraft category detection
- Adaptive information layout by category

### Fixed

- DIST: 0/0. fixed
- Fuel units and fuel flow value 0. fixed
- AGL varies chaotically. Fixed
- Strobe appears ON when NAV is in the center/OFF position. Fixed

---

## [0.2.9] - 2026-04-20

### Added

- Real-time Flight Information Panel
- Flap indicator by aircraft type (Airbus/Boeing)
- Engine panel with N1/N2/EGT/FF
- Light detection (NAV, BEACON, LANDING, TAXI, STROBE)
- Autobrake detection by family

### Changed

- `CurrentFlapsPosition` from double to string
- `UpdateFlightInfoPanel()` now uses usa `FlapsLabel`

### Fixed

- Flaps always displaying "UP"
- Fuel_used set to zero in PIREP
- OnBlock without parking brake and engines off
