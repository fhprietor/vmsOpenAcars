# Changelog - vmsOpenAcars

## [0.3.1]

### Added

- 


### Fixed

- PROGRESO. DIST: 0/0 NM
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
