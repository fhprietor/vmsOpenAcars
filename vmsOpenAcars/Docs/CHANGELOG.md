# CHANGELOG — vmsOpenAcars

---

## [0.7.6] — 2026-06-24

### Fixed

- **Hotel Mode — Block Off falso al arrancar Motor 2 en Boarding** — el ATR72-600 y similares
  arrancan el Motor 2 como generador de tierra (Hotel Mode: turbina en marcha, hélice bloqueada)
  antes del vuelo. Esto hacía que `EnginesRunning` pasara a `true`, disparando la lógica de
  Block Off por encendido de motores en la fase Boarding (prevista para aviones sin pushback).
  El sistema registraba el Block Off antes de que el avión se moviera. Corregido añadiendo
  `&& !data.HotelModeActive` al guard de la condición en `FlightManager` (~línea 1766).

- **Hotel Mode — Block On bloqueado en TaxiIn** — con el Motor 2 en Hotel Mode (N1 > 10%),
  `_areEnginesOn` se mantenía `true` indefinidamente, impidiendo que la condición de Block On
  (`!_areEnginesOn` + 90 s detenido) se satisficiera. El Block On no se registraba hasta que
  el Motor 2 se apagaba completamente, lo que podía ser mucho tiempo después del parqueo.
  Corregido cambiando el guard a `(!_areEnginesOn || data.HotelModeActive)` en el case TaxiIn
  de `FlightManager` (~línea 1478).

- **SEND no se deshabilita / CANCEL borra PIREP ya enviado** — bajo ciertas condiciones de red
  (timeout en respuesta HTTP, error transitorio), `FilePirep()` podía devolver `false` aunque
  el PIREP hubiera llegado correctamente a phpVMS. Resultado: `SendPirep()` no tenía rama
  `else`, la UI no se actualizaba (SEND permanecía verde, CANCEL no cambiaba a EXIT), y al
  pulsar CANCEL el sistema borraba el PIREP ya archivado porque `ActivePirepId` seguía activo.
  Tres correcciones:
  1. **`FlightManager.FilePirep()`**: `ActivePirepId` se limpia inmediatamente cuando la API
     confirma éxito, antes de `ResetFlightState()` — así CANCEL nunca puede borrar un PIREP
     ya enviado aunque algo falle después localmente.
  2. **`MainViewModel.SendPirep()`**: añadida rama `else` con mensaje de error visible al
     piloto ("No se pudo enviar el PIREP, verifique la conexión"), y try/catch para excepciones
     inesperadas. En ambos casos SEND queda habilitado para reintento.
  3. **`MainViewModel.OnFlightPhaseChanged()`**: el guard de habilitación de SEND ahora
     verifica `!string.IsNullOrEmpty(_flightManager.ActivePirepId)` — previene que una
     continuación asíncrona tardía re-habilite el botón tras el éxito del envío.

---

## [0.7.5] — 2026-06-15

### Fixed

- **Turboprop — offset TRQ incorrecto** — `FsuipcService` leía el offset `0x2068` (FLOAT64) como "Torque %" cuando en FSUIPC7 ese offset corresponde a **Fuel Flow (lb/hr)**. En aeronaves como el ATR72-600 (PW127M) el flujo en crucero es 500–700 lb/hr, produciendo valores de "torque" de varios cientos de porcentaje. El offset correcto para Torque % es `0x2020` (motor 1) / `0x2120` (motor 2). Corregido en `FsuipcService._eng1TorquePctF64` y `_eng2TorquePctF64`.

- **Hotel Mode — falsa penalización de BEACON** — el ATR72-600 y otros turbohélices soportan **Hotel Mode**: el motor 2 arranca como generador de tierra (turbina en marcha, NH ~65-70%) con la hélice bloqueada, sin que el BEACON sea requerido (las hélices no están girando). vmsOpenAcars penalizaba −5 pts en dos puntos distintos del código:
  1. **Transición ON→OFF→ON** (en `OnRawDataUpdated`, línea de detección `EnginesRunning && !_areEnginesOn`): se evaluaba `!_isBeaconOn` antes de que `_hotelModeActive` se actualizara → siempre penalizaba. Corregido usando `data.HotelModeActive` directamente del paquete de telemetría.
  2. **Loop continuo** (`CheckViolations`): `beaconExempt` ahora incluye `|| _hotelModeActive`.

  **Detección de Hotel Mode** (`FsuipcService`): para categoría Turboprop, `HotelModeActive = true` cuando algún motor tiene N1 > 10% (turbina en marcha) pero `PropRpm < 50` RPM (hélice bloqueada). Se propaga en `RawTelemetryData.HotelModeActive`. En cuanto la hélice comienza a girar (`PropRpm ≥ 50`), hotel mode se desactiva y el beacon vuelve a ser obligatorio.

---

## [0.7.4] — 2026-06-14

### Added

- **Scoring en aeropuerto alterno** — al entrar en fase Approach, el sistema intenta adquirir el umbral de pista primero en el aeropuerto de destino del OFP (`SimbriefPlan.Destination`); si no lo encuentra (el avión está alineado con una pista de otro aeropuerto), reintenta con el alterno del OFP (`SimbriefPlan.Alternate`). Al confirmar el alterno se loguea `"⚠️ Approaching ALTERNATE — XXXX"` y se activa el modo alterno para el resto del vuelo:
  - **TDZ y Centreline**: `LookupRunwayData` usa `_approachDestination` (alterno) en lugar de `_activePlan.Destination` → `FindTouchdownRunway` localiza correctamente la pista en el aeropuerto alterno.
  - **ILS / Localizer / Minimums**: `LoadApproachData` carga los datos de ILS, approach type y fixes del alterno → el gate de 1 000 ft evalúa estos criterios contra la frecuencia y procedimiento reales del alterno.
  - **QNH de llegada**: `FlightManager._effectiveDestination` (nuevo campo) se establece al ICAO del alterno → ambas rutas de check QNH (basada en Transition Level y basada en gate 1 000 ft) llaman `GetWeatherAsync(alterno)` en lugar del destino original → sin falsos positivos por diferencia de QNH entre aeropuertos.
  - Si el aterrizaje ocurre en un aeropuerto que no es ni el destino ni el alterno del OFP, el comportamiento es el existente: TDZ/Centreline/ILS omitidos sin penalización.
- **`MainViewModel._approachDestination`** — nuevo campo `string` que almacena el ICAO resuelto (destino o alterno) desde la adquisición del umbral hasta el touchdown. Se resetea a null al iniciar cada nueva fase Approach; persiste intencionalmente al salir de Approach para que `LookupRunwayData` pueda usarlo tras el touchdown.
- **`FlightManager.SetEffectiveDestination(string)`** — nuevo método público que `MainViewModel` llama cuando detecta el alterno; redirige los dos puntos de check QNH de destino.

---

## [0.7.3] — 2026-06-14

### Changed

- **Overspeed — exención por instrucción ATC (IVAO)** — cuando el piloto tiene COM1 sintonizada en la frecuencia de una estación ATC activa en IVAO (TWR, APP, DEP, CTR…), los eventos de overspeed ya no generan deducción de puntos; la advertencia en el log y el OSD `OVERSPEED  XXX KTS` se mantienen. Si no hay ATC activo o COM1 está en UNICOM (122.8) u otra frecuencia no-ATC, aplican las penalizaciones habituales (0→0 / 1→−7 / ≥2→−15 pts). El desglose del PIREP diferencia eventos penalizados de exentos: `"3 event(s), 1 penalized (ATC exempt: 2)"`.

- **Vapp gate (Stabilized Approach, 1 000 ft AGL) — exención por instrucción ATC** — igual que overspeed: si COM1 está sintonizada en ATC activo IVAO, la deducción de −5 pts por velocidad fuera del rango Vapp queda suprimida, pero el log Warning persiste. Los otros seis sub-criterios del gate (VS, bank, pitch, gear, flaps, ILS) no cambian.

- Implementado mediante delegate `FlightManager.IsOnAtcFrequency` (`Func<bool>`) inyectado por `MainViewModel`. El helper `IsAtcOnCom1()` consulta `FsuipcService.Com1FrequencyMhz` (offset FSUIPC `0x034E` BCD) contra `AirspaceMonitorService.GetAtcStations()` con tolerancia ±0.005 MHz (5 kHz). `FlightManager` mantiene contadores separados `_overspeedCount` (total) y `_overspeedPenaltyCount` (solo penalizados), propagados en `FlightScoreData` al calcular el score.

---

## [0.7.2] — 2026-06-01

### Fixed

- **Touchdown Zone — umbral desplazado (displaced threshold)** — `FindTouchdownRunway` (NavDataService) restaba `NavRunway.OffsetThresholdFt` de la distancia proyectada antes de evaluar la penalización TDZ. Antes, la distancia se medía desde el extremo físico de la superficie pavimentada (`threshold_lat`/`threshold_lon`), ignorando que el umbral legal de aterrizaje puede estar hasta 1 800+ ft más adelante. Ejemplo: KSAM RWY 27 con `offset_threshold_ft = 1810 ft` — un aterrizaje a ~3 140 ft del extremo físico es en realidad a ~1 330 ft del umbral desplazado (dentro de la TDZ, sin penalización). Ahora: `distFt = along × 3.28084 − OffsetThresholdFt`; si `distFt < 0` (toca antes del umbral, en la zona TORA), se fija a 0 sin penalización. Pistas sin umbral desplazado (`OffsetThresholdFt = 0`) no cambian.

---

## [0.7.1] — 2026-06-01

### Added

- **AirspaceMonitorService: alertas predictivas** — `CheckPosition` acepta ahora `headingDeg` y `groundSpeedKts` para proyectar la posición ~3 min hacia adelante (lookahead = min(GS×3/60, 20 NM)):
  - `OnAirspaceApproaching` (nuevo evento): se dispara cuando la posición proyectada cae dentro del polígono y los límites verticales de un espacio Prohibited/Restricted/Danger. Se suprime mientras el ID esté en `_approachingIds`; se cancela automáticamente al entrar al espacio o al girar fuera de la trayectoria. Log: `⚠️ AIRSPACE AHEAD  {TYPE}  {ICAO}  [{lower} – {upper}]`. OSD: `AIRSPACE AHEAD  {TYPE}  {ICAO}` (Warning). No activo si GS < 30 kt.
  - `OnAirspaceOverflight` (nuevo evento): se dispara cuando el avión está lateralmente dentro del polígono pero por encima del límite superior (`altFt > upperFt`). Avisa que la restricción existe bajo él y que no debe descender. Log: `⚠️ OVERFLIGHT  {TYPE}  {ICAO}  [ABOVE {upper}]  DO NOT DESCEND`. OSD: `ABOVE  {ICAO}  DO NOT DESCEND` (Warning).
  - `_approachingIds` y `_overflightIds` — nuevos `HashSet<string>` de supresión; se limpian en `Reset()`.

