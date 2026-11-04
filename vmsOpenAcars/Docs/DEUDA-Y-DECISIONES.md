# Deuda y decisiones pendientes — vmsOpenAcars

> Movido aquí desde `CLAUDE.md` el 12/10/2026 para liberar el presupuesto de instrucciones del
> agente (el harness truncaba en silencio por el final, que era justo *Próximas áreas*). El
> contenido técnico está **copiado literal** de allí —causa raíz, cifras, rutas, nombres y
> umbrales—; en `CLAUDE.md` queda la lista corta de lo que espera una decisión del mantenedor y
> el puntero a este fichero.

## Estado de «Próximas áreas»

Cerradas en v0.9.8: MetarRaw en el LOGBOOK, fallback regional de TA/TL, panel ATC/ATIS
detallado y los tramos de carta sin coordenadas. Cerrada en v0.9.9: la clase de falso desvío por
alineación casual con un aeródromo de la derrota, con dos casos reales (SKTL en un SKRG→SKBQ y
28M/1B9/KOWD en un SKCG→KBOS). Pendiente por decisión de diseño, no por falta de trabajo:

## SayIntentions.AI — investigación cerrada y nada implementado

- **SayIntentions.AI — investigación cerrada y NADA implementado (07/10/2026).** Todo está en
  `Docs/2026-10-07_VMSOPENACARS_SAYINTENTIONS_sayintentions_01_base-integracion.md` (claves, LVARs,
  `flight.json`, la palanca de anuncios y sus dos matices) y en `Docs/TRANSCRIPCION-ATC.md` (primera
  pieza). Orden acordado: **1) `flight.json` + LVARs al mapa → 2) `getCommsHistory` → 3) arbitraje de
  anuncios**. La documentación buena de SAPI no es la KB: es `portal.sayintentions.ai/p2d/docs/`.

## Base de rutas de rodaje reales en NavData

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

## Campos del PIREP y avisos del RAAS

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
     desde `TelemetryCoordinator`, con la posición y en segundo plano). **Va por `POST /api/pireps/{id}/acars/logs`**,
     que es lo que phpVMS recomienda: el servidor fuerza `type = 2` (LOG) y el aviso **no entra en la**
     **traza de vuelo** —con `type = 0` entraba, que fue el remedio equivocado—.
     Se lee con **`GET /api/pireps/{id}/acars/logs`**. Y **`fuel` sí se guardaba**: lo que fallaba era su serialización
     (corregida en `AcarsRoute`), así que seguimos enviando **libras** y el cliente no cambia.
  De su lado queda **`crossings`** de NavData y nada más de phpVMS: el endpoint global, el
  `activity_log` y un `TXI_OUT`/`TXI_IN` quedaron **descartados**.
  **Receta del corpus, verificada:** `?id=<piloto>&source_name=vmsOpenACars&limit=1000` funciona (0,4 s);
  **`?limit=100` o más SIN filtro devuelve 503** — el filtro es lo que reduce el conjunto, así que
  **siempre con `source_name`**. Auth: **`X-API-KEY`**.

## El grafo y el `node_id`

- **El grafo y el `node_id`: hecho, medido y con el interruptor apagado (v0.9.16).** La identidad por
  id está implementada (por defecto `Suggest(..., useNodeIds = false)`), los **empalmes calculados** de
  NavData entran como aristas, el **umbral de confianza** va en dos niveles (0,5), y desde 0.9.22 el empalme llega al grafo con **su** confianza (`TelemetryCoordinator.JoinToSegment`): hasta entonces entraban todas con 1,0 y el umbral estaba **inerte** y el grafo enruta al
  **borde de plataforma** si el destino cae en otro componente. Medido sobre 37 PIREPs: **31/31 de
  cobertura con las dos políticas**, 14 idénticas, 17 distintas y **+329 m (+15%)** con ids → sigue
  **apagado**. Las tres mediciones del día, con sus correcciones, en **`Docs/architecture.md` →
  "El grafo de rodaje"**. **Ya se puede encender (30/09/2026)**: NavData cerró la conectividad —**7 de 7 con `components_after_joins: 1`**— y desaparece la razón de tenerlo apagado; nuestra medición dice que gana en precisión..

