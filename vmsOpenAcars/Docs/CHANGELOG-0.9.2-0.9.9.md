# Changelog resumido — v0.9.2 → v0.9.9

> Resumen breve de los cambios entre la v0.9.2 y la v0.9.9. El detalle completo, con
> evidencia de vuelo y justificación de cada decisión, está en `Docs/CHANGELOG.md`.

---

## [0.9.9] — 24/09/2026
**Falsos desvíos de llegada (dos casos reales: SKRG→SKBQ y SKCG→KBOS).**

- La detección de desvío exige ahora **seis filtros independientes**: lateral (≤3 NM), cono
  angular (4° con suelo de 0.25 NM), gradiente de descenso (≤700 ft/NM), alterno más cercano
  que el destino, establecido en final (`along ≤ 0`) y persistencia de 2 sondeos.
- **Nuevo corredor de la llegada planificada** (`Helpers/RouteCorridor.cs`): 5 NM a cada lado
  de los últimos 40 NM del navlog de SimBrief. Dentro del corredor no se marca desvío.
- `SelectApproachThreshold`/`ThresholdHeading` proyectan con el **bearing verdadero** (antes
  magnético: 2.8 NM de error a 19 NM de distancia).
- `LookupRunwayData` prueba en orden: aeropuerto resuelto → origen → último alterno →
  **destino planeado**.
- Nuevas claves de log de rechazo (una sola vez por aeródromo y motivo) y
  `GeoMath.DistanceToSegmentNm`.
- Tests: `ApproachThresholdTests` (26) y `RouteCorridorTests` (7), con coordenadas y altitudes
  de vuelos reales.

## [0.9.8] — 21/09/2026
- **Eliminado todo el andamiaje de simulación**: `MockSimulator`, `SeedMockData` y el botón
  SEED DEMO DATA → el LOGBOOK arranca vacío.
- Eliminados ~12,5 MB de PDF/HTML/PNG obsoletos en `Docs/` (el BRIEFING publicado era de
  v0.8.7).
- Eliminado código muerto roto: `GetNearestAirport` (404), `DetectNearestAirport`,
  `MovePilotAsync` (405), `GetAutobrakeName`.
- **Nuevo panel ATC/ATIS en el mapa** (`AtcPanel.cs`) con el texto completo del ATIS y orden
  local primero (`AtcStationOrder.cs`).
- Carta de aproximación: encuadre en metros con `cos(lat)`, FAF unificado y dibujo de tramos
  sin coordenadas (CI/VI/CA/VA/FA/FM/VM) y arcos RF.
- El METAR de llegada vuelve a llegar al LOGBOOK; los slots se rellenan desde el METAR ya
  descargado en vuelo.
- Respaldo regional de TA/TL por `iso_country` cuando NavData no los publica
  (`TransitionDefaults.cs`).
- `GeoMath.cs` como único punto de verdad de la geometría flat-earth.
- `FireAndForget.Run` observa las excepciones antes perdidas; 14 campos muertos y 4 `CS4014`
  corregidos → build sin warnings.

## [0.9.7] — 21/09/2026
- Un fallo transitorio de red ya no borra el METAR en pantalla (solo se reemplaza si la
  descarga tiene éxito).
- El QNH cacheado pasa a tener **TTL de 1 hora**; un valor caducado se descarta y **no
  penaliza**.
- `LoadRoute` con **token de generación**: las cargas obsoletas ya no pisan la ruta, el
  sidebar ni el spinner.

## [0.9.6] — 21/09/2026
- **"PIREP FILED" falso**: el fallback de `/file` decidía con `Status` (código de fase ACARS)
  en vez de `state`. Nuevo `PirepState`/`Pirep.IsActiveState`; un estado ilegible se trata como
  activo (reintentable).
- 13 tests de `PirepStateTests`.

## [0.9.5] — 21/09/2026
- Concurrencia real entre el hilo de polling y las tareas de NavData: `ApproachBuffer` con
  lock e instantáneas, geometría de pista publicada como referencia inmutable
  (`RunwayGeometry`), `_arrivalAirportElevation` con patrón de bits + `Interlocked`, y
  `_effectiveDestination`/`_divertedAirport` volátiles.
- `IsApproachStabilized` usa la misma tabla de velocidades que el gate de scoring (antes
  100–160 kt fijos).
- Documentado que los dos debounces de luces son capas paralelas, no redundantes (sin cambio
  de lógica).

## [0.9.4] — 21/09/2026
- **Nace `vmsOpenAcars.Tests/`** con 132 tests de `ScoringService`, un test por frontera en
  ambos lados.
- Touch-and-go / stop-and-go ya no congelan la máquina de fases en `TaxiIn`.
- Aterrizaje no capturado: centinela `NoLandingData = -1` + `LandingDataCaptured` → criterio
  omitido, calificación "Unknown" y `—` en el LOGBOOK; el centinela nunca llega a phpVMS.
- `App.config` destrackeado de git.

## [0.9.3] — 21/09/2026

### Seguridad
- Credenciales de producción comiteadas: `App.Release.config` con placeholders, `App.config`
  como config local y `AppConfig.cs` sin defaults ⚠️ **las claves deben rotarse**.
- La API key de phpVMS viajaba a `simbrief.com`: cliente dedicado
  `HttpClientProvider.Simbrief` y `HttpClient` fuera de `IApiService`.
- Usuario de SimBrief escapado con `Uri.EscapeDataString`.

### Correcciones
- `FromPirepStatus` y `GetStatusCode` no compartían vocabulario → fase errónea al reanudar
  (`APR` se mapeaba a Approach).
- `SetUpdateIntervalForPhase` era código muerto: los intervalos por fase de `App.config`
  vuelven a aplicarse (cambia el volumen de tráfico hacia phpVMS).
- `ApproachBuffer` solo se limpia si el vuelo se persistió (`SaveFlight` > 0).
- STD pasa a criterio propio (`StdPressureViolation`, −5) y deja de consumir el tope del QNH.
- Corregido `NullReferenceException` al arrancar sin configuración y `navdata_api_domain`
  ahora sí se respeta.
- Documentado: son **17** criterios de scoring, no 14.