- **AirspaceMonitorService: límites verticales robustos** — nuevo helper `ParseAltDisplay(string)` parsea el campo textual `NavAirspaceLimit.Display` cuando `ValueFt == null`: `"FL095"` → 9500 ft, `"GND"`/`"SFC"` → 0 ft, cadenas numéricas → ft, `"UNL"` → null (ilimitado). `IsWithinVerticalLimits` usa `ParseAltDisplay` como fallback para ambos límites, eliminando falsas alertas cuando `UpperLimit.ValueFt` es null pero `Display` contiene `"FLxxx"`. Nuevo helper `GetUpperLimitFt(NavAirspace)` para las comprobaciones de sobrevuelo. `OnAirspaceAlert` incluye `[{lower} – {upper}]` en el mensaje de log.

- **Validación de tipo de aeronave contra OFP SimBrief**:
  - `SetActivePlan`: si FSUIPC está conectado y el ICAO del sim difiere del OFP, emite log advisory `⚠️ Aircraft mismatch — Simulator: {simType} / OFP: {planType}` (Theme.Warning).
  - `StartFlight`: diálogo de confirmación bloqueante (YesNo) si los tipos difieren. El piloto puede continuar o cancelar.

### Changed

- **ScoringService: umbrales de Landing Rate** — ≤150 fpm → 0 / ≤250 → −5 / ≤350 → −15 / ≤450 → −25 / ≤650 → −35 / >650 → −40 (antes: ≤100/200/300/400/600/>600). `GetLandingRating` actualizado en consecuencia (etiqueta "Butter" hasta 150 fpm).
- **ScoringService: umbrales de G-Force** — ≤1.5g → 0 / ≤1.7g → −7 / >1.7g → −15 (antes: ≤1.3g/≤1.5g/>1.5g).

---

## [0.7.0] — 2026-05-28

### Added

- **MapForm: transiciones de aproximación** — nuevo combo `Trans.` en el sidebar de destino, inmediatamente debajo del selector de aproximación. Muestra los IAF de entrada (transiciones) del procedimiento activo:
  - `NavApproachTransition` (nuevo DTO en `Models/NavData.cs`): campos `Fix` (nombre del IAF), `FixType`, `FixRegion`, `Type`, `Legs` (`List<NavApproachLeg>`).
  - Propiedad `Transitions` (`List<NavApproachTransition>`) añadida a `NavApproach`.
  - `FillApproachTransCombo(cmb, approach, ref selection)` — puebla el combo con `(none)` + fixes ordenados por nombre; preserva la selección activa si el fix sigue disponible tras cambiar approach.
  - `OnApproachTransChanged` — al cambiar la selección llama `DrawApproachOverlay(app, trans, rwy, ils)`.
  - `DrawApproachOverlay` (firma actualizada) — acepta `NavApproachTransition trans`; si no es null, prepende los legs de la transición a los legs del procedimiento antes de dibujar la polilínea magenta.
  - El combo se limpia y re-puebla al cambiar el approach activo; se resetea a `(none)` al cambiar la pista de destino.

- **Invalidación manual de caché NavData** — botón **REFRESH NAVDATA** en Settings → sección NavData API (junto al botón TEST):
  - `NavDataCache.PurgeAirportData()` — elimina todas las filas de `airport_entries` y `navaid_entries` en la BD SQLite sin tocar `airspace_entries`.
  - `NavDataClient.ClearMemoryCache()` — limpia todos los `ConcurrentDictionary` de aeropuertos/procedimientos en memoria de sesión; sin necesidad de reiniciar la app.
  - Actualiza `lblNavDataStatus` con mensaje de confirmación. La siguiente llamada a `PrefetchAirport(icao)` descarga los datos frescos del API automáticamente.

### Fixed

- **MapForm: filtrado bidireccional SID/STAR ↔ pista** — al cambiar SID o STAR en el sidebar, `GetCompatibleRunways()` filtra el combo de pista de salida/llegada mostrando solo las pistas compatibles con el procedimiento seleccionado. Simetría completa: al cambiar pista, el SID/STAR incompatible sigue mostrando el diálogo de confirmación existente.

- **MapForm: `MatchProcedure` — preselección con nombres de sufijo NavData** — los nombres de procedimiento con designador de transición (ej. `"BIVI3C.01"`) no coincidían con el nombre base SimBrief (ej. `"BIVI3C"`). Añadidos dos pasos de lookup adicional que comparan el nombre truncado al primer punto con el nombre del plan, y viceversa, garantizando la preselección correcta del SID/STAR al cargar el OFP.

- **MapForm: barra de estado — recorte de controles de zoom y capas** — `_lblStatus` tenía `DockStyle.Left` con ancho fijo de 380 px; al reducir el ancho del formulario el label empujaba fuera de vista los controles de zoom (`−`/`+`, dropdown de proveedor) y las checkboxes de capa. Cambiado a `DockStyle.Fill` y añadido al final de la secuencia `Controls.Add` para que tome el espacio residual y los controles `DockStyle.Right` siempre queden visibles.

---

## [0.6.9] — 2026-05-27

### Changed

- **IVAO ATC — filtrado de estaciones para reducir ruido en el log** — el reporte de posiciones IVAO se filtra ahora con tres criterios encadenados antes de mostrarse en el log y en el mapa:
  1. **Suppressión de duplicados consecutivos** — si una estación (mismo callsign, frecuencia, posición y ATIS) no cambió desde el poll anterior, no se muestra. Elimina la repetición de las mismas 3-4 estaciones cada 3 minutos durante crucero.
  2. **Filtro de distancia** — se omiten estaciones cuyo aeropuerto está a más de 150 NM del avión (80 NM en fase Approach/Landing). Las coordenadas de aeropuerto se cachean perezosamente desde `NavDataClient.GetAirportInfo()`. Los matches de prefijo FIR (ej. todos los `SK*`) sin coordenadas en caché pasan sin filtrar — ya están limitados por `_relevantIcaos`.
  3. **Priorización por fase** — en fases Approach y Landing, solo se muestran estaciones del aeropuerto de destino + posiciones APP/DEP de aeropuertos cercanos. Elimina las estaciones del origen y otros aeropuertos de ruta que no son relevantes en la aproximación.

- **`AirspaceMonitorService`** — nuevos campos de estado (`_lastAtcPoll`, `_airportCoordsCache`, `_lastAcLat/Lon`, `_isApproachPhase`, `_destIcao`), métodos `UpdateAircraftState()` (inyectado desde MainViewModel en cada ciclo de telemetría) y `FilterAtcStations()` (aplicado en `PollIvaoAsync` antes de `OnAtcUpdated`). `InitRouteAsync` acepta parámetros opcionales `initLat/initLon` para inicializar la posición en el primer poll.

- **`MainViewModel`** — llamada a `UpdateAircraftState()` cada 30 segundos junto con `CheckPosition()`. Las dos llamadas a `InitRouteAsync` (en `SetActivePlan` y `StartFlight`) ahora pasan la posición actual del avión para que el primer poll IVAO ya tenga coordenadas válidas.

### Resultado esperado

En el vuelo SKRG→MMMX, el log pasó de ~200 líneas de 📻 a un número significativamente menor. En crucero solo aparecen estaciones dentro de 150 NM. En Approach, solo MMMX TWR/GND/APP y APP/DEP cercanos.

---

## [0.6.8] — 2026-05-27

### Added

- **Cartas de aproximación dinámicas** (`ApproachChartForm`) — ventana no-modal 960×720 px, paleta oscura, redimensionable. Se abre desde el botón **"📋 APPROACH CHART"** en el sidebar de destino del mapa.
  - **Plan view (GDI+ north-up):** bounding-box automático de todos los legs, escala flat-earth, extended centerline punteada (5 NM), rectángulo de pista, legs approach (línea sólida blanco/cyan para missed), arcos DME (`AF` leg con `DrawDmeArc`). Símbolos en fixes: triángulo = IAF, círculo verde = FAF, cuadrado cyan = MAP, punto = intermedio. Labels con ident, altitud con descriptor (`+`/`-`/`B`) y velocidad en kt.
  - **Profile view (GDI+ cross-section):** eje X = NM desde threshold (recorrido inverso de `legs[]`); eje Y = ft MSL. Glideslope naranja (`ils_gs`): línea sólida desde FAF a threshold+50 ft usando `NavIls.Glideslope.PitchDeg`. Glidepath verde (`vnav_path`): ángulo de `legs[FafIndex].VerticalAngle`. Advisory gris punteado. Escalera de escalones (`null`/non-precision). DA/MDA línea roja punteada horizontal (último leg con `AltDescriptor="A"`). Ticks de fix con ident y altitud.
  - **Selector de approach:** ComboBox con todos los approaches del aeropuerto, ordenado por pista y tipo; preselecciona el approach activo en el sidebar del mapa.
  - **Header informativo:** nombre de aproximación, ICAO + nombre del aeropuerto, frecuencia ILS o VOR/NDB, elevación pista, TA, TL, ciclo AIRAC.
  - **Carga de datos:** `NavDataClient.PrefetchAirport` + GetApproaches/GetIls/GetRunways/GetAirportInfo; muestra panel "Loading…" mientras carga; reutiliza caché si el aeropuerto ya fue prefetchado en `StartFlight`.
- **`NavApproach`:** campos `ApproachName`, `VerticalGuidance`, `FafIndex`, `Navaid` + propiedad calculada `DisplayName`.
- **`NavApproachLeg`:** campos `FixType`, `FixRegion`, `TurnDirection`, `Rnp`, `SpeedLimitType`, `DmeRadiusNm`, `DmeRadial`, `CenterFix`, `CenterFixRegion`, `CenterLat`, `CenterLon`.
- **`NavNavaid`:** campos `Region`, `VorType`, `NdbType`, `MagVar`.

---

## [0.6.7] — 2026-05-26

### Added

- **Mapa: ATC geográfico estilo WebEye** — las posiciones IVAO se representan como `GMapPolygon` escalados geográficamente (20 nm de radio), reemplazando el dot de 10 px en espacio de pantalla:
  - **TWR** → círculo de radio 20 nm, borde rojo (α 170) + relleno rojo muy transparente (α 30).
  - **GND** → estrella de 4 puntas alineada N/S/E/W, radio exterior 20 nm, radio interior 7.6 nm (0.38×), amarillo.
  - **DEL** → estrella de 4 puntas rotada 45° (puntas NE/SE/SW/NW), mismos radios, naranja.
  - Las puntas de GND y DEL rozan el borde del círculo TWR cuando coexisten (mismo radio 20 nm).
  - `AtcLabelMarker` centrado en ARP: texto ICAO 7 pt Consolas Bold con sombra de 4 px para visibilidad sobre formas de color + punto de 4 px en el centro.
  - Helpers privados en `MapForm`: `MakeCirclePolygon(lat, lon, radiusNm, fill, stroke, n=72)` y `MakeStarPolygon(lat, lon, outerNm, innerRatio, startDeg, fill, stroke)`.
  - APP / CTR / DEP / FSS siguen usando `AtcStationMarker` (text-box) sin cambios.
