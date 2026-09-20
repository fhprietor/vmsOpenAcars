---
name: vmsopenacars-estado-del-proyecto
description: "Estado actual del desarrollo (v0.9.2), qué está completo y áreas pendientes"
metadata: 
  node_type: memory
  type: project
  originSessionId: f238ccc7-782f-45f3-9692-2f8be14998ff
---

Cliente ACARS Windows Forms (.NET 4.8, C# 7.3) que conecta simuladores con phpVMS v7 via FSUIPC/XUIPC. Versión actual: **v0.9.2**. El usuario compila desde Visual Studio 2017 (nunca desde CLI).

**Why:** El `CLAUDE.md` del repo y `Docs/architecture.md` tienen el detalle completo de arquitectura, esquema de BD y referencias de líneas clave. Leerlos antes de tocar cualquier archivo.

**How to apply:** Esta memoria solo rastrea estado de alto nivel; `CLAUDE.md`/`architecture.md` son la fuente de verdad técnica y deben preferirse siempre que haya discrepancia con este documento.

## Implementado y funcional (hasta v0.9.2)

- **Scoring** (14 criterios + bonus SET): Landing Rate, G-Force, Bank, Pitch, Overspeed, Lights, Stabilized Approach, QNH, IVAO, On-Time Departure, TDZ, Centreline, ILS/Localizer, Minimums, Procedure Speed. Bonus Single Engine Taxi (+5 pts) condicionado por tipo de motor desde v0.8.7 (Piston nunca elegible / Turboprop siempre / Jet solo si cumple warm-up + cool-down).
- **NavDataClient + NavDataService** — reemplazó por completo a `RunwayService` (ya no existe en el proyecto). API REST; prefetch paralelo de 6 endpoints; caché en memoria por ICAO.
- **NavDataCache** (`NavData_cache.sqlite`) — caché SQLite persistente entre sesiones (airport_entries, navaid_entries, airspace_entries); invalidación automática por ciclo AIRAC; purga selectiva de airport/navaid vía `PurgeAirportData()` (v0.7.0) sin afectar airspaces.
- **MapForm** (GMap.NET) — ruta suavizada fly-by/fly-over, sidebar de procedimientos (pista/SID/STAR/approach + transiciones) totalmente implementado y estable desde v0.6.5 (ver [[feature-map-sidebar]]), overlays de airspace y ATC IVAO (formas estilo WebEye), 3 proveedores de mapa, capas toggleables TILES/ROUTE/SPACES/IVAO.
- **AirspaceMonitorService** (v0.7.1) — alertas Prohibited/Restricted/Danger (entrada + predictivas + sobrevuelo), entrada/salida CTR/TMA/RMZ, polling ATC/ATIS IVAO con filtrado por distancia y fase de vuelo.
- **Engine Lifecycle Monitor** (v0.8.5–v0.8.7) — `EngineStartMonitor` (warm-up en ralentí antes de TOGA) + `ThrustReverserMonitor` (cool-down post-reversa) + `EngineMonitorPanel` (N1/RPM/TRQ, IDLE, OIL, barra de reversa). Ahora condiciona el bonus Single Engine Taxi para jets.
- **Cabin Announcements** — MP3 desde NavData API, NAudio, 7 fases, idioma automático según país de la aerolínea.
- **OSD Overlay** — 4 severidades, chimes, fade-in/out, incluye alertas de airspace predictivas/sobrevuelo.
- **Landing Analysis** — historial SQLite, 4 gráficos, modo comparación.
- **Recuperación de vuelo** — retomar PIREP IN_PROGRESS con historial ACARS + checkpoints CHK + recarga OFP SimBrief.
- **Checkpoints scoring** — envío automático cada 60 s a tabla ACARS phpVMS.
- **SystemInfoHelper** — OS/RAM/GPU sin WMI, enviado al log ACARS al iniciar vuelo.

## v0.8.8–v0.9.1 — Aeropuerto de llegada distinto al planeado

Bug real reportado con vuelos de producción (SKCL→SKBO nunca llegó a SKBO; SKBO→SKCG con
emergencia y regreso a SKBO; SKRG→SKCL que en realidad voló y aterrizó en SKBO; SKLT→SKBO con
falsos positivos de desvío durante giros de STAR): el PIREP se fileaba con `arr_airport_id` del
destino planeado aunque el aterrizaje real fuera en otro aeropuerto, y el criterio QNH
Compliance validaba contra el METAR equivocado. Detalle completo en `CLAUDE.md` sección
"Aeropuerto de llegada distinto al planeado" y en
`CHANGELOG.md [0.8.8]`/`[0.8.9]`/`[0.8.10]`/`[0.9.0]`/`[0.9.1]`.

- **v0.8.8:** detección vía cross-reference local (destino → alterno → origen →
  `GetNearestAirport` de phpVMS) + corrección de `arr_airport_id`/`curr_airport_id`. Validado
  en vivo contra producción (vholar.co): `PUT /api/pireps/{id}` con `arr_airport_id` es
  aceptado mientras el PIREP está `in_progress`, rechazado (`503`) si ya está `Accepted`.
- **v0.8.9:** el equipo de NavData expuso `GET /nearest/approach-airport/`, que reemplaza la
  cadena dest→alt→origin por una re-confirmación continua durante toda la aproximación y
  resuelve correctamente **pistas paralelas** (validado para SKBO 14L/14R). phpVMS a su vez
  dejó de inferir diversiones desde `alt_airport_id` y ahora requiere el campo
  `diversion-airport` explícito en el pirep — ya integrado (`PirepBuilder.BuildPayload` como
  `Dictionary`, clave ausente si no hay desvío).
- **v0.8.10:** caso descubierto donde `FlightPhase.Approach` **nunca se alcanza** — causa raíz
  en `FlightManager.Telemetry.cs` (`DistanceToDestinationNm = -1` hardcodeado siempre; la
  rama de altitud usa la elevación del destino planeado, que puede diferir miles de ft del
  aeropuerto real). Fix en tres partes: (1) detección extendida a `FlightPhase.Descent`, con
  `{Descent, Approach}` tratado como superestado; (2) retiro del fallback roto de
  `GetNearestAirport` en touchdown (404 confirmado en producción); (3) QNH de llegada
  rediseñado como provisional en vuelo, confirmado/revertido recién al filear el PIREP (estilo
  "comisarios de F1") — `ApproachValidator.CheckArrivalQnhProvisionalAsync`/
  `FinalizeArrivalQnhAsync`.
- **v0.9.0:** vuelo real de prueba tras v0.8.10 reveló dos problemas nuevos. (1) La elevación
  de referencia (AGL) seguía usando el destino planeado incluso tras confirmar el desvío —
  `⚠️ Landing log no grabado: solo 0 puntos en buffer` confirmado en vivo. Fix: nuevo
  `FlightManager.SetArrivalAirportElevation`/`ArrivalAirportElevationFt` + `NavDataService.
  GetAirportElevationFt`, seteado en los mismos call sites que ya confirman el desvío;
  corrige `ReferenceAirportElevation`, `BuildPhaseInput().DestinationElevation` (resuelve de
  rebote la limitación de v0.8.10 sobre Stabilized Approach) y `altitude_agl` enviado a
  phpVMS. (2) `MovePilotAsync` confirmado roto (`405`, ruta no soporta PUT) — retirado de
  `FilePirep()`; confirmado en el mismo vuelo que phpVMS reubica al piloto por su cuenta vía
  `diversion-airport`.
- **v0.9.1:** otro vuelo real (`9QJYgErnggMdgKPO`, SKLT→SKBO) reveló falsos positivos de
  desvío durante giros de STAR — un aeropuerto cruzado de pasada, a >11 NM del eje de su pista,
  se marcaba como desvío por coincidencia casual de heading, y ese flag **nunca se revertía**
  aunque el tracking local reconfirmara el destino planeado segundos después (sobrevivió un
  touch-and-go y una segunda aproximación completa; el PIREP se fileó igual con el aeropuerto
  falso). Fix de dos capas: (1) filtro de plausibilidad geométrica vía `cross_track_nm` (ya
  devuelto por NavData, antes descartado) — exige ≤3 NM antes de aceptar un match como desvío
  genuino; (2) nuevo `FlightManager.ClearDivertedAirport()`, revierte los flags cuando un poll
  posterior reconfirma el destino planeado. Detalle completo en `CLAUDE.md` sección "Falsos
  positivos de desvío durante giros de STAR" y `CHANGELOG.md [0.9.1]`.

## v0.9.2 — Falso "TAKEOFF ROLL" durante taxi rápido

PIREP real en curso (`E7DK47e88XdabzoL`, SKRG rwy01→SKBQ) reportado por el usuario: un pico
breve de GS>30 kt durante el rodaje (calle paralela a la pista 01/19) disparó
`FlightPhase.TakeoffRoll` — única transición de `FlightPhaseStateMachine.cs` sin debounce — y
antes de que revirtiera a `TaxiOut` 2 s después, ya se habían aplicado de forma irreversible
tres penalizaciones de -5 pts (landing lights, strobe, QNH de salida) pensadas para un despegue
real. `NavDataService.ProjectOnRunway` agravaba el síntoma: reportó "PISTA 19" con 614 ft de
desviación de centerline (geométricamente imposible) en vez de fallar limpio, porque no tenía
fallback a `null` cuando ninguna pista pasaba el chequeo de footprint. Fix de tres partes:
(1) debounce de 5 s sostenidos en `TaxiOut→TakeoffRoll`, mismo patrón que el resto de la máquina
de fases; (2) `ProjectOnRunway` retorna `null` si ninguna pista pasa `WithinFootprint`;
(3) guards de un solo disparo por vuelo (`_departureQnhChecked`/`_takeoffLightsChecked`) para
evitar doble penalización en un rebote de fase o un rechazo de despegue real. Detalle completo
en `CLAUDE.md` sección "Falso TAKEOFF ROLL durante taxi rápido" y `CHANGELOG.md [0.9.2]`.

## Áreas pendientes sin prioridad

(según sección "Próximas áreas" de `CLAUDE.md`)

- Touch-and-go real — scoring y approach buffer deben resetearse para el segundo aterrizaje.
- MetarRaw en logbook — `FlightRecord.MetarRaw` existe pero no se popula en `SnapshotLandingRecord`.
- TA/TL fallback regional — cuando NavData devuelve `null`, sin OSD ni check STD.
- Panel ATC/ATIS detallado — lista persistente de todas las posiciones activas con ATIS completo.
- Approach chart — leg CI/PI/FA sin coordenadas propias, actualmente omitidos.
