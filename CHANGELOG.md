# Changelog - vmsOpenAcars

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