- **Mapa: capas toggleables en tiempo real** — cuatro `CheckBox` en la barra inferior permiten activar/desactivar capas independientemente sin recargar el mapa:
  - **TILES** — oculta los tiles del proveedor (usa `EmptyProvider`), mantiene la vista y el zoom.
  - **ROUTE** — oculta/muestra la ruta, waypoints, SID/STAR, overlay de aproximación y línea al alterno.
  - **SPACES** — oculta/muestra los polígonos de espacio aéreo (Prohibited/Restricted/Danger/CTR/TMA…).
  - **IVAO** — oculta/muestra las formas geográficas ATC y los `AtcStationMarker` de área.
- **Mapa: icono de aeronave por categoría** — `AircraftMarker` dibuja una silueta diferente según `FsuipcService.AircraftCategory`:
  - Jet (categoría A/B) — silueta de avión de fuselaje estrecho.
  - Turboprop (categoría C) — silueta con motores de hélice más anchos.
  - Piston (categoría D) — silueta de aeronave ligera.
  - Helicopter / Unknown — silueta genérica / flecha.
  - `SetAircraftCategory(FsuipcService.AircraftCategory cat)` en `MapForm`; llamado desde `MainForm` al abrir el mapa y al cambiar el OFP.
- **Mapa: ATC e airspaces visibles sin iniciar vuelo** — `SetActivePlan()` en `MainViewModel` lanza `InitRouteAsync` en segundo plano en cuanto se acepta el OFP, sin necesidad de pulsar START. Al abrir el mapa (`BtnMap_Click`), `MainForm` pre-carga airspaces y estaciones ATC ya disponibles.

### Fixed

- **IVAO ATC filtrado por `_relevantIcaos` — SKRG_TWR y posiciones locales no visibles** — `_relevantIcaos` se construía únicamente a partir de los objetos de espacio aéreo devueltos por NavData; si ningún airspace alrededor del aeropuerto tenía un `ExtractIcao()` que coincidiera exactamente, las posiciones TWR/GND/DEL quedaban filtradas. Corregido añadiendo `originIcao` y `destIcao` explícitamente al set en `InitRouteAsync`, garantizando que el ATC local de salida y llegada siempre pasa el filtro.
- **Debounce de luces — falsos positivos por parpadeo del simulador (~1.6 s)** — el modelo anterior (cooldown 1.5 s: dispara inmediatamente, bloquea repetidos) era sensible a glitches breves del sim que cambiaban el estado de una luz por 1–2 ciclos. Reemplazado por un **hold debounce de 2.5 s**: el nuevo estado debe mantenerse estable durante 2.5 s continuos antes de disparar el evento; cualquier revertido del estado cancela el contador.

### Changed

- **Opacidad de airspaces reducida al 50 %** respecto a v0.6.6 para evitar confusión visual con las formas ATC de IVAO. Valores de ejemplo: Prohibited fill (20,220,0,0) / stroke (95,200,0,0), CTR fill (12,0,180,255) / stroke (70,0,160,230).

---

## [0.6.5] — 2026-05-24

### Added

- **Sidebar de procedimientos en MapForm (estilo Navigraph Maps)** — panel lateral izquierdo que permite cambiar en tiempo real la pista, SID/transición, STAR/transición y aproximación de salida y llegada:
  - Panel colapsable (230 px expandido / 18 px colapsado, botón `◀`/`▶`).
  - Sección **ORIGIN**: label de aeropuerto (ICAO + nombre), selector de pista, SID, transición SID. Chip de viento HW/TW + XW calculado con el METAR en vigor.
  - Sección **DESTINATION**: igual + STAR, transición STAR, aproximación, contador de aproximaciones disponibles para la pista seleccionada.
  - **Validación de compatibilidad** al cambiar pista: si el SID/STAR activo no aplica a la nueva pista se muestra un `EcamDialog` de confirmación; si el usuario rechaza, el combo se revierte. El approach se borra silenciosamente en cualquier cambio de pista destino (siempre runway-specific).
  - **Overlay de aproximación** independiente (`_approachOverlay`, sobre la ruta enroute pero bajo el marcador de avión): trayectoria de legs con coordenadas, extended centerline ±5 NM (semitransparente punteado), missed approach (cian punteado). Color approach = magenta `#FF00C8`, missed = cian `#00C8FF`. No toca `LoadRoute` al cambiar approach.
  - **Chips de viento** actualizados en tiempo real desde `MainForm.UpdateMetarPanel` → `MapForm.SetMetarData()` (índice 0 = ORIG, índice 1 = DEST).
  - **Callback `OnProcedureChanged`** (`Action<string, string, string, string>`) — disparado en cada `RedrawRoute`; `MainForm` lo suscribe y llama `MainViewModel.UpdateProcedureOverrides` para mantener el plan activo sincronizado.
  - `RedrawRoute()` — redibujar la ruta completa con el estado actual del sidebar sin cambiar el par de aeropuertos.
  - `MainViewModel.UpdateProcedureOverrides(originRwy, sidName, destRwy, starName)` — actualiza los campos `OriginRunway`, `SidName`, `DestinationRunway`, `StarName` del plan activo.

### Changed

- **`LoadRoute`** guarda `_currentWaypoints/Icao/AltIcao` para `RedrawRoute`. Las selecciones del sidebar se resetean a los valores del plan de SimBrief solo cuando cambia el par de aeropuertos (no en cada llamada sucesiva de redibujado).
- **`InitMap`** — `_approachOverlay` insertado entre `_waypointOverlay` y `_aircraftOverlay`.

---

## [0.6.4] — 2026-05-24

### Added

- **Recuperación de vuelo activo con historial ACARS, penalizaciones y OFP** — al retomar un PIREP `IN_PROGRESS` tras reiniciar la app, el sistema ahora:
  - **Lee el historial ACARS** de phpVMS (`GET /api/pireps/{id}/acars`) y muestra los últimos 20 registros no-CHK en el log para reconstruir el contexto del vuelo.
  - **Parsea el último checkpoint de penalizaciones** (registro con `status = "CHK"`) y restaura todos los contadores de scoring: overspeed, luces, aproximación inestable, QNH, vuelo offline, salida tardía, velocidades en procedimientos, localizer violations y below minimums. Si no hay checkpoint previo, muestra un OSD de aviso.
  - **Recarga el OFP de SimBrief** — si el usuario tiene `simbrief_user` configurado, descarga automáticamente el último plan activo y lo carga si el origen/destino coincide con el PIREP. El mapa, la ruta y el FMA se populan exactamente igual que al iniciar un vuelo nuevo.
- **Checkpoints de penalizaciones cada 60 s** — mientras hay un vuelo activo, cada 60 segundos se envía automáticamente un registro `AcarsPosition { status = "CHK" }` al servidor phpVMS con el estado actual de todas las penalizaciones en el formato compacto `SC:ov=N,lt=N,sa=N,qnh=N,it=N,od=N,spd=N,lz=N,bm=N,ts=<unix>`. Estos registros son los que la recuperación anterior parsea al retomar el vuelo.
- **Sin límite de tiempo para retomar vuelos** — eliminado el filtro de 20 minutos en `CheckAndResumeFlight`; cualquier PIREP `IN_PROGRESS` (independientemente de cuándo fue la última actualización) se ofrece para retomar.

### Changed

- **`FlightManager`**: añadidas propiedades públicas de solo lectura para todos los contadores de penalizaciones (`OverspeedCount`, `LightsViolationCount`, `StabilizedApproachDeductions`, `QnhViolationCount`, `IsOfflineFlight`, `DepartedLate`, `ProcedureSpdViolations`, `LocalizerViolations`, `BelowMinimums`) y nuevo método `SetResumedPenalties()` para restaurarlos al retomar.
- **`ApiService`**: nuevo método `GetPirepAcarsAsync(pirepId)` — `GET /api/pireps/{id}/acars`, retorna `List<AcarsPosition>`.

---

## [0.6.3] — 2026-05-24

### Fixed

- **Conflicto de versión de System.Data.SQLite en equipos con GAC** — en Windows con software corporativo o de desarrollo instalado (Visual Studio, SQL Server Tools, etc.) el GAC puede contener una versión anterior de `System.Data.SQLite.dll` (p. ej. 1.0.115.5) que entraba en conflicto con la 1.0.119.0 incluida en vmsOpenAcars, produciendo el error `0x80131040` ("La definición del manifiesto del ensamblado no coincide con la referencia"). El `App.config` ya tenía el binding redirect correcto (`0.0.0.0-1.0.119.0 → 1.0.119.0`), pero `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>` en el `.csproj` podía hacer que MSBuild sobreescribiera ese redirect en el `exe.config` de salida si el equipo del desarrollador no tenía SQLite en su propio GAC. Corregido añadiendo `<GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>` al `.csproj`, que preserva los redirects manuales del `App.config` en el output sin que la generación automática los sobreescriba.

  > **Usuarios afectados:** editar manualmente `vmsOpenAcars.exe.config` en la carpeta de instalación y verificar que existe el bloque siguiente dentro de `<runtime><assemblyBinding>`:
  > ```xml
  > <dependentAssembly>
  >   <assemblyIdentity name="System.Data.SQLite" publicKeyToken="db937bc2d44ff139" culture="neutral" />
  >   <bindingRedirect oldVersion="0.0.0.0-1.0.119.0" newVersion="1.0.119.0" />
  > </dependentAssembly>
  > ```
  > Los builds posteriores a v0.6.3 incluyen este redirect de forma fiable.

---

## [0.6.2] — 2026-05-24

### Added

- **Log de sistema en la tabla ACARS de phpVMS al iniciar el vuelo** — al pulsar START y confirmar el inicio del vuelo, se envía automáticamente un `AcarsPositionUpdate` con hasta 4 entradas `log` a la tabla ACARS del servidor phpVMS: sistema operativo + RAM, GPU + VRAM, simulador + versión, y tipo de aeronave / fabricante del add-on. Cada entrada aparece como fila independiente en el historial ACARS del PIREP con `status = "ground"`. El envío es asíncrono (`Task.Run`), no bloquea la interfaz y usa la posición actual del avión como coordenadas de la entrada.
- **Líneas de información de sistema en el log local al arrancar** — al iniciar la aplicación el log muestra: (1) versión de vmsOpenAcars, (2) sistema operativo y RAM, (3) GPU y VRAM. Al conectar FSUIPC se añade una cuarta línea con el simulador y su versión. Todas estas líneas también se incluyen en el campo `notes` del prefile phpVMS.
- **`SystemInfoHelper`** — nueva clase `Helpers/SystemInfoHelper.cs` que recopila información de hardware sin WMI:
  - **OS + RAM**: nombre del SO desde el registro (`ProductName`; detecta Windows 11 por `CurrentBuildNumber >= 22000`) + RAM total vía P/Invoke `GlobalMemoryStatusEx` (kernel32, sin WMI).
  - **GPU**: lectura directa de `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968...}`. Selección por **rango discreto primario** (NVIDIA/GeForce/RTX/GTX/Quadro = 3 · AMD Radeon RX/Pro/Intel Arc = 2 · otros = 1 · Intel integrado = 0); la VRAM es desempate secundario dentro del mismo rango. Garantiza que en portátiles NVIDIA Optimus (GPU discreta como `Render-Only Device` con VRAM = 0 en registro) se muestre la GPU dedicada y no la iGPU Intel. Filtra adaptadores virtuales (Hyper-V, VMware, VirtualBox, Parsec, VDDM, Remote Desktop). VRAM leída como QWORD (8 bytes), soporta tarjetas >4 GB.
  - **Simulador**: `FileVersionInfo` del proceso activo (`FlightSimulator2024`, `FlightSimulator`, `X-Plane`, `Prepar3D`), versión recortada a 3 partes.