## Tile proxy de NavData

- **Tile proxy en NavData (propuesta del mantenedor, sin empezar)**: que NavData sirva las teselas
  de CARTO con **caché**, para que la clave no viaje a cada piloto y la cuota se divida por el
  número de pilotos en vez de multiplicarse. Resuelve de verdad eso, el 403 por `Referer` y el
  precalentado de los aeropuertos del OFP; **no** elimina el tráfico a CARTO (lo centraliza) y añade
  un punto único de fallo, así que el cliente debe caer a CARTO directo si NavData no responde.
  **Antes de diseñarlo hay que preguntar a CARTO si sus términos permiten cachear y reservir las
  teselas** —si el proxy se considera un servicio de teselas para terceros puede exigir plan
  comercial—. El pedido concreto para el equipo de NavData (endpoint, `style` en lista blanca para
  no ser un proxy abierto, validación de `z/x/y`, TTL y LRU) está en `Docs/PEDIDO-NAVDATA-TILES.md`. **Decidido (30/09/2026): el proxy es el camino** y se adopta en v0.9.18, con caída a CARTO. **Sin clave de CARTO no hay mapa**: responde `API KEY REQUIRED`, no una tesela marcada (medido el 30/09/2026), así que el proxy es lo único que hace funcionar el mapa en la distribución. **Adoptado en v0.9.18**: los dos proveedores piden al proxy y caen a CARTO directo si falla, con `ProxyFallbacks` y `PlaceholderTiles` para reportar.

## SimConnect y datarefs de X-Plane — evaluado y descartado

- **SimConnect y datarefs de X-Plane: evaluado y DESCARTADO — se sigue con FSUIPC/XUIPC**
  (v0.9.16). El mantenedor lo planteó para quitar a los pilotos de MSFS el requisito de instalar
  FSUIPC7, se midió lo que se gana y lo que se pierde, y lo cerró él mismo: «mejor dejémoslo quieto,
  seguimos con fsuipc». La balanza la decidió que el estado de sistemas de un avión complejo
  (PMDG/Fenix) vive en **LVars** que SimConnect a secas no lee —lo hace FSUIPC7 con un WASM dentro
  del simulador—. El análisis completo, con lo que se gana y lo que se pierde por simulador y los
  **disparadores** que reabrirían la decisión, está en **`Docs/architecture.md` → "Capa de
  telemetría"**. No proponer la migración otra vez sin uno de esos disparadores.

## Rumbo verdadero y magnético

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

## Decisiones del mantenedor pendientes

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

## El fin del pushback no está en NavData

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

## Touch-and-go, `landing_rate` y `PirepState`

- **Touch-and-go de entrenamiento (varios ciclos)** — v0.9.4 desbloquea la máquina de fases
  tras un touch/stop-and-go (`TaxiIn` maneja el despegue y `_wasOnGround` se refresca sin
  condiciones) y la segunda aproximación se puntúa desde cero. Lo que **no** está soportado
  es un circuito de varios ciclos: la primera toma ya quedó registrada en el PIREP vía ACARS
  y el `ApproachBuffer` se reinicia, así que solo se conserva el último aterrizaje.
- **`landing_rate = 0` en el payload** — cuando no hubo touchdown capturado, `PirepBuilder`
  normaliza el centinela `NoLandingData` a `0` porque phpVMS espera un número. Si se
  confirmara que acepta `null`, sería más honesto omitir el campo.
- **`PirepState` VERIFICADO (v0.9.56)** (`app/Enums/PirepState.php`): `0 IN_PROGRESS, 1 PENDING, 2 ACCEPTED,
  3 CANCELLED, 4 DELETED, 5 DRAFT, 6 REJECTED, 7 PAUSED`; la tabla del cliente estaba inventada (`Paused=1`).
  **`state` no se manda: lo fija el servidor** (phpVMS, 27/10/2026). Un vuelo a revisión lleva
  `fields.restricted_airspace` (`Helpers/PirepFields.ReviewMark`), y antes se mandaba **2 = aceptado** en
  todos, así que ninguno pasaba por revisión. La marca se arma aunque el OSD esté apagado.

## Deuda estructural

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
