# Estado del bloque del FLARE — 08/10/2026

Traspaso para continuar el tema del flare en un hilo nuevo. **No es un resumen de la
conversación**: la parte entregada está en `Docs/CHANGELOG.md` (secciones 0.9.31 a 0.9.36, con
la causa raíz y las cifras) y aquí solo se apunta. **Lo que sí vive únicamente aquí es la deuda
de verificación**, que es lo primero que se pierde al cambiar de sesión.

Version al escribir esto: **v0.9.36** · suite **655/655** · idiomas simetricos **460 claves**.

---

## 1. Qué está entregado, y dónde leerlo

| Versión | Qué trajo | Dónde |
|---|---|---|
| 0.9.31 | Closeup del toque en el perfil vertical (pista, umbral, punto de toque, bandas TDZ) | CHANGELOG 0.9.31 |
| 0.9.32 | Traza fina del flare a **10 Hz** (`flare_track`), ventana propia (`FlareAnalysisForm`), eje X del closeup en pies enteros | CHANGELOG 0.9.32 |
| 0.9.33 | La **Y del closeup se autoescala** al tramo visible; el closeup pinta la traza fina cuando existe (la de 2 s como respaldo, avisando cuál usa) | CHANGELOG 0.9.33 |
| 0.9.34 | **La traza fina no se guardaba en ningún vuelo** (se copiaba después del reset): se copian las dos trazas antes del `await` | CHANGELOG 0.9.34 |
| 0.9.35 | **Flaps** y **corte de potencia (N1)** en el análisis; `ChartAxisSafety` para que un gráfico no reviente al pintarse | CHANGELOG 0.9.35 |
| 0.9.36 | **Cabecera de datos** en el gráfico para compartirlo + botón **PNG**; identidad de aeronave **única** para el log y el gráfico | CHANGELOG 0.9.36 |

Módulos del bloque, para situarse rápido: `Helpers/FlareCapturePolicy.cs`, `FlareSampleBuffer`,
`FlareChartLayout`, `CloseupAxis`, `CloseupVerticalAxis`, `CloseupTrackSource`,
`TouchdownCloseupGeometry`, `TouchdownZonePolicy`, `ThresholdToTouchdown`, `PowerCut`,
`FlapSetting`, `FlapTrackSummary`, `LandingHeader`, `AircraftIdentity`, `ChartAxisSafety`;
formularios `UI/Forms/FlareAnalysisForm.cs` y el closeup de `LandingAnalysisForm.cs`;
persistencia en `Services/LandingLogService.cs` (tabla `flare_track` + columnas nuevas en `flights`).

---

## 2. Deuda de verificación (esto es lo importante)

**Verificado con un vuelo real el 09/10/2026: el vuelo 43 (VHR8062, SKCL→SKCC pista 16).** La
traza fina **ya se captura y se guarda**: **97 muestras** en `flare_track` (seq 0–96, todas
distintas), armada a **1 492 ft del umbral** (el armado es a 1 500) y cerrada **2 s después del
toque**, ~**12,1 s** de captura de −1 144 ft a +1 492 ft. El fallo de la 0.9.34 queda cerrado de
verdad: snapshot antes del `await` → `SaveFlareTrack` funciona de extremo a extremo. Y es un caso
duro, ideal para mirar el gráfico: **−530 fpm, 2,4 g, score 43**, touchdown a 1 156 ft, desviación
45 ft.

Punto por punto (antes de esto, todo era una hipótesis):

1. ✅ **Radioaltímetro** (`0x31E4`): **97 valores reales** (12,7–167,2 ft) con el iFly 737 MAX 8.
   El 0→NULL sigue sin ejercitarse (este addon sí publica).
2. ✅ **LTOW**: **150 468,6 lb** (68 252 kg) — ya se le ha visto devolver un peso real, y es
   plausible (SKCL→SKCC es un salto corto, con fuel a bordo).
3. ✅ **Flaps / detent real** (`0x0BFC`): **`flaps_index = 7`** constante → **«FLAPS 30»** sin `≈`,
   y el porcentaje (`0x0BDC` = **87,5 %**, raw 14 335) cae justo en la banda alta de FLAPS 30
   (14 336 es la frontera con FLAPS 40): **las dos señales coinciden**. La tabla 737 queda
   contrastada con una traza real por primera vez.
4. ✅ **Identidad de aeronave con título real** (no reconstruido): `iFly B38M VHR N665VH (178Seat)`,
   `aircraft_model = 737 MAX 8`, `aircraft_icao = B38M`. Las columnas nuevas se pueblan.
5. ⚠️ **Cadencia: es ~8 Hz, no 10 Hz.** 97 muestras en 12,1 s = **126 ms de media** (112–154 ms).
   `FlareInterval` es 100 ms, pero el ciclo de telemetría corre a ~126 ms con el simulador cargado
   (el «20 Hz» de 50 ms es el nominal, no el real). Consecuencia: la distancia de toma se mueve
   **±1 muestra ≈ 32 ft a 151 kt** (no 25 ft), y el «10 Hz» de la doc pasa a «~8 Hz».

**Hallazgos nuevos que la traza destapó** (no estaban en la deuda, hay que decidir):