---

## [0.6.1] — 2026-05-23

### Added

- **Restricciones de altitud/velocidad en fixes SID/STAR** — los fixes de salida (CLB) y llegada (DSC) que tienen restricciones publicadas en la base de datos NavData muestran ahora debajo de su etiqueta en el mapa:
  - Texto de altitud con las líneas aeronáuticas estándar: línea inferior ("a o superior" `+`), línea superior ("a o inferior" `-`), ambas ("exactamente" `A`/`@`), rango entre dos valores (`B`). Color amarillo cálido `#FFDC78`, fuente 9 pt Consolas. Visible con zoom ≥ 9.
  - Texto de velocidad en kt debajo de la restricción de altitud.
  - Nuevos campos en `NavProcedureLeg`: `Altitude2Ft`, `AltDescriptor`, `SpeedKts`, `SpeedLimitType`.
  - Nueva clase `Models/FixRestriction.cs` con helpers `AltText()`, `SpdText()`, `OsdLine()`.
  - Campo `Restriction` en `SimbriefWaypoint`.
- **OSD de fix próximo** — durante la fase Climb y Descent, cuando el avión se aproxima a ≤ 3 NM de un fix con restricción, aparece un OSD `"PRÓXIMO SIGOX  7000A  250 kts"` (una vez por fix). El log también registra el evento.
- **Scoring: velocidad en procedimientos** — al pasar el fix (≤ 0.5 NM), si la IAS supera el límite publicado en más de 5 kt, se registra una violación. Al enviar el PIREP: −3 pts por violación, cap −10 pts (`Score_CritProcSpeed`).
- **ILS con heading verdadero** — `GetIlsForRunway` usa ahora `loc_true_heading` del endpoint `/airport/{icao}/ils/` (heading TRUE) en lugar de `ils_course` del endpoint de pistas (magnético). Misma clase de error corregida en v0.5.7 para `TrueRunwayBearing`. La DA ahora usa `glideslope.altitude_ft` de la API cuando está disponible, en lugar de `threshold_elevation + 200 ft` constante.
- **Weather desde NavData** — `WeatherService` usa como fuente primaria el endpoint `/weather/{icao}/` de la NavData API (QNH pre-parseado, caché de 5 min) con fallback a aviationweather.gov.
- **is_flyover generalizado** — cualquier fix de SID/STAR con `is_flyover = true` en los legs de NavData recibe tratamiento de fly-over en el mapa (arco Bézier cúbico). Antes solo el primer fix del SID estaba hardcodeado.
- **Ciclo AIRAC en UI** — tras el test de conexión exitoso, si el ciclo AIRAC está expirado se muestra un OSD Warning y se registra en el log con fecha de expiración.
- **Mapa: proveedor Carto Dark** — nuevo proveedor de tiles "Dark (Carto)" (`dark_all`) añadido al combo. Pasa a ser el proveedor por defecto. La preferencia se persiste en `App.config` clave `map_provider_index` y se restaura al abrir el mapa.
- **Mapa: proyección de salida sin SID** — cuando el plan de SimBrief no incluye SID real (ningún fix con `is_sid_star = 1` en la fase CLB), el mapa dibuja la pista física y traza un arco de salida desde el final de pista hasta el primer fix del navlog:
  - Se proyecta un punto de pivote a 3 NM del final de pista en el eje de despegue. Si existe un waypoint publicado entre 2 y 5 NM en esa dirección (alineado con ≤ 25° del eje), se usa ese waypoint como pivote y recibe marcador `apfx` propio.
  - Desde el pivote se traza un arco circular de radio 2.5 NM que gira hasta que la tangente apunta al primer fix del navlog. El arco incluye una recta de tangencia hasta el fix.
  - Función `ComputeDepartureArc`; fallback a `ComputeTransitionCurve` (Bézier cúbico) cuando el fix está muy cerca del arco.
- **Mapa: proyección de llegada sin STAR** — cuando el plan no incluye STAR real, el mapa calcula la pista de llegada desde NavData y construye la llegada visual:
  - Se proyecta el punto `thr-5nm` a 5 NM delante del umbral en el eje de aproximación contrario. El último fix del navlog actúa como fly-over; `BuildSmoothedRoutes` genera la curva de interceptación hacia `thr-5nm`.
  - Tramo físico coloreado `thr-5nm → umbral` con marcadores en ambos extremos.
  - Nueva función `FindArrivalRunway` — selecciona la pista cuyo eje de aproximación es más próximo al bearing del último fix hacia el umbral; respeta `destRunway` cuando SimBrief lo provee.
- **Mapa: waypoint alineado como guía de final** — en dos escenarios, el mapa busca en la caché de waypoints ambient el fix más próximo a 10 NM del umbral alineado con el eje de pista (tolerancia ±20°):
  - *Sin STAR*: el fix encontrado se inserta como punto interior entre el último fix del navlog y `thr-5nm`; `BuildSmoothedRoutes` genera la curva fly-by en ese punto, llegando a `thr-5nm` ya en el eje.
  - *Con STAR desalineada* (último fix de la STAR con diferencia de rumbo > 25° respecto al eje de final): igual — el fix se inserta y hace de interceptor del eje final, añadiéndose también el umbral como endpoint.
  - El fix alineado recibe siempre un marcador `apfx` propio y se excluye del layer ambient.
- **Mapa: waypoints ambient del aeropuerto de origen** — además del destino, `LoadRoute` carga ahora los waypoints ambient del aeropuerto de **origen** y los muestra en el overlay ambient (atenuados), limitados a ≤ 20 NM del aeropuerto. Los fixes que ya tienen marcador explícito en la ruta (fix alineado, waypoint de salida) se excluyen automáticamente para evitar duplicados.
- **Mapa: visibilidad de waypoints ambient según zoom** — el overlay `_ambientOverlay` (navaids y fixes alrededor de origen/destino) solo es visible con zoom ≥ 10. Se aplica en el cambio de zoom (`UpdateZoomInStatus`) y al completar la carga de la ruta.
- **Mapa: anillos de distancia al umbral** — se dibujan dos círculos punteados a 5 NM y 10 NM alrededor del umbral de llegada cuando se detecta la pista de destino (tanto con STAR como sin STAR). Ayudan a estimar distancia al umbral durante la aproximación.
- **Mapa: línea al alterno** — si SimBrief provee aeropuerto alterno, se traza una línea punteada violeta desde el aeropuerto de destino hasta el alterno con marcador `apt`.
- **Mapa: icono de barra de tareas** — la ventana `MapForm` usa el mismo icono `logo.png` que `MainForm` (antes mostraba el icono genérico de Windows).
- **Mapa: redimensionado de ventana** — la ventana del mapa (`FormBorderStyle.None`) puede ahora redimensionarse arrastrando los bordes y esquinas como cualquier ventana normal de Windows. La solución añade `Padding = new Padding(6)` para que la franja de 6 px del borde quede expuesta al `WndProc` de `WM_NCHITTEST` sin ser interceptada por `GMapControl`.
- **NavData caché SQLite persistente** (`NavData_cache.sqlite`) — nueva clase `Services/NavDataCache.cs` que persiste localmente todos los datos estáticos de la API NavData entre sesiones:
  - Tablas: `meta` (ciclo AIRAC y fecha de validez), `airport_entries` (runways, taxiways, approaches, SIDs, STARs, waypoints por ICAO), `navaid_entries` (VOR, NDB, DME). Archivo junto al ejecutable.
  - `NavDataClient` integrado: comprueba la caché antes de cada petición HTTP y almacena tras fetch exitoso. Los datos estáticos (renovados solo con el AIRAC cada 28 días) no se refrescan hasta que el ciclo cambia.
  - Invalidación automática: `SyncAirac(airac, validUntil)` borra las entradas del ciclo anterior en una transacción atómica al detectar cambio de AIRAC. `Initialize()` lee `airac_valid_until`; si la fecha está expirada al arrancar la app, purga toda la caché antes del primer acceso.
  - Ganancia típica: 50–500× más rápido para aeropuertos ya cacheados; ~96 % menos peticiones a NavData API por sesión.
- **DISPATCH: carga condicional** — al abrir `FlightPlannerForm`, solo se cargan los bids del aeropuerto actual. Si no hay bids, se cargan los vuelos disponibles y se activa automáticamente la pestaña "Available Flights". Antes se cargaban ambas fuentes en paralelo siempre.
- **DISPATCH: eliminar bid** — nuevo botón `🗑 DELETE BID` en la pestaña "My Bids" (rojo, deshabilitado hasta seleccionar un bid). Requiere confirmación con diálogo ECAM `"CONFIRM DELETE BID"`. Tras borrar, refresca la lista y si queda vacía activa automáticamente "Available Flights".
- **Botón OFP deshabilitado sin plan** — `btnOfp` arranca `Enabled = false` y se habilita únicamente cuando hay un plan activo (`OnPlanChanged`). Elimina el modal de advertencia que aparecía antes al pulsarlo sin plan.

### Changed

- **`LoadRoute` ampliado** — firma extendida a `LoadRoute(waypoints, originIcao, originRunway, destIcao, destRunway, altIcao, sidName, starName)`. `MainForm` pasa `plan.Origin`, `plan.OriginRunway`, `plan.Destination`, `plan.DestinationRunway`, `plan.Alternate`, `plan.SidName` y `plan.StarName`.
- **Detección de SID/STAR real** — `hasSid` y `hasStar` ya no se basan en el conteo de fixes con `Stage == "CLB"/"DSC"`, sino en que al menos un fix tenga `IsSidStar = true` (campo `is_sid_star` del navlog de SimBrief). Elimina falsos positivos en rutas donde SimBrief usa `Stage = CLB` para todos los waypoints de subida aunque no haya SID publicada.
- **`SimbriefPlan`** — nuevos campos `OriginRunway`, `DestinationRunway`, `SidName`, `StarName` y `Alternate` leídos desde el JSON de SimBrief.
- **`SimbriefWaypoint`** — nuevo campo `IsSidStar` (`is_sid_star == "1"` en el navlog de SimBrief).

