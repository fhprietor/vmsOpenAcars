---
name: vmsopenacars-estado-del-proyecto
description: "Estado actual del desarrollo (v0.8.8), qué está completo y áreas pendientes"
metadata: 
  node_type: memory
  type: project
  originSessionId: f238ccc7-782f-45f3-9692-2f8be14998ff
---

Cliente ACARS Windows Forms (.NET 4.8, C# 7.3) que conecta simuladores con phpVMS v7 via FSUIPC/XUIPC. Versión actual: **v0.8.8**. El usuario compila desde Visual Studio 2017 (nunca desde CLI).

**Why:** El `CLAUDE.md` del repo y `Docs/architecture.md` tienen el detalle completo de arquitectura, esquema de BD y referencias de líneas clave. Leerlos antes de tocar cualquier archivo.

**How to apply:** Esta memoria solo rastrea estado de alto nivel; `CLAUDE.md`/`architecture.md` son la fuente de verdad técnica y deben preferirse siempre que haya discrepancia con este documento.

## Implementado y funcional (hasta v0.8.8)

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

## v0.8.8 — Corrección de aeropuerto de llegada distinto al planeado

Bug real reportado con vuelos de producción (SKCL→SKBO nunca llegó a SKBO; SKBO→SKCG con
emergencia y regreso a SKBO): el PIREP se fileaba con `arr_airport_id` del destino planeado
aunque el aterrizaje real fuera en otro aeropuerto, y el criterio QNH Compliance validaba
contra el METAR equivocado. Ver [[feature-map-sidebar]] no aplica aquí — detalle completo en
`CLAUDE.md` sección "Aeropuerto de llegada distinto al planeado" y en `CHANGELOG.md [0.8.8]`.

Validado en vivo contra producción (vholar.co): `PUT /api/pireps/{id}` con `arr_airport_id`
es aceptado mientras el PIREP está `in_progress`, rechazado (`503`) si ya está `Accepted` —
irrelevante para el flujo normal porque la corrección corre antes de filear.

## Áreas pendientes sin prioridad

(según sección "Próximas áreas" de `CLAUDE.md`)

- Touch-and-go real — scoring y approach buffer deben resetearse para el segundo aterrizaje.
- MetarRaw en logbook — `FlightRecord.MetarRaw` existe pero no se popula en `SnapshotLandingRecord`.
- TA/TL fallback regional — cuando NavData devuelve `null`, sin OSD ni check STD.
- Panel ATC/ATIS detallado — lista persistente de todas las posiciones activas con ATIS completo.
- Approach chart — leg CI/PI/FA sin coordenadas propias, actualmente omitidos.