- **`dist_ft` se colapsa a 0 tras el toque.** Las 16 muestras en tierra (seq 81–96) llevan
  `dist_ft = 0,0` y `agl_ft = 0,0`: `CaptureFlareSample` escribe `distFt ?? 0.0`, y al salir de la
  fase Approach el `_approachThreshold` se pone a `null` (`TelemetryCoordinator` L461) mientras la
  captura **sigue viva** los 2 s posteriores. El margen que captura «la frenada y el morro bajando»
  **no se puede posicionar en la pista**: en el gráfico cae todo en x=0.
  **Arreglado y verificado en vuelo (vuelo 44, SKCC→SKBG):** `FlareDistanceFt` congela las
  coordenadas del umbral al armarse la captura (`FreezeFlareThreshold`) y sigue midiendo contra ese
  umbral tras el toque, aunque `_approachThreshold` se suelte. En el vuelo 44 las 15 muestras en
  tierra ya **no colapsan**: `dist_ft` sigue negativa de −698 a −1196 (~500 ft de rodaje en ~1,9 s,
  coherente con 145 kt frenando). Compila Debug/Release y **655/655**.
- **El flag `on_ground` llega tarde**: el AGL cruza 0 en la muestra 74 (dist −939) pero `on_ground`
  no pasa a 1 hasta la 81 (~0,9 s después). El touchdown registrado (1 156 ft) es coherente con el
  contacto real (más allá de donde el MSL iguala la elevación de campo), pero el detector de toque
  usa una señal distinta al cruce de AGL.
- **La distancia de toma no coincide con la traza — es el umbral desplazado, no un bug (vuelo 44).**
  `touchdown_dist_ft` = **202 ft** y la traza del flare sitúa el contacto a **~698 ft** del umbral:
  **496 ft de diferencia**. Causa raíz: **referencias distintas**. `ProjectOnRunway` (touchdown,
  v0.7.2) mide desde el **umbral legal** de aterrizaje restando `OffsetThresholdFt`
  (`along − offset`), mientras `ComputeApproachMetrics` (el flare) proyecta sobre el **extremo
  físico** (`threshold_lat/lon`) sin restar nada. Los 496 ft son el `offset_threshold_ft` de la
  pista 17 de SKBG. **No** es el detector de toque, ni lo introdujo el fix de `dist_ft`: es un
  desajuste de referencia que ya existía. **Consecuencia**: en el gráfico del flare la marca `TD`
  y las bandas de la TDZ (umbral legal) quedan ~496 ft corridas respecto a la traza (umbral
  físico); y en el dato para el servidor, las dos «distancias al umbral» no serían comparables tal
  cual. **Decisión pendiente**: alinear el flare al umbral legal (restar el offset en
  `FlareDistanceFt`, exige propagar `OffsetThresholdFt` al umbral congelado) o documentar las dos
  referencias y dejar el gráfico como está.

**Sigue pendiente** (este vuelo no lo cubre):

- El respaldo a **300 ft AGL** y el **timeout de 45 s** (este vuelo tenía distancia al umbral, no
  se ejercitaron).
- El **corte de potencia**: el N1 es real (28,8–60,5 %) pero **cae monótono desde el arranque** de
  la captura; falta confirmar qué «corte» reporta el criterio y si es sensato (relativo al pico de
  la aproximación, que cae fuera de los 12 s de la traza).
- La regla que **descarta el `0`** del detent con el mando desplegado (este vuelo dio índice 7, no 0).
- **`ChartAxisSafety`**: sigue sin reproducirse la excepción.

---

## 3. Abiertos del tema (decisiones, no trabajo mecánico)

- **El bloque de texto no sale en el PNG** (es una franja del formulario): para que la imagen se
  entienda sola hay una **identidad compacta en el título del gráfico**. Falta decidir si el bloque
  debe dibujarse **dentro** de la imagen.
- **El pitch** se captura, pero `FlareLayout.HasPitch` puede faltar y el área sale con **SIN DATOS**:
  decidir si se pinta o se retira el área.
- **Ventana del flare vs. closeup**: hoy enseñan lo mismo en dos sitios. Decidir el reparto.
- **La línea técnica** de `TouchdownCloseupGeometry.Summary` mide **1 505 px** y **ya se cortaba
  antes** de todo esto: está **documentada en un test, no arreglada**.
- **`FsuipcService.GetAircraftDeveloper()`** quedó **sin uso** en el cliente (delega en el helper):
  se dejó para no ampliar el cambio.
- **Métricas que se propusieron y NO se implementaron**: tiempo desde 50 ft, recorrido del flare y
  tasa de descenso en el flare. Salen de la misma traza.

---

## 4. Reglas que este bloque ya rompió una vez (no repetirlas)

- **Copiar los buffers ANTES del `await`** en `SendPirep`. Es el fallo de la 0.9.32–0.9.33: el
  reset vacía el buffer del flare y el guardado va después.
- **Un gráfico no puede reventar al pintarse**, y **cualquier volcado a PNG usa `Chart.SaveImage`,
  nunca `DrawToBitmap`** (eso abrió una ventana de excepción en la pantalla del mantenedor).
- **Degradar sin datos**: `NULL` y la línea se omite; nunca un cero que parezca una medida.
- **Nada de etiquetas inventadas**: si no se puede deducir, se enseña el número en crudo y, si es
  aproximado, se marca (`≈`).
- Antes de dar algo por hecho: **build Debug y Release + la suite completa**, y **ningún diálogo en
  pantalla** durante las pruebas.