### Fixed

- **Cross-thread exception al cerrar la app** — `OsdOverlayForm.ShowMessage()` y `HideOsd()` llamaban `Invoke` (bloqueante) sin verificar `IsDisposed || !IsHandleCreated`. Al cerrar la app con FSUIPC activo, el hilo de telemetría podía disparar un OSD sobre un handle destruido. Corregido: guard `IsDisposed || !IsHandleCreated` + cambio a `BeginInvoke` (no bloqueante).

---

## [0.5.10] — 2026-05-20

### Added

- **Control de volumen en tiempo real** — nuevo `TrackBar` en Settings → Cabin Announcements (rango 0–100 %, default 80 %, pasos de 5). El cambio se aplica **en tiempo real** sobre el audio en reproducción vía `AudioFileReader.Volume` (NAudio). Cadena de propagación: `trkCabinVolume.ValueChanged` → `CabinVolumeChangedCallback` → `MainViewModel.SetCabinVolume()` → `CabinAnnouncementService.SetVolume()` → `_currentReader.Volume`. La clave `cabin_announcements_volume` se persiste **de forma inmediata** en `App.config` en el momento del cambio (auto-save), sin necesidad de pulsar Save.
- **Auto-save en controles de cabina** — el slider de volumen y el toggle "Enabled" se persisten automáticamente al cambiar, mediante `SaveConfigKey()`. No forman parte del ciclo `HasChanges` / `BtnSave`, por lo que mover el slider o activar/desactivar los anuncios **no activa el botón Save ni cierra el diálogo**.

### Changed

- **`TestAnnouncementAsync`** — al seleccionar una nueva fase en el botón `TEST ▾`, ahora detiene el audio en reproducción (`StopCurrent()`) y limpia la cola antes de encolar el nuevo anuncio. Evitaba que fases anteriores continuasen sonando mientras se reproducía la nueva selección.
- **`Reset()`** — también llama a `StopCurrent()` para detener inmediatamente cualquier audio al finalizar o cancelar un vuelo.

---

## [0.5.9] — 2026-05-20

### Added

