---
name: vmsopenacars-estado-del-proyecto
description: "Estado actual del desarrollo (v0.6.5 en progreso), qué está completo y áreas pendientes"
metadata: 
  node_type: memory
  type: project
  originSessionId: f238ccc7-782f-45f3-9692-2f8be14998ff
---

Cliente ACARS Windows Forms (.NET 4.8, C# 7.3) que conecta simuladores con phpVMS v7 via FSUIPC/XUIPC. Versión actual: **v0.6.5 (en desarrollo — código escrito, pendiente compilar)**. El usuario compila desde Visual Studio 2017 (nunca desde CLI).

**Why:** El CLAUDE.md del repo tiene el detalle completo de arquitectura, esquema de BD y referencias de líneas clave. Leerlo antes de tocar cualquier archivo.

**How to apply:** La memoria solo rastrea estado de alto nivel; el CLAUDE.md es la fuente de verdad técnica.

## Implementado y funcional en v0.6.4

- **Scoring** (14 criterios + bonus SET): Landing Rate, G-Force, Bank, Pitch, Overspeed, Lights, Stabilized Approach, QNH, IVAO, On-Time Departure, TDZ, Centreline, ILS/Localizer, Minimums, Single Engine Taxi (+5 pts), Procedure Speed.
- **NavDataClient + NavDataService** — API REST; prefetch paralelo 6 endpoints; caché en memoria por ICAO.
- **NavDataCache** (`NavData_cache.sqlite`) — caché SQLite persistente entre sesiones; invalidación automática por ciclo AIRAC; ~96% menos peticiones por sesión.
- **MapForm** (GMap.NET) — ruta suavizada fly-by/fly-over, proyecciones virtuales sin SID/STAR, waypoints ambient, anillos, línea punteada al alterno.
- **Cabin Announcements** — MP3 desde NavData API, NAudio, 7 fases, idioma automático.
- **OSD Overlay** — 4 severidades, chimes, fade-in/out.
- **Landing Analysis** — historial SQLite, 4 gráficos, modo comparación.
- **Recuperación de vuelo** — retomar PIREP IN_PROGRESS con historial ACARS + checkpoints CHK + recarga OFP SimBrief.
- **Checkpoints scoring** — envío automático cada 60 s a tabla ACARS phpVMS.
- **SystemInfoHelper** — OS/RAM/GPU sin WMI, enviado al log ACARS al iniciar vuelo.

## En desarrollo — v0.6.5 (código escrito, NO compilado aún)

**Sidebar de procedimientos en MapForm** — ver [[feature-map-sidebar]] para todos los detalles de implementación.

Resumen: panel lateral izquierdo colapsable (230 px) con selección en tiempo real de pista, SID/trans, STAR/trans y aproximación para origen y destino. Validación de compatibilidad SID/STAR con EcamDialog. Overlay de aproximación independiente (_approachOverlay). Chips de viento HW/TW+XW desde METAR. Callback OnProcedureChanged → MainViewModel.UpdateProcedureOverrides.

**Próximo paso:** compilar en VS2017, resolver errores si los hay, probar funcionalmente.

## Áreas pendientes sin prioridad

- Touch-and-go real: verificar reset de scoring e approach buffer para el segundo aterrizaje.
- MetarRaw en logbook: `FlightRecord.MetarRaw` existe pero no se popula en `SnapshotLandingRecord`.
- DA calculada dinámicamente desde approach_leg (actualmente threshold_elevation + 200 ft constante).
- TA/TL null en NavData: podría añadirse fallback regional.
- MapForm: arcos DME/RF (InterpolateArcLegs ya implementado; verificar con múltiples arcos encadenados).
- NavDataCache: purga selectiva por aeropuerto (actualmente se purga todo el ciclo en bloque).
