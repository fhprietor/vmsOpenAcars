ESTA DETECTANDO BIEN LA PUERTA INICIAL AL INICIAR EL PITEP, PERO NO DETECTA SPOT EN EL PUSHBACK, NI TAXIWAYS EN EL
  TAXIOUT, TAMPOCO EL SHORTHOLD. 12:29:54 - 📊 PIREP Status: APR
  12:29:53 - ✈️ Phase changed: Climb → Descent
  12:29:53 - ✈️ Descent started from 25066 ft (direct from climb)
  12:25:53 - 💡 LANDING lights OFF
  12:24:25 - 🛫 Flaps: 15% → 0%
  12:21:28 - 💡 LANDING lights ON
  12:21:28 - 💡 STROBE lights ON
  12:21:28 - 🔴 BEACON ON
  12:21:27 - ⚠️ Landing lights OFF below 10,000 ft (8627 ft)
  12:21:27 - 💡 LANDING lights OFF
  12:21:27 - 💡 STROBE lights OFF
  12:21:26 - 🔴 BEACON OFF
  12:20:47 - 🛬 Gear UP
  12:20:46 - 📊 PIREP Status: ICL
  12:20:44 - ✈️ Phase changed: Takeoff → Climb
  12:20:44 - 🛫 LIFTOFF! Speed: 179 kts, VS: 63 fpm
  12:20:44 -    OAT: 16°C | Wind: 1@360°
  12:20:44 -    Flaps: 15%
  12:20:44 -    N1: 83% / 83%
  12:20:44 -    Heading: 359°
  12:20:44 -    Pitch: 6.9° | Bank: 0.3°
  12:20:44 -    Ground Speed: 180 kts
  12:20:44 -    Rotation Speed: 158 kts
  12:20:44 - 🛫 ACCURATE TAKEOFF DATA:
  12:20:42 - 📊 PIREP Status: TOF
  12:20:41 - ✈️ Phase changed: TakeoffRoll → Takeoff
  12:20:41 - 🛫 TAKEOFF DETECTED - Speed: 170 kts, Alt: 7021 ft, VS: 140 fpm
  12:20:41 - 🛫 ROTATION at 170 kts, Pitch: 2.1°
  12:20:02 - 📊 PIREP Status: INI
  12:20:01 - ✅ QNH | Avión: 1024 hPa  SKRG: 1024 hPa  Δ0 hPa
  12:20:01 - 🛫 RWY 01 | LINEUP: 0 ft from threshold | CL: 4 ft deviation
  12:20:01 - ✈️ Phase changed: TaxiOut → TakeoffRoll
  12:20:01 - 🛫 Takeoff roll started at 31 kts
  12:18:58 - 💡 LANDING lights ON
  12:18:55 - 💡 NAV lights ON
  12:18:55 - 💡 STROBE lights ON
  12:18:54 - 💡 NAV lights OFF
  12:13:19 - 📊 PIREP Status: TXI
  12:13:17 - ✈️ Phase changed: Pushback → TaxiOut
  12:13:17 - 🛻 Taxi out (after pushback) at 6.0 kts
  12:11:20 - 🅿️ Parking Brake: RELEASED
  12:10:19 - 🛫 Flaps: 1% → 15%
  12:10:18 - 🛫 Flaps: 0% → 1%
  12:09:50 - 🅿️ Parking Brake: SET
  12:09:07 - 🔄 Engines started
  12:07:55 - 📊 PIREP Status: PBT
  12:07:55 - ⏱️ Block Off recorded at 12:07:55 UTC
  12:07:54 - ✅ Conectado en IVAO (VID 194102)
  12:07:54 - 🛫 Block Off (entering Pushback)
  12:07:54 - ✈️ Phase changed: Boarding → Pushback
  12:07:54 - 🔄 Pushback confirmed at 3.0 kts
  12:04:33 - 🔴 BEACON ON
  11:59:53 - 💡 NAV lights ON
  11:57:43 - 🅿️ Parking Brake: RELEASED
  
# Changelog - vmsOpenAcars

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