- **Cabin Announcements** — pregrabados descargados desde la NavData API y reproducidos en cabina durante las fases de vuelo. Al iniciar el vuelo, `CabinAnnouncementService` descarga en paralelo los MP3 de 7 fases (`boarding`, `taxi_out`, `on_runway`, `cruise`, `top_of_descent`, `approach`, `taxi_in`) vía `/briefing/check/` + `/briefing/download/`. Los archivos se cachean en `%TEMP%\vmsacars\briefing\`. Reproducción: chime WAV (NAudio/SoundPlayer) + MP3 en cola FIFO secuencial vía **NAudio** (`AudioFileReader` + `WaveOutEvent`). Vuelos internacionales (ICAO prefix distinto): inglés primero, luego idioma nativo. Vuelos domésticos: solo idioma nativo. Trigger `on_runway`: primer evento `LandingLightChanged(on=true)` o `StrobeLightChanged(on=true)` con GS ≤ 40 kts. Trigger `cruise`: Enroute + AGL > 10 000 ft sostenido 30 s. Idioma nativo: detectado por `airline.country` (phpVMS API) — países hispanohablantes → `es`, resto → `en`. Supresión automática en aeronaves con capacidad < 40 pasajeros (`curr_aircraft.subfleet.total_seats`).
- **Settings — Cabin Announcements** — sección en el panel derecho de Settings con checkbox de activación (efecto inmediato, sin reinicio) y botón `TEST ▾` con dropdown por fase. El test descarga el audio bajo demanda si no está en caché, reproduce chime + MP3, y muestra el resultado debajo del botón: formato real del archivo (`MP3/ID3`, `OGG`, `AAC`…), tamaño en KB y nombre de archivo.
- **Settings — layout apaisado** — rediseño completo de `SettingsForm` de una sola columna (21 filas, ~760 px de alto) a dos columnas lado a lado (920 × 560 px, mínimo 760 × 520 px). Columna izquierda: Conexión + SimBrief + NavData API. Columna derecha: Landing Log + OSD + Cabin Announcements. Los botones Guardar/Cancelar quedan en un panel fijo inferior, siempre visibles.

### Fixed

- **Reproducción MP3** — reemplazado Win32 MCI (`mciSendString` + `type mpegvideo`) por **NAudio 2.3.0** (`AudioFileReader` + `WaveOutEvent` + `ManualResetEventSlim`). MCI fallaba silenciosamente en Windows 11 para streams de audio puro, reproduciendo solo el chime WAV pero no los MP3 de cabina.

---

## [0.5.8] — 2026-05-18

### Added

- **Mapa en movimiento (MAP)** — nuevo botón MAP (reemplaza MSG no utilizado) que abre una ventana no modal `MapForm` con un mapa GMap.NET en tiempo real. El avión se representa como un marcador amarillo con forma de flecha girado por heading. Modos de seguimiento: FOLLOW activa auto-centrado; el piloto puede desactivarlo para explorar el mapa manualmente. Proveedores disponibles: OpenStreetMap (defecto) y ESRI World Imagery (satélite, sin API key). Botones de zoom ±. Barra de estado inferior con coordenadas lat/lon, heading y zoom actual. El mapa se actualiza cada 5 ciclos de telemetría (~250 ms). Paquete NuGet: **GMap.NET.WinForms 17.2.0** y **GMap.NET.Core 17.2.0**.
- **Criterio de transición de calle de rodaje angular** — el cambio de calle ya no se basa únicamente en proximidad temporal. Una vez confirmada una calle (`_lastLoggedTaxiway`), el contador de histéresis sólo avanza si el heading del avión diverge más de **25°** respecto al bearing bidireccional del segmento actual de la calle confirmada (`FindTaxiwaySegmentBearing()`). Esto elimina los falsos cambios por calles paralelas o calles de cruce que momentáneamente están más cerca. Mientras el avión mantenga el rumbo de la calle actual, los segmentos de otras calles más cercanas se ignoran. Se usa la función `HeadingDeltaBidirectional` para tratar la calle como una línea sin dirección (ambos sentidos de rodaje son válidos).

### Changed

- `NavDataService` — nuevo método público `FindTaxiwaySegmentBearing(airport, taxiwayName, lat, lon)` que devuelve el bearing geográfico del segmento más próximo de una calle de rodaje dada (usado por el criterio angular).
- `MainViewModel` — `HandleTaxiPositionUpdate` modificado para usar el criterio angular; nuevo campo `_mapUpdateCounter` y evento `OnMapPositionUpdate` (lat, lon, heading) que dispara cada 5 ciclos de `RawDataUpdated` (~250 ms, independiente de la tasa de telemetría adaptativa). **Fix:** el contador estaba inicialmente en `OnTelemetryUpdated` (tasa adaptativa: 30 s en taxi → mapa se actualizaba cada 150 s); movido a `OnRawDataUpdated`.

---

## [0.5.7] — 2026-05-18

### Fixed

- **`WithinFootprint` y `ProjectOnRunway` usaban heading magnético como eje de proyección geográfica** — el bug idéntico al de v0.5.6 (heading magnético ≠ bearing geográfico verdadero) afectaba a dos rutas de código adicionales: (1) `WithinFootprint`, que proyecta la posición del avión sobre el eje de pista para detectar entradas y backtracks; (2) la proyección final de touchdown en `ProjectOnRunway`, que tras el fix de 0.5.6 usaba el heading verdadero del *avión* en lugar del magnético de NavData, pero ese heading incluye el ángulo de crab de viento en cruce. Caso real: **TJSJ pista 08** (var. −14°W) con el avión rodando por la Calle S a 513 ft del centreline — el código calculaba −90 ft de desviación lateral (footprint 96 ft halfW) y disparaba un falso backtrack. **SKBO pista 14L** (var. −8.5°W), aterrizaje prácticamente en el eje del ILS, reportaba 226 ft de desviación de centreline.
- Solución unificada: nuevo método privado `TrueRunwayBearing(NavRunway)` que calcula el bearing geográfico verdadero a partir de las coordenadas `EndLat/EndLon` → `ThresholdLat/ThresholdLon` de NavData (WGS-84, libres de variación magnética). Se usa en `WithinFootprint` (elimina falsos positivos en footprint check) y en `ProjectOnRunway` (touchdown metrics precisos independientemente del crab angle). El heading magnético de NavData (`rwy.Heading`) se conserva **únicamente** en `HeadingDelta` para la selección de pista, donde el error de ~13° no impacta la clasificación (umbrales 45°/135°).

---

## [0.5.6] — 2026-05-18

### Fixed

- **Desviación de centreline y distancia al umbral incorrectas por heading magnético vs. verdadero** — `ProjectOnRunway` usaba `rwy.Heading` (rumbo magnético publicado en el AIRAC, ej. 220° para la pista 22R de KEWR) como eje de proyección geométrica, pero FSUIPC offset `0x0580` devuelve el heading **verdadero (true)** del avión. La diferencia entre ambos es la variación magnética local; en KEWR (−13°W) eso produce un error de cross-track de `along × sin(13°)` ≈ **183 m (600 ft)** a 800 m del umbral, aunque el avión aterrice en el centro exacto de la pista. Corregido usando el heading verdadero del avión (`heading` ya disponible en el parámetro) en la llamada final a `Project()`. El heading de NavData se conserva para la selección de pista y el footprint check (donde el error angular de ~13° no afecta la desambiguación de pistas paralelas). El fix es universal: aplica a todos los aeropuertos con variación magnética significativa (Europa, Escandinavia, Alaska, América del Norte).

---

## [0.5.5] — 2026-05-18

### Added

- **Log de transiciones de fase** — cada cambio de `FlightPhase` registra una entrada en el log con el nombre de la fase nueva (formato `── FASE ──`), excepto la transición inicial a `Idle`. Implementado en `FlightManager.TransitionTo` con `OnLog?.Invoke`. Las 16 fases no-Idle tienen claves de localización en `es.json` / `en.json` (`Log_PhaseBoarding`, `Log_PhaseTaxiOut`, `Log_PhaseClimb`…).
- **OSD callout `10 000 FT` en climb** — al superar 10 000 ft AGL durante la fase Climb, el OSD muestra `10 000 FT` (Info). Dispara una sola vez por vuelo (`_passing10kFtSent`); se resetea con el vuelo.
- **Botón minimizar** — nueva acción `─` en el header junto al botón de ajustes ⚙️. Llama a `WindowState = FormWindowState.Minimized`. Compatible con la ventana borderless (`FormBorderStyle.None`); respeta el drag-to-reposition.

### Fixed

- **Falso backtrack / entrada a pista en calle de rodaje paralela** — `FindRunwayEntry` podía devolver un positivo en una calle paralela si las coordenadas de umbral o el ancho de pista en NavData eran ligeramente imprecisos (caso real: SKRG CALLE A detectada como backtrack en pista 14L durante un único ciclo de telemetría). Añadido debounce de **2 ciclos consecutivos** (`_pendingRunwayOnCount >= 2`) antes de confirmar presencia en pista, análogo al `_pendingTaxiwayCount >= 3` de los cambios de calle. Un falso positivo de 1 ciclo queda filtrado sin retardo perceptible en la detección legítima (~6 s con actualizaciones SCH).
- **Severidades OSD incorrectas** — tres mensajes de fase tenían severidad inconsistente con la especificación: `TAKEOFF ROLL` (`Warning` → `Info`), `APPROACH` (`Warning` → `Info`), `ON BLOCK` (`Success` → `Info`).

---

## [0.5.4] — 2026-05-17

### Added

- **Alertas sonoras de cabina para mensajes OSD** — cada mensaje OSD dispara ahora un chime de cabina acorde a su severidad: Info → single chime, Success → double chime, Warning → cavalry charge (3 tonos ascendentes Airbus), Critical → master warning burst. Los cuatro sonidos se compilan como `EmbeddedResource` dentro del ejecutable (no requieren archivos sueltos en la instalación). Los players se pre-cargan en memoria al arrancar, por lo que cada reproducción es instantánea y no bloquea el hilo UI.
- **Toggle de chimes en Settings** — nueva fila "Chimes" en la sección OSD de SettingsForm con checkbox "Play cockpit chimes" (activado por defecto). La preferencia se guarda en `App.config` como `osd_sound_enabled` y se lee vía `AppConfig.OsdSoundEnabled`.
- **Botón TEST ▾ en Settings** — botón desplegable junto al checkbox de chimes que permite probar los cuatro tipos de mensaje (Info / Success / Warning / Critical) directamente desde la ventana de ajustes, sin necesidad de simular un vuelo. El chime respeta el estado actual del checkbox aunque el cambio no haya sido guardado todavía. El OSD visual se muestra sobre la pantalla configurada.

### Changed

- **`OsdAudio.Play`** acepta parámetro opcional `forcePlay = false` para que el TEST ignore `AppConfig.OsdSoundEnabled` y use en su lugar el estado del checkbox en tiempo real.
- **Test OSD en menú MENU** — los cuatro items del submenú "Test OSD" disparan ahora también el chime correspondiente (antes solo mostraban el OSD visual).

---

## [0.5.3] — 2026-05-17

### Fixed

- **Distancia en actualizaciones de posición enviada en km en lugar de NM** — `PrepareTelemetry` en `MainViewModel` construía el campo `distance` de `AcarsPosition` con `totalDistanceKm` sin convertir. phpVMS espera millas náuticas; el valor llegaba aproximadamente 1,85× mayor de lo real. Corregido aplicando la conversión `× 0.539957`, consistente con el PIREP final y `UpdateTimerPirep`, que ya usaban NM.
- **QNH de aeropuerto de origen — fallo silencioso en despegue si la red no responde** — `WeatherService.GetQnhMbAsync` hacía siempre un fetch en vivo y devolvía `null` en cualquier error, sin caché. Si la petición HTTP fallaba en el momento del TakeoffRoll (la ventana de QNH de salida), se registraba `⚠️ QNH {ICAO}: no se pudo obtener el METAR` aunque el METAR hubiera sido obtenido exitosamente durante el embarque. Añadida caché estática por ICAO (`ConcurrentDictionary`): cada fetch exitoso persiste el valor; en caso de error (red, timeout, array vacío, altim nulo) se devuelve el último valor cacheado para ese ICAO. Si la caché también está vacía, el comportamiento es idéntico al anterior: devuelve `null` y la penalización no se aplica.
- **A/P ENGAGED falso al despegar** — el contador de debounce `_apEngagedCounter` se acumulaba durante la fase TakeoffRoll (fase de tierra no incluida en el guard de fases). Al producirse el liftoff y transicionar a Takeoff (`isAirbornePhase = true`), el contador ya tenía 6 ciclos y disparaba inmediatamente `A/P ENGAGED` con 0 ft AGL. Corregido reseteando `_apEngagedCounter = 0` al entrar en TakeoffRoll (`CheckProcedureAtPhaseEntry`), de modo que cualquier señal acumulada en tierra queda descartada y el counter debe acumular 6 ciclos frescos una vez en el aire.

---

## [0.5.2] — 2026-05-16

### Added

- **Detección de taxi en single-engine y bonificación +5 pts** — en aeronaves multi-motor, si el piloto rueda con un solo motor durante **≥ 50 % del tiempo de movimiento** en TaxiOut y/o TaxiIn, el sistema otorga automáticamente **+5 puntos** al score final (sin superar 100). La detección usa cuatro contadores de ciclos (`_taxiOut/InMovingCycles`, `_taxiOut/InSingleEngineCycles`) acumulados durante el rodaje. La evaluación ocurre al entrar en TakeoffRoll (TaxiOut) y al detectar OnBlock (TaxiIn). La bonificación solo aplica en aeronaves donde ambos motores corrieron simultáneamente en algún momento del vuelo (`_bothEnginesRunning`), lo que excluye aviones monomotor. El OSD muestra `SINGLE ENGINE TAXI  +5 PTS` (Success) en el momento de la detección.
- **Log de consumo de combustible en fases clave** — tres nuevas entradas en el log de vuelo registran automáticamente el combustible consumido en cada etapa en tierra:
  - Al entrar en **TakeoffRoll**: combustible consumido en taxi-out (bloque off → carrera).
  - Al abandonar pista (**AfterLanding → TaxiIn**): combustible de viaje (carrera de despegue → salida de pista).
  - Al detectar **OnBlock**: combustible consumido en taxi-in (entrada a pista de rodaje → puerta).

### Changed

- **AGL en mensajes de piloto automático** — los mensajes de log `A/P ENGAGED` y `A/P DISENGAGED` incluyen ahora el AGL en el momento del evento. Ejemplo: `🤖 A/P ENGAGED — HDG/ALT  2 340 ft AGL`.

### Fixed

- **Aproximación RNP/visual penalizada por no sintonizar ILS** — cuando una pista tiene ILS en NavData pero el piloto ejecuta una aproximación RNP, visual o de otro tipo, el sistema comprobaba si NAV1 estaba en la frecuencia del ILS y penalizaba −3 pts por no estarlo. Ahora, al cruzar el gate de 1 000 ft AGL, si NAV1 difiere de la frecuencia ILS esperada en más de 0.05 MHz, el criterio completo se omite en silencio: se anulan `_expectedIls` y `_daAltitudeFt`, lo que cancela tanto la verificación de ILS en el gate como los controles de Localizer Alignment y Minimums Compliance por debajo de 500 ft AGL.
- **Piloto automático — falsos positivos con iFly B38M (MSFS 2024)** — al mover cualquier dial del MCP (p. ej. el selector de HDG), el offset `0x07CC` recibía un valor no nulo por 1-2 ciclos de telemetría, lo que disparaba inmediatamente "A/P ENGAGED". Añadido debounce de confirmación: la señal raw debe mantenerse `true` durante **6 ciclos consecutivos (≈ 300 ms)** antes de confirmar el engagement y registrarlo. La desconexión (`DISENGAGED`) sigue siendo inmediata.
- **`ObjectDisposedException` en `FlightPlannerForm`** — al cerrar el planner de vuelo mientras `LoadAllDataAsync()` aún awaita la respuesta de la API, la continuación asíncrona intentaba acceder a un `RichTextBox` ya destruido. Añadida guarda `if (IsDisposed || !IsHandleCreated) return;` al inicio de `AppendLog()`.

### Removed

- **Penalización de 14 pts por NavData no disponible** — se elimina el criterio "LNM Database" (−14 pts aplicados cuando `NavDataClient.IsKeyValid` era `false`). La NavData API es una función premium; aerolíneas virtuales sin acceso pueden seguir obteniendo puntuaciones perfectas. Los criterios Touchdown Zone, Centreline Deviation, Localizer Alignment y Minimums Compliance simplemente no se evalúan cuando los datos no están disponibles, sin penalización adicional.

---

## [0.5.1] — 2026-05-16

### Added

- **Migración NavData API** — la dependencia de la base de datos local de LittleNavMap se reemplaza por un servicio REST alojado (`https://navdata.vholar.co/api/v1/`). El nuevo `NavDataService` (`Services/`) implementa la misma interfaz pública que el anterior `RunwayService`. La geometría flat-earth y la lógica de scoring se conservan intactas.
- **NavDataClient** — cliente HTTP estático con caché por ICAO (`ConcurrentDictionary`). Prefetch paralelo de 6 endpoints por aeropuerto (runways, taxiways, parkings, holdshort, approaches, info). Acceso síncrono thread-safe para uso desde `Task.Run`. Expone `IsReachable` e `IsKeyValid`.
- **NavAirportInfo** — endpoint `/airport/{icao}/` cargado durante el prefetch. Proporciona `transition_altitude_ft` y `transition_level_ft` (ambos `double?`, null cuando el aeropuerto no tiene el dato).
- **Transition Altitude OSD** — al ascender a través de la TA del aeropuerto de origen, el OSD muestra `TRANS ALT  SET STD 1013` (Warning). Dispara una sola vez por vuelo; se resetea en touch-and-go.
- **Transition Level OSD** — al descender a través del TL del aeropuerto de destino, el OSD muestra `TRANS LEVEL  SET QNH` (Warning). Dispara una sola vez por vuelo.
- **Penalización QNH en climb (STD)** — 1 000 ft por encima de la TA, se comprueba si el altímetro está en estándar (1 013 ±2 hPa). Si no, aplica penalización de QNH (−5 pts) con OSD `PENALTY  QNH  −5 PTS`. Comparación directa contra 1 013,25 hPa; no requiere METAR.
- **Diagnóstico de prefetch NavData** — al iniciar el vuelo, `LogNavDataPrefetch` registra en el log el conteo de pistas, calles, gates y aproximaciones por aeropuerto. Si todos los conteos son cero, avisa con `⚠️ NavData {ICAO}: sin datos`.
- **API key por defecto** — si `navdata_api_key` está vacía o ausente, `AppConfig` usa `vhr-1c4c4be385814eed` como fallback; `App.Release.config` la incluye preconfigurada para nuevas instalaciones.
- **Localización completa de mensajes de log** — todos los mensajes hardcodeados en `FlightManager.cs` y `MainViewModel.cs` migrados a claves de localización en `es.json` / `en.json` (70+ claves nuevas). Cubre: luces, fases, scoring, combustible, ILS, IVAO, NavData API, landing log, login, equipamiento del avión y gestión de PIREPs activos.

### Changed

- **QNH de llegada — gate basado en Transition Level** — si NavData provee el TL del destino, la comprobación de QNH de llegada se traslada a TL−1 000 ft MSL. Sin datos de TL, el fallback sigue siendo 1 000 ft AGL en `CheckStabilizedApproachGate`.
- **Validación de API key NavData (dos pasos)** — TEST en SettingsForm y comprobación al iniciar ACARS: (1) `/status/` verifica alcanzabilidad; (2) `/airport/LEMD/runways/` con key verifica validez (401/403 = rechazada). `LnmDbAvailable` se inicializa con `NavDataClient.IsKeyValid`.
- **`GetApproachFixes`** — firma cambiada de `(int approachId)` a `(string airport, string runway)` acorde a la API REST.
- **`RunwayService.cs`** — reducido a solo los tipos resultado (`RunwayTouchdownResult`, `RunwayEntry`, `HoldingPoint`, `ParkingSpot`, `IlsData`, `ApproachInfo`, `ApproachFix`). El código SQLite ha sido eliminado.
- **SettingsForm** — sección "NavMap Database" (SQLite) reemplazada por "NavData API" con label de estado y botón TEST con resultado de conectividad y AIRAC vigente.
- **AppConfig** — claves nuevas: `navdata_api_url`, `navdata_api_key`. Fallback hardcodeado en `??` para garantizar operación sin entrada de configuración.
- **Idioma desbloqueado** — `LocalizationService` respeta la preferencia de idioma configurada en `App.config`. La versión anterior forzaba `es` incondicionalmente.

### Fixed

- **Decodificación BCD de frecuencias de radio (NAV1 / COM1)** — `DecodeNav1Bcd()` usaba la fórmula incorrecta `d3×100 + d2×10 + d1 + d0×0.1`, que leía 110.70 MHz como 107.00 MHz. Fórmula corregida: `100 + d3×10 + d2 + d1×0.1 + d0×0.01`. El formato real de FSUIPC es `(freq − 100) × 100` como número BCD de 4 dígitos. El error provocaba que la comprobación de ILS penalizara con −3 pts incluso con el ILS correctamente sintonizado.
- **Penalización "Below Minimums" falsa positiva** — `_belowMinimums` se activaba al cruzar la DA/DH pero nunca se limpiaba cuando el avión finalmente aterrizaba. Todo aterrizaje normal (que cruza la DA y aterriza) era penalizado. Corregido: `RegisterTouchdown()` establece `_belowMinimums = false`. La penalización solo se aplica si el avión cruzó la DA sin hacer un touchdown posterior.
- **`[[Score_CritMinimums]]` y `[[Score_CritLocalizer]]` en el desglose del score** — ambas claves de localización faltaban en `es.json` y `en.json`. Añadidas: `Score_CritMinimums` y `Score_CritLocalizer`.
- **Criterio "On-Time Departure" sin traducir** — faltaba la entrada en el mapa de criterios → claves de FlightManager. Añadido `"On-Time Departure" → "Score_CritDeparture"`.
- **Endpoints NavData en plural** — todos los fetch usaban la forma `/airports/` (que devolvía cuerpo vacío). Corregidos a la forma singular `/airport/` en los 6 endpoints.

---

## [0.4.17] — 2026-05-15

### Added

- **Detección de taxiway con filtro de rumbo** — `NearestTaxiway()` en `RunwayService` ahora recibe el rumbo del avión. Los segmentos cuyo bearing difiere más de 50° del rumbo del avión reciben una penalización ×2,5 en la distancia efectiva, haciendo que los taxiways transversales (ej. R al cruzar A∩R) no ganen sobre el taxiway alineado. Todos los puntos de llamada públicos (`FindNearestTaxiway`, `FindNextIntersection`) propagan el heading.

- **Histéresis de cambio de taxiway (3 ciclos)** — `HandleTaxiPositionUpdate` en `MainViewModel` requiere 3 detecciones consecutivas del mismo taxiway nuevo antes de registrar el cambio en el log. Los cambios de "next intersection" dentro del mismo taxiway siguen siendo instantáneos. Elimina la oscilación A↔D al pasar por intersecciones y los blips puntuales de taxiways transversales.

### Changed

- **Idioma forzado a español** — `LocalizationService` carga `es.json` incondicionalmente al iniciar, ignorando la preferencia guardada en Settings. El fallback por defecto también cambia de `en.json` a `es.json`.

---

## [0.4.16] — 2026-05-14

### Added

- **Log de inicio — simulador y aeronave** — las dos primeras líneas del log al confirmar el inicio del vuelo ahora muestran el simulador en uso (`🖥️ MSFS 2020`) y el tipo de aeronave con el desarrollador del add-on si se detecta (`✈️ B738  [PMDG]`). La detección de desarrollador (`GetAircraftDeveloper()` en `FsuipcService`) cubre 33 add-ons conocidos (PMDG, ToLiss, Fenix, FlyByWire/A32NX, iniBuilds, Majestic, Leonardo, Zibo, iFly, Carenado, Aerosoft, FlightFactor, Rotate, JustFlight, Headwind, Black Square, etc.).

### Changed

- **Landing lights — tolerancia de 500 ft** — la penalización por landing lights apagadas se activa a **9 500 ft AGL** (antes 10 000 ft). El OSD reminder de aviso previo sigue en 10 500 ft, dejando una ventana de 1 000 ft desde el aviso hasta la penalización.
- **Pitch Angle ideal — ampliado a 7°** — el rango sin deducción pasa de 1°–5° a **1°–7°** nose-up en touchdown. El umbral de flare excesivo (> 8° → −5 pts) no cambia.

---

## [0.4.15] — 2026-05-13

### Added

- **OSD reminder de landing lights a 10 500 ft AGL** — durante la fase **Descent**, al cruzar los 10 500 ft AGL con las landing lights apagadas, el OSD muestra `LANDING LT OFF` (Warning). Dispara una sola vez por descenso (flag `_landingLightReminderSent`); se resetea si el AGL sube por encima de 10 500 ft (go-around) o al iniciar un nuevo vuelo. No aplica penalización — es un aviso previo al check de −5 pts que sigue activo al cruzar los 10 000 ft.

---

## [0.4.14] — 2026-05-12

### Added

- **OSD — penalizaciones en tiempo real** — cada penalización de luces, QNH, overspeed y aproximación no estabilizada genera inmediatamente un mensaje OSD (`Warning` o `Critical`), sin esperar al envío del PIREP. Mensajes: `PENALTY  NAV/TAXI/STROBE/LANDING LT  −5 PTS`, `PENALTY  QNH  −5 PTS`, `OVERSPEED  XXX KTS`, `UNSTABILIZED  −N PTS`.
- **OSD — GO AROUND** — la detección de go-around emite `GO AROUND` (Warning) en el overlay.
- **OSD — TOUCH AND GO** — ya existía en v0.4.6; confirmado que se dispara correctamente desde `FlightManager.OnOsdMessage`.
- **Evento `OnOsdMessage` en FlightManager** — `public event Action<string, OsdSeverity> OnOsdMessage` propagado por `MainViewModel` hacia el overlay OSD existente, sin duplicar lógica.

### Removed

- **Seatbelts — eliminación completa** — eliminado todo el código, offsets FSUIPC (`0x0EC6`), propiedades (`IsSeatBeltSignOn`, `SeatBeltSign`), labels UI (`_lblSeatBelt`), claves de localización (`Status_SeatbeltOn`, `Status_SeatbeltOff`) y referencias en todos los archivos.
- **`Debug.WriteLine` — eliminación completa** — eliminadas todas las llamadas `System.Diagnostics.Debug.WriteLine` (activas y comentadas) en `ApiService`, `FsuipcService`, `FlightManager`, `MainViewModel`, `LandingLogService`, `FlightPlannerForm`, `PhpVmsFlightService`, `SimbriefEnhancedService` y `UIService`. El panel de salida de VS ya no recibe tráfico de depuración en runtime.
- **Entradas `[[...]]` en el log de vuelo** — eliminadas todas las llamadas `OnLog?.Invoke` que usaban claves de localización inexistentes (`Log_BidRemoved`, `Log_InitialFuel`, `Log_TimerStarted`, `Log_AircraftTypeInfo`, `Log_PirepCreated`, `Log_PhaseChanged`, `Log_BlockOffPushback`, `Log_TaxiOutAfterPush`, `Log_ApproachInfo`, y otras). El log solo muestra mensajes con clave válida o hardcoded.

---

## [0.4.13] — 2026-05-11

### Added

- **Localización de logs** — Migración de mensajes hardcodeados en el log (`MainViewModel.cs`) a archivos de idioma dinámicos (`es.json` y `en.json`) usando el servicio de localización existente.

### Changed

- **Idioma por defecto** — La aplicación ahora ignora temporalmente la preferencia de idioma de usuario (`App.config`) y fuerza la carga del archivo de idioma `en.json` (Inglés) al iniciar.

### Fixed

- **Error de compilación CS1061** — Se solucionó un problema en `FlightManager.cs` al acceder a `GearIsDown` en vez de la propiedad correcta `GearDown` definida en `RawTelemetryData`.
- **Lógica duplicada y CS0103** — Se limpió un bloque redundante no localizado en la evaluación `CheckStabilizedApproachGate` de `FlightManager.cs`, el cual incluía la variable no declarada `_isApproachUnstable`.

---

## [0.4.12] — 2026-05-11

### Added

- **Penalización por ausencia de base LNM** — si al iniciar el vuelo no está disponible la base de datos LittleNavMap, se notifica en el log inmediatamente (`⚠️ −14 pts: Base de datos LNM no disponible`) y posteriormente se aplican **14 puntos** fijos de penalización al score durante `FilePirep()` (cubre los criterios TDZ + Centreline que dependen de LNM). La penalización se calcula en `ScoringService` vía el nuevo campo `LnmDbAvailable` en `FlightScoreData`, inicializado desde `RunwayService.IsAvailable` en `StartFlight()`.

---

## [0.4.10] — 2026-05-11

### Fixed

- **Panel METAR congelado en "fetching..."** — dos bugs en cascada: (1) `MetarService.DoFetchAsync` tragaba excepciones y jamás llamaba `OnMetarUpdated` si ocurría cualquier error, dejando la UI indefinidamente en estado Fetching; (2) al corregir (1), fallaba `BeginInvoke` con `TargetParameterCountException` porque `MetarData[]`, al ser asignable a `object[]` por covarianza de arrays, se desempaquetaba como argumentos individuales en `BeginInvoke(Delegate, params object[])` en lugar de como un solo argumento. **Fix:** wrappers `SafeFetch*` independientes + `OnMetarUpdated` en `finally` + `ParseMetarToken` en try/catch + `BeginInvoke(..., new object[] { metars })` para evitar el desempaquetado de array. Añadido evento `OnLog` al servicio.

---

## [0.4.9] — 2026-05-11

### Fixed

- **Detección errónea de pista en aproximación con giros base→final** — `RunwayService.GetRunwayThreshold` rediseñado: ya no acepta cualquier pista con heading delta < 45°. Ahora exige **simultáneamente** heading delta ≤ 15°, desviación lateral al eje extendido ≤ 2 NM, y posición *antes* del umbral. Si ninguna pista cumple, devuelve null y la captura se difiere. En SKCC (RWY 16, llegada con giro 355°→159°) ya no se selecciona la pista 03 durante el giro de base.
- **Captura de aproximación no se reevaluaba** — la adquisición del threshold se hacía una sola vez al entrar en fase Approach, fijando una pista posiblemente incorrecta. Ahora `OnRawDataUpdated` llama a `GetRunwayThreshold` cada ciclo mientras `_approachThreshold == null`, garantizando que se adquiere solo cuando el avión está alineado y lateralmente cercano. Eliminado el bloque de "refinamiento a 5 NM" y el flag `_approachThresholdLocked`, ahora innecesarios.
- **Logs de diagnóstico en `SaveLandingRecord`** — antes fallaba silenciosamente (`Debug.WriteLine` solamente). Ahora emite eventos `OnLog` con detalle: servicio no disponible, buffer insuficiente (<3 puntos), excepción en `SaveFlight`, o éxito con número de vuelo y conteo de puntos.

---

## [0.4.8] — 2026-05-10

### Fixed

- **"Collection was modified" al salir** — race condition en `FsuipcService.Stop()`: el timer de polling se destruía y se llamaba a `Disconnect()` → `FSUIPCConnection.Close()` inmediatamente, sin esperar a que un callback `OnPollingTick` ya en vuelo (ThreadPool) terminara su `FSUIPCConnection.Process()`. La librería FSUIPC itera internamente su lista de offsets con `foreach` en `Process()`; `Close()` la modificaba concurrentemente → `InvalidOperationException: Collection was modified`. **Fix:** spin-wait con `Volatile.Read(ref _isPolling)` antes de `Disconnect()`, usando el flag de reentrancia ya existente.

---

## [0.4.7] — 2026-05-09

### Added

- **Versión en log de inicio de PIREP** — la línea `⏱️ PIREP created at: HH:mm:ss UTC` ahora incluye la versión del cliente: `(v0.4.7)`. La versión se lee desde `AssemblyInformationalVersionAttribute` via `AppInfo.Version`.

### Fixed

- **Pista paralela incorrecta en trayectoria de aproximación** — el threshold de re-evaluación para pistas paralelas bajó de 6 NM a **5 NM**. A 5 NM el avión ya está establecido en final y su posición lateral discrimina correctamente entre 14L/14R (o cualquier par paralelo), eliminando el falso offset de ~4 500 ft en la desviación de centreline del logbook.
- **G-Force log mostraba valor post-impacto ("1.02g Perfect")** — el sensor FSUIPC `0x11BA` ya ha decaído a ~1.0g cuando el evento `TouchdownDetected` se procesa. El log de touchdown ahora usa `GForcePeak` (`Math.Max(_peakGforceApproach, CurrentGForce)`) en lugar de `GForceAtTouch`, mostrando el pico real capturado durante el approach/impacto. La calificación cualitativa (Perfect/Normal/Hard/Crash) usa el mismo valor.

---

## [0.4.6] — 2026-05-08

### Added

- **OSD Overlay** — nueva ventana `OsdOverlayForm` (TopMost, sin borde, sin barra de tareas) que muestra notificaciones en pantalla superpuestas al simulador. Soporta 4 niveles de severidad: Info (azul), Success (verde), Warning (dorado), Critical (rojo parpadeante). Animación de fade-in/fade-out configurable.
- **OSD en fases de vuelo** — mensajes automáticos en cada transición de fase: `TAXI OUT`, `TAKEOFF ROLL`, `CRUISE`, `DESCENDING`, `APPROACH`, `ON BLOCK`, `TOUCH AND GO`.
- **OSD en touchdown** — muestra calificación cualitativa (`BUTTER / SMOOTH / NORMAL LANDING / FIRM LANDING / HARD LANDING / SLAM LANDING`), tasa en fpm y G-force en el momento del aterrizaje.
- **OSD al iniciar vuelo** — `ACARS ACTIVE` (Success) al pulsar START y confirmar inicio.
- **OSD al enviar PIREP** — muestra la puntuación final (`PIREP FILED — SCORE: XX/100`).
- **Botón MENU** — nuevo botón en la barra principal. Abre un menú desplegable con submenú **Test OSD** para probar los 4 niveles de severidad (Info / Success / Warning / Critical) sin necesidad de volar.
- **Configuración OSD en Settings** — nueva sección **OSD** en SettingsForm con:
  - Checkbox `Enable OSD` para activar/desactivar globalmente.
  - `Duration (s)` — tiempo de visualización (1–30 s).
  - `Screen index` — índice de pantalla donde mostrar el OSD (0 = primaria).
  - `Opacity (%)` — opacidad del overlay (10–100 %).
- **App.config — claves OSD:** `osd_enabled` (true), `osd_duration_seconds` (4), `osd_screen_index` (0), `osd_opacity` (90).

### Fixed

- **Botón START no se reactivaba tras enviar PIREP** — `SetActivePlan()` ahora emite `OnButtonStateChanged("START", enabled=true)` al cargar un nuevo plan, reactivando el botón inmediatamente sin esperar al siguiente ciclo de validación.
- **UI bloqueada ~120 s tras aceptar OFP** — `TriggerMetarFetchAsync()` y `DownloadOFPPdfAsync()` se llamaban con `var __ = ...` desde el hilo UI, capturando el `WindowsFormsSynchronizationContext`. Las continuaciones de los 4+ requests HTTP (incluido el doble bucle de `FetchNearestAsync`) se acumulaban en el message pump. Corregido usando `Task.Run(() => ...)` en ambas llamadas para ejecutar toda la cadena async en el ThreadPool sin capturar el contexto UI.
- **OSD mal posicionado en modo ventana del simulador** — la posición solo se calculaba en el constructor. Ahora se recalcula en cada llamada a `ShowMessage()` usando `Screen.Bounds` (área completa de la pantalla) en lugar de `WorkingArea` (que excluye la barra de tareas), garantizando posicionamiento correcto tanto en modo ventana como fullscreen.

---

## [0.4.5] — 2026-05-07

### Added
- **COM1/NAV1 en barra de estado** — dos nuevas pills muestran en tiempo real la frecuencia activa de COM1 (`COM 118.50`) y NAV1 (`NAV 111.30/135`). El curso solo aparece cuando el OBS está configurado (≠ 0°). Offset nuevo: `0x034E` BCD para COM1.
- **Autopilot fallback MSFS** — `apMaster` ahora es `true` si `0x07BC != 0` **ó** `0x07CC != 0`. Los add-ons complejos de MSFS (iFly, PMDG, FBW) no escriben el offset maestro `0x07BC`; el offset de modo de navegación (`0x07CC`) actúa como fallback.

### Fixed
- **Pistas paralelas — identificación errónea** — el threshold de aproximación se capturaba una sola vez al entrar en fase Approach, a veces con el avión a > 10 NM y sin estar alineado. Ahora se re-evalúa automáticamente la primera vez que el buffer detecta distancia < 6 NM; si el resultado es una pista distinta se limpia el buffer y se recarga el procedimiento de ILS/aproximación.
- **Touch-and-go falso por rebote** — un rebote (< 1 s en tierra) disparaba la detección de T&G y transitaba a fase Climb sin retorno. Se añadió un guard mínimo de **5 segundos en tierra** (`_touchdownTimestamp`) antes de aceptar un T&G; los rebotes se ignoran.
- **Cuenta atrás de salida — lógica de colores** — nueva escala: rojo (> 10 min antes) → amarillo (5–10 min) → verde (ventana ±5 min) → amarillo (5–10 min de retraso) → rojo (> 10 min de retraso).
- **Penalización IVAO al iniciar TaxiOut** — el check de IVAO se trasladó al inicio de la fase TaxiOut (independientemente de la fase previa). El diálogo bloqueante de StartFlight se reemplazó por un simple aviso informativo en el log. La penalización de −5 pts se aplica únicamente si el piloto no está en IVAO al comenzar el rodaje.

---

## [0.4.4]

### Added

- **ILS detection** — `RunwayService.GetIlsForRunway(airport, runway)` queries the LittleNavMap DB for the ILS serving the landing runway, filtering LOC-only procedures via `gs_pitch > 0.1`. Returns frequency (MHz), localizer course, glideslope pitch, and threshold position.
- **Approach type identification** — `RunwayService.GetApproachType(airport, runway)` returns the best available approach procedure (ILS > RNAV > other) with an `HasVerticalGuidance` flag. Falls back to `runway_end_id` join when `approach.runway_name` is empty.
- **Approach fix waypoint sequencing** — `RunwayService.GetApproachFixes(approachId)` loads IF, FAF, and MAP fixes from `approach_leg`. During approach, FlightManager logs each fix as the aircraft passes within 0.5 NM.
- **NAV1 frequency reading** — FSUIPC offset `0x0350` (BCD-encoded, `Offset<short>`) decoded to MHz. Offset `0x0C4E` reads the OBS / ILS course. Both fields added to `RawTelemetryData`.
- **ILS tuning check** (at 1000 ft AGL gate) — if an ILS approach was detected, FlightManager verifies that NAV1 is tuned to the correct frequency (±0.05 MHz tolerance). Logs confirmation or warning; non-compliance increments `LocalizerViolations`.
- **Localizer alignment scoring** — monitors aircraft heading vs ILS course below 500 ft AGL. Deviations > 5° are counted (max 2). `ScoringService` deducts up to **−5 pts** combining ILS-not-tuned (−3) and heading deviations (−1 each, max −2).
- **Decision altitude (DA) and minimums check** — DA computed as threshold elevation + 200 ft. If the aircraft descends below DA without landing, `BelowMinimums` is flagged → **−5 pts** in scoring.
- **Approach data loaded in MainViewModel** — `LoadApproachData(airport, runwayName)` runs on `Task.Run` when the Approach phase starts, calling `_flightManager.SetApproachData(ils, approach, fixes)` to wire up all ILS/approach scoring.

### Changed

- `FlightScoreData` gains three new fields: `IlsTunedCorrectly` (bool, default `true`), `LocalizerViolations` (int), `BelowMinimums` (bool). Included in `FilePirep()` score assembly and `Reset()`.
- `ScoringService` gains two new criteria: **Localizer Alignment** (max −5 pts) and **Minimums Compliance** (−5 pts). Max raw deduction sum now 130 pts; final score still clamped to 0.
- `FlightManager.CheckStabilizedApproachGate` extended with ILS tuning verification (criterion 7); QNH check renumbered to criterion 9.
- Touch-and-go reset block extended to clear ILS/approach state for the second circuit.

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
